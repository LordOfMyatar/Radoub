using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Radoub.Formats.Utc;
using System.Collections.ObjectModel;

namespace Quartermaster.Views.Panels;

public partial class ClassesPanel : UserControl
{
    private TextBlock? _totalLevelText;
    private ItemsControl? _classesList;
    private TextBlock? _noClassesText;
    private TextBlock? _alignmentName;
    private ProgressBar? _goodEvilBar;
    private TextBlock? _goodEvilValue;
    private ProgressBar? _lawChaosBar;
    private TextBlock? _lawChaosValue;
    private TextBlock? _raceText;
    private TextBlock? _genderText;
    private TextBlock? _subraceText;
    private TextBlock? _deityText;

    private ObservableCollection<ClassViewModel> _classes = new();

    public ClassesPanel()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

        _totalLevelText = this.FindControl<TextBlock>("TotalLevelText");
        _classesList = this.FindControl<ItemsControl>("ClassesList");
        _noClassesText = this.FindControl<TextBlock>("NoClassesText");
        _alignmentName = this.FindControl<TextBlock>("AlignmentName");
        _goodEvilBar = this.FindControl<ProgressBar>("GoodEvilBar");
        _goodEvilValue = this.FindControl<TextBlock>("GoodEvilValue");
        _lawChaosBar = this.FindControl<ProgressBar>("LawChaosBar");
        _lawChaosValue = this.FindControl<TextBlock>("LawChaosValue");
        _raceText = this.FindControl<TextBlock>("RaceText");
        _genderText = this.FindControl<TextBlock>("GenderText");
        _subraceText = this.FindControl<TextBlock>("SubraceText");
        _deityText = this.FindControl<TextBlock>("DeityText");

        if (_classesList != null)
            _classesList.ItemsSource = _classes;
    }

    public void LoadCreature(UtcFile? creature)
    {
        _classes.Clear();

        if (creature == null)
        {
            ClearPanel();
            return;
        }

        // Load classes
        int totalLevel = 0;
        foreach (var creatureClass in creature.ClassList)
        {
            _classes.Add(new ClassViewModel
            {
                ClassId = creatureClass.Class,
                ClassName = GetClassName(creatureClass.Class),
                ClassDescription = $"Class ID: {creatureClass.Class}",
                Level = creatureClass.ClassLevel,
                LevelDisplay = $"Lv {creatureClass.ClassLevel}"
            });
            totalLevel += creatureClass.ClassLevel;
        }

        SetText(_totalLevelText, $"Total Level: {totalLevel}");

        if (_noClassesText != null)
            _noClassesText.IsVisible = _classes.Count == 0;

        // Load alignment
        LoadAlignment(creature.GoodEvil, creature.LawfulChaotic);

        // Load identity
        SetText(_raceText, GetRaceName(creature.Race));
        SetText(_genderText, GetGenderName(creature.Gender));
        SetText(_subraceText, string.IsNullOrEmpty(creature.Subrace) ? "-" : creature.Subrace);
        SetText(_deityText, string.IsNullOrEmpty(creature.Deity) ? "-" : creature.Deity);
    }

    public void ClearPanel()
    {
        _classes.Clear();
        SetText(_totalLevelText, "Total Level: 0");
        if (_noClassesText != null)
            _noClassesText.IsVisible = true;
        LoadAlignment(50, 50);
        SetText(_raceText, "Unknown");
        SetText(_genderText, "Unknown");
        SetText(_subraceText, "-");
        SetText(_deityText, "-");
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

    private static string GetClassName(int classId)
    {
        // TODO: Look up from classes.2da via TLK
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

    private static string GetRaceName(byte raceId)
    {
        // TODO: Look up from racialtypes.2da via TLK
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

    private static string GetGenderName(byte genderId)
    {
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

    private static void SetText(TextBlock? block, string text)
    {
        if (block != null)
            block.Text = text;
    }
}

public class ClassViewModel
{
    public int ClassId { get; set; }
    public string ClassName { get; set; } = "";
    public string ClassDescription { get; set; } = "";
    public int Level { get; set; }
    public string LevelDisplay { get; set; } = "";
}
