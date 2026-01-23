using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace Quartermaster.Views.Dialogs;

public partial class SpellPickerWindow : Window
{
    private readonly ListBox _spellsListBox;
    private readonly TextBox _searchTextBox;
    private readonly Button _clearSearchButton;
    private readonly Button _okButton;
    private readonly TextBlock _infoLabel;
    private readonly List<SpellItem> _allSpells;
    private List<SpellItem> _filteredSpells;

    public bool Confirmed { get; private set; }
    public ushort? SelectedSpellId { get; private set; }
    public string SelectedSpellName { get; private set; } = "";

    public SpellPickerWindow() : this(new List<(int Id, string Name, int InnateLevel)>())
    {
    }

    public SpellPickerWindow(List<(int Id, string Name, int InnateLevel)> spells)
    {
        InitializeComponent();

        _spellsListBox = this.FindControl<ListBox>("SpellsListBox")!;
        _searchTextBox = this.FindControl<TextBox>("SearchTextBox")!;
        _clearSearchButton = this.FindControl<Button>("ClearSearchButton")!;
        _okButton = this.FindControl<Button>("OkButton")!;
        _infoLabel = this.FindControl<TextBlock>("InfoLabel")!;

        // Convert to display items
        _allSpells = spells.Select(s => new SpellItem
        {
            Id = s.Id,
            Name = s.Name,
            InnateLevel = s.InnateLevel,
            LevelDisplay = $"Lv {s.InnateLevel}"
        }).OrderBy(s => s.Name).ToList();

        _filteredSpells = _allSpells;

        // Set item source
        _spellsListBox.ItemsSource = _filteredSpells;

        // Wire up search
        _searchTextBox.TextChanged += OnSearchTextChanged;
        _clearSearchButton.Click += OnClearSearchClick;

        // Update info label
        _infoLabel.Text = $"{spells.Count} spells loaded from spells.2da";
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        ApplyFilter();
    }

    private void OnClearSearchClick(object? sender, RoutedEventArgs e)
    {
        _searchTextBox.Text = "";
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var searchText = _searchTextBox.Text?.Trim() ?? "";

        if (string.IsNullOrEmpty(searchText))
        {
            _filteredSpells = _allSpells;
        }
        else
        {
            _filteredSpells = _allSpells
                .Where(s => s.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                            s.Id.ToString().Contains(searchText))
                .ToList();
        }

        _spellsListBox.ItemsSource = _filteredSpells;
        _infoLabel.Text = $"Showing {_filteredSpells.Count} of {_allSpells.Count} spells";
    }

    private void OnSpellSelected(object? sender, SelectionChangedEventArgs e)
    {
        _okButton.IsEnabled = _spellsListBox.SelectedItem != null;
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        if (_spellsListBox.SelectedItem is SpellItem item)
        {
            SelectedSpellId = (ushort)item.Id;
            SelectedSpellName = item.Name;
            Confirmed = true;
            Close();
        }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Confirmed = false;
        Close();
    }

    private class SpellItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int InnateLevel { get; set; }
        public string LevelDisplay { get; set; } = string.Empty;
    }
}
