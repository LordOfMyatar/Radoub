using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Quartermaster.Services;
using Radoub.Formats.Logging;
using Radoub.Formats.Services;
using Radoub.UI.Services;

namespace Quartermaster.Views.Dialogs;

/// <summary>
/// Step 8: Spell selection by level for caster classes.
/// </summary>
public partial class NewCharacterWizardWindow
{
    #region Step 8: Spells (was Step 7)

    private void PrepareStep8()
    {
        int classId = _selectedClassId >= 0 ? _selectedClassId : 0;
        bool isCaster = _displayService.IsCasterClass(classId);
        UnifiedLogger.Log(LogLevel.DEBUG, $"PrepareStep7: classId={classId}, isCaster={isCaster}", "NewCharWiz", "🧙");

        if (!isCaster)
        {
            _needsSpellSelection = false;
            _isDivineCaster = false;
            return;
        }

        bool isSpontaneous = _displayService.Spells.IsSpontaneousCaster(classId);
        _maxSpellLevelForClass = _displayService.Spells.GetMaxSpellLevel(classId, 1);
        _isDivineCaster = _displayService.IsDivineCaster(classId);
        UnifiedLogger.Log(LogLevel.DEBUG, $"PrepareStep7: isSpontaneous={isSpontaneous}, maxSpellLevel={_maxSpellLevelForClass}, isDivine={_isDivineCaster}", "NewCharWiz", "🧙");

        if (_maxSpellLevelForClass < 0)
        {
            _needsSpellSelection = false;
            return;
        }

        // Check if there are actually any spells to select at this level
        // (some casters like Paladins/Rangers have no spells at level 1)
        if (!_isDivineCaster)
        {
            bool hasSpellsToSelect = false;
            for (int level = 0; level <= _maxSpellLevelForClass; level++)
            {
                if (GetMaxSpellsForLevel(classId, level) > 0)
                {
                    hasSpellsToSelect = true;
                    break;
                }
            }

            if (!hasSpellsToSelect)
            {
                _needsSpellSelection = false;
                return;
            }
        }

        _needsSpellSelection = true;

        if (_isDivineCaster)
        {
            // Divine casters (Cleric, Druid) get spells automatically
            var className = _displayService.GetClassName(classId);
            var infoLines = new List<string>
            {
                $"As a {className}, your deity grants you access to all {className.ToLowerInvariant()} spells.",
                "You can prepare spells each day after resting."
            };

            // Show domain spell info for clerics
            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"NCW.PrepareStep8: isDivine=true classNeedsDomains={_classNeedsDomains}");
            if (_classNeedsDomains)
            {
                var d1Id = GetSelectedDomainId(_domain1ComboBox);
                var d2Id = GetSelectedDomainId(_domain2ComboBox);
                UnifiedLogger.LogApplication(LogLevel.INFO,
                    $"NCW.PrepareStep8: domain1Id={d1Id} domain2Id={d2Id}");
                var domainLines = BuildDomainSpellSummary(d1Id, d2Id);
                UnifiedLogger.LogApplication(LogLevel.INFO,
                    $"NCW.PrepareStep8: domainLines.Count={domainLines.Count}");
                if (domainLines.Count > 0)
                {
                    infoLines.Add("");
                    infoLines.Add("Your domains also grant additional spells:");
                    infoLines.AddRange(domainLines);
                }
            }

            _divineSpellInfoLabel.Text = string.Join("\n", infoLines);
            _divineSpellInfoPanel.IsVisible = true;

            // Hide the two-panel selection UI
            _spellSelectionTwoPanel.IsVisible = false;
            _spellLevelTabsPanel.IsVisible = false;
            _spellSelectionCountLabel.IsVisible = false;

            _spellStepDescription.Text = $"{className}s receive all their class spells automatically through divine power.";
            return;
        }

        // Spontaneous casters (Bard, Sorcerer) or Wizard: show spell selection UI
        _divineSpellInfoPanel.IsVisible = false;
        _spellSelectionTwoPanel.IsVisible = true;
        _spellLevelTabsPanel.IsVisible = true;
        _spellSelectionCountLabel.IsVisible = true;

        string classNameForDesc = _displayService.GetClassName(classId);
        if (isSpontaneous)
            _spellStepDescription.Text = $"Choose spells known for your {classNameForDesc}. These are the spells you can cast.";
        else
            _spellStepDescription.Text = $"Choose spells for your {classNameForDesc}'s spellbook.";

        // Initialize spell level selections if not already done
        if (!_step8Loaded)
        {
            _step8Loaded = true;
            _selectedSpellsByLevel.Clear();
        }

        // Build spell level tabs
        BuildSpellLevelTabs();

        // Default to level 0 (cantrips)
        _currentSpellLevel = 0;
        SelectSpellLevelTab(0);
    }

    private void BuildSpellLevelTabs()
    {
        _spellLevelTabsPanel.Children.Clear();

        for (int level = 0; level <= _maxSpellLevelForClass; level++)
        {
            var btn = new ToggleButton
            {
                Content = level == 0 ? "Cantrips" : $"Level {level}",
                Tag = level,
                Margin = new Avalonia.Thickness(0, 0, 2, 0),
                IsChecked = level == 0
            };
            btn.Click += OnSpellLevelTabClick;
            _spellLevelTabsPanel.Children.Add(btn);
        }
    }

    private void OnSpellLevelTabClick(object? sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton btn && btn.Tag is int level)
        {
            SelectSpellLevelTab(level);
        }
    }

    private void SelectSpellLevelTab(int level)
    {
        _currentSpellLevel = level;

        // Update tab checked states
        foreach (var child in _spellLevelTabsPanel.Children)
        {
            if (child is ToggleButton tb && tb.Tag is int tabLevel)
                tb.IsChecked = tabLevel == level;
        }

        // Load available spells for this level
        LoadAvailableSpellsForLevel(level);
        UpdateSelectedSpellsDisplay();
        UpdateSpellSelectionCount();
        ValidateCurrentStep();
    }

    private void LoadAvailableSpellsForLevel(int spellLevel)
    {
        int classId = _selectedClassId >= 0 ? _selectedClassId : 0;
        var allSpellIds = _displayService.Spells.GetAllSpellIds();
        var selectedForLevel = _selectedSpellsByLevel.GetValueOrDefault(spellLevel, new List<int>());

        _availableSpellsForLevel = new List<SpellDisplayItem>();

        foreach (var spellId in allSpellIds)
        {
            var info = _displayService.Spells.GetSpellInfo(spellId);
            if (info == null) continue;

            int levelForClass = info.GetLevelForClass(classId);
            if (levelForClass != spellLevel) continue;

            // Skip if already selected
            if (selectedForLevel.Contains(spellId)) continue;

            _availableSpellsForLevel.Add(new SpellDisplayItem
            {
                SpellId = spellId,
                Name = info.Name,
                SchoolAbbrev = GetSchoolAbbrev(info.School)
            });
        }

        _availableSpellsForLevel = _availableSpellsForLevel.OrderBy(s => s.Name).ToList();
        ApplySpellFilter();
    }

    private void ApplySpellFilter()
    {
        var filter = _spellSearchBox2?.Text?.Trim() ?? "";

        if (string.IsNullOrEmpty(filter))
        {
            _filteredAvailableSpells = new List<SpellDisplayItem>(_availableSpellsForLevel);
        }
        else
        {
            _filteredAvailableSpells = _availableSpellsForLevel
                .Where(s => s.Name.Contains(filter, System.StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        _availableSpellsListBox.ItemsSource = _filteredAvailableSpells;
    }

    private void OnSpellSearchChanged(object? sender, TextChangedEventArgs e)
    {
        ApplySpellFilter();
    }

    private void UpdateSelectedSpellsDisplay()
    {
        var selectedForLevel = _selectedSpellsByLevel.GetValueOrDefault(_currentSpellLevel, new List<int>());
        var items = selectedForLevel.Select(id =>
        {
            var info = _displayService.Spells.GetSpellInfo(id);
            return new SpellDisplayItem
            {
                SpellId = id,
                Name = info?.Name ?? $"Spell {id}",
                SchoolAbbrev = info != null ? GetSchoolAbbrev(info.School) : ""
            };
        }).OrderBy(s => s.Name).ToList();

        _selectedSpellsListBox.ItemsSource = items;

        // Update count for this level
        int classId = _selectedClassId >= 0 ? _selectedClassId : 0;
        int maxForLevel = GetMaxSpellsForLevel(classId, _currentSpellLevel);
        _selectedSpellCountLabel.Text = $"({selectedForLevel.Count} / {maxForLevel})";

        if (selectedForLevel.Count > maxForLevel)
            _selectedSpellCountLabel.Foreground = BrushManager.GetErrorBrush(this);
        else if (selectedForLevel.Count == maxForLevel)
            _selectedSpellCountLabel.ClearValue(TextBlock.ForegroundProperty);
        else
            _selectedSpellCountLabel.Foreground = BrushManager.GetSuccessBrush(this);
    }

    private void UpdateSpellSelectionCount()
    {
        int classId = _selectedClassId >= 0 ? _selectedClassId : 0;
        int totalSelected = 0;
        int totalRequired = 0;

        for (int level = 0; level <= _maxSpellLevelForClass; level++)
        {
            int selected = _selectedSpellsByLevel.GetValueOrDefault(level, new List<int>()).Count;
            int max = GetMaxSpellsForLevel(classId, level);
            totalSelected += selected;
            totalRequired += max;
        }

        _spellSelectionCountLabel.Text = $"Total: {totalSelected} / {totalRequired}";
    }

    private int GetMaxSpellsForLevel(int classId, int spellLevel)
    {
        bool isSpontaneous = _displayService.Spells.IsSpontaneousCaster(classId);

        if (isSpontaneous)
        {
            // Spontaneous casters: use SpellsKnownLimit
            var knownLimits = _displayService.Spells.GetSpellsKnownLimit(classId, 1);
            if (knownLimits != null && spellLevel < knownLimits.Length)
                return knownLimits[spellLevel];
            return 0;
        }
        else
        {
            // Wizard: use spell slots as guide for initial spellbook
            // At level 1, wizards get 3 + INT mod cantrips and all level 0 plus (3 + INT mod) level 1 spells
            // Simplified: use spell slots
            var slots = _displayService.Spells.GetSpellSlots(classId, 1);
            if (slots != null && spellLevel < slots.Length)
                return System.Math.Max(slots[spellLevel], 0);
            return 0;
        }
    }

    private void OnAddSpellClick(object? sender, RoutedEventArgs e)
    {
        var selected = _availableSpellsListBox.SelectedItems?.Cast<SpellDisplayItem>().ToList();
        if (selected == null || selected.Count == 0) return;

        int classId = _selectedClassId >= 0 ? _selectedClassId : 0;
        int maxForLevel = GetMaxSpellsForLevel(classId, _currentSpellLevel);

        if (!_selectedSpellsByLevel.ContainsKey(_currentSpellLevel))
            _selectedSpellsByLevel[_currentSpellLevel] = new List<int>();

        foreach (var spell in selected)
        {
            if (_selectedSpellsByLevel[_currentSpellLevel].Count >= maxForLevel)
                break;
            if (!_selectedSpellsByLevel[_currentSpellLevel].Contains(spell.SpellId))
                _selectedSpellsByLevel[_currentSpellLevel].Add(spell.SpellId);
        }

        LoadAvailableSpellsForLevel(_currentSpellLevel);
        UpdateSelectedSpellsDisplay();
        UpdateSpellSelectionCount();
        ValidateCurrentStep();
    }

    private void OnRemoveSpellClick(object? sender, RoutedEventArgs e)
    {
        var selected = _selectedSpellsListBox.SelectedItems?.Cast<SpellDisplayItem>().ToList();
        if (selected == null || selected.Count == 0) return;

        if (!_selectedSpellsByLevel.ContainsKey(_currentSpellLevel)) return;

        foreach (var spell in selected)
        {
            _selectedSpellsByLevel[_currentSpellLevel].Remove(spell.SpellId);
        }

        if (_selectedSpellsByLevel[_currentSpellLevel].Count == 0)
            _selectedSpellsByLevel.Remove(_currentSpellLevel);

        LoadAvailableSpellsForLevel(_currentSpellLevel);
        UpdateSelectedSpellsDisplay();
        UpdateSpellSelectionCount();
        ValidateCurrentStep();
    }

    private void OnSpellAutoAssignClick(object? sender, RoutedEventArgs e)
    {
        int classId = _selectedClassId >= 0 ? _selectedClassId : 0;

        // Clear current selections
        _selectedSpellsByLevel.Clear();

        // Use shared service method (NCW is level 1, no existing spells)
        var assigned = _displayService.Spells.AutoAssignSpells(
            classId,
            _selectedPackageId,
            _maxSpellLevelForClass,
            maxPerLevel: level => GetMaxSpellsForLevel(classId, level),
            existingSpells: null);

        foreach (var (level, spellIds) in assigned)
        {
            _selectedSpellsByLevel[level] = spellIds;
        }

        // Refresh display
        LoadAvailableSpellsForLevel(_currentSpellLevel);
        UpdateSelectedSpellsDisplay();
        UpdateSpellSelectionCount();
        ValidateCurrentStep();
    }

    private bool IsSpellSelectionComplete()
    {
        int classId = _selectedClassId >= 0 ? _selectedClassId : 0;

        for (int level = 0; level <= _maxSpellLevelForClass; level++)
        {
            int maxForLevel = GetMaxSpellsForLevel(classId, level);
            if (maxForLevel <= 0) continue;

            int selected = _selectedSpellsByLevel.GetValueOrDefault(level, new List<int>()).Count;
            if (selected < maxForLevel)
                return false;
        }

        return true;
    }

    private static string GetSchoolAbbrev(SpellSchool school) => school switch
    {
        SpellSchool.Abjuration => "Abj",
        SpellSchool.Conjuration => "Con",
        SpellSchool.Divination => "Div",
        SpellSchool.Enchantment => "Enc",
        SpellSchool.Evocation => "Evo",
        SpellSchool.Illusion => "Ill",
        SpellSchool.Necromancy => "Nec",
        SpellSchool.Transmutation => "Tra",
        _ => ""
    };

    #endregion

    #region Domain Spell Summary

    /// <summary>
    /// Builds a summary of domain spells for display in the spells step.
    /// </summary>
    private List<string> BuildDomainSpellSummary(byte domain1Id, byte domain2Id)
    {
        var lines = new List<string>();

        var d1 = _displayService.Domains.GetDomainInfo(domain1Id);
        if (d1 != null && d1.DomainSpells.Count > 0)
        {
            lines.Add($"  {d1.Name}:");
            foreach (var spell in d1.DomainSpells)
                lines.Add($"    Level {spell.Level}: {spell.Name}");
        }

        var d2 = _displayService.Domains.GetDomainInfo(domain2Id);
        if (d2 != null && d2.DomainSpells.Count > 0)
        {
            lines.Add($"  {d2.Name}:");
            foreach (var spell in d2.DomainSpells)
                lines.Add($"    Level {spell.Level}: {spell.Name}");
        }

        return lines;
    }

    #endregion
}
