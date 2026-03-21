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
    private NumericUpDown? _goodEvilNumeric;
    private Slider? _lawChaosSlider;
    private NumericUpDown? _lawChaosNumeric;
    private TextBlock? _packageText;
    private Button? _packagePickerButton;
    private Button? _levelupWizardButton;

    // Domain UI (editable ComboBoxes)
    private Border? _domainSection;
    private ComboBox? _domain1ComboBox;
    private ComboBox? _domain2ComboBox;
    private TextBlock? _domainInfoText;
    private List<(int Id, string Name)> _domainItems = new();
    private CreatureClass? _clericClass; // Tracks the domain-bearing class entry

    // Familiar UI
    private Border? _familiarSection;
    private ComboBox? _familiarComboBox;
    private TextBox? _familiarNameTextBox;
    private List<(int Id, string Name)> _familiarItems = new();

    private ObservableCollection<ClassSlotViewModel> _classSlots = new();

    public event EventHandler? AlignmentChanged;
    public event EventHandler? PackageChanged;
    public event EventHandler? ClassesChanged;

    /// <summary>
    /// Fired when user requests a level-up via the Level Up or Add Class buttons.
    /// MainWindow handles this by launching the LUW wizard.
    /// </summary>
    public event EventHandler? LevelUpRequested;

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
        _goodEvilNumeric = this.FindControl<NumericUpDown>("GoodEvilNumeric");
        _lawChaosSlider = this.FindControl<Slider>("LawChaosSlider");
        _lawChaosNumeric = this.FindControl<NumericUpDown>("LawChaosNumeric");
        _packageText = this.FindControl<TextBlock>("PackageText");
        _packagePickerButton = this.FindControl<Button>("PackagePickerButton");
        _levelupWizardButton = this.FindControl<Button>("LevelupWizardButton");

        // Domain controls (editable)
        _domainSection = this.FindControl<Border>("DomainSection");
        _domain1ComboBox = this.FindControl<ComboBox>("Domain1ComboBox");
        _domain2ComboBox = this.FindControl<ComboBox>("Domain2ComboBox");
        _domainInfoText = this.FindControl<TextBlock>("DomainInfoText");

        // Familiar controls
        _familiarSection = this.FindControl<Border>("FamiliarSection");
        _familiarComboBox = this.FindControl<ComboBox>("FamiliarComboBox");
        _familiarNameTextBox = this.FindControl<TextBox>("FamiliarNameTextBox");

        if (_classSlotsList != null)
            _classSlotsList.ItemsSource = _classSlots;

        if (_goodEvilSlider != null)
            _goodEvilSlider.ValueChanged += OnAlignmentSliderChanged;
        if (_lawChaosSlider != null)
            _lawChaosSlider.ValueChanged += OnAlignmentSliderChanged;
        if (_goodEvilNumeric != null)
            _goodEvilNumeric.ValueChanged += OnAlignmentNumericChanged;
        if (_lawChaosNumeric != null)
            _lawChaosNumeric.ValueChanged += OnAlignmentNumericChanged;

        if (_packagePickerButton != null)
            _packagePickerButton.Click += OnPackagePickerClick;

        if (_domain1ComboBox != null)
            _domain1ComboBox.SelectionChanged += OnDomainSelectionChanged;
        if (_domain2ComboBox != null)
            _domain2ComboBox.SelectionChanged += OnDomainSelectionChanged;

        if (_familiarComboBox != null)
            _familiarComboBox.SelectionChanged += OnFamiliarSelectionChanged;
        if (_familiarNameTextBox != null)
            _familiarNameTextBox.LostFocus += OnFamiliarNameChanged;
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

        LoadAlignment(50, 50);
        SetText(_packageText, "None");
    }

    #region Level-Up and Add Class

    private void OnLevelUpClick(object? sender, RoutedEventArgs e)
    {
        if (IsLoading || CurrentCreature == null || _displayService == null) return;

        var totalLevel = CalculateTotalLevel();
        if (totalLevel >= MaxTotalLevel) return;

        // Delegate to MainWindow which launches the Level Up Wizard
        LevelUpRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnAddClassClick(object? sender, RoutedEventArgs e)
    {
        if (IsLoading || CurrentCreature == null || _displayService == null) return;

        var totalLevel = CalculateTotalLevel();
        if (totalLevel >= MaxTotalLevel) return;
        if (CurrentCreature.ClassList.Count >= MaxClassSlots) return;

        // Delegate to MainWindow which launches the Level Up Wizard
        // LUW's class selection step handles both existing and new classes
        LevelUpRequested?.Invoke(this, EventArgs.Empty);
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
        if (_goodEvilNumeric != null) _goodEvilNumeric.Value = goodEvil;

        if (_lawChaosSlider != null) _lawChaosSlider.Value = lawChaotic;
        if (_lawChaosNumeric != null) _lawChaosNumeric.Value = lawChaotic;

        SetText(_alignmentName, GetAlignmentName(goodEvil, lawChaotic));
    }

    private void OnAlignmentSliderChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (IsLoading || CurrentCreature == null) return;

        var goodEvil = (byte)(_goodEvilSlider?.Value ?? 50);
        var lawChaotic = (byte)(_lawChaosSlider?.Value ?? 50);

        CurrentCreature.GoodEvil = goodEvil;
        CurrentCreature.LawfulChaotic = lawChaotic;

        // Sync numeric inputs with slider values
        if (_goodEvilNumeric != null) _goodEvilNumeric.Value = goodEvil;
        if (_lawChaosNumeric != null) _lawChaosNumeric.Value = lawChaotic;
        SetText(_alignmentName, GetAlignmentName(goodEvil, lawChaotic));

        AlignmentChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnAlignmentNumericChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (IsLoading || CurrentCreature == null) return;

        var goodEvil = (byte)(_goodEvilNumeric?.Value ?? 50);
        var lawChaotic = (byte)(_lawChaosNumeric?.Value ?? 50);

        CurrentCreature.GoodEvil = goodEvil;
        CurrentCreature.LawfulChaotic = lawChaotic;

        // Sync sliders with numeric values
        if (_goodEvilSlider != null) _goodEvilSlider.Value = goodEvil;
        if (_lawChaosSlider != null) _lawChaosSlider.Value = lawChaotic;
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

        // Find the cleric (domain-using) class
        _clericClass = null;
        foreach (var cls in CurrentCreature.ClassList)
        {
            if (_displayService.ClassHasDomains(cls.Class))
            {
                _clericClass = cls;
                break;
            }
        }

        if (_clericClass == null)
        {
            _domainSection.IsVisible = false;
            return;
        }

        _domainSection.IsVisible = true;
        PopulateDomainComboBoxes();

        // Resolve current domains: Domain1/Domain2 if set, else infer from feats
        var (d1Id, d2Id) = _displayService.Domains.ResolveDomains(_clericClass, CurrentCreature.FeatList);

        SelectDomainInComboBox(_domain1ComboBox, d1Id >= 0 ? d1Id : 0);
        SelectDomainInComboBox(_domain2ComboBox, d2Id >= 0 ? d2Id : 0);

        UpdateDomainInfoDisplay();
    }

    private void PopulateDomainComboBoxes()
    {
        if (_displayService == null) return;

        _domainItems = _displayService.Domains.GetAllDomains();

        _domain1ComboBox?.Items.Clear();
        _domain2ComboBox?.Items.Clear();

        foreach (var domain in _domainItems)
        {
            _domain1ComboBox?.Items.Add(new ComboBoxItem { Content = domain.Name, Tag = domain.Id });
            _domain2ComboBox?.Items.Add(new ComboBoxItem { Content = domain.Name, Tag = domain.Id });
        }
    }

    private static void SelectDomainInComboBox(ComboBox? combo, int domainId)
    {
        if (combo == null) return;
        for (int i = 0; i < combo.Items.Count; i++)
        {
            if (combo.Items[i] is ComboBoxItem item && item.Tag is int id && id == domainId)
            {
                combo.SelectedIndex = i;
                return;
            }
        }
        if (combo.Items.Count > 0)
            combo.SelectedIndex = 0;
    }

    private void OnDomainSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (IsLoading || CurrentCreature == null || _displayService == null || _clericClass == null)
            return;

        var newD1Id = GetSelectedDomainId(_domain1ComboBox);
        var newD2Id = GetSelectedDomainId(_domain2ComboBox);

        var oldD1Id = _clericClass.Domain1;
        var oldD2Id = _clericClass.Domain2;

        // Update the class entry
        _clericClass.Domain1 = newD1Id;
        _clericClass.Domain2 = newD2Id;

        // Swap granted feats in FeatList
        SwapDomainFeat(oldD1Id, newD1Id);
        SwapDomainFeat(oldD2Id, newD2Id);

        UpdateDomainInfoDisplay();
        ClassesChanged?.Invoke(this, EventArgs.Empty);
    }

    private static byte GetSelectedDomainId(ComboBox? combo)
    {
        if (combo?.SelectedItem is ComboBoxItem item && item.Tag is int id)
            return (byte)id;
        return 0;
    }

    private void SwapDomainFeat(int oldDomainId, int newDomainId)
    {
        if (CurrentCreature == null || _displayService == null) return;
        if (oldDomainId == newDomainId) return;

        // Remove old domain's granted feat
        var oldFeatId = _displayService.Domains.GetGrantedFeatId(oldDomainId);
        if (oldFeatId >= 0)
        {
            CurrentCreature.FeatList.Remove((ushort)oldFeatId);
        }

        // Add new domain's granted feat (if not already present)
        var newFeatId = _displayService.Domains.GetGrantedFeatId(newDomainId);
        if (newFeatId >= 0 && !CurrentCreature.FeatList.Contains((ushort)newFeatId))
        {
            CurrentCreature.FeatList.Add((ushort)newFeatId);
        }
    }

    private void UpdateDomainInfoDisplay()
    {
        if (_displayService == null || _domainInfoText == null) return;

        var d1Id = GetSelectedDomainId(_domain1ComboBox);
        var d2Id = GetSelectedDomainId(_domain2ComboBox);
        var parts = new List<string>();

        var d1 = _displayService.Domains.GetDomainInfo(d1Id);
        if (d1?.GrantedFeatId >= 0)
            parts.Add($"{d1.Name}: {d1.GrantedFeatName}");

        var d2 = _displayService.Domains.GetDomainInfo(d2Id);
        if (d2?.GrantedFeatId >= 0)
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

        // Load familiar name
        if (_familiarNameTextBox != null)
            _familiarNameTextBox.Text = CurrentCreature.FamiliarName ?? "";
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

    private void OnFamiliarNameChanged(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (IsLoading || CurrentCreature == null || _familiarNameTextBox == null) return;

        CurrentCreature.FamiliarName = _familiarNameTextBox.Text ?? "";
        ClassesChanged?.Invoke(this, EventArgs.Empty);
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
