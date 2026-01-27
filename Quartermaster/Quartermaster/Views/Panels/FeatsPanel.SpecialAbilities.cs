using Avalonia.Controls;
using Quartermaster.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Quartermaster.Views.Panels;

/// <summary>
/// FeatsPanel partial class - Special abilities loading and management.
/// </summary>
public partial class FeatsPanel
{
    /// <summary>
    /// Loads special abilities from the creature.
    /// </summary>
    private void LoadSpecialAbilities(Radoub.Formats.Utc.UtcFile creature)
    {
        foreach (var ability in creature.SpecAbilityList)
        {
            var vm = new SpecialAbilityViewModel
            {
                SpellId = ability.Spell,
                AbilityName = GetSpellNameInternal(ability.Spell),
                CasterLevelDisplay = $"CL {ability.SpellCasterLevel}",
                Flags = ability.SpellFlags
            };
            // Set CasterLevel last to avoid triggering callback during load
            vm._casterLevel = ability.SpellCasterLevel;
            vm.OnCasterLevelChanged = OnAbilityCasterLevelChanged;
            vm.OnFlagsChanged = OnAbilityFlagsChanged;
            vm.RemoveCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(() => RemoveAbility(vm));
            _abilities.Add(vm);
        }

        UpdateAbilitiesVisibility();
    }

    /// <summary>
    /// Updates visibility of the "no abilities" text and expander state.
    /// </summary>
    private void UpdateAbilitiesVisibility()
    {
        if (_noAbilitiesText != null)
            _noAbilitiesText.IsVisible = _abilities.Count == 0;

        // Show expander if there are abilities
        if (_specialAbilitiesExpander != null && _abilities.Count > 0)
            _specialAbilitiesExpander.IsExpanded = true;
    }

    /// <summary>
    /// Handles caster level changes for special abilities.
    /// </summary>
    private void OnAbilityCasterLevelChanged(SpecialAbilityViewModel vm)
    {
        if (_isLoading || _currentCreature == null) return;

        // Update the creature's SpecAbilityList
        var ability = _currentCreature.SpecAbilityList.FirstOrDefault(a => a.Spell == vm.SpellId);
        if (ability != null)
        {
            ability.SpellCasterLevel = vm.CasterLevel;
            SpecialAbilitiesChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Handles flags changes for special abilities.
    /// </summary>
    private void OnAbilityFlagsChanged(SpecialAbilityViewModel vm)
    {
        if (_isLoading || _currentCreature == null) return;

        // Update the creature's SpecAbilityList
        var ability = _currentCreature.SpecAbilityList.FirstOrDefault(a => a.Spell == vm.SpellId);
        if (ability != null)
        {
            ability.SpellFlags = vm.Flags;
            SpecialAbilitiesChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Removes a special ability from the creature.
    /// </summary>
    private void RemoveAbility(SpecialAbilityViewModel vm)
    {
        if (_currentCreature == null) return;

        // Remove from creature
        var ability = _currentCreature.SpecAbilityList.FirstOrDefault(a => a.Spell == vm.SpellId);
        if (ability != null)
        {
            _currentCreature.SpecAbilityList.Remove(ability);
        }

        // Remove from UI
        _abilities.Remove(vm);
        UpdateAbilitiesVisibility();

        SpecialAbilitiesChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Handles the Add Ability button click - shows spell picker dialog.
    /// </summary>
    private async void OnAddAbilityClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_displayService == null || _currentCreature == null) return;

        // Get all spells for picker
        var spellIds = _displayService.GetAllSpellIds();
        var spells = new List<(int Id, string Name, int InnateLevel)>();

        foreach (var spellId in spellIds)
        {
            var spellName = _displayService.GetSpellName(spellId);
            var spellInfo = _displayService.GetSpellInfo(spellId);
            int innateLevel = spellInfo?.InnateLevel ?? 0;
            spells.Add((spellId, spellName, innateLevel));
        }

        var picker = new Dialogs.SpellPickerWindow(spells);
        var parentWindow = TopLevel.GetTopLevel(this) as Window;
        if (parentWindow != null)
        {
            await picker.ShowDialog(parentWindow);
        }
        else
        {
            picker.Show();
        }

        if (picker.Confirmed && picker.SelectedSpellId.HasValue)
        {
            var spellId = picker.SelectedSpellId.Value;

            // Check if already exists
            if (_currentCreature.SpecAbilityList.Any(a => a.Spell == spellId))
            {
                return; // Already has this ability
            }

            // Add to creature
            var newAbility = new Radoub.Formats.Utc.SpecialAbility
            {
                Spell = spellId,
                SpellCasterLevel = 1, // Default caster level
                SpellFlags = 0x01 // Default: readied
            };
            _currentCreature.SpecAbilityList.Add(newAbility);

            // Add to UI
            var vm = new SpecialAbilityViewModel
            {
                SpellId = spellId,
                AbilityName = picker.SelectedSpellName,
                CasterLevelDisplay = "CL 1",
                Flags = 0x01
            };
            vm._casterLevel = 1;
            vm.OnCasterLevelChanged = OnAbilityCasterLevelChanged;
            vm.OnFlagsChanged = OnAbilityFlagsChanged;
            vm.RemoveCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(() => RemoveAbility(vm));
            _abilities.Add(vm);

            UpdateAbilitiesVisibility();
            SpecialAbilitiesChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
