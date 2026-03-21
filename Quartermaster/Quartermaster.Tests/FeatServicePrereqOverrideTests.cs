using Quartermaster.Services;
using Radoub.Formats.Utc;
using Radoub.TestUtilities.Builders;
using Radoub.TestUtilities.Mocks;
using Xunit;

namespace Quartermaster.Tests;

/// <summary>
/// Tests for FeatPrereqOverrides — projected creature state for wizards (#1744, #1738, #1800).
/// Validates that CheckFeatPrerequisites uses overridden values when provided.
/// </summary>
public class FeatServicePrereqOverrideTests
{
    private readonly MockGameDataService _mockGameData;
    private readonly FeatService _featService;

    public FeatServicePrereqOverrideTests()
    {
        _mockGameData = new MockGameDataService(includeSampleData: true);
        SetupTestData();
        var skillService = new SkillService(_mockGameData);
        var cacheService = new FeatCacheService();
        _featService = new FeatService(_mockGameData, skillService, cacheService);
    }

    private int CalculateBab(UtcFile creature)
    {
        int bab = 0;
        foreach (var cc in creature.ClassList)
            bab += cc.ClassLevel;
        return bab;
    }

    private string GetClassName(int classId) => classId switch
    {
        4 => "Fighter",
        10 => "Wizard",
        _ => $"Class {classId}"
    };

    #region Class Level Overrides (#1744)

    [Fact]
    public void Overrides_ProjectedClassLevel_MeetsClassLevelReq()
    {
        // Creature is Fighter 2, but we're projecting to Fighter 4
        var creature = new CreatureBuilder()
            .WithAbilities(14, 10, 10, 10, 10, 10)
            .WithClass(4, 2)
            .Build();

        // Feat 81 requires Fighter level 4
        var overrides = new FeatPrereqOverrides
        {
            ClassLevelOverrides = new Dictionary<int, int> { { 4, 4 } }
        };

        var result = _featService.CheckFeatPrerequisites(
            creature, 81, new HashSet<ushort>(),
            CalculateBab, GetClassName, overrides);

        Assert.True(result.AllMet);
    }

    [Fact]
    public void Overrides_ProjectedClassLevel_StillBelowReq_NotMet()
    {
        var creature = new CreatureBuilder()
            .WithAbilities(14, 10, 10, 10, 10, 10)
            .WithClass(4, 2)
            .Build();

        // Project to Fighter 3, still below 4
        var overrides = new FeatPrereqOverrides
        {
            ClassLevelOverrides = new Dictionary<int, int> { { 4, 3 } }
        };

        var result = _featService.CheckFeatPrerequisites(
            creature, 81, new HashSet<ushort>(),
            CalculateBab, GetClassName, overrides);

        Assert.False(result.AllMet);
    }

    [Fact]
    public void Overrides_ProjectedTotalLevel_MeetsMinLevel()
    {
        // Creature is Fighter 2, but projecting to Fighter 5
        var creature = new CreatureBuilder()
            .WithClass(4, 2)
            .Build();

        // Feat 80 requires character level 5
        var overrides = new FeatPrereqOverrides
        {
            TotalLevelOverride = 5
        };

        var result = _featService.CheckFeatPrerequisites(
            creature, 80, new HashSet<ushort>(),
            CalculateBab, GetClassName, overrides);

        Assert.True(result.AllMet);
    }

    [Fact]
    public void Overrides_ProjectedTotalLevel_BelowMinLevel_NotMet()
    {
        var creature = new CreatureBuilder()
            .WithClass(4, 2)
            .Build();

        var overrides = new FeatPrereqOverrides
        {
            TotalLevelOverride = 4 // Still below 5
        };

        var result = _featService.CheckFeatPrerequisites(
            creature, 80, new HashSet<ushort>(),
            CalculateBab, GetClassName, overrides);

        Assert.False(result.AllMet);
    }

    [Fact]
    public void Overrides_ProjectedTotalLevel_AffectsMaxLevel()
    {
        // Creature is Fighter 5, projecting to Fighter 12
        var creature = new CreatureBuilder()
            .WithClass(4, 5)
            .Build();

        // Feat 82 has MaxLevel 10; projected level 12 should fail
        var overrides = new FeatPrereqOverrides
        {
            TotalLevelOverride = 12
        };

        var result = _featService.CheckFeatPrerequisites(
            creature, 82, new HashSet<ushort>(),
            CalculateBab, GetClassName, overrides);

        Assert.False(result.AllMet);
    }

    [Fact]
    public void Overrides_ProjectedTotalLevel_AffectsEpic()
    {
        // Creature is Fighter 10, projecting to Fighter 21 (epic)
        var creature = new CreatureBuilder()
            .WithAbilities(14, 10, 10, 10, 10, 10)
            .WithClass(4, 10)
            .Build();

        // Feat 50 requires epic + BAB 5
        var overrides = new FeatPrereqOverrides
        {
            TotalLevelOverride = 21
        };

        var result = _featService.CheckFeatPrerequisites(
            creature, 50, new HashSet<ushort>(),
            c => 21, GetClassName, overrides);

        Assert.True(result.AllMet);
    }

    #endregion

    #region Skill Overrides (#1744)

    [Fact]
    public void Overrides_ProjectedSkills_MeetsSkillReq()
    {
        // Creature has no skills, but we project Discipline 5 and Tumble 3
        var creature = new CreatureBuilder()
            .WithAbilities(14, 14, 10, 10, 10, 10)
            .WithClass(4, 10)
            .Build();

        // Feat 70 requires Discipline 5 + Tumble 3
        var overrides = new FeatPrereqOverrides
        {
            SkillRankOverrides = new Dictionary<int, int> { { 3, 5 }, { 16, 3 } }
        };

        var result = _featService.CheckFeatPrerequisites(
            creature, 70, new HashSet<ushort>(),
            CalculateBab, GetClassName, overrides);

        Assert.True(result.AllMet);
    }

    [Fact]
    public void Overrides_ProjectedSkills_PartiallyMet_NotMet()
    {
        var creature = new CreatureBuilder()
            .WithAbilities(14, 14, 10, 10, 10, 10)
            .WithClass(4, 10)
            .Build();

        var overrides = new FeatPrereqOverrides
        {
            SkillRankOverrides = new Dictionary<int, int> { { 3, 5 }, { 16, 1 } } // Tumble too low
        };

        var result = _featService.CheckFeatPrerequisites(
            creature, 70, new HashSet<ushort>(),
            CalculateBab, GetClassName, overrides);

        Assert.False(result.AllMet);
    }

    #endregion

    #region Ability Score Overrides (#1800 - NCW projected abilities)

    [Fact]
    public void Overrides_ProjectedAbilities_MeetsStrReq()
    {
        // Creature has STR 10, but project STR 14
        var creature = new CreatureBuilder()
            .WithAbilities(10, 10, 10, 10, 10, 10)
            .WithClass(4, 1)
            .Build();

        // Feat 10 requires STR 13
        var overrides = new FeatPrereqOverrides
        {
            StrOverride = 14
        };

        var result = _featService.CheckFeatPrerequisites(
            creature, 10, new HashSet<ushort>(),
            CalculateBab, GetClassName, overrides);

        Assert.True(result.AllMet);
    }

    [Fact]
    public void Overrides_ProjectedAbilities_AllSix()
    {
        // Creature has all 8s, but project high enough values
        var creature = new CreatureBuilder()
            .WithAbilities(8, 8, 8, 8, 8, 8)
            .WithClass(4, 1)
            .Build();

        // Feat 91 requires all six abilities
        var overrides = new FeatPrereqOverrides
        {
            StrOverride = 12,
            DexOverride = 12,
            ConOverride = 12,
            IntOverride = 12,
            WisOverride = 14,
            ChaOverride = 12
        };

        var result = _featService.CheckFeatPrerequisites(
            creature, 91, new HashSet<ushort>(),
            CalculateBab, GetClassName, overrides);

        Assert.True(result.AllMet);
    }

    #endregion

    #region Spell Level Override (#1744 - projected class level affects spell access)

    [Fact]
    public void Overrides_ProjectedClassLevel_GrantsSpellAccess()
    {
        // Wizard at level 3 can't cast level 3 spells
        // But projected to Wizard 5, can cast level 3
        var creature = new CreatureBuilder()
            .WithAbilities(10, 10, 10, 16, 10, 10)
            .WithClass(10, 3)
            .Build();

        // Feat 60 requires level 3 spells
        var overrides = new FeatPrereqOverrides
        {
            ClassLevelOverrides = new Dictionary<int, int> { { 10, 5 } }
        };

        var result = _featService.CheckFeatPrerequisites(
            creature, 60, new HashSet<ushort>(),
            CalculateBab, GetClassName, overrides);

        Assert.True(result.AllMet);
    }

    #endregion

    #region No Overrides — backward compatibility

    [Fact]
    public void NoOverrides_BehavesIdentically()
    {
        var creature = new CreatureBuilder()
            .WithAbilities(14, 14, 10, 10, 10, 10)
            .WithClass(4, 4)
            .Build();

        // Feat 81 requires Fighter 4 — should pass without overrides
        var withoutOverrides = _featService.CheckFeatPrerequisites(
            creature, 81, new HashSet<ushort>(),
            CalculateBab, GetClassName);

        var withNullOverrides = _featService.CheckFeatPrerequisites(
            creature, 81, new HashSet<ushort>(),
            CalculateBab, GetClassName, null);

        Assert.Equal(withoutOverrides.AllMet, withNullOverrides.AllMet);
    }

    #endregion

    private void SetupTestData()
    {
        // Fighter class
        _mockGameData.Set2DAValue("classes", 4, "FeatsTable", "cls_feat_fight");
        _mockGameData.Set2DAValue("classes", 4, "Name", "204");
        _mockGameData.SetTlkString(204, "Fighter");

        // Wizard class
        _mockGameData.Set2DAValue("classes", 10, "FeatsTable", "cls_feat_wiz");
        _mockGameData.Set2DAValue("classes", 10, "SpellGainTable", "cls_spgn_wiz");
        _mockGameData.Set2DAValue("classes", 10, "Name", "210");
        _mockGameData.SetTlkString(210, "Wizard");

        // Wizard spell gain table
        _mockGameData.Set2DAValue("cls_spgn_wiz", 0, "SpellLevel0", "3");
        _mockGameData.Set2DAValue("cls_spgn_wiz", 0, "SpellLevel1", "1");
        _mockGameData.Set2DAValue("cls_spgn_wiz", 0, "SpellLevel2", "-");
        _mockGameData.Set2DAValue("cls_spgn_wiz", 0, "SpellLevel3", "-");
        _mockGameData.Set2DAValue("cls_spgn_wiz", 2, "SpellLevel2", "1");
        _mockGameData.Set2DAValue("cls_spgn_wiz", 2, "SpellLevel3", "-");
        _mockGameData.Set2DAValue("cls_spgn_wiz", 4, "SpellLevel3", "1");

        // Feat data
        // Feat 10: Power Attack (STR 13)
        _mockGameData.Set2DAValue("feat", 10, "LABEL", "Power_Attack");
        _mockGameData.Set2DAValue("feat", 10, "FEAT", "403");
        _mockGameData.Set2DAValue("feat", 10, "ALLCLASSESCANUSE", "1");
        _mockGameData.Set2DAValue("feat", 10, "MINSTR", "13");
        _mockGameData.SetTlkString(403, "Power Attack");

        // Feat 50: Epic Prowess (epic + BAB 5)
        _mockGameData.Set2DAValue("feat", 50, "LABEL", "Epic_Prowess");
        _mockGameData.Set2DAValue("feat", 50, "FEAT", "450");
        _mockGameData.Set2DAValue("feat", 50, "ALLCLASSESCANUSE", "1");
        _mockGameData.Set2DAValue("feat", 50, "MINATTACKBONUS", "5");
        _mockGameData.Set2DAValue("feat", 50, "PreReqEpic", "1");
        _mockGameData.SetTlkString(450, "Epic Prowess");

        // Feat 60: Spell Focus (level 3 spells)
        _mockGameData.Set2DAValue("feat", 60, "LABEL", "Spell_Focus");
        _mockGameData.Set2DAValue("feat", 60, "FEAT", "460");
        _mockGameData.Set2DAValue("feat", 60, "ALLCLASSESCANUSE", "1");
        _mockGameData.Set2DAValue("feat", 60, "MINSPELLLVL", "3");
        _mockGameData.SetTlkString(460, "Greater Spell Focus");

        // Feat 70: Spring Attack (Discipline 5 + Tumble 3)
        _mockGameData.Set2DAValue("feat", 70, "LABEL", "Spring_Attack");
        _mockGameData.Set2DAValue("feat", 70, "FEAT", "470");
        _mockGameData.Set2DAValue("feat", 70, "ALLCLASSESCANUSE", "1");
        _mockGameData.Set2DAValue("feat", 70, "REQSKILL", "3");
        _mockGameData.Set2DAValue("feat", 70, "ReqSkillMinRanks", "5");
        _mockGameData.Set2DAValue("feat", 70, "REQSKILL2", "16");
        _mockGameData.Set2DAValue("feat", 70, "ReqSkillMinRanks2", "3");
        _mockGameData.SetTlkString(470, "Spring Attack");

        // Feat 80: Extra Turning (level 5)
        _mockGameData.Set2DAValue("feat", 80, "LABEL", "Extra_Turning");
        _mockGameData.Set2DAValue("feat", 80, "FEAT", "480");
        _mockGameData.Set2DAValue("feat", 80, "ALLCLASSESCANUSE", "1");
        _mockGameData.Set2DAValue("feat", 80, "MinLevel", "5");
        _mockGameData.SetTlkString(480, "Extra Turning");

        // Feat 81: Weapon Spec (Fighter 4)
        _mockGameData.Set2DAValue("feat", 81, "LABEL", "Weapon_Spec");
        _mockGameData.Set2DAValue("feat", 81, "FEAT", "481");
        _mockGameData.Set2DAValue("feat", 81, "ALLCLASSESCANUSE", "0");
        _mockGameData.Set2DAValue("feat", 81, "MinLevel", "4");
        _mockGameData.Set2DAValue("feat", 81, "MinLevelClass", "4");
        _mockGameData.SetTlkString(481, "Weapon Specialization");

        // Feat 82: Newbie Feat (max level 10)
        _mockGameData.Set2DAValue("feat", 82, "LABEL", "Newbie_Feat");
        _mockGameData.Set2DAValue("feat", 82, "FEAT", "482");
        _mockGameData.Set2DAValue("feat", 82, "ALLCLASSESCANUSE", "1");
        _mockGameData.Set2DAValue("feat", 82, "MaxLevel", "10");
        _mockGameData.SetTlkString(482, "Newbie Feat");

        // Feat 91: Perfect Self (all six ability mins)
        _mockGameData.Set2DAValue("feat", 91, "LABEL", "Perfect_Self");
        _mockGameData.Set2DAValue("feat", 91, "FEAT", "491");
        _mockGameData.Set2DAValue("feat", 91, "ALLCLASSESCANUSE", "1");
        _mockGameData.Set2DAValue("feat", 91, "MINSTR", "12");
        _mockGameData.Set2DAValue("feat", 91, "MINDEX", "12");
        _mockGameData.Set2DAValue("feat", 91, "MININT", "12");
        _mockGameData.Set2DAValue("feat", 91, "MINWIS", "14");
        _mockGameData.Set2DAValue("feat", 91, "MINCON", "12");
        _mockGameData.Set2DAValue("feat", 91, "MINCHA", "12");
        _mockGameData.SetTlkString(491, "Perfect Self");

        // Skill names
        _mockGameData.Set2DAValue("skills", 3, "Name", "203");
        _mockGameData.SetTlkString(203, "Discipline");
        _mockGameData.Set2DAValue("skills", 16, "Name", "216");
        _mockGameData.SetTlkString(216, "Tumble");

        // Fighter feat table
        _mockGameData.Set2DAValue("cls_feat_fight", 0, "FeatIndex", "81");
        _mockGameData.Set2DAValue("cls_feat_fight", 0, "List", "1");
        _mockGameData.Set2DAValue("cls_feat_fight", 0, "GrantedOnLevel", "****");
    }
}
