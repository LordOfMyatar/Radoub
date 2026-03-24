using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Quartermaster.Services;
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

        // Build temp creature for feat availability and prereq checks (#1800)
        _tempCreature = new UtcFile
        {
            Race = _selectedRaceId,
            ClassList = new List<CreatureClass>
            {
                new CreatureClass { Class = classId, ClassLevel = 1 }
            }
        };

        var expectedInfo = _displayService.Feats.GetExpectedFeatCount(_tempCreature);
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
            if (!_displayService.Feats.IsFeatAvailable(_tempCreature, featId))
                continue;

            // Check prerequisites against current wizard state via FeatService (#1800)
            // In None mode (Chaotic Evil), skip prereq filtering — show all feats
            if (_validationLevel != ValidationLevel.None)
            {
                if (!CheckFeatPrereqsForWizard(featId)) continue;
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

    /// <summary>
    /// Checks feat prerequisites using FeatService with projected wizard state (#1800).
    /// Replaces inline CheckWizardFeatPrereqs — delegates to shared service method.
    /// </summary>
    private bool CheckFeatPrereqsForWizard(int featId)
    {
        int classId = _selectedClassId >= 0 ? _selectedClassId : 0;
        var racialMods = _displayService.GetRacialModifiers(_selectedRaceId);

        var overrides = new FeatPrereqOverrides
        {
            StrOverride = _abilityBaseScores["STR"] + racialMods.Str,
            DexOverride = _abilityBaseScores["DEX"] + racialMods.Dex,
            ConOverride = _abilityBaseScores["CON"] + racialMods.Con,
            IntOverride = _abilityBaseScores["INT"] + racialMods.Int,
            WisOverride = _abilityBaseScores["WIS"] + racialMods.Wis,
            ChaOverride = _abilityBaseScores["CHA"] + racialMods.Cha,
            TotalLevelOverride = 1,
            ClassLevelOverrides = new Dictionary<int, int> { { classId, 1 } },
            SkillRankOverrides = new Dictionary<int, int>(_skillRanksAllocated)
        };

        // Build current feats set from granted + chosen
        var grantedFeats = GetGrantedFeatIds();
        var currentFeats = new HashSet<ushort>();
        foreach (var gf in grantedFeats)
            currentFeats.Add((ushort)gf);
        foreach (var cf in _chosenFeatIds)
            currentFeats.Add((ushort)cf);

        int bab = _displayService.GetClassBab(classId, 1);

        var result = _displayService.Feats.CheckFeatPrerequisites(
            _tempCreature,
            featId,
            currentFeats,
            c => bab,
            cid => _displayService.GetClassName(cid),
            overrides);

        return result.AllMet;
    }

    private void ApplyFeatFilter()
    {
        _filteredAvailableFeats = SkillDisplayHelper.FilterByName(_availableFeats, _featSearchBox?.Text?.Trim());
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
            _featSelectionCountLabel.Foreground = BrushManager.GetWarningBrush(this);
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
                meetsPrereqs = CheckFeatPrereqsForWizard(item.FeatId);
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
        var grantedFeatIds = GetGrantedFeatIds();

        // Use shared service method with FeatService prereq checker (#1800)
        var assigned = _displayService.Feats.AutoAssignFeats(
            _tempCreature,
            classId,
            _selectedPackageId,
            grantedFeatIds,
            _featsToChoose,
            bonusFeatPool: null,
            prereqChecker: CheckFeatPrereqsForWizard);

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
