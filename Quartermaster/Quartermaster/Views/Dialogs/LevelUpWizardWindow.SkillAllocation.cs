using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Quartermaster.Services;
using Radoub.UI.Services;

namespace Quartermaster.Views.Dialogs;

/// <summary>
/// Step 4: Skill point allocation with class/cross-class cost tracking and auto-assign.
/// </summary>
public partial class LevelUpWizardWindow
{
    #region Step 4: Skill Allocation

    // Tracks class skill status for the level being gained
    private HashSet<int> _classSkillIds = new();
    private HashSet<int> _unavailableSkillIds = new();
    private List<SkillDisplayItem> _allSkills = new();

    private void PrepareStep4()
    {
        // Pool skill points across all levels, accounting for INT modifier changes (#1645)
        _skillPointsToAllocate = 0;
        int effectiveInt = _creature.Int;

        // Check if INT was increased in ability step (index 3)
        bool intIncreased = _selectedAbilityIncrease == 3;

        int totalLevel = _creature.ClassList.Sum(c => c.ClassLevel);
        int basePoints = _displayService.GetClassSkillPointBase(_selectedClassId);
        int racialExtra = _displayService.GetRacialExtraSkillPointsPerLevel(_creature.Race);

        for (int i = 1; i <= _levelsToAdd; i++)
        {
            int charLevel = totalLevel + i;

            // Apply INT increase if it happens at this character level
            if (intIncreased && _abilityIncreaseLevels.Contains(charLevel))
                effectiveInt = (byte)System.Math.Min(255, effectiveInt + 1);

            int intMod = CreatureDisplayService.CalculateAbilityBonus(effectiveInt);
            _skillPointsToAllocate += System.Math.Max(1, basePoints + intMod) + racialExtra;
        }

        _skillPointsAdded.Clear();

        // Cache class skills for the level being gained
        _classSkillIds = _displayService.GetClassSkillIds(_selectedClassId);

        // Determine unavailable skills (e.g., Use Magic Device for non-Rogue classes)
        _unavailableSkillIds = _displayService.GetUnavailableSkillIds(_creature, _displayService.GetSkillCount());

        // Display formula breakdown
        int intModBase = CreatureDisplayService.CalculateAbilityBonus(_creature.Int);
        string racialLabel = racialExtra > 0 ? $" + Racial({racialExtra})" : "";
        if (_levelsToAdd > 1)
            _skillPointsTotalLabel.Text = $"({_levelsToAdd} levels × ~{basePoints} + INT{racialLabel} = {_skillPointsToAllocate})";
        else
            _skillPointsTotalLabel.Text = $"(Base {basePoints} + INT {intModBase}{racialLabel} = {_skillPointsToAllocate})";
        UpdateSkillPointsDisplay();

        // Clear search filter
        _skillSearchBox.Text = "";

        // Build skill list
        _allSkills = BuildSkillList();
        ApplySkillFilter();
    }

    private List<SkillDisplayItem> BuildSkillList()
    {
        var skills = new List<SkillDisplayItem>();
        var successBrush = BrushManager.GetSuccessBrush(this);
        var defaultBrush = (this.TryFindResource("SystemControlForegroundBaseHighBrush", this.ActualThemeVariant, out var res) && res is Avalonia.Media.IBrush b)
            ? b : Avalonia.Media.Brushes.Black; // theme-ok: fallback only if resource lookup fails

        int skillCount = _displayService.GetSkillCount();
        for (int i = 0; i < skillCount; i++)
        {
            int currentRanks = i < _creature.SkillList.Count ? _creature.SkillList[i] : 0;
            bool isClassSkill = _classSkillIds.Contains(i);
            bool isUnavailable = _unavailableSkillIds.Contains(i);

            skills.Add(new SkillDisplayItem
            {
                SkillId = i,
                Name = _displayService.GetSkillName(i),
                CurrentRanks = currentRanks,
                AddedRanks = _skillPointsAdded.GetValueOrDefault(i, 0),
                IsClassSkill = isClassSkill,
                IsUnavailable = isUnavailable,
                MaxRanks = isUnavailable ? 0 : CalculateMaxRanks(isClassSkill),
                Cost = isClassSkill ? 1 : 2,
                NameBrush = SkillDisplayHelper.ShouldUseClassSkillColor(isClassSkill, isUnavailable) ? successBrush : defaultBrush
            });
        }

        // Sort: class skills first, then by name
        return skills.OrderByDescending(s => s.IsClassSkill).ThenBy(s => s.Name).ToList();
    }

    private void ApplySkillFilter()
    {
        var filter = _skillSearchBox?.Text?.Trim() ?? "";

        if (string.IsNullOrEmpty(filter))
        {
            _skillsItemsControl.ItemsSource = _allSkills;
        }
        else
        {
            _skillsItemsControl.ItemsSource = _allSkills
                .Where(s => s.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
    }

    private void OnSkillSearchChanged(object? sender, TextChangedEventArgs e)
    {
        ApplySkillFilter();
    }

    private int CalculateMaxRanks(bool isClassSkill)
    {
        int totalLevel = _creature.ClassList.Sum(c => c.ClassLevel) + _levelsToAdd;
        return LevelUpApplicationService.CalculateMaxSkillRanks(isClassSkill, totalLevel);
    }

    private int GetRemainingSkillPoints() =>
        LevelUpApplicationService.CalculateRemainingSkillPoints(
            _skillPointsToAllocate, _skillPointsAdded, _classSkillIds);

    private void UpdateSkillPointsDisplay()
    {
        _skillPointsRemainingLabel.Text = $"Points remaining: {GetRemainingSkillPoints()}";
        ValidateCurrentStep();
    }

    private void OnSkillIncrease(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is int skillId)
        {
            bool isClassSkill = _classSkillIds.Contains(skillId);
            int cost = isClassSkill ? 1 : 2;
            int remaining = GetRemainingSkillPoints();

            // In CE mode, bypass point cost validation
            if (_validationLevel == ValidationLevel.None || remaining >= cost)
            {
                int currentAdded = _skillPointsAdded.GetValueOrDefault(skillId, 0);
                int currentRanks = skillId < _creature.SkillList.Count ? _creature.SkillList[skillId] : 0;
                int maxRanks = _validationLevel == ValidationLevel.None
                    ? 255 // CE mode: no rank cap
                    : CalculateMaxRanks(isClassSkill);

                // Check if we can add another rank
                if (currentRanks + currentAdded < maxRanks)
                {
                    _skillPointsAdded[skillId] = currentAdded + 1;
                    _allSkills = BuildSkillList();
                    ApplySkillFilter();
                    UpdateSkillPointsDisplay();
                }
            }
        }
    }

    private void OnSkillDecrease(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is int skillId)
        {
            if (_skillPointsAdded.GetValueOrDefault(skillId, 0) > 0)
            {
                _skillPointsAdded[skillId]--;
                if (_skillPointsAdded[skillId] == 0)
                    _skillPointsAdded.Remove(skillId);
                _allSkills = BuildSkillList();
                ApplySkillFilter();
                UpdateSkillPointsDisplay();
            }
        }
    }

    private void OnSkillAutoAssignClick(object? sender, RoutedEventArgs e)
    {
        int totalLevel = _creature.ClassList.Sum(c => c.ClassLevel) + 1;
        _skillPointsAdded = _displayService.Skills.AutoAssignSkills(
            _resolvedPackageId,
            _classSkillIds,
            _unavailableSkillIds,
            _skillPointsToAllocate,
            totalLevel,
            _creature.SkillList);

        _allSkills = BuildSkillList();
        ApplySkillFilter();
        UpdateSkillPointsDisplay();
    }

    #endregion
}
