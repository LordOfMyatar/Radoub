using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Quartermaster.Services;

namespace Quartermaster.Views.Dialogs;

/// <summary>
/// Step 4: Appearance, body parts, and colors.
/// </summary>
public partial class NewCharacterWizardWindow
{
    #region Step 4: Appearance

    private void PrepareStep4()
    {
        if (_step3Loaded)
            return;

        _step3Loaded = true;

        // Load appearances into backing list and populate ListBox
        _allAppearances = _displayService.GetAllAppearances();
        _filteredAppearances = new List<AppearanceInfo>(_allAppearances);
        _appearanceListBox.ItemsSource = _filteredAppearances;

        // Set default appearance based on race
        var defaultAppId = GetDefaultAppearanceForRace(_selectedRaceId);
        var defaultApp = _allAppearances.FirstOrDefault(a => a.AppearanceId == defaultAppId);
        if (defaultApp != null)
            _appearanceListBox.SelectedItem = defaultApp;
        else if (_allAppearances.Count > 0)
            _appearanceListBox.SelectedItem = _allAppearances[0];

        // Load phenotypes
        var phenotypes = _displayService.GetAllPhenotypes();
        _phenotypeComboBox.ItemsSource = phenotypes;
        if (phenotypes.Count > 0)
            _phenotypeComboBox.SelectedItem = phenotypes[0];

        // Initialize color swatches
        UpdateAllColorSwatches();
    }

    private void OnAppearanceSearchChanged(object? sender, TextChangedEventArgs e)
    {
        var previousSelection = _appearanceListBox.SelectedItem as AppearanceInfo;

        _filteredAppearances = SkillDisplayHelper.FilterByName(_allAppearances, _appearanceSearchBox.Text?.Trim());
        _appearanceListBox.ItemsSource = _filteredAppearances;

        // Restore selection if still in filtered list
        if (previousSelection != null && _filteredAppearances.Contains(previousSelection))
            _appearanceListBox.SelectedItem = previousSelection;
    }

    private void OnAppearanceSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_appearanceListBox.SelectedItem is not AppearanceInfo selected)
            return;

        _selectedAppearanceId = selected.AppearanceId;
        _isPartBased = selected.IsPartBased;
        UpdateBodyPartsVisibility();
    }

    private void OnPhenotypeSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_phenotypeComboBox.SelectedItem is not PhenotypeInfo selected)
            return;

        _selectedPhenotype = selected.PhenotypeId;
    }

    private void UpdateBodyPartsVisibility()
    {
        // Show/hide body part controls based on whether appearance is part-based
        _bodyPartsContent.IsEnabled = _isPartBased;
        _bodyPartsNotApplicableLabel.IsVisible = !_isPartBased;

        // Color controls are always enabled for part-based appearances
        _skinColorNumericUpDown.IsEnabled = _isPartBased;
        _hairColorNumericUpDown.IsEnabled = _isPartBased;
        _tattoo1ColorNumericUpDown.IsEnabled = _isPartBased;
        _tattoo2ColorNumericUpDown.IsEnabled = _isPartBased;
        _skinColorSwatch.IsEnabled = _isPartBased;
        _hairColorSwatch.IsEnabled = _isPartBased;
        _tattoo1ColorSwatch.IsEnabled = _isPartBased;
        _tattoo2ColorSwatch.IsEnabled = _isPartBased;
    }

    private void OnBodyPartChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        // Sync all body part values from controls to state
        _headVariation = (byte)(_headNumericUpDown.Value ?? 1);
        _neckVariation = (byte)(_neckNumericUpDown.Value ?? 1);
        _torsoVariation = (byte)(_torsoNumericUpDown.Value ?? 1);
        _pelvisVariation = (byte)(_pelvisNumericUpDown.Value ?? 1);
        _beltVariation = (byte)(_beltNumericUpDown.Value ?? 0);
        _lShoulVariation = (byte)(_lShoulNumericUpDown.Value ?? 0);
        _rShoulVariation = (byte)(_rShoulNumericUpDown.Value ?? 0);
        _lBicepVariation = (byte)(_lBicepNumericUpDown.Value ?? 1);
        _rBicepVariation = (byte)(_rBicepNumericUpDown.Value ?? 1);
        _lFArmVariation = (byte)(_lFArmNumericUpDown.Value ?? 1);
        _rFArmVariation = (byte)(_rFArmNumericUpDown.Value ?? 1);
        _lHandVariation = (byte)(_lHandNumericUpDown.Value ?? 1);
        _rHandVariation = (byte)(_rHandNumericUpDown.Value ?? 1);
        _lThighVariation = (byte)(_lThighNumericUpDown.Value ?? 1);
        _rThighVariation = (byte)(_rThighNumericUpDown.Value ?? 1);
        _lShinVariation = (byte)(_lShinNumericUpDown.Value ?? 1);
        _rShinVariation = (byte)(_rShinNumericUpDown.Value ?? 1);
        _lFootVariation = (byte)(_lFootNumericUpDown.Value ?? 1);
        _rFootVariation = (byte)(_rFootNumericUpDown.Value ?? 1);
    }

    private void OnColorValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (!e.NewValue.HasValue) return;

        var value = (byte)Math.Clamp(e.NewValue.Value, 0, 175);

        if (sender == _skinColorNumericUpDown)
        {
            _skinColor = value;
            UpdateColorSwatch(_skinColorSwatch, PaletteColorService.Palettes.Skin, value);
        }
        else if (sender == _hairColorNumericUpDown)
        {
            _hairColor = value;
            UpdateColorSwatch(_hairColorSwatch, PaletteColorService.Palettes.Hair, value);
        }
        else if (sender == _tattoo1ColorNumericUpDown)
        {
            _tattoo1Color = value;
            UpdateColorSwatch(_tattoo1ColorSwatch, PaletteColorService.Palettes.Tattoo1, value);
        }
        else if (sender == _tattoo2ColorNumericUpDown)
        {
            _tattoo2Color = value;
            UpdateColorSwatch(_tattoo2ColorSwatch, PaletteColorService.Palettes.Tattoo2, value);
        }
    }

    private void OnSkinColorSwatchClick(object? sender, PointerPressedEventArgs e)
    {
        OpenColorPicker(PaletteColorService.Palettes.Skin, _skinColor, newIndex =>
        {
            _skinColor = newIndex;
            _skinColorNumericUpDown.Value = newIndex;
            UpdateColorSwatch(_skinColorSwatch, PaletteColorService.Palettes.Skin, newIndex);
        });
    }

    private void OnHairColorSwatchClick(object? sender, PointerPressedEventArgs e)
    {
        OpenColorPicker(PaletteColorService.Palettes.Hair, _hairColor, newIndex =>
        {
            _hairColor = newIndex;
            _hairColorNumericUpDown.Value = newIndex;
            UpdateColorSwatch(_hairColorSwatch, PaletteColorService.Palettes.Hair, newIndex);
        });
    }

    private void OnTattoo1ColorSwatchClick(object? sender, PointerPressedEventArgs e)
    {
        OpenColorPicker(PaletteColorService.Palettes.Tattoo1, _tattoo1Color, newIndex =>
        {
            _tattoo1Color = newIndex;
            _tattoo1ColorNumericUpDown.Value = newIndex;
            UpdateColorSwatch(_tattoo1ColorSwatch, PaletteColorService.Palettes.Tattoo1, newIndex);
        });
    }

    private void OnTattoo2ColorSwatchClick(object? sender, PointerPressedEventArgs e)
    {
        OpenColorPicker(PaletteColorService.Palettes.Tattoo2, _tattoo2Color, newIndex =>
        {
            _tattoo2Color = newIndex;
            _tattoo2ColorNumericUpDown.Value = newIndex;
            UpdateColorSwatch(_tattoo2ColorSwatch, PaletteColorService.Palettes.Tattoo2, newIndex);
        });
    }

    private async void OpenColorPicker(string paletteName, byte currentIndex, Action<byte> onColorSelected)
    {
        if (_paletteColorService == null) return;

        var picker = new ColorPickerWindow(_paletteColorService, paletteName, currentIndex);
        await picker.ShowDialog(this);

        if (picker.Confirmed)
        {
            onColorSelected(picker.SelectedColorIndex);
        }
    }

    private void UpdateColorSwatch(Border? swatch, string paletteName, byte colorIndex)
    {
        if (swatch == null || _paletteColorService == null) return;
        var color = _paletteColorService.GetPaletteColor(paletteName, colorIndex);
        swatch.Background = new SolidColorBrush(color);
    }

    private void UpdateAllColorSwatches()
    {
        UpdateColorSwatch(_skinColorSwatch, PaletteColorService.Palettes.Skin, _skinColor);
        UpdateColorSwatch(_hairColorSwatch, PaletteColorService.Palettes.Hair, _hairColor);
        UpdateColorSwatch(_tattoo1ColorSwatch, PaletteColorService.Palettes.Tattoo1, _tattoo1Color);
        UpdateColorSwatch(_tattoo2ColorSwatch, PaletteColorService.Palettes.Tattoo2, _tattoo2Color);
    }

    #endregion
}
