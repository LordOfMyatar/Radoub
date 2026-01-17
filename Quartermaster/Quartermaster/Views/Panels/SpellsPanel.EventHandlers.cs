using Avalonia.Controls;
using Avalonia.Media;
using Quartermaster.ViewModels;
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
            if (_memorizedSpellCounts.ContainsKey(spell.SpellId))
            {
                // Remove all memorizations of this spell
                _memorizedSpellCounts.Remove(spell.SpellId);
                var memorizedList = classEntry.MemorizedSpells[spell.SpellLevel];
                memorizedList.RemoveAll(s => s.Spell == spell.SpellId);
                spell.MemorizedCount = 0;
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
        // Legacy handler - redirect to count-based handler
        OnSpellMemorizedCountChanged(spell, isNowMemorized ? 1 : -spell.MemorizedCount);
    }

    private void OnSpellMemorizedCountChanged(SpellListViewModel spell, int delta)
    {
        if (_isLoading || _currentCreature == null) return;
        if (_selectedClassIndex >= _currentCreature.ClassList.Count) return;

        var classEntry = _currentCreature.ClassList[_selectedClassIndex];
        var memorizedList = classEntry.MemorizedSpells[spell.SpellLevel];
        int currentCount = (_memorizedSpellCounts.TryGetValue(spell.SpellId, out var count) ? count : 0);

        if (delta > 0)
        {
            // Add memorizations
            for (int i = 0; i < delta; i++)
            {
                memorizedList.Add(new MemorizedSpell
                {
                    Spell = (ushort)spell.SpellId,
                    SpellFlags = 0x01,
                    SpellMetaMagic = 0,
                    Ready = 1
                });
            }
            _memorizedSpellCounts[spell.SpellId] = currentCount + delta;
        }
        else if (delta < 0)
        {
            // Remove memorizations
            int toRemove = Math.Min(-delta, currentCount);
            for (int i = 0; i < toRemove; i++)
            {
                var existing = memorizedList.FirstOrDefault(s => s.Spell == spell.SpellId);
                if (existing != null)
                {
                    memorizedList.Remove(existing);
                }
            }

            int newCount = currentCount - toRemove;
            if (newCount <= 0)
                _memorizedSpellCounts.Remove(spell.SpellId);
            else
                _memorizedSpellCounts[spell.SpellId] = newCount;
        }

        // Update the view model
        spell.MemorizedCount = _memorizedSpellCounts.TryGetValue(spell.SpellId, out var updatedCount) ? updatedCount : 0;

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

        if (spell.IsBlocked)
        {
            spell.StatusText = "Blocked";
            spell.StatusColor = GetDisabledBrush();
            spell.RowBackground = GetTransparentRowBackground(spell.StatusColor, 20);
            spell.TextOpacity = 0.5;
            spell.MemorizedCountColor = GetDisabledBrush();
        }
        else if (isKnown && memorizedCount > 0)
        {
            // Show memorization count if > 1
            spell.StatusText = memorizedCount > 1 ? $"K + M×{memorizedCount}" : "K + M";
            spell.StatusColor = GetSelectionBrush();
            spell.RowBackground = GetTransparentRowBackground(spell.StatusColor, 30);
            spell.TextOpacity = 1.0;
            spell.MemorizedCountColor = GetSelectionBrush();
        }
        else if (memorizedCount > 0)
        {
            // Memorized but not known (edge case - shouldn't happen normally)
            spell.StatusText = memorizedCount > 1 ? $"M×{memorizedCount}" : "Memorized";
            spell.StatusColor = GetSelectionBrush();
            spell.RowBackground = GetTransparentRowBackground(spell.StatusColor, 30);
            spell.TextOpacity = 1.0;
            spell.MemorizedCountColor = GetSelectionBrush();
        }
        else if (isKnown)
        {
            spell.StatusText = "Known";
            spell.StatusColor = GetSuccessBrush();
            spell.RowBackground = GetTransparentRowBackground(spell.StatusColor, 30);
            spell.TextOpacity = 1.0;
            spell.MemorizedCountColor = GetDisabledBrush();
        }
        else
        {
            spell.StatusText = "";
            spell.StatusColor = Brushes.Transparent;
            spell.RowBackground = Brushes.Transparent;
            spell.TextOpacity = 0.7;
            spell.MemorizedCountColor = GetDisabledBrush();
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

        var confirmed = await DialogHelper.ShowConfirmationDialog(parentWindow, "Clear All Spells", message);

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
