using System.Linq;
using Avalonia.Controls;
using Quartermaster.Services;

namespace Quartermaster.Views.Panels;

/// <summary>
/// StatsPanel partial class - Ability scores (STR/DEX/CON/INT/WIS/CHA).
/// </summary>
public partial class StatsPanel
{
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
        UpdateAbilityPointsSummary();
        AbilityScoresChanged?.Invoke(this, System.EventArgs.Empty);
    }

    private void OnDexValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (_isLoading || _currentCreature == null) return;
        _currentCreature.Dex = (byte)(e.NewValue ?? 10);
        UpdateAbilityDisplay("Dex", _currentCreature.Dex);
        UpdateDexAcDisplay(); // Dex affects AC
        UpdateSavingThrows(); // Dex affects Reflex save
        UpdateAbilityPointsSummary();
        AbilityScoresChanged?.Invoke(this, System.EventArgs.Empty);
    }

    private void OnConValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (_isLoading || _currentCreature == null) return;
        _currentCreature.Con = (byte)(e.NewValue ?? 10);
        UpdateAbilityDisplay("Con", _currentCreature.Con);
        UpdateHitPointsDisplay(); // Con affects HP
        UpdateSavingThrows(); // Con affects Fortitude save
        UpdateAbilityPointsSummary();
        AbilityScoresChanged?.Invoke(this, System.EventArgs.Empty);
    }

    private void OnIntValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (_isLoading || _currentCreature == null) return;
        _currentCreature.Int = (byte)(e.NewValue ?? 10);
        UpdateAbilityDisplay("Int", _currentCreature.Int);
        UpdateAbilityPointsSummary();
        AbilityScoresChanged?.Invoke(this, System.EventArgs.Empty);
    }

    private void OnWisValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (_isLoading || _currentCreature == null) return;
        _currentCreature.Wis = (byte)(e.NewValue ?? 10);
        UpdateAbilityDisplay("Wis", _currentCreature.Wis);
        UpdateSavingThrows(); // Wis affects Will save
        UpdateAbilityPointsSummary();
        AbilityScoresChanged?.Invoke(this, System.EventArgs.Empty);
    }

    private void OnChaValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (_isLoading || _currentCreature == null) return;
        _currentCreature.Cha = (byte)(e.NewValue ?? 10);
        UpdateAbilityDisplay("Cha", _currentCreature.Cha);
        UpdateAbilityPointsSummary();
        AbilityScoresChanged?.Invoke(this, System.EventArgs.Empty);
    }

    /// <summary>
    /// Refreshes the ability points summary display.
    /// Call this when class levels change externally.
    /// </summary>
    public void RefreshAbilityPointsSummary()
    {
        UpdateAbilityPointsSummary();
    }

    /// <summary>
    /// Updates the ability points summary display.
    /// Shows expected vs used ability points from level-up bonuses.
    /// Characters gain 1 ability point at levels 4, 8, 12, 16, 20, 24, 28, 32, 36, 40.
    /// </summary>
    private void UpdateAbilityPointsSummary()
    {
        if (_abilityPointsSummary == null || _currentCreature == null)
        {
            SetText(_abilityPointsSummary, "");
            return;
        }

        // Calculate total level
        int totalLevel = _currentCreature.ClassList.Sum(c => c.ClassLevel);

        // Expected ability points from leveling: 1 point every 4 levels
        int expectedPoints = totalLevel / 4;

        // Calculate used points by comparing current base scores against starting values
        // NWN point-buy starts at 8 for all abilities, and players distribute 30 points
        // For creatures, we estimate starting scores as 8 (minimum typical point-buy value)
        // Used points = sum of (current - 8) for all abilities, clamped to non-negative
        var abilityPointsInfo = CalculateAbilityPointsFromLeveling();
        int usedPoints = abilityPointsInfo.UsedPoints;

        // Only show for creatures with levels (not level 0 templates)
        if (totalLevel == 0)
        {
            SetText(_abilityPointsSummary, "");
            return;
        }

        // Format: "Level Points: X/Y" where X = used, Y = expected
        string text = $"Level Points: {usedPoints}/{expectedPoints}";

        // Change color based on status
        if (_abilityPointsSummary != null)
        {
            if (usedPoints > expectedPoints)
            {
                // Over-allocated (possibly from items, buffs, or manual editing)
                _abilityPointsSummary.Foreground = this.FindResource("ThemeWarning") as Avalonia.Media.IBrush
                    ?? this.FindResource("SystemControlForegroundBaseMediumBrush") as Avalonia.Media.IBrush;
                text += " (over)";
            }
            else if (usedPoints < expectedPoints)
            {
                // Under-allocated (points available)
                _abilityPointsSummary.Foreground = this.FindResource("ThemeInfo") as Avalonia.Media.IBrush
                    ?? this.FindResource("SystemControlForegroundBaseMediumBrush") as Avalonia.Media.IBrush;
                text += $" ({expectedPoints - usedPoints} available)";
            }
            else
            {
                // Exactly matched
                _abilityPointsSummary.Foreground = this.FindResource("ThemeSuccess") as Avalonia.Media.IBrush
                    ?? this.FindResource("SystemControlForegroundBaseMediumBrush") as Avalonia.Media.IBrush;
            }
        }

        SetText(_abilityPointsSummary, text);
    }

    /// <summary>
    /// Calculates ability points from leveling by estimating how many points are beyond point-buy max.
    /// NWN point-buy max per ability is 18 at creation.
    /// Level-up points add +1 directly, bypassing point-buy cost escalation.
    /// We count points above 18 as definite level-up points, plus estimate for 17-18 range.
    /// </summary>
    private (int UsedPoints, int EstimatedStartingTotal) CalculateAbilityPointsFromLeveling()
    {
        if (_currentCreature == null)
            return (0, 0);

        // Count ability points that must have come from level-ups
        // Any score above 18 definitely came from level-up (can't point-buy above 18)
        // For scores 17-18, we estimate based on how common those starting values are
        int levelUpPoints = 0;
        levelUpPoints += CountLevelUpPointsForAbility(_currentCreature.Str);
        levelUpPoints += CountLevelUpPointsForAbility(_currentCreature.Dex);
        levelUpPoints += CountLevelUpPointsForAbility(_currentCreature.Con);
        levelUpPoints += CountLevelUpPointsForAbility(_currentCreature.Int);
        levelUpPoints += CountLevelUpPointsForAbility(_currentCreature.Wis);
        levelUpPoints += CountLevelUpPointsForAbility(_currentCreature.Cha);

        // For display purposes, calculate what the starting total would have been
        int currentTotal = _currentCreature.Str + _currentCreature.Dex + _currentCreature.Con +
                          _currentCreature.Int + _currentCreature.Wis + _currentCreature.Cha;
        int estimatedStartingTotal = currentTotal - levelUpPoints;

        return (levelUpPoints, estimatedStartingTotal);
    }

    /// <summary>
    /// Estimates how many level-up points contributed to a single ability score.
    /// Points above 18 are definitely from level-ups (can't point-buy above 18).
    /// </summary>
    private static int CountLevelUpPointsForAbility(int score)
    {
        // Scores above 18 must have come from level-up points
        // (Point-buy caps at 18)
        if (score > 18)
            return score - 18;

        // Scores 18 and below could be entirely from point-buy
        return 0;
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
}
