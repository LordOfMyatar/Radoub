using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace Quartermaster.Views.Dialogs;

/// <summary>
/// Picker for MASTERFEAT subtypes (e.g. choose "Weapon Focus (Club)" from
/// the Weapon Focus master). Consolidates the wall-of-text feat list (#1734).
/// </summary>
public partial class FeatSubtypePickerWindow : Window
{
    public sealed class SubtypeItem
    {
        public int FeatId { get; init; }
        public string Name { get; init; } = "";
        public bool MeetsPrereqs { get; init; } = true;
        public bool AlreadyOwned { get; init; }
        public string Badge => AlreadyOwned ? "(owned)" : (MeetsPrereqs ? "" : "(prereqs)");
    }

    private readonly TextBlock _headerLabel;
    private readonly TextBox _searchBox;
    private readonly ListBox _subtypesListBox;
    private readonly Button _okButton;
    private readonly List<SubtypeItem> _allSubtypes;

    public bool Confirmed { get; private set; }
    public int? SelectedFeatId { get; private set; }

    public FeatSubtypePickerWindow() : this("Select subtype", new List<SubtypeItem>())
    {
    }

    public FeatSubtypePickerWindow(string masterFeatName, List<SubtypeItem> subtypes)
    {
        InitializeComponent();

        _headerLabel = this.FindControl<TextBlock>("HeaderLabel")!;
        _searchBox = this.FindControl<TextBox>("SearchBox")!;
        _subtypesListBox = this.FindControl<ListBox>("SubtypesListBox")!;
        _okButton = this.FindControl<Button>("OkButton")!;

        _headerLabel.Text = $"Select a subtype for {masterFeatName}";
        _allSubtypes = subtypes;

        _searchBox.TextChanged += OnSearchChanged;
        ApplyFilter();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnSearchChanged(object? sender, TextChangedEventArgs e) => ApplyFilter();

    private void ApplyFilter()
    {
        var text = _searchBox.Text?.Trim() ?? "";
        IEnumerable<SubtypeItem> filtered = _allSubtypes;
        if (!string.IsNullOrEmpty(text))
            filtered = filtered.Where(s => s.Name.Contains(text, StringComparison.OrdinalIgnoreCase));

        _subtypesListBox.ItemsSource = filtered
            .OrderByDescending(s => s.MeetsPrereqs && !s.AlreadyOwned)
            .ThenBy(s => s.Name)
            .ToList();

        _subtypesListBox.SelectedItem = null;
        _okButton.IsEnabled = false;
    }

    private void OnSubtypeSelected(object? sender, SelectionChangedEventArgs e)
    {
        _okButton.IsEnabled = _subtypesListBox.SelectedItem is SubtypeItem s && !s.AlreadyOwned;
    }

    private void OnSubtypeDoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        if (_subtypesListBox.SelectedItem is SubtypeItem s && !s.AlreadyOwned)
        {
            SelectedFeatId = s.FeatId;
            Confirmed = true;
            Close();
        }
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        if (_subtypesListBox.SelectedItem is SubtypeItem s && !s.AlreadyOwned)
        {
            SelectedFeatId = s.FeatId;
            Confirmed = true;
            Close();
        }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Confirmed = false;
        Close();
    }
}
