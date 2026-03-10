using System.Linq;
using Avalonia.Input;
using Quartermaster.Services;

namespace Quartermaster.Views.Dialogs;

/// <summary>
/// Step 2: Ability score increase at levels 4/8/12/16/20/24/28/32/36/40.
/// </summary>
public partial class LevelUpWizardWindow
{
    #region Step 2: Ability Score Increase

    private void PrepareStep2_AbilityScore()
    {
        int totalLevel = _creature.ClassList.Sum(c => c.ClassLevel) + 1;
        _needsAbilityIncrease = _validationLevel == ValidationLevel.None || totalLevel % 4 == 0;

        if (!_needsAbilityIncrease)
            return;

        // Update description for CE mode
        _abilityIncreaseDescription.Text = _validationLevel == ValidationLevel.None
            ? "Select ability scores to increase by +1. (CE mode: no restrictions)"
            : "Choose one ability score to increase by +1.";

        // Populate current ability scores
        byte[] scores = { _creature.Str, _creature.Dex, _creature.Con, _creature.Int, _creature.Wis, _creature.Cha };
        for (int i = 0; i < 6; i++)
        {
            _abilityValues[i].Text = scores[i].ToString();
            _abilityChanges[i].Text = "";
            _abilityRadios[i].Text = "○";
            _abilityBorders[i].Classes.Set("step-indicator", true);
            _abilityBorders[i].Classes.Set("current", false);
        }

        // Restore previous selection if going back
        if (_validationLevel == ValidationLevel.None)
        {
            foreach (var idx in _ceAbilityIncreases)
                UpdateAbilityToggle(idx, true);
        }
        else if (_selectedAbilityIncrease >= 0)
        {
            UpdateAbilitySelection(_selectedAbilityIncrease);
        }
    }

    private void OnAbilityBorderClick(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Avalonia.Controls.Border border && border.Tag is string tagStr && int.TryParse(tagStr, out int index))
        {
            if (_validationLevel == ValidationLevel.None)
            {
                // CE mode: toggle multiple abilities
                if (_ceAbilityIncreases.Contains(index))
                {
                    _ceAbilityIncreases.Remove(index);
                    UpdateAbilityToggle(index, false);
                }
                else
                {
                    _ceAbilityIncreases.Add(index);
                    UpdateAbilityToggle(index, true);
                }
                // Keep _selectedAbilityIncrease in sync (use last toggled on, or -1)
                _selectedAbilityIncrease = _ceAbilityIncreases.Count > 0 ? _ceAbilityIncreases.Last() : -1;
            }
            else
            {
                // Normal mode: radio selection
                _selectedAbilityIncrease = index;
                _ceAbilityIncreases.Clear();
                UpdateAbilitySelection(index);
            }
            UpdateSidebarSummaries();
            ValidateCurrentStep();
        }
    }

    private void UpdateAbilitySelection(int selectedIndex)
    {
        byte[] scores = { _creature.Str, _creature.Dex, _creature.Con, _creature.Int, _creature.Wis, _creature.Cha };

        for (int i = 0; i < 6; i++)
        {
            if (i == selectedIndex)
            {
                _abilityRadios[i].Text = "●";
                _abilityChanges[i].Text = $"{scores[i]} → {scores[i] + 1}";
                _abilityBorders[i].Classes.Set("current", true);
            }
            else
            {
                _abilityRadios[i].Text = "○";
                _abilityChanges[i].Text = "";
                _abilityBorders[i].Classes.Set("current", false);
            }
        }
    }

    private void UpdateAbilityToggle(int index, bool selected)
    {
        byte[] scores = { _creature.Str, _creature.Dex, _creature.Con, _creature.Int, _creature.Wis, _creature.Cha };

        if (selected)
        {
            _abilityRadios[index].Text = "●";
            _abilityChanges[index].Text = $"{scores[index]} → {scores[index] + 1}";
            _abilityBorders[index].Classes.Set("current", true);
        }
        else
        {
            _abilityRadios[index].Text = "○";
            _abilityChanges[index].Text = "";
            _abilityBorders[index].Classes.Set("current", false);
        }
    }

    #endregion
}
