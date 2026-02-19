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
    private byte _skinColor;
    private byte _hairColor;
    private byte _tattoo1Color;
    private byte _tattoo2Color;
    private bool _isPartBased;
    private bool _step3Loaded;

    // Step 4: Class & Package
    private int _selectedClassId = -1;
    private byte _selectedPackageId = 255; // sentinel for none
    private int _favoredClassId = -1;
    private List<ClassDisplayItem> _allClasses = new();
    private List<ClassDisplayItem> _filteredClasses = new();
    private bool _step4Loaded;
    private bool _prestigePlanningExpanded;

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
    private readonly NumericUpDown _skinColorNumericUpDown;
    private readonly NumericUpDown _hairColorNumericUpDown;
    private readonly NumericUpDown _tattoo1ColorNumericUpDown;
    private readonly NumericUpDown _tattoo2ColorNumericUpDown;
    private readonly TextBlock _bodyPartsNotApplicableLabel;
    private readonly Grid _bodyPartsPanel;

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
        _skinColorNumericUpDown = this.FindControl<NumericUpDown>("SkinColorNumericUpDown")!;
        _hairColorNumericUpDown = this.FindControl<NumericUpDown>("HairColorNumericUpDown")!;
        _tattoo1ColorNumericUpDown = this.FindControl<NumericUpDown>("Tattoo1ColorNumericUpDown")!;
        _tattoo2ColorNumericUpDown = this.FindControl<NumericUpDown>("Tattoo2ColorNumericUpDown")!;
        _bodyPartsNotApplicableLabel = this.FindControl<TextBlock>("BodyPartsNotApplicableLabel")!;
        _bodyPartsPanel = this.FindControl<Grid>("BodyPartsPanel")!;

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
            _ => true // Placeholder steps always valid
        };

        _nextButton.IsEnabled = canProceed;
        _finishButton.IsEnabled = canProceed;

        _statusLabel.Text = _currentStep switch
        {
            2 when !canProceed => "Select a race to continue.",
            4 when !canProceed => "Select a class to continue.",
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
        _headNumericUpDown.IsEnabled = _isPartBased;
        _skinColorNumericUpDown.IsEnabled = _isPartBased;
        _hairColorNumericUpDown.IsEnabled = _isPartBased;
        _tattoo1ColorNumericUpDown.IsEnabled = _isPartBased;
        _tattoo2ColorNumericUpDown.IsEnabled = _isPartBased;
        _bodyPartsNotApplicableLabel.IsVisible = !_isPartBased;
    }

    private void OnBodyPartChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        // Sync values from controls to state
        _headVariation = (byte)(_headNumericUpDown.Value ?? 1);
        _skinColor = (byte)(_skinColorNumericUpDown.Value ?? 0);
        _hairColor = (byte)(_hairColorNumericUpDown.Value ?? 0);
        _tattoo1Color = (byte)(_tattoo1ColorNumericUpDown.Value ?? 0);
        _tattoo2Color = (byte)(_tattoo2ColorNumericUpDown.Value ?? 0);
    }

    private async void OnBrowsePortraitClick(object? sender, RoutedEventArgs e)
    {
        if (_itemIconService == null)
            return;

        var browser = new PortraitBrowserWindow(_gameDataService, _itemIconService);
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

    #region Build Creature

    /// <summary>
    /// Builds a UtcFile from the current wizard selections.
    /// Populates race, gender, appearance, and class from Steps 1-4.
    /// Future sprints will add stats, skills, spells, etc.
    /// </summary>
    private UtcFile BuildCreature()
    {
        // Determine class and hit die for HP calculation
        int classId = _selectedClassId >= 0 ? _selectedClassId : 255; // Commoner fallback
        var hitDie = _selectedClassId >= 0
            ? _displayService.Classes.GetClassMetadata(_selectedClassId).HitDie
            : 4;

        return new UtcFile
        {
            // Identity
            FirstName = new CExoLocString { StrRef = 0xFFFFFFFF },
            LastName = new CExoLocString { StrRef = 0xFFFFFFFF },
            Tag = "new_creature",
            TemplateResRef = "new_creature",
            Description = new CExoLocString { StrRef = 0xFFFFFFFF },

            // Palette
            PaletteID = 1,

            // Race & gender from Step 2
            Race = _selectedRaceId,
            Gender = _selectedGender,

            // Appearance from Step 3
            AppearanceType = _selectedAppearanceId > 0 ? _selectedAppearanceId : GetDefaultAppearanceForRace(_selectedRaceId),
            Phenotype = _selectedPhenotype,
            PortraitId = _selectedPortraitId,

            // Body parts from Step 3
            AppearanceHead = _headVariation,
            BodyPart_Belt = 0,
            BodyPart_LBicep = 1,
            BodyPart_RBicep = 1,
            BodyPart_LFArm = 1,
            BodyPart_RFArm = 1,
            BodyPart_LFoot = 1,
            BodyPart_RFoot = 1,
            BodyPart_LHand = 1,
            BodyPart_RHand = 1,
            BodyPart_LShin = 1,
            BodyPart_RShin = 1,
            BodyPart_LShoul = 0,
            BodyPart_RShoul = 0,
            BodyPart_LThigh = 1,
            BodyPart_RThigh = 1,
            BodyPart_Neck = 1,
            BodyPart_Pelvis = 1,
            BodyPart_Torso = 1,

            // Colors from Step 3
            Color_Skin = _skinColor,
            Color_Hair = _hairColor,
            Color_Tattoo1 = _tattoo1Color,
            Color_Tattoo2 = _tattoo2Color,

            // Default ability scores (will be set by Step 5 in Sprint 3)
            Str = 10,
            Dex = 10,
            Con = 10,
            Int = 10,
            Wis = 10,
            Cha = 10,

            // HP based on class hit die
            HitPoints = (short)hitDie,
            CurrentHitPoints = (short)hitDie,
            MaxHitPoints = (short)hitDie,

            // Alignment - True Neutral
            GoodEvil = 50,
            LawfulChaotic = 50,

            // Behavior defaults
            FactionID = 1,
            PerceptionRange = 11,
            WalkRate = 4,
            DecayTime = 5000,
            Interruptable = true,

            // Starting package
            StartingPackage = _selectedPackageId != 255 ? _selectedPackageId : (byte)0,

            // Class from Step 4
            ClassList = new List<CreatureClass>
            {
                new CreatureClass
                {
                    Class = classId,
                    ClassLevel = 1
                }
            },

            // Empty lists (will be populated by future steps)
            FeatList = new List<ushort>(),
            SkillList = new List<byte>(),
            SpecAbilityList = new List<SpecialAbility>(),
            ItemList = new List<InventoryItem>(),
            EquipItemList = new List<EquippedItem>()
        };
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

    #endregion
}
