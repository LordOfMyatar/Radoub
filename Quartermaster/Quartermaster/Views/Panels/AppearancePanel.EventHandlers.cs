// AppearancePanel - Event handlers partial class
// All event wiring and handler methods

using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Quartermaster.Services;
using Quartermaster.Views.Dialogs;

namespace Quartermaster.Views.Panels;

public partial class AppearancePanel
{
    private void WireEvents()
    {
        if (_appearanceComboBox != null)
            _appearanceComboBox.SelectionChanged += OnAppearanceSelectionChanged;

        if (_genderComboBox != null)
            _genderComboBox.SelectionChanged += OnGenderSelectionChanged;

        if (_phenotypeComboBox != null)
            _phenotypeComboBox.SelectionChanged += OnPhenotypeSelectionChanged;

        if (_portraitComboBox != null)
            _portraitComboBox.SelectionChanged += OnPortraitSelectionChanged;

        // Color value changed events
        if (_skinColorNumeric != null)
            _skinColorNumeric.ValueChanged += OnColorValueChanged;
        if (_hairColorNumeric != null)
            _hairColorNumeric.ValueChanged += OnColorValueChanged;
        if (_tattoo1ColorNumeric != null)
            _tattoo1ColorNumeric.ValueChanged += OnColorValueChanged;
        if (_tattoo2ColorNumeric != null)
            _tattoo2ColorNumeric.ValueChanged += OnColorValueChanged;

        // Color swatch click events
        if (_skinColorSwatch != null)
        {
            _skinColorSwatch.Cursor = new Cursor(StandardCursorType.Hand);
            _skinColorSwatch.PointerPressed += OnSkinColorSwatchClicked;
        }
        if (_hairColorSwatch != null)
        {
            _hairColorSwatch.Cursor = new Cursor(StandardCursorType.Hand);
            _hairColorSwatch.PointerPressed += OnHairColorSwatchClicked;
        }
        if (_tattoo1ColorSwatch != null)
        {
            _tattoo1ColorSwatch.Cursor = new Cursor(StandardCursorType.Hand);
            _tattoo1ColorSwatch.PointerPressed += OnTattoo1ColorSwatchClicked;
        }
        if (_tattoo2ColorSwatch != null)
        {
            _tattoo2ColorSwatch.Cursor = new Cursor(StandardCursorType.Hand);
            _tattoo2ColorSwatch.PointerPressed += OnTattoo2ColorSwatchClicked;
        }

        // 3D Preview button events
        if (_rotateLeftButton != null)
            _rotateLeftButton.Click += OnRotateLeftClicked;
        if (_rotateRightButton != null)
            _rotateRightButton.Click += OnRotateRightClicked;
        if (_resetViewButton != null)
            _resetViewButton.Click += OnResetViewClicked;
        if (_zoomInButton != null)
            _zoomInButton.Click += OnZoomInClicked;
        if (_zoomOutButton != null)
            _zoomOutButton.Click += OnZoomOutClicked;
    }

    private void OnAppearanceSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isLoading || _appearanceComboBox?.SelectedItem is not ComboBoxItem item) return;

        if (item.Tag is ushort appearanceId)
        {
            var isPartBased = _displayService?.IsPartBasedAppearance(appearanceId) ?? false;
            UpdateBodyPartsEnabledState(isPartBased);

            if (_currentCreature != null)
            {
                _currentCreature.AppearanceType = appearanceId;
                UpdateModelPreview();
            }

            AppearanceChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnGenderSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isLoading || _genderComboBox?.SelectedItem is not ComboBoxItem item) return;

        if (item.Tag is byte genderId && _currentCreature != null)
        {
            _currentCreature.Gender = genderId;
            UpdateModelPreview();
            AppearanceChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnPhenotypeSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isLoading || _phenotypeComboBox?.SelectedItem is not ComboBoxItem item) return;

        if (item.Tag is int phenotypeId && _currentCreature != null)
        {
            _currentCreature.Phenotype = phenotypeId;
            UpdateModelPreview();
            AppearanceChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnPortraitSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isLoading || _portraitComboBox?.SelectedItem is not ComboBoxItem item) return;

        if (item.Tag is ushort portraitId && _currentCreature != null)
        {
            _currentCreature.PortraitId = portraitId;
            AppearanceChanged?.Invoke(this, EventArgs.Empty);
            PortraitChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnColorValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (_isLoading || _currentCreature == null || !e.NewValue.HasValue) return;

        // Clamp to valid byte range (0-175 per AXAML, but enforce 0-255 for safety)
        var rawValue = (int)Math.Clamp(e.NewValue.Value, 0, 255);
        var value = (byte)rawValue;

        if (sender == _skinColorNumeric)
        {
            _currentCreature.Color_Skin = value;
            UpdateColorSwatch(_skinColorSwatch, PaletteColorService.Palettes.Skin, value);
        }
        else if (sender == _hairColorNumeric)
        {
            _currentCreature.Color_Hair = value;
            UpdateColorSwatch(_hairColorSwatch, PaletteColorService.Palettes.Hair, value);
        }
        else if (sender == _tattoo1ColorNumeric)
        {
            _currentCreature.Color_Tattoo1 = value;
            UpdateColorSwatch(_tattoo1ColorSwatch, PaletteColorService.Palettes.Tattoo1, value);
        }
        else if (sender == _tattoo2ColorNumeric)
        {
            _currentCreature.Color_Tattoo2 = value;
            UpdateColorSwatch(_tattoo2ColorSwatch, PaletteColorService.Palettes.Tattoo2, value);
        }

        UpdateModelPreview();
        AppearanceChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnSkinColorSwatchClicked(object? sender, PointerPressedEventArgs e)
    {
        OpenColorPicker(PaletteColorService.Palettes.Skin, _currentCreature?.Color_Skin ?? 0, newIndex =>
        {
            if (_currentCreature != null) _currentCreature.Color_Skin = newIndex;
            if (_skinColorNumeric != null) _skinColorNumeric.Value = newIndex;
            UpdateColorSwatch(_skinColorSwatch, PaletteColorService.Palettes.Skin, newIndex);
            AppearanceChanged?.Invoke(this, EventArgs.Empty);
        });
    }

    private void OnHairColorSwatchClicked(object? sender, PointerPressedEventArgs e)
    {
        OpenColorPicker(PaletteColorService.Palettes.Hair, _currentCreature?.Color_Hair ?? 0, newIndex =>
        {
            if (_currentCreature != null) _currentCreature.Color_Hair = newIndex;
            if (_hairColorNumeric != null) _hairColorNumeric.Value = newIndex;
            UpdateColorSwatch(_hairColorSwatch, PaletteColorService.Palettes.Hair, newIndex);
            AppearanceChanged?.Invoke(this, EventArgs.Empty);
        });
    }

    private void OnTattoo1ColorSwatchClicked(object? sender, PointerPressedEventArgs e)
    {
        OpenColorPicker(PaletteColorService.Palettes.Tattoo1, _currentCreature?.Color_Tattoo1 ?? 0, newIndex =>
        {
            if (_currentCreature != null) _currentCreature.Color_Tattoo1 = newIndex;
            if (_tattoo1ColorNumeric != null) _tattoo1ColorNumeric.Value = newIndex;
            UpdateColorSwatch(_tattoo1ColorSwatch, PaletteColorService.Palettes.Tattoo1, newIndex);
            AppearanceChanged?.Invoke(this, EventArgs.Empty);
        });
    }

    private void OnTattoo2ColorSwatchClicked(object? sender, PointerPressedEventArgs e)
    {
        OpenColorPicker(PaletteColorService.Palettes.Tattoo2, _currentCreature?.Color_Tattoo2 ?? 0, newIndex =>
        {
            if (_currentCreature != null) _currentCreature.Color_Tattoo2 = newIndex;
            if (_tattoo2ColorNumeric != null) _tattoo2ColorNumeric.Value = newIndex;
            UpdateColorSwatch(_tattoo2ColorSwatch, PaletteColorService.Palettes.Tattoo2, newIndex);
            AppearanceChanged?.Invoke(this, EventArgs.Empty);
        });
    }

    private async void OpenColorPicker(string paletteName, byte currentIndex, Action<byte> onColorSelected)
    {
        if (_paletteColorService == null) return;

        var picker = new ColorPickerWindow(_paletteColorService, paletteName, currentIndex);

        var parentWindow = TopLevel.GetTopLevel(this) as Window;
        if (parentWindow != null)
        {
            await picker.ShowDialog(parentWindow);
        }
        else
        {
            picker.Show();
            return;
        }

        if (picker.Confirmed)
        {
            onColorSelected(picker.SelectedColorIndex);
        }
    }

    // 3D Preview control handlers
    private void OnRotateLeftClicked(object? sender, RoutedEventArgs e)
    {
        _modelPreview?.Rotate(-0.3f);
    }

    private void OnRotateRightClicked(object? sender, RoutedEventArgs e)
    {
        _modelPreview?.Rotate(0.3f);
    }

    private void OnResetViewClicked(object? sender, RoutedEventArgs e)
    {
        _modelPreview?.ResetView();
    }

    private void OnZoomInClicked(object? sender, RoutedEventArgs e)
    {
        if (_modelPreview != null)
            _modelPreview.Zoom *= 1.2f;
    }

    private void OnZoomOutClicked(object? sender, RoutedEventArgs e)
    {
        if (_modelPreview != null)
            _modelPreview.Zoom /= 1.2f;
    }
}
