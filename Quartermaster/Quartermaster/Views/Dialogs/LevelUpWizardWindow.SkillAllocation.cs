using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Quartermaster.Services;

namespace Quartermaster.Views.Dialogs;

/// <summary>
/// Step 3: Skill point allocation with class/cross-class cost tracking and auto-assign.
/// </summary>
public partial class LevelUpWizardWindow
{
    #region Step 3: Skill Allocation

    // Tracks class skill status for the level being gained
    private HashSet<int> _classSkillIds = new();
    private HashSet<int> _unavailableSkillIds = new();

    private void PrepareStep3()
    {
        // Calculate skill points for this level
        int basePoints = _displayService.GetClassSkillPointBase(_selectedClassId);
        int intMod = CreatureDisplayService.CalculateAbilityBonus(_creature.Int);
        _skillPointsToAllocate = Math.Max(1, basePoints + intMod);

        // D&D 3.5/NWN rule: level 1 gets 4x skill points (engine rule, not 2DA-configurable)
        const int FirstLevelSkillMultiplier = 4;
        int totalLevel = _creature.ClassList.Sum(c => c.ClassLevel) + 1;
        if (totalLevel == 1)
            _skillPointsToAllocate *= FirstLevelSkillMultiplier;

        // Racial bonus skill points (from racialtypes.2da ExtraSkillPointsPerLvl)
        int racialExtraPerLevel = _displayService.GetRacialExtraSkillPointsPerLevel(_creature.Race);
        if (racialExtraPerLevel > 0)
            _skillPointsToAllocate += totalLevel == 1 ? racialExtraPerLevel * FirstLevelSkillMultiplier : racialExtraPerLevel;

        _skillPointsAdded.Clear();

        // Cache class skills for the level being gained
        _classSkillIds = _displayService.GetClassSkillIds(_selectedClassId);

        // Determine unavailable skills (e.g., Use Magic Device for non-Rogue classes)
        _unavailableSkillIds = _displayService.GetUnavailableSkillIds(_creature, _displayService.GetSkillCount());

        string racialLabel = racialExtraPerLevel > 0 ? $" + Racial({racialExtraPerLevel})" : "";
        _skillPointsTotalLabel.Text = $"(Base {basePoints} + INT {intMod}{racialLabel} = {_skillPointsToAllocate})";
        UpdateSkillPointsDisplay();

        // Build skill list
        var skills = BuildSkillList();
        _skillsItemsControl.ItemsSource = skills;
    }

    private List<SkillDisplayItem> BuildSkillList()
    {
        var skills = new List<SkillDisplayItem>();

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
                Cost = isClassSkill ? 1 : 2
            });
        }

        // Sort: class skills first, then by name
        return skills.OrderByDescending(s => s.IsClassSkill).ThenBy(s => s.Name).ToList();
    }

    private int CalculateMaxRanks(bool isClassSkill)
    {
        int totalLevel = _creature.ClassList.Sum(c => c.ClassLevel) + 1;
        return isClassSkill ? totalLevel + 3 : (totalLevel + 3) / 2;
    }

    private int GetRemainingSkillPoints()
    {
        int spent = 0;
        foreach (var (skillId, ranks) in _skillPointsAdded)
        {
            bool isClassSkill = _classSkillIds.Contains(skillId);
            int cost = isClassSkill ? 1 : 2;
            spent += ranks * cost;
        }
        return _skillPointsToAllocate - spent;
    }

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

            if (remaining >= cost)
            {
                int currentAdded = _skillPointsAdded.GetValueOrDefault(skillId, 0);
                int currentRanks = skillId < _creature.SkillList.Count ? _creature.SkillList[skillId] : 0;
                int maxRanks = CalculateMaxRanks(isClassSkill);

                // Check if we can add another rank
                if (currentRanks + currentAdded < maxRanks)
                {
                    _skillPointsAdded[skillId] = currentAdded + 1;
                    _skillsItemsControl.ItemsSource = BuildSkillList();
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
                _skillsItemsControl.ItemsSource = BuildSkillList();
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

        _skillsItemsControl.ItemsSource = BuildSkillList();
        UpdateSkillPointsDisplay();
    }

    #endregion
}
