using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Quartermaster.Services;
using Radoub.UI.Services;

namespace Quartermaster.Views.Dialogs;

/// <summary>
/// Step 1: Class selection with filtering and prerequisite display.
/// </summary>
public partial class LevelUpWizardWindow
{
    #region Step 1: Class Selection

    private void LoadClassList()
    {
        var availableClasses = _displayService.Classes.GetAvailableClasses(_creature, includeUnqualified: true);

        _allClasses = availableClasses.Select(ac => new ClassDisplayItem
        {
            ClassId = ac.ClassId,
            Name = ac.Name,
            Description = ac.Description,
            IsPrestige = ac.IsPrestige,
            HitDie = ac.HitDie,
            SkillPoints = ac.SkillPoints,
            MaxLevel = ac.MaxLevel,
            CurrentLevel = ac.CurrentLevel,
            Qualification = ac.Qualification,
            PrerequisiteResult = ac.PrerequisiteResult,
            CanSelect = ac.CanSelect
        }).ToList();

        ApplyClassFilter();
    }

    private void ApplyClassFilter()
    {
        var searchText = _classSearchBox?.Text?.Trim() ?? "";
        var showUnqualified = _showUnqualifiedPrestigeCheckBox?.IsChecked == true;

        _filteredClasses = _allClasses.Where(c =>
        {
            if (!string.IsNullOrEmpty(searchText) &&
                !c.Name.Contains(searchText, System.StringComparison.OrdinalIgnoreCase))
                return false;

            // In None mode (Chaotic Evil), always show unqualified prestige classes
            if (_validationLevel != ValidationLevel.None && !showUnqualified && c.IsPrestige && !c.CanSelect)
                return false;

            return true;
        }).ToList();

        _classListBox.ItemsSource = _filteredClasses;
    }

    private void OnClassSearchChanged(object? sender, TextChangedEventArgs e)
    {
        ApplyClassFilter();
    }

    private void OnClassFilterChanged(object? sender, RoutedEventArgs e)
    {
        ApplyClassFilter();
    }

    private void OnClassSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_classListBox.SelectedItem is ClassDisplayItem item)
        {
            ShowClassDetails(item);

            if (item.CanSelect || _validationLevel == ValidationLevel.None)
            {
                _selectedClassId = item.ClassId;
                _isNewClass = item.CurrentLevel == 0;
                _newClassLevel = item.CurrentLevel + 1;
            }
            else
            {
                _selectedClassId = -1;
            }
        }
        else
        {
            ClearClassDetails();
            _selectedClassId = -1;
        }

        ValidateCurrentStep();
    }

    private void ShowClassDetails(ClassDisplayItem item)
    {
        _selectedClassNameLabel.Text = item.DisplayName;
        _classStatsPanel.IsVisible = true;
        _classHitDieLabel.Text = $"d{item.HitDie}";
        _classSkillPointsLabel.Text = $"{item.SkillPoints} + INT";

        // Prerequisites
        if (item.IsPrestige && item.PrerequisiteResult != null)
        {
            _classPrereqPanel.IsVisible = true;
            _classPrereqItems.ItemsSource = BuildPrereqDisplay(item.PrerequisiteResult);
        }
        else
        {
            _classPrereqPanel.IsVisible = false;
        }

        // Description
        if (!string.IsNullOrWhiteSpace(item.Description))
        {
            _classDescriptionLabel.Text = item.Description;
            _classDescriptionLabel.ClearValue(Avalonia.Controls.TextBlock.ForegroundProperty);
        }
        else
        {
            _classDescriptionLabel.Text = "(No description available)";
            _classDescriptionLabel.Foreground = BrushManager.GetDisabledBrush(this);
        }
    }

    private void ClearClassDetails()
    {
        _selectedClassNameLabel.Text = "Select a class";
        _classStatsPanel.IsVisible = false;
        _classPrereqPanel.IsVisible = false;
        _classDescriptionLabel.Text = "Select a class to see its description.";
        _classDescriptionLabel.Foreground = BrushManager.GetDisabledBrush(this);
    }

    private List<PrereqDisplayItem> BuildPrereqDisplay(ClassPrereqResult result)
    {
        var items = new List<PrereqDisplayItem>();

        foreach (var (_, desc, met) in result.RequiredFeats)
        {
            items.Add(new PrereqDisplayItem { Icon = met ? "[Y]" : "[N]", Text = desc });
        }

        if (result.OrRequiredFeats.Count > 0)
        {
            bool anyMet = result.OrRequiredFeats.Any(f => f.Met);
            items.Add(new PrereqDisplayItem { Icon = anyMet ? "[Y]" : "[N]", Text = "One of:" });
            foreach (var (_, name, met) in result.OrRequiredFeats)
            {
                items.Add(new PrereqDisplayItem { Icon = met ? "  [Y]" : "  [ ]", Text = name });
            }
        }

        foreach (var (desc, met) in result.SkillRequirements)
        {
            items.Add(new PrereqDisplayItem { Icon = met ? "[Y]" : "[N]", Text = desc });
        }

        foreach (var (desc, met) in result.OtherRequirements)
        {
            items.Add(new PrereqDisplayItem { Icon = met.HasValue ? (met.Value ? "[Y]" : "[N]") : "[?]", Text = desc });
        }

        return items;
    }

    #endregion
}
