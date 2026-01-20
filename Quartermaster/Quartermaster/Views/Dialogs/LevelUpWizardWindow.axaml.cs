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
            4 => !_needsSpellSelection || _selectedSpells.Count > 0,
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
            // Perform step completion actions
            if (_currentStep == 1)
            {
                PrepareStep2(); // Calculate feat requirements
            }
            else if (_currentStep == 2)
            {
                PrepareStep3(); // Calculate skill points
            }
            else if (_currentStep == 3)
            {
                PrepareStep4(); // Check spell requirements
            }
            else if (_currentStep == 4)
            {
                PrepareStep5(); // Build summary
            }

            _currentStep++;

            // Skip steps that don't apply
            if (_currentStep == 2 && _featsToSelect == 0)
                _currentStep++;
            if (_currentStep == 4 && !_needsSpellSelection)
                _currentStep++;

            UpdateStepDisplay();
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

        // Class bonus feats (Fighter, Wizard, etc.)
        _featsToSelect += GetClassBonusFeats(_selectedClassId, _newClassLevel);

        // Human bonus feat at level 1
        if (totalLevel == 1 && _creature.Race == 6) // Human
            _featsToSelect++;

        _selectedFeats.Clear();

        _featAllocationLabel.Text = _featsToSelect > 0
            ? $"You have {_featsToSelect} feat(s) to select."
            : "No feats to select at this level.";

        // TODO: Load available feats
        _availableFeatsListBox.ItemsSource = new List<string> { "(Feat list coming soon)" };
        _selectedFeatsListBox.ItemsSource = _selectedFeats.Select(f => _displayService.GetFeatName(f));

        UpdateFeatSelectionUI();
    }

    private int GetClassBonusFeats(int classId, int classLevel)
    {
        // Check bonus feats table for this class/level
        // For now, simplified hardcoded check
        // Fighter: bonus feat at 1, 2, 4, 6, 8, 10...
        // Wizard: bonus feat at 5, 10, 15, 20
        if (classId == 4) // Fighter
            return (classLevel == 1 || classLevel % 2 == 0) ? 1 : 0;
        if (classId == 10) // Wizard
            return (classLevel % 5 == 0) ? 1 : 0;

        return 0;
    }

    private void UpdateFeatSelectionUI()
    {
        _selectedFeatsHeader.Text = $"Selected Feats ({_selectedFeats.Count}/{_featsToSelect})";
        ValidateCurrentStep();
    }

    private void OnFeatSearchChanged(object? sender, TextChangedEventArgs e)
    {
        // TODO: Filter available feats
    }

    private void OnAvailableFeatSelected(object? sender, SelectionChangedEventArgs e)
    {
        _addFeatButton.IsEnabled = _availableFeatsListBox.SelectedItem != null &&
                                   _selectedFeats.Count < _featsToSelect;
    }

    private void OnFeatDoubleClicked(object? sender, TappedEventArgs e)
    {
        // TODO: Add feat
    }

    private void OnSelectedFeatDoubleClicked(object? sender, TappedEventArgs e)
    {
        // TODO: Remove feat
    }

    private void OnAddFeatClick(object? sender, RoutedEventArgs e)
    {
        // TODO: Add selected feat
    }

    private void OnRemoveFeatClick(object? sender, RoutedEventArgs e)
    {
        // TODO: Remove selected feat
    }

    #endregion

    #region Step 3: Skill Allocation

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

        _skillPointsAdded.Clear();

        _skillPointsTotalLabel.Text = $"(Base {basePoints} + INT {intMod} = {_skillPointsToAllocate})";
        UpdateSkillPointsDisplay();

        // Build skill list
        var skills = BuildSkillList();
        _skillsItemsControl.ItemsSource = skills;
    }

    private List<SkillDisplayItem> BuildSkillList()
    {
        var skills = new List<SkillDisplayItem>();
        var classSkillIds = _displayService.GetCombinedClassSkillIds(_creature);

        // Add selected class's skills
        var newClassSkills = _displayService.GetClassSkillIds(_selectedClassId);
        foreach (var s in newClassSkills)
            classSkillIds.Add(s);

        for (int i = 0; i < 28; i++) // Standard NWN skill count
        {
            int currentRanks = i < _creature.SkillList.Count ? _creature.SkillList[i] : 0;
            bool isClassSkill = classSkillIds.Contains(i);

            skills.Add(new SkillDisplayItem
            {
                SkillId = i,
                Name = _displayService.GetSkillName(i),
                CurrentRanks = currentRanks,
                AddedRanks = _skillPointsAdded.GetValueOrDefault(i, 0),
                IsClassSkill = isClassSkill,
                MaxRanks = CalculateMaxRanks(isClassSkill)
            });
        }

        return skills;
    }

    private int CalculateMaxRanks(bool isClassSkill)
    {
        int totalLevel = _creature.ClassList.Sum(c => c.ClassLevel) + 1;
        return isClassSkill ? totalLevel + 3 : (totalLevel + 3) / 2;
    }

    private int GetRemainingSkillPoints()
    {
        int spent = _skillPointsAdded.Values.Sum();
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
            int remaining = GetRemainingSkillPoints();
            if (remaining > 0)
            {
                _skillPointsAdded[skillId] = _skillPointsAdded.GetValueOrDefault(skillId, 0) + 1;
                _skillsItemsControl.ItemsSource = BuildSkillList();
                UpdateSkillPointsDisplay();
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

    private void PrepareStep4()
    {
        // Check if this class/level grants new spells
        _needsSpellSelection = _displayService.IsCasterClass(_selectedClassId);

        // For now, simplified - just note whether spells are needed
        // Full implementation would check spell slots gained at this level
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
            int oldLevel = _creature.ClassList.First(c => c.Class == _selectedClassId).ClassLevel;
            _summaryClassLabel.Text = $"{className} {oldLevel} -> {oldLevel + 1}";
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

        public string ClassSkillIndicator => IsClassSkill ? "(class skill)" : "(cross-class)";
        public bool CanIncrease => CurrentRanks + AddedRanks < MaxRanks;
        public bool CanDecrease => AddedRanks > 0;
    }

    #endregion
}
