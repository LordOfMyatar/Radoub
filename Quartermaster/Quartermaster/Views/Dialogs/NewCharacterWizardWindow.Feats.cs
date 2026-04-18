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

        // Build raw list of candidate feat IDs (visible to player in this step)
        var candidateFeatIds = new List<int>();
        foreach (var featId in allFeatIds)
        {
            if (grantedFeatIds.Contains(featId)) continue;
            if (_chosenFeatIds.Contains(featId)) continue;
            if (!_displayService.Feats.IsFeatAvailable(_tempCreature, featId)) continue;
            if (_validationLevel != ValidationLevel.None && !CheckFeatPrereqsForWizard(featId)) continue;
            var featName = _displayService.GetFeatName(featId);
            if (string.IsNullOrEmpty(featName)) continue;
            candidateFeatIds.Add(featId);
        }

        // Group by MASTERFEAT so variants like Weapon Focus (Club/Dagger/...) collapse
        // into a single selectable row (#1734). Singletons remain as-is.
        _availableFeats = new List<FeatDisplayItem>();
        foreach (var group in _displayService.Feats.GroupFeatsByMaster(candidateFeatIds))
        {
            string name;
            FeatCategory category;
            if (group.IsMasterFeat)
            {
                // group.FeatId is a masterfeats.2da row — look up via GetMasterFeatName
                name = _displayService.Feats.GetMasterFeatName(group.FeatId);
                // Category comes from a child feat (all subtypes share the same category)
                category = _displayService.Feats.GetFeatCategory(group.SubtypeIds[0]);
            }
            else
            {
                name = _displayService.GetFeatName(group.FeatId);
                category = _displayService.Feats.GetFeatCategory(group.FeatId);
            }
            if (string.IsNullOrEmpty(name)) continue;

            _availableFeats.Add(new FeatDisplayItem
            {
                FeatId = group.FeatId,
                Name = group.IsMasterFeat ? $"{name} (choose type)" : name,
                CategoryAbbrev = GetFeatCategoryAbbrev(category),
                IsGranted = false,
                MeetsPrereqs = true,
                SourceLabel = "",
                IsMasterFeat = group.IsMasterFeat,
                SubtypeIds = group.IsMasterFeat ? group.SubtypeIds.ToList() : new List<int>()
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
        var allItems = new List<FeatDisplayItem>();

        foreach (var featId in _chosenFeatIds)
        {
            var name = _displayService.GetFeatName(featId);
            if (string.IsNullOrEmpty(name)) continue;
            allItems.Add(new FeatDisplayItem
            {
                FeatId = featId,
                Name = name,
                IsGranted = false,
                SourceLabel = "(chosen)",
                MeetsPrereqs = true
            });
        }

        foreach (var featId in grantedFeatIds)
        {
            var name = _displayService.GetFeatName(featId);
            if (string.IsNullOrEmpty(name)) continue;
            allItems.Add(new FeatDisplayItem
            {
                FeatId = featId,
                Name = name,
                IsGranted = true,
                SourceLabel = "(granted)",
                MeetsPrereqs = true
            });
        }

        // Chosen feats first (player-selected), granted last (auto-granted) — #1883
        var sorted = SkillDisplayHelper.SortSelectedFeats(allItems, f => f.IsGranted, f => f.Name);

        _selectedFeatsListBox.ItemsSource = sorted;
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

    private async void OnAddFeatClick(object? sender, RoutedEventArgs e)
    {
        var selectedItems = _availableFeatsListBox.SelectedItems?
            .OfType<FeatDisplayItem>()
            .Where(f => f.MeetsPrereqs)
            .ToList() ?? new();

        foreach (var item in selectedItems)
        {
            // Only Strict (LG) enforces feat count cap
            if (_validationLevel == ValidationLevel.Strict && _chosenFeatIds.Count >= _featsToChoose) break;

            if (item.IsMasterFeat)
            {
                // #1734: open sub-picker to resolve a subtype feat
                var chosenSubtype = await PromptForFeatSubtypeAsync(item);
                if (chosenSubtype is null) continue;

                if (!_chosenFeatIds.Contains(chosenSubtype.Value))
                {
                    _chosenFeatIds.Add(chosenSubtype.Value);
                    // Remove the entire master group from available (master collapse assumes
                    // a single variant is enough — matches current GAINMULTIPLE UI behavior).
                    _availableFeats.RemoveAll(f => f.FeatId == item.FeatId && f.IsMasterFeat);
                }
                continue;
            }

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

    /// <summary>
    /// Shows the subtype picker for a master feat (#1734). Returns the chosen
    /// subtype's feat ID or null if the user cancelled.
    /// </summary>
    private async System.Threading.Tasks.Task<int?> PromptForFeatSubtypeAsync(FeatDisplayItem master)
    {
        var subtypes = master.SubtypeIds
            .Select(id => new FeatSubtypePickerWindow.SubtypeItem
            {
                FeatId = id,
                Name = _displayService.GetFeatName(id),
                MeetsPrereqs = _validationLevel == ValidationLevel.None || CheckFeatPrereqsForWizard(id),
                AlreadyOwned = _chosenFeatIds.Contains(id)
                    || GetGrantedFeatIds().Contains(id)
            })
            .ToList();

        var masterName = _displayService.Feats.GetMasterFeatName(master.FeatId);
        var picker = new FeatSubtypePickerWindow(masterName, subtypes);
        await picker.ShowDialog(this);
        return picker.Confirmed ? picker.SelectedFeatId : null;
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
        }

        // Rebuild available list so removed subtypes regroup under their master (#1734)
        PrepareStep7();
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
