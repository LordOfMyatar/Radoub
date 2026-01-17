using Avalonia.Media;
using Quartermaster.Services;
using Quartermaster.ViewModels;
using Radoub.Formats.Logging;
using Radoub.Formats.Utc;
using System.Collections.Generic;
using System.Linq;

namespace Quartermaster.Views.Panels;

public partial class SpellsPanel
{
    public void LoadCreature(UtcFile? creature)
    {
        _displayedSpells.Clear();
        _allSpells.Clear();
        _knownSpellIds.Clear();
        _memorizedSpellCounts.Clear();
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

        // Update class combo box based on creature's classes
        UpdateClassComboBox(creature);

        // Load spells for the first (selected) class
        LoadSpellsForClass(_selectedClassIndex);

        // Hide loading state
        if (_loadingText != null)
            _loadingText.IsVisible = false;

        _isLoading = false;
    }

    private void UpdateClassComboBox(UtcFile creature)
    {
        // Create a new list to ensure ComboBox detects the change
        var newItems = new List<ClassComboItem>();
        int firstEnabledIndex = -1;

        for (int i = 0; i < creature.ClassList.Count && i < 8; i++)
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
            string displayName;
            if (isCaster && maxSpellLevel >= 0)
            {
                displayName = $"{className} ({classEntry.ClassLevel}) - Lvl {maxSpellLevel}";
            }
            else
            {
                displayName = $"{className} ({classEntry.ClassLevel})";
            }

            // Enable if: detected as caster with spells, OR has actual spell data in creature
            bool isEnabled = (isCaster && maxSpellLevel >= 0) || hasSpellsInData;

            newItems.Add(new ClassComboItem
            {
                Index = i,
                DisplayName = displayName,
                IsEnabled = isEnabled
            });

            // Track first enabled class
            if (isEnabled && firstEnabledIndex < 0)
            {
                firstEnabledIndex = newItems.Count - 1;
            }
        }

        // Replace the items list and update combo box
        _classItems = newItems;
        if (_classComboBox != null)
        {
            // Clear and reassign to force refresh
            _classComboBox.ItemsSource = null;
            _classComboBox.ItemsSource = _classItems;

            // Select first enabled caster class, or first class if none enabled
            int selectedIndex = firstEnabledIndex >= 0 ? firstEnabledIndex : (_classItems.Count > 0 ? 0 : -1);
            if (selectedIndex >= 0)
            {
                _classComboBox.SelectedIndex = selectedIndex;
                _selectedClassIndex = _classItems[selectedIndex].Index;
            }
        }
    }

    private void LoadSpellsForClass(int classIndex)
    {
        _displayedSpells.Clear();
        _allSpells.Clear();
        _knownSpellIds.Clear();
        _memorizedSpellCounts.Clear();
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

        // Populate memorized spell counts from parsed MemorizedList0-9
        // Same spell can appear multiple times (memorized in multiple slots)
        for (int level = 0; level < 10; level++)
        {
            foreach (var spell in classEntry.MemorizedSpells[level])
            {
                if (_memorizedSpellCounts.ContainsKey(spell.Spell))
                    _memorizedSpellCounts[spell.Spell]++;
                else
                    _memorizedSpellCounts[spell.Spell] = 1;
            }
        }

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
        var memorizedCount = (_memorizedSpellCounts.TryGetValue(spellId, out var cnt) ? cnt : 0);

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
                MemorizedCount = memorizedCount,
                IsBlocked = false,
                IsSpontaneousCaster = _isSpontaneousCaster,
                BlockedReason = "",
                Description = spellName
            };
            fallbackVm.OnKnownChanged = OnSpellKnownChanged;
            fallbackVm.OnMemorizedCountChanged = OnSpellMemorizedCountChanged;
            UpdateMemorizedCountColor(fallbackVm);
            LoadSpellIcon(fallbackVm);
            return fallbackVm;
        }

        // Get spell level for this class
        int spellLevel = spellInfo.GetLevelForClass(classId);
        bool isAvailableToClass = spellLevel >= 0;

        var isKnown = _knownSpellIds.Contains(spellId);
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
            statusColor = GetDisabledBrush();
            rowBackground = GetTransparentRowBackground(statusColor, 20);
            textOpacity = 0.5;
        }
        else if (memorizedCount > 0)
        {
            statusText = memorizedCount > 1 ? $"M×{memorizedCount}" : "Memorized";
            statusColor = GetSelectionBrush();
            rowBackground = GetTransparentRowBackground(statusColor, 30);
            textOpacity = 1.0;
        }
        else if (isKnown)
        {
            statusText = "Known";
            statusColor = GetSuccessBrush();
            rowBackground = GetTransparentRowBackground(statusColor, 30);
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
            MemorizedCount = memorizedCount,
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
        vm.OnMemorizedCountChanged = OnSpellMemorizedCountChanged;

        // Set memorized count color
        UpdateMemorizedCountColor(vm);

        // Load spell icon if available
        LoadSpellIcon(vm);

        return vm;
    }

    private void UpdateMemorizedCountColor(SpellListViewModel vm)
    {
        if (vm.MemorizedCount > 0)
            vm.MemorizedCountColor = GetSelectionBrush();
        else if (vm.IsKnown && !vm.IsSpontaneousCaster)
            vm.MemorizedCountColor = GetDisabledBrush();
        else
            vm.MemorizedCountColor = GetDisabledBrush();
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
}
