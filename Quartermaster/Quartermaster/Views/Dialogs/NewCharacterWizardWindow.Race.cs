using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Quartermaster.Services;
using Radoub.UI.Services;

namespace Quartermaster.Views.Dialogs;

/// <summary>
/// Step 2: Race &amp; Sex selection.
/// </summary>
public partial class NewCharacterWizardWindow
{
    #region Step 2: Race & Sex

    private void PrepareStep2()
    {
        if (_allRaces.Count > 0)
            return; // Already loaded

        // Load races based on file type
        var races = _isBicFile
            ? _displayService.GetPlayerRaces()
            : _displayService.GetAllRaces();

        _allRaces = races.Select(r => new RaceDisplayItem
        {
            Id = r.Id,
            Name = r.Name
        }).ToList();

        _filteredRaces = new List<RaceDisplayItem>(_allRaces);
        _raceListBox.ItemsSource = _filteredRaces;

        // Select first race by default (list is already sorted, no hardcoded race ID)
        var humanItem = _filteredRaces.FirstOrDefault();
        if (humanItem != null)
        {
            _raceListBox.SelectedItem = humanItem;
        }
        else if (_filteredRaces.Count > 0)
        {
            _raceListBox.SelectedItem = _filteredRaces[0];
        }
    }

    private void OnRaceSearchChanged(object? sender, TextChangedEventArgs e)
    {
        var filter = _raceSearchBox.Text?.Trim() ?? "";

        if (string.IsNullOrEmpty(filter))
        {
            _filteredRaces = new List<RaceDisplayItem>(_allRaces);
        }
        else
        {
            _filteredRaces = _allRaces
                .Where(r => r.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        _raceListBox.ItemsSource = _filteredRaces;
    }

    private void OnRaceSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_raceListBox.SelectedItem is not RaceDisplayItem selected)
            return;

        _selectedRaceId = selected.Id;

        // Update point-buy budget from racialtypes.2da (e.g., Animal races get fewer points)
        int newPointBuy = _displayService.GetRacialAbilitiesPointBuyNumber(_selectedRaceId);
        if (newPointBuy != _pointBuyTotal)
        {
            _pointBuyTotal = newPointBuy;
            // Reset ability scores when point pool changes — previous allocation may be invalid
            foreach (var ability in AbilityNames)
                _abilityBaseScores[ability] = AbilityMinBase;
            _step5Loaded = false; // Force rebuild of ability rows on next visit
        }

        UpdateRaceInfoPanel();
        ValidateCurrentStep();
    }

    private void UpdateRaceInfoPanel()
    {
        var raceName = _displayService.GetRaceName(_selectedRaceId);
        _selectedRaceNameLabel.Text = raceName;

        // Ability modifiers
        var mods = _displayService.GetRacialModifiers(_selectedRaceId);
        _raceModifiersPanel.IsVisible = true;

        UpdateModifierLabel(_strModLabel, "STR", mods.Str);
        UpdateModifierLabel(_dexModLabel, "DEX", mods.Dex);
        UpdateModifierLabel(_conModLabel, "CON", mods.Con);
        UpdateModifierLabel(_intModLabel, "INT", mods.Int);
        UpdateModifierLabel(_wisModLabel, "WIS", mods.Wis);
        UpdateModifierLabel(_chaModLabel, "CHA", mods.Cha);

        // Favored class
        _favoredClassId = _displayService.GetFavoredClass(_selectedRaceId);
        _favoredClassLabel.Text = _favoredClassId == -1
            ? "Any"
            : _displayService.GetClassName(_favoredClassId);

        // Size
        _raceSizeLabel.Text = _displayService.GetRaceSizeCategory(_selectedRaceId);

        _raceTraitsPanel.IsVisible = true;

        // Description
        var descStrRef = _displayService.GameDataService.Get2DAValue("racialtypes", _selectedRaceId, "Description");
        if (!string.IsNullOrEmpty(descStrRef) && descStrRef != "****")
        {
            var desc = _displayService.GameDataService.GetString(descStrRef);
            _raceDescriptionLabel.Text = !string.IsNullOrEmpty(desc) ? desc : $"The {raceName}.";
            _raceDescriptionLabel.Foreground = null;
        }
        else
        {
            _raceDescriptionLabel.Text = $"The {raceName}.";
            _raceDescriptionLabel.Foreground = null;
        }

        _raceDescSeparator.IsVisible = true;

        UpdateSidebarSummary();
    }

    private void UpdateModifierLabel(TextBlock label, string ability, int modifier)
    {
        label.Text = modifier == 0
            ? $"{ability}: +0"
            : $"{ability}: {CreatureDisplayService.FormatBonus(modifier)}";

        if (modifier > 0)
            label.Foreground = BrushManager.GetSuccessBrush(this);
        else if (modifier < 0)
            label.Foreground = BrushManager.GetWarningBrush(this);
        else
            label.Foreground = new SolidColorBrush(Colors.Transparent);

        if (modifier == 0)
            label.ClearValue(TextBlock.ForegroundProperty);
    }

    private void OnMaleToggleClick(object? sender, RoutedEventArgs e)
    {
        _selectedGender = 0;
        _maleToggle.IsChecked = true;
        _femaleToggle.IsChecked = false;
        UpdateSidebarSummary();
    }

    private void OnFemaleToggleClick(object? sender, RoutedEventArgs e)
    {
        _selectedGender = 1;
        _maleToggle.IsChecked = false;
        _femaleToggle.IsChecked = true;
        UpdateSidebarSummary();
    }

    #endregion
}
