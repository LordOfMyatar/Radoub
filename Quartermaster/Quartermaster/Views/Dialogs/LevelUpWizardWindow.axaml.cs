using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Quartermaster.Services;
using Radoub.Formats.Utc;

namespace Quartermaster.Views.Dialogs;

/// <summary>
/// Multi-step wizard for leveling up a creature.
/// Guides through class selection, feat selection, skill allocation, and spell selection.
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
    private List<int> _selectedSpells = new();

    // Step applicability
    private int _featsToSelect;
    private int _skillPointsToAllocate;
    private bool _needsSpellSelection;

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

    public LevelUpWizardWindow() : this(null!, null!)
    {
    }

    public LevelUpWizardWindow(CreatureDisplayService displayService, UtcFile creature)
    {
        InitializeComponent();

        _displayService = displayService;
        _creature = creature;
        _originalCreature = creature; // TODO: Deep copy for cancellation

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

    private void ValidateCurrentStep()
    {
        bool canProceed = _currentStep switch
        {
            1 => _selectedClassId >= 0,
            2 => _selectedFeats.Count >= _featsToSelect,
            3 => GetRemainingSkillPoints() == 0,
            4 => true, // Spell selection is deferred - always allow proceeding
            5 => true,
            _ => false
        };

        _nextButton.IsEnabled = canProceed;
        _finishButton.IsEnabled = canProceed;

        // Update status message
        _statusLabel.Text = _currentStep switch
        {
            1 when _selectedClassId < 0 => "Select a class to continue.",
            2 when _selectedFeats.Count < _featsToSelect => $"Select {_featsToSelect - _selectedFeats.Count} more feat(s).",
            3 when GetRemainingSkillPoints() > 0 => $"Allocate {GetRemainingSkillPoints()} remaining skill point(s).",
            4 when _needsSpellSelection => "Spell selection deferred. Add spells via the Spells panel after leveling.",
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
        ApplyLevelUp();
        Confirmed = true;
        Close();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Confirmed = false;
        Close();
    }

    #endregion

    #region Step 1: Class Selection

    private void LoadClassList()
    {
        var availableClasses = _displayService.Classes.GetAvailableClasses(_creature, includeUnqualified: true);

        _allClasses = availableClasses.Select(ac => new ClassDisplayItem
        {
            ClassId = ac.ClassId,
            Name = ac.Name,
            Description = ac.Description,
            IsPrestige = ac.IsPrestige,
            HitDie = ac.HitDie,
            SkillPoints = ac.SkillPoints,
            MaxLevel = ac.MaxLevel,
            CurrentLevel = ac.CurrentLevel,
            Qualification = ac.Qualification,
            PrerequisiteResult = ac.PrerequisiteResult,
            CanSelect = ac.CanSelect
        }).ToList();

        ApplyClassFilter();
    }

    private void ApplyClassFilter()
    {
        var searchText = _classSearchBox?.Text?.Trim() ?? "";
        var showUnqualified = _showUnqualifiedPrestigeCheckBox?.IsChecked == true;

        _filteredClasses = _allClasses.Where(c =>
        {
            if (!string.IsNullOrEmpty(searchText) &&
                !c.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                return false;

            if (!showUnqualified && c.IsPrestige && !c.CanSelect)
                return false;

            return true;
        }).ToList();

        _classListBox.ItemsSource = _filteredClasses;
    }

    private void OnClassSearchChanged(object? sender, TextChangedEventArgs e)
    {
        ApplyClassFilter();
    }

    private void OnClassFilterChanged(object? sender, RoutedEventArgs e)
    {
        ApplyClassFilter();
    }

    private void OnClassSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_classListBox.SelectedItem is ClassDisplayItem item)
        {
            ShowClassDetails(item);

            if (item.CanSelect)
            {
                _selectedClassId = item.ClassId;
                _isNewClass = item.CurrentLevel == 0;
                _newClassLevel = item.CurrentLevel + 1;
            }
            else
            {
                _selectedClassId = -1;
            }
        }
        else
        {
            ClearClassDetails();
            _selectedClassId = -1;
        }

        ValidateCurrentStep();
    }

    private void ShowClassDetails(ClassDisplayItem item)
    {
        _selectedClassNameLabel.Text = item.DisplayName;
        _classStatsPanel.IsVisible = true;
        _classHitDieLabel.Text = $"d{item.HitDie}";
        _classSkillPointsLabel.Text = $"{item.SkillPoints} + INT";

        // Prerequisites
        if (item.IsPrestige && item.PrerequisiteResult != null)
        {
            _classPrereqPanel.IsVisible = true;
            _classPrereqItems.ItemsSource = BuildPrereqDisplay(item.PrerequisiteResult);
        }
        else
        {
            _classPrereqPanel.IsVisible = false;
        }

        // Description
        if (!string.IsNullOrWhiteSpace(item.Description))
        {
            _classDescriptionLabel.Text = item.Description;
            _classDescriptionLabel.Foreground = Avalonia.Media.Brushes.White;
        }
        else
        {
            _classDescriptionLabel.Text = "(No description available)";
            _classDescriptionLabel.Foreground = Avalonia.Media.Brushes.Gray;
        }
    }

    private void ClearClassDetails()
    {
        _selectedClassNameLabel.Text = "Select a class";
        _classStatsPanel.IsVisible = false;
        _classPrereqPanel.IsVisible = false;
        _classDescriptionLabel.Text = "Select a class to see its description.";
        _classDescriptionLabel.Foreground = Avalonia.Media.Brushes.Gray;
    }

    private List<PrereqDisplayItem> BuildPrereqDisplay(ClassPrereqResult result)
    {
        var items = new List<PrereqDisplayItem>();

        foreach (var (_, desc, met) in result.RequiredFeats)
        {
            items.Add(new PrereqDisplayItem { Icon = met ? "[Y]" : "[N]", Text = desc });
        }

        if (result.OrRequiredFeats.Count > 0)
        {
            bool anyMet = result.OrRequiredFeats.Any(f => f.Met);
            items.Add(new PrereqDisplayItem { Icon = anyMet ? "[Y]" : "[N]", Text = "One of:" });
            foreach (var (_, name, met) in result.OrRequiredFeats)
            {
                items.Add(new PrereqDisplayItem { Icon = met ? "  [Y]" : "  [ ]", Text = name });
            }
        }

        foreach (var (desc, met) in result.SkillRequirements)
        {
            items.Add(new PrereqDisplayItem { Icon = met ? "[Y]" : "[N]", Text = desc });
        }

        foreach (var (desc, met) in result.OtherRequirements)
        {
            items.Add(new PrereqDisplayItem { Icon = met.HasValue ? (met.Value ? "[Y]" : "[N]") : "[?]", Text = desc });
        }

        return items;
    }

    #endregion

    #region Step 2: Feat Selection

    private void PrepareStep2()
    {
        // Calculate feats to select this level
        int totalLevel = _creature.ClassList.Sum(c => c.ClassLevel) + 1;

        // Base feat at level 1, then every 3 levels (1, 3, 6, 9, 12, 15, 18, 21...)
        _featsToSelect = 0;
        if (totalLevel == 1 || totalLevel % 3 == 0)
            _featsToSelect++;

        // Class bonus feats (from cls_bfeat_*.2da)
        _featsToSelect += GetClassBonusFeats(_selectedClassId, _newClassLevel);

        // Human bonus feat at level 1
        if (totalLevel == 1 && _creature.Race == 6) // Human
            _featsToSelect++;

        _selectedFeats.Clear();

        _featAllocationLabel.Text = _featsToSelect > 0
            ? $"You have {_featsToSelect} feat(s) to select."
            : "No feats to select at this level.";

        // Build the list of available feats
        LoadAvailableFeats();
        ApplyFeatFilter();
        RefreshSelectedFeatsList();

        UpdateFeatSelectionUI();
    }

    private void LoadAvailableFeats()
    {
        _allAvailableFeats.Clear();

        // Get all feats the creature already has
        var existingFeats = new HashSet<int>(_creature.FeatList.Select(f => (int)f));

        // Add any feats we've already selected in this wizard session
        foreach (var selectedFeat in _selectedFeats)
        {
            existingFeats.Add(selectedFeat);
        }

        // Get class feat tables for the selected class and existing classes
        var classFeatIds = new HashSet<int>();
        var selectedClassFeats = GetClassSelectableFeatIds(_selectedClassId);
        foreach (var f in selectedClassFeats)
            classFeatIds.Add(f);

        foreach (var creatureClass in _creature.ClassList)
        {
            var classFeats = GetClassSelectableFeatIds(creatureClass.Class);
            foreach (var f in classFeats)
                classFeatIds.Add(f);
        }

        // Create current feats set (including tentatively selected)
        var currentFeats = new HashSet<ushort>(_creature.FeatList);
        foreach (var sf in _selectedFeats)
            currentFeats.Add((ushort)sf);

        // Get all feat IDs
        var allFeatIds = _displayService.Feats.GetAllFeatIds();

        foreach (var featId in allFeatIds)
        {
            // Skip feats the creature already has
            if (existingFeats.Contains(featId))
                continue;

            // Check if feat is available to select (universal or in class table)
            bool isUniversal = _displayService.Feats.IsFeatUniversal(featId);
            bool isClassFeat = classFeatIds.Contains(featId);

            if (!isUniversal && !isClassFeat)
                continue;

            // Get feat info
            var featInfo = _displayService.Feats.GetFeatInfo(featId);

            // Check prerequisites
            var prereqResult = _displayService.Feats.CheckFeatPrerequisites(
                _creature,
                featId,
                currentFeats,
                c => _displayService.CalculateBaseAttackBonus(c),
                cid => _displayService.GetClassName(cid));

            _allAvailableFeats.Add(new FeatDisplayItem
            {
                FeatId = featId,
                Name = featInfo.Name,
                Description = featInfo.Description,
                Category = featInfo.Category,
                MeetsPrereqs = prereqResult.AllMet,
                PrereqResult = prereqResult,
                IsClassFeat = isClassFeat && !isUniversal,
                CanSelect = prereqResult.AllMet
            });
        }

        // Sort: selectable first, then by name
        _allAvailableFeats = _allAvailableFeats
            .OrderByDescending(f => f.CanSelect)
            .ThenBy(f => f.Name)
            .ToList();
    }

    private HashSet<int> GetClassSelectableFeatIds(int classId)
    {
        var result = new HashSet<int>();
        var featTable = _displayService.GameDataService.Get2DAValue("classes", classId, "FeatsTable");
        if (string.IsNullOrEmpty(featTable) || featTable == "****")
            return result;

        for (int row = 0; row < 300; row++)
        {
            var featIndexStr = _displayService.GameDataService.Get2DAValue(featTable, row, "FeatIndex");
            if (string.IsNullOrEmpty(featIndexStr) || featIndexStr == "****")
                break;

            if (int.TryParse(featIndexStr, out int featId))
            {
                var listType = _displayService.GameDataService.Get2DAValue(featTable, row, "List");
                // List = 1: Bonus feat only, 2: Normal selectable, 3: Automatic/granted
                // We want 1 and 2 for selection
                if (listType == "1" || listType == "2")
                {
                    result.Add(featId);
                }
            }
        }

        return result;
    }

    private int GetClassBonusFeats(int classId, int classLevel)
    {
        // Read from cls_bfeat_*.2da (BonusFeatsTable)
        var bfeatTable = _displayService.GameDataService.Get2DAValue("classes", classId, "BonusFeatsTable");
        if (string.IsNullOrEmpty(bfeatTable) || bfeatTable == "****")
            return 0;

        // Check if this specific level grants a bonus feat
        var bonus = _displayService.GameDataService.Get2DAValue(bfeatTable, classLevel - 1, "Bonus");
        return bonus == "1" ? 1 : 0;
    }

    private void ApplyFeatFilter()
    {
        var searchText = _featSearchBox?.Text?.Trim() ?? "";

        _filteredAvailableFeats = _allAvailableFeats.Where(f =>
        {
            // Don't show already-selected feats in available list
            if (_selectedFeats.Contains(f.FeatId))
                return false;

            if (!string.IsNullOrEmpty(searchText) &&
                !f.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                return false;

            return true;
        }).ToList();

        _availableFeatsListBox.ItemsSource = _filteredAvailableFeats;
    }

    private void RefreshSelectedFeatsList()
    {
        var selectedItems = _selectedFeats.Select(featId =>
        {
            var item = _allAvailableFeats.FirstOrDefault(f => f.FeatId == featId);
            return item?.Name ?? _displayService.GetFeatName(featId);
        }).ToList();

        _selectedFeatsListBox.ItemsSource = selectedItems;
    }

    private void UpdateFeatSelectionUI()
    {
        _selectedFeatsHeader.Text = $"Selected Feats ({_selectedFeats.Count}/{_featsToSelect})";

        // Update button states
        var selectedItem = _availableFeatsListBox.SelectedItem as FeatDisplayItem;
        _addFeatButton.IsEnabled = selectedItem != null &&
                                   selectedItem.CanSelect &&
                                   _selectedFeats.Count < _featsToSelect;
        _removeFeatButton.IsEnabled = _selectedFeatsListBox.SelectedItem != null;

        ValidateCurrentStep();
    }

    private void OnFeatSearchChanged(object? sender, TextChangedEventArgs e)
    {
        ApplyFeatFilter();
    }

    private void OnAvailableFeatSelected(object? sender, SelectionChangedEventArgs e)
    {
        UpdateFeatSelectionUI();
    }

    private void OnFeatDoubleClicked(object? sender, TappedEventArgs e)
    {
        AddSelectedFeat();
    }

    private void OnSelectedFeatDoubleClicked(object? sender, TappedEventArgs e)
    {
        RemoveSelectedFeat();
    }

    private void OnAddFeatClick(object? sender, RoutedEventArgs e)
    {
        AddSelectedFeat();
    }

    private void OnRemoveFeatClick(object? sender, RoutedEventArgs e)
    {
        RemoveSelectedFeat();
    }

    private void AddSelectedFeat()
    {
        if (_availableFeatsListBox.SelectedItem is not FeatDisplayItem item)
            return;

        if (!item.CanSelect || _selectedFeats.Count >= _featsToSelect)
            return;

        _selectedFeats.Add(item.FeatId);

        // Re-evaluate prerequisites since selected feat may unlock others
        RefreshFeatPrerequisites();
        ApplyFeatFilter();
        RefreshSelectedFeatsList();
        UpdateFeatSelectionUI();
    }

    private void RemoveSelectedFeat()
    {
        var index = _selectedFeatsListBox.SelectedIndex;
        if (index < 0 || index >= _selectedFeats.Count)
            return;

        _selectedFeats.RemoveAt(index);

        // Re-evaluate prerequisites since removed feat may lock others
        RefreshFeatPrerequisites();
        ApplyFeatFilter();
        RefreshSelectedFeatsList();
        UpdateFeatSelectionUI();
    }

    private void RefreshFeatPrerequisites()
    {
        // Create current feats set including tentatively selected
        var currentFeats = new HashSet<ushort>(_creature.FeatList);
        foreach (var sf in _selectedFeats)
            currentFeats.Add((ushort)sf);

        // Re-check prerequisites for all feats
        foreach (var feat in _allAvailableFeats)
        {
            var prereqResult = _displayService.Feats.CheckFeatPrerequisites(
                _creature,
                feat.FeatId,
                currentFeats,
                c => _displayService.CalculateBaseAttackBonus(c),
                cid => _displayService.GetClassName(cid));

            feat.MeetsPrereqs = prereqResult.AllMet;
            feat.PrereqResult = prereqResult;
            feat.CanSelect = prereqResult.AllMet;
        }

        // Re-sort: selectable first, then by name
        _allAvailableFeats = _allAvailableFeats
            .OrderByDescending(f => f.CanSelect)
            .ThenBy(f => f.Name)
            .ToList();
    }

    #endregion

    #region Step 3: Skill Allocation

    // Tracks class skill status for the level being gained
    private HashSet<int> _classSkillIds = new();

    private void PrepareStep3()
    {
        // Calculate skill points for this level
        int basePoints = _displayService.GetClassSkillPointBase(_selectedClassId);
        int intMod = CreatureDisplayService.CalculateAbilityBonus(_creature.Int);
        _skillPointsToAllocate = Math.Max(1, basePoints + intMod);

        // First level gets 4x skill points
        int totalLevel = _creature.ClassList.Sum(c => c.ClassLevel) + 1;
        if (totalLevel == 1)
            _skillPointsToAllocate *= 4;

        // Human bonus skill points
        if (_creature.Race == 6) // Human
            _skillPointsToAllocate += totalLevel == 1 ? 4 : 1;

        _skillPointsAdded.Clear();

        // Cache class skills for the level being gained
        _classSkillIds = _displayService.GetClassSkillIds(_selectedClassId);

        _skillPointsTotalLabel.Text = $"(Base {basePoints} + INT {intMod}{(_creature.Race == 6 ? " + Human" : "")} = {_skillPointsToAllocate})";
        UpdateSkillPointsDisplay();

        // Build skill list
        var skills = BuildSkillList();
        _skillsItemsControl.ItemsSource = skills;
    }

    private List<SkillDisplayItem> BuildSkillList()
    {
        var skills = new List<SkillDisplayItem>();

        for (int i = 0; i < 28; i++) // Standard NWN skill count
        {
            int currentRanks = i < _creature.SkillList.Count ? _creature.SkillList[i] : 0;
            bool isClassSkill = _classSkillIds.Contains(i);

            skills.Add(new SkillDisplayItem
            {
                SkillId = i,
                Name = _displayService.GetSkillName(i),
                CurrentRanks = currentRanks,
                AddedRanks = _skillPointsAdded.GetValueOrDefault(i, 0),
                IsClassSkill = isClassSkill,
                MaxRanks = CalculateMaxRanks(isClassSkill),
                Cost = isClassSkill ? 1 : 2
            });
        }

        // Sort: class skills first, then by name
        return skills.OrderByDescending(s => s.IsClassSkill).ThenBy(s => s.Name).ToList();
    }

    private int CalculateMaxRanks(bool isClassSkill)
    {
        int totalLevel = _creature.ClassList.Sum(c => c.ClassLevel) + 1;
        return isClassSkill ? totalLevel + 3 : (totalLevel + 3) / 2;
    }

    private int GetRemainingSkillPoints()
    {
        int spent = 0;
        foreach (var (skillId, ranks) in _skillPointsAdded)
        {
            bool isClassSkill = _classSkillIds.Contains(skillId);
            int cost = isClassSkill ? 1 : 2;
            spent += ranks * cost;
        }
        return _skillPointsToAllocate - spent;
    }

    private void UpdateSkillPointsDisplay()
    {
        _skillPointsRemainingLabel.Text = $"Points remaining: {GetRemainingSkillPoints()}";
        ValidateCurrentStep();
    }

    private void OnSkillIncrease(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is int skillId)
        {
            bool isClassSkill = _classSkillIds.Contains(skillId);
            int cost = isClassSkill ? 1 : 2;
            int remaining = GetRemainingSkillPoints();

            if (remaining >= cost)
            {
                int currentAdded = _skillPointsAdded.GetValueOrDefault(skillId, 0);
                int currentRanks = skillId < _creature.SkillList.Count ? _creature.SkillList[skillId] : 0;
                int maxRanks = CalculateMaxRanks(isClassSkill);

                // Check if we can add another rank
                if (currentRanks + currentAdded < maxRanks)
                {
                    _skillPointsAdded[skillId] = currentAdded + 1;
                    _skillsItemsControl.ItemsSource = BuildSkillList();
                    UpdateSkillPointsDisplay();
                }
            }
        }
    }

    private void OnSkillDecrease(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is int skillId)
        {
            if (_skillPointsAdded.GetValueOrDefault(skillId, 0) > 0)
            {
                _skillPointsAdded[skillId]--;
                if (_skillPointsAdded[skillId] == 0)
                    _skillPointsAdded.Remove(skillId);
                _skillsItemsControl.ItemsSource = BuildSkillList();
                UpdateSkillPointsDisplay();
            }
        }
    }

    #endregion

    #region Step 4: Spell Selection

    private TextBlock? _spellPlaceholder;
    private int _spellsToSelect;
    private int _maxSpellLevelThisLevel;

    private void PrepareStep4()
    {
        // Get the placeholder control if not yet found
        _spellPlaceholder ??= this.FindControl<TextBlock>("SpellPlaceholder");

        // Check if this class/level grants new spells
        bool isCaster = _displayService.IsCasterClass(_selectedClassId);
        if (!isCaster)
        {
            _needsSpellSelection = false;
            if (_spellPlaceholder != null)
                _spellPlaceholder.Text = $"{_displayService.GetClassName(_selectedClassId)} is not a spellcasting class.";
            return;
        }

        // Check spell progression for this class
        bool isSpontaneous = _displayService.Spells.IsSpontaneousCaster(_selectedClassId);
        int maxSpellLevel = _displayService.Spells.GetMaxSpellLevel(_selectedClassId, _newClassLevel);
        int prevMaxSpellLevel = _newClassLevel > 1 ? _displayService.Spells.GetMaxSpellLevel(_selectedClassId, _newClassLevel - 1) : -1;

        _maxSpellLevelThisLevel = maxSpellLevel;

        if (isSpontaneous)
        {
            // For spontaneous casters (Sorcerer/Bard), check spells known
            var knownAtLevel = _displayService.Spells.GetSpellsKnownLimit(_selectedClassId, _newClassLevel);
            var knownAtPrevLevel = _newClassLevel > 1 ? _displayService.Spells.GetSpellsKnownLimit(_selectedClassId, _newClassLevel - 1) : null;

            _spellsToSelect = 0;
            if (knownAtLevel != null)
            {
                for (int i = 0; i <= 9; i++)
                {
                    int prevKnown = knownAtPrevLevel?[i] ?? 0;
                    int newKnown = knownAtLevel[i];
                    if (newKnown > prevKnown)
                        _spellsToSelect += (newKnown - prevKnown);
                }
            }

            if (_spellsToSelect > 0)
            {
                _needsSpellSelection = true;
                if (_spellPlaceholder != null)
                    _spellPlaceholder.Text = $"Spell selection for spontaneous casters is not yet implemented.\n" +
                                              $"You can learn {_spellsToSelect} new spell(s) at this level (up to level {maxSpellLevel}).\n" +
                                              $"Use the Spells panel after leveling up to add spells manually.";
            }
            else
            {
                _needsSpellSelection = false;
                if (_spellPlaceholder != null)
                    _spellPlaceholder.Text = "No new spells to learn at this level.";
            }
        }
        else
        {
            // For prepared casters (Wizard, Cleric, Druid), check if we gained a new spell level
            bool gainedNewLevel = maxSpellLevel > prevMaxSpellLevel;

            if (gainedNewLevel && maxSpellLevel > 0)
            {
                _needsSpellSelection = true;
                if (_spellPlaceholder != null)
                    _spellPlaceholder.Text = $"Spell selection for prepared casters is not yet implemented.\n" +
                                              $"You have gained access to level {maxSpellLevel} spells.\n" +
                                              $"Use the Spells panel after leveling up to prepare spells.";
            }
            else
            {
                _needsSpellSelection = false;
                if (_spellPlaceholder != null)
                    _spellPlaceholder.Text = "No new spell levels gained. Use the Spells panel to prepare different spells.";
            }
        }
    }

    #endregion

    #region Step 5: Summary

    private void PrepareStep5()
    {
        // Class summary
        var className = _displayService.GetClassName(_selectedClassId);
        if (_isNewClass)
        {
            _summaryClassLabel.Text = $"Taking level 1 in {className}";
        }
        else
        {
            var existingClass = _creature.ClassList.FirstOrDefault(c => c.Class == _selectedClassId);
            if (existingClass != null)
            {
                int oldLevel = existingClass.ClassLevel;
                _summaryClassLabel.Text = $"{className} {oldLevel} -> {oldLevel + 1}";
            }
            else
            {
                // Fallback - shouldn't happen but be safe
                _summaryClassLabel.Text = $"Taking level {_newClassLevel} in {className}";
            }
        }

        // Feats summary
        if (_selectedFeats.Count > 0)
        {
            _summaryFeatsPanel.IsVisible = true;
            _summaryFeatsList.ItemsSource = _selectedFeats.Select(f => _displayService.GetFeatName(f));
        }
        else
        {
            _summaryFeatsPanel.IsVisible = false;
        }

        // Skills summary
        var skillChanges = _skillPointsAdded
            .Where(kv => kv.Value > 0)
            .Select(kv => $"{_displayService.GetSkillName(kv.Key)} +{kv.Value}")
            .ToList();
        _summarySkillsList.ItemsSource = skillChanges.Count > 0 ? skillChanges : new[] { "(No skills allocated)" };

        // Spells summary
        if (_selectedSpells.Count > 0)
        {
            _summarySpellsPanel.IsVisible = true;
            _summarySpellsList.ItemsSource = _selectedSpells.Select(s => _displayService.GetSpellName(s));
        }
        else
        {
            _summarySpellsPanel.IsVisible = false;
        }

        // Derived stats
        int hitDie = _displayService.Classes.GetClassMetadata(_selectedClassId).HitDie;
        _summaryHpLabel.Text = $"+d{hitDie}";

        // Calculate BAB change
        int oldBab = _displayService.GetClassBab(_selectedClassId, _newClassLevel - 1);
        int newBab = _displayService.GetClassBab(_selectedClassId, _newClassLevel);
        int babChange = newBab - oldBab;
        _summaryBabLabel.Text = babChange > 0 ? $"+{babChange}" : "0";

        // Calculate save changes
        var oldSaves = _displayService.GetClassSaves(_selectedClassId, _newClassLevel - 1);
        var newSaves = _displayService.GetClassSaves(_selectedClassId, _newClassLevel);
        var saveChanges = new List<string>();
        if (newSaves.Fortitude > oldSaves.Fortitude) saveChanges.Add($"Fort +{newSaves.Fortitude - oldSaves.Fortitude}");
        if (newSaves.Reflex > oldSaves.Reflex) saveChanges.Add($"Ref +{newSaves.Reflex - oldSaves.Reflex}");
        if (newSaves.Will > oldSaves.Will) saveChanges.Add($"Will +{newSaves.Will - oldSaves.Will}");
        _summarySavesLabel.Text = saveChanges.Count > 0 ? string.Join(", ", saveChanges) : "(none)";
    }

    #endregion

    #region Apply Level-Up

    private void ApplyLevelUp()
    {
        // Find or create class entry
        var classEntry = _creature.ClassList.FirstOrDefault(c => c.Class == _selectedClassId);
        if (classEntry != null)
        {
            classEntry.ClassLevel++;
        }
        else
        {
            _creature.ClassList.Add(new CreatureClass
            {
                Class = _selectedClassId,
                ClassLevel = 1
            });
        }

        // Add feats
        foreach (var featId in _selectedFeats)
        {
            if (!_creature.FeatList.Contains((ushort)featId))
                _creature.FeatList.Add((ushort)featId);
        }

        // Add skill points
        foreach (var (skillId, points) in _skillPointsAdded)
        {
            while (_creature.SkillList.Count <= skillId)
                _creature.SkillList.Add(0);
            _creature.SkillList[skillId] = (byte)Math.Min(255, _creature.SkillList[skillId] + points);
        }

        // Add spells (TODO: implement spell addition)

        // Record level history if enabled
        RecordLevelHistory();
    }

    private void RecordLevelHistory()
    {
        var settings = SettingsService.Instance;
        if (!settings.RecordLevelHistory)
            return;

        // Build this level's record
        var record = new LevelRecord
        {
            TotalLevel = _creature.ClassList.Sum(c => c.ClassLevel),
            ClassId = _selectedClassId,
            ClassLevel = _newClassLevel,
            Feats = _selectedFeats.ToList(),
            Skills = _skillPointsAdded.Where(kv => kv.Value > 0).ToDictionary(kv => kv.Key, kv => kv.Value),
            AbilityIncrease = -1 // TODO: Track ability increases when implemented
        };

        // Get existing history or create new
        var existingHistory = LevelHistoryService.Decode(_creature.Comment) ?? new List<LevelRecord>();
        existingHistory.Add(record);

        // Encode and update comment
        _creature.Comment = LevelHistoryService.AppendToComment(
            _creature.Comment,
            existingHistory,
            settings.LevelHistoryEncoding);
    }

    #endregion

    #region Display Classes

    private class ClassDisplayItem
    {
        public int ClassId { get; set; }
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public bool IsPrestige { get; set; }
        public int HitDie { get; set; }
        public int SkillPoints { get; set; }
        public int MaxLevel { get; set; }
        public int CurrentLevel { get; set; }
        public ClassQualification Qualification { get; set; }
        public ClassPrereqResult? PrerequisiteResult { get; set; }
        public bool CanSelect { get; set; }

        public string Icon => IsPrestige ? "*" : " ";
        public string DisplayName => CurrentLevel > 0 ? $"{Name} (Lvl {CurrentLevel})" : Name;
        public string Badge => CanSelect ? "" : Qualification switch
        {
            ClassQualification.PrerequisitesNotMet => "(prereqs)",
            ClassQualification.MaxLevelReached => "(max)",
            _ => ""
        };
    }

    private class PrereqDisplayItem
    {
        public string Icon { get; set; } = "";
        public string Text { get; set; } = "";
    }

    private class SkillDisplayItem
    {
        public int SkillId { get; set; }
        public string Name { get; set; } = "";
        public int CurrentRanks { get; set; }
        public int AddedRanks { get; set; }
        public bool IsClassSkill { get; set; }
        public int MaxRanks { get; set; }
        public int Cost { get; set; } = 1;

        public string ClassSkillIndicator => IsClassSkill ? "(class skill, 1 pt)" : "(cross-class, 2 pts)";
        public bool CanIncrease => CurrentRanks + AddedRanks < MaxRanks;
        public bool CanDecrease => AddedRanks > 0;
    }

    private class FeatDisplayItem
    {
        public int FeatId { get; set; }
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public FeatCategory Category { get; set; }
        public bool MeetsPrereqs { get; set; }
        public FeatPrereqResult? PrereqResult { get; set; }
        public bool IsClassFeat { get; set; }
        public bool CanSelect { get; set; }

        public string DisplayName => Name;
        public string PrereqTooltip => PrereqResult?.GetTooltip() ?? "No prerequisites";
        public string Badge => !MeetsPrereqs ? "(prereqs)" : IsClassFeat ? "(class)" : "";
    }

    #endregion
}
