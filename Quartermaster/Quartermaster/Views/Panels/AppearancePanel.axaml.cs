using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Quartermaster.Controls;
using Quartermaster.Services;
using Radoub.Formats.Mdl;
using Radoub.Formats.Utc;

namespace Quartermaster.Views.Panels;

/// <summary>
/// Panel for editing creature appearance settings.
/// Split into partial classes for maintainability.
/// </summary>
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
        FindControls();
        WireEvents();
    }

    private void FindControls()
    {
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

    public void ClearPanel()
    {
        if (_appearanceComboBox != null)
            _appearanceComboBox.SelectedIndex = -1;
        if (_phenotypeComboBox != null)
            _phenotypeComboBox.SelectedIndex = -1;
        if (_portraitComboBox != null)
            _portraitComboBox.SelectedIndex = -1;

        UpdateBodyPartsEnabledState(false);

        if (_skinColorNumeric != null)
            _skinColorNumeric.Value = 0;
        if (_hairColorNumeric != null)
            _hairColorNumeric.Value = 0;
        if (_tattoo1ColorNumeric != null)
            _tattoo1ColorNumeric.Value = 0;
        if (_tattoo2ColorNumeric != null)
            _tattoo2ColorNumeric.Value = 0;
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
