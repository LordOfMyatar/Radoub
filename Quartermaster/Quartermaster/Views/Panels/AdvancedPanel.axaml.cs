using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Radoub.Formats.Utc;

namespace Quartermaster.Views.Panels;

public partial class AdvancedPanel : UserControl
{
    // Flag icons
    private TextBlock? _plotIcon;
    private TextBlock? _immortalIcon;
    private TextBlock? _noPermDeathIcon;
    private TextBlock? _isPCIcon;
    private TextBlock? _disarmableIcon;
    private TextBlock? _lootableIcon;
    private TextBlock? _interruptableIcon;

    // Behavior
    private TextBlock? _factionText;
    private TextBlock? _perceptionText;
    private TextBlock? _walkRateText;
    private TextBlock? _soundSetText;
    private TextBlock? _decayTimeText;

    // Appearance
    private TextBlock? _appearanceText;
    private TextBlock? _phenotypeText;
    private TextBlock? _portraitText;
    private TextBlock? _tailText;
    private TextBlock? _wingsText;
    private TextBlock? _bodyBagText;

    // Blueprint
    private TextBlock? _templateResRefText;
    private TextBlock? _tagText;
    private TextBlock? _commentText;

    private const string CheckedIcon = "[X]";
    private const string UncheckedIcon = "[  ]";

    public AdvancedPanel()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

        // Flag icons
        _plotIcon = this.FindControl<TextBlock>("PlotIcon");
        _immortalIcon = this.FindControl<TextBlock>("ImmortalIcon");
        _noPermDeathIcon = this.FindControl<TextBlock>("NoPermDeathIcon");
        _isPCIcon = this.FindControl<TextBlock>("IsPCIcon");
        _disarmableIcon = this.FindControl<TextBlock>("DisarmableIcon");
        _lootableIcon = this.FindControl<TextBlock>("LootableIcon");
        _interruptableIcon = this.FindControl<TextBlock>("InterruptableIcon");

        // Behavior
        _factionText = this.FindControl<TextBlock>("FactionText");
        _perceptionText = this.FindControl<TextBlock>("PerceptionText");
        _walkRateText = this.FindControl<TextBlock>("WalkRateText");
        _soundSetText = this.FindControl<TextBlock>("SoundSetText");
        _decayTimeText = this.FindControl<TextBlock>("DecayTimeText");

        // Appearance
        _appearanceText = this.FindControl<TextBlock>("AppearanceText");
        _phenotypeText = this.FindControl<TextBlock>("PhenotypeText");
        _portraitText = this.FindControl<TextBlock>("PortraitText");
        _tailText = this.FindControl<TextBlock>("TailText");
        _wingsText = this.FindControl<TextBlock>("WingsText");
        _bodyBagText = this.FindControl<TextBlock>("BodyBagText");

        // Blueprint
        _templateResRefText = this.FindControl<TextBlock>("TemplateResRefText");
        _tagText = this.FindControl<TextBlock>("TagText");
        _commentText = this.FindControl<TextBlock>("CommentText");
    }

    public void LoadCreature(UtcFile? creature)
    {
        if (creature == null)
        {
            ClearPanel();
            return;
        }

        // Flags
        SetFlag(_plotIcon, creature.Plot);
        SetFlag(_immortalIcon, creature.IsImmortal);
        SetFlag(_noPermDeathIcon, creature.NoPermDeath);
        SetFlag(_isPCIcon, creature.IsPC);
        SetFlag(_disarmableIcon, creature.Disarmable);
        SetFlag(_lootableIcon, creature.Lootable);
        SetFlag(_interruptableIcon, creature.Interruptable);

        // Behavior
        SetText(_factionText, creature.FactionID.ToString());
        SetText(_perceptionText, GetPerceptionRangeName(creature.PerceptionRange));
        SetText(_walkRateText, GetWalkRateName(creature.WalkRate));
        SetText(_soundSetText, creature.SoundSetFile.ToString());
        SetText(_decayTimeText, $"{creature.DecayTime} ms");

        // Appearance
        SetText(_appearanceText, creature.AppearanceType.ToString());
        SetText(_phenotypeText, GetPhenotypeName(creature.Phenotype));
        SetText(_portraitText, creature.PortraitId.ToString());
        SetText(_tailText, creature.Tail == 0 ? "None" : creature.Tail.ToString());
        SetText(_wingsText, creature.Wings == 0 ? "None" : creature.Wings.ToString());
        SetText(_bodyBagText, creature.BodyBag.ToString());

        // Blueprint
        SetText(_templateResRefText, string.IsNullOrEmpty(creature.TemplateResRef) ? "-" : creature.TemplateResRef);
        SetText(_tagText, string.IsNullOrEmpty(creature.Tag) ? "-" : creature.Tag);
        SetText(_commentText, string.IsNullOrEmpty(creature.Comment) ? "-" : creature.Comment);
    }

    public void ClearPanel()
    {
        // Clear all flags
        SetFlag(_plotIcon, false);
        SetFlag(_immortalIcon, false);
        SetFlag(_noPermDeathIcon, false);
        SetFlag(_isPCIcon, false);
        SetFlag(_disarmableIcon, false);
        SetFlag(_lootableIcon, false);
        SetFlag(_interruptableIcon, true); // Default

        // Clear behavior
        SetText(_factionText, "0");
        SetText(_perceptionText, "Normal");
        SetText(_walkRateText, "Normal");
        SetText(_soundSetText, "0");
        SetText(_decayTimeText, "5000 ms");

        // Clear appearance
        SetText(_appearanceText, "0");
        SetText(_phenotypeText, "Normal");
        SetText(_portraitText, "0");
        SetText(_tailText, "None");
        SetText(_wingsText, "None");
        SetText(_bodyBagText, "0");

        // Clear blueprint
        SetText(_templateResRefText, "-");
        SetText(_tagText, "-");
        SetText(_commentText, "-");
    }

    private static string GetPerceptionRangeName(byte range)
    {
        // From ranges.2da
        return range switch
        {
            9 => "Short",
            10 => "Medium",
            11 => "Normal",
            12 => "Long",
            13 => "Maximum",
            _ => $"{range}"
        };
    }

    private static string GetWalkRateName(int rate)
    {
        // From creaturespeed.2da
        return rate switch
        {
            0 => "PC",
            1 => "Immobile",
            2 => "Very Slow",
            3 => "Slow",
            4 => "Normal",
            5 => "Fast",
            6 => "Very Fast",
            7 => "Default",
            _ => $"{rate}"
        };
    }

    private static string GetPhenotypeName(int phenotype)
    {
        return phenotype switch
        {
            0 => "Normal",
            2 => "Large",
            _ => $"Phenotype {phenotype}"
        };
    }

    private void SetFlag(TextBlock? icon, bool value)
    {
        if (icon != null)
            icon.Text = value ? CheckedIcon : UncheckedIcon;
    }

    private static void SetText(TextBlock? block, string text)
    {
        if (block != null)
            block.Text = text;
    }
}
