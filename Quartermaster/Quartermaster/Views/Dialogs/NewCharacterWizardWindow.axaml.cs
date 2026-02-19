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
using Radoub.Formats.Utc;
using Radoub.UI.Services;

namespace Quartermaster.Views.Dialogs;

/// <summary>
/// Multi-step wizard for creating a new creature from scratch.
/// 8 steps: File Type, Race &amp; Sex, Appearance, Class, Abilities, Skills, Spells, Summary.
/// Steps 3-8 are placeholders pending future sprints.
/// </summary>
public partial class NewCharacterWizardWindow : Window
{
    private readonly CreatureDisplayService _displayService;

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

    public NewCharacterWizardWindow(CreatureDisplayService displayService)
    {
        InitializeComponent();

        _displayService = displayService;

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
            2 => _selectedRaceId != 255, // Must have a race selected (255 = sentinel for "none")
            _ => true // Placeholder steps always valid
        };

        _nextButton.IsEnabled = canProceed;
        _finishButton.IsEnabled = canProceed;

        _statusLabel.Text = _currentStep switch
        {
            2 when !canProceed => "Select a race to continue.",
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
            // Future steps will prepare here
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
        // Build minimal creature with what we have so far
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
        var favoredClassId = _displayService.GetFavoredClass(_selectedRaceId);
        _favoredClassLabel.Text = favoredClassId == -1
            ? "Any"
            : _displayService.GetClassName(favoredClassId);

        // Size
        _raceSizeLabel.Text = _displayService.GetRaceSizeCategory(_selectedRaceId);

        _raceTraitsPanel.IsVisible = true;

        // Description
        var descStrRef = _displayService.GameDataService.Get2DAValue("racialtypes", _selectedRaceId, "Description");
        if (!string.IsNullOrEmpty(descStrRef) && descStrRef != "****")
        {
            var desc = _displayService.GameDataService.GetString(descStrRef);
            _raceDescriptionLabel.Text = !string.IsNullOrEmpty(desc) ? desc : $"The {raceName}.";
            _raceDescriptionLabel.Foreground = null; // Use default foreground
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
            label.Foreground = new SolidColorBrush(Colors.Transparent); // Will use default via null

        // For zero modifiers, use the default text color
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

    #region Build Creature

    /// <summary>
    /// Builds a UtcFile from the current wizard selections.
    /// Currently only populates race and gender (Sprint 1).
    /// Future sprints will add class, stats, skills, spells, etc.
    /// </summary>
    private UtcFile BuildCreature()
    {
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

            // Race & gender from wizard
            Race = _selectedRaceId,
            Gender = _selectedGender,

            // Appearance defaults (will be set by Step 3 in Sprint 2)
            AppearanceType = GetDefaultAppearanceForRace(_selectedRaceId),
            Phenotype = 0,
            PortraitId = 1,

            // Default body parts
            AppearanceHead = 1,
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

            // Colors
            Color_Skin = 0,
            Color_Hair = 0,
            Color_Tattoo1 = 0,
            Color_Tattoo2 = 0,

            // Default ability scores (will be set by Step 5 in Sprint 3)
            Str = 10,
            Dex = 10,
            Con = 10,
            Int = 10,
            Wis = 10,
            Cha = 10,

            // HP
            HitPoints = 4,
            CurrentHitPoints = 4,
            MaxHitPoints = 4,

            // Alignment - True Neutral
            GoodEvil = 50,
            LawfulChaotic = 50,

            // Behavior defaults
            FactionID = 1,
            PerceptionRange = 11,
            WalkRate = 4,
            DecayTime = 5000,
            Interruptable = true,

            // Commoner level 1 (will be replaced by Step 4 in Sprint 2)
            ClassList = new List<CreatureClass>
            {
                new CreatureClass
                {
                    Class = 255, // Commoner
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

    #endregion
}
