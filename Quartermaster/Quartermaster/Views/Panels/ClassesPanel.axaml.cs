using System;
using System.Collections.Generic;
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

    // Domain UI
    private Border? _domainSection;
    private ComboBox? _domain1ComboBox;
    private ComboBox? _domain2ComboBox;
    private TextBlock? _domainInfoText;
    private List<(int Id, string Name)> _domainItems = new();
    private int _domainClassIndex = -1; // Index in ClassList of the domain-granting class

    // Familiar UI
    private Border? _familiarSection;
    private ComboBox? _familiarComboBox;
    private List<(int Id, string Name)> _familiarItems = new();

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

        // Domain controls
        _domainSection = this.FindControl<Border>("DomainSection");
        _domain1ComboBox = this.FindControl<ComboBox>("Domain1ComboBox");
        _domain2ComboBox = this.FindControl<ComboBox>("Domain2ComboBox");
        _domainInfoText = this.FindControl<TextBlock>("DomainInfoText");

        // Familiar controls
        _familiarSection = this.FindControl<Border>("FamiliarSection");
        _familiarComboBox = this.FindControl<ComboBox>("FamiliarComboBox");

        if (_classSlotsList != null)
            _classSlotsList.ItemsSource = _classSlots;

        if (_goodEvilSlider != null)
            _goodEvilSlider.ValueChanged += OnAlignmentSliderChanged;
        if (_lawChaosSlider != null)
            _lawChaosSlider.ValueChanged += OnAlignmentSliderChanged;

        if (_packagePickerButton != null)
            _packagePickerButton.Click += OnPackagePickerClick;

        if (_domain1ComboBox != null)
            _domain1ComboBox.SelectionChanged += OnDomainSelectionChanged;
        if (_domain2ComboBox != null)
            _domain2ComboBox.SelectionChanged += OnDomainSelectionChanged;

        if (_familiarComboBox != null)
            _familiarComboBox.SelectionChanged += OnFamiliarSelectionChanged;
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
        RefreshDomainSection();
        RefreshFamiliarSection();

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

        if (_domainSection != null)
            _domainSection.IsVisible = false;
        if (_familiarSection != null)
            _familiarSection.IsVisible = false;

        _domainClassIndex = -1;

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
        IsLoading = true;
        RefreshClassSlots();
        RefreshDomainSection();
        RefreshFamiliarSection();
        IsLoading = false;

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

    #region Domains

    private void RefreshDomainSection()
    {
        if (CurrentCreature == null || _displayService == null || _domainSection == null)
            return;

        _domainClassIndex = -1;

        // Find the first class that has domains
        for (int i = 0; i < CurrentCreature.ClassList.Count; i++)
        {
            if (_displayService.ClassHasDomains(CurrentCreature.ClassList[i].Class))
            {
                _domainClassIndex = i;
                break;
            }
        }

        if (_domainClassIndex < 0)
        {
            _domainSection.IsVisible = false;
            return;
        }

        _domainSection.IsVisible = true;
        PopulateDomainComboBoxes();

        // Select current domain values
        var classEntry = CurrentCreature.ClassList[_domainClassIndex];
        var d1 = classEntry.Domain1;
        var d2 = classEntry.Domain2;

        // BioWare toolset often doesn't write Domain1/Domain2 to GFF — they default to 0.
        // Infer from creature's feat list (domain powers are granted as feats).
        if (d1 == 0 && d2 == 0)
        {
            var inferred = _displayService.Domains.InferDomainsFromFeats(CurrentCreature.FeatList);
            if (inferred.Count >= 1) d1 = (byte)inferred[0];
            if (inferred.Count >= 2) d2 = (byte)inferred[1];

            // Write inferred values back to the class entry so they persist on save
            classEntry.Domain1 = d1;
            classEntry.Domain2 = d2;
        }

        SelectDomainById(_domain1ComboBox, d1);
        SelectDomainById(_domain2ComboBox, d2);

        UpdateDomainInfoDisplay();
    }

    private void PopulateDomainComboBoxes()
    {
        if (_displayService == null || _domain1ComboBox == null || _domain2ComboBox == null)
            return;

        _domainItems = _displayService.Domains.GetAllDomains();

        _domain1ComboBox.Items.Clear();
        _domain2ComboBox.Items.Clear();

        // No "(None)" option — domain 0 is Air, a valid domain.
        // Clerics always have two domains; non-clerics don't show this section at all.
        foreach (var domain in _domainItems)
        {
            _domain1ComboBox.Items.Add(new ComboBoxItem { Content = domain.Name, Tag = domain.Id });
            _domain2ComboBox.Items.Add(new ComboBoxItem { Content = domain.Name, Tag = domain.Id });
        }
    }

    private static void SelectDomainById(ComboBox? comboBox, byte domainId)
    {
        if (comboBox == null) return;

        for (int i = 0; i < comboBox.Items.Count; i++)
        {
            if (comboBox.Items[i] is ComboBoxItem item && item.Tag is int tagId && tagId == domainId)
            {
                comboBox.SelectedIndex = i;
                return;
            }
        }

        // Default to first domain if exact match not found
        if (comboBox.Items.Count > 0)
            comboBox.SelectedIndex = 0;
    }

    private static byte GetSelectedDomainId(ComboBox? comboBox)
    {
        if (comboBox?.SelectedItem is ComboBoxItem item && item.Tag is int id)
            return (byte)id;
        return 0;
    }

    private void OnDomainSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (IsLoading || CurrentCreature == null || _domainClassIndex < 0) return;

        var classEntry = CurrentCreature.ClassList[_domainClassIndex];
        classEntry.Domain1 = GetSelectedDomainId(_domain1ComboBox);
        classEntry.Domain2 = GetSelectedDomainId(_domain2ComboBox);

        UpdateDomainInfoDisplay();
        ClassesChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateDomainInfoDisplay()
    {
        if (_displayService == null || _domainInfoText == null) return;

        var d1Id = GetSelectedDomainId(_domain1ComboBox);
        var d2Id = GetSelectedDomainId(_domain2ComboBox);

        var parts = new List<string>();

        var d1 = _displayService.Domains.GetDomainInfo(d1Id);
        if (d1 != null && d1.GrantedFeatId >= 0)
            parts.Add($"{d1.Name}: {d1.GrantedFeatName}");

        var d2 = _displayService.Domains.GetDomainInfo(d2Id);
        if (d2 != null && d2.GrantedFeatId >= 0)
            parts.Add($"{d2.Name}: {d2.GrantedFeatName}");

        SetText(_domainInfoText, parts.Count > 0 ? "Granted: " + string.Join(", ", parts) : "");
    }

    #endregion

    #region Familiar

    private void RefreshFamiliarSection()
    {
        if (CurrentCreature == null || _displayService == null || _familiarSection == null)
            return;

        // Check if any class grants a familiar
        bool hasFamiliarClass = false;
        foreach (var classEntry in CurrentCreature.ClassList)
        {
            if (_displayService.ClassGrantsFamiliar(classEntry.Class))
            {
                hasFamiliarClass = true;
                break;
            }
        }

        if (!hasFamiliarClass)
        {
            _familiarSection.IsVisible = false;
            return;
        }

        _familiarSection.IsVisible = true;
        PopulateFamiliarComboBox();

        // Select current familiar type
        SelectFamiliarById(CurrentCreature.FamiliarType);
    }

    private void PopulateFamiliarComboBox()
    {
        if (_displayService == null || _familiarComboBox == null) return;

        _familiarItems = _displayService.GetAllFamiliars();
        _familiarComboBox.Items.Clear();

        foreach (var familiar in _familiarItems)
        {
            _familiarComboBox.Items.Add(new ComboBoxItem { Content = familiar.Name, Tag = familiar.Id });
        }
    }

    private void SelectFamiliarById(int familiarType)
    {
        if (_familiarComboBox == null) return;

        for (int i = 0; i < _familiarComboBox.Items.Count; i++)
        {
            if (_familiarComboBox.Items[i] is ComboBoxItem item && item.Tag is int tagId && tagId == familiarType)
            {
                _familiarComboBox.SelectedIndex = i;
                return;
            }
        }

        // Default to first
        if (_familiarComboBox.Items.Count > 0)
            _familiarComboBox.SelectedIndex = 0;
    }

    private void OnFamiliarSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (IsLoading || CurrentCreature == null) return;

        if (_familiarComboBox?.SelectedItem is ComboBoxItem item && item.Tag is int id)
        {
            CurrentCreature.FamiliarType = id;
            ClassesChanged?.Invoke(this, EventArgs.Empty);
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

    #pragma warning disable CS0067 // Event is never used - required by INotifyPropertyChanged
    public event PropertyChangedEventHandler? PropertyChanged;
    #pragma warning restore CS0067
}
