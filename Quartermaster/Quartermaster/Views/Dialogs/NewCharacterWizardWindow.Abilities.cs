using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Quartermaster.Services;
using Radoub.UI.Services;

namespace Quartermaster.Views.Dialogs;

/// <summary>
/// Step 6: Ability score allocation (point-buy system).
/// </summary>
public partial class NewCharacterWizardWindow
{
    #region Step 6: Ability Scores

    // Canonical order lives in AbilityPointBuyService; a divergent copy here would map racial
    // modifiers to the wrong stat (#2581).
    private static string[] AbilityNames => AbilityPointBuyService.AbilityNames;
    private static string[] AbilityFullNames => AbilityPointBuyService.AbilityFullNames;

    private void PrepareStep6()
    {
        // Update point-buy budget from racialtypes.2da (race may have changed since last visit)
        _pointBuyTotal = _displayService.GetRacialAbilitiesPointBuyNumber(_selectedRaceId);

        // Update subtitle to reflect race-specific budget
        _abilityStepSubtitle.Text = $"Distribute {_pointBuyTotal} points across your abilities. Base scores start at 8. Higher scores cost more points.";

        if (!_step5Loaded)
        {
            _step5Loaded = true;
            BuildAbilityRows();
        }

        UpdateAbilityDisplay();
        UpdatePrestigeAbilityBanner();
    }

    private void BuildAbilityRows()
    {
        _abilityRowsPanel.Children.Clear();

        for (int i = 0; i < AbilityNames.Length; i++)
        {
            var ability = AbilityNames[i];
            var row = new Grid
            {
                ColumnDefinitions = ColumnDefinitions.Parse("120,70,35,35,70,70,70,*"),
                Margin = new Avalonia.Thickness(0, 2)
            };

            // Ability name
            var nameLabel = new TextBlock
            {
                Text = AbilityFullNames[i],
                FontWeight = FontWeight.SemiBold,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };
            Grid.SetColumn(nameLabel, 0);
            row.Children.Add(nameLabel);

            // Base score
            var baseLabel = new TextBlock
            {
                Text = _abilityBaseScores[ability].ToString(),
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                FontSize = this.FindResource("FontSizeMedium") as double? ?? 16,
                FontWeight = FontWeight.Bold,
                Tag = $"Base_{ability}"
            };
            Grid.SetColumn(baseLabel, 1);
            row.Children.Add(baseLabel);

            // [-] button
            var decreaseBtn = new Button
            {
                Content = "−",
                Width = 28,
                HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                Tag = ability
            };
            decreaseBtn.Click += OnAbilityDecrease;
            Grid.SetColumn(decreaseBtn, 2);
            row.Children.Add(decreaseBtn);

            // [+] button
            var increaseBtn = new Button
            {
                Content = "+",
                Width = 28,
                HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                Tag = ability
            };
            increaseBtn.Click += OnAbilityIncrease;
            Grid.SetColumn(increaseBtn, 3);
            row.Children.Add(increaseBtn);

            // Racial modifier
            var racialLabel = new TextBlock
            {
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Tag = $"Racial_{ability}"
            };
            Grid.SetColumn(racialLabel, 4);
            row.Children.Add(racialLabel);

            // Total score
            var totalLabel = new TextBlock
            {
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                FontWeight = FontWeight.Bold,
                FontSize = this.FindResource("FontSizeMedium") as double? ?? 16,
                Tag = $"Total_{ability}"
            };
            Grid.SetColumn(totalLabel, 5);
            row.Children.Add(totalLabel);

            // Modifier
            var modLabel = new TextBlock
            {
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Tag = $"Mod_{ability}"
            };
            Grid.SetColumn(modLabel, 6);
            row.Children.Add(modLabel);

            // Cost
            var costLabel = new TextBlock
            {
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Foreground = this.FindResource("SystemControlForegroundBaseMediumBrush") as IBrush,
                Tag = $"Cost_{ability}"
            };
            Grid.SetColumn(costLabel, 7);
            row.Children.Add(costLabel);

            _abilityRowsPanel.Children.Add(row);
        }
    }

    private void UpdateAbilityDisplay()
    {
        var racialMods = _displayService.GetRacialModifiers(_selectedRaceId);

        foreach (var row in _abilityRowsPanel.Children.OfType<Grid>())
        {
            foreach (var child in row.Children)
            {
                if (child is not TextBlock tb || child.Tag is not string tag)
                    continue;

                if (tag.StartsWith("Base_"))
                {
                    var ability = tag[5..];
                    tb.Text = _abilityBaseScores[ability].ToString();
                }
                else if (tag.StartsWith("Racial_"))
                {
                    var ability = tag[7..];
                    int racialMod = GetRacialModForAbility(racialMods, ability);
                    if (racialMod == 0)
                    {
                        tb.Text = "—";
                        tb.ClearValue(TextBlock.ForegroundProperty);
                    }
                    else
                    {
                        tb.Text = CreatureDisplayService.FormatBonus(racialMod);
                        tb.Foreground = racialMod > 0
                            ? BrushManager.GetSuccessBrush(this)
                            : BrushManager.GetWarningBrush(this);
                    }
                }
                else if (tag.StartsWith("Total_"))
                {
                    var ability = tag[6..];
                    int baseScore = _abilityBaseScores[ability];
                    int racialMod = GetRacialModForAbility(racialMods, ability);
                    int total = baseScore + racialMod;
                    tb.Text = total.ToString();
                }
                else if (tag.StartsWith("Mod_"))
                {
                    var ability = tag[4..];
                    int baseScore = _abilityBaseScores[ability];
                    int racialMod = GetRacialModForAbility(racialMods, ability);
                    int total = baseScore + racialMod;
                    int bonus = CreatureDisplayService.CalculateAbilityBonus(total);
                    tb.Text = CreatureDisplayService.FormatBonus(bonus);
                }
                else if (tag.StartsWith("Cost_"))
                {
                    var ability = tag[5..];
                    int cost = AbilityPointBuyService.GetCostForScore(_abilityBaseScores[ability]);
                    tb.Text = cost.ToString();
                }
            }

            // Update button enabled states
            foreach (var child in row.Children)
            {
                if (child is Button btn && btn.Tag is string ability)
                {
                    int baseScore = _abilityBaseScores[ability];
                    int remaining = GetAbilityPointsRemaining();

                    if (btn.Content?.ToString() == "−")
                        btn.IsEnabled = baseScore > AbilityMinBase;
                    else if (btn.Content?.ToString() == "+")
                    {
                        if (_validationLevel == ValidationLevel.Strict)
                        {
                            // Lawful Good: enforce point-buy budget
                            int costDelta = AbilityPointBuyService.GetIncreaseCost(baseScore);
                            btn.IsEnabled = baseScore < AbilityMaxBase && remaining >= costDelta;
                        }
                        else
                        {
                            // CE and TN: no point cost, no cap (except absolute max)
                            btn.IsEnabled = baseScore < AbilityMaxBase;
                        }
                    }
                }
            }
        }

        // Update points remaining (show remaining / total for clarity)
        int pointsRemaining = GetAbilityPointsRemaining();
        _abilityPointsRemainingLabel.Text = $"{pointsRemaining} / {_pointBuyTotal}";

        if (pointsRemaining > 0)
            _abilityPointsRemainingLabel.Foreground = BrushManager.GetSuccessBrush(this);
        else if (pointsRemaining == 0)
            _abilityPointsRemainingLabel.ClearValue(TextBlock.ForegroundProperty);
        else
            _abilityPointsRemainingLabel.Foreground = BrushManager.GetErrorBrush(this);

        ValidateCurrentStep();
    }

    private int GetAbilityPointsRemaining()
    {
        return AbilityPointBuyService.CalculatePointsRemaining(_pointBuyTotal, _abilityBaseScores);
    }

    private static int GetRacialModForAbility(RacialModifiers mods, string ability) => ability switch
    {
        "STR" => mods.Str,
        "DEX" => mods.Dex,
        "CON" => mods.Con,
        "INT" => mods.Int,
        "WIS" => mods.Wis,
        "CHA" => mods.Cha,
        _ => 0
    };

    private void OnAbilityIncrease(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string ability)
        {
            int currentScore = _abilityBaseScores[ability];
            if (currentScore < AbilityMaxBase)
            {
                if (_validationLevel == ValidationLevel.Strict)
                {
                    // Lawful Good: enforce point-buy budget
                    int costDelta = AbilityPointBuyService.GetIncreaseCost(currentScore);

                    if (GetAbilityPointsRemaining() >= costDelta)
                    {
                        _abilityBaseScores[ability]++;
                        UpdateAbilityDisplay();
                    }
                }
                else
                {
                    // CE and TN: free increases, no point cost
                    _abilityBaseScores[ability]++;
                    UpdateAbilityDisplay();
                }
            }
        }
    }

    private void OnAbilityDecrease(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string ability)
        {
            if (_abilityBaseScores[ability] > AbilityMinBase)
            {
                _abilityBaseScores[ability]--;
                UpdateAbilityDisplay();
            }
        }
    }

    private void OnAbilityAutoAssignClick(object? sender, RoutedEventArgs e)
    {
        // Read package primary ability from packages.2da Attribute column
        string? primaryAbility = _selectedPackageId != 255
            ? _gameDataService.Get2DAValue("packages", _selectedPackageId, "Attribute")?.ToUpperInvariant()
            : null;

        var assigned = AbilityPointBuyService.AutoAssign(_pointBuyTotal, primaryAbility);
        foreach (var (ability, score) in assigned)
            _abilityBaseScores[ability] = score;

        UpdateAbilityDisplay();
    }

    private void UpdatePrestigeAbilityBanner()
    {
        if (_prestigeClassComboBox.SelectedItem is ClassDisplayItem selected)
        {
            var prereqs = _displayService.Classes.GetPrestigePrerequisites(selected.Id);
            var abilityPrereqs = new List<string>();

            // Check for skill prerequisites that imply minimum ability scores
            // Not directly tracked in prestige prereqs, but advisory
            if (prereqs.Count > 0)
            {
                _prestigeAbilityBannerLabel.Text = $"Prestige goal: {selected.Name} — Review prerequisites in the Class step to plan ability scores.";
                _prestigeAbilityBanner.IsVisible = true;
                return;
            }
        }

        _prestigeAbilityBanner.IsVisible = false;
    }

    #endregion
}
