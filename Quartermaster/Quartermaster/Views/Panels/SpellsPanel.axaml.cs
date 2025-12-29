using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Quartermaster.Services;
using Radoub.Formats.Utc;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Quartermaster.Views.Panels;

public partial class SpellsPanel : UserControl
{
    private CreatureDisplayService? _displayService;
    private UtcFile? _currentCreature;

    // UI Controls
    private TextBlock? _spellsSummaryText;
    private TextBox? _searchTextBox;
    private Button? _clearSearchButton;
    private ComboBox? _levelFilterComboBox;
    private ComboBox? _schoolFilterComboBox;
    private ComboBox? _statusFilterComboBox;
    private ItemsControl? _spellsList;
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

    public SpellsPanel()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

        // Find UI controls
        _spellsSummaryText = this.FindControl<TextBlock>("SpellsSummaryText");
        _searchTextBox = this.FindControl<TextBox>("SearchTextBox");
        _clearSearchButton = this.FindControl<Button>("ClearSearchButton");
        _levelFilterComboBox = this.FindControl<ComboBox>("LevelFilterComboBox");
        _schoolFilterComboBox = this.FindControl<ComboBox>("SchoolFilterComboBox");
        _statusFilterComboBox = this.FindControl<ComboBox>("StatusFilterComboBox");
        _spellsList = this.FindControl<ItemsControl>("SpellsList");
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

    private void OnClassRadioChecked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is RadioButton radio)
        {
            var index = Array.IndexOf(_classRadios, radio);
            if (index >= 0 && index != _selectedClassIndex)
            {
                _selectedClassIndex = index;
                // Reload spells for the selected class
                if (_currentCreature != null)
                    LoadSpellsForClass(_selectedClassIndex);
            }
        }
    }

    /// <summary>
    /// Sets the display service for 2DA/TLK lookups.
    /// </summary>
    public void SetDisplayService(CreatureDisplayService displayService)
    {
        _displayService = displayService;
    }

    public void LoadCreature(UtcFile? creature)
    {
        _displayedSpells.Clear();
        _allSpells.Clear();
        _knownSpellIds.Clear();
        _memorizedSpellIds.Clear();
        _currentCreature = creature;
        _selectedClassIndex = 0;

        if (creature == null)
        {
            ClearPanel();
            return;
        }

        // Show loading state
        if (_loadingText != null)
            _loadingText.IsVisible = true;

        // Update class radio buttons based on creature's classes
        UpdateClassRadioButtons(creature);

        // Load spells for the first (selected) class
        LoadSpellsForClass(_selectedClassIndex);

        // Hide loading state
        if (_loadingText != null)
            _loadingText.IsVisible = false;
    }

    private void UpdateClassRadioButtons(UtcFile creature)
    {
        // Reset all radio buttons
        bool foundCaster = false;

        for (int i = 0; i < _classRadios.Length; i++)
        {
            var radio = _classRadios[i];
            if (radio == null) continue;

            if (i < creature.ClassList.Count)
            {
                var classEntry = creature.ClassList[i];
                var className = _displayService?.GetClassName(classEntry.Class) ?? $"Class {classEntry.Class}";

                // Check if this class can cast spells using the display service
                bool isCaster = _displayService?.IsCasterClass(classEntry.Class) ?? false;
                int maxSpellLevel = isCaster ? (_displayService?.GetMaxSpellLevel(classEntry.Class, classEntry.ClassLevel) ?? -1) : -1;

                // Format: "Wizard (10) - Lvl 5" or "Fighter (5)" for non-casters
                if (isCaster && maxSpellLevel >= 0)
                {
                    radio.Content = $"{className} ({classEntry.ClassLevel}) - Lvl {maxSpellLevel}";
                }
                else if (isCaster)
                {
                    // Caster but no spells yet (e.g., Paladin 1-3, Ranger 1-3)
                    radio.Content = $"{className} ({classEntry.ClassLevel}) - No spells";
                }
                else
                {
                    radio.Content = $"{className} ({classEntry.ClassLevel})";
                }

                radio.IsEnabled = isCaster && maxSpellLevel >= 0;
                radio.IsVisible = true;

                // Select first enabled caster class by default
                if (!foundCaster && isCaster && maxSpellLevel >= 0)
                {
                    radio.IsChecked = true;
                    _selectedClassIndex = i;
                    foundCaster = true;
                }
            }
            else
            {
                radio.Content = $"Class {i + 1}";
                radio.IsEnabled = false;
                radio.IsVisible = i < 3; // Show first 3 placeholders
            }
        }

        // If no caster class found, select first anyway (even if disabled)
        if (!foundCaster && _classRadios[0] != null)
            _classRadios[0].IsChecked = true;
    }

    private void LoadSpellsForClass(int classIndex)
    {
        _allSpells.Clear();
        _knownSpellIds.Clear();
        _memorizedSpellIds.Clear();

        if (_currentCreature == null || _displayService == null)
        {
            ApplyFilters();
            UpdateSummary();
            return;
        }

        if (classIndex >= _currentCreature.ClassList.Count)
        {
            ApplyFilters();
            UpdateSummary();
            return;
        }

        var classEntry = _currentCreature.ClassList[classIndex];

        // NOTE: Spell lists (KnownList0-9, MemorizedList0-9) are stored per-class
        // but not yet parsed in our current UtcFile implementation.
        // For now, we show all available spells for the class without
        // marking known/memorized status. This is read-only.
        // TODO: Add spell list parsing to UtcFile and CreatureClass

        // Load all spells from spells.2da
        LoadAllSpells(classEntry.Class);

        // Apply filters
        ApplyFilters();

        // Update summary
        UpdateSummary();
    }

    private void LoadAllSpells(int classId)
    {
        if (_displayService == null) return;

        var allSpellIds = _displayService.GetAllSpellIds();

        foreach (var spellId in allSpellIds)
        {
            var vm = CreateSpellViewModel(spellId, classId);
            if (vm != null)
                _allSpells.Add(vm);
        }

        // Sort by name
        _allSpells = _allSpells.OrderBy(s => s.SpellName).ToList();
    }

    private SpellListViewModel? CreateSpellViewModel(int spellId, int classId)
    {
        if (_displayService == null) return null;

        var spellName = _displayService.GetSpellName(spellId);
        var spellInfo = _displayService.GetSpellInfo(spellId);

        if (spellInfo == null)
        {
            // Basic fallback if no spell info available
            return new SpellListViewModel
            {
                SpellId = spellId,
                SpellName = spellName,
                SpellLevel = 0,
                SpellLevelDisplay = "?",
                InnateLevel = 0,
                InnateLevelDisplay = "?",
                School = SpellSchool.Unknown,
                SchoolName = "Unknown",
                IsKnown = _knownSpellIds.Contains(spellId),
                IsMemorized = _memorizedSpellIds.Contains(spellId),
                IsBlocked = false,
                BlockedReason = "",
                Description = spellName
            };
        }

        // Get spell level for this class
        int spellLevel = spellInfo.GetLevelForClass(classId);
        bool isAvailableToClass = spellLevel >= 0;

        var isKnown = _knownSpellIds.Contains(spellId);
        var isMemorized = _memorizedSpellIds.Contains(spellId);
        var isBlocked = !isAvailableToClass;
        var blockedReason = isBlocked ? "Not available to this class" : "";

        // Determine status display
        string statusText;
        IBrush statusColor;
        IBrush rowBackground;
        double textOpacity;

        if (isBlocked)
        {
            statusText = "Blocked";
            statusColor = new SolidColorBrush(Colors.Gray);
            rowBackground = new SolidColorBrush(Color.FromArgb(20, 128, 128, 128));
            textOpacity = 0.5;
        }
        else if (isMemorized)
        {
            statusText = "Memorized";
            statusColor = new SolidColorBrush(Colors.Gold);
            rowBackground = new SolidColorBrush(Color.FromArgb(30, 255, 215, 0));
            textOpacity = 1.0;
        }
        else if (isKnown)
        {
            statusText = "Known";
            statusColor = new SolidColorBrush(Colors.Green);
            rowBackground = new SolidColorBrush(Color.FromArgb(30, 0, 128, 0));
            textOpacity = 1.0;
        }
        else
        {
            statusText = "";
            statusColor = Brushes.Transparent;
            rowBackground = Brushes.Transparent;
            textOpacity = 0.7;
        }

        return new SpellListViewModel
        {
            SpellId = spellId,
            SpellName = spellName,
            SpellLevel = spellLevel >= 0 ? spellLevel : spellInfo.InnateLevel,
            SpellLevelDisplay = spellLevel >= 0 ? spellLevel.ToString() : "-",
            InnateLevel = spellInfo.InnateLevel,
            InnateLevelDisplay = spellInfo.InnateLevel.ToString(),
            School = spellInfo.School,
            SchoolName = GetSchoolName(spellInfo.School),
            IsKnown = isKnown,
            IsMemorized = isMemorized,
            IsBlocked = isBlocked,
            BlockedReason = blockedReason,
            Description = BuildTooltip(spellName, spellInfo, blockedReason),
            StatusText = statusText,
            StatusColor = statusColor,
            RowBackground = rowBackground,
            TextOpacity = textOpacity
        };
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
        _displayedSpells.Clear();
        foreach (var spell in filtered)
        {
            _displayedSpells.Add(spell);
        }

        // Show "no spells" message if empty
        if (_noSpellsText != null)
            _noSpellsText.IsVisible = _displayedSpells.Count == 0;

        UpdateSummary();
    }

    private void UpdateSummary()
    {
        var knownCount = _knownSpellIds.Count;
        var memorizedCount = _memorizedSpellIds.Count;
        var displayedCount = _displayedSpells.Count;
        var totalCount = _allSpells.Count;

        var filterNote = displayedCount < totalCount
            ? $" (showing {displayedCount} of {totalCount})"
            : "";

        SetText(_spellsSummaryText,
            $"Known: {knownCount} | Memorized: {memorizedCount}{filterNote}");
    }

    public void ClearPanel()
    {
        _displayedSpells.Clear();
        _allSpells.Clear();
        _knownSpellIds.Clear();
        _memorizedSpellIds.Clear();
        _currentCreature = null;
        _selectedClassIndex = 0;

        SetText(_spellsSummaryText, "No spells loaded");

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
}

/// <summary>
/// View model for a spell in the spells list.
/// </summary>
public class SpellListViewModel
{
    public int SpellId { get; set; }
    public string SpellName { get; set; } = "";
    public int SpellLevel { get; set; }
    public string SpellLevelDisplay { get; set; } = "";
    public int InnateLevel { get; set; }
    public string InnateLevelDisplay { get; set; } = "";
    public SpellSchool School { get; set; }
    public string SchoolName { get; set; } = "";
    public bool IsKnown { get; set; }
    public bool IsMemorized { get; set; }
    public bool IsBlocked { get; set; }
    public string BlockedReason { get; set; } = "";
    public string Description { get; set; } = "";
    public string StatusText { get; set; } = "";
    public IBrush StatusColor { get; set; } = Brushes.Transparent;
    public IBrush RowBackground { get; set; } = Brushes.Transparent;
    public double TextOpacity { get; set; } = 1.0;
}
