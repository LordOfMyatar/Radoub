using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Quartermaster.Services;
using Radoub.Formats.Utc;

namespace Quartermaster.Views.Dialogs;

/// <summary>
/// Step 3: Feat selection with prerequisites, bonus feat pools, and auto-assign.
/// </summary>
public partial class LevelUpWizardWindow
{
    #region Step 3: Feat Selection

    // Projected state overrides for feat prereqs in consolidated mode (#1744, #1738)
    private FeatPrereqOverrides? _prereqOverrides;

    private void PrepareStep3()
    {
        // Apply pending ability increments so feat prereqs see projected scores (#1737)
        ApplyAbilityIncrementsToCreature();

        // Calculate projected BAB for feat prereqs in consolidated mode (#1741)
        int currentBab = _displayService.CalculateBaseAttackBonus(_creature);
        int oldClassBab = _displayService.GetClassBab(_selectedClassId, _fromClassLevel - 1);
        int newClassBab = _displayService.GetClassBab(_selectedClassId, _newClassLevel);
        _projectedBab = currentBab + (newClassBab - oldClassBab);

        // Build projected state overrides for class levels and total level (#1744)
        var classLevelOverrides = new Dictionary<int, int>();
        foreach (var cc in _creature.ClassList)
        {
            classLevelOverrides[cc.Class] = cc.Class == _selectedClassId
                ? _newClassLevel
                : cc.ClassLevel;
        }
        // If leveling a new class not yet in ClassList, add it
        if (!classLevelOverrides.ContainsKey(_selectedClassId))
            classLevelOverrides[_selectedClassId] = _newClassLevel;

        int projectedTotalLevel = _creature.ClassList.Sum(c => c.ClassLevel)
            + (_newClassLevel - (_creature.ClassList.FirstOrDefault(c => c.Class == _selectedClassId)?.ClassLevel ?? 0));

        _prereqOverrides = new FeatPrereqOverrides
        {
            ClassLevelOverrides = classLevelOverrides,
            TotalLevelOverride = projectedTotalLevel
        };

        // Resolve default package for auto-assign
        var pkgStr = _displayService.GameDataService.Get2DAValue("classes", _selectedClassId, "Package");
        _resolvedPackageId = (!string.IsNullOrEmpty(pkgStr) && pkgStr != "****" && byte.TryParse(pkgStr, out byte pkgId))
            ? pkgId : (byte)255;

        // Pool feat allocations across all levels in range (#1645)
        _featsToSelect = 0;
        _generalFeatsToSelect = 0;
        _bonusFeatsToSelect = 0;

        for (int lvl = _fromClassLevel; lvl <= _newClassLevel; lvl++)
        {
            var featInfo = _displayService.Feats.GetLevelUpFeatCount(_creature, _selectedClassId, lvl);
            _generalFeatsToSelect += featInfo.GeneralFeats + featInfo.RacialBonusFeats;
            _bonusFeatsToSelect += featInfo.ClassBonusFeats;
        }
        _featsToSelect = _generalFeatsToSelect + _bonusFeatsToSelect;

        // Get the bonus feat pool (union across all levels — usually same pool)
        _bonusFeatPool = _bonusFeatsToSelect > 0
            ? _displayService.Feats.GetClassBonusFeatPool(_selectedClassId)
            : null;

        _selectedFeats.Clear();

        // Show breakdown if multiple sources contribute
        var parts = new List<string>();
        if (_generalFeatsToSelect > 0) parts.Add($"{_generalFeatsToSelect} general");
        if (_bonusFeatsToSelect > 0) parts.Add($"{_bonusFeatsToSelect} class bonus (restricted)");
        var breakdown = parts.Count > 1 ? $" ({string.Join(" + ", parts)})" : "";

        if (_validationLevel == ValidationLevel.None)
            _featAllocationLabel.Text = _featsToSelect > 0
                ? $"You have {_featsToSelect} feat(s) to select{breakdown}. (CE mode: no limit)"
                : "No feats granted at this level. (CE mode: select any feats you want)";
        else
            _featAllocationLabel.Text = _featsToSelect > 0
                ? $"You have {_featsToSelect} feat(s) to select{breakdown}."
                : "No feats to select at this level.";

        // Build the list of available feats
        LoadAvailableFeats();
        ApplyFeatFilter();
        RefreshSelectedFeatsList();

        UpdateFeatSelectionUI();
    }

    private void LoadAvailableFeats()
    {
        _allAvailableFeats.Clear();

        // Get all feats the creature already has
        var existingFeats = new HashSet<int>(_creature.FeatList.Select(f => (int)f));

        // Add any feats we've already selected in this wizard session
        foreach (var selectedFeat in _selectedFeats)
        {
            existingFeats.Add(selectedFeat);
        }

        // Get class feat tables for the selected class and existing classes
        var classFeatIds = new HashSet<int>();
        var selectedClassFeats = GetClassSelectableFeatIds(_selectedClassId);
        foreach (var f in selectedClassFeats)
            classFeatIds.Add(f);

        foreach (var creatureClass in _creature.ClassList)
        {
            var classFeats = GetClassSelectableFeatIds(creatureClass.Class);
            foreach (var f in classFeats)
                classFeatIds.Add(f);
        }

        // Create current feats set (including tentatively selected)
        var currentFeats = new HashSet<ushort>(_creature.FeatList);
        foreach (var sf in _selectedFeats)
            currentFeats.Add((ushort)sf);

        // Get all feat IDs
        var allFeatIds = _displayService.Feats.GetAllFeatIds();

        foreach (var featId in allFeatIds)
        {
            // Skip feats the creature already has — unless GAINMULTIPLE=1 in feat.2da
            if (existingFeats.Contains(featId) && !_displayService.CanFeatBeGainedMultipleTimes(featId))
                continue;

            // Skip level-1-only feats (MaxLevel=1 in feat.2da) — LUW is always level 2+
            var maxLevelStr = _displayService.GameDataService.Get2DAValue("feat", featId, "MaxLevel");
            if (!string.IsNullOrEmpty(maxLevelStr) && maxLevelStr != "****" &&
                int.TryParse(maxLevelStr, out int featMaxLevel) && featMaxLevel == 1)
                continue;

            // Check if feat is available to select (universal or in class table)
            bool isUniversal = _displayService.Feats.IsFeatUniversal(featId);
            bool isClassFeat = classFeatIds.Contains(featId);

            if (!isUniversal && !isClassFeat)
                continue;

            // Get feat info
            var featInfo = _displayService.Feats.GetFeatInfo(featId);

            // Check prerequisites with projected state (#1744)
            var prereqResult = _displayService.Feats.CheckFeatPrerequisites(
                _creature,
                featId,
                currentFeats,
                c => _projectedBab,
                cid => _displayService.GetClassName(cid),
                _prereqOverrides);

            // Strict: must meet prereqs. Warning/None: can select regardless
            bool canSelect = _validationLevel != ValidationLevel.Strict || prereqResult.AllMet;

            _allAvailableFeats.Add(new FeatDisplayItem
            {
                FeatId = featId,
                Name = featInfo.Name,
                Description = featInfo.Description,
                Category = featInfo.Category,
                MeetsPrereqs = prereqResult.AllMet,
                PrereqResult = prereqResult,
                IsClassFeat = isClassFeat && !isUniversal,
                CanSelect = canSelect
            });
        }

        // Sort: selectable first, then by name
        _allAvailableFeats = _allAvailableFeats
            .OrderByDescending(f => f.CanSelect)
            .ThenBy(f => f.Name)
            .ToList();
    }

    private HashSet<int> GetClassSelectableFeatIds(int classId)
    {
        var result = new HashSet<int>();
        var featTable = _displayService.GameDataService.Get2DAValue("classes", classId, "FeatsTable");
        if (string.IsNullOrEmpty(featTable) || featTable == "****")
            return result;

        int rowCount = _displayService.GameDataService.Get2DA(featTable)?.RowCount ?? 300;
        for (int row = 0; row < rowCount; row++)
        {
            var featIndexStr = _displayService.GameDataService.Get2DAValue(featTable, row, "FeatIndex");
            if (string.IsNullOrEmpty(featIndexStr) || featIndexStr == "****")
                break;

            if (int.TryParse(featIndexStr, out int featId))
            {
                var listType = _displayService.GameDataService.Get2DAValue(featTable, row, "List");
                // List = 1: Bonus feat only, 2: Normal selectable, 3: Automatic/granted
                // We want 1 and 2 for selection
                if (listType == "1" || listType == "2")
                {
                    result.Add(featId);
                }
            }
        }

        return result;
    }

    private void ApplyFeatFilter()
    {
        var searchText = _featSearchBox?.Text?.Trim() ?? "";
        bool inBonusPhase = IsSelectingBonusFeat();

        _filteredAvailableFeats = _allAvailableFeats.Where(f =>
        {
            // Don't show already-selected feats — unless GAINMULTIPLE allows re-selection
            if (_selectedFeats.Contains(f.FeatId) && !_displayService.CanFeatBeGainedMultipleTimes(f.FeatId))
                return false;

            // In bonus feat phase, only show feats from the restricted pool
            if (inBonusPhase && _bonusFeatPool != null && !_bonusFeatPool.Contains(f.FeatId))
                return false;

            if (!string.IsNullOrEmpty(searchText) &&
                !f.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                return false;

            return true;
        }).ToList();

        _availableFeatsListBox.ItemsSource = _filteredAvailableFeats;
    }

    private void RefreshSelectedFeatsList()
    {
        var selectedItems = _selectedFeats.Select(featId =>
        {
            var item = _allAvailableFeats.FirstOrDefault(f => f.FeatId == featId);
            return item?.Name ?? _displayService.GetFeatName(featId);
        }).ToList();

        _selectedFeatsListBox.ItemsSource = selectedItems;
    }

    private void UpdateFeatSelectionUI()
    {
        bool isCeMode = _validationLevel == ValidationLevel.None;

        // Show header with count
        if (isCeMode)
        {
            _selectedFeatsHeader.Text = $"Selected Feats ({_selectedFeats.Count}) - CE mode: no limit";
        }
        else if (IsSelectingBonusFeat())
        {
            _selectedFeatsHeader.Text = $"Selected Feats ({_selectedFeats.Count}/{_featsToSelect}) - Bonus Feat (restricted pool)";
        }
        else
        {
            _selectedFeatsHeader.Text = $"Selected Feats ({_selectedFeats.Count}/{_featsToSelect})";
        }

        // Update button states
        var selectedItem = _availableFeatsListBox.SelectedItem as FeatDisplayItem;
        bool canAdd = selectedItem != null && selectedItem.CanSelect;

        // In non-CE mode, enforce feat count limit
        if (canAdd && !isCeMode && _selectedFeats.Count >= _featsToSelect)
            canAdd = false;

        // If in bonus feat phase (non-CE), only allow feats from the bonus pool
        if (canAdd && !isCeMode && IsSelectingBonusFeat() && _bonusFeatPool != null && selectedItem != null)
        {
            canAdd = _bonusFeatPool.Contains(selectedItem.FeatId);
        }

        _addFeatButton.IsEnabled = canAdd;
        _removeFeatButton.IsEnabled = _selectedFeatsListBox.SelectedItem != null;

        ValidateCurrentStep();
    }

    private void OnFeatSearchChanged(object? sender, TextChangedEventArgs e)
    {
        ApplyFeatFilter();
    }

    private void OnAvailableFeatSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (_availableFeatsListBox.SelectedItem is FeatDisplayItem item &&
            !string.IsNullOrWhiteSpace(item.Description))
        {
            _featDescriptionLabel.Text = item.Description;
            _featDescriptionLabel.ClearValue(Avalonia.Controls.TextBlock.ForegroundProperty);
        }
        else
        {
            _featDescriptionLabel.Text = "Select a feat to see its description.";
            _featDescriptionLabel.Foreground = Radoub.UI.Services.BrushManager.GetDisabledBrush(this);
        }

        UpdateFeatSelectionUI();
    }

    private void OnFeatDoubleClicked(object? sender, TappedEventArgs e)
    {
        AddSelectedFeat();
    }

    private void OnSelectedFeatDoubleClicked(object? sender, TappedEventArgs e)
    {
        RemoveSelectedFeat();
    }

    private void OnSelectedFeatSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        UpdateFeatSelectionUI();
    }

    private void OnAddFeatClick(object? sender, RoutedEventArgs e)
    {
        AddSelectedFeat();
    }

    private void OnRemoveFeatClick(object? sender, RoutedEventArgs e)
    {
        RemoveSelectedFeat();
    }

    private void AddSelectedFeat()
    {
        if (_availableFeatsListBox.SelectedItem is not FeatDisplayItem item)
            return;

        if (!item.CanSelect)
            return;

        bool isCeMode = _validationLevel == ValidationLevel.None;

        // In non-CE mode, enforce feat count limit
        if (!isCeMode && _selectedFeats.Count >= _featsToSelect)
            return;

        // Enforce bonus feat pool restriction (non-CE only)
        if (!isCeMode && IsSelectingBonusFeat() && _bonusFeatPool != null && !_bonusFeatPool.Contains(item.FeatId))
            return;

        _selectedFeats.Add(item.FeatId);

        // Re-evaluate prerequisites since selected feat may unlock others
        RefreshFeatPrerequisites();
        ApplyFeatFilter();
        RefreshSelectedFeatsList();
        UpdateFeatSelectionUI();
    }

    private void RemoveSelectedFeat()
    {
        var index = _selectedFeatsListBox.SelectedIndex;
        if (index < 0 || index >= _selectedFeats.Count)
            return;

        _selectedFeats.RemoveAt(index);

        // Re-evaluate prerequisites since removed feat may lock others
        RefreshFeatPrerequisites();
        ApplyFeatFilter();
        RefreshSelectedFeatsList();
        UpdateFeatSelectionUI();
    }

    private void RefreshFeatPrerequisites()
    {
        // Create current feats set including tentatively selected
        var currentFeats = new HashSet<ushort>(_creature.FeatList);
        foreach (var sf in _selectedFeats)
            currentFeats.Add((ushort)sf);

        // Re-check prerequisites for all feats with projected state (#1744)
        foreach (var feat in _allAvailableFeats)
        {
            var prereqResult = _displayService.Feats.CheckFeatPrerequisites(
                _creature,
                feat.FeatId,
                currentFeats,
                c => _projectedBab,
                cid => _displayService.GetClassName(cid),
                _prereqOverrides);

            feat.MeetsPrereqs = prereqResult.AllMet;
            feat.PrereqResult = prereqResult;
            feat.CanSelect = _validationLevel != ValidationLevel.Strict || prereqResult.AllMet;
        }

        // Re-sort: selectable first, then by name
        _allAvailableFeats = _allAvailableFeats
            .OrderByDescending(f => f.CanSelect)
            .ThenBy(f => f.Name)
            .ToList();
    }

    private void OnFeatAutoAssignClick(object? sender, RoutedEventArgs e)
    {
        _selectedFeats.Clear();

        var existingFeats = new HashSet<int>(_creature.FeatList.Select(f => (int)f));
        var currentFeats = new HashSet<ushort>(_creature.FeatList);

        bool CheckPrereqs(int featId)
        {
            var result = _displayService.Feats.CheckFeatPrerequisites(
                _creature, featId, currentFeats,
                c => _projectedBab,
                cid => _displayService.GetClassName(cid),
                _prereqOverrides);
            return result.AllMet;
        }

        // Auto-assign general feats first (unrestricted pool)
        if (_generalFeatsToSelect > 0)
        {
            var generalFeats = _displayService.Feats.AutoAssignFeats(
                _creature, _selectedClassId, _resolvedPackageId,
                existingFeats, _generalFeatsToSelect, null, CheckPrereqs);
            _selectedFeats.AddRange(generalFeats);

            // Add to currentFeats for prereq chain
            foreach (var f in generalFeats)
            {
                existingFeats.Add(f);
                currentFeats.Add((ushort)f);
            }
        }

        // Auto-assign bonus feats (restricted pool)
        if (_bonusFeatsToSelect > 0 && _bonusFeatPool != null)
        {
            var bonusFeats = _displayService.Feats.AutoAssignFeats(
                _creature, _selectedClassId, _resolvedPackageId,
                existingFeats, _bonusFeatsToSelect, _bonusFeatPool, CheckPrereqs);
            _selectedFeats.AddRange(bonusFeats);
        }

        RefreshFeatPrerequisites();
        ApplyFeatFilter();
        RefreshSelectedFeatsList();
        UpdateFeatSelectionUI();
    }

    /// <summary>
    /// Determines if the current feat selection is in the bonus feat phase.
    /// General feats are selected first, then bonus feats from the restricted pool.
    /// </summary>
    private bool IsSelectingBonusFeat()
    {
        return _bonusFeatsToSelect > 0 && _selectedFeats.Count >= _generalFeatsToSelect;
    }

    #endregion
}
