using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Quartermaster.Controls;
using Quartermaster.Services;
using Quartermaster.Views.Dialogs;
using Radoub.Formats.Mdl;
using Radoub.Formats.Utc;

namespace Quartermaster.Views.Panels;

public partial class AppearancePanel : UserControl
{
    // Appearance section
    private ComboBox? _appearanceComboBox;
    private ComboBox? _phenotypeComboBox;
    private ComboBox? _portraitComboBox;

    // Body parts section
    private Border? _bodyPartsSection;
    private StackPanel? _bodyPartsContent;
    private TextBlock? _bodyPartsStatusText;

    // Body part combos - central
    private ComboBox? _headComboBox;
    private ComboBox? _neckComboBox;
    private ComboBox? _torsoComboBox;
    private ComboBox? _pelvisComboBox;
    private ComboBox? _beltComboBox;
    private ComboBox? _tailComboBox;
    private ComboBox? _wingsComboBox;

    // Body part combos - limbs
    private ComboBox? _lShoulComboBox;
    private ComboBox? _rShoulComboBox;
    private ComboBox? _lBicepComboBox;
    private ComboBox? _rBicepComboBox;
    private ComboBox? _lFArmComboBox;
    private ComboBox? _rFArmComboBox;
    private ComboBox? _lHandComboBox;
    private ComboBox? _rHandComboBox;
    private ComboBox? _lThighComboBox;
    private ComboBox? _rThighComboBox;
    private ComboBox? _lShinComboBox;
    private ComboBox? _rShinComboBox;
    private ComboBox? _lFootComboBox;
    private ComboBox? _rFootComboBox;

    // Color controls
    private NumericUpDown? _skinColorNumeric;
    private NumericUpDown? _hairColorNumeric;
    private NumericUpDown? _tattoo1ColorNumeric;
    private NumericUpDown? _tattoo2ColorNumeric;

    // Color swatches
    private Border? _skinColorSwatch;
    private Border? _hairColorSwatch;
    private Border? _tattoo1ColorSwatch;
    private Border? _tattoo2ColorSwatch;

    // 3D Preview
    private Border? _modelPreviewContainer;
    private ModelPreviewControl? _modelPreview;
    private Button? _rotateLeftButton;
    private Button? _rotateRightButton;
    private Button? _resetViewButton;
    private Button? _zoomInButton;
    private Button? _zoomOutButton;

    private CreatureDisplayService? _displayService;
    private PaletteColorService? _paletteColorService;
    private ModelService? _modelService;
    private TextureService? _textureService;
    private UtcFile? _currentCreature;
    private List<AppearanceInfo>? _appearances;
    private List<PhenotypeInfo>? _phenotypes;
    private bool _isLoading;

    public event EventHandler? AppearanceChanged;

    public AppearancePanel()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

        // Appearance section
        _appearanceComboBox = this.FindControl<ComboBox>("AppearanceComboBox");
        _phenotypeComboBox = this.FindControl<ComboBox>("PhenotypeComboBox");
        _portraitComboBox = this.FindControl<ComboBox>("PortraitComboBox");

        // Body parts section
        _bodyPartsSection = this.FindControl<Border>("BodyPartsSection");
        _bodyPartsContent = this.FindControl<StackPanel>("BodyPartsContent");
        _bodyPartsStatusText = this.FindControl<TextBlock>("BodyPartsStatusText");

        // Body part combos - central
        _headComboBox = this.FindControl<ComboBox>("HeadComboBox");
        _neckComboBox = this.FindControl<ComboBox>("NeckComboBox");
        _torsoComboBox = this.FindControl<ComboBox>("TorsoComboBox");
        _pelvisComboBox = this.FindControl<ComboBox>("PelvisComboBox");
        _beltComboBox = this.FindControl<ComboBox>("BeltComboBox");
        _tailComboBox = this.FindControl<ComboBox>("TailComboBox");
        _wingsComboBox = this.FindControl<ComboBox>("WingsComboBox");

        // Body part combos - limbs
        _lShoulComboBox = this.FindControl<ComboBox>("LShoulComboBox");
        _rShoulComboBox = this.FindControl<ComboBox>("RShoulComboBox");
        _lBicepComboBox = this.FindControl<ComboBox>("LBicepComboBox");
        _rBicepComboBox = this.FindControl<ComboBox>("RBicepComboBox");
        _lFArmComboBox = this.FindControl<ComboBox>("LFArmComboBox");
        _rFArmComboBox = this.FindControl<ComboBox>("RFArmComboBox");
        _lHandComboBox = this.FindControl<ComboBox>("LHandComboBox");
        _rHandComboBox = this.FindControl<ComboBox>("RHandComboBox");
        _lThighComboBox = this.FindControl<ComboBox>("LThighComboBox");
        _rThighComboBox = this.FindControl<ComboBox>("RThighComboBox");
        _lShinComboBox = this.FindControl<ComboBox>("LShinComboBox");
        _rShinComboBox = this.FindControl<ComboBox>("RShinComboBox");
        _lFootComboBox = this.FindControl<ComboBox>("LFootComboBox");
        _rFootComboBox = this.FindControl<ComboBox>("RFootComboBox");

        // Color controls
        _skinColorNumeric = this.FindControl<NumericUpDown>("SkinColorNumeric");
        _hairColorNumeric = this.FindControl<NumericUpDown>("HairColorNumeric");
        _tattoo1ColorNumeric = this.FindControl<NumericUpDown>("Tattoo1ColorNumeric");
        _tattoo2ColorNumeric = this.FindControl<NumericUpDown>("Tattoo2ColorNumeric");

        // Color swatches
        _skinColorSwatch = this.FindControl<Border>("SkinColorSwatch");
        _hairColorSwatch = this.FindControl<Border>("HairColorSwatch");
        _tattoo1ColorSwatch = this.FindControl<Border>("Tattoo1ColorSwatch");
        _tattoo2ColorSwatch = this.FindControl<Border>("Tattoo2ColorSwatch");

        // 3D Preview
        _modelPreviewContainer = this.FindControl<Border>("ModelPreviewContainer");
        _modelPreview = this.FindControl<ModelPreviewControl>("ModelPreview");
        _rotateLeftButton = this.FindControl<Button>("RotateLeftButton");
        _rotateRightButton = this.FindControl<Button>("RotateRightButton");
        _resetViewButton = this.FindControl<Button>("ResetViewButton");
        _zoomInButton = this.FindControl<Button>("ZoomInButton");
        _zoomOutButton = this.FindControl<Button>("ZoomOutButton");

        // Wire up events
        if (_appearanceComboBox != null)
            _appearanceComboBox.SelectionChanged += OnAppearanceSelectionChanged;

        // Color value changed events
        if (_skinColorNumeric != null)
            _skinColorNumeric.ValueChanged += OnColorValueChanged;
        if (_hairColorNumeric != null)
            _hairColorNumeric.ValueChanged += OnColorValueChanged;
        if (_tattoo1ColorNumeric != null)
            _tattoo1ColorNumeric.ValueChanged += OnColorValueChanged;
        if (_tattoo2ColorNumeric != null)
            _tattoo2ColorNumeric.ValueChanged += OnColorValueChanged;

        // Color swatch click events - open color picker
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

    public void SetDisplayService(CreatureDisplayService displayService)
    {
        _displayService = displayService;
        LoadAppearanceData();
        LoadBodyPartData();
    }

    public void SetPaletteColorService(PaletteColorService paletteColorService)
    {
        _paletteColorService = paletteColorService;
    }

    public void SetModelService(ModelService modelService)
    {
        _modelService = modelService;
    }

    public void SetTextureService(TextureService textureService)
    {
        _textureService = textureService;
        _modelPreview?.SetTextureService(textureService);
    }

    private void LoadAppearanceData()
    {
        if (_displayService == null) return;

        _isLoading = true;

        // Load appearances from 2DA
        _appearances = _displayService.GetAllAppearances();
        if (_appearanceComboBox != null)
        {
            _appearanceComboBox.Items.Clear();
            foreach (var app in _appearances)
            {
                var displayText = app.IsPartBased
                    ? $"(Dynamic) {app.Name}"
                    : app.Name;
                _appearanceComboBox.Items.Add(new ComboBoxItem
                {
                    Content = displayText,
                    Tag = app.AppearanceId
                });
            }
        }

        // Load phenotypes from 2DA
        _phenotypes = _displayService.GetAllPhenotypes();
        if (_phenotypeComboBox != null)
        {
            _phenotypeComboBox.Items.Clear();
            foreach (var pheno in _phenotypes)
            {
                _phenotypeComboBox.Items.Add(new ComboBoxItem
                {
                    Content = pheno.Name,
                    Tag = pheno.PhenotypeId
                });
            }
        }

        // Load portraits from 2DA
        LoadPortraitData();

        _isLoading = false;
    }

    private void LoadPortraitData()
    {
        if (_displayService == null || _portraitComboBox == null) return;

        _portraitComboBox.Items.Clear();
        var portraits = _displayService.GetAllPortraits();
        foreach (var (id, name) in portraits)
        {
            _portraitComboBox.Items.Add(new ComboBoxItem
            {
                Content = name,
                Tag = id
            });
        }
    }

    private void LoadBodyPartData()
    {
        if (_displayService == null) return;

        // For now, populate with numeric values 0-20
        // TODO: Load from model_*.2da files when available
        void PopulateBodyPartCombo(ComboBox? combo, int max = 20)
        {
            if (combo == null) return;
            combo.Items.Clear();
            for (int i = 0; i <= max; i++)
            {
                combo.Items.Add(new ComboBoxItem { Content = i.ToString(), Tag = (byte)i });
            }
        }

        PopulateBodyPartCombo(_headComboBox, 30);
        PopulateBodyPartCombo(_neckComboBox);
        PopulateBodyPartCombo(_torsoComboBox);
        PopulateBodyPartCombo(_pelvisComboBox);
        PopulateBodyPartCombo(_beltComboBox);

        // Tail/Wings from 2DA
        LoadTailWingsData();

        // Limbs
        PopulateBodyPartCombo(_lShoulComboBox);
        PopulateBodyPartCombo(_rShoulComboBox);
        PopulateBodyPartCombo(_lBicepComboBox);
        PopulateBodyPartCombo(_rBicepComboBox);
        PopulateBodyPartCombo(_lFArmComboBox);
        PopulateBodyPartCombo(_rFArmComboBox);
        PopulateBodyPartCombo(_lHandComboBox);
        PopulateBodyPartCombo(_rHandComboBox);
        PopulateBodyPartCombo(_lThighComboBox);
        PopulateBodyPartCombo(_rThighComboBox);
        PopulateBodyPartCombo(_lShinComboBox);
        PopulateBodyPartCombo(_rShinComboBox);
        PopulateBodyPartCombo(_lFootComboBox);
        PopulateBodyPartCombo(_rFootComboBox);
    }

    private void LoadTailWingsData()
    {
        if (_displayService == null) return;

        // Load tails
        if (_tailComboBox != null)
        {
            _tailComboBox.Items.Clear();
            var tails = _displayService.GetAllTails();
            foreach (var (id, name) in tails)
            {
                _tailComboBox.Items.Add(new ComboBoxItem { Content = name, Tag = id });
            }
        }

        // Load wings
        if (_wingsComboBox != null)
        {
            _wingsComboBox.Items.Clear();
            var wings = _displayService.GetAllWings();
            foreach (var (id, name) in wings)
            {
                _wingsComboBox.Items.Add(new ComboBoxItem { Content = name, Tag = id });
            }
        }
    }

    public void LoadCreature(UtcFile? creature)
    {
        _isLoading = true;
        _currentCreature = creature;

        if (creature == null)
        {
            ClearPanel();
            _isLoading = false;
            return;
        }

        // Appearance - select in combo
        SelectAppearance(creature.AppearanceType);
        SelectPhenotype(creature.Phenotype);
        SelectPortrait(creature.PortraitId);

        // Body parts - update enabled state and values
        var isPartBased = _displayService?.IsPartBasedAppearance(creature.AppearanceType) ?? false;
        UpdateBodyPartsEnabledState(isPartBased);
        LoadBodyPartValues(creature);

        // Load model preview
        UpdateModelPreview();

        // Defer clearing _isLoading until after dispatcher processes queued SelectionChanged events
        Avalonia.Threading.Dispatcher.UIThread.Post(() => _isLoading = false, Avalonia.Threading.DispatcherPriority.Background);
    }

    private void UpdateModelPreview()
    {
        if (_modelService == null || _currentCreature == null || _modelPreview == null)
            return;

        try
        {
            // Update character colors for PLT rendering
            _modelPreview.SetCharacterColors(
                _currentCreature.Color_Skin,
                _currentCreature.Color_Hair,
                _currentCreature.Color_Tattoo1,
                _currentCreature.Color_Tattoo2);

            var model = _modelService.LoadCreatureModel(_currentCreature);
            _modelPreview.Model = model;
        }
        catch (Exception)
        {
            // Failed to load model - show empty preview
            _modelPreview.Model = null;
        }
    }

    private void UpdateBodyPartsEnabledState(bool isPartBased)
    {
        if (_bodyPartsContent != null)
            _bodyPartsContent.IsEnabled = isPartBased;

        if (_bodyPartsStatusText != null)
        {
            _bodyPartsStatusText.Text = isPartBased
                ? "(Dynamic Appearance)"
                : "(Static Appearance - body parts not editable)";
        }

        // Set opacity for visual feedback
        if (_bodyPartsContent != null)
            _bodyPartsContent.Opacity = isPartBased ? 1.0 : 0.5;
    }

    private void LoadBodyPartValues(UtcFile creature)
    {
        SelectComboByTag(_headComboBox, creature.AppearanceHead);
        SelectComboByTag(_neckComboBox, creature.BodyPart_Neck);
        SelectComboByTag(_torsoComboBox, creature.BodyPart_Torso);
        SelectComboByTag(_pelvisComboBox, creature.BodyPart_Pelvis);
        SelectComboByTag(_beltComboBox, creature.BodyPart_Belt);
        SelectComboByTag(_tailComboBox, creature.Tail);
        SelectComboByTag(_wingsComboBox, creature.Wings);

        SelectComboByTag(_lShoulComboBox, creature.BodyPart_LShoul);
        SelectComboByTag(_rShoulComboBox, creature.BodyPart_RShoul);
        SelectComboByTag(_lBicepComboBox, creature.BodyPart_LBicep);
        SelectComboByTag(_rBicepComboBox, creature.BodyPart_RBicep);
        SelectComboByTag(_lFArmComboBox, creature.BodyPart_LFArm);
        SelectComboByTag(_rFArmComboBox, creature.BodyPart_RFArm);
        SelectComboByTag(_lHandComboBox, creature.BodyPart_LHand);
        SelectComboByTag(_rHandComboBox, creature.BodyPart_RHand);
        SelectComboByTag(_lThighComboBox, creature.BodyPart_LThigh);
        SelectComboByTag(_rThighComboBox, creature.BodyPart_RThigh);
        SelectComboByTag(_lShinComboBox, creature.BodyPart_LShin);
        SelectComboByTag(_rShinComboBox, creature.BodyPart_RShin);
        SelectComboByTag(_lFootComboBox, creature.BodyPart_LFoot);
        SelectComboByTag(_rFootComboBox, creature.BodyPart_RFoot);

        // Colors
        if (_skinColorNumeric != null)
            _skinColorNumeric.Value = creature.Color_Skin;
        if (_hairColorNumeric != null)
            _hairColorNumeric.Value = creature.Color_Hair;
        if (_tattoo1ColorNumeric != null)
            _tattoo1ColorNumeric.Value = creature.Color_Tattoo1;
        if (_tattoo2ColorNumeric != null)
            _tattoo2ColorNumeric.Value = creature.Color_Tattoo2;

        // Update color swatches
        UpdateAllColorSwatches();
    }

    private void UpdateAllColorSwatches()
    {
        if (_currentCreature == null) return;

        UpdateColorSwatch(_skinColorSwatch, PaletteColorService.Palettes.Skin, _currentCreature.Color_Skin);
        UpdateColorSwatch(_hairColorSwatch, PaletteColorService.Palettes.Hair, _currentCreature.Color_Hair);
        UpdateColorSwatch(_tattoo1ColorSwatch, PaletteColorService.Palettes.Tattoo1, _currentCreature.Color_Tattoo1);
        UpdateColorSwatch(_tattoo2ColorSwatch, PaletteColorService.Palettes.Tattoo2, _currentCreature.Color_Tattoo2);
    }

    private void UpdateColorSwatch(Border? swatch, string paletteName, byte colorIndex)
    {
        if (swatch == null) return;

        if (_paletteColorService != null)
        {
            var color = _paletteColorService.GetPaletteColor(paletteName, colorIndex);
            swatch.Background = new SolidColorBrush(color);
        }
        else
        {
            swatch.Background = new SolidColorBrush(Colors.Gray);
        }
    }

    private void SelectComboByTag(ComboBox? combo, byte value)
    {
        if (combo == null) return;

        for (int i = 0; i < combo.Items.Count; i++)
        {
            if (combo.Items[i] is ComboBoxItem item && item.Tag is byte id && id == value)
            {
                combo.SelectedIndex = i;
                return;
            }
        }

        // If not found, add it
        combo.Items.Add(new ComboBoxItem { Content = value.ToString(), Tag = value });
        combo.SelectedIndex = combo.Items.Count - 1;
    }

    private void SelectAppearance(ushort appearanceId)
    {
        if (_appearanceComboBox == null || _appearances == null) return;

        for (int i = 0; i < _appearanceComboBox.Items.Count; i++)
        {
            if (_appearanceComboBox.Items[i] is ComboBoxItem item &&
                item.Tag is ushort id && id == appearanceId)
            {
                _appearanceComboBox.SelectedIndex = i;
                return;
            }
        }

        // If not found, add it
        _appearanceComboBox.Items.Add(new ComboBoxItem
        {
            Content = $"Appearance {appearanceId}",
            Tag = appearanceId
        });
        _appearanceComboBox.SelectedIndex = _appearanceComboBox.Items.Count - 1;
    }

    private void SelectPhenotype(int phenotypeId)
    {
        if (_phenotypeComboBox == null || _phenotypes == null) return;

        for (int i = 0; i < _phenotypeComboBox.Items.Count; i++)
        {
            if (_phenotypeComboBox.Items[i] is ComboBoxItem item &&
                item.Tag is int id && id == phenotypeId)
            {
                _phenotypeComboBox.SelectedIndex = i;
                return;
            }
        }

        // If not found, add it
        _phenotypeComboBox.Items.Add(new ComboBoxItem
        {
            Content = $"Phenotype {phenotypeId}",
            Tag = phenotypeId
        });
        _phenotypeComboBox.SelectedIndex = _phenotypeComboBox.Items.Count - 1;
    }

    private void SelectPortrait(ushort portraitId)
    {
        if (_portraitComboBox == null) return;

        for (int i = 0; i < _portraitComboBox.Items.Count; i++)
        {
            if (_portraitComboBox.Items[i] is ComboBoxItem item &&
                item.Tag is ushort id && id == portraitId)
            {
                _portraitComboBox.SelectedIndex = i;
                return;
            }
        }

        // If not found, add it
        var name = _displayService?.GetPortraitName(portraitId) ?? $"Portrait {portraitId}";
        _portraitComboBox.Items.Add(new ComboBoxItem
        {
            Content = name,
            Tag = portraitId
        });
        _portraitComboBox.SelectedIndex = _portraitComboBox.Items.Count - 1;
    }

    private void OnAppearanceSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isLoading || _appearanceComboBox?.SelectedItem is not ComboBoxItem item) return;

        if (item.Tag is ushort appearanceId)
        {
            var isPartBased = _displayService?.IsPartBasedAppearance(appearanceId) ?? false;
            UpdateBodyPartsEnabledState(isPartBased);

            // Update model preview when appearance changes
            if (_currentCreature != null)
            {
                _currentCreature.AppearanceType = appearanceId;
                UpdateModelPreview();
            }

            AppearanceChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnColorValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (_isLoading || _currentCreature == null) return;

        var value = (byte)(e.NewValue ?? 0);

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

        // Update model preview with new colors
        UpdateModelPreview();

        AppearanceChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ClearPanel()
    {
        // Clear appearance
        if (_appearanceComboBox != null)
            _appearanceComboBox.SelectedIndex = -1;
        if (_phenotypeComboBox != null)
            _phenotypeComboBox.SelectedIndex = -1;
        if (_portraitComboBox != null)
            _portraitComboBox.SelectedIndex = -1;

        // Disable body parts section
        UpdateBodyPartsEnabledState(false);

        // Clear colors
        if (_skinColorNumeric != null)
            _skinColorNumeric.Value = 0;
        if (_hairColorNumeric != null)
            _hairColorNumeric.Value = 0;
        if (_tattoo1ColorNumeric != null)
            _tattoo1ColorNumeric.Value = 0;
        if (_tattoo2ColorNumeric != null)
            _tattoo2ColorNumeric.Value = 0;
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

        // Get the parent window
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

    /// <summary>
    /// Set a model for the 3D preview.
    /// </summary>
    public void SetPreviewModel(MdlModel? model)
    {
        if (_modelPreview != null)
            _modelPreview.Model = model;
    }
}
