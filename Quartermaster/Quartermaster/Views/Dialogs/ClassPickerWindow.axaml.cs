using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Quartermaster.Services;

namespace Quartermaster.Views.Dialogs;

public partial class ClassPickerWindow : Window
{
    private readonly ListBox _classesListBox;
    private readonly Button _okButton;
    private readonly TextBlock _infoLabel;
    private readonly CheckBox _playerClassesOnlyCheckBox;
    private readonly List<ClassItem> _allClasses;
    private List<ClassItem> _filteredClasses;

    public bool Confirmed { get; private set; }
    public int? SelectedClassId { get; private set; }

    public ClassPickerWindow() : this(new List<ClassInfo>())
    {
    }

    public ClassPickerWindow(List<ClassInfo> classes)
    {
        InitializeComponent();

        _classesListBox = this.FindControl<ListBox>("ClassesListBox")!;
        _okButton = this.FindControl<Button>("OkButton")!;
        _infoLabel = this.FindControl<TextBlock>("InfoLabel")!;
        _playerClassesOnlyCheckBox = this.FindControl<CheckBox>("PlayerClassesOnlyCheckBox")!;

        // Convert to display items
        _allClasses = classes.Select(c => new ClassItem
        {
            Id = c.Id,
            Name = c.Name,
            IsPlayerClass = c.IsPlayerClass,
            MaxLevel = c.MaxLevel
        }).ToList();

        _filteredClasses = new List<ClassItem>();

        // Wire up filter checkbox
        _playerClassesOnlyCheckBox.IsCheckedChanged += OnFilterChanged;

        // Initial filter
        ApplyFilter();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnFilterChanged(object? sender, RoutedEventArgs e)
    {
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var showPlayerOnly = _playerClassesOnlyCheckBox.IsChecked == true;

        _filteredClasses = showPlayerOnly
            ? _allClasses.Where(c => c.IsPlayerClass).ToList()
            : _allClasses;

        _classesListBox.ItemsSource = _filteredClasses;

        // Update info label
        var filterText = showPlayerOnly ? "player classes" : "all classes";
        _infoLabel.Text = $"{_filteredClasses.Count} {filterText} loaded from classes.2da";

        // Clear selection when filter changes
        _classesListBox.SelectedItem = null;
        _okButton.IsEnabled = false;
    }

    private void OnClassSelected(object? sender, SelectionChangedEventArgs e)
    {
        _okButton.IsEnabled = _classesListBox.SelectedItem != null;
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        if (_classesListBox.SelectedItem is ClassItem item)
        {
            SelectedClassId = item.Id;
            Confirmed = true;
            Close();
        }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Confirmed = false;
        Close();
    }

    private class ClassItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsPlayerClass { get; set; }
        public int MaxLevel { get; set; }

        public string MaxLevelDisplay => MaxLevel > 0 ? $"(max {MaxLevel})" : "";
    }
}
