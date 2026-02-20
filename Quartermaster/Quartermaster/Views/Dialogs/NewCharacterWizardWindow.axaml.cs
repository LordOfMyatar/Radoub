using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Quartermaster.Services;
using Radoub.Formats.Gff;
using Radoub.Formats.Services;
using Radoub.Formats.Utc;
using Radoub.UI.Services;

namespace Quartermaster.Views.Dialogs;

/// <summary>
/// Multi-step wizard for creating a new creature from scratch.
/// 8 steps: File Type, Race &amp; Sex, Appearance, Class, Abilities, Skills, Spells, Summary.
/// Steps 5-8 are placeholders pending future sprints.
/// </summary>
public partial class NewCharacterWizardWindow : Window
{
    private readonly CreatureDisplayService _displayService;
    private readonly IGameDataService _gameDataService;
    private readonly ItemIconService? _itemIconService;

    // Wizard state
    private int _currentStep = 1;
    private const int TotalSteps = 8;

    // Step 1: File Type
    private bool _isBicFile; // false = UTC (default), true = BIC

    // Step 2: Race & Sex
    private byte _selectedRaceId = 6; // Human default
    private byte _selectedGender; // 0 = Male
    private List<RaceDisplayItem> _allRaces = new();
    private List<RaceDisplayItem> _filteredRaces = new();

    // Step 3: Appearance
    private ushort _selectedAppearanceId;
    private int _selectedPhenotype;
    private ushort _selectedPortraitId = 1;
    private byte _headVariation = 1;
    private byte _neckVariation = 1;
    private byte _torsoVariation = 1;
    private byte _pelvisVariation = 1;
    private byte _beltVariation;
    private byte _lShoulVariation;
    private byte _rShoulVariation;
    private byte _lBicepVariation = 1;
    private byte _rBicepVariation = 1;
    private byte _lFArmVariation = 1;
    private byte _rFArmVariation = 1;
    private byte _lHandVariation = 1;
    private byte _rHandVariation = 1;
    private byte _lThighVariation = 1;
    private byte _rThighVariation = 1;
    private byte _lShinVariation = 1;
    private byte _rShinVariation = 1;
    private byte _lFootVariation = 1;
    private byte _rFootVariation = 1;
    private byte _skinColor;
    private byte _hairColor;
    private byte _tattoo1Color;
    private byte _tattoo2Color;
    private bool _isPartBased;
    private bool _step3Loaded;
    private PaletteColorService? _paletteColorService;

    // Step 4: Class & Package
    private int _selectedClassId = -1;
    private byte _selectedPackageId = 255; // sentinel for none
    private int _favoredClassId = -1;
    private List<ClassDisplayItem> _allClasses = new();
    private List<ClassDisplayItem> _filteredClasses = new();
    private bool _step4Loaded;
    private bool _prestigePlanningExpanded;

    // Step 5: Ability Scores
    private readonly Dictionary<string, int> _abilityBaseScores = new()
    {
        { "STR", 8 }, { "DEX", 8 }, { "CON", 8 },
        { "INT", 8 }, { "WIS", 8 }, { "CHA", 8 }
    };
    private const int PointBuyTotal = 30;
    private const int AbilityMinBase = 8;
    private const int AbilityMaxBase = 18;
    private static readonly int[] PointBuyCosts = { 0, 1, 2, 3, 4, 5, 6, 8, 10, 13, 16 }; // index = score - 8
    private bool _step5Loaded;

    // Step 6: Skills
    private int _skillPointsTotal;
    private readonly Dictionary<int, int> _skillRanksAllocated = new();
    private HashSet<int> _classSkillIds = new();
    private HashSet<int> _unavailableSkillIds = new();
    private List<SkillDisplayItem> _allSkills = new();
    private List<SkillDisplayItem> _filteredSkills = new();
    private bool _step6Loaded;

    // Step 7: Spells
    private bool _needsSpellSelection;
    private bool _isDivineCaster;
    private bool _step7Loaded;
    private int _currentSpellLevel; // Currently selected spell level tab
    private int _maxSpellLevelForClass;
    private readonly Dictionary<int, List<int>> _selectedSpellsByLevel = new(); // spell level → list of spell IDs
    private List<SpellDisplayItem> _availableSpellsForLevel = new();
    private List<SpellDisplayItem> _filteredAvailableSpells = new();

    // Step 8: Summary
    private string _characterName = "";
    private byte _paletteId = 1;

    // Controls - navigation
    private readonly TextBlock _sidebarTitle;
    private readonly TextBlock _sidebarSummary;
    private readonly Border[] _stepBorders;
    private readonly Grid[] _stepPanels;
    private readonly Button _backButton;
    private readonly Button _nextButton;
    private readonly Button _finishButton;
    private readonly TextBlock _statusLabel;

    // Step 1 controls
    private readonly Border _utcCard;
    private readonly Border _bicCard;

    // Step 2 controls
    private readonly TextBox _raceSearchBox;
    private readonly ListBox _raceListBox;
    private readonly ToggleButton _maleToggle;
    private readonly ToggleButton _femaleToggle;
    private readonly TextBlock _selectedRaceNameLabel;
    private readonly Border _raceModifiersPanel;
    private readonly TextBlock _strModLabel;
    private readonly TextBlock _dexModLabel;
    private readonly TextBlock _conModLabel;
    private readonly TextBlock _intModLabel;
    private readonly TextBlock _wisModLabel;
    private readonly TextBlock _chaModLabel;
    private readonly StackPanel _raceTraitsPanel;
    private readonly TextBlock _favoredClassLabel;
    private readonly TextBlock _raceSizeLabel;
    private readonly Border _raceDescSeparator;
    private readonly TextBlock _raceDescriptionLabel;

    // Step 3 controls
    private readonly ComboBox _appearanceComboBox;
    private readonly ComboBox _phenotypeComboBox;
    private readonly Image _portraitPreviewImage;
    private readonly TextBlock _portraitNameLabel;
    private readonly NumericUpDown _headNumericUpDown;
    private readonly NumericUpDown _neckNumericUpDown;
    private readonly NumericUpDown _torsoNumericUpDown;
    private readonly NumericUpDown _pelvisNumericUpDown;
    private readonly NumericUpDown _beltNumericUpDown;
    private readonly NumericUpDown _lShoulNumericUpDown;
    private readonly NumericUpDown _rShoulNumericUpDown;
    private readonly NumericUpDown _lBicepNumericUpDown;
    private readonly NumericUpDown _rBicepNumericUpDown;
    private readonly NumericUpDown _lFArmNumericUpDown;
    private readonly NumericUpDown _rFArmNumericUpDown;
    private readonly NumericUpDown _lHandNumericUpDown;
    private readonly NumericUpDown _rHandNumericUpDown;
    private readonly NumericUpDown _lThighNumericUpDown;
    private readonly NumericUpDown _rThighNumericUpDown;
    private readonly NumericUpDown _lShinNumericUpDown;
    private readonly NumericUpDown _rShinNumericUpDown;
    private readonly NumericUpDown _lFootNumericUpDown;
    private readonly NumericUpDown _rFootNumericUpDown;
    private readonly NumericUpDown _skinColorNumericUpDown;
    private readonly NumericUpDown _hairColorNumericUpDown;
    private readonly NumericUpDown _tattoo1ColorNumericUpDown;
    private readonly NumericUpDown _tattoo2ColorNumericUpDown;
    private readonly Border _skinColorSwatch;
    private readonly Border _hairColorSwatch;
    private readonly Border _tattoo1ColorSwatch;
    private readonly Border _tattoo2ColorSwatch;
    private readonly TextBlock _bodyPartsNotApplicableLabel;
    private readonly StackPanel _bodyPartsContent;
    private readonly Grid _bodyPartsPanel;

    // Step 5 controls
    private readonly TextBlock _abilityPointsRemainingLabel;
    private readonly StackPanel _abilityRowsPanel;
    private readonly Border _prestigeAbilityBanner;
    private readonly TextBlock _prestigeAbilityBannerLabel;

    // Step 6 controls
    private readonly TextBlock _skillPointsRemainingLabel;
    private readonly StackPanel _skillRowsPanel;
    private readonly TextBox _skillSearchBox;

    // Step 7 controls
    private readonly TextBlock _spellStepDescription;
    private readonly StackPanel _spellLevelTabsPanel;
    private readonly TextBlock _spellSelectionCountLabel;
    private readonly TextBox _spellSearchBox2;
    private readonly ListBox _availableSpellsListBox;
    private readonly ListBox _selectedSpellsListBox;
    private readonly TextBlock _selectedSpellCountLabel;
    private readonly Border _divineSpellInfoPanel;
    private readonly TextBlock _divineSpellInfoLabel;

    // Step 8 controls
    private readonly TextBox _characterNameTextBox;
    private readonly TextBlock _generatedTagLabel;
    private readonly TextBlock _generatedResRefLabel;
    private readonly TextBlock _paletteIdLabelText;
    private readonly NumericUpDown _paletteIdNumericUpDown;
    private readonly TextBlock _paletteIdNote;
    private readonly TextBlock _summaryFileTypeLabel;
    private readonly TextBlock _summaryRaceLabel;
    private readonly TextBlock _summaryAppearanceLabel;
    private readonly TextBlock _summaryClassLabel;
    private readonly TextBlock _summaryAbilitiesLabel;
    private readonly TextBlock _summarySkillsLabel;
    private readonly TextBlock _summarySpellsLabel;
    private readonly Grid _summarySpellsSection;
    private readonly TextBlock _summaryFeatsLabel;

    // Step 4 controls
    private readonly TextBox _classSearchBox;
    private readonly ListBox _classListBox;
    private readonly TextBlock _selectedClassNameLabel;
    private readonly Border _classStatsPanel;
    private readonly TextBlock _classHitDieLabel;
    private readonly TextBlock _classSkillPointsLabel;
    private readonly TextBlock _classPrimaryAbilityLabel;
    private readonly TextBlock _classCasterLabel;
    private readonly TextBlock _classAlignmentLabel;
    private readonly StackPanel _packageSection;
    private readonly ComboBox _packageComboBox;
    private readonly Border _classDescSeparator;
    private readonly TextBlock _classDescriptionLabel;
    private readonly TextBlock _prestigeToggleArrow;
    private readonly StackPanel _prestigePlanningContent;
    private readonly ComboBox _prestigeClassComboBox;
    private readonly TextBlock _prestigePrereqLabel;

    /// <summary>
    /// The created creature, available after Confirmed is true.
    /// </summary>
    public UtcFile? CreatedCreature { get; private set; }

    /// <summary>
    /// Whether the user selected BIC file type.
    /// </summary>
    public bool IsBicFile => _isBicFile;

    /// <summary>
    /// Whether the user completed the wizard.
    /// </summary>
    public bool Confirmed { get; private set; }

    [Obsolete("Designer use only", error: true)]
    public NewCharacterWizardWindow() => throw new NotSupportedException("Use parameterized constructor");

    public NewCharacterWizardWindow(CreatureDisplayService displayService, IGameDataService gameDataService, ItemIconService? itemIconService = null)
    {
        InitializeComponent();

        _displayService = displayService;
        _gameDataService = gameDataService;
        _itemIconService = itemIconService;

        // Navigation controls
        _sidebarTitle = this.FindControl<TextBlock>("SidebarTitle")!;
        _sidebarSummary = this.FindControl<TextBlock>("SidebarSummary")!;

        _stepBorders = new[]
        {
            this.FindControl<Border>("Step1Border")!,
            this.FindControl<Border>("Step2Border")!,
            this.FindControl<Border>("Step3Border")!,
            this.FindControl<Border>("Step4Border")!,
            this.FindControl<Border>("Step5Border")!,
            this.FindControl<Border>("Step6Border")!,
            this.FindControl<Border>("Step7Border")!,
            this.FindControl<Border>("Step8Border")!
        };

        _stepPanels = new[]
        {
            this.FindControl<Grid>("Step1Panel")!,
            this.FindControl<Grid>("Step2Panel")!,
            this.FindControl<Grid>("Step3Panel")!,
            this.FindControl<Grid>("Step4Panel")!,
            this.FindControl<Grid>("Step5Panel")!,
            this.FindControl<Grid>("Step6Panel")!,
            this.FindControl<Grid>("Step7Panel")!,
            this.FindControl<Grid>("Step8Panel")!
        };

        _backButton = this.FindControl<Button>("BackButton")!;
        _nextButton = this.FindControl<Button>("NextButton")!;
        _finishButton = this.FindControl<Button>("FinishButton")!;
        _statusLabel = this.FindControl<TextBlock>("StatusLabel")!;

        // Step 1 controls
        _utcCard = this.FindControl<Border>("UtcCard")!;
        _bicCard = this.FindControl<Border>("BicCard")!;

        // Step 2 controls
        _raceSearchBox = this.FindControl<TextBox>("RaceSearchBox")!;
        _raceListBox = this.FindControl<ListBox>("RaceListBox")!;
        _maleToggle = this.FindControl<ToggleButton>("MaleToggle")!;
        _femaleToggle = this.FindControl<ToggleButton>("FemaleToggle")!;
        _selectedRaceNameLabel = this.FindControl<TextBlock>("SelectedRaceNameLabel")!;
        _raceModifiersPanel = this.FindControl<Border>("RaceModifiersPanel")!;
        _strModLabel = this.FindControl<TextBlock>("StrModLabel")!;
        _dexModLabel = this.FindControl<TextBlock>("DexModLabel")!;
        _conModLabel = this.FindControl<TextBlock>("ConModLabel")!;
        _intModLabel = this.FindControl<TextBlock>("IntModLabel")!;
        _wisModLabel = this.FindControl<TextBlock>("WisModLabel")!;
        _chaModLabel = this.FindControl<TextBlock>("ChaModLabel")!;
        _raceTraitsPanel = this.FindControl<StackPanel>("RaceTraitsPanel")!;
        _favoredClassLabel = this.FindControl<TextBlock>("FavoredClassLabel")!;
        _raceSizeLabel = this.FindControl<TextBlock>("RaceSizeLabel")!;
        _raceDescSeparator = this.FindControl<Border>("RaceDescSeparator")!;
        _raceDescriptionLabel = this.FindControl<TextBlock>("RaceDescriptionLabel")!;

        // Step 3 controls
        _appearanceComboBox = this.FindControl<ComboBox>("AppearanceComboBox")!;
        _phenotypeComboBox = this.FindControl<ComboBox>("PhenotypeComboBox")!;
        _portraitPreviewImage = this.FindControl<Image>("PortraitPreviewImage")!;
        _portraitNameLabel = this.FindControl<TextBlock>("PortraitNameLabel")!;
        _headNumericUpDown = this.FindControl<NumericUpDown>("HeadNumericUpDown")!;
        _neckNumericUpDown = this.FindControl<NumericUpDown>("NeckNumericUpDown")!;
        _torsoNumericUpDown = this.FindControl<NumericUpDown>("TorsoNumericUpDown")!;
        _pelvisNumericUpDown = this.FindControl<NumericUpDown>("PelvisNumericUpDown")!;
        _beltNumericUpDown = this.FindControl<NumericUpDown>("BeltNumericUpDown")!;
        _lShoulNumericUpDown = this.FindControl<NumericUpDown>("LShoulNumericUpDown")!;
        _rShoulNumericUpDown = this.FindControl<NumericUpDown>("RShoulNumericUpDown")!;
        _lBicepNumericUpDown = this.FindControl<NumericUpDown>("LBicepNumericUpDown")!;
        _rBicepNumericUpDown = this.FindControl<NumericUpDown>("RBicepNumericUpDown")!;
        _lFArmNumericUpDown = this.FindControl<NumericUpDown>("LFArmNumericUpDown")!;
        _rFArmNumericUpDown = this.FindControl<NumericUpDown>("RFArmNumericUpDown")!;
        _lHandNumericUpDown = this.FindControl<NumericUpDown>("LHandNumericUpDown")!;
        _rHandNumericUpDown = this.FindControl<NumericUpDown>("RHandNumericUpDown")!;
        _lThighNumericUpDown = this.FindControl<NumericUpDown>("LThighNumericUpDown")!;
        _rThighNumericUpDown = this.FindControl<NumericUpDown>("RThighNumericUpDown")!;
        _lShinNumericUpDown = this.FindControl<NumericUpDown>("LShinNumericUpDown")!;
        _rShinNumericUpDown = this.FindControl<NumericUpDown>("RShinNumericUpDown")!;
        _lFootNumericUpDown = this.FindControl<NumericUpDown>("LFootNumericUpDown")!;
        _rFootNumericUpDown = this.FindControl<NumericUpDown>("RFootNumericUpDown")!;
        _skinColorNumericUpDown = this.FindControl<NumericUpDown>("SkinColorNumericUpDown")!;
        _hairColorNumericUpDown = this.FindControl<NumericUpDown>("HairColorNumericUpDown")!;
        _tattoo1ColorNumericUpDown = this.FindControl<NumericUpDown>("Tattoo1ColorNumericUpDown")!;
        _tattoo2ColorNumericUpDown = this.FindControl<NumericUpDown>("Tattoo2ColorNumericUpDown")!;
        _skinColorSwatch = this.FindControl<Border>("SkinColorSwatch")!;
        _hairColorSwatch = this.FindControl<Border>("HairColorSwatch")!;
        _tattoo1ColorSwatch = this.FindControl<Border>("Tattoo1ColorSwatch")!;
        _tattoo2ColorSwatch = this.FindControl<Border>("Tattoo2ColorSwatch")!;
        _bodyPartsNotApplicableLabel = this.FindControl<TextBlock>("BodyPartsNotApplicableLabel")!;
        _bodyPartsContent = this.FindControl<StackPanel>("BodyPartsContent")!;
        _bodyPartsPanel = this.FindControl<Grid>("BodyPartsPanel")!;

        // Initialize palette color service for color swatches
        _paletteColorService = new PaletteColorService(_gameDataService);

        // Step 4 controls
        _classSearchBox = this.FindControl<TextBox>("ClassSearchBox")!;
        _classListBox = this.FindControl<ListBox>("ClassListBox")!;
        _selectedClassNameLabel = this.FindControl<TextBlock>("SelectedClassNameLabel")!;
        _classStatsPanel = this.FindControl<Border>("ClassStatsPanel")!;
        _classHitDieLabel = this.FindControl<TextBlock>("ClassHitDieLabel")!;
        _classSkillPointsLabel = this.FindControl<TextBlock>("ClassSkillPointsLabel")!;
        _classPrimaryAbilityLabel = this.FindControl<TextBlock>("ClassPrimaryAbilityLabel")!;
        _classCasterLabel = this.FindControl<TextBlock>("ClassCasterLabel")!;
        _classAlignmentLabel = this.FindControl<TextBlock>("ClassAlignmentLabel")!;
        _packageSection = this.FindControl<StackPanel>("PackageSection")!;
        _packageComboBox = this.FindControl<ComboBox>("PackageComboBox")!;
        _classDescSeparator = this.FindControl<Border>("ClassDescSeparator")!;
        _classDescriptionLabel = this.FindControl<TextBlock>("ClassDescriptionLabel")!;
        _prestigeToggleArrow = this.FindControl<TextBlock>("PrestigeToggleArrow")!;
        _prestigePlanningContent = this.FindControl<StackPanel>("PrestigePlanningContent")!;
        _prestigeClassComboBox = this.FindControl<ComboBox>("PrestigeClassComboBox")!;
        _prestigePrereqLabel = this.FindControl<TextBlock>("PrestigePrereqLabel")!;

        // Step 5 controls
        _abilityPointsRemainingLabel = this.FindControl<TextBlock>("AbilityPointsRemainingLabel")!;
        _abilityRowsPanel = this.FindControl<StackPanel>("AbilityRowsPanel")!;
        _prestigeAbilityBanner = this.FindControl<Border>("PrestigeAbilityBanner")!;
        _prestigeAbilityBannerLabel = this.FindControl<TextBlock>("PrestigeAbilityBannerLabel")!;

        // Step 6 controls
        _skillPointsRemainingLabel = this.FindControl<TextBlock>("SkillPointsRemainingLabel")!;
        _skillRowsPanel = this.FindControl<StackPanel>("SkillRowsPanel")!;
        _skillSearchBox = this.FindControl<TextBox>("SkillSearchBox")!;

        // Step 7 controls
        _spellStepDescription = this.FindControl<TextBlock>("SpellStepDescription")!;
        _spellLevelTabsPanel = this.FindControl<StackPanel>("SpellLevelTabsPanel")!;
        _spellSelectionCountLabel = this.FindControl<TextBlock>("SpellSelectionCountLabel")!;
        _spellSearchBox2 = this.FindControl<TextBox>("SpellSearchBox")!;
        _availableSpellsListBox = this.FindControl<ListBox>("AvailableSpellsListBox")!;
        _selectedSpellsListBox = this.FindControl<ListBox>("SelectedSpellsListBox")!;
        _selectedSpellCountLabel = this.FindControl<TextBlock>("SelectedSpellCountLabel")!;
        _divineSpellInfoPanel = this.FindControl<Border>("DivineSpellInfoPanel")!;
        _divineSpellInfoLabel = this.FindControl<TextBlock>("DivineSpellInfoLabel")!;

        // Step 8 controls
        _characterNameTextBox = this.FindControl<TextBox>("CharacterNameTextBox")!;
        _generatedTagLabel = this.FindControl<TextBlock>("GeneratedTagLabel")!;
        _generatedResRefLabel = this.FindControl<TextBlock>("GeneratedResRefLabel")!;
        _paletteIdLabelText = this.FindControl<TextBlock>("PaletteIdLabelText")!;
        _paletteIdNumericUpDown = this.FindControl<NumericUpDown>("PaletteIdNumericUpDown")!;
        _paletteIdNote = this.FindControl<TextBlock>("PaletteIdNote")!;
        _summaryFileTypeLabel = this.FindControl<TextBlock>("SummaryFileTypeLabel")!;
        _summaryRaceLabel = this.FindControl<TextBlock>("SummaryRaceLabel")!;
        _summaryAppearanceLabel = this.FindControl<TextBlock>("SummaryAppearanceLabel")!;
        _summaryClassLabel = this.FindControl<TextBlock>("SummaryClassLabel")!;
        _summaryAbilitiesLabel = this.FindControl<TextBlock>("SummaryAbilitiesLabel")!;
        _summarySkillsLabel = this.FindControl<TextBlock>("SummarySkillsLabel")!;
        _summarySpellsLabel = this.FindControl<TextBlock>("SummarySpellsLabel")!;
        _summarySpellsSection = this.FindControl<Grid>("SummarySpellsSection")!;
        _summaryFeatsLabel = this.FindControl<TextBlock>("SummaryFeatsLabel")!;

        UpdateStepDisplay();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    #region Step Navigation

    private void UpdateStepDisplay()
    {
        // Update step indicators
        for (int i = 0; i < TotalSteps; i++)
        {
            _stepBorders[i].Classes.Clear();
            _stepBorders[i].Classes.Add("step-indicator");

            if (i + 1 < _currentStep)
                _stepBorders[i].Classes.Add("completed");
            else if (i + 1 == _currentStep)
                _stepBorders[i].Classes.Add("current");
        }

        // Show/hide panels
        for (int i = 0; i < TotalSteps; i++)
        {
            _stepPanels[i].IsVisible = (i + 1 == _currentStep);
        }

        // Button state
        _backButton.IsEnabled = _currentStep > 1;
        _nextButton.IsVisible = _currentStep < TotalSteps;
        _finishButton.IsVisible = _currentStep == TotalSteps;

        ValidateCurrentStep();
        UpdateSidebarSummary();
    }

    private void ValidateCurrentStep()
    {
        bool canProceed = _currentStep switch
        {
            1 => true, // File type always has a selection (UTC default)
            2 => _selectedRaceId != 255, // Must have a race selected
            3 => true, // Appearance always has defaults
            4 => _selectedClassId >= 0, // Must have a class selected
            5 => GetAbilityPointsRemaining() == 0 || !_isBicFile, // BIC must spend all points
            6 => GetSkillPointsRemaining() >= 0, // Can't overspend
            7 => !_needsSpellSelection || _isDivineCaster || IsSpellSelectionComplete(),
            8 => true, // Summary is always valid (name is optional)
            _ => true
        };

        _nextButton.IsEnabled = canProceed;
        _finishButton.IsEnabled = canProceed;

        _statusLabel.Text = _currentStep switch
        {
            2 when !canProceed => "Select a race to continue.",
            4 when !canProceed => "Select a class to continue.",
            5 when !canProceed => $"Spend all {PointBuyTotal} ability points to continue.",
            7 when !canProceed => "Select all required spells to continue.",
            _ => ""
        };
    }

    private void PrepareCurrentStep()
    {
        switch (_currentStep)
        {
            case 2:
                PrepareStep2();
                break;
            case 3:
                PrepareStep3();
                break;
            case 4:
                PrepareStep4();
                break;
            case 5:
                PrepareStep5();
                break;
            case 6:
                PrepareStep6();
                break;
            case 7:
                PrepareStep7();
                if (!_needsSpellSelection)
                {
                    _currentStep++;
                    PrepareCurrentStep(); // Skip to step 8
                }
                break;
            case 8:
                PrepareStep8();
                break;
        }
    }

    private void OnNextClick(object? sender, RoutedEventArgs e)
    {
        if (_currentStep < TotalSteps)
        {
            _currentStep++;
            PrepareCurrentStep();
            UpdateStepDisplay();
        }
    }

    private void OnBackClick(object? sender, RoutedEventArgs e)
    {
        if (_currentStep > 1)
        {
            _currentStep--;

            // Skip spell step when going back if non-caster
            if (_currentStep == 7 && !_needsSpellSelection)
                _currentStep--;

            UpdateStepDisplay();
        }
    }

    private void OnFinishClick(object? sender, RoutedEventArgs e)
    {
        CreatedCreature = BuildCreature();
        Confirmed = true;
        Close();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Confirmed = false;
        Close();
    }

    private void UpdateSidebarSummary()
    {
        var parts = new List<string>();

        if (_currentStep >= 2 || _isBicFile)
            parts.Add(_isBicFile ? "BIC" : "UTC");

        if (_currentStep >= 2 && _selectedRaceId != 255)
        {
            var raceName = _displayService.GetRaceName(_selectedRaceId);
            var genderName = _selectedGender == 0 ? "Male" : "Female";
            parts.Add($"{genderName} {raceName}");
        }

        if (_currentStep >= 4 && _selectedClassId >= 0)
        {
            parts.Add(_displayService.GetClassName(_selectedClassId));
        }

        _sidebarSummary.Text = parts.Count > 0
            ? string.Join(" | ", parts)
            : "Choose your path...";
    }

    #endregion

    #region Step 1: File Type

    private void OnUtcCardClick(object? sender, PointerPressedEventArgs e)
    {
        _isBicFile = false;
        UpdateFileTypeCards();
    }

    private void OnBicCardClick(object? sender, PointerPressedEventArgs e)
    {
        _isBicFile = true;
        UpdateFileTypeCards();
    }

    private void UpdateFileTypeCards()
    {
        _utcCard.Classes.Clear();
        _utcCard.Classes.Add("file-type-card");
        if (!_isBicFile)
            _utcCard.Classes.Add("selected");

        _bicCard.Classes.Clear();
        _bicCard.Classes.Add("file-type-card");
        if (_isBicFile)
            _bicCard.Classes.Add("selected");

        UpdateSidebarSummary();
    }

    #endregion

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

        // Select Human by default if available
        var humanItem = _filteredRaces.FirstOrDefault(r => r.Id == 6);
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

    #region Step 3: Appearance

    private void PrepareStep3()
    {
        if (_step3Loaded)
            return;

        _step3Loaded = true;

        // Load appearances
        var appearances = _displayService.GetAllAppearances();
        _appearanceComboBox.ItemsSource = appearances;

        // Set default appearance based on race
        var defaultAppId = GetDefaultAppearanceForRace(_selectedRaceId);
        var defaultApp = appearances.FirstOrDefault(a => a.AppearanceId == defaultAppId);
        if (defaultApp != null)
            _appearanceComboBox.SelectedItem = defaultApp;
        else if (appearances.Count > 0)
            _appearanceComboBox.SelectedItem = appearances[0];

        // Load phenotypes
        var phenotypes = _displayService.GetAllPhenotypes();
        _phenotypeComboBox.ItemsSource = phenotypes;
        if (phenotypes.Count > 0)
            _phenotypeComboBox.SelectedItem = phenotypes[0];

        // Set default portrait
        UpdatePortraitDisplay();

        // Initialize color swatches
        UpdateAllColorSwatches();
    }

    private void OnAppearanceSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_appearanceComboBox.SelectedItem is not AppearanceInfo selected)
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

    private async void OnBrowsePortraitClick(object? sender, RoutedEventArgs e)
    {
        if (_itemIconService == null)
            return;

        var browser = new PortraitBrowserWindow(_gameDataService, _itemIconService);

        // Pre-populate filters based on wizard selections
        browser.SetInitialFilters(_selectedRaceId, _selectedGender);

        var result = await browser.ShowDialog<ushort?>(this);

        if (result.HasValue)
        {
            _selectedPortraitId = result.Value;
            UpdatePortraitDisplay();
        }
    }

    private void UpdatePortraitDisplay()
    {
        var resRef = _displayService.GetPortraitResRef(_selectedPortraitId);
        _portraitNameLabel.Text = resRef ?? $"Portrait {_selectedPortraitId}";

        // Load portrait preview image if icon service available
        if (_itemIconService != null && resRef != null)
        {
            var image = _itemIconService.GetPortrait(resRef);
            _portraitPreviewImage.Source = image;
        }
        else
        {
            _portraitPreviewImage.Source = null;
        }
    }

    #endregion

    #region Step 4: Class & Package

    private void PrepareStep4()
    {
        // Reload favored class (may have changed if race changed)
        _favoredClassId = _displayService.GetFavoredClass(_selectedRaceId);

        if (!_step4Loaded)
        {
            _step4Loaded = true;
            LoadPrestigeClasses();
        }

        LoadClassList();
    }

    private void LoadClassList()
    {
        var allMetadata = _displayService.Classes.GetAllClassMetadata();

        // For BIC: player classes only, base classes only (no prestige at level 1)
        // For UTC: all classes
        _allClasses = allMetadata
            .Where(c => !_isBicFile || (c.IsPlayerClass && !c.IsPrestige))
            .Select(c => new ClassDisplayItem
            {
                Id = c.ClassId,
                Name = c.Name,
                IsFavored = _favoredClassId >= 0 && c.ClassId == _favoredClassId
            })
            .OrderByDescending(c => c.IsFavored)
            .ThenBy(c => c.Name)
            .ToList();

        _filteredClasses = new List<ClassDisplayItem>(_allClasses);
        _classListBox.ItemsSource = _filteredClasses;

        // If previously selected class is still in list, re-select it
        if (_selectedClassId >= 0)
        {
            var existing = _filteredClasses.FirstOrDefault(c => c.Id == _selectedClassId);
            if (existing != null)
            {
                _classListBox.SelectedItem = existing;
                return;
            }
        }

        // Select favored class by default, or first class
        var favored = _filteredClasses.FirstOrDefault(c => c.IsFavored);
        if (favored != null)
            _classListBox.SelectedItem = favored;
        else if (_filteredClasses.Count > 0)
            _classListBox.SelectedItem = _filteredClasses[0];
    }

    private void OnClassSearchChanged(object? sender, TextChangedEventArgs e)
    {
        var filter = _classSearchBox.Text?.Trim() ?? "";

        if (string.IsNullOrEmpty(filter))
        {
            _filteredClasses = new List<ClassDisplayItem>(_allClasses);
        }
        else
        {
            _filteredClasses = _allClasses
                .Where(c => c.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        _classListBox.ItemsSource = _filteredClasses;
    }

    private void OnClassSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_classListBox.SelectedItem is not ClassDisplayItem selected)
            return;

        _selectedClassId = selected.Id;
        UpdateClassDetailPanel();
        LoadPackagesForClass();
        ValidateCurrentStep();
        UpdateSidebarSummary();
    }

    private void UpdateClassDetailPanel()
    {
        var metadata = _displayService.Classes.GetClassMetadata(_selectedClassId);
        _selectedClassNameLabel.Text = metadata.Name;
        _classStatsPanel.IsVisible = true;

        // Stats
        _classHitDieLabel.Text = $"Hit Die: d{metadata.HitDie}";
        _classSkillPointsLabel.Text = $"Skill Points: {metadata.SkillPointsPerLevel} + INT";

        // Primary ability
        var primaryAbility = metadata.PrimaryAbility;
        _classPrimaryAbilityLabel.Text = string.IsNullOrEmpty(primaryAbility) || primaryAbility == "****"
            ? "Primary: —"
            : $"Primary: {FormatAbilityName(primaryAbility)}";

        // Spellcasting info
        if (metadata.IsCaster)
        {
            var casterType = metadata.IsSpontaneousCaster ? "Spontaneous" : "Prepared";
            _classCasterLabel.Text = $"Spellcasting: {casterType}";
        }
        else
        {
            _classCasterLabel.Text = "Spellcasting: None";
        }

        // Alignment restrictions
        if (metadata.AlignmentRestriction != null)
        {
            var alignDesc = FormatAlignmentRestriction(metadata.AlignmentRestriction);
            _classAlignmentLabel.Text = alignDesc;
            _classAlignmentLabel.IsVisible = !string.IsNullOrEmpty(alignDesc);
        }
        else
        {
            _classAlignmentLabel.IsVisible = false;
        }

        // Description
        var desc = _displayService.Classes.GetClassDescription(_selectedClassId);
        if (!string.IsNullOrEmpty(desc))
        {
            _classDescriptionLabel.Text = desc;
            _classDescriptionLabel.Foreground = null;
            _classDescSeparator.IsVisible = true;
        }
        else
        {
            _classDescriptionLabel.Text = "No description available.";
            _classDescSeparator.IsVisible = false;
        }
    }

    private void LoadPackagesForClass()
    {
        var packages = _displayService.GetPackagesForClass(_selectedClassId);

        if (packages.Count > 0)
        {
            var packageItems = packages.Select(p => new PackageDisplayItem
            {
                Id = p.Id,
                Name = p.Name
            }).ToList();

            _packageComboBox.ItemsSource = packageItems;
            _packageComboBox.SelectedItem = packageItems[0];
            _packageSection.IsVisible = true;
        }
        else
        {
            _packageComboBox.ItemsSource = null;
            _packageSection.IsVisible = false;
            _selectedPackageId = 255;
        }
    }

    private void OnPackageSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_packageComboBox.SelectedItem is not PackageDisplayItem selected)
            return;

        _selectedPackageId = selected.Id;
    }

    private static string FormatAbilityName(string abilityCode) => abilityCode.ToUpperInvariant() switch
    {
        "STR" => "Strength",
        "DEX" => "Dexterity",
        "CON" => "Constitution",
        "INT" => "Intelligence",
        "WIS" => "Wisdom",
        "CHA" => "Charisma",
        _ => abilityCode
    };

    private static string FormatAlignmentRestriction(AlignmentRestriction restriction)
    {
        var parts = new List<string>();
        if ((restriction.RestrictionMask & 0x02) != 0) parts.Add("Lawful");
        if ((restriction.RestrictionMask & 0x04) != 0) parts.Add("Chaotic");
        if ((restriction.RestrictionMask & 0x08) != 0) parts.Add("Good");
        if ((restriction.RestrictionMask & 0x10) != 0) parts.Add("Evil");
        if ((restriction.RestrictionMask & 0x01) != 0) parts.Add("Neutral");

        if (parts.Count == 0) return "";

        string verb = restriction.Inverted ? "Cannot be" : "Must be";
        return $"{verb}: {string.Join(" or ", parts)}";
    }

    #endregion

    #region Prestige Planning

    private void LoadPrestigeClasses()
    {
        var allMetadata = _displayService.Classes.GetAllClassMetadata();
        var prestigeClasses = allMetadata
            .Where(c => c.IsPrestige && c.IsPlayerClass)
            .Select(c => new ClassDisplayItem { Id = c.ClassId, Name = c.Name })
            .OrderBy(c => c.Name)
            .ToList();

        _prestigeClassComboBox.ItemsSource = prestigeClasses;
    }

    private void OnPrestigePlanningToggle(object? sender, PointerPressedEventArgs e)
    {
        _prestigePlanningExpanded = !_prestigePlanningExpanded;
        _prestigePlanningContent.IsVisible = _prestigePlanningExpanded;
        _prestigeToggleArrow.Text = _prestigePlanningExpanded ? "▾" : "▸";
    }

    private void OnPrestigeClassSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_prestigeClassComboBox.SelectedItem is not ClassDisplayItem selected)
            return;

        var prereqs = _displayService.Classes.GetPrestigePrerequisites(selected.Id);

        if (prereqs.Count == 0)
        {
            _prestigePrereqLabel.Text = "No prerequisites listed.";
            return;
        }

        var lines = new List<string>();
        foreach (var prereq in prereqs)
        {
            string desc = prereq.Type switch
            {
                PrereqType.Feat => $"Feat: {_displayService.GetFeatName(prereq.Param1)}",
                PrereqType.FeatOr => $"  or: {_displayService.GetFeatName(prereq.Param1)}",
                PrereqType.Skill => $"Skill: {_displayService.Skills.GetSkillName(prereq.Param1)} ({prereq.Param2}+ ranks)",
                PrereqType.Bab => $"Base Attack Bonus: +{prereq.Param1}",
                PrereqType.Race => $"Race: {_displayService.GetRaceName((byte)prereq.Param1)}",
                PrereqType.ArcaneSpell => prereq.Param1 > 0
                    ? $"Can cast arcane spells level {prereq.Param1}+"
                    : "Can cast arcane spells",
                PrereqType.DivineSpell => prereq.Param1 > 0
                    ? $"Can cast divine spells level {prereq.Param1}+"
                    : "Can cast divine spells",
                _ => prereq.Label
            };
            lines.Add(desc);
        }

        _prestigePrereqLabel.Text = string.Join("\n", lines);
    }

    #endregion

    #region Step 5: Ability Scores

    private static readonly string[] AbilityNames = { "STR", "DEX", "CON", "INT", "WIS", "CHA" };
    private static readonly string[] AbilityFullNames = { "Strength", "Dexterity", "Constitution", "Intelligence", "Wisdom", "Charisma" };

    private void PrepareStep5()
    {
        if (!_step5Loaded)
        {
            _step5Loaded = true;
            BuildAbilityRows();
        }

        UpdateAbilityDisplay();
        UpdatePrestigeAbilityBanner();
    }

    private void BuildAbilityRows()
    {
        _abilityRowsPanel.Children.Clear();

        for (int i = 0; i < AbilityNames.Length; i++)
        {
            var ability = AbilityNames[i];
            var row = new Grid
            {
                ColumnDefinitions = ColumnDefinitions.Parse("120,70,35,35,70,70,70,*"),
                Margin = new Avalonia.Thickness(0, 2)
            };

            // Ability name
            var nameLabel = new TextBlock
            {
                Text = AbilityFullNames[i],
                FontWeight = FontWeight.SemiBold,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };
            Grid.SetColumn(nameLabel, 0);
            row.Children.Add(nameLabel);

            // Base score
            var baseLabel = new TextBlock
            {
                Text = _abilityBaseScores[ability].ToString(),
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                FontSize = 16,
                FontWeight = FontWeight.Bold,
                Tag = $"Base_{ability}"
            };
            Grid.SetColumn(baseLabel, 1);
            row.Children.Add(baseLabel);

            // [-] button
            var decreaseBtn = new Button
            {
                Content = "−",
                Width = 28,
                HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                Tag = ability
            };
            decreaseBtn.Click += OnAbilityDecrease;
            Grid.SetColumn(decreaseBtn, 2);
            row.Children.Add(decreaseBtn);

            // [+] button
            var increaseBtn = new Button
            {
                Content = "+",
                Width = 28,
                HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                Tag = ability
            };
            increaseBtn.Click += OnAbilityIncrease;
            Grid.SetColumn(increaseBtn, 3);
            row.Children.Add(increaseBtn);

            // Racial modifier
            var racialLabel = new TextBlock
            {
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Tag = $"Racial_{ability}"
            };
            Grid.SetColumn(racialLabel, 4);
            row.Children.Add(racialLabel);

            // Total score
            var totalLabel = new TextBlock
            {
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                FontWeight = FontWeight.Bold,
                FontSize = 16,
                Tag = $"Total_{ability}"
            };
            Grid.SetColumn(totalLabel, 5);
            row.Children.Add(totalLabel);

            // Modifier
            var modLabel = new TextBlock
            {
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Tag = $"Mod_{ability}"
            };
            Grid.SetColumn(modLabel, 6);
            row.Children.Add(modLabel);

            // Cost
            var costLabel = new TextBlock
            {
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Foreground = this.FindResource("SystemControlForegroundBaseMediumLowBrush") as IBrush,
                Tag = $"Cost_{ability}"
            };
            Grid.SetColumn(costLabel, 7);
            row.Children.Add(costLabel);

            _abilityRowsPanel.Children.Add(row);
        }
    }

    private void UpdateAbilityDisplay()
    {
        var racialMods = _displayService.GetRacialModifiers(_selectedRaceId);

        foreach (var row in _abilityRowsPanel.Children.OfType<Grid>())
        {
            foreach (var child in row.Children)
            {
                if (child is not TextBlock tb || child.Tag is not string tag)
                    continue;

                if (tag.StartsWith("Base_"))
                {
                    var ability = tag[5..];
                    tb.Text = _abilityBaseScores[ability].ToString();
                }
                else if (tag.StartsWith("Racial_"))
                {
                    var ability = tag[7..];
                    int racialMod = GetRacialModForAbility(racialMods, ability);
                    if (racialMod == 0)
                    {
                        tb.Text = "—";
                        tb.ClearValue(TextBlock.ForegroundProperty);
                    }
                    else
                    {
                        tb.Text = CreatureDisplayService.FormatBonus(racialMod);
                        tb.Foreground = racialMod > 0
                            ? BrushManager.GetSuccessBrush(this)
                            : BrushManager.GetWarningBrush(this);
                    }
                }
                else if (tag.StartsWith("Total_"))
                {
                    var ability = tag[6..];
                    int baseScore = _abilityBaseScores[ability];
                    int racialMod = GetRacialModForAbility(racialMods, ability);
                    int total = baseScore + racialMod;
                    tb.Text = total.ToString();
                }
                else if (tag.StartsWith("Mod_"))
                {
                    var ability = tag[4..];
                    int baseScore = _abilityBaseScores[ability];
                    int racialMod = GetRacialModForAbility(racialMods, ability);
                    int total = baseScore + racialMod;
                    int bonus = CreatureDisplayService.CalculateAbilityBonus(total);
                    tb.Text = CreatureDisplayService.FormatBonus(bonus);
                }
                else if (tag.StartsWith("Cost_"))
                {
                    var ability = tag[5..];
                    int baseScore = _abilityBaseScores[ability];
                    int costIndex = baseScore - AbilityMinBase;
                    int cost = costIndex >= 0 && costIndex < PointBuyCosts.Length ? PointBuyCosts[costIndex] : 0;
                    tb.Text = cost.ToString();
                }
            }

            // Update button enabled states
            foreach (var child in row.Children)
            {
                if (child is Button btn && btn.Tag is string ability)
                {
                    int baseScore = _abilityBaseScores[ability];
                    int remaining = GetAbilityPointsRemaining();

                    if (btn.Content?.ToString() == "−")
                        btn.IsEnabled = baseScore > AbilityMinBase;
                    else if (btn.Content?.ToString() == "+")
                    {
                        int nextCostIndex = baseScore + 1 - AbilityMinBase;
                        int nextCost = nextCostIndex < PointBuyCosts.Length ? PointBuyCosts[nextCostIndex] : int.MaxValue;
                        int currentCost = PointBuyCosts[baseScore - AbilityMinBase];
                        int costDelta = nextCost - currentCost;
                        btn.IsEnabled = baseScore < AbilityMaxBase && remaining >= costDelta;
                    }
                }
            }
        }

        // Update points remaining
        int pointsRemaining = GetAbilityPointsRemaining();
        _abilityPointsRemainingLabel.Text = pointsRemaining.ToString();

        if (pointsRemaining > 0)
            _abilityPointsRemainingLabel.Foreground = BrushManager.GetSuccessBrush(this);
        else if (pointsRemaining == 0)
            _abilityPointsRemainingLabel.ClearValue(TextBlock.ForegroundProperty);
        else
            _abilityPointsRemainingLabel.Foreground = BrushManager.GetErrorBrush(this);

        ValidateCurrentStep();
    }

    private int GetAbilityPointsRemaining()
    {
        int spent = 0;
        foreach (var ability in AbilityNames)
        {
            int costIndex = _abilityBaseScores[ability] - AbilityMinBase;
            if (costIndex >= 0 && costIndex < PointBuyCosts.Length)
                spent += PointBuyCosts[costIndex];
        }
        return PointBuyTotal - spent;
    }

    private static int GetRacialModForAbility(RacialModifiers mods, string ability) => ability switch
    {
        "STR" => mods.Str,
        "DEX" => mods.Dex,
        "CON" => mods.Con,
        "INT" => mods.Int,
        "WIS" => mods.Wis,
        "CHA" => mods.Cha,
        _ => 0
    };

    private void OnAbilityIncrease(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string ability)
        {
            if (_abilityBaseScores[ability] < AbilityMaxBase)
            {
                int currentScore = _abilityBaseScores[ability];
                int nextCostIndex = currentScore + 1 - AbilityMinBase;
                int nextCost = nextCostIndex < PointBuyCosts.Length ? PointBuyCosts[nextCostIndex] : int.MaxValue;
                int currentCost = PointBuyCosts[currentScore - AbilityMinBase];
                int costDelta = nextCost - currentCost;

                if (GetAbilityPointsRemaining() >= costDelta)
                {
                    _abilityBaseScores[ability]++;
                    UpdateAbilityDisplay();
                }
            }
        }
    }

    private void OnAbilityDecrease(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string ability)
        {
            if (_abilityBaseScores[ability] > AbilityMinBase)
            {
                _abilityBaseScores[ability]--;
                UpdateAbilityDisplay();
            }
        }
    }

    private void OnAbilityAutoAssignClick(object? sender, RoutedEventArgs e)
    {
        // Reset all to base 8
        foreach (var ability in AbilityNames)
            _abilityBaseScores[ability] = AbilityMinBase;

        // Read package primary ability from packages.2da Attribute column
        string? primaryAbility = null;
        if (_selectedPackageId != 255)
        {
            primaryAbility = _gameDataService.Get2DAValue("packages", _selectedPackageId, "Attribute")?.ToUpperInvariant();
        }

        // Default distribution: balanced with emphasis on primary ability
        // Strategy: primary ability gets priority, then spread remaining across useful stats
        if (!string.IsNullOrEmpty(primaryAbility) && primaryAbility != "****" && _abilityBaseScores.ContainsKey(primaryAbility))
        {
            // Push primary ability to 16 (cost 10), leaves 20 points
            _abilityBaseScores[primaryAbility] = 16;
            int remaining = GetAbilityPointsRemaining();

            // Distribute remaining points across other abilities
            // Priority: CON > DEX > other stats
            var priorityOrder = primaryAbility switch
            {
                "STR" => new[] { "CON", "DEX", "WIS", "INT", "CHA" },
                "DEX" => new[] { "CON", "STR", "WIS", "INT", "CHA" },
                "CON" => new[] { "STR", "DEX", "WIS", "INT", "CHA" },
                "INT" => new[] { "CON", "DEX", "WIS", "STR", "CHA" },
                "WIS" => new[] { "CON", "DEX", "INT", "STR", "CHA" },
                "CHA" => new[] { "CON", "DEX", "WIS", "INT", "STR" },
                _ => new[] { "CON", "DEX", "WIS", "INT", "CHA" }
            };

            // Try to raise each secondary ability to 14 (cost 6), then 12 (cost 4)
            foreach (var target in new[] { 14, 12 })
            {
                foreach (var ability in priorityOrder)
                {
                    while (_abilityBaseScores[ability] < target)
                    {
                        int currentScore = _abilityBaseScores[ability];
                        int nextCostIndex = currentScore + 1 - AbilityMinBase;
                        if (nextCostIndex >= PointBuyCosts.Length) break;
                        int costDelta = PointBuyCosts[nextCostIndex] - PointBuyCosts[currentScore - AbilityMinBase];
                        if (GetAbilityPointsRemaining() < costDelta) break;
                        _abilityBaseScores[ability]++;
                    }
                }
            }

            // Spend any remaining single points
            foreach (var ability in priorityOrder)
            {
                while (GetAbilityPointsRemaining() > 0 && _abilityBaseScores[ability] < AbilityMaxBase)
                {
                    int currentScore = _abilityBaseScores[ability];
                    int nextCostIndex = currentScore + 1 - AbilityMinBase;
                    if (nextCostIndex >= PointBuyCosts.Length) break;
                    int costDelta = PointBuyCosts[nextCostIndex] - PointBuyCosts[currentScore - AbilityMinBase];
                    if (GetAbilityPointsRemaining() < costDelta) break;
                    _abilityBaseScores[ability]++;
                }
            }
        }
        else
        {
            // No primary ability: balanced spread (all 12s = 24 points, then raise STR/CON)
            foreach (var ability in AbilityNames)
                _abilityBaseScores[ability] = 12;

            var boostOrder = new[] { "STR", "CON", "DEX", "WIS", "INT", "CHA" };
            foreach (var ability in boostOrder)
            {
                while (GetAbilityPointsRemaining() > 0 && _abilityBaseScores[ability] < AbilityMaxBase)
                {
                    int currentScore = _abilityBaseScores[ability];
                    int nextCostIndex = currentScore + 1 - AbilityMinBase;
                    if (nextCostIndex >= PointBuyCosts.Length) break;
                    int costDelta = PointBuyCosts[nextCostIndex] - PointBuyCosts[currentScore - AbilityMinBase];
                    if (GetAbilityPointsRemaining() < costDelta) break;
                    _abilityBaseScores[ability]++;
                }
            }
        }

        UpdateAbilityDisplay();
    }

    private void UpdatePrestigeAbilityBanner()
    {
        if (_prestigeClassComboBox.SelectedItem is ClassDisplayItem selected)
        {
            var prereqs = _displayService.Classes.GetPrestigePrerequisites(selected.Id);
            var abilityPrereqs = new List<string>();

            // Check for skill prerequisites that imply minimum ability scores
            // Not directly tracked in prestige prereqs, but advisory
            if (prereqs.Count > 0)
            {
                _prestigeAbilityBannerLabel.Text = $"Prestige goal: {selected.Name} — Review prerequisites in the Class step to plan ability scores.";
                _prestigeAbilityBanner.IsVisible = true;
                return;
            }
        }

        _prestigeAbilityBanner.IsVisible = false;
    }

    #endregion

    #region Step 6: Skills

    private void PrepareStep6()
    {
        // Recalculate skill points (INT may have changed in Step 5)
        int intScore = _abilityBaseScores["INT"] + _displayService.GetRacialModifier(_selectedRaceId, "INT");
        int intMod = CreatureDisplayService.CalculateAbilityBonus(intScore);
        int basePoints = _displayService.GetClassSkillPointBase(_selectedClassId >= 0 ? _selectedClassId : 0);
        _skillPointsTotal = Math.Max(1, basePoints + intMod) * 4; // Level 1 gets 4x

        // Human bonus: +4 skill points at level 1
        if (_selectedRaceId == 6) // Human
            _skillPointsTotal += 4;

        // Get class skills and unavailable skills
        _classSkillIds = _displayService.Skills.GetClassSkillIds(_selectedClassId >= 0 ? _selectedClassId : 0);

        // Build a temporary creature to check skill availability
        var tempCreature = new UtcFile
        {
            ClassList = new List<CreatureClass>
            {
                new CreatureClass { Class = _selectedClassId >= 0 ? _selectedClassId : 0, ClassLevel = 1 }
            }
        };
        _unavailableSkillIds = _displayService.Skills.GetUnavailableSkillIds(tempCreature, 28);

        if (!_step6Loaded)
        {
            _step6Loaded = true;
            _skillRanksAllocated.Clear();
        }

        BuildSkillList();
        RenderSkillRows();
    }

    private void BuildSkillList()
    {
        _allSkills = new List<SkillDisplayItem>();

        for (int i = 0; i < 28; i++)
        {
            bool isUnavailable = _unavailableSkillIds.Contains(i);
            bool isClassSkill = _classSkillIds.Contains(i);
            int maxRanks = isClassSkill ? 4 : 2; // Level 1: class skill max = level + 3 = 4, cross-class = (level + 3) / 2 = 2

            _allSkills.Add(new SkillDisplayItem
            {
                SkillId = i,
                Name = _displayService.Skills.GetSkillName(i),
                KeyAbility = _displayService.Skills.GetSkillKeyAbility(i),
                IsClassSkill = isClassSkill,
                IsUnavailable = isUnavailable,
                MaxRanks = maxRanks,
                AllocatedRanks = _skillRanksAllocated.GetValueOrDefault(i, 0),
                Cost = isClassSkill ? 1 : 2
            });
        }

        // Sort: class skills first, then alphabetical
        _allSkills = _allSkills
            .OrderByDescending(s => s.IsClassSkill)
            .ThenBy(s => s.Name)
            .ToList();

        ApplySkillFilter();
    }

    private void ApplySkillFilter()
    {
        var filter = _skillSearchBox?.Text?.Trim() ?? "";

        if (string.IsNullOrEmpty(filter))
        {
            _filteredSkills = new List<SkillDisplayItem>(_allSkills);
        }
        else
        {
            _filteredSkills = _allSkills
                .Where(s => s.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
    }

    private void RenderSkillRows()
    {
        _skillRowsPanel.Children.Clear();

        foreach (var skill in _filteredSkills)
        {
            var row = new Grid
            {
                ColumnDefinitions = ColumnDefinitions.Parse("180,50,35,35,60,60,*"),
                Margin = new Avalonia.Thickness(12, 3, 12, 3),
                Opacity = skill.IsUnavailable ? 0.4 : 1.0
            };

            // Skill name — class skills in green, cross-class uses theme default
            var nameLabel = new TextBlock
            {
                Text = skill.Name,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };
            if (skill.IsClassSkill)
                nameLabel.Foreground = BrushManager.GetSuccessBrush(this);
            Grid.SetColumn(nameLabel, 0);
            row.Children.Add(nameLabel);

            // Key ability
            var keyLabel = new TextBlock
            {
                Text = skill.KeyAbility,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Foreground = this.FindResource("SystemControlForegroundBaseMediumLowBrush") as IBrush,
                FontSize = 11
            };
            Grid.SetColumn(keyLabel, 1);
            row.Children.Add(keyLabel);

            // [-] button
            var decreaseBtn = new Button
            {
                Content = "−",
                Width = 28,
                HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                Tag = skill.SkillId,
                IsEnabled = !skill.IsUnavailable && skill.AllocatedRanks > 0
            };
            decreaseBtn.Click += OnSkillDecrease;
            Grid.SetColumn(decreaseBtn, 2);
            row.Children.Add(decreaseBtn);

            // [+] button
            var increaseBtn = new Button
            {
                Content = "+",
                Width = 28,
                HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                Tag = skill.SkillId,
                IsEnabled = !skill.IsUnavailable && skill.AllocatedRanks < skill.MaxRanks && GetSkillPointsRemaining() >= skill.Cost
            };
            increaseBtn.Click += OnSkillIncrease;
            Grid.SetColumn(increaseBtn, 3);
            row.Children.Add(increaseBtn);

            // Allocated ranks
            var ranksLabel = new TextBlock
            {
                Text = skill.AllocatedRanks > 0 ? skill.AllocatedRanks.ToString() : "—",
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                FontWeight = skill.AllocatedRanks > 0 ? FontWeight.Bold : FontWeight.Normal
            };
            if (skill.AllocatedRanks > 0)
                ranksLabel.Foreground = BrushManager.GetSuccessBrush(this);
            Grid.SetColumn(ranksLabel, 4);
            row.Children.Add(ranksLabel);

            // Max ranks
            var maxLabel = new TextBlock
            {
                Text = skill.IsUnavailable ? "—" : skill.MaxRanks.ToString(),
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Foreground = this.FindResource("SystemControlForegroundBaseMediumLowBrush") as IBrush
            };
            Grid.SetColumn(maxLabel, 5);
            row.Children.Add(maxLabel);

            // Type indicator
            var typeLabel = new TextBlock
            {
                Text = skill.IsUnavailable ? "Unavailable" : skill.IsClassSkill ? "Class (1 pt)" : "Cross-class (2 pts)",
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                FontSize = 11,
                Foreground = this.FindResource("SystemControlForegroundBaseMediumLowBrush") as IBrush
            };
            Grid.SetColumn(typeLabel, 6);
            row.Children.Add(typeLabel);

            _skillRowsPanel.Children.Add(row);
        }

        UpdateSkillPointsDisplay();
    }

    private void UpdateSkillPointsDisplay()
    {
        int remaining = GetSkillPointsRemaining();
        _skillPointsRemainingLabel.Text = remaining.ToString();

        if (remaining > 0)
            _skillPointsRemainingLabel.Foreground = BrushManager.GetSuccessBrush(this);
        else if (remaining == 0)
            _skillPointsRemainingLabel.ClearValue(TextBlock.ForegroundProperty);
        else
            _skillPointsRemainingLabel.Foreground = BrushManager.GetErrorBrush(this);

        ValidateCurrentStep();
    }

    private int GetSkillPointsRemaining()
    {
        int spent = 0;
        foreach (var (skillId, ranks) in _skillRanksAllocated)
        {
            bool isClassSkill = _classSkillIds.Contains(skillId);
            int cost = isClassSkill ? 1 : 2;
            spent += ranks * cost;
        }
        return _skillPointsTotal - spent;
    }

    private void OnSkillIncrease(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is int skillId)
        {
            bool isClassSkill = _classSkillIds.Contains(skillId);
            int cost = isClassSkill ? 1 : 2;
            int maxRanks = isClassSkill ? 4 : 2;

            if (GetSkillPointsRemaining() >= cost)
            {
                int current = _skillRanksAllocated.GetValueOrDefault(skillId, 0);
                if (current < maxRanks)
                {
                    _skillRanksAllocated[skillId] = current + 1;
                    UpdateSkillItem(skillId);
                    RenderSkillRows();
                }
            }
        }
    }

    private void OnSkillDecrease(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is int skillId)
        {
            int current = _skillRanksAllocated.GetValueOrDefault(skillId, 0);
            if (current > 0)
            {
                _skillRanksAllocated[skillId] = current - 1;
                if (_skillRanksAllocated[skillId] == 0)
                    _skillRanksAllocated.Remove(skillId);
                UpdateSkillItem(skillId);
                RenderSkillRows();
            }
        }
    }

    private void UpdateSkillItem(int skillId)
    {
        var skill = _allSkills.FirstOrDefault(s => s.SkillId == skillId);
        if (skill != null)
        {
            skill.AllocatedRanks = _skillRanksAllocated.GetValueOrDefault(skillId, 0);
        }
    }

    private void OnSkillSearchChanged(object? sender, TextChangedEventArgs e)
    {
        ApplySkillFilter();
        RenderSkillRows();
    }

    private void OnSkillAutoAssignClick(object? sender, RoutedEventArgs e)
    {
        // Clear all allocations
        _skillRanksAllocated.Clear();

        // Try to read package skill preferences from SkillPref2DA in packages.2da
        var preferredSkillIds = new List<int>();
        if (_selectedPackageId != 255)
        {
            var skillPref2da = _gameDataService.Get2DAValue("packages", _selectedPackageId, "SkillPref2DA");
            if (!string.IsNullOrEmpty(skillPref2da) && skillPref2da != "****")
            {
                // Read skill indices from the package skill preference table
                for (int row = 0; row < 50; row++)
                {
                    var skillIndexStr = _gameDataService.Get2DAValue(skillPref2da, row, "SkillIndex");
                    if (string.IsNullOrEmpty(skillIndexStr) || skillIndexStr == "****")
                        break;
                    if (int.TryParse(skillIndexStr, out int skillIndex))
                        preferredSkillIds.Add(skillIndex);
                }
            }
        }

        // If no package preferences, use class skills sorted alphabetically
        if (preferredSkillIds.Count == 0)
        {
            preferredSkillIds = _classSkillIds.OrderBy(id => _displayService.Skills.GetSkillName(id)).ToList();
        }

        // Distribute points to preferred skills, prioritizing class skills
        // First pass: class skills from preferences
        foreach (var skillId in preferredSkillIds.Where(id => _classSkillIds.Contains(id) && !_unavailableSkillIds.Contains(id)))
        {
            int maxRanks = 4; // Class skill max at level 1
            while (_skillRanksAllocated.GetValueOrDefault(skillId, 0) < maxRanks && GetSkillPointsRemaining() >= 1)
            {
                _skillRanksAllocated[skillId] = _skillRanksAllocated.GetValueOrDefault(skillId, 0) + 1;
            }
        }

        // Second pass: cross-class skills from preferences (if points remain)
        foreach (var skillId in preferredSkillIds.Where(id => !_classSkillIds.Contains(id) && !_unavailableSkillIds.Contains(id)))
        {
            int maxRanks = 2; // Cross-class max at level 1
            while (_skillRanksAllocated.GetValueOrDefault(skillId, 0) < maxRanks && GetSkillPointsRemaining() >= 2)
            {
                _skillRanksAllocated[skillId] = _skillRanksAllocated.GetValueOrDefault(skillId, 0) + 1;
            }
        }

        // Update display items
        foreach (var skill in _allSkills)
        {
            skill.AllocatedRanks = _skillRanksAllocated.GetValueOrDefault(skill.SkillId, 0);
        }

        RenderSkillRows();
    }

    #endregion

    #region Step 7: Spells

    private void PrepareStep7()
    {
        int classId = _selectedClassId >= 0 ? _selectedClassId : 0;
        bool isCaster = _displayService.IsCasterClass(classId);

        if (!isCaster)
        {
            _needsSpellSelection = false;
            _isDivineCaster = false;
            return;
        }

        bool isSpontaneous = _displayService.Spells.IsSpontaneousCaster(classId);
        _maxSpellLevelForClass = _displayService.Spells.GetMaxSpellLevel(classId, 1);
        _isDivineCaster = !isSpontaneous && (classId == 2 || classId == 3); // Cleric=2, Druid=3

        if (_maxSpellLevelForClass < 0)
        {
            _needsSpellSelection = false;
            return;
        }

        _needsSpellSelection = true;

        if (_isDivineCaster)
        {
            // Divine casters (Cleric, Druid) get spells automatically
            var className = _displayService.GetClassName(classId);
            _divineSpellInfoLabel.Text = $"As a {className}, your deity grants you access to all {className.ToLowerInvariant()} spells.\n" +
                $"You can prepare spells each day after resting.";
            _divineSpellInfoPanel.IsVisible = true;

            // Hide the two-panel selection UI
            _spellLevelTabsPanel.IsVisible = false;
            _spellSelectionCountLabel.IsVisible = false;
            _spellSearchBox2.IsVisible = false;
            _availableSpellsListBox.IsVisible = false;
            _selectedSpellsListBox.IsVisible = false;
            _selectedSpellCountLabel.IsVisible = false;
            // Hide the parent grids of the available/selected panels
            var twoPanel = _availableSpellsListBox.Parent?.Parent as Grid;
            if (twoPanel != null) twoPanel.IsVisible = false;

            _spellStepDescription.Text = $"{className}s receive all their class spells automatically through divine power.";
            return;
        }

        // Spontaneous casters (Bard, Sorcerer) or Wizard: show spell selection UI
        _divineSpellInfoPanel.IsVisible = false;
        var selectionParent = _availableSpellsListBox.Parent?.Parent as Grid;
        if (selectionParent != null) selectionParent.IsVisible = true;
        _spellLevelTabsPanel.IsVisible = true;
        _spellSelectionCountLabel.IsVisible = true;
        _spellSearchBox2.IsVisible = true;
        _availableSpellsListBox.IsVisible = true;
        _selectedSpellsListBox.IsVisible = true;
        _selectedSpellCountLabel.IsVisible = true;

        string classNameForDesc = _displayService.GetClassName(classId);
        if (isSpontaneous)
            _spellStepDescription.Text = $"Choose spells known for your {classNameForDesc}. These are the spells you can cast.";
        else
            _spellStepDescription.Text = $"Choose spells for your {classNameForDesc}'s spellbook.";

        // Initialize spell level selections if not already done
        if (!_step7Loaded)
        {
            _step7Loaded = true;
            _selectedSpellsByLevel.Clear();
        }

        // Build spell level tabs
        BuildSpellLevelTabs();

        // Default to level 0 (cantrips)
        _currentSpellLevel = 0;
        SelectSpellLevelTab(0);
    }

    private void BuildSpellLevelTabs()
    {
        _spellLevelTabsPanel.Children.Clear();

        for (int level = 0; level <= _maxSpellLevelForClass; level++)
        {
            var btn = new ToggleButton
            {
                Content = level == 0 ? "Cantrips" : $"Level {level}",
                Tag = level,
                Margin = new Avalonia.Thickness(0, 0, 2, 0),
                IsChecked = level == 0
            };
            btn.Click += OnSpellLevelTabClick;
            _spellLevelTabsPanel.Children.Add(btn);
        }
    }

    private void OnSpellLevelTabClick(object? sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton btn && btn.Tag is int level)
        {
            SelectSpellLevelTab(level);
        }
    }

    private void SelectSpellLevelTab(int level)
    {
        _currentSpellLevel = level;

        // Update tab checked states
        foreach (var child in _spellLevelTabsPanel.Children)
        {
            if (child is ToggleButton tb && tb.Tag is int tabLevel)
                tb.IsChecked = tabLevel == level;
        }

        // Load available spells for this level
        LoadAvailableSpellsForLevel(level);
        UpdateSelectedSpellsDisplay();
        UpdateSpellSelectionCount();
        ValidateCurrentStep();
    }

    private void LoadAvailableSpellsForLevel(int spellLevel)
    {
        int classId = _selectedClassId >= 0 ? _selectedClassId : 0;
        var allSpellIds = _displayService.Spells.GetAllSpellIds();
        var selectedForLevel = _selectedSpellsByLevel.GetValueOrDefault(spellLevel, new List<int>());

        _availableSpellsForLevel = new List<SpellDisplayItem>();

        foreach (var spellId in allSpellIds)
        {
            var info = _displayService.Spells.GetSpellInfo(spellId);
            if (info == null) continue;

            int levelForClass = info.GetLevelForClass(classId);
            if (levelForClass != spellLevel) continue;

            // Skip if already selected
            if (selectedForLevel.Contains(spellId)) continue;

            _availableSpellsForLevel.Add(new SpellDisplayItem
            {
                SpellId = spellId,
                Name = info.Name,
                SchoolAbbrev = GetSchoolAbbrev(info.School)
            });
        }

        _availableSpellsForLevel = _availableSpellsForLevel.OrderBy(s => s.Name).ToList();
        ApplySpellFilter();
    }

    private void ApplySpellFilter()
    {
        var filter = _spellSearchBox2?.Text?.Trim() ?? "";

        if (string.IsNullOrEmpty(filter))
        {
            _filteredAvailableSpells = new List<SpellDisplayItem>(_availableSpellsForLevel);
        }
        else
        {
            _filteredAvailableSpells = _availableSpellsForLevel
                .Where(s => s.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        _availableSpellsListBox.ItemsSource = _filteredAvailableSpells;
    }

    private void OnSpellSearchChanged(object? sender, TextChangedEventArgs e)
    {
        ApplySpellFilter();
    }

    private void UpdateSelectedSpellsDisplay()
    {
        var selectedForLevel = _selectedSpellsByLevel.GetValueOrDefault(_currentSpellLevel, new List<int>());
        var items = selectedForLevel.Select(id =>
        {
            var info = _displayService.Spells.GetSpellInfo(id);
            return new SpellDisplayItem
            {
                SpellId = id,
                Name = info?.Name ?? $"Spell {id}",
                SchoolAbbrev = info != null ? GetSchoolAbbrev(info.School) : ""
            };
        }).OrderBy(s => s.Name).ToList();

        _selectedSpellsListBox.ItemsSource = items;

        // Update count for this level
        int classId = _selectedClassId >= 0 ? _selectedClassId : 0;
        int maxForLevel = GetMaxSpellsForLevel(classId, _currentSpellLevel);
        _selectedSpellCountLabel.Text = $"({selectedForLevel.Count} / {maxForLevel})";

        if (selectedForLevel.Count > maxForLevel)
            _selectedSpellCountLabel.Foreground = BrushManager.GetErrorBrush(this);
        else if (selectedForLevel.Count == maxForLevel)
            _selectedSpellCountLabel.ClearValue(TextBlock.ForegroundProperty);
        else
            _selectedSpellCountLabel.Foreground = BrushManager.GetSuccessBrush(this);
    }

    private void UpdateSpellSelectionCount()
    {
        int classId = _selectedClassId >= 0 ? _selectedClassId : 0;
        int totalSelected = 0;
        int totalRequired = 0;

        for (int level = 0; level <= _maxSpellLevelForClass; level++)
        {
            int selected = _selectedSpellsByLevel.GetValueOrDefault(level, new List<int>()).Count;
            int max = GetMaxSpellsForLevel(classId, level);
            totalSelected += selected;
            totalRequired += max;
        }

        _spellSelectionCountLabel.Text = $"Total: {totalSelected} / {totalRequired}";
    }

    private int GetMaxSpellsForLevel(int classId, int spellLevel)
    {
        bool isSpontaneous = _displayService.Spells.IsSpontaneousCaster(classId);

        if (isSpontaneous)
        {
            // Spontaneous casters: use SpellsKnownLimit
            var knownLimits = _displayService.Spells.GetSpellsKnownLimit(classId, 1);
            if (knownLimits != null && spellLevel < knownLimits.Length)
                return knownLimits[spellLevel];
            return 0;
        }
        else
        {
            // Wizard: use spell slots as guide for initial spellbook
            // At level 1, wizards get 3 + INT mod cantrips and all level 0 plus (3 + INT mod) level 1 spells
            // Simplified: use spell slots
            var slots = _displayService.Spells.GetSpellSlots(classId, 1);
            if (slots != null && spellLevel < slots.Length)
                return Math.Max(slots[spellLevel], 0);
            return 0;
        }
    }

    private void OnAddSpellClick(object? sender, RoutedEventArgs e)
    {
        var selected = _availableSpellsListBox.SelectedItems?.Cast<SpellDisplayItem>().ToList();
        if (selected == null || selected.Count == 0) return;

        int classId = _selectedClassId >= 0 ? _selectedClassId : 0;
        int maxForLevel = GetMaxSpellsForLevel(classId, _currentSpellLevel);

        if (!_selectedSpellsByLevel.ContainsKey(_currentSpellLevel))
            _selectedSpellsByLevel[_currentSpellLevel] = new List<int>();

        foreach (var spell in selected)
        {
            if (_selectedSpellsByLevel[_currentSpellLevel].Count >= maxForLevel)
                break;
            if (!_selectedSpellsByLevel[_currentSpellLevel].Contains(spell.SpellId))
                _selectedSpellsByLevel[_currentSpellLevel].Add(spell.SpellId);
        }

        LoadAvailableSpellsForLevel(_currentSpellLevel);
        UpdateSelectedSpellsDisplay();
        UpdateSpellSelectionCount();
        ValidateCurrentStep();
    }

    private void OnRemoveSpellClick(object? sender, RoutedEventArgs e)
    {
        var selected = _selectedSpellsListBox.SelectedItems?.Cast<SpellDisplayItem>().ToList();
        if (selected == null || selected.Count == 0) return;

        if (!_selectedSpellsByLevel.ContainsKey(_currentSpellLevel)) return;

        foreach (var spell in selected)
        {
            _selectedSpellsByLevel[_currentSpellLevel].Remove(spell.SpellId);
        }

        if (_selectedSpellsByLevel[_currentSpellLevel].Count == 0)
            _selectedSpellsByLevel.Remove(_currentSpellLevel);

        LoadAvailableSpellsForLevel(_currentSpellLevel);
        UpdateSelectedSpellsDisplay();
        UpdateSpellSelectionCount();
        ValidateCurrentStep();
    }

    private void OnSpellAutoAssignClick(object? sender, RoutedEventArgs e)
    {
        int classId = _selectedClassId >= 0 ? _selectedClassId : 0;

        // Clear current selections
        _selectedSpellsByLevel.Clear();

        // Try to read package spell preferences
        var preferredSpellIds = new List<int>();
        if (_selectedPackageId != 255)
        {
            var spellPref2da = _gameDataService.Get2DAValue("packages", _selectedPackageId, "SpellPref2DA");
            if (!string.IsNullOrEmpty(spellPref2da) && spellPref2da != "****")
            {
                for (int row = 0; row < 100; row++)
                {
                    var spellIdStr = _gameDataService.Get2DAValue(spellPref2da, row, "SpellIndex");
                    if (string.IsNullOrEmpty(spellIdStr) || spellIdStr == "****")
                        break;
                    if (int.TryParse(spellIdStr, out int spellId))
                        preferredSpellIds.Add(spellId);
                }
            }
        }

        // Get all available spells organized by level
        var allSpellIds = _displayService.Spells.GetAllSpellIds();
        var spellsByLevel = new Dictionary<int, List<SpellAutoAssignItem>>();

        foreach (var spellId in allSpellIds)
        {
            var info = _displayService.Spells.GetSpellInfo(spellId);
            if (info == null) continue;

            int levelForClass = info.GetLevelForClass(classId);
            if (levelForClass < 0 || levelForClass > _maxSpellLevelForClass) continue;

            if (!spellsByLevel.ContainsKey(levelForClass))
                spellsByLevel[levelForClass] = new List<SpellAutoAssignItem>();
            spellsByLevel[levelForClass].Add(new SpellAutoAssignItem { Id = spellId, Name = info.Name });
        }

        // Fill each level
        for (int level = 0; level <= _maxSpellLevelForClass; level++)
        {
            int maxForLevel = GetMaxSpellsForLevel(classId, level);
            if (maxForLevel <= 0) continue;

            _selectedSpellsByLevel[level] = new List<int>();
            var availableForLevel = spellsByLevel.GetValueOrDefault(level, new List<SpellAutoAssignItem>());

            // Prefer package spells first
            foreach (var prefId in preferredSpellIds)
            {
                if (_selectedSpellsByLevel[level].Count >= maxForLevel) break;
                if (availableForLevel.Any(s => s.Id == prefId) && !_selectedSpellsByLevel[level].Contains(prefId))
                    _selectedSpellsByLevel[level].Add(prefId);
            }

            // Fill remaining with alphabetical order
            foreach (var spell in availableForLevel.OrderBy(s => s.Name))
            {
                if (_selectedSpellsByLevel[level].Count >= maxForLevel) break;
                if (!_selectedSpellsByLevel[level].Contains(spell.Id))
                    _selectedSpellsByLevel[level].Add(spell.Id);
            }
        }

        // Refresh display
        LoadAvailableSpellsForLevel(_currentSpellLevel);
        UpdateSelectedSpellsDisplay();
        UpdateSpellSelectionCount();
        ValidateCurrentStep();
    }

    private bool IsSpellSelectionComplete()
    {
        int classId = _selectedClassId >= 0 ? _selectedClassId : 0;

        for (int level = 0; level <= _maxSpellLevelForClass; level++)
        {
            int maxForLevel = GetMaxSpellsForLevel(classId, level);
            if (maxForLevel <= 0) continue;

            int selected = _selectedSpellsByLevel.GetValueOrDefault(level, new List<int>()).Count;
            if (selected < maxForLevel)
                return false;
        }

        return true;
    }

    private static string GetSchoolAbbrev(SpellSchool school) => school switch
    {
        SpellSchool.Abjuration => "Abj",
        SpellSchool.Conjuration => "Con",
        SpellSchool.Divination => "Div",
        SpellSchool.Enchantment => "Enc",
        SpellSchool.Evocation => "Evo",
        SpellSchool.Illusion => "Ill",
        SpellSchool.Necromancy => "Nec",
        SpellSchool.Transmutation => "Tra",
        _ => ""
    };

    #endregion

    #region Step 8: Summary

    private void PrepareStep8()
    {
        // Populate summary fields
        _summaryFileTypeLabel.Text = _isBicFile ? "Player Character (BIC)" : "Creature Blueprint (UTC)";

        var raceName = _displayService.GetRaceName(_selectedRaceId);
        var genderName = _selectedGender == 0 ? "Male" : "Female";
        _summaryRaceLabel.Text = $"{genderName} {raceName}";

        // Appearance
        var appName = _selectedAppearanceId > 0
            ? _displayService.GetAppearanceName(_selectedAppearanceId)
            : _displayService.GetAppearanceName(GetDefaultAppearanceForRace(_selectedRaceId));
        _summaryAppearanceLabel.Text = $"{appName}, Portrait {_selectedPortraitId}";

        // Class
        if (_selectedClassId >= 0)
        {
            var className = _displayService.GetClassName(_selectedClassId);
            _summaryClassLabel.Text = $"{className} (Level 1)";
        }

        // Abilities
        var racialMods = _displayService.GetRacialModifiers(_selectedRaceId);
        var abilityParts = new List<string>();
        foreach (var ability in AbilityNames)
        {
            int baseScore = _abilityBaseScores[ability];
            int racialMod = GetRacialModForAbility(racialMods, ability);
            int total = baseScore + racialMod;
            abilityParts.Add($"{ability} {total}");
        }
        _summaryAbilitiesLabel.Text = string.Join("  |  ", abilityParts);

        // Skills
        int skillCount = _skillRanksAllocated.Count(kvp => kvp.Value > 0);
        int skillPointsSpent = _skillPointsTotal - GetSkillPointsRemaining();
        if (skillCount > 0)
        {
            var topSkills = _skillRanksAllocated
                .Where(kvp => kvp.Value > 0)
                .OrderByDescending(kvp => kvp.Value)
                .Take(5)
                .Select(kvp => $"{_displayService.Skills.GetSkillName(kvp.Key)} ({kvp.Value})")
                .ToList();
            _summarySkillsLabel.Text = $"{skillPointsSpent} points in {skillCount} skills: {string.Join(", ", topSkills)}" +
                (skillCount > 5 ? $" +{skillCount - 5} more" : "");
        }
        else
        {
            _summarySkillsLabel.Text = "No skills allocated";
        }

        // Spells
        if (_needsSpellSelection)
        {
            _summarySpellsSection.IsVisible = true;
            if (_isDivineCaster)
            {
                _summarySpellsLabel.Text = "All spells granted by deity (divine caster)";
            }
            else
            {
                int totalSpells = _selectedSpellsByLevel.Values.Sum(list => list.Count);
                _summarySpellsLabel.Text = $"{totalSpells} spells selected";
            }
        }
        else
        {
            _summarySpellsSection.IsVisible = false;
        }

        // Feats (auto-granted)
        var grantedFeats = GetGrantedFeatIds();
        if (grantedFeats.Count > 0)
        {
            var featNames = grantedFeats
                .Select(id => _displayService.GetFeatName(id))
                .Where(n => !string.IsNullOrEmpty(n))
                .OrderBy(n => n)
                .ToList();
            _summaryFeatsLabel.Text = featNames.Count > 0
                ? string.Join(", ", featNames)
                : "None";
        }
        else
        {
            _summaryFeatsLabel.Text = "None";
        }

        // Palette ID visibility (UTC only)
        bool showPalette = !_isBicFile;
        _paletteIdLabelText.IsVisible = showPalette;
        _paletteIdNumericUpDown.IsVisible = showPalette;
        _paletteIdNote.IsVisible = showPalette;
    }

    private void OnCharacterNameChanged(object? sender, TextChangedEventArgs e)
    {
        _characterName = _characterNameTextBox.Text?.Trim() ?? "";

        // Generate tag and resref from name
        var sanitized = SanitizeForResRef(_characterName);
        _generatedTagLabel.Text = string.IsNullOrEmpty(sanitized) ? "new_creature" : sanitized;
        _generatedResRefLabel.Text = string.IsNullOrEmpty(sanitized) ? "new_creature" : sanitized;
    }

    private void OnSummaryEditClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string stepStr && int.TryParse(stepStr, out int targetStep))
        {
            _currentStep = targetStep;
            UpdateStepDisplay();
        }
    }

    private static string SanitizeForResRef(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "";

        // Lowercase, replace spaces with underscores, remove non-alphanumeric/underscore
        var sanitized = name.ToLowerInvariant()
            .Replace(' ', '_');

        var chars = sanitized.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray();
        var result = new string(chars);

        // Enforce Aurora Engine 16-char limit
        if (result.Length > 16)
            result = result[..16];

        // Remove trailing underscores
        result = result.TrimEnd('_');

        return result;
    }

    private HashSet<int> GetGrantedFeatIds()
    {
        int classId = _selectedClassId >= 0 ? _selectedClassId : 0;

        var racialFeats = _displayService.Feats.GetRaceGrantedFeatIds(_selectedRaceId);
        var classFeats = _displayService.Feats.GetClassGrantedFeatIds(classId);

        var combined = new HashSet<int>(racialFeats);
        combined.UnionWith(classFeats);
        return combined;
    }

    #endregion

    #region Build Creature

    /// <summary>
    /// Builds a UtcFile from all wizard selections.
    /// Populates all fields from Steps 1-8 using 2DA data.
    /// </summary>
    private UtcFile BuildCreature()
    {
        int classId = _selectedClassId >= 0 ? _selectedClassId : 255;
        var hitDie = _selectedClassId >= 0
            ? _displayService.Classes.GetClassMetadata(_selectedClassId).HitDie
            : 4;

        int conTotal = _abilityBaseScores["CON"] + _displayService.GetRacialModifier(_selectedRaceId, "CON");
        int hp = Math.Max(1, hitDie + CreatureDisplayService.CalculateAbilityBonus(conTotal));

        // Saving throws from class progression at level 1
        var saves = _selectedClassId >= 0
            ? _displayService.GetClassSaves(_selectedClassId, 1)
            : new SavingThrows();

        // Tag/ResRef from user input
        var sanitized = SanitizeForResRef(_characterName);
        var tag = string.IsNullOrEmpty(sanitized) ? "new_creature" : sanitized;
        var resRef = tag;

        // Palette ID from step 8
        _paletteId = (byte)(_paletteIdNumericUpDown.Value ?? 1);

        // Build class entry with spell data
        var creatureClass = new CreatureClass
        {
            Class = classId,
            ClassLevel = 1
        };

        // Populate spells on the class
        PopulateClassSpells(creatureClass, classId);

        // FirstName
        var firstName = new CExoLocString { StrRef = 0xFFFFFFFF };
        if (!string.IsNullOrEmpty(_characterName))
        {
            firstName.LocalizedStrings[0] = _characterName; // English (language 0)
        }

        return new UtcFile
        {
            // Identity (Step 8)
            FirstName = firstName,
            LastName = new CExoLocString { StrRef = 0xFFFFFFFF },
            Tag = tag,
            TemplateResRef = resRef,
            Description = new CExoLocString { StrRef = 0xFFFFFFFF },
            PaletteID = _isBicFile ? (byte)0 : _paletteId,

            // Race & gender (Step 2)
            Race = _selectedRaceId,
            Gender = _selectedGender,

            // Appearance (Step 3)
            AppearanceType = _selectedAppearanceId > 0 ? _selectedAppearanceId : GetDefaultAppearanceForRace(_selectedRaceId),
            Phenotype = _selectedPhenotype,
            PortraitId = _selectedPortraitId,

            // Body parts (Step 3)
            AppearanceHead = _headVariation,
            BodyPart_Neck = _neckVariation,
            BodyPart_Torso = _torsoVariation,
            BodyPart_Pelvis = _pelvisVariation,
            BodyPart_Belt = _beltVariation,
            BodyPart_LShoul = _lShoulVariation,
            BodyPart_RShoul = _rShoulVariation,
            BodyPart_LBicep = _lBicepVariation,
            BodyPart_RBicep = _rBicepVariation,
            BodyPart_LFArm = _lFArmVariation,
            BodyPart_RFArm = _rFArmVariation,
            BodyPart_LHand = _lHandVariation,
            BodyPart_RHand = _rHandVariation,
            BodyPart_LThigh = _lThighVariation,
            BodyPart_RThigh = _rThighVariation,
            BodyPart_LShin = _lShinVariation,
            BodyPart_RShin = _rShinVariation,
            BodyPart_LFoot = _lFootVariation,
            BodyPart_RFoot = _rFootVariation,

            // Colors (Step 3)
            Color_Skin = _skinColor,
            Color_Hair = _hairColor,
            Color_Tattoo1 = _tattoo1Color,
            Color_Tattoo2 = _tattoo2Color,

            // Ability scores (Step 5) — base scores only, game applies racial mods
            Str = (byte)_abilityBaseScores["STR"],
            Dex = (byte)_abilityBaseScores["DEX"],
            Con = (byte)_abilityBaseScores["CON"],
            Int = (byte)_abilityBaseScores["INT"],
            Wis = (byte)_abilityBaseScores["WIS"],
            Cha = (byte)_abilityBaseScores["CHA"],

            // HP (hit die + CON mod at level 1)
            HitPoints = (short)hp,
            CurrentHitPoints = (short)hp,
            MaxHitPoints = (short)hp,

            // Saving throws from class (level 1)
            FortBonus = (short)saves.Fortitude,
            RefBonus = (short)saves.Reflex,
            WillBonus = (short)saves.Will,

            // Alignment — True Neutral
            GoodEvil = 50,
            LawfulChaotic = 50,

            // Behavior defaults
            FactionID = 1,
            PerceptionRange = 11,
            WalkRate = 4,
            DecayTime = 5000,
            Interruptable = true,

            // Starting package (Step 4)
            StartingPackage = _selectedPackageId != 255 ? _selectedPackageId : (byte)0,

            // Class (Step 4) with spells (Step 7)
            ClassList = new List<CreatureClass> { creatureClass },

            // Feats: granted by race + class
            FeatList = GetGrantedFeatIds().Select(id => (ushort)id).ToList(),

            // Skills (Step 6)
            SkillList = BuildSkillList_ForCreature(),

            // Empty lists
            SpecAbilityList = new List<SpecialAbility>(),
            ItemList = new List<InventoryItem>(),
            EquipItemList = new List<EquippedItem>()
        };
    }

    private void PopulateClassSpells(CreatureClass creatureClass, int classId)
    {
        if (!_needsSpellSelection || _isDivineCaster)
            return;

        foreach (var (spellLevel, spellIds) in _selectedSpellsByLevel)
        {
            if (spellLevel < 0 || spellLevel >= 10) continue;

            foreach (var spellId in spellIds)
            {
                creatureClass.KnownSpells[spellLevel].Add(new KnownSpell
                {
                    Spell = (ushort)spellId,
                    SpellFlags = 0x01, // Readied
                    SpellMetaMagic = 0
                });
            }
        }
    }

    /// <summary>
    /// Builds the skill list for the creature (28 skills, ordered by skill ID).
    /// Each byte is the number of ranks allocated to that skill.
    /// </summary>
    private List<byte> BuildSkillList_ForCreature()
    {
        var skills = new List<byte>();
        for (int i = 0; i < 28; i++)
        {
            skills.Add((byte)_skillRanksAllocated.GetValueOrDefault(i, 0));
        }
        return skills;
    }

    /// <summary>
    /// Gets a default appearance ID for a race by reading racialtypes.2da Appearance column.
    /// </summary>
    private ushort GetDefaultAppearanceForRace(byte raceId)
    {
        var appStr = _displayService.GameDataService.Get2DAValue("racialtypes", raceId, "Appearance");
        if (!string.IsNullOrEmpty(appStr) && appStr != "****" && ushort.TryParse(appStr, out ushort appId))
            return appId;
        return 6; // Human fallback
    }

    #endregion

    #region Display Items

    private class RaceDisplayItem
    {
        public byte Id { get; init; }
        public string Name { get; init; } = "";
    }

    private class ClassDisplayItem
    {
        public int Id { get; init; }
        public string Name { get; init; } = "";
        public bool IsFavored { get; init; }
    }

    private class PackageDisplayItem
    {
        public byte Id { get; init; }
        public string Name { get; init; } = "";
    }

    private class SkillDisplayItem
    {
        public int SkillId { get; set; }
        public string Name { get; set; } = "";
        public string KeyAbility { get; set; } = "";
        public bool IsClassSkill { get; set; }
        public bool IsUnavailable { get; set; }
        public int MaxRanks { get; set; }
        public int AllocatedRanks { get; set; }
        public int Cost { get; set; } = 1;
    }

    private class SpellDisplayItem
    {
        public int SpellId { get; set; }
        public string Name { get; set; } = "";
        public string SchoolAbbrev { get; set; } = "";
    }

    private class SpellAutoAssignItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    #endregion
}
