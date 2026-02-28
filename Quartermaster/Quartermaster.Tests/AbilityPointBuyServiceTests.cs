using Quartermaster.Services;
using Xunit;

namespace Quartermaster.Tests;

/// <summary>
/// Tests for AbilityPointBuyService — D&amp;D 3.5e/NWN point-buy ability allocation.
/// </summary>
public class AbilityPointBuyServiceTests
{
    #region GetCostForScore

    [Theory]
    [InlineData(8, 0)]
    [InlineData(9, 1)]
    [InlineData(10, 2)]
    [InlineData(11, 3)]
    [InlineData(12, 4)]
    [InlineData(13, 5)]
    [InlineData(14, 6)]
    [InlineData(15, 8)]
    [InlineData(16, 10)]
    [InlineData(17, 13)]
    [InlineData(18, 16)]
    public void GetCostForScore_ReturnsExpectedCost(int score, int expectedCost)
    {
        Assert.Equal(expectedCost, AbilityPointBuyService.GetCostForScore(score));
    }

    [Theory]
    [InlineData(7)]
    [InlineData(19)]
    [InlineData(0)]
    [InlineData(-1)]
    public void GetCostForScore_OutOfRange_ReturnsZero(int score)
    {
        Assert.Equal(0, AbilityPointBuyService.GetCostForScore(score));
    }

    #endregion

    #region GetIncreaseCost

    [Theory]
    [InlineData(8, 1)]   // 1 - 0
    [InlineData(9, 1)]   // 2 - 1
    [InlineData(13, 1)]  // 6 - 5
    [InlineData(14, 2)]  // 8 - 6
    [InlineData(15, 2)]  // 10 - 8
    [InlineData(16, 3)]  // 13 - 10
    [InlineData(17, 3)]  // 16 - 13
    public void GetIncreaseCost_ReturnsExpectedMarginalCost(int currentScore, int expectedCost)
    {
        Assert.Equal(expectedCost, AbilityPointBuyService.GetIncreaseCost(currentScore));
    }

    [Fact]
    public void GetIncreaseCost_AtMax_ReturnsMaxValue()
    {
        Assert.Equal(int.MaxValue, AbilityPointBuyService.GetIncreaseCost(18));
    }

    #endregion

    #region CalculatePointsSpent

    [Fact]
    public void CalculatePointsSpent_AllMinimum_ReturnsZero()
    {
        var scores = AllScoresAt(8);
        Assert.Equal(0, AbilityPointBuyService.CalculatePointsSpent(scores));
    }

    [Fact]
    public void CalculatePointsSpent_AllAt10_Returns12()
    {
        // 6 abilities * 2 points each = 12
        var scores = AllScoresAt(10);
        Assert.Equal(12, AbilityPointBuyService.CalculatePointsSpent(scores));
    }

    [Fact]
    public void CalculatePointsSpent_AllAt12_Returns24()
    {
        // 6 abilities * 4 points each = 24
        var scores = AllScoresAt(12);
        Assert.Equal(24, AbilityPointBuyService.CalculatePointsSpent(scores));
    }

    [Fact]
    public void CalculatePointsSpent_AllAtMax_Returns96()
    {
        // 6 abilities * 16 points each = 96
        var scores = AllScoresAt(18);
        Assert.Equal(96, AbilityPointBuyService.CalculatePointsSpent(scores));
    }

    [Fact]
    public void CalculatePointsSpent_MixedScores_CalculatesCorrectly()
    {
        var scores = new Dictionary<string, int>
        {
            { "STR", 16 }, // 10 points
            { "DEX", 14 }, // 6 points
            { "CON", 14 }, // 6 points
            { "INT", 10 }, // 2 points
            { "WIS", 8 },  // 0 points
            { "CHA", 8 }   // 0 points
        };
        Assert.Equal(24, AbilityPointBuyService.CalculatePointsSpent(scores));
    }

    #endregion

    #region CalculatePointsRemaining

    [Fact]
    public void CalculatePointsRemaining_AllMinimum_ReturnsBudget()
    {
        var scores = AllScoresAt(8);
        Assert.Equal(30, AbilityPointBuyService.CalculatePointsRemaining(30, scores));
    }

    [Fact]
    public void CalculatePointsRemaining_FullySpent_ReturnsZero()
    {
        // All at 12 = 24 points, budget 24
        var scores = AllScoresAt(12);
        Assert.Equal(0, AbilityPointBuyService.CalculatePointsRemaining(24, scores));
    }

    [Fact]
    public void CalculatePointsRemaining_Overspent_ReturnsNegative()
    {
        // All at 12 = 24 points, budget 20
        var scores = AllScoresAt(12);
        Assert.Equal(-4, AbilityPointBuyService.CalculatePointsRemaining(20, scores));
    }

    #endregion

    #region CanIncrease / CanDecrease

    [Theory]
    [InlineData(8, 1, true)]
    [InlineData(17, 3, true)]
    [InlineData(17, 2, false)]  // Need 3 but only have 2
    [InlineData(18, 100, false)] // At max
    [InlineData(14, 2, true)]
    [InlineData(14, 1, false)]  // Need 2 but only have 1
    public void CanIncrease_ReturnsExpected(int score, int remaining, bool expected)
    {
        Assert.Equal(expected, AbilityPointBuyService.CanIncrease(score, remaining));
    }

    [Theory]
    [InlineData(8, false)]
    [InlineData(9, true)]
    [InlineData(18, true)]
    public void CanDecrease_ReturnsExpected(int score, bool expected)
    {
        Assert.Equal(expected, AbilityPointBuyService.CanDecrease(score));
    }

    #endregion

    #region AutoAssign

    [Fact]
    public void AutoAssign_NoPrimary_ReturnsBalancedSpread()
    {
        var result = AbilityPointBuyService.AutoAssign(30, null);

        Assert.Equal(6, result.Count);
        foreach (var ability in AbilityPointBuyService.AbilityNames)
        {
            Assert.True(result[ability] >= 8, $"{ability} should be >= 8");
            Assert.True(result[ability] <= 18, $"{ability} should be <= 18");
        }

        int spent = AbilityPointBuyService.CalculatePointsSpent(result);
        Assert.True(spent <= 30, $"Should not overspend: spent {spent}");
    }

    [Fact]
    public void AutoAssign_WithPrimary_PrioritizesPrimaryAbility()
    {
        var result = AbilityPointBuyService.AutoAssign(30, "STR");

        Assert.Equal(16, result["STR"]); // Primary goes to 16

        int spent = AbilityPointBuyService.CalculatePointsSpent(result);
        Assert.True(spent <= 30, $"Should not overspend: spent {spent}");
    }

    [Fact]
    public void AutoAssign_WithInvalidPrimary_FallsBackToBalanced()
    {
        var result = AbilityPointBuyService.AutoAssign(30, "****");

        // Should still produce valid scores
        Assert.Equal(6, result.Count);
        int spent = AbilityPointBuyService.CalculatePointsSpent(result);
        Assert.True(spent <= 30);
    }

    [Theory]
    [InlineData("STR")]
    [InlineData("DEX")]
    [InlineData("CON")]
    [InlineData("INT")]
    [InlineData("WIS")]
    [InlineData("CHA")]
    public void AutoAssign_AllPrimaryAbilities_NeverOverspend(string primary)
    {
        var result = AbilityPointBuyService.AutoAssign(30, primary);

        int spent = AbilityPointBuyService.CalculatePointsSpent(result);
        Assert.True(spent <= 30, $"Primary={primary}: spent {spent} > 30");

        // Primary should be 16
        Assert.Equal(16, result[primary]);
    }

    [Fact]
    public void AutoAssign_SmallBudget_StaysWithinBounds()
    {
        var result = AbilityPointBuyService.AutoAssign(10, "STR");

        int spent = AbilityPointBuyService.CalculatePointsSpent(result);
        Assert.True(spent <= 10, $"Spent {spent} > 10");

        foreach (var score in result.Values)
        {
            Assert.True(score >= 8);
            Assert.True(score <= 18);
        }
    }

    #endregion

    #region Constants

    [Fact]
    public void AbilityNames_HasSixEntries()
    {
        Assert.Equal(6, AbilityPointBuyService.AbilityNames.Length);
    }

    [Fact]
    public void AbilityFullNames_MatchesAbilityNames()
    {
        Assert.Equal(AbilityPointBuyService.AbilityNames.Length, AbilityPointBuyService.AbilityFullNames.Length);
    }

    [Fact]
    public void PointBuyCosts_HasElevenEntries()
    {
        // Scores 8-18 = 11 entries
        Assert.Equal(11, AbilityPointBuyService.PointBuyCosts.Length);
    }

    [Fact]
    public void PointBuyCosts_IsMonotonicallyIncreasing()
    {
        for (int i = 1; i < AbilityPointBuyService.PointBuyCosts.Length; i++)
        {
            Assert.True(AbilityPointBuyService.PointBuyCosts[i] > AbilityPointBuyService.PointBuyCosts[i - 1],
                $"Cost at index {i} ({AbilityPointBuyService.PointBuyCosts[i]}) should be > index {i - 1} ({AbilityPointBuyService.PointBuyCosts[i - 1]})");
        }
    }

    #endregion

    #region Helpers

    private static Dictionary<string, int> AllScoresAt(int score)
    {
        var scores = new Dictionary<string, int>();
        foreach (var ability in AbilityPointBuyService.AbilityNames)
            scores[ability] = score;
        return scores;
    }

    #endregion
}
