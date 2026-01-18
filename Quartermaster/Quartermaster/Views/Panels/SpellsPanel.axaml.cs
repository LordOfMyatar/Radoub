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
    private TextBlock? _spellSlotTableTitle;
    private Border? _knownSpellsListBorder;
    private StackPanel? _knownSpellsListPanel;
    private Border? _memorizedSpellsTableBorder;
    private Grid? _memorizedSpellsTableGrid;
    private TextBlock? _spellSlotSummaryText;
    private TextBox? _searchTextBox;
    private Button? _clearSearchButton;
    private Button? _clearSpellListButton;
    private ComboBox? _levelFilterComboBox;
    private ComboBox? _schoolFilterComboBox;
    private ComboBox? _statusFilterComboBox;
    private ListBox? _spellsList;
    private TextBlock? _noSpellsText;
    private TextBlock? _loadingText;
    private Expander? _metaMagicExpander;

    // Class selector
    private ComboBox? _classComboBox;
    private List<ClassComboItem> _classItems = new();

    // Class restrictions toggle
    private CheckBox? _ignoreRestrictionsCheckBox;
    private bool _ignoreClassRestrictions = false;

    // Data
    private ObservableCollection<SpellListViewModel> _displayedSpells = new();
    private List<SpellListViewModel> _allSpells = new();
    private HashSet<int> _knownSpellIds = new();
    private Dictionary<int, int> _memorizedSpellCounts = new();  // spellId -> count
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
            e.PropertyName == nameof(SettingsService.FontFamily) ||
            e.PropertyName == nameof(SettingsService.FontSize))
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
        _spellSlotTableTitle = this.FindControl<TextBlock>("SpellSlotTableTitle");
        _knownSpellsListBorder = this.FindControl<Border>("KnownSpellsListBorder");
        _knownSpellsListPanel = this.FindControl<StackPanel>("KnownSpellsListPanel");
        _memorizedSpellsTableBorder = this.FindControl<Border>("MemorizedSpellsTableBorder");
        _memorizedSpellsTableGrid = this.FindControl<Grid>("MemorizedSpellsTableGrid");
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

        // Find class selector
        _classComboBox = this.FindControl<ComboBox>("ClassComboBox");

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

        _clearSpellListButton = this.FindControl<Button>("ClearSpellListButton");
        if (_clearSpellListButton != null)
        {
            _clearSpellListButton.Click += OnClearSpellListClick;
        }

        if (_levelFilterComboBox != null)
            _levelFilterComboBox.SelectionChanged += (s, e) => ApplyFilters();

        if (_schoolFilterComboBox != null)
            _schoolFilterComboBox.SelectionChanged += (s, e) => ApplyFilters();

        if (_statusFilterComboBox != null)
            _statusFilterComboBox.SelectionChanged += (s, e) => ApplyFilters();

        // Wire up class combo box
        if (_classComboBox != null)
            _classComboBox.SelectionChanged += OnClassComboBoxChanged;

        // Wire up ignore restrictions checkbox
        _ignoreRestrictionsCheckBox = this.FindControl<CheckBox>("IgnoreRestrictionsCheckBox");
        if (_ignoreRestrictionsCheckBox != null)
        {
            _ignoreRestrictionsCheckBox.IsCheckedChanged += (s, e) =>
            {
                _ignoreClassRestrictions = _ignoreRestrictionsCheckBox.IsChecked == true;
                // Reload spells to recalculate blocked status
                if (_currentCreature != null)
                    LoadSpellsForClass(_selectedClassIndex);
            };
        }
    }

    /// <summary>
    /// Simple item class for the class selector combo box.
    /// </summary>
    private class ClassComboItem
    {
        public int Index { get; set; }
        public string DisplayName { get; set; } = "";
        public bool IsEnabled { get; set; }

        public override string ToString() => DisplayName;
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
        // Note: "All" (index 0) excludes blocked spells by default for cleaner view
        // Use "Blocked" filter to explicitly see blocked spells
        int statusIndex = _statusFilterComboBox?.SelectedIndex ?? 0;
        filtered = statusIndex switch
        {
            1 => filtered.Where(s => s.IsKnown),          // Known Only
            2 => filtered.Where(s => s.IsMemorized),      // Memorized Only
            3 => filtered.Where(s => !s.IsBlocked),       // Available
            4 => filtered.Where(s => s.IsBlocked),        // Blocked
            _ => filtered.Where(s => !s.IsBlocked)        // All (excludes blocked)
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
        _memorizedSpellCounts.Clear();
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

        // Hide memorized spells table
        if (_memorizedSpellsTableBorder != null)
            _memorizedSpellsTableBorder.IsVisible = false;
        if (_memorizedSpellsTableGrid != null)
        {
            _memorizedSpellsTableGrid.Children.Clear();
            _memorizedSpellsTableGrid.ColumnDefinitions.Clear();
            _memorizedSpellsTableGrid.RowDefinitions.Clear();
        }

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

        // Reset class combo box
        _classItems.Clear();
        if (_classComboBox != null)
        {
            _classComboBox.ItemsSource = null;
            _classComboBox.SelectedIndex = -1;
        }

        // Reset ignore restrictions checkbox
        _ignoreClassRestrictions = false;
        if (_ignoreRestrictionsCheckBox != null)
            _ignoreRestrictionsCheckBox.IsChecked = false;
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
