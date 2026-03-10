using System.Linq;
using Avalonia.Input;

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
        _needsAbilityIncrease = totalLevel % 4 == 0;

        if (!_needsAbilityIncrease)
            return;

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
        if (_selectedAbilityIncrease >= 0)
        {
            UpdateAbilitySelection(_selectedAbilityIncrease);
        }
    }

    private void OnAbilityBorderClick(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Avalonia.Controls.Border border && border.Tag is string tagStr && int.TryParse(tagStr, out int index))
        {
            _selectedAbilityIncrease = index;
            UpdateAbilitySelection(index);
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

    #endregion
}
