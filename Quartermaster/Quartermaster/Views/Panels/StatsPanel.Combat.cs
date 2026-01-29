using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Quartermaster.Services;
using Radoub.Formats.Uti;
using Radoub.Formats.Utc;

namespace Quartermaster.Views.Panels;

/// <summary>
/// StatsPanel partial class - Combat stats (HP, AC, BAB, CR).
/// </summary>
public partial class StatsPanel
{
    private void OnCRAdjustValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (_isLoading || _currentCreature == null) return;

        _currentCreature.CRAdjust = (int)(e.NewValue ?? 0);
        UpdateCrTotalDisplay();
        CRAdjustChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Updates the total CR display (Calculated + Adjustment).
    /// </summary>
    private void UpdateCrTotalDisplay()
    {
        if (_crTotalDisplay == null || _currentCreature == null) return;

        float totalCr = _currentCreature.ChallengeRating + _currentCreature.CRAdjust;
        SetText(_crTotalDisplay, totalCr.ToString("0.##"));
    }

    private void OnNaturalAcValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (_isLoading || _currentCreature == null) return;

        _currentCreature.NaturalAC = (byte)(e.NewValue ?? 0);
        RecalculateTotalAc();
        NaturalAcChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnBaseHpValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (_isLoading || _currentCreature == null) return;

        short newBaseHp = (short)(e.NewValue ?? 1);
        _currentCreature.HitPoints = newBaseHp;

        // Recalculate MaxHP = BaseHP + Con contribution
        RecalculateMaxHp();

        HitPointsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RecalculateMaxHp()
    {
        if (_currentCreature == null) return;

        var racialMods = _displayService?.GetRacialModifiers(_currentCreature.Race) ?? new RacialModifiers();
        int conTotal = _currentCreature.Con + racialMods.Con;
        int conBonus = CreatureDisplayService.CalculateAbilityBonus(conTotal);
        int totalLevel = _currentCreature.ClassList.Sum(c => c.ClassLevel);
        int conHpContribution = conBonus * totalLevel;

        // Calculate new MaxHP
        int newMaxHp = _currentCreature.HitPoints + conHpContribution;
        _currentCreature.MaxHitPoints = (short)Math.Max(1, newMaxHp);

        // Set CurrentHP = MaxHP (creatures spawn at full health)
        _currentCreature.CurrentHitPoints = _currentCreature.MaxHitPoints;

        // Update displays
        SetText(_maxHpValue, _currentCreature.MaxHitPoints.ToString());
        SetText(_conHpBonus, CreatureDisplayService.FormatBonus(conHpContribution));
    }

    private void UpdateHitPointsDisplay()
    {
        // Delegate to the shared recalculation logic
        RecalculateMaxHp();
    }

    /// <summary>
    /// Updates the expected HP summary display.
    /// Shows comparison of base HP against expected range from class hit dice.
    /// </summary>
    private void UpdateExpectedHpSummary(UtcFile creature)
    {
        if (_expectedHpSummary == null || _displayService == null)
        {
            SetText(_expectedHpSummary, "");
            return;
        }

        int totalLevel = creature.ClassList.Sum(c => c.ClassLevel);
        if (totalLevel == 0)
        {
            SetText(_expectedHpSummary, "");
            Avalonia.Controls.ToolTip.SetTip(_expectedHpSummary, null);
            return;
        }

        // Calculate expected HP range from dice rolls
        var (minHp, avgHp, maxHp) = _displayService.CalculateExpectedHpRange(creature);
        int baseHp = creature.HitPoints;

        // Set tooltip with full explanation
        string tooltip = $"Expected hit points are between {minHp} and {maxHp} with an average of {avgHp}.\nFirst level gets max die, subsequent levels roll 1 to hitDie.";
        Avalonia.Controls.ToolTip.SetTip(_expectedHpSummary, tooltip);

        // Format display text
        string text;
        if (baseHp < minHp)
        {
            // Below minimum - unusual
            _expectedHpSummary.Foreground = this.FindResource("ThemeWarning") as Avalonia.Media.IBrush
                ?? this.FindResource("SystemControlForegroundBaseMediumBrush") as Avalonia.Media.IBrush;
            text = $"Expected: {minHp}-{maxHp} (low)";
        }
        else if (baseHp > maxHp)
        {
            // Above maximum - has bonuses
            _expectedHpSummary.Foreground = this.FindResource("ThemeInfo") as Avalonia.Media.IBrush
                ?? this.FindResource("SystemControlForegroundBaseMediumBrush") as Avalonia.Media.IBrush;
            text = $"Expected: {minHp}-{maxHp} (+{baseHp - maxHp})";
        }
        else if (baseHp >= avgHp)
        {
            // Good rolls
            _expectedHpSummary.Foreground = this.FindResource("ThemeSuccess") as Avalonia.Media.IBrush
                ?? this.FindResource("SystemControlForegroundBaseMediumBrush") as Avalonia.Media.IBrush;
            text = $"Expected: {minHp}-{maxHp}";
        }
        else
        {
            // Below average but valid
            _expectedHpSummary.Foreground = this.FindResource("SystemControlForegroundBaseMediumBrush") as Avalonia.Media.IBrush;
            text = $"Expected: {minHp}-{maxHp}";
        }

        SetText(_expectedHpSummary, text);
    }

    private void UpdateDexAcDisplay()
    {
        if (_currentCreature == null) return;

        var racialMods = _displayService?.GetRacialModifiers(_currentCreature.Race) ?? new RacialModifiers();
        int dexTotal = _currentCreature.Dex + racialMods.Dex;
        int dexBonus = CreatureDisplayService.CalculateAbilityBonus(dexTotal);

        SetText(_dexAcValue, CreatureDisplayService.FormatBonus(dexBonus));

        // Dex change affects Total AC
        RecalculateTotalAc();
    }

    private void RecalculateTotalAc()
    {
        if (_currentCreature == null) return;

        var racialMods = _displayService?.GetRacialModifiers(_currentCreature.Race) ?? new RacialModifiers();
        int dexTotal = _currentCreature.Dex + racialMods.Dex;
        int dexBonus = CreatureDisplayService.CalculateAbilityBonus(dexTotal);
        int sizeAcMod = _displayService?.GetSizeAcModifier(_currentCreature.AppearanceType) ?? 0;

        // Total AC = 10 (base) + Natural AC + Dex Bonus + Size Mod
        int totalAc = 10 + _currentCreature.NaturalAC + dexBonus + sizeAcMod;
        SetText(_totalAcValue, totalAc.ToString());
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
}
