using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Quartermaster.Services;

namespace Quartermaster.Views.Dialogs;

/// <summary>
/// Step 2: Ability score increases using +/- increment buttons.
/// Supports consolidated mode: distributes multiple ability increases across abilities (#1645).
/// In CE mode, no limit on total increments.
/// </summary>
public partial class LevelUpWizardWindow
{
    #region Step 2: Ability Score Increase

    private void PrepareStep2_AbilityScore()
    {
        int totalLevel = _creature.ClassList.Sum(c => c.ClassLevel);

        if (_validationLevel == ValidationLevel.None)
        {
            // CE mode: always show ability step, no limit
            _needsAbilityIncrease = true;
            _abilityIncreaseLevels = new List<int>();
        }
        else
        {
            _abilityIncreaseLevels = LevelUpApplicationService.GetAbilityIncreaseLevels(totalLevel, _levelsToAdd);
            _needsAbilityIncrease = _abilityIncreaseLevels.Count > 0;
        }

        if (!_needsAbilityIncrease)
            return;

        int totalIncreasesAvailable = _validationLevel == ValidationLevel.None ? 0 : _abilityIncreaseLevels.Count;

        // Description
        if (_validationLevel == ValidationLevel.None)
        {
            _abilityIncreaseDescription.Text = "Distribute ability score increases. (CE mode: no restrictions)";
            _abilityIncreaseRemaining.IsVisible = false;
        }
        else if (totalIncreasesAvailable == 1)
        {
            _abilityIncreaseDescription.Text = $"Distribute 1 ability score increase. (Level {_abilityIncreaseLevels[0]})";
            _abilityIncreaseRemaining.IsVisible = true;
        }
        else
        {
            var levelList = string.Join(", ", _abilityIncreaseLevels.Select(l => $"Lvl {l}"));
            _abilityIncreaseDescription.Text = $"Distribute {totalIncreasesAvailable} ability score increases. ({levelList})";
            _abilityIncreaseRemaining.IsVisible = true;
        }

        // Reset increments (or restore if going back)
        // Only reset if this is the first time preparing (increments are all zero)
        bool isFirstPrep = _abilityIncrements.All(i => i == 0);
        if (isFirstPrep)
            _abilityIncrements = new int[6];

        // Populate display
        byte[] scores = { _creature.Str, _creature.Dex, _creature.Con, _creature.Int, _creature.Wis, _creature.Cha };
        for (int i = 0; i < 6; i++)
        {
            _abilityValues[i].Text = scores[i].ToString();
            _abilityRadios[i].Text = _abilityIncrements[i].ToString();
            UpdateAbilityChangeDisplay(i, scores[i]);
            _abilityBorders[i].Classes.Set("current", _abilityIncrements[i] > 0);
        }

        UpdateAbilityRemainingDisplay();
    }

    private void OnAbilityIncrease(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tagStr && int.TryParse(tagStr, out int index))
            ChangeAbilityIncrement(index, 1);
    }

    private void OnAbilityDecrease(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tagStr && int.TryParse(tagStr, out int index))
            ChangeAbilityIncrement(index, -1);
    }

    // Keep old handler for compatibility (borders no longer have PointerPressed but just in case)
    private void OnAbilityBorderClick(object? sender, Avalonia.Input.PointerPressedEventArgs e) { }

    private void ChangeAbilityIncrement(int index, int delta)
    {
        if (index < 0 || index > 5) return;

        int newValue = _abilityIncrements[index] + delta;
        if (newValue < 0) return;

        // In non-CE mode, enforce total cap
        if (_validationLevel != ValidationLevel.None)
        {
            int currentTotal = _abilityIncrements.Sum();
            int maxTotal = _abilityIncreaseLevels.Count;
            if (delta > 0 && currentTotal >= maxTotal) return;
        }

        _abilityIncrements[index] = newValue;

        // Update display
        byte[] scores = { _creature.Str, _creature.Dex, _creature.Con, _creature.Int, _creature.Wis, _creature.Cha };
        _abilityRadios[index].Text = _abilityIncrements[index].ToString();
        UpdateAbilityChangeDisplay(index, scores[index]);
        _abilityBorders[index].Classes.Set("current", _abilityIncrements[index] > 0);

        // Sync legacy fields for compatibility with summary/apply
        SyncAbilityLegacyFields();

        UpdateAbilityRemainingDisplay();
        UpdateSidebarSummaries();
        ValidateCurrentStep();
    }

    private void UpdateAbilityChangeDisplay(int index, byte baseScore)
    {
        int inc = _abilityIncrements[index];
        if (inc > 0)
            _abilityChanges[index].Text = $"{baseScore} → {baseScore + inc} (+{inc})";
        else
            _abilityChanges[index].Text = "";
    }

    private void UpdateAbilityRemainingDisplay()
    {
        if (_validationLevel == ValidationLevel.None)
        {
            int total = _abilityIncrements.Sum();
            _abilityIncreaseRemaining.Text = total > 0 ? $"Total increases: {total}" : "";
            _abilityIncreaseRemaining.IsVisible = total > 0;
        }
        else
        {
            int remaining = _abilityIncreaseLevels.Count - _abilityIncrements.Sum();
            _abilityIncreaseRemaining.Text = $"Remaining: {remaining}";
            if (remaining == 0)
                _abilityIncreaseRemaining.Foreground = Radoub.UI.Services.BrushManager.GetSuccessBrush(this);
            else
                _abilityIncreaseRemaining.ClearValue(TextBlock.ForegroundProperty);
        }
    }

    /// <summary>
    /// Syncs the new increment-based ability tracking with the legacy fields
    /// used by summary, apply, skill points, and HP calculation.
    /// </summary>
    private void SyncAbilityLegacyFields()
    {
        // _selectedAbilityIncrease: set to the ability with most increments (for backward compat)
        int maxIdx = -1;
        int maxVal = 0;
        for (int i = 0; i < 6; i++)
        {
            if (_abilityIncrements[i] > maxVal)
            {
                maxVal = _abilityIncrements[i];
                maxIdx = i;
            }
        }
        _selectedAbilityIncrease = maxIdx;

        // _ceAbilityIncreases: populate with all abilities that have increments
        _ceAbilityIncreases.Clear();
        for (int i = 0; i < 6; i++)
        {
            for (int j = 0; j < _abilityIncrements[i]; j++)
                _ceAbilityIncreases.Add(i);
        }

        // _abilityIncreasesByLevel: distribute increments across ability increase levels
        // Spread them in order: first N goes to first ability with increments, etc.
        _abilityIncreasesByLevel.Clear();
        int levelIdx = 0;
        for (int i = 0; i < 6; i++)
        {
            for (int j = 0; j < _abilityIncrements[i]; j++)
            {
                if (levelIdx < _abilityIncreaseLevels.Count)
                {
                    _abilityIncreasesByLevel[_abilityIncreaseLevels[levelIdx]] = i;
                    levelIdx++;
                }
            }
        }
    }

    // Unused legacy methods kept as stubs for compatibility
    private void UpdateAbilitySelection(int selectedIndex) { }
    private void UpdateAbilityToggle(int index, bool selected) { }

    #endregion
}
