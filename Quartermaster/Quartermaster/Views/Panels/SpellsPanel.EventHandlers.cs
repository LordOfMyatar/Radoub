using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Quartermaster.ViewModels;
using Radoub.UI.Services;
using Quartermaster.Views.Helpers;
using Radoub.Formats.Utc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Quartermaster.Views.Panels;

public partial class SpellsPanel
{
    private void OnClassComboBoxChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isLoading) return;

        if (_classComboBox?.SelectedItem is ClassComboItem item)
        {
            if (item.Index != _selectedClassIndex)
            {
                _isLoading = true;
                _selectedClassIndex = item.Index;
                // Reload spells for the selected class
                if (_currentCreature != null)
                    LoadSpellsForClass(_selectedClassIndex);
                _isLoading = false;
            }
        }
    }

    private void OnSpellKnownChanged(SpellListViewModel spell, bool isNowKnown)
    {
        if (_isLoading || _currentCreature == null) return;
        if (_selectedClassIndex >= _currentCreature.ClassList.Count) return;

        // Metamagic variants can't toggle Known — only base spells
        if (spell.IsMetamagicVariant) return;

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

            // Remove all memorizations of this spell (base + all metamagic variants)
            var keysToRemove = _memorizedSpellCounts.Keys
                .Where(k => k.spellId == spell.SpellId).ToList();
            foreach (var key in keysToRemove)
            {
                _memorizedSpellCounts.Remove(key);
            }
            // Remove from all memorized levels (metamagic spells stored at base level)
            for (int level = 0; level <= 9; level++)
            {
                classEntry.MemorizedSpells[level].RemoveAll(s => s.Spell == spell.SpellId);
            }
            spell.MemorizedCount = 0;
        }

        // Update visual status for the base spell
        UpdateSpellVisualStatus(spell);

        // Update all metamagic variant rows for this spell
        foreach (var variant in _allSpells.Where(s => s.SpellId == spell.SpellId && s.IsMetamagicVariant))
        {
            variant.IsKnown = isNowKnown;
            if (!isNowKnown)
                variant.MemorizedCount = 0;
            UpdateSpellVisualStatus(variant);
        }

        // Notify that spells changed
        SpellsChanged?.Invoke(this, EventArgs.Empty);

        // Update summary
        UpdateSummary();
    }

    private void OnSpellMemorizedChanged(SpellListViewModel spell, bool isNowMemorized)
    {
        // Legacy handler - redirect to count-based handler
        OnSpellMemorizedCountChanged(spell, isNowMemorized ? 1 : -spell.MemorizedCount);
    }

    private void OnSpellMemorizedCountChanged(SpellListViewModel spell, int delta)
    {
        if (_isLoading || _currentCreature == null) return;
        if (_selectedClassIndex >= _currentCreature.ClassList.Count) return;

        var classEntry = _currentCreature.ClassList[_selectedClassIndex];
        // Metamagic spells are stored at the BASE spell level in the GFF (NWN convention)
        var memorizedList = classEntry.MemorizedSpells[spell.BaseSpellLevel];
        var countKey = (spell.SpellId, spell.MetamagicFlag);
        int currentCount = (_memorizedSpellCounts.TryGetValue(countKey, out var count) ? count : 0);

        if (delta > 0)
        {
            // Add memorizations with the metamagic flag
            for (int i = 0; i < delta; i++)
            {
                memorizedList.Add(new MemorizedSpell
                {
                    Spell = (ushort)spell.SpellId,
                    SpellFlags = 0x01,
                    SpellMetaMagic = spell.MetamagicFlag,
                    Ready = 1
                });
            }
            _memorizedSpellCounts[countKey] = currentCount + delta;
        }
        else if (delta < 0)
        {
            // Remove memorizations matching this spell + metamagic combination
            int toRemove = Math.Min(-delta, currentCount);
            for (int i = 0; i < toRemove; i++)
            {
                var existing = memorizedList.FirstOrDefault(s =>
                    s.Spell == spell.SpellId && s.SpellMetaMagic == spell.MetamagicFlag);
                if (existing != null)
                {
                    memorizedList.Remove(existing);
                }
            }

            int newCount = currentCount - toRemove;
            if (newCount <= 0)
                _memorizedSpellCounts.Remove(countKey);
            else
                _memorizedSpellCounts[countKey] = newCount;
        }

        // Update the view model
        spell.MemorizedCount = _memorizedSpellCounts.TryGetValue(countKey, out var updatedCount) ? updatedCount : 0;

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
        int memorizedCount = spell.MemorizedCount;
        bool isVariant = spell.IsMetamagicVariant;

        if (spell.IsBlocked)
        {
            spell.StatusText = "Blocked";
            spell.StatusColor = BrushManager.GetDisabledBrush(this);
            spell.RowBackground = GetTransparentRowBackground(spell.StatusColor, 20);
            spell.TextOpacity = 0.5;
            spell.MemorizedCountColor = BrushManager.GetDisabledBrush(this);
        }
        else if (isVariant && memorizedCount > 0)
        {
            // Metamagic variant with memorizations
            spell.StatusText = memorizedCount > 1 ? $"M×{memorizedCount}" : "Memorized";
            spell.StatusColor = BrushManager.GetWarningBrush(this);
            spell.RowBackground = GetTransparentRowBackground(spell.StatusColor, 20);
            spell.TextOpacity = 0.85;
            spell.MemorizedCountColor = BrushManager.GetWarningBrush(this);
        }
        else if (isVariant)
        {
            // Metamagic variant without memorizations
            spell.StatusText = "";
            spell.StatusColor = Brushes.Transparent;
            spell.RowBackground = Brushes.Transparent;
            spell.TextOpacity = 0.55;
            spell.MemorizedCountColor = isKnown && !spell.IsSpontaneousCaster
                ? BrushManager.GetInfoBrush(this)
                : BrushManager.GetDisabledBrush(this);
        }
        else if (isKnown && memorizedCount > 0)
        {
            // Base spell: known and memorized
            spell.StatusText = memorizedCount > 1 ? $"K + M×{memorizedCount}" : "K + M";
            spell.StatusColor = BrushManager.GetWarningBrush(this);
            spell.RowBackground = GetTransparentRowBackground(spell.StatusColor, 30);
            spell.TextOpacity = 1.0;
            spell.MemorizedCountColor = BrushManager.GetWarningBrush(this);
        }
        else if (memorizedCount > 0)
        {
            // Memorized but not known (edge case)
            spell.StatusText = memorizedCount > 1 ? $"M×{memorizedCount}" : "Memorized";
            spell.StatusColor = BrushManager.GetWarningBrush(this);
            spell.RowBackground = GetTransparentRowBackground(spell.StatusColor, 30);
            spell.TextOpacity = 1.0;
            spell.MemorizedCountColor = BrushManager.GetWarningBrush(this);
        }
        else if (isKnown)
        {
            spell.StatusText = "Known";
            spell.StatusColor = BrushManager.GetSuccessBrush(this);
            spell.RowBackground = GetTransparentRowBackground(spell.StatusColor, 30);
            spell.TextOpacity = 1.0;
            if (!spell.IsSpontaneousCaster)
            {
                spell.MemorizedCountColor = BrushManager.GetInfoBrush(this);
            }
            else
            {
                spell.MemorizedCountColor = BrushManager.GetDisabledBrush(this);
            }
        }
        else
        {
            spell.StatusText = "";
            spell.StatusColor = Brushes.Transparent;
            spell.RowBackground = Brushes.Transparent;
            spell.TextOpacity = 0.7;
            spell.MemorizedCountColor = BrushManager.GetDisabledBrush(this);
        }
    }

    private async void OnClearSpellListClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_currentCreature == null || _selectedClassIndex >= _currentCreature.ClassList.Count)
            return;

        var classEntry = _currentCreature.ClassList[_selectedClassIndex];
        var className = _displayService?.GetClassName(classEntry.Class) ?? $"Class {classEntry.Class}";

        // Count how many spells will be cleared
        int knownCount = classEntry.KnownSpells.Sum(list => list.Count);
        int memorizedCount = classEntry.MemorizedSpells.Sum(list => list.Count);

        if (knownCount == 0 && memorizedCount == 0)
        {
            // Nothing to clear
            return;
        }

        // Show confirmation dialog
        var parentWindow = TopLevel.GetTopLevel(this) as Window;
        if (parentWindow == null)
            return;

        var message = $"Clear all spells for {className}?\n\n" +
                      $"This will remove:\n" +
                      $"• {knownCount} known spell(s)\n" +
                      $"• {memorizedCount} memorized spell(s)\n\n" +
                      "This action cannot be undone.";

        var confirmed = await Helpers.DialogHelper.ShowConfirmationDialog(parentWindow, "Clear All Spells", message);

        if (!confirmed)
            return;

        // Clear all spells
        _isLoading = true;

        // Clear model data
        for (int level = 0; level <= 9; level++)
        {
            classEntry.KnownSpells[level].Clear();
            classEntry.MemorizedSpells[level].Clear();
        }

        // Clear tracking sets
        _knownSpellIds.Clear();
        _memorizedSpellCounts.Clear();

        // Update all view models
        foreach (var spell in _allSpells)
        {
            spell.IsKnown = false;
            spell.MemorizedCount = 0;
            UpdateSpellVisualStatus(spell);
        }

        _isLoading = false;

        // Notify that spells changed
        SpellsChanged?.Invoke(this, EventArgs.Empty);

        // Update summary
        UpdateSummary();
    }

    private void OnIncrementMemorizedClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is SpellListViewModel spell)
        {
            OnSpellMemorizedCountChanged(spell, 1);
        }
    }

    private void OnDecrementMemorizedClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is SpellListViewModel spell)
        {
            OnSpellMemorizedCountChanged(spell, -1);
        }
    }
}
