using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Quartermaster.Views.Panels;

public partial class SpellsPanel
{
    private void UpdateSummary()
    {
        // Update all caster classes summary
        UpdateAllClassesSummary();

        // Update known spells list
        UpdateKnownSpellsList();

        // Update memorized spells table
        UpdateMemorizedSpellsTable();

        // Update selected class spell slot summary
        UpdateSpellSlotSummary();
    }

    /// <summary>
    /// Updates the Known Spells table on the left side.
    /// Shows spontaneous casters (Sorcerer, Bard) with their known spells count vs. limit.
    /// Prepared casters are shown in the Memorized Spells table instead.
    /// </summary>
    private void UpdateAllClassesSummary()
    {
        if (_spellSlotTableGrid == null || _spellSlotTableBorder == null) return;

        // Clear existing content
        _spellSlotTableGrid.Children.Clear();
        _spellSlotTableGrid.ColumnDefinitions.Clear();
        _spellSlotTableGrid.RowDefinitions.Clear();

        if (_currentCreature == null || _displayService == null)
        {
            _spellSlotTableBorder.IsVisible = false;
            return;
        }

        // Get theme-aware font sizes
        var normalFontSize = this.FindResource("FontSizeNormal") as double? ?? 14;

        var spontaneousCasters = new List<(int classIndex, string className, int classLevel, int[] limits, Radoub.Formats.Utc.CreatureClass classEntry)>();

        // Find only SPONTANEOUS caster classes (Sorcerer, Bard)
        // Prepared casters (Wizard, Cleric, etc.) go in the Memorized Spells table
        for (int i = 0; i < _currentCreature.ClassList.Count; i++)
        {
            var classEntry = _currentCreature.ClassList[i];
            var className = _displayService.GetClassName(classEntry.Class) ?? $"Class {classEntry.Class}";
            var isSpontaneous = _displayService.IsSpontaneousCaster(classEntry.Class);

            if (!isSpontaneous)
                continue;  // Skip prepared casters - they go in Memorized table

            // For spontaneous casters, use spells known limit from cls_spkn_*.2da
            var limits = _displayService.GetSpellsKnownLimit(classEntry.Class, classEntry.ClassLevel);

            // Check if this class has spell limits or has known spell data
            bool hasSpellLimits = limits != null && limits.Any(s => s > 0);
            bool hasSpellData = classEntry.KnownSpells.Any(list => list.Count > 0);

            if (hasSpellLimits || hasSpellData)
            {
                spontaneousCasters.Add((i, className, classEntry.ClassLevel, limits ?? new int[10], classEntry));
            }
        }

        if (spontaneousCasters.Count == 0)
        {
            _spellSlotTableBorder.IsVisible = false;
            return;
        }

        _spellSlotTableBorder.IsVisible = true;

        // Find all spell levels that have limits across any spontaneous class
        var activeLevels = new List<int>();
        for (int level = 0; level <= 9; level++)
        {
            if (spontaneousCasters.Any(c => c.limits[level] > 0))
            {
                activeLevels.Add(level);
            }
        }

        if (activeLevels.Count == 0)
        {
            _spellSlotTableBorder.IsVisible = false;
            return;
        }

        // Build grid structure: first column for level labels, then one column per class
        // Row 0 = header row with class names
        // Rows 1+ = spell levels

        // Add column definitions: Label column + one per class
        _spellSlotTableGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        foreach (var _ in spontaneousCasters)
        {
            _spellSlotTableGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
        }

        // Add row definitions: header + one per active level
        _spellSlotTableGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto)); // Header
        foreach (var _ in activeLevels)
        {
            _spellSlotTableGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        }

        // Add header row - "Lvl" label in first column
        var lvlHeader = new TextBlock
        {
            Text = "Lvl",
            FontWeight = Avalonia.Media.FontWeight.SemiBold,
            FontSize = normalFontSize,
            Margin = new Avalonia.Thickness(0, 0, 10, 4),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
        };
        Grid.SetRow(lvlHeader, 0);
        Grid.SetColumn(lvlHeader, 0);
        _spellSlotTableGrid.Children.Add(lvlHeader);

        // Add class name headers
        for (int col = 0; col < spontaneousCasters.Count; col++)
        {
            var (classIndex, className, classLevel, _, _) = spontaneousCasters[col];
            var isSelected = classIndex == _selectedClassIndex;

            var classHeader = new TextBlock
            {
                Text = className,
                FontWeight = Avalonia.Media.FontWeight.SemiBold,
                FontSize = normalFontSize,
                Margin = new Avalonia.Thickness(4, 0, 4, 4),
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                Foreground = isSelected
                    ? GetInfoBrush()
                    : this.FindResource("SystemControlForegroundBaseHighBrush") as IBrush
                      ?? GetDisabledBrush()
            };
            Grid.SetRow(classHeader, 0);
            Grid.SetColumn(classHeader, col + 1);
            _spellSlotTableGrid.Children.Add(classHeader);
        }

        // Add data rows for each spell level
        for (int rowIdx = 0; rowIdx < activeLevels.Count; rowIdx++)
        {
            int spellLevel = activeLevels[rowIdx];
            int gridRow = rowIdx + 1; // +1 for header

            // Level label
            var levelLabel = new TextBlock
            {
                Text = spellLevel.ToString(),
                FontSize = normalFontSize,
                FontWeight = Avalonia.Media.FontWeight.SemiBold,
                Margin = new Avalonia.Thickness(0, 2, 10, 2),
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };
            Grid.SetRow(levelLabel, gridRow);
            Grid.SetColumn(levelLabel, 0);
            _spellSlotTableGrid.Children.Add(levelLabel);

            // Known spell counts for each spontaneous class
            for (int col = 0; col < spontaneousCasters.Count; col++)
            {
                var (classIndex, className, _, limits, classEntry) = spontaneousCasters[col];
                int totalLimit = limits[spellLevel];

                // Count known spells at this level
                // When ignoring class restrictions, count all known spells
                // Otherwise, only count spells valid for this class in spells.2da
                int usedCount = 0;
                foreach (var knownSpell in classEntry.KnownSpells[spellLevel])
                {
                    if (_ignoreClassRestrictions)
                    {
                        // Count all known spells when ignoring restrictions
                        usedCount++;
                    }
                    else
                    {
                        var spellInfo = _displayService.GetSpellInfo(knownSpell.Spell);
                        int classSpellLevel = spellInfo?.GetLevelForClass(classEntry.Class) ?? -1;

                        // Only count spells that are valid for this class (filter out feat-based abilities)
                        if (spellInfo != null && classSpellLevel >= 0)
                        {
                            usedCount++;
                        }
                    }
                }

                var isSelected = classIndex == _selectedClassIndex;

                string cellText;
                IBrush cellColor;

                if (totalLimit <= 0)
                {
                    cellText = "-";
                    cellColor = GetDisabledBrush();
                }
                else if (usedCount >= totalLimit)
                {
                    // Full - show in gold/yellow
                    cellText = $"{usedCount}/{totalLimit}";
                    cellColor = GetSelectionBrush();
                }
                else if (usedCount > 0)
                {
                    // Partial - show in green
                    cellText = $"{usedCount}/{totalLimit}";
                    cellColor = GetSuccessBrush();
                }
                else
                {
                    // Empty - use info brush for visibility on dark themes
                    cellText = $"0/{totalLimit}";
                    cellColor = GetInfoBrush();
                }

                var slotCell = new TextBlock
                {
                    Text = cellText,
                    FontSize = normalFontSize,
                    Margin = new Avalonia.Thickness(4, 2, 4, 2),
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    Foreground = cellColor
                };
                Grid.SetRow(slotCell, gridRow);
                Grid.SetColumn(slotCell, col + 1);
                _spellSlotTableGrid.Children.Add(slotCell);
            }
        }
    }

    /// <summary>
    /// Updates the known spells list panel below the spell slot table.
    /// Shows spell names grouped by class and level for ALL caster classes.
    /// Highlights overlapping spells that appear in multiple classes.
    /// </summary>
    private void UpdateKnownSpellsList()
    {
        if (_knownSpellsListPanel == null || _knownSpellsListBorder == null) return;

        _knownSpellsListPanel.Children.Clear();

        if (_currentCreature == null || _displayService == null)
        {
            _knownSpellsListBorder.IsVisible = false;
            return;
        }

        // Get theme-aware font sizes
        var normalFontSize = this.FindResource("FontSizeNormal") as double? ?? 14;

        // First pass: collect all known spells and count occurrences across classes
        var spellOccurrences = new Dictionary<int, List<string>>(); // spellId -> list of class names
        var casterClasses = new List<(int classIndex, string className, Radoub.Formats.Utc.CreatureClass classEntry)>();

        for (int i = 0; i < _currentCreature.ClassList.Count; i++)
        {
            var classEntry = _currentCreature.ClassList[i];
            var className = _displayService.GetClassName(classEntry.Class) ?? $"Class {classEntry.Class}";

            bool hasSpells = classEntry.KnownSpells.Any(list => list.Count > 0);
            if (!hasSpells) continue;

            casterClasses.Add((i, className, classEntry));

            // Track spell occurrences
            for (int level = 0; level <= 9; level++)
            {
                foreach (var spell in classEntry.KnownSpells[level])
                {
                    if (!spellOccurrences.ContainsKey(spell.Spell))
                        spellOccurrences[spell.Spell] = new List<string>();
                    spellOccurrences[spell.Spell].Add(className);
                }
            }
        }

        if (casterClasses.Count == 0)
        {
            _knownSpellsListBorder.IsVisible = false;
            return;
        }

        bool hasAnySpells = false;

        // Build spell list grouped by class, then by level
        foreach (var (classIndex, className, classEntry) in casterClasses)
        {
            bool classHasSpells = false;

            // Check if this class has any spells
            for (int level = 0; level <= 9; level++)
            {
                if (classEntry.KnownSpells[level].Count > 0)
                {
                    classHasSpells = true;
                    break;
                }
            }

            if (!classHasSpells) continue;

            hasAnySpells = true;
            var isSelectedClass = classIndex == _selectedClassIndex;

            // Class header
            var classHeader = new TextBlock
            {
                Text = className,
                FontWeight = Avalonia.Media.FontWeight.Bold,
                FontSize = normalFontSize,
                Foreground = isSelectedClass
                    ? GetInfoBrush()
                    : this.FindResource("SystemControlForegroundBaseHighBrush") as IBrush ?? GetDisabledBrush(),
                Margin = new Avalonia.Thickness(0, hasAnySpells && _knownSpellsListPanel.Children.Count > 0 ? 12 : 0, 0, 6)
            };
            _knownSpellsListPanel.Children.Add(classHeader);

            // Spells grouped by level
            for (int level = 0; level <= 9; level++)
            {
                var knownAtLevel = classEntry.KnownSpells[level];
                if (knownAtLevel.Count == 0) continue;

                // Level header - use info brush for better contrast on dark themes
                var levelHeader = new TextBlock
                {
                    Text = level == 0 ? "  Cantrips" : $"  Level {level}",
                    FontWeight = Avalonia.Media.FontWeight.SemiBold,
                    FontSize = normalFontSize,
                    Foreground = GetInfoBrush(),
                    Margin = new Avalonia.Thickness(0, 4, 0, 2)
                };
                _knownSpellsListPanel.Children.Add(levelHeader);

                // Spell names
                foreach (var spell in knownAtLevel)
                {
                    var spellName = _displayService.GetSpellName(spell.Spell);
                    var spellInfo = _displayService.GetSpellInfo(spell.Spell);
                    int classSpellLevel = spellInfo?.GetLevelForClass(classEntry.Class) ?? -1;

                    // Check if spell appears in multiple classes (overlap)
                    bool isOverlap = spellOccurrences.TryGetValue(spell.Spell, out var occurrences) && occurrences.Count > 1;

                    // Check memorization count and collect metamagic info
                    int memCount = 0;
                    var metamagicAbbrevs = new List<string>();
                    foreach (var memSpell in classEntry.MemorizedSpells[level])
                    {
                        if (memSpell.Spell == spell.Spell)
                        {
                            memCount++;
                            if (memSpell.SpellMetaMagic != 0)
                            {
                                metamagicAbbrevs.Add(GetMetamagicAbbreviation(memSpell.SpellMetaMagic));
                            }
                        }
                    }

                    // Show spell name with indicators
                    var displayName = spellName;
                    IBrush foreground;

                    // Add memorization count in parentheses if memorized, plus metamagic
                    if (memCount > 0)
                    {
                        var metamagicDisplay = metamagicAbbrevs.Count > 0
                            ? " " + string.Join(" ", metamagicAbbrevs.Distinct())
                            : "";
                        displayName = $"{spellName} ({memCount}){metamagicDisplay}";
                    }

                    if (classSpellLevel < 0 && !_ignoreClassRestrictions)
                    {
                        // Not a standard class spell (e.g., feat-based ability)
                        // Only mark with asterisk if not ignoring restrictions
                        displayName = memCount > 0 ? $"{spellName} ({memCount}) *" : $"{spellName} *";
                        foreground = GetDisabledBrush();
                    }
                    else if (memCount > 0)
                    {
                        // Memorized - highlight in gold
                        foreground = GetSelectionBrush();
                    }
                    else if (isOverlap)
                    {
                        // Spell appears in multiple classes - highlight in gold
                        displayName = $"{spellName} ⬥";
                        foreground = GetSelectionBrush();
                    }
                    else
                    {
                        foreground = this.FindResource("SystemControlForegroundBaseHighBrush") as IBrush ?? GetDisabledBrush();
                    }

                    var spellLabel = new TextBlock
                    {
                        Text = $"    {displayName}",
                        FontSize = normalFontSize,
                        Foreground = foreground,
                        Margin = new Avalonia.Thickness(0, 1, 0, 1)
                    };
                    _knownSpellsListPanel.Children.Add(spellLabel);
                }
            }
        }

        _knownSpellsListBorder.IsVisible = hasAnySpells;
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

    /// <summary>
    /// Updates the memorized spells table showing counts by spell level.
    /// Only shown for prepared casters (not spontaneous).
    /// </summary>
    private void UpdateMemorizedSpellsTable()
    {
        if (_memorizedSpellsTableGrid == null || _memorizedSpellsTableBorder == null) return;

        // Clear existing content
        _memorizedSpellsTableGrid.Children.Clear();
        _memorizedSpellsTableGrid.ColumnDefinitions.Clear();
        _memorizedSpellsTableGrid.RowDefinitions.Clear();

        if (_currentCreature == null || _displayService == null)
        {
            _memorizedSpellsTableBorder.IsVisible = false;
            return;
        }

        // Get theme-aware font sizes
        var normalFontSize = this.FindResource("FontSizeNormal") as double? ?? 14;

        // Collect memorized spells by class and level
        var classMemorized = new List<(int classIndex, string className, int[] memorizedCounts, bool isSpontaneous)>();

        for (int i = 0; i < _currentCreature.ClassList.Count; i++)
        {
            var classEntry = _currentCreature.ClassList[i];
            var className = _displayService.GetClassName(classEntry.Class) ?? $"Class {classEntry.Class}";
            var isSpontaneous = _displayService.IsSpontaneousCaster(classEntry.Class);

            // Count memorized spells by level - count ALL entries in the list
            var counts = new int[10];
            bool hasAnyMemorized = false;
            for (int level = 0; level <= 9; level++)
            {
                // Each entry in MemorizedSpells represents one memorized slot
                counts[level] = classEntry.MemorizedSpells[level].Count;
                if (counts[level] > 0) hasAnyMemorized = true;
            }

            // Only include if has memorized spells and is not spontaneous
            if (hasAnyMemorized && !isSpontaneous)
            {
                classMemorized.Add((i, className, counts, isSpontaneous));
            }
        }

        if (classMemorized.Count == 0)
        {
            _memorizedSpellsTableBorder.IsVisible = false;
            return;
        }

        _memorizedSpellsTableBorder.IsVisible = true;

        // Find active levels (levels with any memorized spells)
        var activeLevels = new List<int>();
        for (int level = 0; level <= 9; level++)
        {
            if (classMemorized.Any(c => c.memorizedCounts[level] > 0))
            {
                activeLevels.Add(level);
            }
        }

        if (activeLevels.Count == 0)
        {
            _memorizedSpellsTableBorder.IsVisible = false;
            return;
        }

        // Build grid: Level column + one per class
        _memorizedSpellsTableGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        foreach (var _ in classMemorized)
        {
            _memorizedSpellsTableGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
        }

        // Row definitions: header + one per active level
        _memorizedSpellsTableGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        foreach (var _ in activeLevels)
        {
            _memorizedSpellsTableGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        }

        // Header row
        var lvlHeader = new TextBlock
        {
            Text = "Lvl",
            FontWeight = Avalonia.Media.FontWeight.SemiBold,
            FontSize = normalFontSize,
            Margin = new Avalonia.Thickness(0, 0, 10, 4),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
        };
        Grid.SetRow(lvlHeader, 0);
        Grid.SetColumn(lvlHeader, 0);
        _memorizedSpellsTableGrid.Children.Add(lvlHeader);

        // Class name headers
        for (int col = 0; col < classMemorized.Count; col++)
        {
            var (classIndex, className, _, _) = classMemorized[col];
            var isSelected = classIndex == _selectedClassIndex;

            var classHeader = new TextBlock
            {
                Text = className,
                FontWeight = Avalonia.Media.FontWeight.SemiBold,
                FontSize = normalFontSize,
                Margin = new Avalonia.Thickness(4, 0, 4, 4),
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                Foreground = isSelected
                    ? GetInfoBrush()
                    : this.FindResource("SystemControlForegroundBaseHighBrush") as IBrush
                      ?? GetDisabledBrush()
            };
            Grid.SetRow(classHeader, 0);
            Grid.SetColumn(classHeader, col + 1);
            _memorizedSpellsTableGrid.Children.Add(classHeader);
        }

        // Data rows for each level
        for (int rowIdx = 0; rowIdx < activeLevels.Count; rowIdx++)
        {
            int spellLevel = activeLevels[rowIdx];
            int gridRow = rowIdx + 1;

            // Level label
            var levelLabel = new TextBlock
            {
                Text = spellLevel.ToString(),
                FontSize = normalFontSize,
                FontWeight = Avalonia.Media.FontWeight.SemiBold,
                Margin = new Avalonia.Thickness(0, 2, 10, 2),
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };
            Grid.SetRow(levelLabel, gridRow);
            Grid.SetColumn(levelLabel, 0);
            _memorizedSpellsTableGrid.Children.Add(levelLabel);

            // Memorized counts for each class
            for (int col = 0; col < classMemorized.Count; col++)
            {
                var (classIndex, _, counts, _) = classMemorized[col];
                int count = counts[spellLevel];
                var isSelected = classIndex == _selectedClassIndex;

                IBrush cellColor;
                if (count > 0)
                {
                    cellColor = GetSelectionBrush();
                }
                else
                {
                    // Use info brush for visibility on dark themes
                    cellColor = GetInfoBrush();
                }

                var countCell = new TextBlock
                {
                    Text = count.ToString(),
                    FontSize = normalFontSize,
                    FontWeight = count > 0 ? Avalonia.Media.FontWeight.Bold : Avalonia.Media.FontWeight.Normal,
                    Margin = new Avalonia.Thickness(4, 2, 4, 2),
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    Foreground = cellColor
                };
                Grid.SetRow(countCell, gridRow);
                Grid.SetColumn(countCell, col + 1);
                _memorizedSpellsTableGrid.Children.Add(countCell);
            }
        }
    }

    /// <summary>
    /// Gets a short abbreviation for metamagic flags applied to a spell.
    /// </summary>
    private static string GetMetamagicAbbreviation(byte metamagic)
    {
        if (metamagic == 0) return "";

        var abbrev = new StringBuilder();
        if ((metamagic & 0x01) != 0) abbrev.Append("E");  // Empower
        if ((metamagic & 0x02) != 0) abbrev.Append("X");  // Extend
        if ((metamagic & 0x04) != 0) abbrev.Append("M");  // Maximize
        if ((metamagic & 0x08) != 0) abbrev.Append("Q");  // Quicken
        if ((metamagic & 0x10) != 0) abbrev.Append("S");  // Silent
        if ((metamagic & 0x20) != 0) abbrev.Append("T");  // Still

        return abbrev.Length > 0 ? $"[{abbrev}]" : "";
    }

    /// <summary>
    /// Gets the full name of a metamagic type.
    /// </summary>
    public static string GetMetamagicName(byte metamagic)
    {
        return metamagic switch
        {
            0x01 => "Empower Spell",
            0x02 => "Extend Spell",
            0x04 => "Maximize Spell",
            0x08 => "Quicken Spell",
            0x10 => "Silent Spell",
            0x20 => "Still Spell",
            _ => "None"
        };
    }
}
