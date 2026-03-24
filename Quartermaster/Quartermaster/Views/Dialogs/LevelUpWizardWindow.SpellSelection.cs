using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Quartermaster.Services;
using Radoub.UI.Services;

namespace Quartermaster.Views.Dialogs;

/// <summary>
/// Step 5: Spell selection with divine/spontaneous/prepared caster logic.
/// </summary>
public partial class LevelUpWizardWindow
{
    #region Step 5: Spell Selection

    private void PrepareStep5()
    {
        bool isCaster = _displayService.IsCasterClass(_selectedClassId);
        if (!isCaster)
        {
            _needsSpellSelection = false;
            _spellStepDescription.Text = $"{_displayService.GetClassName(_selectedClassId)} is not a spellcasting class.";
            _spellTabsBar.IsVisible = false;
            _spellSelectionTwoPanel.IsVisible = false;
            _divineSpellInfoPanel.IsVisible = false;
            return;
        }

        _isSpontaneousCaster = _displayService.Spells.IsSpontaneousCaster(_selectedClassId);
        _isDivineCaster = _displayService.IsDivineCaster(_selectedClassId);
        int maxSpellLevel = _displayService.Spells.GetMaxSpellLevel(_selectedClassId, _newClassLevel);
        int prevMaxSpellLevel = _newClassLevel > 1 ? _displayService.Spells.GetMaxSpellLevel(_selectedClassId, _newClassLevel - 1) : -1;
        _maxSpellLevelThisLevel = maxSpellLevel;

        if (maxSpellLevel < 0)
        {
            _needsSpellSelection = false;
            _spellStepDescription.Text = "No spells available at this class level.";
            _spellTabsBar.IsVisible = false;
            _spellSelectionTwoPanel.IsVisible = false;
            _divineSpellInfoPanel.IsVisible = false;
            return;
        }

        var className = _displayService.GetClassName(_selectedClassId);

        // Divine casters (Cleric, Druid, Ranger, Paladin) - show info panel
        if (_isDivineCaster)
        {
            _needsSpellSelection = true; // Step shows, but no selection needed
            _spellTabsBar.IsVisible = false;
            _spellSelectionTwoPanel.IsVisible = false;
            _divineSpellInfoPanel.IsVisible = true;

            _spellStepDescription.Text = $"{className} spells are granted automatically.";
            _divineSpellInfoLabel.Text = $"As a {className}, you gain access to all {className.ToLowerInvariant()} spells " +
                                          $"up to level {maxSpellLevel}. No spell selection is needed.";

            // Show domain spells for divine casters with domains
            var classEntry = _creature.ClassList.FirstOrDefault(c => (int)c.Class == _selectedClassId);
            if (classEntry != null)
            {
                var domain1Id = (int)classEntry.Domain1;
                var domain2Id = (int)classEntry.Domain2;
                if (domain1Id > 0 || domain2Id > 0)
                {
                    var domainLines = new List<string>();
                    void AddDomainInfo(int domainId)
                    {
                        var domainInfo = _displayService.Domains.GetDomainInfo(domainId);
                        if (domainInfo == null) return;
                        domainLines.Add($"  {domainInfo.Name}:");
                        if (domainInfo.GrantedFeatId >= 0)
                            domainLines.Add($"    Feat: {domainInfo.GrantedFeatName}");
                        foreach (var spell in domainInfo.DomainSpells)
                            domainLines.Add($"    Level {spell.Level}: {spell.Name}");
                    }

                    if (domain1Id > 0) AddDomainInfo(domain1Id);
                    if (domain2Id > 0) AddDomainInfo(domain2Id);

                    if (domainLines.Count > 0)
                    {
                        _divineSpellInfoLabel.Text += "\n\nYour domains also grant additional spells:\n" +
                            string.Join("\n", domainLines);
                    }
                }
            }

            // Show new spells gained at this level for informational purposes
            var newSpellNames = new List<string>();
            for (int level = Math.Max(0, prevMaxSpellLevel + 1); level <= maxSpellLevel; level++)
            {
                var spellIds = _displayService.Spells.GetSpellsForClassAtLevel(_selectedClassId, level);
                foreach (var spellId in spellIds)
                    newSpellNames.Add($"  [{level}] {_displayService.GetSpellName(spellId)}");
            }

            if (newSpellNames.Count > 0)
            {
                _divineSpellsList.ItemsSource = newSpellNames.OrderBy(s => s).Take(50).ToList();
            }
            else
            {
                _divineSpellsList.ItemsSource = new[] { "No new spell levels gained." };
            }
            return;
        }

        // Spontaneous casters (Sorcerer/Bard) - calculate new spells per level
        _newSpellsPerLevel.Clear();
        _selectedSpellsByLevel.Clear();

        if (_isSpontaneousCaster)
        {
            // Consolidated: compare known limits at target vs base level (#1645)
            var knownAtTarget = _displayService.Spells.GetSpellsKnownLimit(_selectedClassId, _newClassLevel);
            var knownAtBase = _fromClassLevel > 1
                ? _displayService.Spells.GetSpellsKnownLimit(_selectedClassId, _fromClassLevel - 1)
                : null;

            if (knownAtTarget != null)
            {
                for (int i = 0; i <= maxSpellLevel; i++)
                {
                    int baseKnown = knownAtBase?[i] ?? 0;
                    int targetKnown = knownAtTarget[i];
                    if (targetKnown > baseKnown)
                        _newSpellsPerLevel[i] = targetKnown - baseKnown;
                }
            }

            _spellStepDescription.Text = _levelsToAdd > 1
                ? $"Choose new spells known for your {className} (levels {_fromClassLevel}-{_newClassLevel})."
                : $"Choose new spells known for your {className}.";
        }
        else
        {
            // Wizard spellbook: gets 2 free spells per level gained (NWN convention). (#1645)
            _wizardFreeSpellsRemaining = 2 * _levelsToAdd;

            if (_validationLevel == ValidationLevel.None)
            {
                // CE mode: any castable spell level gets the full budget — no restrictions
                var slotsAtLevel = _displayService.Spells.GetSpellSlots(_selectedClassId, _newClassLevel);
                for (int i = 0; i <= maxSpellLevel; i++)
                {
                    if (slotsAtLevel != null && i < slotsAtLevel.Length && slotsAtLevel[i] > 0)
                        _newSpellsPerLevel[i] = _wizardFreeSpellsRemaining;
                }
            }
            else
            {
                // LG/TN mode: cap per spell level based on how many class levels in the
                // range had access to that spell level. A Wizard only gains 2 free spells
                // per class level, and can only pick from spell levels available at that
                // class level. E.g., level 4 spells are only available at class level 7+.
                for (int spellLvl = 0; spellLvl <= maxSpellLevel; spellLvl++)
                {
                    int classLevelsWithAccess = 0;
                    for (int clsLvl = _fromClassLevel; clsLvl <= _newClassLevel; clsLvl++)
                    {
                        int maxAtLevel = _displayService.Spells.GetMaxSpellLevel(_selectedClassId, clsLvl);
                        if (maxAtLevel >= spellLvl)
                            classLevelsWithAccess++;
                    }
                    if (classLevelsWithAccess > 0)
                        _newSpellsPerLevel[spellLvl] = 2 * classLevelsWithAccess;
                }
            }

            // Remove spell levels where creature already knows all available spells (#1647)
            RemoveFullyKnownSpellLevels();

            _spellStepDescription.Text = _levelsToAdd > 1
                ? $"Choose {_wizardFreeSpellsRemaining} spells to add to your {className}'s spellbook ({_levelsToAdd} levels × 2)."
                : $"Choose 2 spells to add to your {className}'s spellbook.";
        }

        bool hasNewSpells = _newSpellsPerLevel.Values.Any(v => v > 0);
        if (!hasNewSpells)
        {
            _needsSpellSelection = false;
            _spellStepDescription.Text = "No new spells to learn at this level.";
            _spellTabsBar.IsVisible = false;
            _spellSelectionTwoPanel.IsVisible = false;
            _divineSpellInfoPanel.IsVisible = false;
            return;
        }

        _needsSpellSelection = true;
        _spellTabsBar.IsVisible = true;
        _spellSelectionTwoPanel.IsVisible = true;
        _divineSpellInfoPanel.IsVisible = false;

        // Build spell level tabs
        BuildSpellLevelTabs();

        // Default to lowest spell level with available spells.
        // For Wizards, this skips cantrips (already fully known) and shows the most relevant level.
        _currentSpellLevel = _newSpellsPerLevel.Keys.Min();
        SelectSpellLevelTab(_currentSpellLevel);
    }

    /// <summary>
    /// Removes spell levels from _newSpellsPerLevel where the creature already knows
    /// all available spells. Prevents showing empty tabs (e.g., cantrips for Wizards
    /// who learn all cantrips at level 1). (#1647)
    /// </summary>
    private void RemoveFullyKnownSpellLevels()
    {
        var existingSpells = new HashSet<int>(_creature.SpecAbilityList.Select(sa => (int)sa.Spell));
        var spellClass = _creature.ClassList.FirstOrDefault(c => c.Class == _selectedClassId);

        var levelsToRemove = new List<int>();
        foreach (var spellLevel in _newSpellsPerLevel.Keys)
        {
            // Build the set of spells the creature already knows at this level
            var knownAtLevel = new HashSet<int>(existingSpells);
            if (spellClass != null && spellLevel >= 0 && spellLevel < spellClass.KnownSpells.Length)
            {
                foreach (var ks in spellClass.KnownSpells[spellLevel])
                    knownAtLevel.Add((int)ks.Spell);
            }

            // Check if any spells are available that the creature doesn't know
            var allSpellIds = _displayService.Spells.GetSpellsForClassAtLevel(_selectedClassId, spellLevel);
            bool hasNewSpells = allSpellIds.Any(id => !knownAtLevel.Contains(id));

            if (!hasNewSpells)
                levelsToRemove.Add(spellLevel);
        }

        foreach (var level in levelsToRemove)
            _newSpellsPerLevel.Remove(level);
    }

    private void BuildSpellLevelTabs()
    {
        _spellLevelTabsPanel.Children.Clear();
        foreach (var level in _newSpellsPerLevel.Keys.OrderBy(k => k))
        {
            var btn = new ToggleButton
            {
                Content = level == 0 ? "Cantrips" : $"Level {level}",
                Tag = level,
                Margin = new Thickness(0, 0, 2, 0),
                IsChecked = false
            };
            btn.Click += OnSpellLevelTabClick;
            _spellLevelTabsPanel.Children.Add(btn);
        }
    }

    private void OnSpellLevelTabClick(object? sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton btn && btn.Tag is int level)
            SelectSpellLevelTab(level);
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

        LoadAvailableSpellsForLevel(level);
        UpdateSelectedSpellsDisplay();
        UpdateSpellSelectionCount();
        ValidateCurrentStep();
    }

    private void LoadAvailableSpellsForLevel(int spellLevel)
    {
        var allSpellIds = _displayService.Spells.GetSpellsForClassAtLevel(_selectedClassId, spellLevel);
        var selectedForLevel = _selectedSpellsByLevel.GetValueOrDefault(spellLevel, new List<int>());

        // Get creature's existing spells to exclude (check both SpecAbilityList and KnownSpells)
        var existingSpells = new HashSet<int>(_creature.SpecAbilityList.Select(sa => (int)sa.Spell));
        var spellClass = _creature.ClassList.FirstOrDefault(c => c.Class == _selectedClassId);
        if (spellClass != null && spellLevel >= 0 && spellLevel < spellClass.KnownSpells.Length)
        {
            foreach (var ks in spellClass.KnownSpells[spellLevel])
                existingSpells.Add((int)ks.Spell);
        }

        _availableSpellsForLevel = new List<SpellDisplayItem>();
        foreach (var spellId in allSpellIds)
        {
            if (selectedForLevel.Contains(spellId)) continue;
            if (existingSpells.Contains(spellId)) continue;

            var info = _displayService.Spells.GetSpellInfo(spellId);
            if (info == null) continue;

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
        _filteredAvailableSpells = SkillDisplayHelper.FilterByName(_availableSpellsForLevel, _spellSearchBox?.Text?.Trim());
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

        if (_wizardFreeSpellsRemaining > 0)
        {
            // Wizard: show per-level count and cap
            int maxForLevel = _newSpellsPerLevel.GetValueOrDefault(_currentSpellLevel, 0);
            _selectedSpellCountLabel.Text = $"({selectedForLevel.Count} / {maxForLevel} at this level)";
            if (selectedForLevel.Count >= maxForLevel)
                _selectedSpellCountLabel.ClearValue(TextBlock.ForegroundProperty);
            else
                _selectedSpellCountLabel.Foreground = BrushManager.GetSuccessBrush(this);
        }
        else
        {
            int maxForLevel = _newSpellsPerLevel.GetValueOrDefault(_currentSpellLevel, 0);
            _selectedSpellCountLabel.Text = $"({selectedForLevel.Count} / {maxForLevel})";

            if (selectedForLevel.Count > maxForLevel)
                _selectedSpellCountLabel.Foreground = BrushManager.GetErrorBrush(this);
            else if (selectedForLevel.Count == maxForLevel)
                _selectedSpellCountLabel.ClearValue(TextBlock.ForegroundProperty);
            else
                _selectedSpellCountLabel.Foreground = BrushManager.GetSuccessBrush(this);
        }
    }

    private void UpdateSpellSelectionCount()
    {
        int totalSelected = GetTotalSelectedSpells();

        if (_wizardFreeSpellsRemaining > 0)
        {
            // Wizard: show total against free spell budget
            _spellSelectionCountLabel.Text = $"Total: {totalSelected} / {_wizardFreeSpellsRemaining}";
        }
        else
        {
            int totalRequired = 0;
            foreach (var (_, required) in _newSpellsPerLevel)
                totalRequired += required;
            _spellSelectionCountLabel.Text = $"Total: {totalSelected} / {totalRequired}";
        }
    }

    private int GetTotalSelectedSpells()
    {
        return _selectedSpellsByLevel.Values.Sum(list => list.Count);
    }

    private void OnAddSpellClick(object? sender, RoutedEventArgs e)
    {
        var selected = _availableSpellsListBox.SelectedItems?.Cast<SpellDisplayItem>().ToList();
        if (selected == null || selected.Count == 0) return;

        int maxForLevel = _newSpellsPerLevel.GetValueOrDefault(_currentSpellLevel, 0);
        if (!_selectedSpellsByLevel.ContainsKey(_currentSpellLevel))
            _selectedSpellsByLevel[_currentSpellLevel] = new List<int>();

        foreach (var spell in selected)
        {
            if (_selectedSpellsByLevel[_currentSpellLevel].Count >= maxForLevel) break;
            // Wizard: enforce total cap across all levels
            if (_wizardFreeSpellsRemaining > 0 && GetTotalSelectedSpells() >= _wizardFreeSpellsRemaining) break;
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
            _selectedSpellsByLevel[_currentSpellLevel].Remove(spell.SpellId);

        if (_selectedSpellsByLevel[_currentSpellLevel].Count == 0)
            _selectedSpellsByLevel.Remove(_currentSpellLevel);

        LoadAvailableSpellsForLevel(_currentSpellLevel);
        UpdateSelectedSpellsDisplay();
        UpdateSpellSelectionCount();
        ValidateCurrentStep();
    }

    private void OnSpellAutoAssignClick(object? sender, RoutedEventArgs e)
    {
        var existingSpells = new HashSet<int>(_creature.SpecAbilityList.Select(sa => (int)sa.Spell));
        // Also exclude spells already in KnownSpells for this class
        var autoSpellClass = _creature.ClassList.FirstOrDefault(c => c.Class == _selectedClassId);
        if (autoSpellClass != null)
        {
            foreach (var knownList in autoSpellClass.KnownSpells)
                foreach (var ks in knownList)
                    existingSpells.Add((int)ks.Spell);
        }

        // For Wizards, limit total across all levels to free spell budget
        int totalBudget = _wizardFreeSpellsRemaining > 0 ? _wizardFreeSpellsRemaining : int.MaxValue;

        // For Wizard consolidated mode, distribute budget across levels evenly
        // to avoid auto-assign dumping all spells into level 1 (#1645)
        int perLevelCap(int level)
        {
            if (_wizardFreeSpellsRemaining <= 0)
                return _newSpellsPerLevel.GetValueOrDefault(level, 0);
            int levelCount = _newSpellsPerLevel.Count;
            if (levelCount <= 0) return 0;
            // Distribute evenly, give remainder to highest levels
            int basePerLevel = _wizardFreeSpellsRemaining / levelCount;
            return System.Math.Max(basePerLevel, 2); // At least 2 per level
        }

        var assigned = _displayService.Spells.AutoAssignSpells(
            _selectedClassId,
            _resolvedPackageId,
            _maxSpellLevelThisLevel,
            perLevelCap,
            existingSpells);

        // Apply total budget cap (iterating from highest level down for better distribution)
        _selectedSpellsByLevel.Clear();
        int totalAdded = 0;
        foreach (var (level, spells) in assigned.OrderByDescending(kv => kv.Key))
        {
            var limitedSpells = spells.Take(totalBudget - totalAdded).ToList();
            if (limitedSpells.Count > 0)
                _selectedSpellsByLevel[level] = limitedSpells;
            totalAdded += limitedSpells.Count;
            if (totalAdded >= totalBudget) break;
        }

        LoadAvailableSpellsForLevel(_currentSpellLevel);
        UpdateSelectedSpellsDisplay();
        UpdateSpellSelectionCount();
        ValidateCurrentStep();
    }

    private bool IsSpellSelectionComplete()
    {
        if (_wizardFreeSpellsRemaining > 0)
        {
            // Wizard: check total across all levels
            return GetTotalSelectedSpells() >= _wizardFreeSpellsRemaining;
        }

        foreach (var (level, required) in _newSpellsPerLevel)
        {
            int selected = _selectedSpellsByLevel.GetValueOrDefault(level, new List<int>()).Count;
            if (selected < required) return false;
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
}
