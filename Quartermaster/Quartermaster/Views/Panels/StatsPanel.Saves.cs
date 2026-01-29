using Avalonia.Controls;
using Quartermaster.Services;

namespace Quartermaster.Views.Panels;

/// <summary>
/// StatsPanel partial class - Saving throws (Fortitude, Reflex, Will).
/// </summary>
public partial class StatsPanel
{
    private void WireSavingThrowEvents()
    {
        if (_fortBase != null) _fortBase.ValueChanged += OnFortValueChanged;
        if (_refBase != null) _refBase.ValueChanged += OnRefValueChanged;
        if (_willBase != null) _willBase.ValueChanged += OnWillValueChanged;
    }

    private void OnFortValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (_isLoading || _currentCreature == null) return;
        _currentCreature.FortBonus = (short)(e.NewValue ?? 0);
        UpdateSavingThrowTotal(_fortBase, _fortAbility, _fortTotal, _currentCreature.FortBonus, GetConBonus());
        SavingThrowsChanged?.Invoke(this, System.EventArgs.Empty);
    }

    private void OnRefValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (_isLoading || _currentCreature == null) return;
        _currentCreature.RefBonus = (short)(e.NewValue ?? 0);
        UpdateSavingThrowTotal(_refBase, _refAbility, _refTotal, _currentCreature.RefBonus, GetDexBonus());
        SavingThrowsChanged?.Invoke(this, System.EventArgs.Empty);
    }

    private void OnWillValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (_isLoading || _currentCreature == null) return;
        _currentCreature.WillBonus = (short)(e.NewValue ?? 0);
        UpdateSavingThrowTotal(_willBase, _willAbility, _willTotal, _currentCreature.WillBonus, GetWisBonus());
        SavingThrowsChanged?.Invoke(this, System.EventArgs.Empty);
    }

    private void UpdateSavingThrowTotal(NumericUpDown? baseCtrl, TextBlock? abilityCtrl, TextBlock? totalCtrl,
        short baseValue, int abilityBonus)
    {
        int total = baseValue + abilityBonus;
        SetText(totalCtrl, CreatureDisplayService.FormatBonus(total));
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

    private void LoadSavingThrow(NumericUpDown? baseCtrl, TextBlock? abilityCtrl, TextBlock? totalCtrl,
        short baseValue, int abilityBonus)
    {
        int total = baseValue + abilityBonus;

        if (baseCtrl != null) baseCtrl.Value = baseValue;
        SetText(abilityCtrl, CreatureDisplayService.FormatBonus(abilityBonus));
        SetText(totalCtrl, CreatureDisplayService.FormatBonus(total));
    }

    private int GetConBonus()
    {
        if (_currentCreature == null) return 0;
        var racialMods = _displayService?.GetRacialModifiers(_currentCreature.Race) ?? new RacialModifiers();
        int conTotal = _currentCreature.Con + racialMods.Con;
        return CreatureDisplayService.CalculateAbilityBonus(conTotal);
    }

    private int GetDexBonus()
    {
        if (_currentCreature == null) return 0;
        var racialMods = _displayService?.GetRacialModifiers(_currentCreature.Race) ?? new RacialModifiers();
        int dexTotal = _currentCreature.Dex + racialMods.Dex;
        return CreatureDisplayService.CalculateAbilityBonus(dexTotal);
    }

    private int GetWisBonus()
    {
        if (_currentCreature == null) return 0;
        var racialMods = _displayService?.GetRacialModifiers(_currentCreature.Race) ?? new RacialModifiers();
        int wisTotal = _currentCreature.Wis + racialMods.Wis;
        return CreatureDisplayService.CalculateAbilityBonus(wisTotal);
    }
}
