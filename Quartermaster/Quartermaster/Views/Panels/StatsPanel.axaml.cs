using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Quartermaster.Services;
using Radoub.Formats.Uti;
using Radoub.Formats.Utc;

namespace Quartermaster.Views.Panels;

public partial class StatsPanel : UserControl
{
    private CreatureDisplayService? _displayService;
    private UtcFile? _currentCreature;
    private IEnumerable<UtiFile?>? _equippedItems;

    // Ability score controls
    private TextBlock? _strBase, _strRacial, _strTotal, _strBonus;
    private TextBlock? _dexBase, _dexRacial, _dexTotal, _dexBonus;
    private TextBlock? _conBase, _conRacial, _conTotal, _conBonus;
    private TextBlock? _intBase, _intRacial, _intTotal, _intBonus;
    private TextBlock? _wisBase, _wisRacial, _wisTotal, _wisBonus;
    private TextBlock? _chaBase, _chaRacial, _chaTotal, _chaBonus;

    // Hit points controls
    private TextBlock? _baseHpValue, _baseHpNote;
    private TextBlock? _maxHpValue, _conHpBonus;
    private TextBlock? _currentHpValue, _hpPercent;

    // Combat stats controls
    private TextBlock? _naturalAcValue, _babValue, _babBreakdown, _speedValue, _crValue;

    // Saving throws controls
    private TextBlock? _fortBase, _fortAbility, _fortTotal;
    private TextBlock? _refBase, _refAbility, _refTotal;
    private TextBlock? _willBase, _willAbility, _willTotal;

    public StatsPanel()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);

        // Ability scores
        _strBase = this.FindControl<TextBlock>("StrBase");
        _strRacial = this.FindControl<TextBlock>("StrRacial");
        _strTotal = this.FindControl<TextBlock>("StrTotal");
        _strBonus = this.FindControl<TextBlock>("StrBonus");

        _dexBase = this.FindControl<TextBlock>("DexBase");
        _dexRacial = this.FindControl<TextBlock>("DexRacial");
        _dexTotal = this.FindControl<TextBlock>("DexTotal");
        _dexBonus = this.FindControl<TextBlock>("DexBonus");

        _conBase = this.FindControl<TextBlock>("ConBase");
        _conRacial = this.FindControl<TextBlock>("ConRacial");
        _conTotal = this.FindControl<TextBlock>("ConTotal");
        _conBonus = this.FindControl<TextBlock>("ConBonus");

        _intBase = this.FindControl<TextBlock>("IntBase");
        _intRacial = this.FindControl<TextBlock>("IntRacial");
        _intTotal = this.FindControl<TextBlock>("IntTotal");
        _intBonus = this.FindControl<TextBlock>("IntBonus");

        _wisBase = this.FindControl<TextBlock>("WisBase");
        _wisRacial = this.FindControl<TextBlock>("WisRacial");
        _wisTotal = this.FindControl<TextBlock>("WisTotal");
        _wisBonus = this.FindControl<TextBlock>("WisBonus");

        _chaBase = this.FindControl<TextBlock>("ChaBase");
        _chaRacial = this.FindControl<TextBlock>("ChaRacial");
        _chaTotal = this.FindControl<TextBlock>("ChaTotal");
        _chaBonus = this.FindControl<TextBlock>("ChaBonus");

        // Hit points
        _baseHpValue = this.FindControl<TextBlock>("BaseHpValue");
        _baseHpNote = this.FindControl<TextBlock>("BaseHpNote");
        _maxHpValue = this.FindControl<TextBlock>("MaxHpValue");
        _conHpBonus = this.FindControl<TextBlock>("ConHpBonus");
        _currentHpValue = this.FindControl<TextBlock>("CurrentHpValue");
        _hpPercent = this.FindControl<TextBlock>("HpPercent");

        // Combat stats
        _naturalAcValue = this.FindControl<TextBlock>("NaturalAcValue");
        _babValue = this.FindControl<TextBlock>("BabValue");
        _babBreakdown = this.FindControl<TextBlock>("BabBreakdown");
        _speedValue = this.FindControl<TextBlock>("SpeedValue");
        _crValue = this.FindControl<TextBlock>("CrValue");

        // Saving throws
        _fortBase = this.FindControl<TextBlock>("FortBase");
        _fortAbility = this.FindControl<TextBlock>("FortAbility");
        _fortTotal = this.FindControl<TextBlock>("FortTotal");

        _refBase = this.FindControl<TextBlock>("RefBase");
        _refAbility = this.FindControl<TextBlock>("RefAbility");
        _refTotal = this.FindControl<TextBlock>("RefTotal");

        _willBase = this.FindControl<TextBlock>("WillBase");
        _willAbility = this.FindControl<TextBlock>("WillAbility");
        _willTotal = this.FindControl<TextBlock>("WillTotal");
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
        _currentCreature = creature;

        if (creature == null)
        {
            ClearStats();
            return;
        }

        // Get racial modifiers
        var racialMods = _displayService?.GetRacialModifiers(creature.Race) ?? new RacialModifiers();

        // Load ability scores with racial modifiers
        LoadAbilityScore(_strBase, _strRacial, _strTotal, _strBonus, creature.Str, racialMods.Str);
        LoadAbilityScore(_dexBase, _dexRacial, _dexTotal, _dexBonus, creature.Dex, racialMods.Dex);
        LoadAbilityScore(_conBase, _conRacial, _conTotal, _conBonus, creature.Con, racialMods.Con);
        LoadAbilityScore(_intBase, _intRacial, _intTotal, _intBonus, creature.Int, racialMods.Int);
        LoadAbilityScore(_wisBase, _wisRacial, _wisTotal, _wisBonus, creature.Wis, racialMods.Wis);
        LoadAbilityScore(_chaBase, _chaRacial, _chaTotal, _chaBonus, creature.Cha, racialMods.Cha);

        // Calculate Con bonus for HP display
        int conTotal = creature.Con + racialMods.Con;
        int conBonus = CreatureDisplayService.CalculateAbilityBonus(conTotal);
        int totalLevel = creature.ClassList.Sum(c => c.ClassLevel);
        int conHpContribution = conBonus * totalLevel;

        // Load hit points
        SetText(_baseHpValue, creature.HitPoints.ToString());
        SetText(_maxHpValue, creature.MaxHitPoints.ToString());
        SetText(_currentHpValue, creature.CurrentHitPoints.ToString());
        SetText(_conHpBonus, $"({CreatureDisplayService.FormatBonus(conHpContribution)} Con)");

        // Calculate HP percentage
        var hpPercent = creature.MaxHitPoints > 0
            ? (creature.CurrentHitPoints * 100) / creature.MaxHitPoints
            : 0;
        SetText(_hpPercent, $"({hpPercent}%)");

        // Load combat stats
        SetText(_naturalAcValue, creature.NaturalAC.ToString());
        SetText(_crValue, creature.ChallengeRating.ToString("F1"));

        // Calculate BAB from class levels + equipment
        UpdateBabDisplay();

        // Speed from WalkRate
        var speedName = GetSpeedName(creature.WalkRate);
        SetText(_speedValue, speedName);

        // Load saving throws with ability modifiers
        int dexTotal = creature.Dex + racialMods.Dex;
        int wisTotal = creature.Wis + racialMods.Wis;

        int dexBonus = CreatureDisplayService.CalculateAbilityBonus(dexTotal);
        int wisBonus = CreatureDisplayService.CalculateAbilityBonus(wisTotal);

        LoadSavingThrow(_fortBase, _fortAbility, _fortTotal, creature.FortBonus, conBonus);
        LoadSavingThrow(_refBase, _refAbility, _refTotal, creature.RefBonus, dexBonus);
        LoadSavingThrow(_willBase, _willAbility, _willTotal, creature.WillBonus, wisBonus);
    }

    /// <summary>
    /// Sets the equipped items for combat stat calculations.
    /// Call this after loading creature and populating inventory.
    /// </summary>
    public void SetEquippedItems(IEnumerable<UtiFile?> items)
    {
        _equippedItems = items;
        UpdateBabDisplay();
    }

    private void UpdateBabDisplay()
    {
        if (_currentCreature == null || _displayService == null)
        {
            SetText(_babValue, "+0");
            SetText(_babBreakdown, "");
            return;
        }

        var combatStats = _displayService.CalculateCombatStats(_currentCreature, _equippedItems);

        // Display total BAB
        SetText(_babValue, CreatureDisplayService.FormatBonus(combatStats.TotalBab));

        // Display breakdown if equipment bonus present
        if (combatStats.EquipmentBonus > 0)
        {
            SetText(_babBreakdown, $"({combatStats.BaseBab} base + {combatStats.EquipmentBonus} equip)");
        }
        else
        {
            SetText(_babBreakdown, $"(from class levels)");
        }
    }

    private void LoadAbilityScore(TextBlock? baseCtrl, TextBlock? racialCtrl, TextBlock? totalCtrl, TextBlock? bonusCtrl,
        byte baseValue, int racialMod)
    {
        int total = baseValue + racialMod;
        int bonus = CreatureDisplayService.CalculateAbilityBonus(total);

        SetText(baseCtrl, baseValue.ToString());
        SetText(racialCtrl, CreatureDisplayService.FormatBonus(racialMod));
        SetText(totalCtrl, total.ToString());
        SetText(bonusCtrl, CreatureDisplayService.FormatBonus(bonus));
    }

    private void LoadSavingThrow(TextBlock? baseCtrl, TextBlock? abilityCtrl, TextBlock? totalCtrl,
        short baseValue, int abilityBonus)
    {
        int total = baseValue + abilityBonus;

        SetText(baseCtrl, CreatureDisplayService.FormatBonus(baseValue));
        SetText(abilityCtrl, CreatureDisplayService.FormatBonus(abilityBonus));
        SetText(totalCtrl, CreatureDisplayService.FormatBonus(total));
    }

    private static string GetSpeedName(int walkRate)
    {
        // Walk rates from creaturespeed.2da
        return walkRate switch
        {
            0 => "Immobile",
            1 => "Very Slow",
            2 => "Slow",
            3 => "Normal",
            4 => "Fast",
            5 => "Very Fast",
            6 => "Default",
            7 => "DM Fast",
            _ => $"Rate {walkRate}"
        };
    }

    public void ClearStats()
    {
        // Clear ability scores
        LoadAbilityScore(_strBase, _strRacial, _strTotal, _strBonus, 10, 0);
        LoadAbilityScore(_dexBase, _dexRacial, _dexTotal, _dexBonus, 10, 0);
        LoadAbilityScore(_conBase, _conRacial, _conTotal, _conBonus, 10, 0);
        LoadAbilityScore(_intBase, _intRacial, _intTotal, _intBonus, 10, 0);
        LoadAbilityScore(_wisBase, _wisRacial, _wisTotal, _wisBonus, 10, 0);
        LoadAbilityScore(_chaBase, _chaRacial, _chaTotal, _chaBonus, 10, 0);

        // Clear hit points
        SetText(_baseHpValue, "0");
        SetText(_maxHpValue, "0");
        SetText(_currentHpValue, "0");
        SetText(_conHpBonus, "(+0 Con)");
        SetText(_hpPercent, "(0%)");

        // Clear combat stats
        SetText(_naturalAcValue, "0");
        SetText(_babValue, "+0");
        SetText(_babBreakdown, "");
        SetText(_speedValue, "Normal");
        SetText(_crValue, "0");

        // Clear saving throws
        LoadSavingThrow(_fortBase, _fortAbility, _fortTotal, 0, 0);
        LoadSavingThrow(_refBase, _refAbility, _refTotal, 0, 0);
        LoadSavingThrow(_willBase, _willAbility, _willTotal, 0, 0);
    }

    private static void SetText(TextBlock? block, string text)
    {
        if (block != null)
            block.Text = text;
    }
}
