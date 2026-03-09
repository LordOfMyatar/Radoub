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

    private static readonly string[] AbilityNames = { "STR", "DEX", "CON", "INT", "WIS", "CHA" };
    private static readonly string[] AbilityFullNames = { "Strength", "Dexterity", "Constitution", "Intelligence", "Wisdom", "Charisma" };

    private void PrepareStep6()
    {
        // Update point-buy budget from racialtypes.2da (race may have changed since last visit)
        _pointBuyTotal = _displayService.GetRacialAbilitiesPointBuyNumber(_selectedRaceId);

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
                FontSize = 16,
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
                FontSize = 16,
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
                Foreground = this.FindResource("SystemControlForegroundBaseMediumLowBrush") as IBrush,
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
                    int baseScore = _abilityBaseScores[ability];
                    int costIndex = baseScore - AbilityMinBase;
                    int cost = costIndex >= 0 && costIndex < PointBuyCosts.Length ? PointBuyCosts[costIndex] : 0;
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
                            int nextCostIndex = baseScore + 1 - AbilityMinBase;
                            int nextCost = nextCostIndex < PointBuyCosts.Length ? PointBuyCosts[nextCostIndex] : int.MaxValue;
                            int currentCostIndex = baseScore - AbilityMinBase;
                            int currentCost = currentCostIndex >= 0 && currentCostIndex < PointBuyCosts.Length ? PointBuyCosts[currentCostIndex] : 0;
                            int costDelta = nextCost - currentCost;
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

        // Update points remaining
        int pointsRemaining = GetAbilityPointsRemaining();
        _abilityPointsRemainingLabel.Text = pointsRemaining.ToString();

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
                    int nextCostIndex = currentScore + 1 - AbilityMinBase;
                    int nextCost = nextCostIndex < PointBuyCosts.Length ? PointBuyCosts[nextCostIndex] : int.MaxValue;
                    int currentCostIndex = currentScore - AbilityMinBase;
                    int currentCost = currentCostIndex >= 0 && currentCostIndex < PointBuyCosts.Length ? PointBuyCosts[currentCostIndex] : 0;
                    int costDelta = nextCost - currentCost;

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
        // Reset all to base 8
        foreach (var ability in AbilityNames)
            _abilityBaseScores[ability] = AbilityMinBase;

        // Read package primary ability from packages.2da Attribute column
        string? primaryAbility = null;
        if (_selectedPackageId != 255)
        {
            primaryAbility = _gameDataService.Get2DAValue("packages", _selectedPackageId, "Attribute")?.ToUpperInvariant();
        }

        // Default distribution: balanced with emphasis on primary ability
        // Strategy: primary ability gets priority, then spread remaining across useful stats
        if (!string.IsNullOrEmpty(primaryAbility) && primaryAbility != "****" && _abilityBaseScores.ContainsKey(primaryAbility))
        {
            // Push primary ability to 16 (cost 10), leaves 20 points
            _abilityBaseScores[primaryAbility] = 16;
            int remaining = GetAbilityPointsRemaining();

            // Distribute remaining points across other abilities
            // Priority: CON > DEX > other stats
            var priorityOrder = primaryAbility switch
            {
                "STR" => new[] { "CON", "DEX", "WIS", "INT", "CHA" },
                "DEX" => new[] { "CON", "STR", "WIS", "INT", "CHA" },
                "CON" => new[] { "STR", "DEX", "WIS", "INT", "CHA" },
                "INT" => new[] { "CON", "DEX", "WIS", "STR", "CHA" },
                "WIS" => new[] { "CON", "DEX", "INT", "STR", "CHA" },
                "CHA" => new[] { "CON", "DEX", "WIS", "INT", "STR" },
                _ => new[] { "CON", "DEX", "WIS", "INT", "CHA" }
            };

            // Try to raise each secondary ability to 14 (cost 6), then 12 (cost 4)
            foreach (var target in new[] { 14, 12 })
            {
                foreach (var ability in priorityOrder)
                {
                    while (_abilityBaseScores[ability] < target)
                    {
                        int currentScore = _abilityBaseScores[ability];
                        int nextCostIndex = currentScore + 1 - AbilityMinBase;
                        if (nextCostIndex >= PointBuyCosts.Length) break;
                        int costDelta = PointBuyCosts[nextCostIndex] - PointBuyCosts[currentScore - AbilityMinBase];
                        if (GetAbilityPointsRemaining() < costDelta) break;
                        _abilityBaseScores[ability]++;
                    }
                }
            }

            // Spend any remaining single points
            foreach (var ability in priorityOrder)
            {
                while (GetAbilityPointsRemaining() > 0 && _abilityBaseScores[ability] < AbilityMaxBase)
                {
                    int currentScore = _abilityBaseScores[ability];
                    int nextCostIndex = currentScore + 1 - AbilityMinBase;
                    if (nextCostIndex >= PointBuyCosts.Length) break;
                    int costDelta = PointBuyCosts[nextCostIndex] - PointBuyCosts[currentScore - AbilityMinBase];
                    if (GetAbilityPointsRemaining() < costDelta) break;
                    _abilityBaseScores[ability]++;
                }
            }
        }
        else
        {
            // No primary ability: balanced spread (all 12s = 24 points, then raise STR/CON)
            foreach (var ability in AbilityNames)
                _abilityBaseScores[ability] = 12;

            var boostOrder = new[] { "STR", "CON", "DEX", "WIS", "INT", "CHA" };
            foreach (var ability in boostOrder)
            {
                while (GetAbilityPointsRemaining() > 0 && _abilityBaseScores[ability] < AbilityMaxBase)
                {
                    int currentScore = _abilityBaseScores[ability];
                    int nextCostIndex = currentScore + 1 - AbilityMinBase;
                    if (nextCostIndex >= PointBuyCosts.Length) break;
                    int costDelta = PointBuyCosts[nextCostIndex] - PointBuyCosts[currentScore - AbilityMinBase];
                    if (GetAbilityPointsRemaining() < costDelta) break;
                    _abilityBaseScores[ability]++;
                }
            }
        }

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
