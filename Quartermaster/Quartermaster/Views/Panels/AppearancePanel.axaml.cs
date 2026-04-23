using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Quartermaster.Controls;
using Quartermaster.Services;
using Radoub.Formats.Logging;
using Radoub.UI.Services;
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
    private TextBox? _appearanceSearchBox;
    private ListBox? _appearanceListBox;
    private CheckBox? _showBifCheckBox;
    private CheckBox? _showHakCheckBox;
    private CheckBox? _showOverrideCheckBox;
    private TextBlock? _appearanceCountText;
    private TextBox? _excludePatternBox;
    private ComboBox? _genderComboBox;
    private ComboBox? _phenotypeComboBox;

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

    // Color swatches
    private Border? _skinColorSwatch;
    private Border? _hairColorSwatch;
    private Border? _tattoo1ColorSwatch;
    private Border? _tattoo2ColorSwatch;

    // 3D Preview
    private Border? _modelPreviewContainer;
    private ModelPreviewGLControl? _modelPreviewGL;
    private Border? _modelPreviewInputSurface;
    private Border? _previewStateOverlay;
    private TextBlock? _previewStateText;
    private TextBlock? _modelInfoStatusText;
    private Button? _rotateLeftButton;
    private Button? _rotateRightButton;
    private Button? _resetViewButton;
    private Button? _zoomInButton;
    private Button? _zoomOutButton;
    private Button? _viewFrontButton;
    private Button? _viewBackButton;
    private Button? _viewSideButton;
    private Button? _viewSideRightButton;
    private Button? _viewTopButton;
    // Animation playback (#2124)
    private ComboBox? _animationComboBox;
    private Button? _animPlayButton;
    private Slider? _animTimeSlider;
    private Slider? _animSpeedSlider;

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
        _appearanceSearchBox = this.FindControl<TextBox>("AppearanceSearchBox");
        _appearanceListBox = this.FindControl<ListBox>("AppearanceListBox");
        _showBifCheckBox = this.FindControl<CheckBox>("ShowBifCheckBox");
        _showHakCheckBox = this.FindControl<CheckBox>("ShowHakCheckBox");
        _showOverrideCheckBox = this.FindControl<CheckBox>("ShowOverrideCheckBox");
        _appearanceCountText = this.FindControl<TextBlock>("AppearanceCountText");
        _excludePatternBox = this.FindControl<TextBox>("ExcludePatternBox");
        _genderComboBox = this.FindControl<ComboBox>("GenderComboBox");
        _phenotypeComboBox = this.FindControl<ComboBox>("PhenotypeComboBox");

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

        // Color swatches
        _skinColorSwatch = this.FindControl<Border>("SkinColorSwatch");
        _hairColorSwatch = this.FindControl<Border>("HairColorSwatch");
        _tattoo1ColorSwatch = this.FindControl<Border>("Tattoo1ColorSwatch");
        _tattoo2ColorSwatch = this.FindControl<Border>("Tattoo2ColorSwatch");

        // 3D Preview
        _modelPreviewContainer = this.FindControl<Border>("ModelPreviewContainer");
        _modelPreviewGL = this.FindControl<ModelPreviewGLControl>("ModelPreviewGL");
        _modelPreviewInputSurface = this.FindControl<Border>("ModelPreviewInputSurface");
        _previewStateOverlay = this.FindControl<Border>("PreviewStateOverlay");
        _previewStateText = this.FindControl<TextBlock>("PreviewStateText");
        _modelInfoStatusText = this.FindControl<TextBlock>("ModelInfoStatusText");
        _rotateLeftButton = this.FindControl<Button>("RotateLeftButton");
        _rotateRightButton = this.FindControl<Button>("RotateRightButton");
        _resetViewButton = this.FindControl<Button>("ResetViewButton");
        _zoomInButton = this.FindControl<Button>("ZoomInButton");
        _zoomOutButton = this.FindControl<Button>("ZoomOutButton");
        _viewFrontButton = this.FindControl<Button>("ViewFrontButton");
        _viewBackButton = this.FindControl<Button>("ViewBackButton");
        _viewSideButton = this.FindControl<Button>("ViewSideButton");
        _viewSideRightButton = this.FindControl<Button>("ViewSideRightButton");
        _viewTopButton = this.FindControl<Button>("ViewTopButton");
        _animationComboBox = this.FindControl<ComboBox>("AnimationComboBox");
        _animPlayButton = this.FindControl<Button>("AnimPlayButton");
        _animTimeSlider = this.FindControl<Slider>("AnimTimeSlider");
        _animSpeedSlider = this.FindControl<Slider>("AnimSpeedSlider");
    }

    public void SetDisplayService(CreatureDisplayService displayService)
    {
        _displayService = displayService;
        LoadAppearanceData();
        LoadBodyPartData();

        // Resolve appearance sources asynchronously (non-blocking)
        if (_appearances != null && _appearances.Count > 0)
        {
            _ = System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    displayService.Appearances.ResolveAppearanceSources(_appearances);
                    Avalonia.Threading.Dispatcher.UIThread.Post(RefreshFilteredAppearanceList);
                }
                catch (Exception ex)
                {
                    Radoub.Formats.Logging.UnifiedLogger.LogApplication(
                        Radoub.Formats.Logging.LogLevel.WARN,
                        $"AppearancePanel: Source resolution failed: {ex.Message}");
                }
            });
        }
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
        _modelPreviewGL?.SetTextureService(textureService);
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
        SelectGender(creature.Gender);
        SelectPhenotype(creature.Phenotype);

        // Body parts - update enabled state and values
        var isPartBased = _displayService?.IsPartBasedAppearance(creature.AppearanceType) ?? false;
        UpdateBodyPartsEnabledState(isPartBased);
        LoadBodyPartValues(creature);

        // Load model preview
        UpdateModelPreview();

        // Defer clearing _isLoading until after dispatcher processes queued SelectionChanged events
        Avalonia.Threading.Dispatcher.UIThread.Post(() => _isLoading = false, Avalonia.Threading.DispatcherPriority.Background);
    }

    /// <summary>
    /// Clear model and texture caches. Call when module context changes
    /// to prevent stale HAK resources from being used (#1867, #1869).
    /// </summary>
    public void ClearResourceCaches()
    {
        _modelService?.ClearCache();
        _textureService?.ClearCache();
        _modelPreviewGL?.ClearTextureCache();
        UnifiedLogger.LogApplication(LogLevel.INFO, "AppearancePanel: Cleared model/texture caches (module switch)");
    }

    public void ClearPanel()
    {
        if (_appearanceListBox != null)
            _appearanceListBox.SelectedIndex = -1;
        if (_appearanceSearchBox != null)
            _appearanceSearchBox.Text = "";
        if (_genderComboBox != null)
            _genderComboBox.SelectedIndex = -1;
        if (_phenotypeComboBox != null)
            _phenotypeComboBox.SelectedIndex = -1;

        UpdateBodyPartsEnabledState(false);
    }

    /// <summary>
    /// Set a model for the 3D preview.
    /// </summary>
    public void SetPreviewModel(MdlModel? model)
    {
        if (_modelPreviewGL != null)
            _modelPreviewGL.Model = model;
        RefreshAnimationList(model);
    }

    /// <summary>
    /// Populate the animation dropdown from a model's animation list (#2124).
    /// </summary>
    private void RefreshAnimationList(MdlModel? model)
    {
        if (_animationComboBox == null) return;

        var items = new List<string> { "(none)" };
        if (model?.Animations != null)
        {
            foreach (var anim in model.Animations)
            {
                if (!string.IsNullOrEmpty(anim.Name))
                    items.Add(anim.Name);
            }
        }

        _animationComboBox.ItemsSource = items;
        _animationComboBox.SelectedIndex = 0;
        if (_modelPreviewGL != null)
            _modelPreviewGL.SetActiveAnimation(null);
        if (_animTimeSlider != null)
        {
            _animTimeSlider.Value = 0;
            _animTimeSlider.Maximum = 1;
        }
    }

    /// <summary>
    /// Cycle the shader debug visualisation (0 -> 1 -> 2 -> 3 -> 4 -> 0).
    /// Used for diagnosing shading issues (#2026). Returns the new mode.
    /// </summary>
    public int CycleDebugMode()
    {
        if (_modelPreviewGL == null) return 0;
        var next = (_modelPreviewGL.DebugMode + 1) % 5;
        _modelPreviewGL.DebugMode = next;
        return next;
    }
}
