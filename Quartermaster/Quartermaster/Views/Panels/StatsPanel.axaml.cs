using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Radoub.Formats.Utc;

namespace Quartermaster.Views.Panels;

public partial class StatsPanel : UserControl
{
    private TextBlock? _strValue;
    private TextBlock? _dexValue;
    private TextBlock? _conValue;
    private TextBlock? _intValue;
    private TextBlock? _wisValue;
    private TextBlock? _chaValue;
    private TextBlock? _hpValue;
    private TextBlock? _acValue;
    private TextBlock? _babValue;
    private TextBlock? _crValue;
    private TextBlock? _fortValue;
    private TextBlock? _refValue;
    private TextBlock? _willValue;

    public StatsPanel()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

        _strValue = this.FindControl<TextBlock>("StrValue");
        _dexValue = this.FindControl<TextBlock>("DexValue");
        _conValue = this.FindControl<TextBlock>("ConValue");
        _intValue = this.FindControl<TextBlock>("IntValue");
        _wisValue = this.FindControl<TextBlock>("WisValue");
        _chaValue = this.FindControl<TextBlock>("ChaValue");
        _hpValue = this.FindControl<TextBlock>("HpValue");
        _acValue = this.FindControl<TextBlock>("AcValue");
        _babValue = this.FindControl<TextBlock>("BabValue");
        _crValue = this.FindControl<TextBlock>("CrValue");
        _fortValue = this.FindControl<TextBlock>("FortValue");
        _refValue = this.FindControl<TextBlock>("RefValue");
        _willValue = this.FindControl<TextBlock>("WillValue");
    }

    public void LoadCreature(UtcFile? creature)
    {
        if (creature == null)
        {
            ClearStats();
            return;
        }

        // Ability scores
        SetText(_strValue, creature.Str.ToString());
        SetText(_dexValue, creature.Dex.ToString());
        SetText(_conValue, creature.Con.ToString());
        SetText(_intValue, creature.Int.ToString());
        SetText(_wisValue, creature.Wis.ToString());
        SetText(_chaValue, creature.Cha.ToString());

        // Combat stats
        SetText(_hpValue, $"{creature.CurrentHitPoints} / {creature.MaxHitPoints}");
        SetText(_acValue, creature.NaturalAC.ToString());
        SetText(_crValue, creature.ChallengeRating.ToString("F1"));

        // Saving throws
        SetText(_fortValue, FormatBonus(creature.FortBonus));
        SetText(_refValue, FormatBonus(creature.RefBonus));
        SetText(_willValue, FormatBonus(creature.WillBonus));

        // Base attack bonus (calculated from class levels, show placeholder for now)
        SetText(_babValue, "+0");
    }

    public void ClearStats()
    {
        SetText(_strValue, "10");
        SetText(_dexValue, "10");
        SetText(_conValue, "10");
        SetText(_intValue, "10");
        SetText(_wisValue, "10");
        SetText(_chaValue, "10");
        SetText(_hpValue, "0 / 0");
        SetText(_acValue, "10");
        SetText(_babValue, "+0");
        SetText(_crValue, "0");
        SetText(_fortValue, "+0");
        SetText(_refValue, "+0");
        SetText(_willValue, "+0");
    }

    private static void SetText(TextBlock? block, string text)
    {
        if (block != null)
            block.Text = text;
    }

    private static string FormatBonus(int value)
    {
        return value >= 0 ? $"+{value}" : value.ToString();
    }
}
