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
    private const int TotalSteps = 6;

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
    private bool _needsAbilityIncrease;
    private int _selectedAbilityIncrease = -1; // -1=none, 0=STR, 1=DEX, 2=CON, 3=INT, 4=WIS, 5=CHA
    private readonly HashSet<int> _ceAbilityIncreases = new(); // CE mode: multiple ability increases
    private int _hpIncrease; // Pre-calculated HP gain for this level
    private int _conRetroactiveHp; // Retroactive HP from CON modifier change
    private byte _resolvedPackageId = 255;

    // Consolidated level-up (#1645)
    private int _levelsToAdd = 1;
    private int _fromClassLevel; // First new class level in range
    private readonly int _presetClassId;
    private readonly int _presetLevels;
    private List<int> _abilityIncreaseLevels = new(); // Character levels with +1 ability
    private Dictionary<int, int> _abilityIncreasesByLevel = new(); // charLevel -> abilityIndex
    private int[] _abilityIncrements = new int[6]; // Per-ability increment count (STR=0..CHA=5)

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
    private int _projectedBab; // BAB after all consolidated levels applied (#1741)

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
    private readonly TextBlock _prestigeHintsLabel;
    private readonly Button _backButton;
    private readonly Button _nextButton;
    private readonly Button _finishButton;
    private readonly TextBlock _statusLabel;

    // Sidebar summary labels
    private readonly TextBlock _step1Summary;
    private readonly TextBlock _step2Summary;
    private readonly TextBlock _step3Summary;
    private readonly TextBlock _step4Summary;
    private readonly TextBlock _step5Summary;

    // Step 2 (Ability Score) controls
    private readonly TextBlock _abilityIncreaseDescription;
    private readonly TextBlock _abilityIncreaseRemaining;
    private readonly Border[] _abilityBorders;
    private readonly TextBlock[] _abilityRadios;
    private readonly TextBlock[] _abilityValues;
    private readonly TextBlock[] _abilityChanges;
    private static readonly string[] AbilityNames = { "Strength", "Dexterity", "Constitution", "Intelligence", "Wisdom", "Charisma" };

    // Step 1 controls
    private readonly NumericUpDown _levelsToAddSpinner;
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
    private readonly TextBlock _featDescriptionLabel;

    // Step 3 controls
    private readonly TextBlock _skillPointsRemainingLabel;
    private readonly TextBlock _skillPointsTotalLabel;
    private readonly ItemsControl _skillsItemsControl;
    private readonly TextBox _skillSearchBox;

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

    // Step 6 controls
    private readonly TextBlock _summaryClassLabel;
    private readonly Border _summaryAbilityPanel;
    private readonly TextBlock _summaryAbilityLabel;
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

    private readonly bool _isBicFile;

    public LevelUpWizardWindow(CreatureDisplayService displayService, UtcFile creature,
        bool isBicFile = false, int presetClassId = -1, int presetLevels = 1)
    {
        InitializeComponent();

        _displayService = displayService;
        _creature = creature;
        _isBicFile = isBicFile;
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
            this.FindControl<Border>("Step5Border")!,
            this.FindControl<Border>("Step6Border")!
        };

        _stepPanels = new[]
        {
            this.FindControl<Grid>("Step1Panel")!,
            this.FindControl<Grid>("Step2Panel")!,
            this.FindControl<Grid>("Step3Panel")!,
            this.FindControl<Grid>("Step4Panel")!,
            this.FindControl<Grid>("Step5Panel")!,
            this.FindControl<Grid>("Step6Panel")!
        };

        // Sidebar summaries
        _step1Summary = this.FindControl<TextBlock>("Step1Summary")!;
        _step2Summary = this.FindControl<TextBlock>("Step2Summary")!;
        _step3Summary = this.FindControl<TextBlock>("Step3Summary")!;
        _step4Summary = this.FindControl<TextBlock>("Step4Summary")!;
        _step5Summary = this.FindControl<TextBlock>("Step5Summary")!;

        // Step 2 ability score controls
        _abilityIncreaseDescription = this.FindControl<TextBlock>("AbilityIncreaseDescription")!;
        _abilityIncreaseRemaining = this.FindControl<TextBlock>("AbilityIncreaseRemaining")!;
        _abilityBorders = new[]
        {
            this.FindControl<Border>("AbilityStrBorder")!,
            this.FindControl<Border>("AbilityDexBorder")!,
            this.FindControl<Border>("AbilityConBorder")!,
            this.FindControl<Border>("AbilityIntBorder")!,
            this.FindControl<Border>("AbilityWisBorder")!,
            this.FindControl<Border>("AbilityChaBorder")!
        };
        _abilityRadios = new[]
        {
            this.FindControl<TextBlock>("AbilityStrRadio")!,
            this.FindControl<TextBlock>("AbilityDexRadio")!,
            this.FindControl<TextBlock>("AbilityConRadio")!,
            this.FindControl<TextBlock>("AbilityIntRadio")!,
            this.FindControl<TextBlock>("AbilityWisRadio")!,
            this.FindControl<TextBlock>("AbilityChaRadio")!
        };
        _abilityValues = new[]
        {
            this.FindControl<TextBlock>("AbilityStrValue")!,
            this.FindControl<TextBlock>("AbilityDexValue")!,
            this.FindControl<TextBlock>("AbilityConValue")!,
            this.FindControl<TextBlock>("AbilityIntValue")!,
            this.FindControl<TextBlock>("AbilityWisValue")!,
            this.FindControl<TextBlock>("AbilityChaValue")!
        };
        _abilityChanges = new[]
        {
            this.FindControl<TextBlock>("AbilityStrChange")!,
            this.FindControl<TextBlock>("AbilityDexChange")!,
            this.FindControl<TextBlock>("AbilityConChange")!,
            this.FindControl<TextBlock>("AbilityIntChange")!,
            this.FindControl<TextBlock>("AbilityWisChange")!,
            this.FindControl<TextBlock>("AbilityChaChange")!
        };

        _backButton = this.FindControl<Button>("BackButton")!;
        _nextButton = this.FindControl<Button>("NextButton")!;
        _finishButton = this.FindControl<Button>("FinishButton")!;
        _statusLabel = this.FindControl<TextBlock>("StatusLabel")!;
        _prestigeHintsLabel = this.FindControl<TextBlock>("PrestigeHintsLabel")!;

        // Step 1
        _levelsToAddSpinner = this.FindControl<NumericUpDown>("LevelsToAddSpinner")!;
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

        // Step 3 (Feats)
        _featAllocationLabel = this.FindControl<TextBlock>("FeatAllocationLabel")!;
        _featSearchBox = this.FindControl<TextBox>("FeatSearchBox")!;
        _availableFeatsListBox = this.FindControl<ListBox>("AvailableFeatsListBox")!;
        _selectedFeatsListBox = this.FindControl<ListBox>("SelectedFeatsListBox")!;
        _selectedFeatsHeader = this.FindControl<TextBlock>("SelectedFeatsHeader")!;
        _addFeatButton = this.FindControl<Button>("AddFeatButton")!;
        _removeFeatButton = this.FindControl<Button>("RemoveFeatButton")!;
        _featDescriptionLabel = this.FindControl<TextBlock>("FeatDescriptionLabel")!;

        // Step 4 (Skills)
        _skillPointsRemainingLabel = this.FindControl<TextBlock>("SkillPointsRemainingLabel")!;
        _skillPointsTotalLabel = this.FindControl<TextBlock>("SkillPointsTotalLabel")!;
        _skillsItemsControl = this.FindControl<ItemsControl>("SkillsItemsControl")!;
        _skillSearchBox = this.FindControl<TextBox>("SkillSearchBox")!;

        // Step 5 (Spells)
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

        // Step 6 (Summary)
        _summaryClassLabel = this.FindControl<TextBlock>("SummaryClassLabel")!;
        _summaryAbilityPanel = this.FindControl<Border>("SummaryAbilityPanel")!;
        _summaryAbilityLabel = this.FindControl<TextBlock>("SummaryAbilityLabel")!;
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

        // Consolidated level-up presets (#1645)
        _presetClassId = presetClassId;
        _presetLevels = Math.Max(1, presetLevels);
        _levelsToAddSpinner.Value = _presetLevels;
        _levelsToAddSpinner.ValueChanged += OnLevelsToAddChanged;

        InitializeWizard();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <summary>
    /// Shows prestige class prerequisite hints on feat/skill steps (#1644).
    /// Only shows when the creature is actively working toward a prestige class
    /// (has one in its class list that's below max level).
    /// </summary>
    private void UpdatePrestigeHints()
    {
        _prestigeHintsLabel.IsVisible = false;

        // Only show on feat (3) and skill (4) steps
        if (_currentStep != 3 && _currentStep != 4)
            return;

        // Only show if creature has a prestige class in progress
        bool hasPrestigeInProgress = _creature.ClassList.Any(c =>
            _displayService.Classes.IsPrestigeClass(c.Class) && c.ClassLevel < 10);
        if (!hasPrestigeInProgress)
            return;

        var hints = _displayService.Classes.GetNearQualifyingPrestigeHints(_creature);
        if (hints.Count == 0)
            return;

        _prestigeHintsLabel.Text = "Prestige goals: " + string.Join(" | ", hints.Select(h => h.Summary));
        _prestigeHintsLabel.IsVisible = true;
    }

    private void OnLevelsToAddChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        _levelsToAdd = (int)(e.NewValue ?? 1);
        if (_selectedClassId >= 0)
        {
            var existingClass = _creature.ClassList.FirstOrDefault(c => c.Class == _selectedClassId);
            int currentClassLevel = existingClass?.ClassLevel ?? 0;
            _newClassLevel = currentClassLevel + _levelsToAdd;
            _fromClassLevel = currentClassLevel + 1;
        }
        UpdateSidebarSummaries();

        int currentLevel = _creature.ClassList.Sum(c => c.ClassLevel);
        _characterLevelLabel.Text = _levelsToAdd > 1
            ? $"Level {currentLevel} -> {currentLevel + _levelsToAdd}"
            : $"Level {currentLevel} -> {currentLevel + 1}";
    }

    private void InitializeWizard()
    {
        if (_creature == null || _displayService == null)
            return;

        // Set character info
        var fullName = CreatureDisplayService.GetCreatureFullName(_creature);
        _characterNameLabel.Text = fullName;

        int currentLevel = _creature.ClassList.Sum(c => c.ClassLevel);
        _levelsToAdd = _presetLevels;
        _characterLevelLabel.Text = _levelsToAdd > 1
            ? $"Level {currentLevel} -> {currentLevel + _levelsToAdd}"
            : $"Level {currentLevel} -> {currentLevel + 1}";

        // Load classes for Step 1
        LoadClassList();

        // Auto-select preset class if provided (#1645)
        if (_presetClassId >= 0)
        {
            var presetItem = _filteredClasses.FirstOrDefault(c => c.ClassId == _presetClassId);
            if (presetItem != null)
            {
                _classListBox.SelectedItem = presetItem;
                if (_presetLevels > 1)
                    _levelsToAddSpinner.IsEnabled = false;
            }
        }

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

        // Update sidebar summaries (#1502)
        UpdateSidebarSummaries();

        // Show prestige class hints on feat/skill steps (#1644)
        UpdatePrestigeHints();

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
        // Check if selected class is qualified (for warning display)
        bool classIsQualified = true;
        if (_currentStep == 1 && _selectedClassId >= 0)
        {
            var selectedClass = _filteredClasses?.FirstOrDefault(c => c.ClassId == _selectedClassId);
            classIsQualified = selectedClass?.CanSelect ?? true;
        }

        bool strictValid = _currentStep switch
        {
            1 => _selectedClassId >= 0 && classIsQualified,
            2 => _abilityIncrements.Sum() >= (_abilityIncreaseLevels.Count > 0 ? _abilityIncreaseLevels.Count : 1),
            3 => _selectedFeats.Count >= _featsToSelect,
            4 => GetRemainingSkillPoints() == 0,
            5 => _isDivineCaster || IsSpellSelectionComplete(),
            6 => true,
            _ => false
        };

        bool canProceed = _validationLevel switch
        {
            ValidationLevel.None => _currentStep switch
            {
                1 => _selectedClassId >= 0,
                2 => true, // Allow skipping ability selection in CE mode
                _ => true
            },
            ValidationLevel.Warning => _currentStep switch
            {
                1 => _selectedClassId >= 0, // Must select something, but warn if unqualified
                2 => true,
                _ => true
            },
            _ => strictValid
        };

        _nextButton.IsEnabled = canProceed;
        _finishButton.IsEnabled = canProceed;

        // Status messages
        if (_validationLevel == ValidationLevel.Warning && !strictValid)
        {
            _statusLabel.Foreground = BrushManager.GetWarningBrush(this);
            _statusLabel.Text = _currentStep switch
            {
                1 when !classIsQualified => "⚠ Selected class does not meet prerequisites.",
                2 => "⚠ No ability score selected.",
                3 => $"⚠ {_featsToSelect - _selectedFeats.Count} feat(s) not selected.",
                4 => $"⚠ {GetRemainingSkillPoints()} skill point(s) unspent.",
                5 when !_isDivineCaster => "⚠ Spell selection incomplete.",
                _ => ""
            };
        }
        else if (!canProceed)
        {
            _statusLabel.ClearValue(Avalonia.Controls.TextBlock.ForegroundProperty);
            _statusLabel.Text = _currentStep switch
            {
                1 => "Select a class to continue.",
                2 => "Select an ability score to increase.",
                3 => $"Select {_featsToSelect - _selectedFeats.Count} more feat(s).",
                4 => $"Allocate {GetRemainingSkillPoints()} remaining skill point(s).",
                5 when !_isDivineCaster => "Select spells for each spell level.",
                _ => ""
            };
        }
        else
        {
            _statusLabel.ClearValue(Avalonia.Controls.TextBlock.ForegroundProperty);
            _statusLabel.Text = "";
        }
    }

    private void OnBackClick(object? sender, RoutedEventArgs e)
    {
        if (_currentStep > 1)
        {
            // Unapply projected ability increments when going back to step 2 or earlier (#1737)
            if (_currentStep == 3)
                UnapplyAbilityIncrementsFromCreature();

            _currentStep--;

            // Skip steps that don't apply
            if (_currentStep == 5 && !_needsSpellSelection)
                _currentStep--;
            if (_currentStep == 3 && _featsToSelect == 0 && _validationLevel != ValidationLevel.None)
                _currentStep--;
            if (_currentStep == 2 && !_needsAbilityIncrease)
            {
                UnapplyAbilityIncrementsFromCreature();
                _currentStep--;
            }

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
                PrepareStep2_AbilityScore();
                // Skip if no ability increase at this level
                if (!_needsAbilityIncrease)
                {
                    _currentStep++;
                    PrepareCurrentStep();
                }
                break;

            case 3:
                PrepareStep3(); // Calculate feat requirements
                // Skip if no feats to select (but CE mode never skips — user can add feats freely)
                if (_featsToSelect == 0 && _validationLevel != ValidationLevel.None)
                {
                    _currentStep++;
                    PrepareCurrentStep();
                }
                break;

            case 4:
                PrepareStep4(); // Calculate skill points
                break;

            case 5:
                PrepareStep5(); // Check spell requirements
                // Skip if no spell selection needed
                if (!_needsSpellSelection)
                {
                    _currentStep++;
                    PrepareCurrentStep();
                }
                break;

            case 6:
                PrepareStep6(); // Build summary
                break;
        }
    }

    /// <summary>
    /// Updates sidebar step labels with current choices (#1502).
    /// </summary>
    private void UpdateSidebarSummaries()
    {
        // Step 1: Class (#1645 consolidated)
        if (_selectedClassId >= 0)
        {
            var className = _displayService.GetClassName(_selectedClassId);
            if (_levelsToAdd > 1)
                _step1Summary.Text = $"{className} Lvl {_fromClassLevel}-{_newClassLevel}";
            else if (_isNewClass)
                _step1Summary.Text = $"{className} (new)";
            else
                _step1Summary.Text = $"{className} Lvl {_newClassLevel}";
        }
        else
        {
            _step1Summary.Text = "";
        }

        // Step 2: Ability Score — show per-ability increments
        int totalIncs = _abilityIncrements.Sum();
        if (_needsAbilityIncrease && totalIncs > 0)
        {
            var parts = new List<string>();
            for (int i = 0; i < 6; i++)
            {
                if (_abilityIncrements[i] > 0)
                    parts.Add($"{AbilityNames[i]} +{_abilityIncrements[i]}");
            }
            _step2Summary.Text = string.Join(", ", parts);
        }
        else
        {
            _step2Summary.Text = "";
        }

        // Step 3: Feats
        if (_selectedFeats.Count > 0)
        {
            _step3Summary.Text = $"{_selectedFeats.Count} selected";
        }
        else
        {
            _step3Summary.Text = "";
        }

        // Step 4: Skills
        int totalSpent = _skillPointsAdded.Values.Sum();
        if (totalSpent > 0)
        {
            _step4Summary.Text = $"{totalSpent} pts spent";
        }
        else
        {
            _step4Summary.Text = "";
        }

        // Step 5: Spells
        int totalSpells = _selectedSpellsByLevel.Values.Sum(list => list.Count);
        if (_isDivineCaster)
        {
            _step5Summary.Text = "Auto-granted";
        }
        else if (totalSpells > 0)
        {
            _step5Summary.Text = $"{totalSpells} selected";
        }
        else
        {
            _step5Summary.Text = "";
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
        // Restore creature state in case tentative ability increments were applied (#1737)
        UnapplyAbilityIncrementsFromCreature();
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
        _creature.Str = _originalCreature.Str;
        _creature.Dex = _originalCreature.Dex;
        _creature.Con = _originalCreature.Con;
        _creature.Int = _originalCreature.Int;
        _creature.Wis = _originalCreature.Wis;
        _creature.Cha = _originalCreature.Cha;
    }

    #endregion
}
