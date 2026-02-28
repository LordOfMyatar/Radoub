using System;
using System.Collections.Generic;
using System.Linq;

namespace Quartermaster.Services;

/// <summary>
/// Handles D&amp;D 3.5e/NWN point-buy ability score allocation.
/// Extracted from NewCharacterWizardWindow.Abilities.cs for testability.
/// </summary>
public class AbilityPointBuyService
{
    /// <summary>Minimum base ability score (before racial mods).</summary>
    public const int AbilityMinBase = 8;

    /// <summary>Maximum base ability score (before racial mods).</summary>
    public const int AbilityMaxBase = 18;

    /// <summary>
    /// Point-buy costs by score. Index = score - 8.
    /// Score 8 = 0 points, score 9 = 1, ..., score 18 = 16.
    /// </summary>
    public static readonly int[] PointBuyCosts = { 0, 1, 2, 3, 4, 5, 6, 8, 10, 13, 16 };

    /// <summary>
    /// Standard D&amp;D ability names in order.
    /// </summary>
    public static readonly string[] AbilityNames = { "STR", "DEX", "CON", "INT", "WIS", "CHA" };

    /// <summary>
    /// Full ability names in order, matching AbilityNames.
    /// </summary>
    public static readonly string[] AbilityFullNames = { "Strength", "Dexterity", "Constitution", "Intelligence", "Wisdom", "Charisma" };

    /// <summary>
    /// Gets the point-buy cost for a given base score.
    /// </summary>
    public static int GetCostForScore(int baseScore)
    {
        int index = baseScore - AbilityMinBase;
        if (index < 0 || index >= PointBuyCosts.Length)
            return 0;
        return PointBuyCosts[index];
    }

    /// <summary>
    /// Gets the marginal cost to increase from <paramref name="currentScore"/> to <paramref name="currentScore"/>+1.
    /// </summary>
    public static int GetIncreaseCost(int currentScore)
    {
        int nextIndex = currentScore + 1 - AbilityMinBase;
        if (nextIndex < 0 || nextIndex >= PointBuyCosts.Length)
            return int.MaxValue;
        return PointBuyCosts[nextIndex] - GetCostForScore(currentScore);
    }

    /// <summary>
    /// Calculates total points spent across all abilities.
    /// </summary>
    public static int CalculatePointsSpent(IReadOnlyDictionary<string, int> abilityBaseScores)
    {
        int spent = 0;
        foreach (var ability in AbilityNames)
        {
            if (abilityBaseScores.TryGetValue(ability, out int score))
                spent += GetCostForScore(score);
        }
        return spent;
    }

    /// <summary>
    /// Calculates remaining points from a budget.
    /// </summary>
    public static int CalculatePointsRemaining(int pointBuyTotal, IReadOnlyDictionary<string, int> abilityBaseScores)
    {
        return pointBuyTotal - CalculatePointsSpent(abilityBaseScores);
    }

    /// <summary>
    /// Checks if a score can be increased (under max and enough points).
    /// </summary>
    public static bool CanIncrease(int currentScore, int pointsRemaining)
    {
        if (currentScore >= AbilityMaxBase) return false;
        return pointsRemaining >= GetIncreaseCost(currentScore);
    }

    /// <summary>
    /// Checks if a score can be decreased (above minimum).
    /// </summary>
    public static bool CanDecrease(int currentScore)
    {
        return currentScore > AbilityMinBase;
    }

    /// <summary>
    /// Auto-assigns ability scores based on a primary ability from package data.
    /// Strategy: Push primary to 16, then distribute remaining by priority order.
    /// </summary>
    /// <param name="pointBuyTotal">Total point-buy budget</param>
    /// <param name="primaryAbility">Primary ability abbreviation (e.g. "STR"), or null for balanced spread</param>
    /// <returns>Dictionary of ability abbreviation to base score</returns>
    public static Dictionary<string, int> AutoAssign(int pointBuyTotal, string? primaryAbility)
    {
        var scores = new Dictionary<string, int>();
        foreach (var ability in AbilityNames)
            scores[ability] = AbilityMinBase;

        if (!string.IsNullOrEmpty(primaryAbility) && primaryAbility != "****" && scores.ContainsKey(primaryAbility))
        {
            // Push primary ability to 16 (cost 10), leaves points for secondaries
            scores[primaryAbility] = 16;

            var priorityOrder = GetSecondaryPriorityOrder(primaryAbility);

            // Try to raise each secondary ability to 14, then 12
            foreach (var target in new[] { 14, 12 })
            {
                foreach (var ability in priorityOrder)
                {
                    while (scores[ability] < target)
                    {
                        int cost = GetIncreaseCost(scores[ability]);
                        if (CalculatePointsRemaining(pointBuyTotal, scores) < cost) break;
                        scores[ability]++;
                    }
                }
            }

            // Spend remaining single points
            foreach (var ability in priorityOrder)
            {
                while (scores[ability] < AbilityMaxBase)
                {
                    int cost = GetIncreaseCost(scores[ability]);
                    if (CalculatePointsRemaining(pointBuyTotal, scores) < cost) break;
                    scores[ability]++;
                }
            }
        }
        else
        {
            // No primary: balanced spread (all 12s = 24 points, then raise STR/CON)
            foreach (var ability in AbilityNames)
                scores[ability] = 12;

            var boostOrder = new[] { "STR", "CON", "DEX", "WIS", "INT", "CHA" };
            foreach (var ability in boostOrder)
            {
                while (scores[ability] < AbilityMaxBase)
                {
                    int cost = GetIncreaseCost(scores[ability]);
                    if (CalculatePointsRemaining(pointBuyTotal, scores) < cost) break;
                    scores[ability]++;
                }
            }
        }

        return scores;
    }

    /// <summary>
    /// Gets the secondary ability priority order given a primary ability.
    /// </summary>
    internal static string[] GetSecondaryPriorityOrder(string primaryAbility) => primaryAbility switch
    {
        "STR" => new[] { "CON", "DEX", "WIS", "INT", "CHA" },
        "DEX" => new[] { "CON", "STR", "WIS", "INT", "CHA" },
        "CON" => new[] { "STR", "DEX", "WIS", "INT", "CHA" },
        "INT" => new[] { "CON", "DEX", "WIS", "STR", "CHA" },
        "WIS" => new[] { "CON", "DEX", "INT", "STR", "CHA" },
        "CHA" => new[] { "CON", "DEX", "WIS", "INT", "STR" },
        _ => new[] { "CON", "DEX", "WIS", "INT", "CHA" }
    };
}
