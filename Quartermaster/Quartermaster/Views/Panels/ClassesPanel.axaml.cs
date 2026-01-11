using System;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Markup.Xaml;
using Quartermaster.Services;
using Quartermaster.Views.Dialogs;
using Radoub.Formats.Utc;
using System.Collections.ObjectModel;

namespace Quartermaster.Views.Panels;

public partial class ClassesPanel : BasePanelControl
{
    private const int MaxClassSlots = 8; // Beamdog EE supports 8 classes

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

        _classSlots.Clear();
        int totalLevel = 0;

        for (int i = 0; i < creature.ClassList.Count && i < MaxClassSlots; i++)
        {
            var creatureClass = creature.ClassList[i];
            _classSlots.Add(new ClassSlotViewModel
            {
                SlotIndex = i,
                ClassId = creatureClass.Class,
                ClassName = GetClassName(creatureClass.Class),
                Level = creatureClass.ClassLevel,
                HitDie = GetClassHitDie(creatureClass.Class),
                SkillPoints = GetClassSkillPoints(creatureClass.Class),
                ClassFeatures = GetClassFeatures(creatureClass.Class)
            });
            totalLevel += creatureClass.ClassLevel;
        }

        SetText(_totalLevelText, $"Total Level: {totalLevel}");

        if (_addClassButton != null)
            _addClassButton.IsVisible = creature.ClassList.Count < MaxClassSlots;

        if (_noClassesText != null)
            _noClassesText.IsVisible = creature.ClassList.Count == 0;

        LoadAlignment(creature.GoodEvil, creature.LawfulChaotic);

        SetText(_packageText, _displayService?.GetPackageName(creature.StartingPackage) ?? $"Package {creature.StartingPackage}");

        IsLoading = false;
    }

    public override void ClearPanel()
    {
        CurrentCreature = null;
        _classSlots.Clear();

        SetText(_totalLevelText, "Total Level: 0");

        if (_addClassButton != null)
            _addClassButton.IsVisible = true;

        if (_noClassesText != null)
            _noClassesText.IsVisible = true;

        LoadAlignment(50, 50);
        SetText(_packageText, "None");
    }

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

    private async void OnPackagePickerClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
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
}

public class ClassSlotViewModel
{
    public int SlotIndex { get; set; }
    public string SlotNumber => $"{SlotIndex + 1}.";
    public int ClassId { get; set; }
    public string ClassName { get; set; } = "";
    public int Level { get; set; }
    public string LevelDisplay => $"Lv {Level}";
    public string HitDie { get; set; } = "";
    public int SkillPoints { get; set; }
    public string ClassFeatures { get; set; } = "";

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
}
