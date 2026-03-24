using Quartermaster.Models;
using Xunit;

namespace Quartermaster.Tests;

/// <summary>
/// Tests for shared wizard display item computed properties (#1798).
/// </summary>
public class WizardDisplayItemTests
{
    #region SkillDisplayItem Computed Properties

    [Fact]
    public void ClassSkillIndicator_ClassSkill_ShowsClassAndCost()
    {
        var item = new SkillDisplayItem { IsClassSkill = true, IsUnavailable = false };
        Assert.Equal("(class skill, 1 pt)", item.ClassSkillIndicator);
    }

    [Fact]
    public void ClassSkillIndicator_CrossClass_ShowsCrossClassAndCost()
    {
        var item = new SkillDisplayItem { IsClassSkill = false, IsUnavailable = false };
        Assert.Equal("(cross-class, 2 pts)", item.ClassSkillIndicator);
    }

    [Fact]
    public void ClassSkillIndicator_Unavailable_ShowsUnavailable()
    {
        var item = new SkillDisplayItem { IsClassSkill = false, IsUnavailable = true };
        Assert.Equal("(unavailable)", item.ClassSkillIndicator);
    }

    [Fact]
    public void CanIncrease_Available_BelowMax_ReturnsTrue()
    {
        var item = new SkillDisplayItem { IsUnavailable = false, CurrentRanks = 2, AddedRanks = 1, MaxRanks = 5 };
        Assert.True(item.CanIncrease);
    }

    [Fact]
    public void CanIncrease_AtMax_ReturnsFalse()
    {
        var item = new SkillDisplayItem { IsUnavailable = false, CurrentRanks = 3, AddedRanks = 2, MaxRanks = 5 };
        Assert.False(item.CanIncrease);
    }

    [Fact]
    public void CanIncrease_Unavailable_ReturnsFalse()
    {
        var item = new SkillDisplayItem { IsUnavailable = true, CurrentRanks = 0, AddedRanks = 0, MaxRanks = 5 };
        Assert.False(item.CanIncrease);
    }

    [Fact]
    public void CanDecrease_HasAddedRanks_ReturnsTrue()
    {
        var item = new SkillDisplayItem { AddedRanks = 1 };
        Assert.True(item.CanDecrease);
    }

    [Fact]
    public void CanDecrease_NoAddedRanks_ReturnsFalse()
    {
        var item = new SkillDisplayItem { AddedRanks = 0 };
        Assert.False(item.CanDecrease);
    }

    [Fact]
    public void RowOpacity_Unavailable_Returns04()
    {
        var item = new SkillDisplayItem { IsUnavailable = true };
        Assert.Equal(0.4, item.RowOpacity);
    }

    [Fact]
    public void RowOpacity_Available_Returns10()
    {
        var item = new SkillDisplayItem { IsUnavailable = false };
        Assert.Equal(1.0, item.RowOpacity);
    }

    #endregion

    #region SpellDisplayItem Computed Properties

    [Fact]
    public void DisplayName_WithSchool_IncludesSchoolAbbrev()
    {
        var item = new SpellDisplayItem { Name = "Fireball", SchoolAbbrev = "Evo" };
        Assert.Equal("Fireball [Evo]", item.DisplayName);
    }

    [Fact]
    public void DisplayName_NoSchool_ReturnsNameOnly()
    {
        var item = new SpellDisplayItem { Name = "Fireball", SchoolAbbrev = "" };
        Assert.Equal("Fireball", item.DisplayName);
    }

    [Fact]
    public void DisplayName_NullSchool_ReturnsNameOnly()
    {
        // SchoolAbbrev defaults to "" so this tests the default
        var item = new SpellDisplayItem { Name = "Fireball" };
        Assert.Equal("Fireball", item.DisplayName);
    }

    #endregion

    #region INamedItem Implementation

    [Fact]
    public void SkillDisplayItem_ImplementsINamedItem()
    {
        var item = new SkillDisplayItem { Name = "Hide" };
        Assert.IsAssignableFrom<Services.SkillDisplayHelper.INamedItem>(item);
        Assert.Equal("Hide", ((Services.SkillDisplayHelper.INamedItem)item).Name);
    }

    [Fact]
    public void SpellDisplayItem_ImplementsINamedItem()
    {
        var item = new SpellDisplayItem { Name = "Fireball" };
        Assert.IsAssignableFrom<Services.SkillDisplayHelper.INamedItem>(item);
        Assert.Equal("Fireball", ((Services.SkillDisplayHelper.INamedItem)item).Name);
    }

    #endregion
}
