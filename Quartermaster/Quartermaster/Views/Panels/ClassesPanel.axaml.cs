using System;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Markup.Xaml;
using Quartermaster.Services;
using Radoub.Formats.Utc;
using System.Collections.ObjectModel;

namespace Quartermaster.Views.Panels;

public partial class ClassesPanel : UserControl
{
    private const int MaxClassSlots = 8; // Beamdog EE supports 8 classes

    private CreatureDisplayService? _displayService;
    private UtcFile? _currentCreature;
    private bool _isLoading;

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
    private Button? _levelupWizardButton;

    private ObservableCollection<ClassSlotViewModel> _classSlots = new();

    public event EventHandler? AlignmentChanged;

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
        _levelupWizardButton = this.FindControl<Button>("LevelupWizardButton");

        if (_classSlotsList != null)
            _classSlotsList.ItemsSource = _classSlots;

        // Wire up alignment slider events
        if (_goodEvilSlider != null)
            _goodEvilSlider.ValueChanged += OnAlignmentSliderChanged;
        if (_lawChaosSlider != null)
            _lawChaosSlider.ValueChanged += OnAlignmentSliderChanged;
    }

    /// <summary>
    /// Sets the display service for 2DA/TLK lookups.
    /// </summary>
    public void SetDisplayService(CreatureDisplayService displayService)
    {
        _displayService = displayService;
    }

    public void LoadCreature(UtcFile? creature)
    {
        _isLoading = true;
        _currentCreature = creature;

        if (creature == null)
        {
            ClearPanel();
            _isLoading = false;
            return;
        }

        // Load only active classes (not empty slots)
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

        // Show/hide "Add Class" button based on whether more classes can be added
        if (_addClassButton != null)
            _addClassButton.IsVisible = creature.ClassList.Count < MaxClassSlots;

        // Show "No classes" message if empty
        if (_noClassesText != null)
            _noClassesText.IsVisible = creature.ClassList.Count == 0;

        // Load alignment
        LoadAlignment(creature.GoodEvil, creature.LawfulChaotic);

        // Load auto-levelup package
        SetText(_packageText, GetPackageName(creature.StartingPackage));

        _isLoading = false;
    }

    public void ClearPanel()
    {
        _currentCreature = null;
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
        if (_isLoading || _currentCreature == null) return;

        var goodEvil = (byte)(_goodEvilSlider?.Value ?? 50);
        var lawChaotic = (byte)(_lawChaosSlider?.Value ?? 50);

        // Update creature
        _currentCreature.GoodEvil = goodEvil;
        _currentCreature.LawfulChaotic = lawChaotic;

        // Update display
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

    private string GetClassName(int classId)
    {
        // Use display service if available
        if (_displayService != null)
            return _displayService.GetClassName(classId);

        // Fallback to hardcoded names
        return classId switch
        {
            0 => "Barbarian",
            1 => "Bard",
            2 => "Cleric",
            3 => "Druid",
            4 => "Fighter",
            5 => "Monk",
            6 => "Paladin",
            7 => "Ranger",
            8 => "Rogue",
            9 => "Sorcerer",
            10 => "Wizard",
            11 => "Shadowdancer",
            12 => "Harper Scout",
            13 => "Arcane Archer",
            14 => "Assassin",
            15 => "Blackguard",
            16 => "Champion of Torm",
            17 => "Weapon Master",
            18 => "Pale Master",
            19 => "Shifter",
            20 => "Dwarven Defender",
            21 => "Dragon Disciple",
            27 => "Purple Dragon Knight",
            _ => $"Class {classId}"
        };
    }

    private string GetClassHitDie(int classId)
    {
        // Get hit die from classes.2da HitDie column
        // Fallback to standard D&D values
        return classId switch
        {
            0 => "d12",  // Barbarian
            1 => "d6",   // Bard
            2 => "d8",   // Cleric
            3 => "d8",   // Druid
            4 => "d10",  // Fighter
            5 => "d8",   // Monk
            6 => "d10",  // Paladin
            7 => "d10",  // Ranger (NWN uses d10, 3.5E uses d8)
            8 => "d6",   // Rogue
            9 => "d4",   // Sorcerer
            10 => "d4",  // Wizard
            11 => "d8",  // Shadowdancer
            12 => "d6",  // Harper Scout
            13 => "d8",  // Arcane Archer
            14 => "d6",  // Assassin
            15 => "d10", // Blackguard
            16 => "d10", // Champion of Torm
            17 => "d10", // Weapon Master
            18 => "d6",  // Pale Master
            19 => "d8",  // Shifter
            20 => "d10", // Dwarven Defender
            21 => "d6",  // Dragon Disciple
            27 => "d10", // Purple Dragon Knight
            _ => "d8"    // Default
        };
    }

    private int GetClassSkillPoints(int classId)
    {
        // SkillPointBase from classes.2da (before Int modifier)
        return classId switch
        {
            0 => 4,   // Barbarian
            1 => 4,   // Bard
            2 => 2,   // Cleric
            3 => 4,   // Druid
            4 => 2,   // Fighter
            5 => 4,   // Monk
            6 => 2,   // Paladin
            7 => 4,   // Ranger
            8 => 8,   // Rogue
            9 => 2,   // Sorcerer
            10 => 2,  // Wizard
            11 => 6,  // Shadowdancer
            12 => 4,  // Harper Scout
            13 => 4,  // Arcane Archer
            14 => 4,  // Assassin
            15 => 2,  // Blackguard
            16 => 2,  // Champion of Torm
            17 => 2,  // Weapon Master
            18 => 2,  // Pale Master
            19 => 4,  // Shifter
            20 => 2,  // Dwarven Defender
            21 => 2,  // Dragon Disciple
            27 => 2,  // Purple Dragon Knight
            _ => 2    // Default
        };
    }

    private string GetClassFeatures(int classId)
    {
        // Key class features - abbreviated for display
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

    private string GetPackageName(byte packageId)
    {
        // Package names from packages.2da Label column
        // These are auto-levelup presets for each class
        return packageId switch
        {
            0 => "Barbarian",
            1 => "Bard",
            2 => "Cleric",
            3 => "Druid",
            4 => "Fighter",
            5 => "Monk",
            6 => "Paladin",
            7 => "Ranger",
            8 => "Rogue",
            9 => "Sorcerer",
            10 => "Wizard",
            11 => "Shadowdancer",
            12 => "Harper Scout",
            13 => "Arcane Archer",
            14 => "Assassin",
            15 => "Blackguard",
            16 => "Champion of Torm",
            17 => "Weapon Master",
            18 => "Pale Master",
            19 => "Shifter",
            20 => "Dwarven Defender",
            21 => "Dragon Disciple",
            // Additional package variants
            100 => "Barbarian (Aggressive)",
            101 => "Bard (Performer)",
            102 => "Cleric (Divine)",
            103 => "Druid (Nature)",
            104 => "Fighter (Defender)",
            105 => "Monk (Ascetic)",
            106 => "Paladin (Holy)",
            107 => "Ranger (Archer)",
            108 => "Rogue (Thief)",
            109 => "Sorcerer (Aggressive)",
            110 => "Wizard (Arcane)",
            _ => $"Package {packageId}"
        };
    }

    private static void SetText(TextBlock? block, string text)
    {
        if (block != null)
            block.Text = text;
    }
}

/// <summary>
/// ViewModel for an active class slot.
/// </summary>
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

    /// <summary>
    /// Combined display of hit die and skill points.
    /// </summary>
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
