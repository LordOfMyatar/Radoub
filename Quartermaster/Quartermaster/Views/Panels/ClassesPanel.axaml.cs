using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Quartermaster.Services;
using Radoub.Formats.Utc;
using System.Collections.ObjectModel;

namespace Quartermaster.Views.Panels;

public partial class ClassesPanel : UserControl
{
    private const int MaxClassSlots = 8; // Beamdog EE supports 8 classes

    private CreatureDisplayService? _displayService;

    private TextBlock? _totalLevelText;
    private ItemsControl? _classSlotsList;
    private TextBlock? _alignmentName;
    private ProgressBar? _goodEvilBar;
    private TextBlock? _goodEvilValue;
    private ProgressBar? _lawChaosBar;
    private TextBlock? _lawChaosValue;
    private TextBlock? _raceText;
    private TextBlock? _genderText;
    private TextBlock? _subraceText;
    private TextBlock? _deityText;
    private TextBlock? _packageText;
    private Button? _levelupWizardButton;

    private ObservableCollection<ClassSlotViewModel> _classSlots = new();

    public ClassesPanel()
    {
        InitializeComponent();
        InitializeClassSlots();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

        _totalLevelText = this.FindControl<TextBlock>("TotalLevelText");
        _classSlotsList = this.FindControl<ItemsControl>("ClassSlotsList");
        _alignmentName = this.FindControl<TextBlock>("AlignmentName");
        _goodEvilBar = this.FindControl<ProgressBar>("GoodEvilBar");
        _goodEvilValue = this.FindControl<TextBlock>("GoodEvilValue");
        _lawChaosBar = this.FindControl<ProgressBar>("LawChaosBar");
        _lawChaosValue = this.FindControl<TextBlock>("LawChaosValue");
        _raceText = this.FindControl<TextBlock>("RaceText");
        _genderText = this.FindControl<TextBlock>("GenderText");
        _subraceText = this.FindControl<TextBlock>("SubraceText");
        _deityText = this.FindControl<TextBlock>("DeityText");
        _packageText = this.FindControl<TextBlock>("PackageText");
        _levelupWizardButton = this.FindControl<Button>("LevelupWizardButton");

        if (_classSlotsList != null)
            _classSlotsList.ItemsSource = _classSlots;
    }

    private void InitializeClassSlots()
    {
        _classSlots.Clear();
        for (int i = 0; i < MaxClassSlots; i++)
        {
            _classSlots.Add(new ClassSlotViewModel { SlotIndex = i });
        }
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
        if (creature == null)
        {
            ClearPanel();
            return;
        }

        // Load classes into 8 slots
        int totalLevel = 0;
        for (int i = 0; i < MaxClassSlots; i++)
        {
            var slot = _classSlots[i];
            if (i < creature.ClassList.Count)
            {
                var creatureClass = creature.ClassList[i];
                slot.ClassId = creatureClass.Class;
                slot.ClassName = GetClassName(creatureClass.Class);
                slot.Level = creatureClass.ClassLevel;
                slot.HitDie = GetClassHitDie(creatureClass.Class);
                slot.HasClass = true;
                totalLevel += creatureClass.ClassLevel;
            }
            else
            {
                slot.ClearClass();
            }
        }

        SetText(_totalLevelText, $"Total Level: {totalLevel}");

        // Load alignment
        LoadAlignment(creature.GoodEvil, creature.LawfulChaotic);

        // Load identity using display service if available
        SetText(_raceText, GetRaceName(creature.Race));
        SetText(_genderText, GetGenderName(creature.Gender));
        SetText(_subraceText, string.IsNullOrEmpty(creature.Subrace) ? "-" : creature.Subrace);
        SetText(_deityText, string.IsNullOrEmpty(creature.Deity) ? "-" : creature.Deity);

        // Load auto-levelup package
        SetText(_packageText, GetPackageName(creature.StartingPackage));
    }

    public void ClearPanel()
    {
        // Reset all class slots to empty
        foreach (var slot in _classSlots)
        {
            slot.ClearClass();
        }

        SetText(_totalLevelText, "Total Level: 0");
        LoadAlignment(50, 50);
        SetText(_raceText, "Unknown");
        SetText(_genderText, "Unknown");
        SetText(_subraceText, "-");
        SetText(_deityText, "-");
        SetText(_packageText, "None");
    }

    private void LoadAlignment(byte goodEvil, byte lawChaotic)
    {
        if (_goodEvilBar != null) _goodEvilBar.Value = goodEvil;
        SetText(_goodEvilValue, goodEvil.ToString());

        if (_lawChaosBar != null) _lawChaosBar.Value = lawChaotic;
        SetText(_lawChaosValue, lawChaotic.ToString());

        SetText(_alignmentName, GetAlignmentName(goodEvil, lawChaotic));
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

    private string GetRaceName(byte raceId)
    {
        // Use display service if available
        if (_displayService != null)
            return _displayService.GetRaceName(raceId);

        // Fallback to hardcoded names
        return raceId switch
        {
            0 => "Dwarf",
            1 => "Elf",
            2 => "Gnome",
            3 => "Halfling",
            4 => "Half-Elf",
            5 => "Half-Orc",
            6 => "Human",
            _ => $"Race {raceId}"
        };
    }

    private string GetGenderName(byte genderId)
    {
        // Use display service if available
        if (_displayService != null)
            return _displayService.GetGenderName(genderId);

        return genderId switch
        {
            0 => "Male",
            1 => "Female",
            2 => "Both",
            3 => "Other",
            4 => "None",
            _ => $"Gender {genderId}"
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
/// ViewModel for a class slot in the 8-slot class display.
/// </summary>
public class ClassSlotViewModel : System.ComponentModel.INotifyPropertyChanged
{
    private int _slotIndex;
    private int _classId;
    private string _className = "";
    private int _level;
    private string _hitDie = "";
    private bool _hasClass;

    public int SlotIndex
    {
        get => _slotIndex;
        set { _slotIndex = value; OnPropertyChanged(nameof(SlotIndex)); OnPropertyChanged(nameof(SlotNumber)); }
    }

    public string SlotNumber => $"{SlotIndex + 1}.";

    public int ClassId
    {
        get => _classId;
        set { _classId = value; OnPropertyChanged(nameof(ClassId)); }
    }

    public string ClassName
    {
        get => _className;
        set { _className = value; OnPropertyChanged(nameof(ClassName)); }
    }

    public int Level
    {
        get => _level;
        set { _level = value; OnPropertyChanged(nameof(Level)); OnPropertyChanged(nameof(LevelDisplay)); }
    }

    public string LevelDisplay => $"Lv {Level}";

    public string HitDie
    {
        get => _hitDie;
        set { _hitDie = value; OnPropertyChanged(nameof(HitDie)); OnPropertyChanged(nameof(HitDieDisplay)); }
    }

    public string HitDieDisplay => string.IsNullOrEmpty(HitDie) ? "" : $"Hit Die: {HitDie}";

    public bool HasClass
    {
        get => _hasClass;
        set { _hasClass = value; OnPropertyChanged(nameof(HasClass)); OnPropertyChanged(nameof(Opacity)); }
    }

    public double Opacity => HasClass ? 1.0 : 0.5;

    public void ClearClass()
    {
        ClassId = 0;
        ClassName = "";
        Level = 0;
        HitDie = "";
        HasClass = false;
    }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
    }
}
