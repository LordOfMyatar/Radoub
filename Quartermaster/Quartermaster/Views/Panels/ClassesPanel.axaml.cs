using System;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Quartermaster.Services;
using Quartermaster.Views.Dialogs;
using Radoub.Formats.Utc;
using System.Collections.ObjectModel;

namespace Quartermaster.Views.Panels;

public partial class ClassesPanel : BasePanelControl
{
    private const int MaxClassSlots = 8; // Beamdog EE supports 8 classes
    private const int MaxTotalLevel = 40; // NWN level cap

    private CreatureDisplayService? _displayService;

    private TextBlock? _totalLevelText;
    private ItemsControl? _classSlotsList;
    private Button? _addClassButton;
    private TextBlock? _noClassesText;
    private TextBlock? _alignmentName;
    private Slider? _goodEvilSlider;
    private TextBlock? _goodEvilValue;
    private Slider? _lawChaosSlider;
    private TextBlock? _lawChaosValue;
    private TextBlock? _packageText;
    private Button? _packagePickerButton;
    private Button? _levelupWizardButton;

    private ObservableCollection<ClassSlotViewModel> _classSlots = new();

    public event EventHandler? AlignmentChanged;
    public event EventHandler? PackageChanged;
    public event EventHandler? ClassesChanged;

    public ClassesPanel()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

        _totalLevelText = this.FindControl<TextBlock>("TotalLevelText");
        _classSlotsList = this.FindControl<ItemsControl>("ClassSlotsList");
        _addClassButton = this.FindControl<Button>("AddClassButton");
        _noClassesText = this.FindControl<TextBlock>("NoClassesText");
        _alignmentName = this.FindControl<TextBlock>("AlignmentName");
        _goodEvilSlider = this.FindControl<Slider>("GoodEvilSlider");
        _goodEvilValue = this.FindControl<TextBlock>("GoodEvilValue");
        _lawChaosSlider = this.FindControl<Slider>("LawChaosSlider");
        _lawChaosValue = this.FindControl<TextBlock>("LawChaosValue");
        _packageText = this.FindControl<TextBlock>("PackageText");
        _packagePickerButton = this.FindControl<Button>("PackagePickerButton");
        _levelupWizardButton = this.FindControl<Button>("LevelupWizardButton");

        if (_classSlotsList != null)
            _classSlotsList.ItemsSource = _classSlots;

        if (_goodEvilSlider != null)
            _goodEvilSlider.ValueChanged += OnAlignmentSliderChanged;
        if (_lawChaosSlider != null)
            _lawChaosSlider.ValueChanged += OnAlignmentSliderChanged;

        if (_packagePickerButton != null)
            _packagePickerButton.Click += OnPackagePickerClick;
    }

    public void SetDisplayService(CreatureDisplayService displayService)
    {
        _displayService = displayService;
    }

    public override void LoadCreature(UtcFile? creature)
    {
        IsLoading = true;
        CurrentCreature = creature;

        if (creature == null)
        {
            ClearPanel();
            IsLoading = false;
            return;
        }

        RefreshClassSlots();

        LoadAlignment(creature.GoodEvil, creature.LawfulChaotic);

        SetText(_packageText, _displayService?.GetPackageName(creature.StartingPackage) ?? $"Package {creature.StartingPackage}");

        IsLoading = false;
    }

    private void RefreshClassSlots()
    {
        if (CurrentCreature == null) return;

        int totalLevel = CalculateTotalLevel();

        // Rebuild the collection
        var newSlots = new ObservableCollection<ClassSlotViewModel>();

        for (int i = 0; i < CurrentCreature.ClassList.Count && i < MaxClassSlots; i++)
        {
            var creatureClass = CurrentCreature.ClassList[i];
            var classMaxLevel = _displayService?.GetClassMaxLevel(creatureClass.Class) ?? 0;

            var vm = new ClassSlotViewModel
            {
                SlotIndex = i,
                ClassId = creatureClass.Class,
                ClassName = GetClassName(creatureClass.Class),
                Level = creatureClass.ClassLevel,
                HitDie = GetClassHitDie(creatureClass.Class),
                SkillPoints = GetClassSkillPoints(creatureClass.Class),
                ClassFeatures = GetClassFeatures(creatureClass.Class),
                ClassMaxLevel = classMaxLevel,
                TotalLevel = totalLevel
            };

            newSlots.Add(vm);
        }

        // Replace collection and rebind to force UI refresh
        _classSlots = newSlots;
        if (_classSlotsList != null)
            _classSlotsList.ItemsSource = _classSlots;

        UpdateTotalLevelDisplay(totalLevel);

        if (_addClassButton != null)
        {
            // Can add class if: less than 8 classes AND total level < 40
            _addClassButton.IsVisible = CurrentCreature.ClassList.Count < MaxClassSlots;
            _addClassButton.IsEnabled = totalLevel < MaxTotalLevel;
            if (totalLevel >= MaxTotalLevel)
                _addClassButton.SetValue(ToolTip.TipProperty, "Cannot add class: level cap reached (40)");
            else if (CurrentCreature.ClassList.Count >= MaxClassSlots)
                _addClassButton.SetValue(ToolTip.TipProperty, "Cannot add class: maximum 8 classes");
            else
                _addClassButton.SetValue(ToolTip.TipProperty, null);
        }

        if (_noClassesText != null)
            _noClassesText.IsVisible = CurrentCreature.ClassList.Count == 0;
    }

    private int CalculateTotalLevel()
    {
        if (CurrentCreature == null) return 0;

        int total = 0;
        foreach (var c in CurrentCreature.ClassList)
            total += c.ClassLevel;
        return total;
    }

    private void UpdateTotalLevelDisplay(int totalLevel)
    {
        if (totalLevel >= MaxTotalLevel)
            SetText(_totalLevelText, $"Total Level: {totalLevel} (CAP)");
        else
            SetText(_totalLevelText, $"Total Level: {totalLevel}");
    }

    public override void ClearPanel()
    {
        CurrentCreature = null;
        _classSlots.Clear();

        SetText(_totalLevelText, "Total Level: 0");

        if (_addClassButton != null)
        {
            _addClassButton.IsVisible = true;
            _addClassButton.IsEnabled = true;
        }

        if (_noClassesText != null)
            _noClassesText.IsVisible = true;

        LoadAlignment(50, 50);
        SetText(_packageText, "None");
    }

    #region Level-Up and Add Class

    private void OnLevelUpClick(object? sender, RoutedEventArgs e)
    {
        if (IsLoading || CurrentCreature == null || _displayService == null) return;

        if (sender is Button button && button.Tag is int slotIndex)
        {
            LevelUp(slotIndex);
        }
    }

    private void LevelUp(int slotIndex)
    {
        if (CurrentCreature == null || _displayService == null) return;
        if (slotIndex < 0 || slotIndex >= CurrentCreature.ClassList.Count) return;

        var totalLevel = CalculateTotalLevel();
        if (totalLevel >= MaxTotalLevel) return; // At cap

        var creatureClass = CurrentCreature.ClassList[slotIndex];
        var classMaxLevel = _displayService.GetClassMaxLevel(creatureClass.Class);

        // Check prestige class max level
        if (classMaxLevel > 0 && creatureClass.ClassLevel >= classMaxLevel) return;

        // Increment level
        creatureClass.ClassLevel++;

        // Recalculate derived stats
        RecalculateDerivedStats();

        // Refresh UI
        RefreshClassSlots();

        // Fire change event
        ClassesChanged?.Invoke(this, EventArgs.Empty);
    }

    private async void OnAddClassClick(object? sender, RoutedEventArgs e)
    {
        if (IsLoading || CurrentCreature == null || _displayService == null) return;

        var totalLevel = CalculateTotalLevel();
        if (totalLevel >= MaxTotalLevel) return;
        if (CurrentCreature.ClassList.Count >= MaxClassSlots) return;

        var allClasses = _displayService.GetAllClasses();
        var picker = new ClassPickerWindow(allClasses);

        var parentWindow = this.VisualRoot as Window;
        if (parentWindow != null)
        {
            await picker.ShowDialog(parentWindow);
        }

        if (picker.Confirmed && picker.SelectedClassId.HasValue)
        {
            AddClass(picker.SelectedClassId.Value);
        }
    }

    private void AddClass(int classId)
    {
        if (CurrentCreature == null || _displayService == null) return;

        // Check if class already exists on creature
        foreach (var c in CurrentCreature.ClassList)
        {
            if (c.Class == classId)
            {
                // Class already exists - just level it up instead
                var existingIndex = CurrentCreature.ClassList.IndexOf(c);
                LevelUp(existingIndex);
                return;
            }
        }

        // Add new class at level 1
        var newClass = new CreatureClass
        {
            Class = classId,
            ClassLevel = 1
        };

        CurrentCreature.ClassList.Add(newClass);

        // Recalculate derived stats
        RecalculateDerivedStats();

        // Refresh UI
        RefreshClassSlots();

        // Fire change event
        ClassesChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RecalculateDerivedStats()
    {
        if (CurrentCreature == null || _displayService == null) return;

        // Recalculate BAB
        var newBab = _displayService.CalculateBaseAttackBonus(CurrentCreature);
        // BAB is not stored directly in UTC - it's calculated at runtime by the game
        // But we need to update the display in StatsPanel

        // Recalculate saves
        var newSaves = _displayService.CalculateBaseSavingThrows(CurrentCreature);
        CurrentCreature.FortBonus = (short)newSaves.Fortitude;
        CurrentCreature.RefBonus = (short)newSaves.Reflex;
        CurrentCreature.WillBonus = (short)newSaves.Will;
    }

    #endregion

    #region Alignment

    private void LoadAlignment(byte goodEvil, byte lawChaotic)
    {
        if (_goodEvilSlider != null) _goodEvilSlider.Value = goodEvil;
        SetText(_goodEvilValue, goodEvil.ToString());

        if (_lawChaosSlider != null) _lawChaosSlider.Value = lawChaotic;
        SetText(_lawChaosValue, lawChaotic.ToString());

        SetText(_alignmentName, GetAlignmentName(goodEvil, lawChaotic));
    }

    private void OnAlignmentSliderChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (IsLoading || CurrentCreature == null) return;

        var goodEvil = (byte)(_goodEvilSlider?.Value ?? 50);
        var lawChaotic = (byte)(_lawChaosSlider?.Value ?? 50);

        CurrentCreature.GoodEvil = goodEvil;
        CurrentCreature.LawfulChaotic = lawChaotic;

        SetText(_goodEvilValue, goodEvil.ToString());
        SetText(_lawChaosValue, lawChaotic.ToString());
        SetText(_alignmentName, GetAlignmentName(goodEvil, lawChaotic));

        AlignmentChanged?.Invoke(this, EventArgs.Empty);
    }

    private static string GetAlignmentName(byte goodEvil, byte lawChaotic)
    {
        var geAxis = goodEvil switch
        {
            >= 70 => "Good",
            <= 30 => "Evil",
            _ => "Neutral"
        };

        var lcAxis = lawChaotic switch
        {
            >= 70 => "Lawful",
            <= 30 => "Chaotic",
            _ => "Neutral"
        };

        if (geAxis == "Neutral" && lcAxis == "Neutral")
            return "True Neutral";

        return $"{lcAxis} {geAxis}";
    }

    #endregion

    #region Package

    private async void OnPackagePickerClick(object? sender, RoutedEventArgs e)
    {
        if (CurrentCreature == null || _displayService == null) return;

        var packages = _displayService.GetAllPackages();
        var picker = new PackagePickerWindow(packages, CurrentCreature.StartingPackage);

        var parentWindow = this.VisualRoot as Window;
        if (parentWindow != null)
        {
            await picker.ShowDialog(parentWindow);
        }

        if (picker.Confirmed && picker.SelectedPackageId.HasValue)
        {
            CurrentCreature.StartingPackage = picker.SelectedPackageId.Value;
            SetText(_packageText, _displayService.GetPackageName(picker.SelectedPackageId.Value));
            PackageChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    #endregion

    #region Helper Methods

    private string GetClassName(int classId)
    {
        return _displayService?.GetClassName(classId) ?? $"Class {classId}";
    }

    private string GetClassHitDie(int classId)
    {
        return _displayService?.GetClassHitDie(classId) ?? "d8";
    }

    private int GetClassSkillPoints(int classId)
    {
        return _displayService?.GetClassSkillPointBase(classId) ?? 2;
    }

    private static string GetClassFeatures(int classId)
    {
        // Class features are descriptive summaries not stored in 2DA.
        // This is acceptable hardcoding - it's display-only flavor text.
        return classId switch
        {
            0 => "Rage, Fast Movement",
            1 => "Bardic Music, Spells",
            2 => "Divine Spells, Turn Undead",
            3 => "Nature Spells, Wild Shape",
            4 => "Bonus Feats",
            5 => "Flurry, Unarmed Strike",
            6 => "Lay on Hands, Smite Evil",
            7 => "Dual Wield, Animal Companion",
            8 => "Sneak Attack, Evasion",
            9 => "Arcane Spells (Cha)",
            10 => "Arcane Spells (Int)",
            11 => "Hide in Plain Sight",
            12 => "Favored Enemy, Spells",
            13 => "Enchant Arrow",
            14 => "Death Attack, Sneak Attack",
            15 => "Sneak Attack, Dark Blessing",
            16 => "Lay on Hands, Divine Wrath",
            17 => "Weapon of Choice",
            18 => "Undead Graft",
            19 => "Greater Wild Shape",
            20 => "Defensive Stance",
            21 => "Dragon Abilities",
            27 => "Inspire Courage",
            _ => ""
        };
    }

    #endregion
}

public class ClassSlotViewModel : INotifyPropertyChanged
{
    private const int MaxTotalLevel = 40;

    public int SlotIndex { get; set; }
    public string SlotNumber => $"{SlotIndex + 1}.";
    public int ClassId { get; set; }
    public string ClassName { get; set; } = "";
    public int Level { get; set; }
    public string LevelDisplay => $"Lv {Level}";
    public string HitDie { get; set; } = "";
    public int SkillPoints { get; set; }
    public string ClassFeatures { get; set; } = "";

    /// <summary>
    /// Maximum level for this class (0 = no max for base classes, 10 for most prestige).
    /// </summary>
    public int ClassMaxLevel { get; set; }

    /// <summary>
    /// Current total level across all classes.
    /// </summary>
    public int TotalLevel { get; set; }

    public string ClassInfoDisplay
    {
        get
        {
            var parts = new System.Collections.Generic.List<string>();
            if (!string.IsNullOrEmpty(HitDie))
                parts.Add(HitDie);
            if (SkillPoints > 0)
                parts.Add($"{SkillPoints} skill pts/lvl");
            return string.Join(" | ", parts);
        }
    }

    /// <summary>
    /// Whether this class slot can be leveled up.
    /// </summary>
    public bool CanLevelUp
    {
        get
        {
            // Can't level up if at total level cap
            if (TotalLevel >= MaxTotalLevel) return false;

            // Can't level up if at class max level (prestige classes)
            if (ClassMaxLevel > 0 && Level >= ClassMaxLevel) return false;

            return true;
        }
    }

    /// <summary>
    /// Tooltip explaining why level-up is disabled (if applicable).
    /// </summary>
    public string LevelUpTooltip
    {
        get
        {
            if (TotalLevel >= MaxTotalLevel)
                return "Cannot level up: character at level cap (40)";
            if (ClassMaxLevel > 0 && Level >= ClassMaxLevel)
                return $"Cannot level up: {ClassName} max level is {ClassMaxLevel}";
            return $"Level up {ClassName}";
        }
    }

    /// <summary>
    /// Automation ID for the level-up button.
    /// </summary>
    public string LevelUpButtonId => $"LevelUp_Class{SlotIndex}";

    public event PropertyChangedEventHandler? PropertyChanged;
}
