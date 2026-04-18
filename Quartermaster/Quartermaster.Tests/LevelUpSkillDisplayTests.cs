using System;
using System.Collections.Generic;
using Xunit;

namespace Quartermaster.Tests;

/// <summary>
/// Tests for LUW skill display logic: filtering, class skill indicator, and color assignment rules.
/// These test the pure logic that drives the skill step UI (#1499, #1500).
/// </summary>
public class LevelUpSkillDisplayTests
{
    #region Skill Filter (#1499)

    [Fact]
    public void FilterSkills_EmptyFilter_ReturnsAllSkills()
    {
        var skills = CreateSampleSkills();
        var result = Services.SkillDisplayHelper.FilterByName(skills, "");
        Assert.Equal(skills.Count, result.Count);
    }

    [Fact]
    public void FilterSkills_NullFilter_ReturnsAllSkills()
    {
        var skills = CreateSampleSkills();
        var result = Services.SkillDisplayHelper.FilterByName(skills, null);
        Assert.Equal(skills.Count, result.Count);
    }

    [Fact]
    public void FilterSkills_WhitespaceFilter_ReturnsAllSkills()
    {
        var skills = CreateSampleSkills();
        var result = Services.SkillDisplayHelper.FilterByName(skills, "   ");
        Assert.Equal(skills.Count, result.Count);
    }

    [Fact]
    public void FilterSkills_MatchesSubstring_CaseInsensitive()
    {
        var skills = CreateSampleSkills();
        var result = Services.SkillDisplayHelper.FilterByName(skills, "con");

        // Should match "Concentration" only
        Assert.Single(result);
        Assert.Equal("Concentration", result[0].Name);
    }

    [Fact]
    public void FilterSkills_NoMatch_ReturnsEmpty()
    {
        var skills = CreateSampleSkills();
        var result = Services.SkillDisplayHelper.FilterByName(skills, "zzzzz");
        Assert.Empty(result);
    }

    [Fact]
    public void FilterSkills_MultipleMatches_ReturnsAll()
    {
        var skills = CreateSampleSkills();
        // "s" appears in "Search", "Spellcraft", "Use Magic Device"
        var result = Services.SkillDisplayHelper.FilterByName(skills, "s");
        Assert.True(result.Count >= 2);
    }

    [Fact]
    public void FilterSkills_ExactMatch_ReturnsSingle()
    {
        var skills = CreateSampleSkills();
        var result = Services.SkillDisplayHelper.FilterByName(skills, "Spellcraft");
        Assert.Single(result);
        Assert.Equal("Spellcraft", result[0].Name);
    }

    #endregion

    #region Class Skill Indicator (#1500)

    [Fact]
    public void ClassSkillIndicator_ClassSkill_ShowsClassAndCost()
    {
        var indicator = Services.SkillDisplayHelper.GetClassSkillIndicator(
            isClassSkill: true, isUnavailable: false);
        Assert.Equal("(class skill, 1 pt)", indicator);
    }

    [Fact]
    public void ClassSkillIndicator_CrossClassSkill_ShowsCrossClassAndCost()
    {
        var indicator = Services.SkillDisplayHelper.GetClassSkillIndicator(
            isClassSkill: false, isUnavailable: false);
        Assert.Equal("(cross-class, 2 pts)", indicator);
    }

    [Fact]
    public void ClassSkillIndicator_Unavailable_ShowsUnavailable()
    {
        var indicator = Services.SkillDisplayHelper.GetClassSkillIndicator(
            isClassSkill: false, isUnavailable: true);
        Assert.Equal("(unavailable)", indicator);
    }

    [Fact]
    public void ClassSkillIndicator_UnavailableOverridesClassSkill()
    {
        // Even if it's technically a class skill, unavailable takes precedence
        var indicator = Services.SkillDisplayHelper.GetClassSkillIndicator(
            isClassSkill: true, isUnavailable: true);
        Assert.Equal("(unavailable)", indicator);
    }

    #endregion

    #region Color Assignment Rules (#1500)

    [Fact]
    public void ShouldUseClassSkillColor_ClassSkillAvailable_ReturnsTrue()
    {
        Assert.True(Services.SkillDisplayHelper.ShouldUseClassSkillColor(
            isClassSkill: true, isUnavailable: false));
    }

    [Fact]
    public void ShouldUseClassSkillColor_CrossClass_ReturnsFalse()
    {
        Assert.False(Services.SkillDisplayHelper.ShouldUseClassSkillColor(
            isClassSkill: false, isUnavailable: false));
    }

    [Fact]
    public void ShouldUseClassSkillColor_ClassSkillButUnavailable_ReturnsFalse()
    {
        Assert.False(Services.SkillDisplayHelper.ShouldUseClassSkillColor(
            isClassSkill: true, isUnavailable: true));
    }

    [Fact]
    public void ShouldUseClassSkillColor_Unavailable_ReturnsFalse()
    {
        Assert.False(Services.SkillDisplayHelper.ShouldUseClassSkillColor(
            isClassSkill: false, isUnavailable: true));
    }

    #endregion

    #region Generic FilterByName (#1799)

    private class TestNamedItem : Services.SkillDisplayHelper.INamedItem
    {
        public string Name { get; set; } = "";
    }

    [Fact]
    public void FilterByName_GenericItem_FiltersByNameCaseInsensitive()
    {
        var items = new List<TestNamedItem>
        {
            new() { Name = "Fireball" },
            new() { Name = "Fire Shield" },
            new() { Name = "Ice Storm" }
        };
        var result = Services.SkillDisplayHelper.FilterByName(items, "fire");
        Assert.Equal(2, result.Count);
        Assert.All(result, r => Assert.Contains("Fire", r.Name, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void FilterByName_GenericItem_EmptyFilter_ReturnsAll()
    {
        var items = new List<TestNamedItem>
        {
            new() { Name = "Alpha" },
            new() { Name = "Beta" }
        };
        var result = Services.SkillDisplayHelper.FilterByName(items, "");
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void FilterByName_GenericItem_NoMatch_ReturnsEmpty()
    {
        var items = new List<TestNamedItem>
        {
            new() { Name = "Alpha" },
            new() { Name = "Beta" }
        };
        var result = Services.SkillDisplayHelper.FilterByName(items, "zzz");
        Assert.Empty(result);
    }

    #endregion

    #region Skill Sort (#1881)

    [Fact]
    public void SortForDisplay_ClassSkillsFirst_ThenCrossClass_ThenUnavailable()
    {
        var skills = new List<Services.SkillDisplayHelper.SkillFilterItem>
        {
            new() { Name = "Use Magic Device", IsClassSkill = false, IsUnavailable = true },
            new() { Name = "Hide", IsClassSkill = false, IsUnavailable = false },
            new() { Name = "Concentration", IsClassSkill = true, IsUnavailable = false },
        };

        var result = Services.SkillDisplayHelper.SortForDisplay(skills);

        Assert.Equal("Concentration", result[0].Name);
        Assert.Equal("Hide", result[1].Name);
        Assert.Equal("Use Magic Device", result[2].Name);
    }

    [Fact]
    public void SortForDisplay_WithinBucket_OrdersAlphabetically()
    {
        var skills = new List<Services.SkillDisplayHelper.SkillFilterItem>
        {
            new() { Name = "Spellcraft", IsClassSkill = true, IsUnavailable = false },
            new() { Name = "Concentration", IsClassSkill = true, IsUnavailable = false },
            new() { Name = "Search", IsClassSkill = false, IsUnavailable = false },
            new() { Name = "Hide", IsClassSkill = false, IsUnavailable = false },
        };

        var result = Services.SkillDisplayHelper.SortForDisplay(skills);

        Assert.Equal("Concentration", result[0].Name);
        Assert.Equal("Spellcraft", result[1].Name);
        Assert.Equal("Hide", result[2].Name);
        Assert.Equal("Search", result[3].Name);
    }

    [Fact]
    public void SortForDisplay_UnavailableClassSkill_SortsToBottom()
    {
        // A class skill that is also unavailable should be in the unavailable bucket, not class
        var skills = new List<Services.SkillDisplayHelper.SkillFilterItem>
        {
            new() { Name = "Hide", IsClassSkill = false, IsUnavailable = false },
            new() { Name = "Tumble", IsClassSkill = true, IsUnavailable = true },
        };

        var result = Services.SkillDisplayHelper.SortForDisplay(skills);

        Assert.Equal("Hide", result[0].Name);
        Assert.Equal("Tumble", result[1].Name);
    }

    [Fact]
    public void SortForDisplay_EmptyList_ReturnsEmpty()
    {
        var result = Services.SkillDisplayHelper.SortForDisplay(new List<Services.SkillDisplayHelper.SkillFilterItem>());
        Assert.Empty(result);
    }

    #endregion

    #region Helpers

    private static List<Services.SkillDisplayHelper.SkillFilterItem> CreateSampleSkills()
    {
        return new List<Services.SkillDisplayHelper.SkillFilterItem>
        {
            new() { Name = "Concentration", IsClassSkill = true, IsUnavailable = false },
            new() { Name = "Discipline", IsClassSkill = true, IsUnavailable = false },
            new() { Name = "Hide", IsClassSkill = false, IsUnavailable = false },
            new() { Name = "Search", IsClassSkill = false, IsUnavailable = false },
            new() { Name = "Spellcraft", IsClassSkill = true, IsUnavailable = false },
            new() { Name = "Use Magic Device", IsClassSkill = false, IsUnavailable = true },
        };
    }

    #endregion
}
