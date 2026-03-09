using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Quartermaster.Services;
using Radoub.Formats.Gff;
using Radoub.Formats.Services;
using Radoub.Formats.Utc;
using Radoub.UI.Services;

namespace Quartermaster.Views.Dialogs;

/// <summary>
/// Step 7: Feat selection with prerequisites and auto-assign.
/// </summary>
public partial class NewCharacterWizardWindow
{
    #region Step 7: Feats

    private void PrepareStep7()
    {
        int classId = _selectedClassId >= 0 ? _selectedClassId : 0;

        // Calculate how many feats the player gets to choose
        // Build a temp creature to use ExpectedFeatCount
        var tempCreature = new UtcFile
        {
            Race = _selectedRaceId,
            ClassList = new List<CreatureClass>
            {
                new CreatureClass { Class = classId, ClassLevel = 1 }
            }
        };

        var expectedInfo = _displayService.Feats.GetExpectedFeatCount(tempCreature);
        _featsToChoose = expectedInfo.TotalExpected;

        // Get granted feats (auto-assigned, not choosable)
        var grantedFeatIds = GetGrantedFeatIds();

        // Get all feats available to this class/race
        var allFeatIds = _displayService.Feats.GetAllFeatIds();

        // Build ability scores for prereq checking
        var racialMods = _displayService.GetRacialModifiers(_selectedRaceId);

        // Build available feats list (excluding granted and already chosen)
        _availableFeats = new List<FeatDisplayItem>();
        foreach (var featId in allFeatIds)
        {
            // Skip granted feats
            if (grantedFeatIds.Contains(featId)) continue;

            // Skip already chosen feats
            if (_chosenFeatIds.Contains(featId)) continue;

            // Check if feat is available to this class
            if (!_displayService.Feats.IsFeatAvailable(tempCreature, featId))
                continue;

            // Check prerequisites against current wizard state
            // In None mode (Chaotic Evil), skip prereq filtering — show all feats
            if (_validationLevel != ValidationLevel.None)
            {
                var prereqs = _displayService.Feats.GetFeatPrerequisites(featId);
                if (!CheckWizardFeatPrereqs(prereqs)) continue;
            }

            var name = _displayService.GetFeatName(featId);
            if (string.IsNullOrEmpty(name)) continue;

            var category = _displayService.Feats.GetFeatCategory(featId);

            _availableFeats.Add(new FeatDisplayItem
            {
                FeatId = featId,
                Name = name,
                CategoryAbbrev = GetFeatCategoryAbbrev(category),
                IsGranted = false,
                MeetsPrereqs = true,
                SourceLabel = ""
            });
        }

        _availableFeats = _availableFeats
            .OrderBy(f => f.Name)
            .ToList();

        ApplyFeatFilter();

        // Build selected feats list (granted + chosen)
        UpdateSelectedFeatsDisplay();
        UpdateFeatSelectionCount();

        // Update description
        var parts = new List<string>();
        if (expectedInfo.BaseFeats > 0) parts.Add($"{expectedInfo.BaseFeats} general");
        if (expectedInfo.RacialBonusFeats > 0) parts.Add($"{expectedInfo.RacialBonusFeats} racial bonus");
        if (expectedInfo.ClassBonusFeats > 0) parts.Add($"{expectedInfo.ClassBonusFeats} class bonus");
        var breakdown = parts.Count > 0 ? $" ({string.Join(" + ", parts)})" : "";
        _featStepDescription.Text = $"Choose {_featsToChoose} feat(s){breakdown}. Granted feats from your race and class are shown as pre-selected.";
    }

    private bool CheckWizardFeatPrereqs(FeatPrerequisites prereqs)
    {
        // No prerequisites at all - always available
        bool hasAny = prereqs.RequiredFeats.Count > 0 ||
                      prereqs.OrRequiredFeats.Count > 0 ||
                      prereqs.MinStr > 0 || prereqs.MinDex > 0 || prereqs.MinCon > 0 ||
                      prereqs.MinInt > 0 || prereqs.MinWis > 0 || prereqs.MinCha > 0 ||
                      prereqs.MinBab > 0 || prereqs.MinSpellLevel > 0 ||
                      prereqs.RequiredSkills.Count > 0 ||
                      prereqs.MinLevel > 0 || prereqs.MaxLevel > 0 ||
                      prereqs.RequiresEpic;
        if (!hasAny) return true;

        // Check ability score prerequisites
        var racialMods = _displayService.GetRacialModifiers(_selectedRaceId);
        int strTotal = _abilityBaseScores["STR"] + racialMods.Str;
        int dexTotal = _abilityBaseScores["DEX"] + racialMods.Dex;
        int conTotal = _abilityBaseScores["CON"] + racialMods.Con;
        int intTotal = _abilityBaseScores["INT"] + racialMods.Int;
        int wisTotal = _abilityBaseScores["WIS"] + racialMods.Wis;
        int chaTotal = _abilityBaseScores["CHA"] + racialMods.Cha;

        if (prereqs.MinStr > 0 && strTotal < prereqs.MinStr) return false;
        if (prereqs.MinDex > 0 && dexTotal < prereqs.MinDex) return false;
        if (prereqs.MinCon > 0 && conTotal < prereqs.MinCon) return false;
        if (prereqs.MinInt > 0 && intTotal < prereqs.MinInt) return false;
        if (prereqs.MinWis > 0 && wisTotal < prereqs.MinWis) return false;
        if (prereqs.MinCha > 0 && chaTotal < prereqs.MinCha) return false;

        // Check BAB (level 1 = BAB from class)
        if (prereqs.MinBab > 0)
        {
            int classId = _selectedClassId >= 0 ? _selectedClassId : 0;
            int bab = _displayService.GetClassBab(classId, 1);
            if (bab < prereqs.MinBab) return false;
        }

        // Check level (wizard is always level 1)
        if (prereqs.MinLevel > 1) return false;
        if (prereqs.MaxLevel > 0 && prereqs.MaxLevel < 1) return false;

        // Check required feats (AND logic) — must have all
        if (prereqs.RequiredFeats.Count > 0)
        {
            var grantedFeats = GetGrantedFeatIds();
            foreach (var reqFeatId in prereqs.RequiredFeats)
            {
                if (!grantedFeats.Contains(reqFeatId) && !_chosenFeatIds.Contains(reqFeatId))
                    return false;
            }
        }

        // Check OR required feats — must have at least one
        if (prereqs.OrRequiredFeats.Count > 0)
        {
            var grantedFeats = GetGrantedFeatIds();
            bool hasOne = prereqs.OrRequiredFeats.Any(id => grantedFeats.Contains(id) || _chosenFeatIds.Contains(id));
            if (!hasOne) return false;
        }

        // Check skill requirements
        if (prereqs.RequiredSkills.Count > 0)
        {
            foreach (var (skillId, minRanks) in prereqs.RequiredSkills)
            {
                int allocated = _skillRanksAllocated.GetValueOrDefault(skillId, 0);
                if (allocated < minRanks) return false;
            }
        }

        // Epic feats not available at level 1
        if (prereqs.RequiresEpic) return false;

        return true;
    }

    private void ApplyFeatFilter()
    {
        var filter = _featSearchBox?.Text?.Trim() ?? "";

        if (string.IsNullOrEmpty(filter))
        {
            _filteredAvailableFeats = new List<FeatDisplayItem>(_availableFeats);
        }
        else
        {
            _filteredAvailableFeats = _availableFeats
                .Where(f => f.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        _availableFeatsListBox.ItemsSource = _filteredAvailableFeats;
    }

    private void UpdateSelectedFeatsDisplay()
    {
        var grantedFeatIds = GetGrantedFeatIds();
        var selectedItems = new List<FeatDisplayItem>();

        // Add granted feats first (read-only)
        foreach (var featId in grantedFeatIds.OrderBy(id => _displayService.GetFeatName(id)))
        {
            var name = _displayService.GetFeatName(featId);
            if (string.IsNullOrEmpty(name)) continue;
            selectedItems.Add(new FeatDisplayItem
            {
                FeatId = featId,
                Name = name,
                IsGranted = true,
                SourceLabel = "(granted)",
                MeetsPrereqs = true
            });
        }

        // Add chosen feats
        foreach (var featId in _chosenFeatIds)
        {
            var name = _displayService.GetFeatName(featId);
            if (string.IsNullOrEmpty(name)) continue;
            selectedItems.Add(new FeatDisplayItem
            {
                FeatId = featId,
                Name = name,
                IsGranted = false,
                SourceLabel = "(chosen)",
                MeetsPrereqs = true
            });
        }

        _selectedFeatsListBox.ItemsSource = selectedItems;
        _selectedFeatCountLabel.Text = $"({_chosenFeatIds.Count} chosen + {grantedFeatIds.Count} granted)";
    }

    private void UpdateFeatSelectionCount()
    {
        int remaining = _featsToChoose - _chosenFeatIds.Count;
        _featSelectionCountLabel.Text = $"{_chosenFeatIds.Count} / {_featsToChoose}";

        if (remaining > 0)
            _featSelectionCountLabel.Foreground = BrushManager.GetWarningBrush(this);
        else if (remaining == 0)
            _featSelectionCountLabel.Foreground = BrushManager.GetSuccessBrush(this);
        else
            _featSelectionCountLabel.ClearValue(TextBlock.ForegroundProperty);
    }

    private bool IsFeatSelectionComplete()
    {
        return _chosenFeatIds.Count >= _featsToChoose;
    }

    private void OnFeatSearchChanged(object? sender, TextChangedEventArgs e)
    {
        ApplyFeatFilter();
    }

    private void OnAddFeatClick(object? sender, RoutedEventArgs e)
    {
        var selectedItems = _availableFeatsListBox.SelectedItems?
            .OfType<FeatDisplayItem>()
            .Where(f => f.MeetsPrereqs)
            .ToList() ?? new();

        foreach (var item in selectedItems)
        {
            // Only Strict (LG) enforces feat count cap
            if (_validationLevel == ValidationLevel.Strict && _chosenFeatIds.Count >= _featsToChoose) break;
            if (!_chosenFeatIds.Contains(item.FeatId))
            {
                _chosenFeatIds.Add(item.FeatId);
                _availableFeats.RemoveAll(f => f.FeatId == item.FeatId);
            }
        }

        ApplyFeatFilter();
        UpdateSelectedFeatsDisplay();
        UpdateFeatSelectionCount();
        ValidateCurrentStep();
    }

    private void OnRemoveFeatClick(object? sender, RoutedEventArgs e)
    {
        var selectedItems = _selectedFeatsListBox.SelectedItems?
            .OfType<FeatDisplayItem>()
            .Where(f => !f.IsGranted) // Can't remove granted feats
            .ToList() ?? new();

        foreach (var item in selectedItems)
        {
            _chosenFeatIds.Remove(item.FeatId);

            // Re-add to available list
            var category = _displayService.Feats.GetFeatCategory(item.FeatId);
            bool meetsPrereqs = true;
            if (_validationLevel != ValidationLevel.None)
            {
                var prereqs = _displayService.Feats.GetFeatPrerequisites(item.FeatId);
                meetsPrereqs = CheckWizardFeatPrereqs(prereqs);
            }

            _availableFeats.Add(new FeatDisplayItem
            {
                FeatId = item.FeatId,
                Name = item.Name,
                CategoryAbbrev = GetFeatCategoryAbbrev(category),
                IsGranted = false,
                MeetsPrereqs = meetsPrereqs,
                SourceLabel = meetsPrereqs ? "" : "(prereqs)"
            });
        }

        _availableFeats = _availableFeats
            .OrderByDescending(f => f.MeetsPrereqs)
            .ThenBy(f => f.Name)
            .ToList();

        ApplyFeatFilter();
        UpdateSelectedFeatsDisplay();
        UpdateFeatSelectionCount();
        ValidateCurrentStep();
    }

    private void OnFeatAutoAssignClick(object? sender, RoutedEventArgs e)
    {
        _chosenFeatIds.Clear();

        int classId = _selectedClassId >= 0 ? _selectedClassId : 0;
        var tempCreature = new UtcFile
        {
            Race = _selectedRaceId,
            ClassList = new List<CreatureClass>
            {
                new CreatureClass { Class = classId, ClassLevel = 1 }
            }
        };

        var grantedFeatIds = GetGrantedFeatIds();

        // Use shared service method with NCW prereq checker
        var assigned = _displayService.Feats.AutoAssignFeats(
            tempCreature,
            classId,
            _selectedPackageId,
            grantedFeatIds,
            _featsToChoose,
            bonusFeatPool: null,
            prereqChecker: featId =>
            {
                var prereqs = _displayService.Feats.GetFeatPrerequisites(featId);
                return CheckWizardFeatPrereqs(prereqs);
            });

        foreach (var featId in assigned)
        {
            _chosenFeatIds.Add(featId);
        }

        // Rebuild available list and re-validate
        PrepareStep7();
        ValidateCurrentStep();
    }

    private void OnAvailableFeatSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_availableFeatsListBox.SelectedItem is FeatDisplayItem feat)
            UpdateFeatDescription(feat.FeatId, feat.Name);
    }

    private void OnSelectedFeatSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_selectedFeatsListBox.SelectedItem is FeatDisplayItem feat)
            UpdateFeatDescription(feat.FeatId, feat.Name);
    }

    private void UpdateFeatDescription(int featId, string name)
    {
        _featDescriptionTitle.Text = name;
        var desc = _displayService.GetFeatDescription(featId);
        _featDescriptionText.Text = !string.IsNullOrEmpty(desc) ? desc : "No description available.";
    }

    private static string GetFeatCategoryAbbrev(FeatCategory category) => category switch
    {
        FeatCategory.Combat => "Cmb",
        FeatCategory.ActiveCombat => "Act",
        FeatCategory.Defensive => "Def",
        FeatCategory.Magical => "Mag",
        FeatCategory.ClassRacial => "C/R",
        FeatCategory.Other => "Oth",
        _ => ""
    };

    #endregion
}
