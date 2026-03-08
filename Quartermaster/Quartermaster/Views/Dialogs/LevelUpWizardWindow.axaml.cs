using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Quartermaster.Services;
using Radoub.Formats.Utc;
using Radoub.UI.Services;

namespace Quartermaster.Views.Dialogs;

/// <summary>
/// Multi-step wizard for leveling up a creature.
/// Guides through class selection, feat selection, skill allocation, and spell selection.
/// Partial class files: ClassSelection, FeatSelection, SkillAllocation, SpellSelection, SummaryAndApply.
/// </summary>
public partial class LevelUpWizardWindow : Window
{
    private readonly CreatureDisplayService _displayService;
    private readonly UtcFile _creature;
    private readonly UtcFile _originalCreature; // For cancellation

    // Wizard state
    private int _currentStep = 1;
    private const int TotalSteps = 5;

    // Level-up choices
    private int _selectedClassId = -1;
    private int _newClassLevel;
    private bool _isNewClass;
    private List<int> _selectedFeats = new();
    private Dictionary<int, int> _skillPointsAdded = new();
    private readonly Dictionary<int, List<int>> _selectedSpellsByLevel = new();

    // Step applicability
    private int _featsToSelect;
    private int _generalFeatsToSelect;
    private int _bonusFeatsToSelect;
    private int _skillPointsToAllocate;
    private bool _needsSpellSelection;
    private byte _resolvedPackageId = 255;

    // Spell selection state
    private bool _isDivineCaster;
    private bool _isSpontaneousCaster;
    private int _maxSpellLevelThisLevel;
    private int _currentSpellLevel;
    private Dictionary<int, int> _newSpellsPerLevel = new(); // spell level -> new spells to pick
    private int _wizardFreeSpellsRemaining; // Wizard: 2 free spells distributed across levels
    private List<SpellDisplayItem> _availableSpellsForLevel = new();
    private List<SpellDisplayItem> _filteredAvailableSpells = new();

    // Bonus feat restriction
    private HashSet<int>? _bonusFeatPool;

    // UI state
    private List<ClassDisplayItem> _allClasses = new();
    private List<ClassDisplayItem> _filteredClasses = new();
    private List<FeatDisplayItem> _allAvailableFeats = new();
    private List<FeatDisplayItem> _filteredAvailableFeats = new();

    // Controls
    private readonly TextBlock _characterNameLabel;
    private readonly TextBlock _characterLevelLabel;
    private readonly Border[] _stepBorders;
    private readonly Grid[] _stepPanels;
    private readonly Button _backButton;
    private readonly Button _nextButton;
    private readonly Button _finishButton;
    private readonly TextBlock _statusLabel;

    // Step 1 controls
    private readonly TextBox _classSearchBox;
    private readonly CheckBox _showUnqualifiedPrestigeCheckBox;
    private readonly ListBox _classListBox;
    private readonly TextBlock _selectedClassNameLabel;
    private readonly StackPanel _classStatsPanel;
    private readonly TextBlock _classHitDieLabel;
    private readonly TextBlock _classSkillPointsLabel;
    private readonly Border _classPrereqPanel;
    private readonly ItemsControl _classPrereqItems;
    private readonly TextBlock _classDescriptionLabel;

    // Step 2 controls
    private readonly TextBlock _featAllocationLabel;
    private readonly TextBox _featSearchBox;
    private readonly ListBox _availableFeatsListBox;
    private readonly ListBox _selectedFeatsListBox;
    private readonly TextBlock _selectedFeatsHeader;
    private readonly Button _addFeatButton;
    private readonly Button _removeFeatButton;

    // Step 3 controls
    private readonly TextBlock _skillPointsRemainingLabel;
    private readonly TextBlock _skillPointsTotalLabel;
    private readonly ItemsControl _skillsItemsControl;

    // Step 4 controls
    private readonly TextBlock _spellStepDescription;
    private readonly Border _spellTabsBar;
    private readonly StackPanel _spellLevelTabsPanel;
    private readonly TextBlock _spellSelectionCountLabel;
    private readonly Grid _spellSelectionTwoPanel;
    private readonly TextBox _spellSearchBox;
    private readonly ListBox _availableSpellsListBox;
    private readonly ListBox _selectedSpellsListBox;
    private readonly TextBlock _selectedSpellCountLabel;
    private readonly Border _divineSpellInfoPanel;
    private readonly TextBlock _divineSpellInfoLabel;
    private readonly ItemsControl _divineSpellsList;

    // Validation level (#1503)
    private readonly ComboBox _validationLevelComboBox;
    private ValidationLevel _validationLevel => (ValidationLevel)_validationLevelComboBox.SelectedIndex;

    // Step 5 controls
    private readonly TextBlock _summaryClassLabel;
    private readonly Border _summaryFeatsPanel;
    private readonly ItemsControl _summaryFeatsList;
    private readonly ItemsControl _summarySkillsList;
    private readonly Border _summarySpellsPanel;
    private readonly ItemsControl _summarySpellsList;
    private readonly TextBlock _summaryHpLabel;
    private readonly TextBlock _summaryBabLabel;
    private readonly TextBlock _summarySavesLabel;

    public bool Confirmed { get; private set; }

    /// <summary>
    /// Designer-only constructor. Do not use at runtime.
    /// </summary>
    [Obsolete("Designer use only", error: true)]
    public LevelUpWizardWindow() => throw new NotSupportedException("Use parameterized constructor");

    public LevelUpWizardWindow(CreatureDisplayService displayService, UtcFile creature)
    {
        InitializeComponent();

        _displayService = displayService;
        _creature = creature;
        _originalCreature = creature.DeepCopy(); // Deep copy for cancel/undo rollback

        // Find all controls
        _characterNameLabel = this.FindControl<TextBlock>("CharacterNameLabel")!;
        _characterLevelLabel = this.FindControl<TextBlock>("CharacterLevelLabel")!;

        _stepBorders = new[]
        {
            this.FindControl<Border>("Step1Border")!,
            this.FindControl<Border>("Step2Border")!,
            this.FindControl<Border>("Step3Border")!,
            this.FindControl<Border>("Step4Border")!,
            this.FindControl<Border>("Step5Border")!
        };

        _stepPanels = new[]
        {
            this.FindControl<Grid>("Step1Panel")!,
            this.FindControl<Grid>("Step2Panel")!,
            this.FindControl<Grid>("Step3Panel")!,
            this.FindControl<Grid>("Step4Panel")!,
            this.FindControl<Grid>("Step5Panel")!
        };

        _backButton = this.FindControl<Button>("BackButton")!;
        _nextButton = this.FindControl<Button>("NextButton")!;
        _finishButton = this.FindControl<Button>("FinishButton")!;
        _statusLabel = this.FindControl<TextBlock>("StatusLabel")!;

        // Step 1
        _classSearchBox = this.FindControl<TextBox>("ClassSearchBox")!;
        _showUnqualifiedPrestigeCheckBox = this.FindControl<CheckBox>("ShowUnqualifiedPrestigeCheckBox")!;
        _classListBox = this.FindControl<ListBox>("ClassListBox")!;
        _selectedClassNameLabel = this.FindControl<TextBlock>("SelectedClassNameLabel")!;
        _classStatsPanel = this.FindControl<StackPanel>("ClassStatsPanel")!;
        _classHitDieLabel = this.FindControl<TextBlock>("ClassHitDieLabel")!;
        _classSkillPointsLabel = this.FindControl<TextBlock>("ClassSkillPointsLabel")!;
        _classPrereqPanel = this.FindControl<Border>("ClassPrereqPanel")!;
        _classPrereqItems = this.FindControl<ItemsControl>("ClassPrereqItems")!;
        _classDescriptionLabel = this.FindControl<TextBlock>("ClassDescriptionLabel")!;

        // Step 2
        _featAllocationLabel = this.FindControl<TextBlock>("FeatAllocationLabel")!;
        _featSearchBox = this.FindControl<TextBox>("FeatSearchBox")!;
        _availableFeatsListBox = this.FindControl<ListBox>("AvailableFeatsListBox")!;
        _selectedFeatsListBox = this.FindControl<ListBox>("SelectedFeatsListBox")!;
        _selectedFeatsHeader = this.FindControl<TextBlock>("SelectedFeatsHeader")!;
        _addFeatButton = this.FindControl<Button>("AddFeatButton")!;
        _removeFeatButton = this.FindControl<Button>("RemoveFeatButton")!;

        // Step 3
        _skillPointsRemainingLabel = this.FindControl<TextBlock>("SkillPointsRemainingLabel")!;
        _skillPointsTotalLabel = this.FindControl<TextBlock>("SkillPointsTotalLabel")!;
        _skillsItemsControl = this.FindControl<ItemsControl>("SkillsItemsControl")!;

        // Step 4
        _spellStepDescription = this.FindControl<TextBlock>("SpellStepDescription")!;
        _spellTabsBar = this.FindControl<Border>("SpellTabsBar")!;
        _spellLevelTabsPanel = this.FindControl<StackPanel>("SpellLevelTabsPanel")!;
        _spellSelectionCountLabel = this.FindControl<TextBlock>("SpellSelectionCountLabel")!;
        _spellSelectionTwoPanel = this.FindControl<Grid>("SpellSelectionTwoPanel")!;
        _spellSearchBox = this.FindControl<TextBox>("SpellSearchBox")!;
        _availableSpellsListBox = this.FindControl<ListBox>("AvailableSpellsListBox")!;
        _selectedSpellsListBox = this.FindControl<ListBox>("SelectedSpellsListBox")!;
        _selectedSpellCountLabel = this.FindControl<TextBlock>("SelectedSpellCountLabel")!;
        _divineSpellInfoPanel = this.FindControl<Border>("DivineSpellInfoPanel")!;
        _divineSpellInfoLabel = this.FindControl<TextBlock>("DivineSpellInfoLabel")!;
        _divineSpellsList = this.FindControl<ItemsControl>("DivineSpellsList")!;

        // Step 5
        _summaryClassLabel = this.FindControl<TextBlock>("SummaryClassLabel")!;
        _summaryFeatsPanel = this.FindControl<Border>("SummaryFeatsPanel")!;
        _summaryFeatsList = this.FindControl<ItemsControl>("SummaryFeatsList")!;
        _summarySkillsList = this.FindControl<ItemsControl>("SummarySkillsList")!;
        _summarySpellsPanel = this.FindControl<Border>("SummarySpellsPanel")!;
        _summarySpellsList = this.FindControl<ItemsControl>("SummarySpellsList")!;
        _summaryHpLabel = this.FindControl<TextBlock>("SummaryHpLabel")!;
        _summaryBabLabel = this.FindControl<TextBlock>("SummaryBabLabel")!;
        _summarySavesLabel = this.FindControl<TextBlock>("SummarySavesLabel")!;

        // Validation level toggle (#1503)
        _validationLevelComboBox = this.FindControl<ComboBox>("ValidationLevelComboBox")!;
        _validationLevelComboBox.SelectedIndex = (int)SettingsService.Instance.ValidationLevel;
        _validationLevelComboBox.SelectionChanged += OnValidationLevelChanged;

        InitializeWizard();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void InitializeWizard()
    {
        if (_creature == null || _displayService == null)
            return;

        // Set character info
        var fullName = CreatureDisplayService.GetCreatureFullName(_creature);
        _characterNameLabel.Text = fullName;

        int currentLevel = _creature.ClassList.Sum(c => c.ClassLevel);
        _characterLevelLabel.Text = $"Level {currentLevel} -> {currentLevel + 1}";

        // Load classes for Step 1
        LoadClassList();

        // Show Step 1
        UpdateStepDisplay();
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

        // Update navigation buttons
        _backButton.IsEnabled = _currentStep > 1;
        _nextButton.IsVisible = _currentStep < TotalSteps;
        _finishButton.IsVisible = _currentStep == TotalSteps;

        // Validate current step
        ValidateCurrentStep();
    }

    private void OnValidationLevelChanged(object? sender, SelectionChangedEventArgs e)
    {
        var level = (ValidationLevel)_validationLevelComboBox.SelectedIndex;
        SettingsService.Instance.ValidationLevel = level;
        ValidateCurrentStep();
    }

    private void ValidateCurrentStep()
    {
        bool canProceed;
        if (_validationLevel == ValidationLevel.None)
        {
            // Chaotic Evil: only require class selection
            canProceed = _currentStep switch
            {
                1 => _selectedClassId >= 0,
                _ => true
            };
        }
        else
        {
            canProceed = _currentStep switch
            {
                1 => _selectedClassId >= 0,
                2 => _selectedFeats.Count >= _featsToSelect,
                3 => GetRemainingSkillPoints() == 0,
                4 => _isDivineCaster || IsSpellSelectionComplete(),
                5 => true,
                _ => false
            };
        }

        _nextButton.IsEnabled = canProceed;
        _finishButton.IsEnabled = canProceed;

        // Update status message
        _statusLabel.Text = _currentStep switch
        {
            1 when _selectedClassId < 0 => "Select a class to continue.",
            2 when _selectedFeats.Count < _featsToSelect => $"Select {_featsToSelect - _selectedFeats.Count} more feat(s).",
            3 when GetRemainingSkillPoints() > 0 => $"Allocate {GetRemainingSkillPoints()} remaining skill point(s).",
            4 when !_isDivineCaster && !IsSpellSelectionComplete() => "Select spells for each spell level.",
            _ => ""
        };
    }

    private void OnBackClick(object? sender, RoutedEventArgs e)
    {
        if (_currentStep > 1)
        {
            _currentStep--;

            // Skip steps that don't apply
            if (_currentStep == 4 && !_needsSpellSelection)
                _currentStep--;
            if (_currentStep == 2 && _featsToSelect == 0)
                _currentStep--;

            UpdateStepDisplay();
        }
    }

    private void OnNextClick(object? sender, RoutedEventArgs e)
    {
        if (_currentStep < TotalSteps)
        {
            _currentStep++;

            // Prepare the next step (and handle skip logic)
            PrepareCurrentStep();

            UpdateStepDisplay();
        }
    }

    private void PrepareCurrentStep()
    {
        switch (_currentStep)
        {
            case 2:
                PrepareStep2(); // Calculate feat requirements
                // Skip if no feats to select
                if (_featsToSelect == 0)
                {
                    _currentStep++;
                    PrepareCurrentStep(); // Recursively prepare next step
                }
                break;

            case 3:
                PrepareStep3(); // Calculate skill points
                break;

            case 4:
                PrepareStep4(); // Check spell requirements
                // Skip if no spell selection needed
                if (!_needsSpellSelection)
                {
                    _currentStep++;
                    PrepareCurrentStep(); // Recursively prepare next step
                }
                break;

            case 5:
                PrepareStep5(); // Build summary
                break;
        }
    }

    private void OnFinishClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            ApplyLevelUp();
            Confirmed = true;
        }
        catch (Exception ex)
        {
            // Rollback: restore creature from deep copy
            RestoreFromOriginal();
            Confirmed = false;
            Radoub.Formats.Logging.UnifiedLogger.LogApplication(
                Radoub.Formats.Logging.LogLevel.ERROR,
                $"Level-up failed, rolled back changes: {ex.Message}");
        }
        Close();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Confirmed = false;
        Close();
    }

    /// <summary>
    /// Restores the creature to its pre-wizard state from the deep copy.
    /// Used for rollback on error during ApplyLevelUp.
    /// </summary>
    private void RestoreFromOriginal()
    {
        _creature.ClassList = _originalCreature.ClassList;
        _creature.FeatList = _originalCreature.FeatList;
        _creature.SkillList = _originalCreature.SkillList;
        _creature.SpecAbilityList = _originalCreature.SpecAbilityList;
        _creature.Comment = _originalCreature.Comment;
    }

    #endregion
}
