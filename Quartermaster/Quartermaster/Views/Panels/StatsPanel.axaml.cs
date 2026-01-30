using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Quartermaster.Services;
using Radoub.Formats.Uti;
using Radoub.Formats.Utc;

namespace Quartermaster.Views.Panels;

/// <summary>
/// StatsPanel - Core initialization, loading, and field definitions.
/// </summary>
public partial class StatsPanel : UserControl
{
    private CreatureDisplayService? _displayService;
    private UtcFile? _currentCreature;
    private IEnumerable<UtiFile?>? _equippedItems;
    private bool _isLoading;

    public event EventHandler? CRAdjustChanged;
    public event EventHandler? AbilityScoresChanged;
    public event EventHandler? HitPointsChanged;
    public event EventHandler? NaturalAcChanged;
    public event EventHandler? SavingThrowsChanged;

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
    private NumericUpDown? _baseHpNumeric;
    private TextBlock? _baseHpNote;
    private TextBlock? _maxHpValue, _conHpBonus;

    // Armor class controls
    private NumericUpDown? _naturalAcNumeric;
    private TextBlock? _dexAcValue, _sizeModValue, _totalAcValue;

    // Combat stats controls
    private TextBlock? _babValue, _babBreakdown;
    private TextBlock? _aprValue, _attackSequence;
    private TextBlock? _crTotalDisplay;
    private TextBlock? _crValueDisplay;
    private NumericUpDown? _crAdjustNumeric;
    private StackPanel? _crDisplaySection;

    // Saving throws controls - NumericUpDown for base, TextBlock for derived
    private NumericUpDown? _fortBase;
    private TextBlock? _fortAbility, _fortTotal;
    private NumericUpDown? _refBase;
    private TextBlock? _refAbility, _refTotal;
    private NumericUpDown? _willBase;
    private TextBlock? _willAbility, _willTotal;

    // Ability points summary
    private TextBlock? _abilityPointsSummary;

    // Expected HP summary
    private TextBlock? _expectedHpSummary;

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
        _baseHpNumeric = this.FindControl<NumericUpDown>("BaseHpNumeric");
        _baseHpNote = this.FindControl<TextBlock>("BaseHpNote");
        _maxHpValue = this.FindControl<TextBlock>("MaxHpValue");
        _conHpBonus = this.FindControl<TextBlock>("ConHpBonus");

        // Wire up hit points events
        if (_baseHpNumeric != null)
            _baseHpNumeric.ValueChanged += OnBaseHpValueChanged;

        // Armor class controls
        _naturalAcNumeric = this.FindControl<NumericUpDown>("NaturalAcNumeric");
        if (_naturalAcNumeric != null)
            _naturalAcNumeric.ValueChanged += OnNaturalAcValueChanged;
        _dexAcValue = this.FindControl<TextBlock>("DexAcValue");
        _sizeModValue = this.FindControl<TextBlock>("SizeModValue");
        _totalAcValue = this.FindControl<TextBlock>("TotalAcValue");

        // Combat stats
        _babValue = this.FindControl<TextBlock>("BabValue");
        _babBreakdown = this.FindControl<TextBlock>("BabBreakdown");
        _aprValue = this.FindControl<TextBlock>("AprValue");
        _attackSequence = this.FindControl<TextBlock>("AttackSequence");
        _crTotalDisplay = this.FindControl<TextBlock>("CrTotalDisplay");
        _crValueDisplay = this.FindControl<TextBlock>("CrValueDisplay");
        _crAdjustNumeric = this.FindControl<NumericUpDown>("CRAdjustNumeric");
        _crDisplaySection = this.FindControl<StackPanel>("CrDisplaySection");

        // Wire up events (CR display is read-only, no event needed)
        if (_crAdjustNumeric != null)
            _crAdjustNumeric.ValueChanged += OnCRAdjustValueChanged;

        // Saving throws - NumericUpDown for base values
        _fortBase = this.FindControl<NumericUpDown>("FortBase");
        _fortAbility = this.FindControl<TextBlock>("FortAbility");
        _fortTotal = this.FindControl<TextBlock>("FortTotal");

        _refBase = this.FindControl<NumericUpDown>("RefBase");
        _refAbility = this.FindControl<TextBlock>("RefAbility");
        _refTotal = this.FindControl<TextBlock>("RefTotal");

        _willBase = this.FindControl<NumericUpDown>("WillBase");
        _willAbility = this.FindControl<TextBlock>("WillAbility");
        _willTotal = this.FindControl<TextBlock>("WillTotal");

        // Wire up saving throw change events
        WireSavingThrowEvents();

        // Ability points summary
        _abilityPointsSummary = this.FindControl<TextBlock>("AbilityPointsSummary");

        // Expected HP summary
        _expectedHpSummary = this.FindControl<TextBlock>("ExpectedHpSummary");
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

        // Update ability points summary
        UpdateAbilityPointsSummary();

        // Calculate Con bonus for HP display
        int conTotal = creature.Con + racialMods.Con;
        int conBonus = CreatureDisplayService.CalculateAbilityBonus(conTotal);
        int totalLevel = creature.ClassList.Sum(c => c.ClassLevel);
        int conHpContribution = conBonus * totalLevel;

        // Load hit points
        if (_baseHpNumeric != null)
            _baseHpNumeric.Value = creature.HitPoints;
        SetText(_maxHpValue, creature.MaxHitPoints.ToString());
        SetText(_conHpBonus, CreatureDisplayService.FormatBonus(conHpContribution));

        // Update expected HP summary
        UpdateExpectedHpSummary(creature);

        // Load combat stats
        if (_naturalAcNumeric != null)
            _naturalAcNumeric.Value = creature.NaturalAC;
        if (_crValueDisplay != null)
            SetText(_crValueDisplay, creature.ChallengeRating.ToString("0.##"));
        if (_crAdjustNumeric != null)
            _crAdjustNumeric.Value = creature.CRAdjust;
        UpdateCrTotalDisplay();

        // Calculate BAB from class levels + equipment
        UpdateBabDisplay();

        // Load saving throws with ability modifiers
        int dexTotal = creature.Dex + racialMods.Dex;
        int wisTotal = creature.Wis + racialMods.Wis;

        int dexBonus = CreatureDisplayService.CalculateAbilityBonus(dexTotal);
        int wisBonus = CreatureDisplayService.CalculateAbilityBonus(wisTotal);

        // Display Dex AC bonus
        SetText(_dexAcValue, CreatureDisplayService.FormatBonus(dexBonus));

        // Display Size AC modifier (from appearance.2da SIZECATEGORY)
        int sizeAcMod = _displayService?.GetSizeAcModifier(creature.AppearanceType) ?? 0;
        SetText(_sizeModValue, CreatureDisplayService.FormatBonus(sizeAcMod));

        // Calculate and display Total AC: 10 (base) + Natural AC + Dex Bonus + Size Mod
        int totalAc = 10 + creature.NaturalAC + dexBonus + sizeAcMod;
        SetText(_totalAcValue, totalAc.ToString());

        LoadSavingThrow(_fortBase, _fortAbility, _fortTotal, creature.FortBonus, conBonus);
        LoadSavingThrow(_refBase, _refAbility, _refTotal, creature.RefBonus, dexBonus);
        LoadSavingThrow(_willBase, _willAbility, _willTotal, creature.WillBonus, wisBonus);

        // Defer clearing _isLoading until after dispatcher processes queued ValueChanged events
        Avalonia.Threading.Dispatcher.UIThread.Post(() => _isLoading = false, Avalonia.Threading.DispatcherPriority.Background);
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
        SetText(_abilityPointsSummary, "");

        // Clear hit points
        if (_baseHpNumeric != null)
            _baseHpNumeric.Value = 1;
        SetText(_maxHpValue, "1");
        SetText(_conHpBonus, "+0");
        SetText(_expectedHpSummary, "");

        // Clear armor class
        if (_naturalAcNumeric != null)
            _naturalAcNumeric.Value = 0;
        SetText(_dexAcValue, "+0");
        SetText(_sizeModValue, "+0");
        SetText(_totalAcValue, "10");

        // Clear combat stats
        SetText(_babValue, "+0");
        SetText(_babBreakdown, "");
        SetText(_aprValue, "1");
        SetText(_attackSequence, "");
        SetText(_crTotalDisplay, "0");
        SetText(_crValueDisplay, "0");
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
