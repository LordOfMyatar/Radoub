using Radoub.TestUtilities.Builders;
using Xunit;

namespace Quartermaster.Tests;

public partial class FeatServiceAdvancedTests
{
    #region Level Requirements

    [Fact]
    public void CheckPrereqs_MinLevel_MeetsLevel_AllMet()
    {
        var creature = new CreatureBuilder()
            .WithClass(4, 5) // Fighter 5
            .Build();

        var result = _featService.CheckFeatPrerequisites(
            creature, 80, new HashSet<ushort>(),
            CalculateBab, GetClassName);

        Assert.True(result.AllMet);
        Assert.Contains(result.OtherRequirements, r => r.Description == "Character level 5+");
    }

    [Fact]
    public void CheckPrereqs_MinLevel_BelowLevel_NotMet()
    {
        var creature = new CreatureBuilder()
            .WithClass(4, 3) // Fighter 3 < 5
            .Build();

        var result = _featService.CheckFeatPrerequisites(
            creature, 80, new HashSet<ushort>(),
            CalculateBab, GetClassName);

        Assert.False(result.AllMet);
    }

    [Fact]
    public void CheckPrereqs_MinLevel_Multiclass_TotalLevelCounts()
    {
        // Fighter 2 + Rogue 3 = total 5 (meets MinLevel 5)
        var creature = new CreatureBuilder()
            .WithClass(4, 2)
            .WithClass(8, 3)
            .Build();

        var result = _featService.CheckFeatPrerequisites(
            creature, 80, new HashSet<ushort>(),
            CalculateBab, GetClassName);

        Assert.True(result.AllMet);
    }

    [Fact]
    public void CheckPrereqs_ClassSpecificLevel_MeetsFighterLevel_AllMet()
    {
        var creature = new CreatureBuilder()
            .WithClass(4, 4) // Fighter 4 (meets MinLevel 4 Fighter)
            .Build();

        var result = _featService.CheckFeatPrerequisites(
            creature, 81, new HashSet<ushort>(),
            CalculateBab, GetClassName);

        Assert.True(result.AllMet);
        Assert.Contains(result.OtherRequirements, r => r.Description.Contains("Fighter") && r.Description.Contains("4+"));
    }

    [Fact]
    public void CheckPrereqs_ClassSpecificLevel_WrongClass_NotMet()
    {
        // Has Wizard 4 but needs Fighter 4
        var creature = new CreatureBuilder()
            .WithClass(10, 4) // Wizard 4
            .Build();

        var result = _featService.CheckFeatPrerequisites(
            creature, 81, new HashSet<ushort>(),
            CalculateBab, GetClassName);

        Assert.False(result.AllMet);
    }

    [Fact]
    public void CheckPrereqs_ClassSpecificLevel_MulticlassWithFighter_AllMet()
    {
        // Wizard 10 + Fighter 4 — meets Fighter 4 requirement
        var creature = new CreatureBuilder()
            .WithClass(10, 10)
            .WithClass(4, 4)
            .Build();

        var result = _featService.CheckFeatPrerequisites(
            creature, 81, new HashSet<ushort>(),
            CalculateBab, GetClassName);

        Assert.True(result.AllMet);
    }

    [Fact]
    public void CheckPrereqs_MaxLevel_BelowCap_AllMet()
    {
        var creature = new CreatureBuilder()
            .WithClass(4, 5) // Level 5 <= 10
            .Build();

        var result = _featService.CheckFeatPrerequisites(
            creature, 82, new HashSet<ushort>(),
            CalculateBab, GetClassName);

        Assert.True(result.AllMet);
        Assert.Contains(result.OtherRequirements, r => r.Description == "Max level 10" && r.Met!.Value);
    }

    [Fact]
    public void CheckPrereqs_MaxLevel_ExactlyCap_AllMet()
    {
        var creature = new CreatureBuilder()
            .WithClass(4, 10) // Level 10 = 10
            .Build();

        var result = _featService.CheckFeatPrerequisites(
            creature, 82, new HashSet<ushort>(),
            CalculateBab, GetClassName);

        Assert.True(result.AllMet);
    }

    [Fact]
    public void CheckPrereqs_MaxLevel_AboveCap_NotMet()
    {
        var creature = new CreatureBuilder()
            .WithClass(4, 11) // Level 11 > 10
            .Build();

        var result = _featService.CheckFeatPrerequisites(
            creature, 82, new HashSet<ushort>(),
            CalculateBab, GetClassName);

        Assert.False(result.AllMet);
        Assert.Contains(result.OtherRequirements, r => r.Description == "Max level 10" && !r.Met!.Value);
    }

    [Fact]
    public void CheckPrereqs_MinMaxLevelRange_InRange_AllMet()
    {
        var creature = new CreatureBuilder()
            .WithClass(4, 10) // Level 10: within 5-15 range
            .Build();

        var result = _featService.CheckFeatPrerequisites(
            creature, 83, new HashSet<ushort>(),
            CalculateBab, GetClassName);

        Assert.True(result.AllMet);
    }

    [Fact]
    public void CheckPrereqs_MinMaxLevelRange_BelowMin_NotMet()
    {
        var creature = new CreatureBuilder()
            .WithClass(4, 3) // Level 3 < 5
            .Build();

        var result = _featService.CheckFeatPrerequisites(
            creature, 83, new HashSet<ushort>(),
            CalculateBab, GetClassName);

        Assert.False(result.AllMet);
    }

    [Fact]
    public void CheckPrereqs_MinMaxLevelRange_AboveMax_NotMet()
    {
        var creature = new CreatureBuilder()
            .WithClass(4, 20) // Level 20 > 15
            .Build();

        var result = _featService.CheckFeatPrerequisites(
            creature, 83, new HashSet<ushort>(),
            CalculateBab, GetClassName);

        Assert.False(result.AllMet);
    }

    #endregion

    #region Multiple Ability Prerequisites

    [Fact]
    public void CheckPrereqs_DualAbility_BothMet_AllMet()
    {
        var creature = new CreatureBuilder()
            .WithAbilities(14, 14, 10, 10, 10, 10) // STR 14, DEX 14
            .WithClass(4, 1)
            .Build();

        var result = _featService.CheckFeatPrerequisites(
            creature, 90, new HashSet<ushort>(),
            CalculateBab, GetClassName);

        Assert.True(result.AllMet);
        Assert.Equal(2, result.AbilityRequirements.Count);
        Assert.All(result.AbilityRequirements, r => Assert.True(r.Met));
    }

    [Fact]
    public void CheckPrereqs_DualAbility_OneFailsOnePass_NotMet()
    {
        var creature = new CreatureBuilder()
            .WithAbilities(14, 10, 10, 10, 10, 10) // STR 14 ok, DEX 10 < 13
            .WithClass(4, 1)
            .Build();

        var result = _featService.CheckFeatPrerequisites(
            creature, 90, new HashSet<ushort>(),
            CalculateBab, GetClassName);

        Assert.False(result.AllMet);
        Assert.Contains(result.AbilityRequirements, r => r.Description == "STR 13+" && r.Met);
        Assert.Contains(result.AbilityRequirements, r => r.Description == "DEX 13+" && !r.Met);
    }

    [Fact]
    public void CheckPrereqs_AllSixAbilities_AllMet()
    {
        var creature = new CreatureBuilder()
            .WithAbilities(12, 12, 12, 12, 14, 12) // All meet minimums
            .WithClass(4, 1)
            .Build();

        var result = _featService.CheckFeatPrerequisites(
            creature, 91, new HashSet<ushort>(),
            CalculateBab, GetClassName);

        Assert.True(result.AllMet);
        Assert.Equal(6, result.AbilityRequirements.Count);
    }

    [Fact]
    public void CheckPrereqs_AllSixAbilities_WisdomFails_NotMet()
    {
        var creature = new CreatureBuilder()
            .WithAbilities(12, 12, 12, 12, 10, 12) // WIS 10 < 14 (CON=12, WIS=10)
            .WithClass(4, 1)
            .Build();

        var result = _featService.CheckFeatPrerequisites(
            creature, 91, new HashSet<ushort>(),
            CalculateBab, GetClassName);

        Assert.False(result.AllMet);
        Assert.Contains(result.AbilityRequirements, r => r.Description == "WIS 14+" && !r.Met);
        // Other 5 should pass
        Assert.Equal(5, result.AbilityRequirements.Count(r => r.Met));
    }

    #endregion
}
