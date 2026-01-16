using Avalonia.Controls;
using Avalonia.Media;
using Quartermaster.ViewModels;
using Radoub.Formats.Utc;
using System;
using System.Linq;

namespace Quartermaster.Views.Panels;

public partial class SpellsPanel
{
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
            spell.StatusColor = GetDisabledBrush();
            spell.RowBackground = GetTransparentRowBackground(spell.StatusColor, 20);
            spell.TextOpacity = 0.5;
        }
        else if (isKnown && isMemorized)
        {
            spell.StatusText = "K + M";
            spell.StatusColor = GetSelectionBrush();
            spell.RowBackground = GetTransparentRowBackground(spell.StatusColor, 30);
            spell.TextOpacity = 1.0;
        }
        else if (isMemorized)
        {
            // Memorized but not known (edge case - shouldn't happen normally)
            spell.StatusText = "Memorized";
            spell.StatusColor = GetSelectionBrush();
            spell.RowBackground = GetTransparentRowBackground(spell.StatusColor, 30);
            spell.TextOpacity = 1.0;
        }
        else if (isKnown)
        {
            spell.StatusText = "Known";
            spell.StatusColor = GetSuccessBrush();
            spell.RowBackground = GetTransparentRowBackground(spell.StatusColor, 30);
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
}
