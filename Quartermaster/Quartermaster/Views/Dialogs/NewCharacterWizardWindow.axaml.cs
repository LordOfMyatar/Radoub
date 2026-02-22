using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Quartermaster.Services;
using Quartermaster.Views;
using Radoub.Formats.Services;
using Radoub.Formats.Utc;
using Radoub.UI.Services;

namespace Quartermaster.Views.Dialogs;

/// <summary>
/// Multi-step wizard for creating a new creature from scratch.
/// 10 steps: File Type, Race &amp; Sex, Appearance, Class, Abilities, Feats, Skills, Spells, Equipment, Summary.
/// Partial class files: Race, Appearance, ClassSelection, Abilities, Feats, Skills, Spells,
/// EquipmentAndSummary, BuildCreature.
/// </summary>
public partial class NewCharacterWizardWindow : Window
{
    private readonly CreatureDisplayService _displayService;
    private readonly IGameDataService _gameDataService;
    private readonly ItemIconService? _itemIconService;
    private readonly AudioService? _audioService;

    // Wizard state
    private int _currentStep = 1;
    private const int TotalSteps = 10;

    // Step 1: File Type
    private bool _isBicFile; // false = UTC (default), true = BIC

    // Step 2: Race & Sex
    private byte _selectedRaceId; // Set when race is selected in Step 2
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

    // Step 4: Class, Package & Alignment
    private int _selectedClassId = -1;
    private byte _selectedPackageId = 255; // sentinel for none
    private byte _selectedGoodEvil = 50; // 0=Evil, 50=Neutral, 100=Good — default True Neutral
    private byte _selectedLawChaos = 50; // 0=Chaotic, 50=Neutral, 100=Lawful
    private int _favoredClassId = -1;
    private List<ClassDisplayItem> _allClasses = new();
    private List<ClassDisplayItem> _filteredClasses = new();
    private bool _step4Loaded;
    private bool _prestigePlanningExpanded;
    private bool _classNeedsDomains;
    private List<DomainDisplayItem> _domainList = new();

    // Step 5: Ability Scores
    private readonly Dictionary<string, int> _abilityBaseScores = new()
    {
        { "STR", 8 }, { "DEX", 8 }, { "CON", 8 },
        { "INT", 8 }, { "WIS", 8 }, { "CHA", 8 }
    };
    private int _pointBuyTotal = 30; // Default; updated from racialtypes.2da AbilitiesPointBuyNumber
    private const int AbilityMinBase = 8;
    private const int AbilityMaxBase = 18;
    private static readonly int[] PointBuyCosts = { 0, 1, 2, 3, 4, 5, 6, 8, 10, 13, 16 }; // index = score - 8
    private bool _step5Loaded;

    // Step 6: Feats
    private readonly List<int> _chosenFeatIds = new(); // Player-chosen feats (not granted)
    private List<FeatDisplayItem> _availableFeats = new();
    private List<FeatDisplayItem> _filteredAvailableFeats = new();
    private int _featsToChoose; // Number of feats the player gets to pick

    // Step 7: Skills (was Step 6)
    private int _skillPointsTotal;
    private readonly Dictionary<int, int> _skillRanksAllocated = new();
    private HashSet<int> _classSkillIds = new();
    private HashSet<int> _unavailableSkillIds = new();
    private List<SkillDisplayItem> _allSkills = new();
    private List<SkillDisplayItem> _filteredSkills = new();
    private bool _step7Loaded;

    // Step 8: Spells (was Step 7)
    private bool _needsSpellSelection;
    private bool _isDivineCaster;
    private bool _step8Loaded;
    private int _currentSpellLevel; // Currently selected spell level tab
    private int _maxSpellLevelForClass;
    private readonly Dictionary<int, List<int>> _selectedSpellsByLevel = new(); // spell level → list of spell IDs
    private List<SpellDisplayItem> _availableSpellsForLevel = new();
    private List<SpellDisplayItem> _filteredAvailableSpells = new();

    // Step 9: Equipment
    private readonly List<EquipmentDisplayItem> _equipmentItems = new();
    private bool _step9Loaded;

    // Step 10: Summary (was Step 8)
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
    private readonly StackPanel _defaultScriptsPanel;
    private readonly CheckBox _defaultScriptsCheckBox;

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

    // Step 6 controls (Feats)
    private readonly TextBlock _featStepDescription;
    private readonly TextBlock _featSelectionCountLabel;
    private readonly TextBox _featSearchBox;
    private readonly ListBox _availableFeatsListBox;
    private readonly ListBox _selectedFeatsListBox;
    private readonly TextBlock _selectedFeatCountLabel;

    // Step 7 controls (Skills, was Step 6)
    private readonly TextBlock _skillPointsRemainingLabel;
    private readonly StackPanel _skillRowsPanel;
    private readonly TextBox _skillSearchBox;

    // Step 8 controls (Spells, was Step 7)
    private readonly TextBlock _spellStepDescription;
    private readonly StackPanel _spellLevelTabsPanel;
    private readonly TextBlock _spellSelectionCountLabel;
    private readonly TextBox _spellSearchBox2;
    private readonly ListBox _availableSpellsListBox;
    private readonly ListBox _selectedSpellsListBox;
    private readonly TextBlock _selectedSpellCountLabel;
    private readonly Grid _spellSelectionTwoPanel;
    private readonly Border _divineSpellInfoPanel;
    private readonly TextBlock _divineSpellInfoLabel;

    // Step 9 controls (Equipment)
    private readonly TextBlock _equipmentCountLabel;
    private readonly StackPanel _equipmentItemsPanel;
    private readonly TextBlock _equipmentEmptyLabel;

    // Step 10 controls (Summary, was Step 8)
    private readonly TextBox _characterNameTextBox;
    private readonly TextBox _lastNameTextBox;
    private readonly TextBlock _ageLabelText;
    private readonly NumericUpDown _ageNumericUpDown;
    private readonly TextBlock _ageNote;
    private readonly TextBox _descriptionTextBox;
    private readonly TextBlock _voiceSetLabel;
    private ushort _selectedVoiceSetId;
    private readonly TextBlock _generatedTagLabel;
    private readonly TextBlock _generatedResRefLabel;
    private readonly TextBlock _paletteIdLabelText;
    private readonly ComboBox _paletteIdComboBox;
    private readonly TextBlock _paletteIdNote;
    private readonly TextBlock _summaryFileTypeLabel;
    private readonly TextBlock _summaryRaceLabel;
    private readonly TextBlock _summaryAppearanceLabel;
    private readonly TextBlock _summaryClassLabel;
    private readonly TextBlock _summaryAlignmentLabel;
    private readonly TextBlock _summaryAbilitiesLabel;
    private readonly TextBlock _summaryFeatsLabel;
    private readonly TextBlock _summarySkillsLabel;
    private readonly TextBlock _summarySpellsLabel;
    private readonly Grid _summarySpellsSection;
    private readonly Grid _summaryEquipmentSection;
    private readonly TextBlock _summaryEquipmentLabel;
    private readonly Border _summaryScriptsDivider;
    private readonly Grid _summaryScriptsSection;
    private readonly TextBlock _summaryScriptsLabel;

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
    private readonly StackPanel _domainSelectionPanel;
    private readonly ComboBox _domain1ComboBox;
    private readonly ComboBox _domain2ComboBox;
    private readonly TextBlock _classDescriptionLabel;
    private readonly TextBlock _prestigeToggleArrow;
    private readonly StackPanel _prestigePlanningContent;
    private readonly ComboBox _prestigeClassComboBox;
    private readonly TextBlock _prestigePrereqLabel;
    private readonly ToggleButton[] _alignmentButtons;
    private readonly TextBlock _alignmentRestrictionWarning;

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

    public NewCharacterWizardWindow(CreatureDisplayService displayService, IGameDataService gameDataService, ItemIconService? itemIconService = null, AudioService? audioService = null)
    {
        InitializeComponent();

        _displayService = displayService;
        _gameDataService = gameDataService;
        _itemIconService = itemIconService;
        _audioService = audioService;

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
            this.FindControl<Border>("Step8Border")!,
            this.FindControl<Border>("Step9Border")!,
            this.FindControl<Border>("Step10Border")!
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
            this.FindControl<Grid>("Step8Panel")!,
            this.FindControl<Grid>("Step9Panel")!,
            this.FindControl<Grid>("Step10Panel")!
        };

        _backButton = this.FindControl<Button>("BackButton")!;
        _nextButton = this.FindControl<Button>("NextButton")!;
        _finishButton = this.FindControl<Button>("FinishButton")!;
        _statusLabel = this.FindControl<TextBlock>("StatusLabel")!;

        // Step 1 controls
        _utcCard = this.FindControl<Border>("UtcCard")!;
        _bicCard = this.FindControl<Border>("BicCard")!;
        _defaultScriptsPanel = this.FindControl<StackPanel>("DefaultScriptsPanel")!;
        _defaultScriptsCheckBox = this.FindControl<CheckBox>("DefaultScriptsCheckBox")!;

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
        _domainSelectionPanel = this.FindControl<StackPanel>("DomainSelectionPanel")!;
        _domain1ComboBox = this.FindControl<ComboBox>("Domain1ComboBox")!;
        _domain2ComboBox = this.FindControl<ComboBox>("Domain2ComboBox")!;
        _classDescriptionLabel = this.FindControl<TextBlock>("ClassDescriptionLabel")!;
        _prestigeToggleArrow = this.FindControl<TextBlock>("PrestigeToggleArrow")!;
        _prestigePlanningContent = this.FindControl<StackPanel>("PrestigePlanningContent")!;
        _prestigeClassComboBox = this.FindControl<ComboBox>("PrestigeClassComboBox")!;
        _prestigePrereqLabel = this.FindControl<TextBlock>("PrestigePrereqLabel")!;
        _alignmentButtons = new[]
        {
            this.FindControl<ToggleButton>("AlignLG")!,
            this.FindControl<ToggleButton>("AlignNG")!,
            this.FindControl<ToggleButton>("AlignCG")!,
            this.FindControl<ToggleButton>("AlignLN")!,
            this.FindControl<ToggleButton>("AlignTN")!,
            this.FindControl<ToggleButton>("AlignCN")!,
            this.FindControl<ToggleButton>("AlignLE")!,
            this.FindControl<ToggleButton>("AlignNE")!,
            this.FindControl<ToggleButton>("AlignCE")!
        };
        _alignmentRestrictionWarning = this.FindControl<TextBlock>("AlignmentRestrictionWarning")!;

        // Step 5 controls
        _abilityPointsRemainingLabel = this.FindControl<TextBlock>("AbilityPointsRemainingLabel")!;
        _abilityRowsPanel = this.FindControl<StackPanel>("AbilityRowsPanel")!;
        _prestigeAbilityBanner = this.FindControl<Border>("PrestigeAbilityBanner")!;
        _prestigeAbilityBannerLabel = this.FindControl<TextBlock>("PrestigeAbilityBannerLabel")!;

        // Step 6 controls (Feats)
        _featStepDescription = this.FindControl<TextBlock>("FeatStepDescription")!;
        _featSelectionCountLabel = this.FindControl<TextBlock>("FeatSelectionCountLabel")!;
        _featSearchBox = this.FindControl<TextBox>("FeatSearchBox")!;
        _availableFeatsListBox = this.FindControl<ListBox>("AvailableFeatsListBox")!;
        _selectedFeatsListBox = this.FindControl<ListBox>("SelectedFeatsListBox")!;
        _selectedFeatCountLabel = this.FindControl<TextBlock>("SelectedFeatCountLabel")!;

        // Step 7 controls (Skills)
        _skillPointsRemainingLabel = this.FindControl<TextBlock>("SkillPointsRemainingLabel")!;
        _skillRowsPanel = this.FindControl<StackPanel>("SkillRowsPanel")!;
        _skillSearchBox = this.FindControl<TextBox>("SkillSearchBox")!;

        // Step 8 controls (Spells)
        _spellStepDescription = this.FindControl<TextBlock>("SpellStepDescription")!;
        _spellLevelTabsPanel = this.FindControl<StackPanel>("SpellLevelTabsPanel")!;
        _spellSelectionCountLabel = this.FindControl<TextBlock>("SpellSelectionCountLabel")!;
        _spellSearchBox2 = this.FindControl<TextBox>("SpellSearchBox")!;
        _availableSpellsListBox = this.FindControl<ListBox>("AvailableSpellsListBox")!;
        _selectedSpellsListBox = this.FindControl<ListBox>("SelectedSpellsListBox")!;
        _selectedSpellCountLabel = this.FindControl<TextBlock>("SelectedSpellCountLabel")!;
        _spellSelectionTwoPanel = this.FindControl<Grid>("SpellSelectionTwoPanel")!;
        _divineSpellInfoPanel = this.FindControl<Border>("DivineSpellInfoPanel")!;
        _divineSpellInfoLabel = this.FindControl<TextBlock>("DivineSpellInfoLabel")!;

        // Step 9 controls (Equipment)
        _equipmentCountLabel = this.FindControl<TextBlock>("EquipmentCountLabel")!;
        _equipmentItemsPanel = this.FindControl<StackPanel>("EquipmentItemsPanel")!;
        _equipmentEmptyLabel = this.FindControl<TextBlock>("EquipmentEmptyLabel")!;

        // Step 10 controls (Summary)
        _characterNameTextBox = this.FindControl<TextBox>("CharacterNameTextBox")!;
        _lastNameTextBox = this.FindControl<TextBox>("LastNameTextBox")!;
        _ageLabelText = this.FindControl<TextBlock>("AgeLabelText")!;
        _ageNumericUpDown = this.FindControl<NumericUpDown>("AgeNumericUpDown")!;
        _ageNote = this.FindControl<TextBlock>("AgeNote")!;
        _descriptionTextBox = this.FindControl<TextBox>("DescriptionTextBox")!;
        _voiceSetLabel = this.FindControl<TextBlock>("VoiceSetLabel")!;
        _generatedTagLabel = this.FindControl<TextBlock>("GeneratedTagLabel")!;
        _generatedResRefLabel = this.FindControl<TextBlock>("GeneratedResRefLabel")!;
        _paletteIdLabelText = this.FindControl<TextBlock>("PaletteIdLabelText")!;
        _paletteIdComboBox = this.FindControl<ComboBox>("PaletteIdComboBox")!;
        PopulatePaletteCategories();
        _paletteIdNote = this.FindControl<TextBlock>("PaletteIdNote")!;
        _summaryFileTypeLabel = this.FindControl<TextBlock>("SummaryFileTypeLabel")!;
        _summaryRaceLabel = this.FindControl<TextBlock>("SummaryRaceLabel")!;
        _summaryAppearanceLabel = this.FindControl<TextBlock>("SummaryAppearanceLabel")!;
        _summaryClassLabel = this.FindControl<TextBlock>("SummaryClassLabel")!;
        _summaryAlignmentLabel = this.FindControl<TextBlock>("SummaryAlignmentLabel")!;
        _summaryAbilitiesLabel = this.FindControl<TextBlock>("SummaryAbilitiesLabel")!;
        _summaryFeatsLabel = this.FindControl<TextBlock>("SummaryFeatsLabel")!;
        _summarySkillsLabel = this.FindControl<TextBlock>("SummarySkillsLabel")!;
        _summarySpellsLabel = this.FindControl<TextBlock>("SummarySpellsLabel")!;
        _summarySpellsSection = this.FindControl<Grid>("SummarySpellsSection")!;
        _summaryEquipmentSection = this.FindControl<Grid>("SummaryEquipmentSection")!;
        _summaryEquipmentLabel = this.FindControl<TextBlock>("SummaryEquipmentLabel")!;
        _summaryScriptsDivider = this.FindControl<Border>("SummaryScriptsDivider")!;
        _summaryScriptsSection = this.FindControl<Grid>("SummaryScriptsSection")!;
        _summaryScriptsLabel = this.FindControl<TextBlock>("SummaryScriptsLabel")!;

        UpdateStepDisplay();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void PopulatePaletteCategories()
    {
        _paletteIdComboBox.Items.Clear();

        var categories = _displayService.GetCreaturePaletteCategories().ToList();

        if (categories.Count == 0)
        {
            _paletteIdComboBox.Items.Add(new ComboBoxItem { Content = "Custom (1)", Tag = (byte)1 });
            _paletteIdComboBox.SelectedIndex = 0;
            return;
        }

        int defaultIndex = 0;
        int index = 0;
        foreach (var category in categories.OrderBy(c => c.Id))
        {
            var displayName = !string.IsNullOrEmpty(category.ParentPath)
                ? $"{category.ParentPath}/{category.Name} ({category.Id})"
                : $"{category.Name} ({category.Id})";

            _paletteIdComboBox.Items.Add(new ComboBoxItem
            {
                Content = displayName,
                Tag = category.Id
            });

            if (category.Id == 1) defaultIndex = index;
            index++;
        }

        _paletteIdComboBox.SelectedIndex = defaultIndex;
    }

    private async void OnBrowseVoiceSetClick(object? sender, RoutedEventArgs e)
    {
        if (_audioService == null) return;

        var browser = new SoundsetBrowserWindow(_gameDataService, _audioService);
        var result = await browser.ShowDialog<ushort?>(this);

        if (result.HasValue)
        {
            _selectedVoiceSetId = result.Value;
            var name = _displayService.GetSoundSetName(result.Value);
            _voiceSetLabel.Text = $"{name} ({result.Value})";
        }
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
            6 => IsFeatSelectionComplete(), // Must choose all available feats
            7 => GetSkillPointsRemaining() >= 0, // Can't overspend
            8 => !_needsSpellSelection || _isDivineCaster || IsSpellSelectionComplete(),
            9 => true, // Equipment is optional
            10 => true, // Summary is always valid (name is optional)
            _ => true
        };

        _nextButton.IsEnabled = canProceed;
        _finishButton.IsEnabled = canProceed;

        _statusLabel.Text = _currentStep switch
        {
            2 when !canProceed => "Select a race to continue.",
            4 when !canProceed => "Select a class to continue.",
            5 when !canProceed => $"Spend all {_pointBuyTotal} ability points to continue.",
            6 when !canProceed => $"Select {_featsToChoose - _chosenFeatIds.Count} more feat(s) to continue.",
            8 when !canProceed => "Select all required spells to continue.",
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
                break;
            case 8:
                PrepareStep8();
                if (!_needsSpellSelection)
                {
                    _currentStep++;
                    PrepareCurrentStep(); // Skip to step 9
                }
                break;
            case 9:
                PrepareStep9();
                break;
            case 10:
                PrepareStep10();
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
            if (_currentStep == 8 && !_needsSpellSelection)
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

        // Show default scripts option for UTC only
        _defaultScriptsPanel.IsVisible = !_isBicFile;

        UpdateSidebarSummary();
    }

    #endregion
}
