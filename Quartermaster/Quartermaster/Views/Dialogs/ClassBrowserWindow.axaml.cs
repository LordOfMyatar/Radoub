using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Quartermaster.Services;
using Radoub.Formats.Utc;

namespace Quartermaster.Views.Dialogs;

/// <summary>
/// Browser window for selecting a class during level-up.
/// Shows class descriptions and prerequisite status for prestige classes.
/// </summary>
public partial class ClassBrowserWindow : Window
{
    private readonly ClassService _classService;
    private readonly UtcFile? _creature;
    private readonly ListBox _classListBox;
    private readonly TextBox _searchBox;
    private readonly CheckBox _playerClassesOnlyCheckBox;
    private readonly CheckBox _showUnqualifiedCheckBox;
    private readonly TextBlock _classCountLabel;
    private readonly TextBlock _classNameLabel;
    private readonly TextBlock _hitDieLabel;
    private readonly TextBlock _skillPointsLabel;
    private readonly TextBlock _maxLevelLabel;
    private readonly StackPanel _classStatsPanel;
    private readonly StackPanel _maxLevelPanel;
    private readonly Border _prerequisitesPanel;
    private readonly ItemsControl _prerequisitesItemsControl;
    private readonly TextBlock _descriptionTextBlock;
    private readonly TextBlock _selectedClassLabel;
    private readonly Button _okButton;

    private List<ClassListItem> _allClasses = new();
    private List<ClassListItem> _filteredClasses = new();

    public bool Confirmed { get; private set; }
    public int? SelectedClassId { get; private set; }

    public ClassBrowserWindow() : this(null!, null)
    {
    }

    public ClassBrowserWindow(ClassService classService, UtcFile? creature)
    {
        InitializeComponent();

        _classService = classService;
        _creature = creature;

        // Find controls
        _classListBox = this.FindControl<ListBox>("ClassListBox")!;
        _searchBox = this.FindControl<TextBox>("SearchBox")!;
        _playerClassesOnlyCheckBox = this.FindControl<CheckBox>("PlayerClassesOnlyCheckBox")!;
        _showUnqualifiedCheckBox = this.FindControl<CheckBox>("ShowUnqualifiedCheckBox")!;
        _classCountLabel = this.FindControl<TextBlock>("ClassCountLabel")!;
        _classNameLabel = this.FindControl<TextBlock>("ClassNameLabel")!;
        _hitDieLabel = this.FindControl<TextBlock>("HitDieLabel")!;
        _skillPointsLabel = this.FindControl<TextBlock>("SkillPointsLabel")!;
        _maxLevelLabel = this.FindControl<TextBlock>("MaxLevelLabel")!;
        _classStatsPanel = this.FindControl<StackPanel>("ClassStatsPanel")!;
        _maxLevelPanel = this.FindControl<StackPanel>("MaxLevelPanel")!;
        _prerequisitesPanel = this.FindControl<Border>("PrerequisitesPanel")!;
        _prerequisitesItemsControl = this.FindControl<ItemsControl>("PrerequisitesItemsControl")!;
        _descriptionTextBlock = this.FindControl<TextBlock>("DescriptionTextBlock")!;
        _selectedClassLabel = this.FindControl<TextBlock>("SelectedClassLabel")!;
        _okButton = this.FindControl<Button>("OkButton")!;

        LoadClasses();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void LoadClasses()
    {
        if (_classService == null)
            return;

        var availableClasses = _creature != null
            ? _classService.GetAvailableClasses(_creature, includeUnqualified: true)
            : _classService.GetAllClassMetadata()
                .Where(m => m.IsPlayerClass)
                .Select(m => new AvailableClass
                {
                    ClassId = m.ClassId,
                    Name = m.Name,
                    Description = m.Description,
                    IsPrestige = m.IsPrestige,
                    HitDie = m.HitDie,
                    SkillPoints = m.SkillPointsPerLevel,
                    MaxLevel = m.MaxLevel,
                    Qualification = ClassQualification.Qualified
                })
                .ToList();

        _allClasses = availableClasses.Select(ac => new ClassListItem
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

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var searchText = _searchBox?.Text?.Trim() ?? "";
        var playerOnly = _playerClassesOnlyCheckBox?.IsChecked == true;
        var showUnqualified = _showUnqualifiedCheckBox?.IsChecked == true;

        _filteredClasses = _allClasses.Where(c =>
        {
            // Search filter
            if (!string.IsNullOrEmpty(searchText) &&
                !c.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                return false;

            // Player class filter (always applied when checked - non-player classes filtered out in LoadClasses)
            // Unqualified prestige filter
            if (!showUnqualified && c.IsPrestige && !c.CanSelect)
                return false;

            return true;
        }).ToList();

        _classListBox.ItemsSource = _filteredClasses;
        _classCountLabel.Text = $"{_filteredClasses.Count} classes";

        // Clear selection when filter changes
        _classListBox.SelectedItem = null;
        ClearDetails();
    }

    private void ClearDetails()
    {
        _classNameLabel.Text = "Select a class";
        _classStatsPanel.IsVisible = false;
        _prerequisitesPanel.IsVisible = false;
        _descriptionTextBlock.Text = "Select a class to view its description.";
        _descriptionTextBlock.Foreground = Avalonia.Media.Brushes.Gray;
        _selectedClassLabel.Text = "(none)";
        _okButton.IsEnabled = false;
    }

    private void ShowClassDetails(ClassListItem item)
    {
        _classNameLabel.Text = item.DisplayName;
        _classStatsPanel.IsVisible = true;

        _hitDieLabel.Text = $"d{item.HitDie}";
        _skillPointsLabel.Text = $"{item.SkillPoints} + INT";

        if (item.MaxLevel > 0)
        {
            _maxLevelPanel.IsVisible = true;
            _maxLevelLabel.Text = item.MaxLevel.ToString();
        }
        else
        {
            _maxLevelPanel.IsVisible = false;
        }

        // Show prerequisites for prestige classes
        if (item.IsPrestige && item.PrerequisiteResult != null)
        {
            _prerequisitesPanel.IsVisible = true;
            var prereqItems = BuildPrerequisiteDisplay(item.PrerequisiteResult);
            _prerequisitesItemsControl.ItemsSource = prereqItems;
        }
        else
        {
            _prerequisitesPanel.IsVisible = false;
        }

        // Description
        if (!string.IsNullOrWhiteSpace(item.Description))
        {
            _descriptionTextBlock.Text = item.Description;
            _descriptionTextBlock.Foreground = Avalonia.Media.Brushes.White;
        }
        else
        {
            _descriptionTextBlock.Text = "(No description available)";
            _descriptionTextBlock.Foreground = Avalonia.Media.Brushes.Gray;
        }

        // Selection status
        if (item.CanSelect)
        {
            _selectedClassLabel.Text = item.Name;
            _okButton.IsEnabled = true;
        }
        else
        {
            _selectedClassLabel.Text = $"{item.Name} ({item.Qualification switch
            {
                ClassQualification.PrerequisitesNotMet => "prerequisites not met",
                ClassQualification.MaxLevelReached => "max level reached",
                ClassQualification.MaxClassesReached => "max 8 classes",
                ClassQualification.AlignmentRestricted => "alignment restricted",
                _ => "unavailable"
            }})";
            _okButton.IsEnabled = false;
        }
    }

    private List<PrereqDisplayItem> BuildPrerequisiteDisplay(ClassPrereqResult result)
    {
        var items = new List<PrereqDisplayItem>();

        foreach (var (_, desc, met) in result.RequiredFeats)
        {
            items.Add(new PrereqDisplayItem
            {
                Icon = met ? "[Y]" : "[N]",
                Text = desc
            });
        }

        if (result.OrRequiredFeats.Count > 0)
        {
            bool anyMet = result.OrRequiredFeats.Any(f => f.Met);
            items.Add(new PrereqDisplayItem
            {
                Icon = anyMet ? "[Y]" : "[N]",
                Text = "One of the following:"
            });
            foreach (var (_, name, met) in result.OrRequiredFeats)
            {
                items.Add(new PrereqDisplayItem
                {
                    Icon = met ? "  [Y]" : "  [ ]",
                    Text = name
                });
            }
        }

        foreach (var (desc, met) in result.SkillRequirements)
        {
            items.Add(new PrereqDisplayItem
            {
                Icon = met ? "[Y]" : "[N]",
                Text = desc
            });
        }

        foreach (var (desc, met) in result.OtherRequirements)
        {
            items.Add(new PrereqDisplayItem
            {
                Icon = met.HasValue ? (met.Value ? "[Y]" : "[N]") : "[?]",
                Text = desc
            });
        }

        return items;
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        ApplyFilter();
    }

    private void OnFilterChanged(object? sender, RoutedEventArgs e)
    {
        ApplyFilter();
    }

    private void OnClassSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (_classListBox.SelectedItem is ClassListItem item)
        {
            ShowClassDetails(item);
        }
        else
        {
            ClearDetails();
        }
    }

    private void OnClassDoubleClicked(object? sender, TappedEventArgs e)
    {
        if (_classListBox.SelectedItem is ClassListItem item && item.CanSelect)
        {
            SelectedClassId = item.ClassId;
            Confirmed = true;
            Close();
        }
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        if (_classListBox.SelectedItem is ClassListItem item && item.CanSelect)
        {
            SelectedClassId = item.ClassId;
            Confirmed = true;
            Close();
        }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Confirmed = false;
        Close();
    }

    /// <summary>
    /// Display item for the class list.
    /// </summary>
    private class ClassListItem
    {
        public int ClassId { get; set; }
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public bool IsPrestige { get; set; }
        public int HitDie { get; set; }
        public int SkillPoints { get; set; }
        public int MaxLevel { get; set; }
        public int CurrentLevel { get; set; }
        public ClassQualification Qualification { get; set; }
        public ClassPrereqResult? PrerequisiteResult { get; set; }
        public bool CanSelect { get; set; }

        public string DisplayIcon => IsPrestige ? "*" : " ";

        public string DisplayName
        {
            get
            {
                if (CurrentLevel > 0)
                    return $"{Name} (Lvl {CurrentLevel})";
                return Name;
            }
        }

        public string QualificationBadge
        {
            get
            {
                if (CanSelect) return "";
                return Qualification switch
                {
                    ClassQualification.PrerequisitesNotMet => "(prereqs)",
                    ClassQualification.MaxLevelReached => "(max)",
                    ClassQualification.MaxClassesReached => "(limit)",
                    ClassQualification.AlignmentRestricted => "(align)",
                    _ => ""
                };
            }
        }
    }

    /// <summary>
    /// Display item for prerequisites list.
    /// </summary>
    private class PrereqDisplayItem
    {
        public string Icon { get; set; } = "";
        public string Text { get; set; } = "";
    }
}
