using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Radoub.Formats.Utc;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Quartermaster.Views.Panels;

public partial class FeatsPanel : UserControl
{
    private TextBlock? _featsSummaryText;
    private ItemsControl? _featsList;
    private TextBlock? _noFeatsText;
    private Border? _specialAbilitiesSection;
    private ItemsControl? _specialAbilitiesList;
    private TextBlock? _noAbilitiesText;

    private ObservableCollection<FeatViewModel> _feats = new();
    private ObservableCollection<SpecialAbilityViewModel> _abilities = new();

    public FeatsPanel()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

        _featsSummaryText = this.FindControl<TextBlock>("FeatsSummaryText");
        _featsList = this.FindControl<ItemsControl>("FeatsList");
        _noFeatsText = this.FindControl<TextBlock>("NoFeatsText");
        _specialAbilitiesSection = this.FindControl<Border>("SpecialAbilitiesSection");
        _specialAbilitiesList = this.FindControl<ItemsControl>("SpecialAbilitiesList");
        _noAbilitiesText = this.FindControl<TextBlock>("NoAbilitiesText");

        if (_featsList != null)
            _featsList.ItemsSource = _feats;
        if (_specialAbilitiesList != null)
            _specialAbilitiesList.ItemsSource = _abilities;
    }

    public void LoadCreature(UtcFile? creature)
    {
        _feats.Clear();
        _abilities.Clear();

        if (creature == null)
        {
            ClearPanel();
            return;
        }

        // Load feats
        foreach (var featId in creature.FeatList.OrderBy(f => GetFeatName(f)))
        {
            _feats.Add(new FeatViewModel
            {
                FeatId = featId,
                FeatName = GetFeatName(featId),
                FeatDescription = $"Feat ID: {featId}"
            });
        }

        SetText(_featsSummaryText, $"{_feats.Count} feats");
        if (_noFeatsText != null)
            _noFeatsText.IsVisible = _feats.Count == 0;

        // Load special abilities
        foreach (var ability in creature.SpecAbilityList)
        {
            _abilities.Add(new SpecialAbilityViewModel
            {
                SpellId = ability.Spell,
                AbilityName = GetSpellName(ability.Spell),
                CasterLevel = ability.SpellCasterLevel,
                CasterLevelDisplay = $"CL {ability.SpellCasterLevel}",
                Flags = ability.SpellFlags
            });
        }

        if (_noAbilitiesText != null)
            _noAbilitiesText.IsVisible = _abilities.Count == 0;
    }

    public void ClearPanel()
    {
        _feats.Clear();
        _abilities.Clear();
        SetText(_featsSummaryText, "0 feats");
        if (_noFeatsText != null)
            _noFeatsText.IsVisible = true;
        if (_noAbilitiesText != null)
            _noAbilitiesText.IsVisible = true;
    }

    private static string GetFeatName(ushort featId)
    {
        // TODO: Look up from feat.2da via TLK
        // Common feats for display purposes
        return featId switch
        {
            0 => "Alertness",
            1 => "Ambidexterity",
            2 => "Armor Proficiency (Heavy)",
            3 => "Armor Proficiency (Light)",
            4 => "Armor Proficiency (Medium)",
            5 => "Blind-Fight",
            6 => "Called Shot",
            7 => "Cleave",
            8 => "Combat Casting",
            9 => "Deflect Arrows",
            10 => "Disarm",
            11 => "Dodge",
            12 => "Empower Spell",
            13 => "Extend Spell",
            14 => "Extra Turning",
            15 => "Great Fortitude",
            16 => "Improved Critical",
            17 => "Improved Disarm",
            18 => "Improved Initiative",
            19 => "Improved Knockdown",
            20 => "Improved Parry",
            21 => "Improved Power Attack",
            22 => "Improved Two-Weapon Fighting",
            23 => "Improved Unarmed Strike",
            24 => "Iron Will",
            25 => "Knockdown",
            26 => "Lightning Reflexes",
            27 => "Martial Weapon Proficiency",
            28 => "Maximize Spell",
            29 => "Mobility",
            30 => "Point Blank Shot",
            31 => "Power Attack",
            32 => "Quicken Spell",
            33 => "Rapid Shot",
            34 => "Sap",
            35 => "Shield Proficiency",
            36 => "Silent Spell",
            37 => "Simple Weapon Proficiency",
            38 => "Skill Focus",
            39 => "Spell Focus",
            40 => "Spell Penetration",
            41 => "Spring Attack",
            42 => "Still Spell",
            43 => "Stunning Fist",
            44 => "Toughness",
            45 => "Two-Weapon Fighting",
            46 => "Weapon Finesse",
            47 => "Weapon Focus",
            48 => "Weapon Proficiency (Creature)",
            49 => "Weapon Proficiency (Exotic)",
            50 => "Weapon Specialization",
            51 => "Whirlwind Attack",
            _ => $"Feat {featId}"
        };
    }

    private static string GetSpellName(ushort spellId)
    {
        // TODO: Look up from spells.2da via TLK
        return $"Spell {spellId}";
    }

    private static void SetText(TextBlock? block, string text)
    {
        if (block != null)
            block.Text = text;
    }
}

public class FeatViewModel
{
    public ushort FeatId { get; set; }
    public string FeatName { get; set; } = "";
    public string FeatDescription { get; set; } = "";
}

public class SpecialAbilityViewModel
{
    public ushort SpellId { get; set; }
    public string AbilityName { get; set; } = "";
    public byte CasterLevel { get; set; }
    public string CasterLevelDisplay { get; set; } = "";
    public byte Flags { get; set; }
}
