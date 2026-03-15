// AppearancePanel - Event handlers partial class
// All event wiring and handler methods

using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Quartermaster.Services;
using Quartermaster.Views.Dialogs;
using Radoub.Formats.Logging;

namespace Quartermaster.Views.Panels;

public partial class AppearancePanel
{
    private void WireEvents()
    {
        if (_appearanceListBox != null)
            _appearanceListBox.SelectionChanged += OnAppearanceSelectionChanged;

        if (_appearanceSearchBox != null)
            _appearanceSearchBox.TextChanged += OnAppearanceSearchChanged;

        if (_showBifCheckBox != null)
            _showBifCheckBox.IsCheckedChanged += OnSourceFilterChanged;
        if (_showHakCheckBox != null)
            _showHakCheckBox.IsCheckedChanged += OnSourceFilterChanged;
        if (_showOverrideCheckBox != null)
            _showOverrideCheckBox.IsCheckedChanged += OnSourceFilterChanged;

        if (_genderComboBox != null)
            _genderComboBox.SelectionChanged += OnGenderSelectionChanged;

        if (_phenotypeComboBox != null)
            _phenotypeComboBox.SelectionChanged += OnPhenotypeSelectionChanged;

        // Body part combo events
        WireBodyPartComboEvents();

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
        if (_isLoading || _appearanceListBox?.SelectedItem is not ListBoxItem item) return;

        try
        {
            if (item.Tag is ushort appearanceId)
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG,
                    $"AppearancePanel: Appearance changed to {appearanceId}");
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
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR,
                $"AppearancePanel: Appearance change failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void OnAppearanceSearchChanged(object? sender, TextChangedEventArgs e)
    {
        if (_isLoading) return;
        RefreshFilteredAppearanceList();
    }

    private void OnSourceFilterChanged(object? sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        RefreshFilteredAppearanceList();
    }

    private void OnGenderSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isLoading || _genderComboBox?.SelectedItem is not ComboBoxItem item) return;

        try
        {
            if (item.Tag is byte genderId && _currentCreature != null)
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG,
                    $"AppearancePanel: Gender changed to {genderId}");
                _currentCreature.Gender = genderId;
                UpdateModelPreview();
                AppearanceChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR,
                $"AppearancePanel: Gender change failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void OnPhenotypeSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isLoading || _phenotypeComboBox?.SelectedItem is not ComboBoxItem item) return;

        try
        {
            if (item.Tag is int phenotypeId && _currentCreature != null)
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG,
                    $"AppearancePanel: Phenotype changed to {phenotypeId}");
                _currentCreature.Phenotype = phenotypeId;
                UpdateModelPreview();
                AppearanceChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR,
                $"AppearancePanel: Phenotype change failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void WireBodyPartComboEvents()
    {
        // Central body parts
        if (_headComboBox != null) _headComboBox.SelectionChanged += OnHeadSelectionChanged;
        if (_neckComboBox != null) _neckComboBox.SelectionChanged += OnBodyPartSelectionChanged;
        if (_torsoComboBox != null) _torsoComboBox.SelectionChanged += OnBodyPartSelectionChanged;
        if (_pelvisComboBox != null) _pelvisComboBox.SelectionChanged += OnBodyPartSelectionChanged;
        if (_beltComboBox != null) _beltComboBox.SelectionChanged += OnBodyPartSelectionChanged;
        if (_tailComboBox != null) _tailComboBox.SelectionChanged += OnTailSelectionChanged;
        if (_wingsComboBox != null) _wingsComboBox.SelectionChanged += OnWingsSelectionChanged;

        // Limbs - left
        if (_lShoulComboBox != null) _lShoulComboBox.SelectionChanged += OnBodyPartSelectionChanged;
        if (_lBicepComboBox != null) _lBicepComboBox.SelectionChanged += OnBodyPartSelectionChanged;
        if (_lFArmComboBox != null) _lFArmComboBox.SelectionChanged += OnBodyPartSelectionChanged;
        if (_lHandComboBox != null) _lHandComboBox.SelectionChanged += OnBodyPartSelectionChanged;
        if (_lThighComboBox != null) _lThighComboBox.SelectionChanged += OnBodyPartSelectionChanged;
        if (_lShinComboBox != null) _lShinComboBox.SelectionChanged += OnBodyPartSelectionChanged;
        if (_lFootComboBox != null) _lFootComboBox.SelectionChanged += OnBodyPartSelectionChanged;

        // Limbs - right
        if (_rShoulComboBox != null) _rShoulComboBox.SelectionChanged += OnBodyPartSelectionChanged;
        if (_rBicepComboBox != null) _rBicepComboBox.SelectionChanged += OnBodyPartSelectionChanged;
        if (_rFArmComboBox != null) _rFArmComboBox.SelectionChanged += OnBodyPartSelectionChanged;
        if (_rHandComboBox != null) _rHandComboBox.SelectionChanged += OnBodyPartSelectionChanged;
        if (_rThighComboBox != null) _rThighComboBox.SelectionChanged += OnBodyPartSelectionChanged;
        if (_rShinComboBox != null) _rShinComboBox.SelectionChanged += OnBodyPartSelectionChanged;
        if (_rFootComboBox != null) _rFootComboBox.SelectionChanged += OnBodyPartSelectionChanged;
    }

    private void OnHeadSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isLoading || _currentCreature == null) return;
        try
        {
            if (_headComboBox?.SelectedItem is ComboBoxItem item && item.Tag is byte value)
            {
                _currentCreature.AppearanceHead = value;
                UpdateModelPreview();
                AppearanceChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR,
                $"AppearancePanel: Head change failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void OnTailSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isLoading || _currentCreature == null) return;
        if (_tailComboBox?.SelectedItem is ComboBoxItem item && item.Tag is byte value)
        {
            _currentCreature.Tail = value;
            UpdateModelPreview();
            AppearanceChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnWingsSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isLoading || _currentCreature == null) return;
        if (_wingsComboBox?.SelectedItem is ComboBoxItem item && item.Tag is byte value)
        {
            _currentCreature.Wings = value;
            UpdateModelPreview();
            AppearanceChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnBodyPartSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isLoading || _currentCreature == null) return;
        if (sender is not ComboBox combo || combo.SelectedItem is not ComboBoxItem item || item.Tag is not byte value)
            return;

        try
        {
            // Map combo to creature property
            if (sender == _neckComboBox) _currentCreature.BodyPart_Neck = value;
            else if (sender == _torsoComboBox) _currentCreature.BodyPart_Torso = value;
            else if (sender == _pelvisComboBox) _currentCreature.BodyPart_Pelvis = value;
            else if (sender == _beltComboBox) _currentCreature.BodyPart_Belt = value;
            else if (sender == _lShoulComboBox) _currentCreature.BodyPart_LShoul = value;
            else if (sender == _rShoulComboBox) _currentCreature.BodyPart_RShoul = value;
            else if (sender == _lBicepComboBox) _currentCreature.BodyPart_LBicep = value;
            else if (sender == _rBicepComboBox) _currentCreature.BodyPart_RBicep = value;
            else if (sender == _lFArmComboBox) _currentCreature.BodyPart_LFArm = value;
            else if (sender == _rFArmComboBox) _currentCreature.BodyPart_RFArm = value;
            else if (sender == _lHandComboBox) _currentCreature.BodyPart_LHand = value;
            else if (sender == _rHandComboBox) _currentCreature.BodyPart_RHand = value;
            else if (sender == _lThighComboBox) _currentCreature.BodyPart_LThigh = value;
            else if (sender == _rThighComboBox) _currentCreature.BodyPart_RThigh = value;
            else if (sender == _lShinComboBox) _currentCreature.BodyPart_LShin = value;
            else if (sender == _rShinComboBox) _currentCreature.BodyPart_RShin = value;
            else if (sender == _lFootComboBox) _currentCreature.BodyPart_LFoot = value;
            else if (sender == _rFootComboBox) _currentCreature.BodyPart_RFoot = value;
            else return; // Unknown combo, don't fire event

            UpdateModelPreview();
            AppearanceChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR,
                $"AppearancePanel: Body part change failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void OnSkinColorSwatchClicked(object? sender, PointerPressedEventArgs e)
    {
        OpenColorPicker(PaletteColorService.Palettes.Skin, _currentCreature?.Color_Skin ?? 0, newIndex =>
        {
            if (_currentCreature != null) _currentCreature.Color_Skin = newIndex;
            UpdateColorSwatch(_skinColorSwatch, PaletteColorService.Palettes.Skin, newIndex);
            UpdateModelPreview();
            AppearanceChanged?.Invoke(this, EventArgs.Empty);
        });
    }

    private void OnHairColorSwatchClicked(object? sender, PointerPressedEventArgs e)
    {
        OpenColorPicker(PaletteColorService.Palettes.Hair, _currentCreature?.Color_Hair ?? 0, newIndex =>
        {
            if (_currentCreature != null) _currentCreature.Color_Hair = newIndex;
            UpdateColorSwatch(_hairColorSwatch, PaletteColorService.Palettes.Hair, newIndex);
            UpdateModelPreview();
            AppearanceChanged?.Invoke(this, EventArgs.Empty);
        });
    }

    private void OnTattoo1ColorSwatchClicked(object? sender, PointerPressedEventArgs e)
    {
        OpenColorPicker(PaletteColorService.Palettes.Tattoo1, _currentCreature?.Color_Tattoo1 ?? 0, newIndex =>
        {
            if (_currentCreature != null) _currentCreature.Color_Tattoo1 = newIndex;
            UpdateColorSwatch(_tattoo1ColorSwatch, PaletteColorService.Palettes.Tattoo1, newIndex);
            UpdateModelPreview();
            AppearanceChanged?.Invoke(this, EventArgs.Empty);
        });
    }

    private void OnTattoo2ColorSwatchClicked(object? sender, PointerPressedEventArgs e)
    {
        OpenColorPicker(PaletteColorService.Palettes.Tattoo2, _currentCreature?.Color_Tattoo2 ?? 0, newIndex =>
        {
            if (_currentCreature != null) _currentCreature.Color_Tattoo2 = newIndex;
            UpdateColorSwatch(_tattoo2ColorSwatch, PaletteColorService.Palettes.Tattoo2, newIndex);
            UpdateModelPreview();
            AppearanceChanged?.Invoke(this, EventArgs.Empty);
        });
    }

    private async void OpenColorPicker(string paletteName, byte currentIndex, Action<byte> onColorSelected)
    {
        if (_paletteColorService == null) return;

        try
        {
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
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR,
                $"AppearancePanel: Color picker failed for '{paletteName}': {ex.GetType().Name}: {ex.Message}");
        }
    }

    // 3D Preview control handlers
    private void OnRotateLeftClicked(object? sender, RoutedEventArgs e)
    {
        _modelPreviewGL?.Rotate(-0.3f);
    }

    private void OnRotateRightClicked(object? sender, RoutedEventArgs e)
    {
        _modelPreviewGL?.Rotate(0.3f);
    }

    private void OnResetViewClicked(object? sender, RoutedEventArgs e)
    {
        _modelPreviewGL?.ResetView();
    }

    private void OnZoomInClicked(object? sender, RoutedEventArgs e)
    {
        if (_modelPreviewGL != null)
            _modelPreviewGL.Zoom *= 1.2f;
    }

    private void OnZoomOutClicked(object? sender, RoutedEventArgs e)
    {
        if (_modelPreviewGL != null)
            _modelPreviewGL.Zoom /= 1.2f;
    }
}
