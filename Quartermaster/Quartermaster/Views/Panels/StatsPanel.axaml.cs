using System;
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
    private bool _isLoading;

    public event EventHandler? CRAdjustChanged;
    public event EventHandler? AbilityScoresChanged;

    // Ability score controls - NumericUpDown for base, TextBlock for derived
    private NumericUpDown? _strBase;
    private TextBlock? _strRacial, _strTotal, _strBonus;
    private NumericUpDown? _dexBase;
    private TextBlock? _dexRacial, _dexTotal, _dexBonus;
    private NumericUpDown? _conBase;
    private TextBlock? _conRacial, _conTotal, _conBonus;
    private NumericUpDown? _intBase;
    private TextBlock? _intRacial, _intTotal, _intBonus;
    private NumericUpDown? _wisBase;
    private TextBlock? _wisRacial, _wisTotal, _wisBonus;
    private NumericUpDown? _chaBase;
    private TextBlock? _chaRacial, _chaTotal, _chaBonus;

    // Hit points controls
    private TextBlock? _baseHpValue, _baseHpNote;
    private TextBlock? _maxHpValue, _conHpBonus;
    private TextBlock? _currentHpValue, _hpPercent;

    // Combat stats controls
    private TextBlock? _naturalAcValue, _babValue, _babBreakdown, _speedValue, _crValue;
    private NumericUpDown? _crAdjustNumeric;
    private StackPanel? _crDisplaySection;

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

        // Ability scores - NumericUpDown for base values
        _strBase = this.FindControl<NumericUpDown>("StrBase");
        _strRacial = this.FindControl<TextBlock>("StrRacial");
        _strTotal = this.FindControl<TextBlock>("StrTotal");
        _strBonus = this.FindControl<TextBlock>("StrBonus");

        _dexBase = this.FindControl<NumericUpDown>("DexBase");
        _dexRacial = this.FindControl<TextBlock>("DexRacial");
        _dexTotal = this.FindControl<TextBlock>("DexTotal");
        _dexBonus = this.FindControl<TextBlock>("DexBonus");

        _conBase = this.FindControl<NumericUpDown>("ConBase");
        _conRacial = this.FindControl<TextBlock>("ConRacial");
        _conTotal = this.FindControl<TextBlock>("ConTotal");
        _conBonus = this.FindControl<TextBlock>("ConBonus");

        _intBase = this.FindControl<NumericUpDown>("IntBase");
        _intRacial = this.FindControl<TextBlock>("IntRacial");
        _intTotal = this.FindControl<TextBlock>("IntTotal");
        _intBonus = this.FindControl<TextBlock>("IntBonus");

        _wisBase = this.FindControl<NumericUpDown>("WisBase");
        _wisRacial = this.FindControl<TextBlock>("WisRacial");
        _wisTotal = this.FindControl<TextBlock>("WisTotal");
        _wisBonus = this.FindControl<TextBlock>("WisBonus");

        _chaBase = this.FindControl<NumericUpDown>("ChaBase");
        _chaRacial = this.FindControl<TextBlock>("ChaRacial");
        _chaTotal = this.FindControl<TextBlock>("ChaTotal");
        _chaBonus = this.FindControl<TextBlock>("ChaBonus");

        // Wire up ability score change events
        WireAbilityScoreEvents();

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
        _crAdjustNumeric = this.FindControl<NumericUpDown>("CRAdjustNumeric");
        _crDisplaySection = this.FindControl<StackPanel>("CrDisplaySection");

        // Wire up events
        if (_crAdjustNumeric != null)
            _crAdjustNumeric.ValueChanged += OnCRAdjustValueChanged;

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

    /// <summary>
    /// Set whether the current file is a BIC (player character) or UTC (creature blueprint).
    /// This controls visibility of UTC-only fields like Challenge Rating.
    /// </summary>
    public void SetFileType(bool isBicFile)
    {
        // Hide CR display for BIC files (player characters don't have CR)
        if (_crDisplaySection != null)
            _crDisplaySection.IsVisible = !isBicFile;
    }

    public void LoadCreature(UtcFile? creature)
    {
        _isLoading = true;
        _currentCreature = creature;

        if (creature == null)
        {
            ClearStats();
            _isLoading = false;
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
        if (_crAdjustNumeric != null)
            _crAdjustNumeric.Value = creature.CRAdjust;

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

        // Defer clearing _isLoading until after dispatcher processes queued ValueChanged events
        Avalonia.Threading.Dispatcher.UIThread.Post(() => _isLoading = false, Avalonia.Threading.DispatcherPriority.Background);
    }

    private void OnCRAdjustValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (_isLoading || _currentCreature == null) return;

        _currentCreature.CRAdjust = (int)(e.NewValue ?? 0);
        CRAdjustChanged?.Invoke(this, EventArgs.Empty);
    }

    private void WireAbilityScoreEvents()
    {
        if (_strBase != null) _strBase.ValueChanged += OnStrValueChanged;
        if (_dexBase != null) _dexBase.ValueChanged += OnDexValueChanged;
        if (_conBase != null) _conBase.ValueChanged += OnConValueChanged;
        if (_intBase != null) _intBase.ValueChanged += OnIntValueChanged;
        if (_wisBase != null) _wisBase.ValueChanged += OnWisValueChanged;
        if (_chaBase != null) _chaBase.ValueChanged += OnChaValueChanged;
    }

    private void OnStrValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (_isLoading || _currentCreature == null) return;
        _currentCreature.Str = (byte)(e.NewValue ?? 10);
        UpdateAbilityDisplay("Str", _currentCreature.Str);
        AbilityScoresChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnDexValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (_isLoading || _currentCreature == null) return;
        _currentCreature.Dex = (byte)(e.NewValue ?? 10);
        UpdateAbilityDisplay("Dex", _currentCreature.Dex);
        UpdateSavingThrows(); // Dex affects Reflex save
        AbilityScoresChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnConValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (_isLoading || _currentCreature == null) return;
        _currentCreature.Con = (byte)(e.NewValue ?? 10);
        UpdateAbilityDisplay("Con", _currentCreature.Con);
        UpdateHitPointsDisplay(); // Con affects HP
        UpdateSavingThrows(); // Con affects Fortitude save
        AbilityScoresChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnIntValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (_isLoading || _currentCreature == null) return;
        _currentCreature.Int = (byte)(e.NewValue ?? 10);
        UpdateAbilityDisplay("Int", _currentCreature.Int);
        AbilityScoresChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnWisValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (_isLoading || _currentCreature == null) return;
        _currentCreature.Wis = (byte)(e.NewValue ?? 10);
        UpdateAbilityDisplay("Wis", _currentCreature.Wis);
        UpdateSavingThrows(); // Wis affects Will save
        AbilityScoresChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnChaValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (_isLoading || _currentCreature == null) return;
        _currentCreature.Cha = (byte)(e.NewValue ?? 10);
        UpdateAbilityDisplay("Cha", _currentCreature.Cha);
        AbilityScoresChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateAbilityDisplay(string ability, byte baseValue)
    {
        if (_currentCreature == null) return;

        var racialMods = _displayService?.GetRacialModifiers(_currentCreature.Race) ?? new RacialModifiers();
        int racialMod = ability switch
        {
            "Str" => racialMods.Str,
            "Dex" => racialMods.Dex,
            "Con" => racialMods.Con,
            "Int" => racialMods.Int,
            "Wis" => racialMods.Wis,
            "Cha" => racialMods.Cha,
            _ => 0
        };

        int total = baseValue + racialMod;
        int bonus = CreatureDisplayService.CalculateAbilityBonus(total);

        var (racialCtrl, totalCtrl, bonusCtrl) = ability switch
        {
            "Str" => (_strRacial, _strTotal, _strBonus),
            "Dex" => (_dexRacial, _dexTotal, _dexBonus),
            "Con" => (_conRacial, _conTotal, _conBonus),
            "Int" => (_intRacial, _intTotal, _intBonus),
            "Wis" => (_wisRacial, _wisTotal, _wisBonus),
            "Cha" => (_chaRacial, _chaTotal, _chaBonus),
            _ => (null, null, null)
        };

        SetText(racialCtrl, CreatureDisplayService.FormatBonus(racialMod));
        SetText(totalCtrl, total.ToString());
        SetText(bonusCtrl, CreatureDisplayService.FormatBonus(bonus));
    }

    private void UpdateHitPointsDisplay()
    {
        if (_currentCreature == null) return;

        var racialMods = _displayService?.GetRacialModifiers(_currentCreature.Race) ?? new RacialModifiers();
        int conTotal = _currentCreature.Con + racialMods.Con;
        int conBonus = CreatureDisplayService.CalculateAbilityBonus(conTotal);
        int totalLevel = _currentCreature.ClassList.Sum(c => c.ClassLevel);
        int conHpContribution = conBonus * totalLevel;

        SetText(_conHpBonus, $"({CreatureDisplayService.FormatBonus(conHpContribution)} Con)");
    }

    private void UpdateSavingThrows()
    {
        if (_currentCreature == null) return;

        var racialMods = _displayService?.GetRacialModifiers(_currentCreature.Race) ?? new RacialModifiers();

        int conTotal = _currentCreature.Con + racialMods.Con;
        int dexTotal = _currentCreature.Dex + racialMods.Dex;
        int wisTotal = _currentCreature.Wis + racialMods.Wis;

        int conBonus = CreatureDisplayService.CalculateAbilityBonus(conTotal);
        int dexBonus = CreatureDisplayService.CalculateAbilityBonus(dexTotal);
        int wisBonus = CreatureDisplayService.CalculateAbilityBonus(wisTotal);

        LoadSavingThrow(_fortBase, _fortAbility, _fortTotal, _currentCreature.FortBonus, conBonus);
        LoadSavingThrow(_refBase, _refAbility, _refTotal, _currentCreature.RefBonus, dexBonus);
        LoadSavingThrow(_willBase, _willAbility, _willTotal, _currentCreature.WillBonus, wisBonus);
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

    private void LoadAbilityScore(NumericUpDown? baseCtrl, TextBlock? racialCtrl, TextBlock? totalCtrl, TextBlock? bonusCtrl,
        byte baseValue, int racialMod)
    {
        int total = baseValue + racialMod;
        int bonus = CreatureDisplayService.CalculateAbilityBonus(total);

        if (baseCtrl != null) baseCtrl.Value = baseValue;
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
            0 => "PC",
            1 => "Immobile",
            2 => "Very Slow",
            3 => "Slow",
            4 => "Normal",
            5 => "Fast",
            6 => "Very Fast",
            7 => "Default",
            8 => "DM Fast",
            _ => $"Rate {walkRate}"
        };
    }

    public void ClearStats()
    {
        _isLoading = true;

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
        if (_crAdjustNumeric != null)
            _crAdjustNumeric.Value = 0;

        // Clear saving throws
        LoadSavingThrow(_fortBase, _fortAbility, _fortTotal, 0, 0);
        LoadSavingThrow(_refBase, _refAbility, _refTotal, 0, 0);
        LoadSavingThrow(_willBase, _willAbility, _willTotal, 0, 0);

        Avalonia.Threading.Dispatcher.UIThread.Post(() => _isLoading = false, Avalonia.Threading.DispatcherPriority.Background);
    }

    private static void SetText(TextBlock? block, string text)
    {
        if (block != null)
            block.Text = text;
    }
}
