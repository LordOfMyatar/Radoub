using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Quartermaster.Models;
using Quartermaster.Services;
using Radoub.Formats.Gff;
using Radoub.Formats.Utc;
using Radoub.UI.Services;

namespace Quartermaster.Views.Dialogs;

/// <summary>
/// Step 8: Skill rank allocation.
/// </summary>
public partial class NewCharacterWizardWindow
{
    #region Step 8: Skills

    private void PrepareStep8()
    {
        // Delegate skill point calculation to CharacterCreationService
        var creationService = new CharacterCreationService(_displayService, _gameDataService);
        _skillPointsTotal = creationService.CalculateLevel1SkillPoints(
            _selectedClassId, _selectedRaceId, _abilityBaseScores["INT"]);

        // Get class skills and unavailable skills
        _classSkillIds = _displayService.Skills.GetClassSkillIds(_selectedClassId >= 0 ? _selectedClassId : 0);

        // Build a temporary creature to check skill availability
        var tempCreature = new UtcFile
        {
            ClassList = new List<CreatureClass>
            {
                new CreatureClass { Class = _selectedClassId >= 0 ? _selectedClassId : 0, ClassLevel = 1 }
            }
        };
        _unavailableSkillIds = _displayService.Skills.GetUnavailableSkillIds(tempCreature, _displayService.GetSkillCount());

        if (!_step7Loaded)
        {
            _step7Loaded = true;
            _skillRanksAllocated.Clear();
        }

        BuildSkillList();
        RenderSkillRows();
    }

    private void BuildSkillList()
    {
        _allSkills = new List<SkillDisplayItem>();

        int skillCount = _displayService.GetSkillCount();
        for (int i = 0; i < skillCount; i++)
        {
            bool isUnavailable = _unavailableSkillIds.Contains(i);
            bool isClassSkill = _classSkillIds.Contains(i);
            int maxRanks = CharacterCreationService.CalculateMaxSkillRanks(isClassSkill, characterLevel: 1);

            _allSkills.Add(new SkillDisplayItem
            {
                SkillId = i,
                Name = _displayService.Skills.GetSkillName(i),
                KeyAbility = _displayService.Skills.GetSkillKeyAbility(i),
                IsClassSkill = isClassSkill,
                IsUnavailable = isUnavailable,
                MaxRanks = maxRanks,
                AllocatedRanks = _skillRanksAllocated.GetValueOrDefault(i, 0),
                Cost = isClassSkill ? 1 : 2
            });
        }

        // Sort: class skills → cross-class → unavailable, alpha within bucket (#1881)
        _allSkills = SkillDisplayHelper.SortForDisplay(_allSkills);

        ApplySkillFilter();
    }

    private void ApplySkillFilter()
    {
        _filteredSkills = SkillDisplayHelper.FilterByName(_allSkills, _skillSearchBox?.Text?.Trim());
    }

    private void RenderSkillRows()
    {
        _skillRowsPanel.Children.Clear();

        foreach (var skill in _filteredSkills)
        {
            var row = new Grid
            {
                ColumnDefinitions = ColumnDefinitions.Parse("180,50,35,35,60,60,*"),
                Margin = new Avalonia.Thickness(12, 3, 12, 3),
                Opacity = skill.IsUnavailable ? 0.4 : 1.0
            };

            // Skill name — class skills in green, cross-class uses theme default
            var nameLabel = new TextBlock
            {
                Text = skill.Name,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };
            if (skill.IsClassSkill)
                nameLabel.Foreground = BrushManager.GetSuccessBrush(this);
            Grid.SetColumn(nameLabel, 0);
            row.Children.Add(nameLabel);

            // Key ability
            var keyLabel = new TextBlock
            {
                Text = skill.KeyAbility,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Foreground = this.FindResource("SystemControlForegroundBaseMediumBrush") as IBrush,
                FontSize = this.FindResource("FontSizeSmall") as double? ?? 13
            };
            Grid.SetColumn(keyLabel, 1);
            row.Children.Add(keyLabel);

            // [-] button
            var decreaseBtn = new Button
            {
                Content = "−",
                Width = 28,
                HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                Tag = skill.SkillId,
                IsEnabled = !skill.IsUnavailable && skill.AllocatedRanks > 0
            };
            decreaseBtn.Click += OnSkillDecrease;
            Grid.SetColumn(decreaseBtn, 2);
            row.Children.Add(decreaseBtn);

            // [+] button
            var increaseBtn = new Button
            {
                Content = "+",
                Width = 28,
                HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                Tag = skill.SkillId,
                IsEnabled = !skill.IsUnavailable && (_validationLevel != ValidationLevel.Strict || (skill.AllocatedRanks < skill.MaxRanks && GetSkillPointsRemaining() >= skill.Cost))
            };
            increaseBtn.Click += OnSkillIncrease;
            Grid.SetColumn(increaseBtn, 3);
            row.Children.Add(increaseBtn);

            // Allocated ranks
            var ranksLabel = new TextBlock
            {
                Text = skill.AllocatedRanks > 0 ? skill.AllocatedRanks.ToString() : "—",
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                FontWeight = skill.AllocatedRanks > 0 ? FontWeight.Bold : FontWeight.Normal
            };
            if (skill.AllocatedRanks > 0)
                ranksLabel.Foreground = BrushManager.GetSuccessBrush(this);
            Grid.SetColumn(ranksLabel, 4);
            row.Children.Add(ranksLabel);

            // Max ranks
            var maxLabel = new TextBlock
            {
                Text = skill.IsUnavailable ? "—" : skill.MaxRanks.ToString(),
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Foreground = this.FindResource("SystemControlForegroundBaseMediumBrush") as IBrush
            };
            Grid.SetColumn(maxLabel, 5);
            row.Children.Add(maxLabel);

            // Type indicator
            var typeLabel = new TextBlock
            {
                Text = skill.IsUnavailable ? "Unavailable" : skill.IsClassSkill ? "Class (1 pt)" : "Cross-class (2 pts)",
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                FontSize = this.FindResource("FontSizeSmall") as double? ?? 13,
                Foreground = this.FindResource("SystemControlForegroundBaseMediumBrush") as IBrush
            };
            Grid.SetColumn(typeLabel, 6);
            row.Children.Add(typeLabel);

            _skillRowsPanel.Children.Add(row);
        }

        UpdateSkillPointsDisplay();
    }

    private void UpdateSkillPointsDisplay()
    {
        int remaining = GetSkillPointsRemaining();
        _skillPointsRemainingLabel.Text = remaining.ToString();

        if (remaining > 0)
            _skillPointsRemainingLabel.Foreground = BrushManager.GetSuccessBrush(this);
        else if (remaining == 0)
            _skillPointsRemainingLabel.ClearValue(TextBlock.ForegroundProperty);
        else
            _skillPointsRemainingLabel.Foreground = BrushManager.GetErrorBrush(this);

        ValidateCurrentStep();
    }

    private int GetSkillPointsRemaining()
    {
        int spent = 0;
        foreach (var (skillId, ranks) in _skillRanksAllocated)
        {
            bool isClassSkill = _classSkillIds.Contains(skillId);
            int cost = isClassSkill ? 1 : 2;
            spent += ranks * cost;
        }
        return _skillPointsTotal - spent;
    }

    private void OnSkillIncrease(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is int skillId)
        {
            bool isClassSkill = _classSkillIds.Contains(skillId);
            int cost = isClassSkill ? 1 : 2;
            int maxRanks = CharacterCreationService.CalculateMaxSkillRanks(isClassSkill, characterLevel: 1);

            bool canIncrease = _validationLevel != ValidationLevel.Strict
                || (GetSkillPointsRemaining() >= cost && _skillRanksAllocated.GetValueOrDefault(skillId, 0) < maxRanks);

            if (canIncrease)
            {
                int current = _skillRanksAllocated.GetValueOrDefault(skillId, 0);
                _skillRanksAllocated[skillId] = current + 1;
                UpdateSkillItem(skillId);
                RenderSkillRows();
            }
        }
    }

    private void OnSkillDecrease(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is int skillId)
        {
            int current = _skillRanksAllocated.GetValueOrDefault(skillId, 0);
            if (current > 0)
            {
                _skillRanksAllocated[skillId] = current - 1;
                if (_skillRanksAllocated[skillId] == 0)
                    _skillRanksAllocated.Remove(skillId);
                UpdateSkillItem(skillId);
                RenderSkillRows();
            }
        }
    }

    private void UpdateSkillItem(int skillId)
    {
        var skill = _allSkills.FirstOrDefault(s => s.SkillId == skillId);
        if (skill != null)
        {
            skill.AllocatedRanks = _skillRanksAllocated.GetValueOrDefault(skillId, 0);
        }
    }

    private void OnSkillSearchChanged(object? sender, TextChangedEventArgs e)
    {
        ApplySkillFilter();
        RenderSkillRows();
    }

    private void OnSkillAutoAssignClick(object? sender, RoutedEventArgs e)
    {
        // Clear all allocations
        _skillRanksAllocated.Clear();

        // Use shared service method (NCW is level 1, no existing ranks)
        var allocated = _displayService.Skills.AutoAssignSkills(
            _selectedPackageId,
            _classSkillIds,
            _unavailableSkillIds,
            _skillPointsTotal,
            totalLevel: 1,
            existingRanks: null);

        foreach (var (skillId, points) in allocated)
        {
            _skillRanksAllocated[skillId] = points;
        }

        // Update display items
        foreach (var skill in _allSkills)
        {
            skill.AllocatedRanks = _skillRanksAllocated.GetValueOrDefault(skill.SkillId, 0);
        }

        RenderSkillRows();
    }

    #endregion
}
