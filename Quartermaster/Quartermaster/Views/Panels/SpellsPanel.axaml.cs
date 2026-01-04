using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Quartermaster.Services;
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
    private TextBlock? _spellsSummaryText;
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
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

        // Find UI controls
        _spellsSummaryText = this.FindControl<TextBlock>("SpellsSummaryText");
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

    private void OnClassRadioChecked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_isLoading) return;

        if (sender is RadioButton radio && radio.IsChecked == true)
        {
            var index = Array.IndexOf(_classRadios, radio);
            if (index >= 0 && index != _selectedClassIndex)
            {
                _isLoading = true;
                _selectedClassIndex = index;
                // Reload spells for the selected class
                if (_currentCreature != null)
                    LoadSpellsForClass(_selectedClassIndex);
                _isLoading = false;
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

    /// <summary>
    /// Sets the icon service for loading spell icons.
    /// </summary>
    public void SetIconService(ItemIconService iconService)
    {
        _itemIconService = iconService;
        UnifiedLogger.LogUI(LogLevel.INFO, $"SpellsPanel: IconService set, IsGameDataAvailable={iconService?.IsGameDataAvailable}");
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

        // Prevent dirty marking during load
        _isLoading = true;

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

        _isLoading = false;
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

                // Check if this class actually has spells in the creature data
                bool hasSpellsInData = classEntry.KnownSpells.Any(list => list.Count > 0) ||
                                       classEntry.MemorizedSpells.Any(list => list.Count > 0);

                // Format: "Wizard (10) - Lvl 5" or "Fighter (5)" for non-casters
                if (isCaster && maxSpellLevel >= 0)
                {
                    radio.Content = $"{className} ({classEntry.ClassLevel}) - Lvl {maxSpellLevel}";
                }
                else if (isCaster || hasSpellsInData)
                {
                    // Caster class or has spell data
                    radio.Content = $"{className} ({classEntry.ClassLevel})";
                }
                else
                {
                    radio.Content = $"{className} ({classEntry.ClassLevel})";
                }

                // Enable if: detected as caster with spells, OR has actual spell data in creature
                radio.IsEnabled = (isCaster && maxSpellLevel >= 0) || hasSpellsInData;
                radio.IsVisible = true;

                // Select first enabled caster class by default
                if (!foundCaster && radio.IsEnabled)
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
        _displayedSpells.Clear();
        _allSpells.Clear();
        _knownSpellIds.Clear();
        _memorizedSpellIds.Clear();
        _isSpontaneousCaster = false;

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

        // Check if this is a spontaneous caster (Sorcerer, Bard)
        _isSpontaneousCaster = _displayService.IsSpontaneousCaster(classEntry.Class);

        // Populate known spell IDs from parsed KnownList0-9
        for (int level = 0; level < 10; level++)
        {
            foreach (var spell in classEntry.KnownSpells[level])
            {
                _knownSpellIds.Add(spell.Spell);
            }
        }

        // Populate memorized spell IDs from parsed MemorizedList0-9
        for (int level = 0; level < 10; level++)
        {
            foreach (var spell in classEntry.MemorizedSpells[level])
            {
                _memorizedSpellIds.Add(spell.Spell);
            }
        }

        // Load all spells from spells.2da
        LoadAllSpells(classEntry.Class);

        UnifiedLogger.LogUI(LogLevel.INFO, $"SpellsPanel.LoadSpellsForClass: About to ApplyFilters...");

        // Apply filters
        ApplyFilters();

        UnifiedLogger.LogUI(LogLevel.INFO, $"SpellsPanel.LoadSpellsForClass: About to UpdateSummary...");

        // Update summary
        UpdateSummary();

        UnifiedLogger.LogUI(LogLevel.INFO, $"SpellsPanel.LoadSpellsForClass: Done, returning to caller");
    }

    private void LoadAllSpells(int classId)
    {
        if (_displayService == null) return;

        var allSpellIds = _displayService.GetAllSpellIds();
        UnifiedLogger.LogUI(LogLevel.INFO, $"SpellsPanel.LoadAllSpells: Loading {allSpellIds.Count} spells for class {classId}");

        int count = 0;
        foreach (var spellId in allSpellIds)
        {
            var vm = CreateSpellViewModel(spellId, classId);
            if (vm != null)
                _allSpells.Add(vm);
            count++;
            if (count % 100 == 0)
            {
                UnifiedLogger.LogUI(LogLevel.INFO, $"SpellsPanel.LoadAllSpells: Processed {count}/{allSpellIds.Count} spells");
            }
        }

        UnifiedLogger.LogUI(LogLevel.INFO, $"SpellsPanel.LoadAllSpells: Finished loading {_allSpells.Count} valid spells, now sorting...");

        // Sort by name
        _allSpells = _allSpells.OrderBy(s => s.SpellName).ToList();

        UnifiedLogger.LogUI(LogLevel.INFO, $"SpellsPanel.LoadAllSpells: Done sorting");
    }

    private SpellListViewModel? CreateSpellViewModel(int spellId, int classId)
    {
        if (_displayService == null) return null;

        var spellName = _displayService.GetSpellName(spellId);
        var spellInfo = _displayService.GetSpellInfo(spellId);

        if (spellInfo == null)
        {
            // Basic fallback if no spell info available
            var fallbackVm = new SpellListViewModel
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
                IsSpontaneousCaster = _isSpontaneousCaster,
                BlockedReason = "",
                Description = spellName
            };
            fallbackVm.OnKnownChanged = OnSpellKnownChanged;
            fallbackVm.OnMemorizedChanged = OnSpellMemorizedChanged;
            LoadSpellIcon(fallbackVm);
            return fallbackVm;
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

        var vm = new SpellListViewModel
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
            IsSpontaneousCaster = _isSpontaneousCaster,
            BlockedReason = blockedReason,
            Description = BuildTooltip(spellName, spellInfo, blockedReason),
            StatusText = statusText,
            StatusColor = statusColor,
            RowBackground = rowBackground,
            TextOpacity = textOpacity
        };

        // Wire up change handlers
        vm.OnKnownChanged = OnSpellKnownChanged;
        vm.OnMemorizedChanged = OnSpellMemorizedChanged;

        // Load spell icon if available
        LoadSpellIcon(vm);

        return vm;
    }

    /// <summary>
    /// Loads the game icon for a spell from spells.2da IconResRef.
    /// Icons are loaded lazily when binding requests them.
    /// </summary>
    private void LoadSpellIcon(SpellListViewModel spellVm)
    {
        // Don't load upfront - use lazy loading via IconBitmap getter
        // This prevents loading 467+ bitmaps at once which crashes Avalonia
        spellVm.SetIconService(_itemIconService);
    }

    private void OnSpellKnownChanged(SpellListViewModel spell, bool isNowKnown)
    {
        if (_isLoading || _currentCreature == null) return;
        if (_selectedClassIndex >= _currentCreature.ClassList.Count) return;

        var classEntry = _currentCreature.ClassList[_selectedClassIndex];

        if (isNowKnown)
        {
            // Add to known spells
            if (!_knownSpellIds.Contains(spell.SpellId))
            {
                _knownSpellIds.Add(spell.SpellId);

                // Add to model at appropriate spell level
                classEntry.KnownSpells[spell.SpellLevel].Add(new KnownSpell
                {
                    Spell = (ushort)spell.SpellId,
                    SpellFlags = 0x01,
                    SpellMetaMagic = 0
                });
            }
        }
        else
        {
            // Remove from known spells
            _knownSpellIds.Remove(spell.SpellId);

            // Remove from model
            var knownList = classEntry.KnownSpells[spell.SpellLevel];
            var existing = knownList.FirstOrDefault(s => s.Spell == spell.SpellId);
            if (existing != null)
            {
                knownList.Remove(existing);
            }

            // Also remove from memorized if it was memorized
            if (_memorizedSpellIds.Contains(spell.SpellId))
            {
                _memorizedSpellIds.Remove(spell.SpellId);
                var memorizedList = classEntry.MemorizedSpells[spell.SpellLevel];
                var memorized = memorizedList.FirstOrDefault(s => s.Spell == spell.SpellId);
                if (memorized != null)
                {
                    memorizedList.Remove(memorized);
                }
                spell.IsMemorized = false;
            }
        }

        // Update visual status
        UpdateSpellVisualStatus(spell);

        // Notify that spells changed
        SpellsChanged?.Invoke(this, EventArgs.Empty);

        // Update summary
        UpdateSummary();
    }

    private void OnSpellMemorizedChanged(SpellListViewModel spell, bool isNowMemorized)
    {
        if (_isLoading || _currentCreature == null) return;
        if (_selectedClassIndex >= _currentCreature.ClassList.Count) return;

        var classEntry = _currentCreature.ClassList[_selectedClassIndex];

        if (isNowMemorized)
        {
            // Add to memorized spells
            if (!_memorizedSpellIds.Contains(spell.SpellId))
            {
                _memorizedSpellIds.Add(spell.SpellId);

                // Add to model at appropriate spell level
                classEntry.MemorizedSpells[spell.SpellLevel].Add(new MemorizedSpell
                {
                    Spell = (ushort)spell.SpellId,
                    SpellFlags = 0x01,
                    SpellMetaMagic = 0,
                    Ready = 1
                });
            }
        }
        else
        {
            // Remove from memorized spells
            _memorizedSpellIds.Remove(spell.SpellId);

            // Remove from model
            var memorizedList = classEntry.MemorizedSpells[spell.SpellLevel];
            var existing = memorizedList.FirstOrDefault(s => s.Spell == spell.SpellId);
            if (existing != null)
            {
                memorizedList.Remove(existing);
            }
        }

        // Update visual status
        UpdateSpellVisualStatus(spell);

        // Notify that spells changed
        SpellsChanged?.Invoke(this, EventArgs.Empty);

        // Update summary
        UpdateSummary();
    }

    private void UpdateSpellVisualStatus(SpellListViewModel spell)
    {
        // Update status based on new known/memorized state
        bool isKnown = spell.IsKnown;
        bool isMemorized = _memorizedSpellIds.Contains(spell.SpellId);

        if (spell.IsBlocked)
        {
            spell.StatusText = "Blocked";
            spell.StatusColor = new SolidColorBrush(Colors.Gray);
            spell.RowBackground = new SolidColorBrush(Color.FromArgb(20, 128, 128, 128));
            spell.TextOpacity = 0.5;
        }
        else if (isKnown && isMemorized)
        {
            spell.StatusText = "K + M";
            spell.StatusColor = new SolidColorBrush(Colors.Gold);
            spell.RowBackground = new SolidColorBrush(Color.FromArgb(30, 255, 215, 0));
            spell.TextOpacity = 1.0;
        }
        else if (isMemorized)
        {
            // Memorized but not known (edge case - shouldn't happen normally)
            spell.StatusText = "Memorized";
            spell.StatusColor = new SolidColorBrush(Colors.Gold);
            spell.RowBackground = new SolidColorBrush(Color.FromArgb(30, 255, 215, 0));
            spell.TextOpacity = 1.0;
        }
        else if (isKnown)
        {
            spell.StatusText = "Known";
            spell.StatusColor = new SolidColorBrush(Colors.Green);
            spell.RowBackground = new SolidColorBrush(Color.FromArgb(30, 0, 128, 0));
            spell.TextOpacity = 1.0;
        }
        else
        {
            spell.StatusText = "";
            spell.StatusColor = Brushes.Transparent;
            spell.RowBackground = Brushes.Transparent;
            spell.TextOpacity = 0.7;
        }
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

        // Update spell slot summary
        UpdateSpellSlotSummary();
    }

    private void UpdateSpellSlotSummary()
    {
        if (_spellSlotSummaryText == null) return;

        if (_currentCreature == null || _displayService == null ||
            _selectedClassIndex >= _currentCreature.ClassList.Count)
        {
            _spellSlotSummaryText.Text = "";
            return;
        }

        var classEntry = _currentCreature.ClassList[_selectedClassIndex];
        var slots = _displayService.GetSpellSlots(classEntry.Class, classEntry.ClassLevel);

        if (slots == null)
        {
            _spellSlotSummaryText.Text = "No spell slots for this class";
            return;
        }

        var summaryParts = new List<string>();

        for (int level = 0; level <= 9; level++)
        {
            int totalSlots = slots[level];
            if (totalSlots <= 0) continue;

            // Count known spells at this level
            var knownAtLevel = classEntry.KnownSpells[level];
            int usedSlots = knownAtLevel.Count;

            // Get spell names for this level
            var spellNames = new List<string>();
            foreach (var spell in knownAtLevel)
            {
                var name = _displayService.GetSpellName(spell.Spell);
                if (!string.IsNullOrEmpty(name))
                    spellNames.Add(name);
            }

            // Format: "Lvl 3: 2/3 (Fireball, Haste)" or "Lvl 3: 0/3"
            var levelSummary = $"Lvl {level}: {usedSlots}/{totalSlots}";
            if (spellNames.Count > 0)
            {
                // Truncate long lists
                var displayNames = spellNames.Count > 3
                    ? string.Join(", ", spellNames.Take(3)) + $" +{spellNames.Count - 3}"
                    : string.Join(", ", spellNames);
                levelSummary += $" ({displayNames})";
            }

            summaryParts.Add(levelSummary);
        }

        if (summaryParts.Count == 0)
        {
            _spellSlotSummaryText.Text = "No spell slots available at current level";
        }
        else
        {
            _spellSlotSummaryText.Text = string.Join(" | ", summaryParts);
        }
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
}

/// <summary>
/// View model for a spell in the spells list.
/// </summary>
public class SpellListViewModel : System.ComponentModel.INotifyPropertyChanged
{
    private bool _isKnown;
    private bool _isMemorized;
    private string _statusText = "";
    private IBrush _statusColor = Brushes.Transparent;
    private IBrush _rowBackground = Brushes.Transparent;
    private double _textOpacity = 1.0;
    private Bitmap? _iconBitmap;
    private bool _iconLoaded = false;
    private ItemIconService? _iconService;

    public int SpellId { get; set; }

    /// <summary>
    /// Sets the icon service for lazy loading.
    /// </summary>
    public void SetIconService(ItemIconService? iconService)
    {
        _iconService = iconService;
    }

    /// <summary>
    /// Game icon for this spell (from spells.2da IconResRef).
    /// Loaded lazily on first access.
    /// </summary>
    public Bitmap? IconBitmap
    {
        get
        {
            // Lazy load on first access
            if (!_iconLoaded && _iconService != null && _iconService.IsGameDataAvailable)
            {
                _iconLoaded = true;
                try
                {
                    _iconBitmap = _iconService.GetSpellIcon(SpellId);
                }
                catch
                {
                    // Silently fail - no icon
                }
            }
            return _iconBitmap;
        }
        set
        {
            if (_iconBitmap != value)
            {
                _iconBitmap = value;
                _iconLoaded = true;
                OnPropertyChanged(nameof(IconBitmap));
                OnPropertyChanged(nameof(HasGameIcon));
            }
        }
    }

    /// <summary>
    /// Whether we have a real game icon (not placeholder).
    /// Returns true if icon service is available (assumes icon exists to avoid triggering load).
    /// </summary>
    public bool HasGameIcon => _iconService != null && _iconService.IsGameDataAvailable;
    public string SpellName { get; set; } = "";
    public int SpellLevel { get; set; }
    public string SpellLevelDisplay { get; set; } = "";
    public int InnateLevel { get; set; }
    public string InnateLevelDisplay { get; set; } = "";
    public SpellSchool School { get; set; }
    public string SchoolName { get; set; } = "";

    public bool IsKnown
    {
        get => _isKnown;
        set
        {
            if (_isKnown != value)
            {
                _isKnown = value;
                OnPropertyChanged(nameof(IsKnown));
                OnPropertyChanged(nameof(KnownTooltip));
                OnPropertyChanged(nameof(CanToggleMemorized));
                OnPropertyChanged(nameof(MemorizedTooltip));
                OnKnownChanged?.Invoke(this, value);
            }
        }
    }

    public bool IsMemorized
    {
        get => _isMemorized;
        set
        {
            if (_isMemorized != value)
            {
                _isMemorized = value;
                OnPropertyChanged(nameof(IsMemorized));
                OnPropertyChanged(nameof(MemorizedTooltip));
                OnMemorizedChanged?.Invoke(this, value);
            }
        }
    }

    public bool IsBlocked { get; set; }
    public bool IsSpontaneousCaster { get; set; }
    public string BlockedReason { get; set; } = "";
    public string Description { get; set; } = "";

    public string StatusText
    {
        get => _statusText;
        set
        {
            if (_statusText != value)
            {
                _statusText = value;
                OnPropertyChanged(nameof(StatusText));
            }
        }
    }

    public IBrush StatusColor
    {
        get => _statusColor;
        set
        {
            if (_statusColor != value)
            {
                _statusColor = value;
                OnPropertyChanged(nameof(StatusColor));
            }
        }
    }

    public IBrush RowBackground
    {
        get => _rowBackground;
        set
        {
            if (_rowBackground != value)
            {
                _rowBackground = value;
                OnPropertyChanged(nameof(RowBackground));
            }
        }
    }

    public double TextOpacity
    {
        get => _textOpacity;
        set
        {
            if (Math.Abs(_textOpacity - value) > 0.001)
            {
                _textOpacity = value;
                OnPropertyChanged(nameof(TextOpacity));
            }
        }
    }

    /// <summary>
    /// Whether the Known checkbox can be toggled (not blocked).
    /// </summary>
    public bool CanToggleKnown => !IsBlocked;

    /// <summary>
    /// Whether the Memorized checkbox can be toggled.
    /// Must be known, not blocked, and not a spontaneous caster.
    /// </summary>
    public bool CanToggleMemorized => !IsBlocked && IsKnown && !IsSpontaneousCaster;

    /// <summary>
    /// Tooltip for the Known checkbox.
    /// </summary>
    public string KnownTooltip => IsBlocked
        ? BlockedReason
        : (IsKnown ? "Click to remove from known spells" : "Click to add to known spells");

    /// <summary>
    /// Tooltip for the Memorized checkbox.
    /// </summary>
    public string MemorizedTooltip
    {
        get
        {
            if (IsBlocked) return BlockedReason;
            if (IsSpontaneousCaster) return "Spontaneous casters don't memorize spells";
            if (!IsKnown) return "Must know spell before memorizing";
            return IsMemorized ? "Click to remove from memorized spells" : "Click to memorize spell";
        }
    }

    /// <summary>
    /// Callback when IsKnown changes. Args: (SpellListViewModel spell, bool newValue)
    /// </summary>
    public Action<SpellListViewModel, bool>? OnKnownChanged { get; set; }

    /// <summary>
    /// Callback when IsMemorized changes. Args: (SpellListViewModel spell, bool newValue)
    /// </summary>
    public Action<SpellListViewModel, bool>? OnMemorizedChanged { get; set; }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
    }
}
