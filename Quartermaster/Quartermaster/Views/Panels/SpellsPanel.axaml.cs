using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using Quartermaster.Services;
using Quartermaster.ViewModels;
using Radoub.Formats.Logging;
using Radoub.Formats.Utc;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Quartermaster.Views.Panels;

public partial class SpellsPanel : UserControl
{
    private CreatureDisplayService? _displayService;
    private ItemIconService? _itemIconService;
    private UtcFile? _currentCreature;
    private bool _isLoading;

    /// <summary>
    /// Raised when the user modifies known/memorized spells.
    /// </summary>
    public event EventHandler? SpellsChanged;

    // UI Controls
    private Border? _spellSlotTableBorder;
    private Grid? _spellSlotTableGrid;
    private Border? _knownSpellsListBorder;
    private StackPanel? _knownSpellsListPanel;
    private TextBlock? _spellSlotSummaryText;
    private TextBox? _searchTextBox;
    private Button? _clearSearchButton;
    private ComboBox? _levelFilterComboBox;
    private ComboBox? _schoolFilterComboBox;
    private ComboBox? _statusFilterComboBox;
    private ListBox? _spellsList;
    private TextBlock? _noSpellsText;
    private TextBlock? _loadingText;
    private Expander? _metaMagicExpander;

    // Class radio buttons
    private RadioButton? _classRadio1;
    private RadioButton? _classRadio2;
    private RadioButton? _classRadio3;
    private RadioButton? _classRadio4;
    private RadioButton? _classRadio5;
    private RadioButton? _classRadio6;
    private RadioButton? _classRadio7;
    private RadioButton? _classRadio8;
    private RadioButton[] _classRadios = Array.Empty<RadioButton>();

    // Data
    private ObservableCollection<SpellListViewModel> _displayedSpells = new();
    private List<SpellListViewModel> _allSpells = new();
    private HashSet<int> _knownSpellIds = new();
    private HashSet<int> _memorizedSpellIds = new();
    private int _selectedClassIndex = 0;
    private bool _isSpontaneousCaster = false;

    public SpellsPanel()
    {
        InitializeComponent();

        // Subscribe to theme changes to refresh color-dependent view models
        SettingsService.Instance.PropertyChanged += OnSettingsPropertyChanged;
    }

    private void OnSettingsPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsService.CurrentThemeId) ||
            e.PropertyName == nameof(SettingsService.FontFamily))
        {
            // Theme or font changed - reload creature to refresh view
            if (_currentCreature != null)
            {
                LoadCreature(_currentCreature);
            }
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

        // Find UI controls
        _spellSlotTableBorder = this.FindControl<Border>("SpellSlotTableBorder");
        _spellSlotTableGrid = this.FindControl<Grid>("SpellSlotTableGrid");
        _knownSpellsListBorder = this.FindControl<Border>("KnownSpellsListBorder");
        _knownSpellsListPanel = this.FindControl<StackPanel>("KnownSpellsListPanel");
        _spellSlotSummaryText = this.FindControl<TextBlock>("SpellSlotSummaryText");
        _searchTextBox = this.FindControl<TextBox>("SearchTextBox");
        _clearSearchButton = this.FindControl<Button>("ClearSearchButton");
        _levelFilterComboBox = this.FindControl<ComboBox>("LevelFilterComboBox");
        _schoolFilterComboBox = this.FindControl<ComboBox>("SchoolFilterComboBox");
        _statusFilterComboBox = this.FindControl<ComboBox>("StatusFilterComboBox");
        _spellsList = this.FindControl<ListBox>("SpellsList");
        _noSpellsText = this.FindControl<TextBlock>("NoSpellsText");
        _loadingText = this.FindControl<TextBlock>("LoadingText");
        _metaMagicExpander = this.FindControl<Expander>("MetaMagicExpander");

        // Find class radio buttons
        _classRadio1 = this.FindControl<RadioButton>("ClassRadio1");
        _classRadio2 = this.FindControl<RadioButton>("ClassRadio2");
        _classRadio3 = this.FindControl<RadioButton>("ClassRadio3");
        _classRadio4 = this.FindControl<RadioButton>("ClassRadio4");
        _classRadio5 = this.FindControl<RadioButton>("ClassRadio5");
        _classRadio6 = this.FindControl<RadioButton>("ClassRadio6");
        _classRadio7 = this.FindControl<RadioButton>("ClassRadio7");
        _classRadio8 = this.FindControl<RadioButton>("ClassRadio8");

        _classRadios = new[]
        {
            _classRadio1!, _classRadio2!, _classRadio3!, _classRadio4!,
            _classRadio5!, _classRadio6!, _classRadio7!, _classRadio8!
        };

        // Set up ItemsSource
        if (_spellsList != null)
            _spellsList.ItemsSource = _displayedSpells;

        // Wire up event handlers
        if (_searchTextBox != null)
            _searchTextBox.TextChanged += (s, e) => ApplyFilters();

        if (_clearSearchButton != null)
        {
            _clearSearchButton.Click += (s, e) =>
            {
                if (_searchTextBox != null)
                    _searchTextBox.Text = "";
            };
        }

        if (_levelFilterComboBox != null)
            _levelFilterComboBox.SelectionChanged += (s, e) => ApplyFilters();

        if (_schoolFilterComboBox != null)
            _schoolFilterComboBox.SelectionChanged += (s, e) => ApplyFilters();

        if (_statusFilterComboBox != null)
            _statusFilterComboBox.SelectionChanged += (s, e) => ApplyFilters();

        // Wire up class radio buttons
        foreach (var radio in _classRadios)
        {
            if (radio != null)
                radio.IsCheckedChanged += OnClassRadioChecked;
        }
    }

    /// <summary>
    /// Sets the display service for 2DA/TLK lookups.
    /// </summary>
    public void SetDisplayService(CreatureDisplayService displayService)
    {
        _displayService = displayService;
    }

    /// <summary>
    /// Sets the icon service for loading spell icons.
    /// </summary>
    public void SetIconService(ItemIconService iconService)
    {
        _itemIconService = iconService;
        UnifiedLogger.LogUI(LogLevel.INFO, $"SpellsPanel: IconService set, IsGameDataAvailable={iconService?.IsGameDataAvailable}");
    }

    private static string GetSchoolName(SpellSchool school)
    {
        return school switch
        {
            SpellSchool.Abjuration => "Abjuration",
            SpellSchool.Conjuration => "Conjuration",
            SpellSchool.Divination => "Divination",
            SpellSchool.Enchantment => "Enchantment",
            SpellSchool.Evocation => "Evocation",
            SpellSchool.Illusion => "Illusion",
            SpellSchool.Necromancy => "Necromancy",
            SpellSchool.Transmutation => "Transmutation",
            _ => "General"
        };
    }

    private static string BuildTooltip(string spellName, SpellInfo info, string blockedReason)
    {
        var lines = new List<string> { spellName };

        lines.Add($"School: {GetSchoolName(info.School)}");
        lines.Add($"Innate Level: {info.InnateLevel}");

        if (!string.IsNullOrEmpty(blockedReason))
        {
            lines.Add("");
            lines.Add($"⚠ {blockedReason}");
        }

        return string.Join("\n", lines);
    }

    private void ApplyFilters()
    {
        if (_allSpells.Count == 0)
        {
            _displayedSpells.Clear();
            if (_noSpellsText != null)
                _noSpellsText.IsVisible = true;
            UpdateSummary();
            return;
        }

        var filtered = _allSpells.AsEnumerable();

        // Apply search filter
        var searchText = _searchTextBox?.Text?.Trim() ?? "";
        if (!string.IsNullOrEmpty(searchText))
        {
            filtered = filtered.Where(s =>
                s.SpellName.Contains(searchText, StringComparison.OrdinalIgnoreCase));
        }

        // Apply level filter
        int levelIndex = _levelFilterComboBox?.SelectedIndex ?? 0;
        if (levelIndex > 0)
        {
            int targetLevel = levelIndex - 1; // Index 1 = Level 0, Index 2 = Level 1, etc.
            filtered = filtered.Where(s => s.SpellLevel == targetLevel);
        }

        // Apply school filter
        int schoolIndex = _schoolFilterComboBox?.SelectedIndex ?? 0;
        if (schoolIndex > 0)
        {
            var targetSchool = (SpellSchool)(schoolIndex - 1);
            filtered = filtered.Where(s => s.School == targetSchool);
        }

        // Apply status filter
        int statusIndex = _statusFilterComboBox?.SelectedIndex ?? 0;
        filtered = statusIndex switch
        {
            1 => filtered.Where(s => s.IsKnown),          // Known Only
            2 => filtered.Where(s => s.IsMemorized),      // Memorized Only
            3 => filtered.Where(s => !s.IsBlocked),       // Available
            4 => filtered.Where(s => s.IsBlocked),        // Blocked
            _ => filtered                                  // All Spells
        };

        // Update display
        UnifiedLogger.LogUI(LogLevel.INFO, $"SpellsPanel.ApplyFilters: Clearing displayedSpells...");
        _displayedSpells.Clear();

        var filteredList = filtered.ToList();
        UnifiedLogger.LogUI(LogLevel.INFO, $"SpellsPanel.ApplyFilters: Adding {filteredList.Count} spells to display...");

        int addCount = 0;
        foreach (var spell in filteredList)
        {
            _displayedSpells.Add(spell);
            addCount++;
            if (addCount % 100 == 0)
            {
                UnifiedLogger.LogUI(LogLevel.INFO, $"SpellsPanel.ApplyFilters: Added {addCount}/{filteredList.Count} spells to display");
            }
        }

        UnifiedLogger.LogUI(LogLevel.INFO, $"SpellsPanel.ApplyFilters: Done adding spells, count={_displayedSpells.Count}");

        // Show "no spells" message if empty
        if (_noSpellsText != null)
            _noSpellsText.IsVisible = _displayedSpells.Count == 0;

        UpdateSummary();
    }

    public void ClearPanel()
    {
        _displayedSpells.Clear();
        _allSpells.Clear();
        _knownSpellIds.Clear();
        _memorizedSpellIds.Clear();
        _currentCreature = null;
        _selectedClassIndex = 0;

        // Hide spell slot table
        if (_spellSlotTableBorder != null)
            _spellSlotTableBorder.IsVisible = false;
        if (_spellSlotTableGrid != null)
        {
            _spellSlotTableGrid.Children.Clear();
            _spellSlotTableGrid.ColumnDefinitions.Clear();
            _spellSlotTableGrid.RowDefinitions.Clear();
        }

        // Hide known spells list
        if (_knownSpellsListBorder != null)
            _knownSpellsListBorder.IsVisible = false;
        if (_knownSpellsListPanel != null)
            _knownSpellsListPanel.Children.Clear();

        SetText(_spellSlotSummaryText, "");

        if (_noSpellsText != null)
            _noSpellsText.IsVisible = false;

        if (_searchTextBox != null)
            _searchTextBox.Text = "";

        if (_levelFilterComboBox != null)
            _levelFilterComboBox.SelectedIndex = 0;

        if (_schoolFilterComboBox != null)
            _schoolFilterComboBox.SelectedIndex = 0;

        if (_statusFilterComboBox != null)
            _statusFilterComboBox.SelectedIndex = 0;

        // Reset class radio buttons
        foreach (var radio in _classRadios)
        {
            if (radio != null)
            {
                radio.IsEnabled = false;
                radio.IsVisible = radio == _classRadio1 || radio == _classRadio2 || radio == _classRadio3;
            }
        }
        if (_classRadio1 != null)
            _classRadio1.IsChecked = true;
    }

    private static void SetText(TextBlock? block, string text)
    {
        if (block != null)
            block.Text = text;
    }

    #region Theme-Aware Colors

    // Light theme default colors for fallback
    private static readonly IBrush DefaultDisabledBrush = new SolidColorBrush(Color.Parse("#757575")); // Gray
    private static readonly IBrush DefaultSuccessBrush = new SolidColorBrush(Color.Parse("#388E3C"));  // Green
    private static readonly IBrush DefaultInfoBrush = new SolidColorBrush(Color.Parse("#1976D2"));     // Blue
    private static readonly IBrush DefaultSelectionBrush = new SolidColorBrush(Color.Parse("#FFC107")); // Gold/Yellow

    private IBrush GetDisabledBrush()
    {
        var app = Application.Current;
        if (app?.Resources.TryGetResource("ThemeDisabled", ThemeVariant.Default, out var brush) == true
            && brush is IBrush b)
            return b;
        return DefaultDisabledBrush;
    }

    private IBrush GetSuccessBrush()
    {
        var app = Application.Current;
        if (app?.Resources.TryGetResource("ThemeSuccess", ThemeVariant.Default, out var brush) == true
            && brush is IBrush b)
            return b;
        return DefaultSuccessBrush;
    }

    private IBrush GetInfoBrush()
    {
        var app = Application.Current;
        if (app?.Resources.TryGetResource("ThemeInfo", ThemeVariant.Default, out var brush) == true
            && brush is IBrush b)
            return b;
        return DefaultInfoBrush;
    }

    private IBrush GetSelectionBrush()
    {
        var app = Application.Current;
        if (app?.Resources.TryGetResource("ThemeSelection", ThemeVariant.Default, out var brush) == true
            && brush is IBrush b)
            return b;
        return DefaultSelectionBrush;
    }

    private static IBrush GetTransparentRowBackground(IBrush baseBrush, byte alpha = 30)
    {
        if (baseBrush is SolidColorBrush scb)
        {
            var c = scb.Color;
            return new SolidColorBrush(Color.FromArgb(alpha, c.R, c.G, c.B));
        }
        return Brushes.Transparent;
    }

    #endregion
}
