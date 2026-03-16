using Quartermaster.Services;
using Radoub.Formats.Utc;
using Radoub.TestUtilities.Builders;
using Radoub.TestUtilities.Mocks;
using Xunit;

namespace Quartermaster.Tests;

/// <summary>
/// Advanced FeatService tests covering OR-required feat chains, epic validation,
/// spell prerequisites, multi-level prerequisite resolution, auto-assign, and
/// multiclass scenarios. Addresses coverage gaps identified in #1654/#1658.
/// </summary>
public class FeatServiceAdvancedTests
{
    private readonly MockGameDataService _mockGameData;
    private readonly SkillService _skillService;
    private readonly FeatCacheService _cacheService;
    private readonly FeatService _featService;

    public FeatServiceAdvancedTests()
    {
        _mockGameData = new MockGameDataService(includeSampleData: true);
        SetupAdvancedFeatData();
        _skillService = new SkillService(_mockGameData);
        _cacheService = new FeatCacheService();
        _featService = new FeatService(_mockGameData, _skillService, _cacheService);
    }

    private void SetupAdvancedFeatData()
    {
        // === Base feats (reused from basic tests for prerequisite chains) ===

        // Feat 0: Alertness (universal)
        _mockGameData.Set2DAValue("feat", 0, "LABEL", "Alertness");
        _mockGameData.Set2DAValue("feat", 0, "FEAT", "400");
        _mockGameData.Set2DAValue("feat", 0, "TOOLSCATEGORIES", "6");
        _mockGameData.Set2DAValue("feat", 0, "ALLCLASSESCANUSE", "1");
        _mockGameData.SetTlkString(400, "Alertness");

        // Feat 5: Blind-Fight (not universal, in Fighter table)
        _mockGameData.Set2DAValue("feat", 5, "LABEL", "Blind_Fight");
        _mockGameData.Set2DAValue("feat", 5, "FEAT", "401");
        _mockGameData.Set2DAValue("feat", 5, "TOOLSCATEGORIES", "1");
        _mockGameData.Set2DAValue("feat", 5, "ALLCLASSESCANUSE", "0");
        _mockGameData.SetTlkString(401, "Blind-Fight");

        // Feat 10: Power Attack (requires STR 13)
        _mockGameData.Set2DAValue("feat", 10, "LABEL", "Power_Attack");
        _mockGameData.Set2DAValue("feat", 10, "FEAT", "403");
        _mockGameData.Set2DAValue("feat", 10, "TOOLSCATEGORIES", "1");
        _mockGameData.Set2DAValue("feat", 10, "ALLCLASSESCANUSE", "1");
        _mockGameData.Set2DAValue("feat", 10, "MINSTR", "13");
        _mockGameData.SetTlkString(403, "Power Attack");

        // Feat 7: Cleave (requires Power Attack)
        _mockGameData.Set2DAValue("feat", 7, "LABEL", "Cleave");
        _mockGameData.Set2DAValue("feat", 7, "FEAT", "402");
        _mockGameData.Set2DAValue("feat", 7, "TOOLSCATEGORIES", "1");
        _mockGameData.Set2DAValue("feat", 7, "ALLCLASSESCANUSE", "1");
        _mockGameData.Set2DAValue("feat", 7, "PREREQFEAT1", "10");
        _mockGameData.SetTlkString(402, "Cleave");

        // Feat 11: Dodge (requires DEX 13)
        _mockGameData.Set2DAValue("feat", 11, "LABEL", "Dodge");
        _mockGameData.Set2DAValue("feat", 11, "FEAT", "404");
        _mockGameData.Set2DAValue("feat", 11, "TOOLSCATEGORIES", "3");
        _mockGameData.Set2DAValue("feat", 11, "ALLCLASSESCANUSE", "1");
        _mockGameData.Set2DAValue("feat", 11, "MINDEX", "13");
        _mockGameData.SetTlkString(404, "Dodge");

        // === OR-required feat chains ===

        // Feat 40: Mobility (OR: Dodge OR Alertness)
        _mockGameData.Set2DAValue("feat", 40, "LABEL", "Mobility");
        _mockGameData.Set2DAValue("feat", 40, "FEAT", "440");
        _mockGameData.Set2DAValue("feat", 40, "TOOLSCATEGORIES", "3");
        _mockGameData.Set2DAValue("feat", 40, "ALLCLASSESCANUSE", "1");
        _mockGameData.Set2DAValue("feat", 40, "OrReqFeat0", "11"); // Dodge
        _mockGameData.Set2DAValue("feat", 40, "OrReqFeat1", "0");  // Alertness
        _mockGameData.SetTlkString(440, "Mobility");

        // Feat 41: Wide OR chain (5 OR-required feats)
        _mockGameData.Set2DAValue("feat", 41, "LABEL", "Wide_Or_Feat");
        _mockGameData.Set2DAValue("feat", 41, "FEAT", "441");
        _mockGameData.Set2DAValue("feat", 41, "TOOLSCATEGORIES", "1");
        _mockGameData.Set2DAValue("feat", 41, "ALLCLASSESCANUSE", "1");
        _mockGameData.Set2DAValue("feat", 41, "OrReqFeat0", "0");  // Alertness
        _mockGameData.Set2DAValue("feat", 41, "OrReqFeat1", "5");  // Blind-Fight
        _mockGameData.Set2DAValue("feat", 41, "OrReqFeat2", "7");  // Cleave
        _mockGameData.Set2DAValue("feat", 41, "OrReqFeat3", "10"); // Power Attack
        _mockGameData.Set2DAValue("feat", 41, "OrReqFeat4", "11"); // Dodge
        _mockGameData.SetTlkString(441, "Wide Or Feat");

        // Feat 42: Mixed AND + OR (requires Power Attack AND one of Dodge/Alertness)
        _mockGameData.Set2DAValue("feat", 42, "LABEL", "Mixed_Prereqs");
        _mockGameData.Set2DAValue("feat", 42, "FEAT", "442");
        _mockGameData.Set2DAValue("feat", 42, "TOOLSCATEGORIES", "1");
        _mockGameData.Set2DAValue("feat", 42, "ALLCLASSESCANUSE", "1");
        _mockGameData.Set2DAValue("feat", 42, "PREREQFEAT1", "10"); // Power Attack (AND)
        _mockGameData.Set2DAValue("feat", 42, "OrReqFeat0", "11"); // Dodge (OR)
        _mockGameData.Set2DAValue("feat", 42, "OrReqFeat1", "0");  // Alertness (OR)
        _mockGameData.SetTlkString(442, "Mixed Prerequisites");

        // Feat 43: Two AND prerequisites (requires Power Attack AND Dodge)
        _mockGameData.Set2DAValue("feat", 43, "LABEL", "Dual_And_Prereqs");
        _mockGameData.Set2DAValue("feat", 43, "FEAT", "443");
        _mockGameData.Set2DAValue("feat", 43, "TOOLSCATEGORIES", "1");
        _mockGameData.Set2DAValue("feat", 43, "ALLCLASSESCANUSE", "1");
        _mockGameData.Set2DAValue("feat", 43, "PREREQFEAT1", "10"); // Power Attack
        _mockGameData.Set2DAValue("feat", 43, "PREREQFEAT2", "11"); // Dodge
        _mockGameData.SetTlkString(443, "Dual And Prerequisites");

        // === Epic feats ===

        // Feat 50: Epic Prowess (epic + BAB 5)
        _mockGameData.Set2DAValue("feat", 50, "LABEL", "Epic_Prowess");
        _mockGameData.Set2DAValue("feat", 50, "FEAT", "450");
        _mockGameData.Set2DAValue("feat", 50, "TOOLSCATEGORIES", "1");
        _mockGameData.Set2DAValue("feat", 50, "ALLCLASSESCANUSE", "1");
        _mockGameData.Set2DAValue("feat", 50, "MINATTACKBONUS", "5");
        _mockGameData.Set2DAValue("feat", 50, "PreReqEpic", "1");
        _mockGameData.SetTlkString(450, "Epic Prowess");

        // Feat 51: Epic feat with ability + feat requirement
        _mockGameData.Set2DAValue("feat", 51, "LABEL", "Epic_Devastating_Critical");
        _mockGameData.Set2DAValue("feat", 51, "FEAT", "451");
        _mockGameData.Set2DAValue("feat", 51, "TOOLSCATEGORIES", "1");
        _mockGameData.Set2DAValue("feat", 51, "ALLCLASSESCANUSE", "1");
        _mockGameData.Set2DAValue("feat", 51, "MINSTR", "25");
        _mockGameData.Set2DAValue("feat", 51, "PREREQFEAT1", "10"); // Power Attack
        _mockGameData.Set2DAValue("feat", 51, "PreReqEpic", "1");
        _mockGameData.SetTlkString(451, "Devastating Critical");

        // Feat 52: Non-epic feat (PreReqEpic absent)
        _mockGameData.Set2DAValue("feat", 52, "LABEL", "Not_Epic");
        _mockGameData.Set2DAValue("feat", 52, "FEAT", "452");
        _mockGameData.Set2DAValue("feat", 52, "TOOLSCATEGORIES", "6");
        _mockGameData.Set2DAValue("feat", 52, "ALLCLASSESCANUSE", "1");
        _mockGameData.SetTlkString(452, "Not Epic");

        // === Spell level prerequisites ===

        // Feat 60: Requires ability to cast level 3 spells
        _mockGameData.Set2DAValue("feat", 60, "LABEL", "Spell_Focus");
        _mockGameData.Set2DAValue("feat", 60, "FEAT", "460");
        _mockGameData.Set2DAValue("feat", 60, "TOOLSCATEGORIES", "4");
        _mockGameData.Set2DAValue("feat", 60, "ALLCLASSESCANUSE", "1");
        _mockGameData.Set2DAValue("feat", 60, "MINSPELLLVL", "3");
        _mockGameData.SetTlkString(460, "Greater Spell Focus");

        // Feat 61: Requires level 1 spells (low bar)
        _mockGameData.Set2DAValue("feat", 61, "LABEL", "Combat_Casting");
        _mockGameData.Set2DAValue("feat", 61, "FEAT", "461");
        _mockGameData.Set2DAValue("feat", 61, "TOOLSCATEGORIES", "4");
        _mockGameData.Set2DAValue("feat", 61, "ALLCLASSESCANUSE", "1");
        _mockGameData.Set2DAValue("feat", 61, "MINSPELLLVL", "1");
        _mockGameData.SetTlkString(461, "Combat Casting");

        // === Dual skill requirements ===

        // Feat 70: Requires Discipline (3) 5 ranks + Tumble (16) 3 ranks
        _mockGameData.Set2DAValue("feat", 70, "LABEL", "Spring_Attack");
        _mockGameData.Set2DAValue("feat", 70, "FEAT", "470");
        _mockGameData.Set2DAValue("feat", 70, "TOOLSCATEGORIES", "2");
        _mockGameData.Set2DAValue("feat", 70, "ALLCLASSESCANUSE", "1");
        _mockGameData.Set2DAValue("feat", 70, "REQSKILL", "3");       // Discipline
        _mockGameData.Set2DAValue("feat", 70, "ReqSkillMinRanks", "5");
        _mockGameData.Set2DAValue("feat", 70, "REQSKILL2", "16");     // Tumble
        _mockGameData.Set2DAValue("feat", 70, "ReqSkillMinRanks2", "3");
        _mockGameData.SetTlkString(470, "Spring Attack");

        // === Level requirements ===

        // Feat 80: Requires character level 5
        _mockGameData.Set2DAValue("feat", 80, "LABEL", "Extra_Turning");
        _mockGameData.Set2DAValue("feat", 80, "FEAT", "480");
        _mockGameData.Set2DAValue("feat", 80, "TOOLSCATEGORIES", "6");
        _mockGameData.Set2DAValue("feat", 80, "ALLCLASSESCANUSE", "1");
        _mockGameData.Set2DAValue("feat", 80, "MinLevel", "5");
        _mockGameData.SetTlkString(480, "Extra Turning");

        // Feat 81: Requires class-specific level (Fighter level 4)
        _mockGameData.Set2DAValue("feat", 81, "LABEL", "Weapon_Spec");
        _mockGameData.Set2DAValue("feat", 81, "FEAT", "481");
        _mockGameData.Set2DAValue("feat", 81, "TOOLSCATEGORIES", "1");
        _mockGameData.Set2DAValue("feat", 81, "ALLCLASSESCANUSE", "0");
        _mockGameData.Set2DAValue("feat", 81, "MinLevel", "4");
        _mockGameData.Set2DAValue("feat", 81, "MinLevelClass", "4"); // Fighter
        _mockGameData.SetTlkString(481, "Weapon Specialization");

        // Feat 82: Has MaxLevel cap (only below level 10)
        _mockGameData.Set2DAValue("feat", 82, "LABEL", "Newbie_Feat");
        _mockGameData.Set2DAValue("feat", 82, "FEAT", "482");
        _mockGameData.Set2DAValue("feat", 82, "TOOLSCATEGORIES", "6");
        _mockGameData.Set2DAValue("feat", 82, "ALLCLASSESCANUSE", "1");
        _mockGameData.Set2DAValue("feat", 82, "MaxLevel", "10");
        _mockGameData.SetTlkString(482, "Newbie Feat");

        // Feat 83: MinLevel + MaxLevel range (level 5-15 only)
        _mockGameData.Set2DAValue("feat", 83, "LABEL", "Mid_Level_Feat");
        _mockGameData.Set2DAValue("feat", 83, "FEAT", "483");
        _mockGameData.Set2DAValue("feat", 83, "TOOLSCATEGORIES", "6");
        _mockGameData.Set2DAValue("feat", 83, "ALLCLASSESCANUSE", "1");
        _mockGameData.Set2DAValue("feat", 83, "MinLevel", "5");
        _mockGameData.Set2DAValue("feat", 83, "MaxLevel", "15");
        _mockGameData.SetTlkString(483, "Mid-Level Feat");

        // === Multiple ability prerequisites ===

        // Feat 90: Requires STR 13 + DEX 13 (like Whirlwind Attack in NWN)
        _mockGameData.Set2DAValue("feat", 90, "LABEL", "Whirlwind_Attack");
        _mockGameData.Set2DAValue("feat", 90, "FEAT", "490");
        _mockGameData.Set2DAValue("feat", 90, "TOOLSCATEGORIES", "2");
        _mockGameData.Set2DAValue("feat", 90, "ALLCLASSESCANUSE", "1");
        _mockGameData.Set2DAValue("feat", 90, "MINSTR", "13");
        _mockGameData.Set2DAValue("feat", 90, "MINDEX", "13");
        _mockGameData.SetTlkString(490, "Whirlwind Attack");

        // Feat 91: All six ability minimums
        _mockGameData.Set2DAValue("feat", 91, "LABEL", "Perfect_Self");
        _mockGameData.Set2DAValue("feat", 91, "FEAT", "491");
        _mockGameData.Set2DAValue("feat", 91, "TOOLSCATEGORIES", "6");
        _mockGameData.Set2DAValue("feat", 91, "ALLCLASSESCANUSE", "1");
        _mockGameData.Set2DAValue("feat", 91, "MINSTR", "12");
        _mockGameData.Set2DAValue("feat", 91, "MINDEX", "12");
        _mockGameData.Set2DAValue("feat", 91, "MININT", "12");
        _mockGameData.Set2DAValue("feat", 91, "MINWIS", "14");
        _mockGameData.Set2DAValue("feat", 91, "MINCON", "12");
        _mockGameData.Set2DAValue("feat", 91, "MINCHA", "12");
        _mockGameData.SetTlkString(491, "Perfect Self");

        // === Class & spell gain tables ===

        // Fighter (classId=4) — no spell gain table
        _mockGameData.Set2DAValue("classes", 4, "FeatsTable", "cls_feat_fight");
        _mockGameData.Set2DAValue("classes", 4, "BonusFeatsTable", "cls_bfeat_fight");

        // Wizard (classId=10) — has spell gain table
        _mockGameData.Set2DAValue("classes", 10, "FeatsTable", "cls_feat_wiz");
        _mockGameData.Set2DAValue("classes", 10, "SpellGainTable", "cls_spgn_wiz");
        _mockGameData.Set2DAValue("classes", 10, "BonusFeatsTable", "cls_bfeat_wiz");

        // Cleric (classId=2) — has spell gain table
        _mockGameData.Set2DAValue("classes", 2, "FeatsTable", "cls_feat_cler");
        _mockGameData.Set2DAValue("classes", 2, "SpellGainTable", "cls_spgn_cler");
        _mockGameData.Set2DAValue("classes", 2, "BonusFeatsTable", "cls_bfeat_cler");

        // Rogue (classId=8) — no spells, has feat table
        _mockGameData.Set2DAValue("classes", 8, "FeatsTable", "cls_feat_rog");
        _mockGameData.Set2DAValue("classes", 8, "BonusFeatsTable", "cls_bfeat_rog");

        // Wizard spell gain table:
        // Level 1 (row 0): 3 level-0 slots, 1 level-1 slot
        _mockGameData.Set2DAValue("cls_spgn_wiz", 0, "SpellLevel0", "3");
        _mockGameData.Set2DAValue("cls_spgn_wiz", 0, "SpellLevel1", "1");
        _mockGameData.Set2DAValue("cls_spgn_wiz", 0, "SpellLevel2", "-");
        _mockGameData.Set2DAValue("cls_spgn_wiz", 0, "SpellLevel3", "-");
        // Level 3 (row 2): gains level-2 slots
        _mockGameData.Set2DAValue("cls_spgn_wiz", 2, "SpellLevel1", "2");
        _mockGameData.Set2DAValue("cls_spgn_wiz", 2, "SpellLevel2", "1");
        _mockGameData.Set2DAValue("cls_spgn_wiz", 2, "SpellLevel3", "-");
        // Level 5 (row 4): gains level-3 slots
        _mockGameData.Set2DAValue("cls_spgn_wiz", 4, "SpellLevel1", "3");
        _mockGameData.Set2DAValue("cls_spgn_wiz", 4, "SpellLevel2", "2");
        _mockGameData.Set2DAValue("cls_spgn_wiz", 4, "SpellLevel3", "1");

        // Cleric spell gain table (earlier access to some levels):
        // Level 1 (row 0): level-0 and level-1 spells
        _mockGameData.Set2DAValue("cls_spgn_cler", 0, "SpellLevel0", "3");
        _mockGameData.Set2DAValue("cls_spgn_cler", 0, "SpellLevel1", "1");
        _mockGameData.Set2DAValue("cls_spgn_cler", 0, "SpellLevel2", "-");
        _mockGameData.Set2DAValue("cls_spgn_cler", 0, "SpellLevel3", "-");
        // Level 5 (row 4): gains level-3 spells
        _mockGameData.Set2DAValue("cls_spgn_cler", 4, "SpellLevel3", "1");

        // === Class feat tables ===

        // Fighter feat table
        _mockGameData.Set2DAValue("cls_feat_fight", 0, "FeatIndex", "5");
        _mockGameData.Set2DAValue("cls_feat_fight", 0, "List", "1");
        _mockGameData.Set2DAValue("cls_feat_fight", 0, "GrantedOnLevel", "****");
        _mockGameData.Set2DAValue("cls_feat_fight", 1, "FeatIndex", "10");
        _mockGameData.Set2DAValue("cls_feat_fight", 1, "List", "1");
        _mockGameData.Set2DAValue("cls_feat_fight", 1, "GrantedOnLevel", "****");
        _mockGameData.Set2DAValue("cls_feat_fight", 2, "FeatIndex", "2");
        _mockGameData.Set2DAValue("cls_feat_fight", 2, "List", "3");
        _mockGameData.Set2DAValue("cls_feat_fight", 2, "GrantedOnLevel", "1");

        // Wizard feat table — includes metamagic feats
        _mockGameData.Set2DAValue("cls_feat_wiz", 0, "FeatIndex", "0");
        _mockGameData.Set2DAValue("cls_feat_wiz", 0, "List", "1"); // Bonus pool
        _mockGameData.Set2DAValue("cls_feat_wiz", 0, "GrantedOnLevel", "****");
        _mockGameData.Set2DAValue("cls_feat_wiz", 1, "FeatIndex", "60");
        _mockGameData.Set2DAValue("cls_feat_wiz", 1, "List", "1"); // Spell Focus in bonus pool
        _mockGameData.Set2DAValue("cls_feat_wiz", 1, "GrantedOnLevel", "****");

        // Rogue feat table — includes a feat not in fighter table
        _mockGameData.Set2DAValue("cls_feat_rog", 0, "FeatIndex", "11");
        _mockGameData.Set2DAValue("cls_feat_rog", 0, "List", "1"); // Dodge in rogue bonus pool
        _mockGameData.Set2DAValue("cls_feat_rog", 0, "GrantedOnLevel", "****");

        // Fighter bonus feat table
        _mockGameData.Set2DAValue("cls_bfeat_fight", 0, "Bonus", "1"); // Level 1
        _mockGameData.Set2DAValue("cls_bfeat_fight", 1, "Bonus", "1"); // Level 2
        _mockGameData.Set2DAValue("cls_bfeat_fight", 2, "Bonus", "0"); // Level 3
        _mockGameData.Set2DAValue("cls_bfeat_fight", 3, "Bonus", "1"); // Level 4

        // Wizard bonus feat table
        _mockGameData.Set2DAValue("cls_bfeat_wiz", 0, "Bonus", "0");
        _mockGameData.Set2DAValue("cls_bfeat_wiz", 1, "Bonus", "0");
        _mockGameData.Set2DAValue("cls_bfeat_wiz", 2, "Bonus", "0");
        _mockGameData.Set2DAValue("cls_bfeat_wiz", 3, "Bonus", "0");
        _mockGameData.Set2DAValue("cls_bfeat_wiz", 4, "Bonus", "1"); // Level 5

        // Rogue bonus feat table (no bonus feats)
        _mockGameData.Set2DAValue("cls_bfeat_rog", 0, "Bonus", "0");
        _mockGameData.Set2DAValue("cls_bfeat_rog", 1, "Bonus", "0");

        // Race tables
        _mockGameData.Set2DAValue("racialtypes", 6, "FeatsTable", "race_feat_human");
        _mockGameData.Set2DAValue("racialtypes", 6, "ExtraFeatsAtFirstLevel", "1");
        _mockGameData.Set2DAValue("race_feat_human", 0, "FeatIndex", "99");

        _mockGameData.Set2DAValue("racialtypes", 0, "FeatsTable", "race_feat_dwarf");
        _mockGameData.Set2DAValue("racialtypes", 0, "ExtraFeatsAtFirstLevel", "0");
        _mockGameData.Set2DAValue("race_feat_dwarf", 0, "FeatIndex", "88");

        // Skill names
        _mockGameData.Set2DAValue("skills", 3, "Name", "203");
        _mockGameData.SetTlkString(203, "Discipline");
        _mockGameData.Set2DAValue("skills", 16, "Name", "216");
        _mockGameData.SetTlkString(216, "Tumble");

        // Package data for auto-assign tests
        _mockGameData.Set2DAValue("packages", 1, "FeatPref2DA", "pkg_feat_fight");
        // Package prefers Power Attack, then Cleave, then Dodge
        _mockGameData.Set2DAValue("pkg_feat_fight", 0, "FeatIndex", "10"); // Power Attack
        _mockGameData.Set2DAValue("pkg_feat_fight", 1, "FeatIndex", "7");  // Cleave
        _mockGameData.Set2DAValue("pkg_feat_fight", 2, "FeatIndex", "11"); // Dodge

        // Class name for level prerequisite display
        _mockGameData.Set2DAValue("classes", 4, "Name", "204");
        _mockGameData.SetTlkString(204, "Fighter");
        _mockGameData.Set2DAValue("classes", 10, "Name", "210");
        _mockGameData.SetTlkString(210, "Wizard");
    }

    // Helper: simple BAB calculator for test callbacks
    private int CalculateBab(UtcFile creature)
    {
        int bab = 0;
        foreach (var cc in creature.ClassList)
            bab += cc.ClassLevel; // Simplification: 1 BAB per level
        return bab;
    }

    private string GetClassName(int classId) => classId switch
    {
        4 => "Fighter",
        10 => "Wizard",
        2 => "Cleric",
        8 => "Rogue",
        _ => $"Class {classId}"
    };

    #region OR-Required Feat Chains

    [Fact]
    public void CheckPrereqs_OrRequired_HasBoth_AllMet()
    {
        var creature = new CreatureBuilder()
            .WithAbilities(10, 14, 10, 10, 10, 10)
            .WithClass(4, 1)
            .Build();

        var feats = new HashSet<ushort> { 0, 11 }; // Has both Alertness AND Dodge

        var result = _featService.CheckFeatPrerequisites(
            creature, 40, feats, CalculateBab, GetClassName);

        Assert.True(result.AllMet);
        Assert.Equal(2, result.OrRequiredFeatsMet.Count);
        Assert.All(result.OrRequiredFeatsMet, item => Assert.True(item.Met));
    }

    [Fact]
    public void CheckPrereqs_OrRequired_5Options_HasMiddleOne_AllMet()
    {
        var creature = new CreatureBuilder()
            .WithAbilities(10, 10, 10, 10, 10, 10)
            .WithClass(4, 1)
            .Build();

        var feats = new HashSet<ushort> { 7 }; // Only has Cleave (OrReqFeat2)

        var result = _featService.CheckFeatPrerequisites(
            creature, 41, feats, CalculateBab, GetClassName);

        Assert.True(result.AllMet);
        Assert.Equal(5, result.OrRequiredFeatsMet.Count);
    }

    [Fact]
    public void CheckPrereqs_OrRequired_5Options_HasNone_NotMet()
    {
        var creature = new CreatureBuilder()
            .WithAbilities(10, 10, 10, 10, 10, 10)
            .WithClass(4, 1)
            .Build();

        var feats = new HashSet<ushort>(); // Has none of the 5

        var result = _featService.CheckFeatPrerequisites(
            creature, 41, feats, CalculateBab, GetClassName);

        Assert.False(result.AllMet);
        Assert.All(result.OrRequiredFeatsMet, item => Assert.False(item.Met));
    }

    [Fact]
    public void CheckPrereqs_MixedAndOr_HasAndButNotOr_NotMet()
    {
        var creature = new CreatureBuilder()
            .WithAbilities(14, 10, 10, 10, 10, 10)
            .WithClass(4, 1)
            .Build();

        var feats = new HashSet<ushort> { 10 }; // Has Power Attack but not Dodge or Alertness

        var result = _featService.CheckFeatPrerequisites(
            creature, 42, feats, CalculateBab, GetClassName);

        Assert.False(result.AllMet);
        // AND requirement met
        Assert.Single(result.RequiredFeatsMet);
        Assert.True(result.RequiredFeatsMet[0].Met);
        // OR requirement not met
        Assert.All(result.OrRequiredFeatsMet, item => Assert.False(item.Met));
    }

    [Fact]
    public void CheckPrereqs_MixedAndOr_HasOrButNotAnd_NotMet()
    {
        var creature = new CreatureBuilder()
            .WithAbilities(14, 10, 10, 10, 10, 10)
            .WithClass(4, 1)
            .Build();

        var feats = new HashSet<ushort> { 0 }; // Has Alertness (OR) but not Power Attack (AND)

        var result = _featService.CheckFeatPrerequisites(
            creature, 42, feats, CalculateBab, GetClassName);

        Assert.False(result.AllMet);
        Assert.Single(result.RequiredFeatsMet);
        Assert.False(result.RequiredFeatsMet[0].Met); // Power Attack missing
    }

    [Fact]
    public void CheckPrereqs_MixedAndOr_HasBothAndAndOr_AllMet()
    {
        var creature = new CreatureBuilder()
            .WithAbilities(14, 10, 10, 10, 10, 10)
            .WithClass(4, 1)
            .Build();

        var feats = new HashSet<ushort> { 10, 11 }; // Power Attack + Dodge

        var result = _featService.CheckFeatPrerequisites(
            creature, 42, feats, CalculateBab, GetClassName);

        Assert.True(result.AllMet);
    }

    [Fact]
    public void CheckPrereqs_TwoAndPrereqs_HasBoth_AllMet()
    {
        var creature = new CreatureBuilder()
            .WithAbilities(14, 14, 10, 10, 10, 10)
            .WithClass(4, 1)
            .Build();

        var feats = new HashSet<ushort> { 10, 11 }; // Power Attack + Dodge

        var result = _featService.CheckFeatPrerequisites(
            creature, 43, feats, CalculateBab, GetClassName);

        Assert.True(result.AllMet);
        Assert.Equal(2, result.RequiredFeatsMet.Count);
        Assert.All(result.RequiredFeatsMet, item => Assert.True(item.Met));
    }

    [Fact]
    public void CheckPrereqs_TwoAndPrereqs_HasOnlyFirst_NotMet()
    {
        var creature = new CreatureBuilder()
            .WithAbilities(14, 14, 10, 10, 10, 10)
            .WithClass(4, 1)
            .Build();

        var feats = new HashSet<ushort> { 10 }; // Only Power Attack, missing Dodge

        var result = _featService.CheckFeatPrerequisites(
            creature, 43, feats, CalculateBab, GetClassName);

        Assert.False(result.AllMet);
        Assert.True(result.RequiredFeatsMet[0].Met);   // Power Attack: met
        Assert.False(result.RequiredFeatsMet[1].Met);   // Dodge: not met
    }

    [Fact]
    public void GetFeatPrerequisites_OrRequired_ParsesAll5()
    {
        var prereqs = _featService.GetFeatPrerequisites(41);
        Assert.Equal(5, prereqs.OrRequiredFeats.Count);
        Assert.Contains(0, prereqs.OrRequiredFeats);
        Assert.Contains(5, prereqs.OrRequiredFeats);
        Assert.Contains(7, prereqs.OrRequiredFeats);
        Assert.Contains(10, prereqs.OrRequiredFeats);
        Assert.Contains(11, prereqs.OrRequiredFeats);
    }

    [Fact]
    public void GetFeatPrerequisites_MixedAndOr_ParsesBothTypes()
    {
        var prereqs = _featService.GetFeatPrerequisites(42);
        Assert.Single(prereqs.RequiredFeats);
        Assert.Contains(10, prereqs.RequiredFeats);
        Assert.Equal(2, prereqs.OrRequiredFeats.Count);
        Assert.Contains(11, prereqs.OrRequiredFeats);
        Assert.Contains(0, prereqs.OrRequiredFeats);
    }

    #endregion

    #region Epic Feat Validation

    [Fact]
    public void CheckPrereqs_EpicFeat_Level21_MeetsBab_AllMet()
    {
        var creature = new CreatureBuilder()
            .WithAbilities(14, 10, 10, 10, 10, 10)
            .WithClass(4, 21) // Fighter 21
            .Build();

        var result = _featService.CheckFeatPrerequisites(
            creature, 50, new HashSet<ushort>(),
            CalculateBab, GetClassName);

        Assert.True(result.AllMet);
    }

    [Fact]
    public void CheckPrereqs_EpicFeat_Level20_NotMet()
    {
        var creature = new CreatureBuilder()
            .WithAbilities(14, 10, 10, 10, 10, 10)
            .WithClass(4, 20) // Fighter 20 — not epic
            .Build();

        var result = _featService.CheckFeatPrerequisites(
            creature, 50, new HashSet<ushort>(),
            CalculateBab, GetClassName);

        Assert.False(result.AllMet);
        Assert.Contains(result.OtherRequirements, r => r.Description == "Epic (level 21+)" && !r.Met!.Value);
    }

    [Fact]
    public void CheckPrereqs_EpicFeat_MulticlassLevel21_AllMet()
    {
        // Fighter 15 + Wizard 6 = total 21 (epic)
        var creature = new CreatureBuilder()
            .WithAbilities(14, 10, 10, 10, 10, 10)
            .WithClass(4, 15) // Fighter
            .WithClass(10, 6)  // Wizard
            .Build();

        var result = _featService.CheckFeatPrerequisites(
            creature, 50, new HashSet<ushort>(),
            CalculateBab, GetClassName);

        Assert.True(result.AllMet);
    }

    [Fact]
    public void CheckPrereqs_EpicWithAbilityAndFeat_AllMet()
    {
        var creature = new CreatureBuilder()
            .WithAbilities(26, 10, 10, 10, 10, 10) // STR 26 >= 25
            .WithClass(4, 21)
            .Build();

        var feats = new HashSet<ushort> { 10 }; // Has Power Attack

        var result = _featService.CheckFeatPrerequisites(
            creature, 51, feats, CalculateBab, GetClassName);

        Assert.True(result.AllMet);
    }

    [Fact]
    public void CheckPrereqs_EpicWithAbilityAndFeat_LowStr_NotMet()
    {
        var creature = new CreatureBuilder()
            .WithAbilities(20, 10, 10, 10, 10, 10) // STR 20 < 25
            .WithClass(4, 21)
            .Build();

        var feats = new HashSet<ushort> { 10 }; // Has Power Attack

        var result = _featService.CheckFeatPrerequisites(
            creature, 51, feats, CalculateBab, GetClassName);

        Assert.False(result.AllMet);
        Assert.Contains(result.AbilityRequirements, r => r.Description == "STR 25+" && !r.Met);
    }

    [Fact]
    public void CheckPrereqs_EpicFeat_MetsBabButNotLevel_NotMet()
    {
        // High BAB from some source, but level too low
        var creature = new CreatureBuilder()
            .WithAbilities(14, 10, 10, 10, 10, 10)
            .WithClass(4, 10) // Fighter 10, BAB=10 (>5) but not epic
            .Build();

        var result = _featService.CheckFeatPrerequisites(
            creature, 50, new HashSet<ushort>(),
            CalculateBab, GetClassName);

        Assert.False(result.AllMet);
        // BAB met, epic not met
        Assert.Contains(result.OtherRequirements, r => r.Description == "BAB 5+" && r.Met!.Value);
        Assert.Contains(result.OtherRequirements, r => r.Description == "Epic (level 21+)" && !r.Met!.Value);
    }

    [Fact]
    public void GetFeatPrerequisites_NonEpic_RequiresEpicFalse()
    {
        var prereqs = _featService.GetFeatPrerequisites(52);
        Assert.False(prereqs.RequiresEpic);
    }

    #endregion

    #region Spell Level Prerequisites

    [Fact]
    public void CheckPrereqs_SpellLevel_WizardLevel5_CanCastLevel3_AllMet()
    {
        var creature = new CreatureBuilder()
            .WithAbilities(10, 10, 10, 16, 10, 10) // INT 16 for Wizard
            .WithClass(10, 5) // Wizard 5
            .Build();

        var result = _featService.CheckFeatPrerequisites(
            creature, 60, new HashSet<ushort>(),
            CalculateBab, GetClassName);

        Assert.True(result.AllMet);
        Assert.Contains(result.OtherRequirements, r => r.Description == "Cast level 3 spells" && r.Met!.Value);
    }

    [Fact]
    public void CheckPrereqs_SpellLevel_WizardLevel3_CannotCastLevel3_NotMet()
    {
        var creature = new CreatureBuilder()
            .WithAbilities(10, 10, 10, 16, 10, 10)
            .WithClass(10, 3) // Wizard 3: has level 2 but not level 3
            .Build();

        var result = _featService.CheckFeatPrerequisites(
            creature, 60, new HashSet<ushort>(),
            CalculateBab, GetClassName);

        Assert.False(result.AllMet);
        Assert.Contains(result.OtherRequirements, r => r.Description == "Cast level 3 spells" && !r.Met!.Value);
    }

    [Fact]
    public void CheckPrereqs_SpellLevel_FighterOnly_NoCasting_NotMet()
    {
        var creature = new CreatureBuilder()
            .WithAbilities(14, 10, 10, 10, 10, 10)
            .WithClass(4, 10) // Fighter — no spell gain table
            .Build();

        var result = _featService.CheckFeatPrerequisites(
            creature, 60, new HashSet<ushort>(),
            CalculateBab, GetClassName);

        Assert.False(result.AllMet);
        Assert.Contains(result.OtherRequirements, r => r.Description == "Cast level 3 spells" && !r.Met!.Value);
    }

    [Fact]
    public void CheckPrereqs_SpellLevel1_WizardLevel1_AllMet()
    {
        var creature = new CreatureBuilder()
            .WithAbilities(10, 10, 10, 16, 10, 10)
            .WithClass(10, 1) // Wizard 1: has level 1 spells
            .Build();

        var result = _featService.CheckFeatPrerequisites(
            creature, 61, new HashSet<ushort>(),
            CalculateBab, GetClassName);

        Assert.True(result.AllMet);
    }

    [Fact]
    public void CheckPrereqs_SpellLevel_MulticlassFighterWizard_WizardGrantsSpells_AllMet()
    {
        // Fighter/Wizard multiclass — Wizard portion grants spell access
        var creature = new CreatureBuilder()
            .WithAbilities(14, 10, 10, 16, 10, 10)
            .WithClass(4, 5)  // Fighter 5 — no spells
            .WithClass(10, 5) // Wizard 5 — level 3 spells
            .Build();

        var result = _featService.CheckFeatPrerequisites(
            creature, 60, new HashSet<ushort>(),
            CalculateBab, GetClassName);

        Assert.True(result.AllMet);
    }

    [Fact]
    public void GetFeatPrerequisites_SpellLevel_ParsesCorrectly()
    {
        var prereqs = _featService.GetFeatPrerequisites(60);
        Assert.Equal(3, prereqs.MinSpellLevel);
    }

    #endregion

    #region Dual Skill Requirements

    [Fact]
    public void GetFeatPrerequisites_DualSkill_ParsesBothSkills()
    {
        var prereqs = _featService.GetFeatPrerequisites(70);
        Assert.Equal(2, prereqs.RequiredSkills.Count);
        Assert.Contains(prereqs.RequiredSkills, s => s.SkillId == 3 && s.MinRanks == 5);  // Discipline 5
        Assert.Contains(prereqs.RequiredSkills, s => s.SkillId == 16 && s.MinRanks == 3); // Tumble 3
    }

    [Fact]
    public void CheckPrereqs_DualSkill_BothMet_AllMet()
    {
        var creature = new CreatureBuilder()
            .WithAbilities(14, 14, 10, 10, 10, 10)
            .WithClass(4, 10)
            .Build();
        // Pad skill list to index 16
        while (creature.SkillList.Count <= 16)
            creature.SkillList.Add(0);
        creature.SkillList[3] = 6;   // Discipline: 6 >= 5
        creature.SkillList[16] = 4;  // Tumble: 4 >= 3

        var result = _featService.CheckFeatPrerequisites(
            creature, 70, new HashSet<ushort>(),
            CalculateBab, GetClassName);

        Assert.True(result.AllMet);
        Assert.Equal(2, result.SkillRequirements.Count);
        Assert.All(result.SkillRequirements, s => Assert.True(s.Met));
    }

    [Fact]
    public void CheckPrereqs_DualSkill_OneMet_NotMet()
    {
        var creature = new CreatureBuilder()
            .WithAbilities(14, 14, 10, 10, 10, 10)
            .WithClass(4, 10)
            .Build();
        while (creature.SkillList.Count <= 16)
            creature.SkillList.Add(0);
        creature.SkillList[3] = 6;   // Discipline: 6 >= 5 (met)
        creature.SkillList[16] = 1;  // Tumble: 1 < 3 (not met)

        var result = _featService.CheckFeatPrerequisites(
            creature, 70, new HashSet<ushort>(),
            CalculateBab, GetClassName);

        Assert.False(result.AllMet);
        Assert.Single(result.SkillRequirements, s => s.Met);
        Assert.Single(result.SkillRequirements, s => !s.Met);
    }

    [Fact]
    public void CheckPrereqs_DualSkill_NeitherMet_NotMet()
    {
        var creature = new CreatureBuilder()
            .WithAbilities(14, 14, 10, 10, 10, 10)
            .WithClass(4, 10)
            .Build();
        while (creature.SkillList.Count <= 16)
            creature.SkillList.Add(0);
        creature.SkillList[3] = 2;   // Discipline: 2 < 5
        creature.SkillList[16] = 1;  // Tumble: 1 < 3

        var result = _featService.CheckFeatPrerequisites(
            creature, 70, new HashSet<ushort>(),
            CalculateBab, GetClassName);

        Assert.False(result.AllMet);
        Assert.All(result.SkillRequirements, s => Assert.False(s.Met));
    }

    [Fact]
    public void CheckPrereqs_SkillBeyondSkillList_TreatedAsZero()
    {
        // Creature has no skills at all — skill index beyond list count
        var creature = new CreatureBuilder()
            .WithAbilities(14, 14, 10, 10, 10, 10)
            .WithClass(4, 10)
            .Build();
        // Don't add any skills — SkillList is empty

        var result = _featService.CheckFeatPrerequisites(
            creature, 70, new HashSet<ushort>(),
            CalculateBab, GetClassName);

        Assert.False(result.AllMet);
        Assert.Equal(2, result.SkillRequirements.Count);
        Assert.All(result.SkillRequirements, s => Assert.False(s.Met));
    }

    #endregion

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

    #region AutoAssignFeats

    [Fact]
    public void AutoAssignFeats_NoPackage_AssignsAlphabetically()
    {
        var creature = new CreatureBuilder()
            .WithAbilities(14, 14, 10, 10, 10, 10)
            .WithClass(4, 1)
            .Build();

        var currentFeats = new HashSet<int>();

        var assigned = _featService.AutoAssignFeats(
            creature, 4, 255, // No package
            currentFeats, 2,
            null, // No bonus pool restriction
            featId => true); // All prereqs met

        Assert.Equal(2, assigned.Count);
        // Should be alphabetical — Alertness before Blind-Fight before Cleave etc.
        var names = assigned.Select(id => _featService.GetFeatName(id)).ToList();
        Assert.Equal(names.OrderBy(n => n).ToList(), names);
    }

    [Fact]
    public void AutoAssignFeats_WithPackage_PrefersPackageFeats()
    {
        var creature = new CreatureBuilder()
            .WithAbilities(14, 14, 10, 10, 10, 10)
            .WithClass(4, 1)
            .Build();

        var currentFeats = new HashSet<int>();

        var assigned = _featService.AutoAssignFeats(
            creature, 4, 1, // Package 1 prefers: Power Attack, Cleave, Dodge
            currentFeats, 2,
            null,
            featId => true);

        Assert.Equal(2, assigned.Count);
        // Should pick Power Attack first (from package), then Cleave
        Assert.Equal(10, assigned[0]); // Power Attack
        Assert.Equal(7, assigned[1]);  // Cleave
    }

    [Fact]
    public void AutoAssignFeats_SkipsAlreadyOwnedFeats()
    {
        var creature = new CreatureBuilder()
            .WithAbilities(14, 14, 10, 10, 10, 10)
            .WithClass(4, 1)
            .Build();

        var currentFeats = new HashSet<int> { 10 }; // Already has Power Attack

        var assigned = _featService.AutoAssignFeats(
            creature, 4, 1, // Package prefers: Power Attack, Cleave, Dodge
            currentFeats, 2,
            null,
            featId => true);

        Assert.Equal(2, assigned.Count);
        Assert.DoesNotContain(10, assigned); // Skipped Power Attack
        Assert.Equal(7, assigned[0]);   // Cleave (next preferred)
        Assert.Equal(11, assigned[1]);  // Dodge (next preferred)
    }

    [Fact]
    public void AutoAssignFeats_RespectsMaxCount()
    {
        var creature = new CreatureBuilder()
            .WithAbilities(14, 14, 10, 10, 10, 10)
            .WithClass(4, 1)
            .Build();

        var assigned = _featService.AutoAssignFeats(
            creature, 4, 255, new HashSet<int>(), 1, null, _ => true);

        Assert.Single(assigned);
    }

    [Fact]
    public void AutoAssignFeats_RespectsPrereqChecker()
    {
        var creature = new CreatureBuilder()
            .WithAbilities(14, 14, 10, 10, 10, 10)
            .WithClass(4, 1)
            .Build();

        // Only feat 0 (Alertness) passes prereqs
        var assigned = _featService.AutoAssignFeats(
            creature, 4, 255, new HashSet<int>(), 3,
            null,
            featId => featId == 0);

        Assert.Single(assigned);
        Assert.Equal(0, assigned[0]); // Only Alertness
    }

    [Fact]
    public void AutoAssignFeats_BonusFeatPool_RestrictsToPool()
    {
        var creature = new CreatureBuilder()
            .WithAbilities(14, 14, 10, 10, 10, 10)
            .WithClass(4, 1)
            .Build();

        var bonusPool = new HashSet<int> { 5, 10 }; // Only Blind-Fight and Power Attack

        var assigned = _featService.AutoAssignFeats(
            creature, 4, 255, new HashSet<int>(), 5,
            bonusPool,
            _ => true);

        // Can only pick from pool, even though more feats exist
        Assert.True(assigned.Count <= 2);
        Assert.All(assigned, id => Assert.Contains(id, bonusPool));
    }

    [Fact]
    public void AutoAssignFeats_BonusFeatPool_PackagePrefNotInPool_Skipped()
    {
        var creature = new CreatureBuilder()
            .WithAbilities(14, 14, 10, 10, 10, 10)
            .WithClass(4, 1)
            .Build();

        // Pool excludes Power Attack (10) which is package's first preference
        var bonusPool = new HashSet<int> { 5, 7, 11 }; // Blind-Fight, Cleave, Dodge

        var assigned = _featService.AutoAssignFeats(
            creature, 4, 1, // Package prefers: Power Attack, Cleave, Dodge
            new HashSet<int>(), 2,
            bonusPool,
            _ => true);

        Assert.Equal(2, assigned.Count);
        Assert.DoesNotContain(10, assigned); // Power Attack not in pool
        Assert.Equal(7, assigned[0]);   // Cleave (next pref in pool)
        Assert.Equal(11, assigned[1]);  // Dodge (next pref in pool)
    }

    [Fact]
    public void AutoAssignFeats_ZeroMaxCount_ReturnsEmpty()
    {
        var creature = new CreatureBuilder()
            .WithAbilities(14, 14, 10, 10, 10, 10)
            .WithClass(4, 1)
            .Build();

        var assigned = _featService.AutoAssignFeats(
            creature, 4, 255, new HashSet<int>(), 0, null, _ => true);

        Assert.Empty(assigned);
    }

    [Fact]
    public void AutoAssignFeats_NoDuplicatesInResults()
    {
        var creature = new CreatureBuilder()
            .WithAbilities(14, 14, 10, 10, 10, 10)
            .WithClass(4, 1)
            .Build();

        var assigned = _featService.AutoAssignFeats(
            creature, 4, 1, new HashSet<int>(), 10, null, _ => true);

        // All assigned feat IDs should be unique
        Assert.Equal(assigned.Count, assigned.Distinct().Count());
    }

    [Fact]
    public void AutoAssignFeats_Fallback_PrefersClassFeatsOverUniversal()
    {
        // Set up: universal feat "AAA_Universal" (alphabetically first)
        // Existing class feats in Fighter table should be preferred over universals
        _mockGameData.Set2DAValue("feat", 80, "LABEL", "AAA_Universal");
        _mockGameData.Set2DAValue("feat", 80, "FEAT", "480");
        _mockGameData.Set2DAValue("feat", 80, "ALLCLASSESCANUSE", "1");
        _mockGameData.Set2DAValue("feat", 80, "TOOLSCATEGORIES", "6");
        _mockGameData.SetTlkString(480, "AAA Universal Feat");

        var creature = new CreatureBuilder()
            .WithAbilities(14, 14, 10, 10, 10, 10)
            .WithClass(4, 1) // Fighter
            .Build();

        // No package (255) — forces fallback
        var assigned = _featService.AutoAssignFeats(
            creature, 4, 255, new HashSet<int>(), 1, null, _ => true);

        // Should prefer a class feat (e.g., Blind-Fight/5) over AAA Universal (80)
        Assert.Single(assigned);
        Assert.NotEqual(80, assigned[0]); // NOT the alphabetically-first universal feat
    }

    #endregion

    #region Multiclass Feat Scenarios

    [Fact]
    public void IsFeatAvailable_InSecondClassTable_Available()
    {
        // Rogue table has Dodge (11), Fighter does not have it as universal
        var creature = new CreatureBuilder()
            .WithClass(4, 5)  // Fighter
            .WithClass(8, 3)  // Rogue (has Dodge in feat table)
            .Build();

        Assert.True(_featService.IsFeatAvailable(creature, 11)); // Dodge in Rogue table
    }

    [Fact]
    public void IsFeatAvailable_NotInAnyClassTable_NotUniversal_Unavailable()
    {
        // Create a non-universal feat not in any class table
        _mockGameData.Set2DAValue("feat", 95, "LABEL", "ClassExclusive");
        _mockGameData.Set2DAValue("feat", 95, "FEAT", "495");
        _mockGameData.Set2DAValue("feat", 95, "ALLCLASSESCANUSE", "0");
        _mockGameData.SetTlkString(495, "Class Exclusive Feat");

        var creature = new CreatureBuilder()
            .WithClass(4, 5)  // Fighter
            .WithClass(8, 3)  // Rogue
            .Build();

        Assert.False(_featService.IsFeatAvailable(creature, 95));
    }

    [Fact]
    public void GetUnavailableFeatIds_Multiclass_UnionOfBothTables()
    {
        var creature = new CreatureBuilder()
            .WithClass(4, 5)  // Fighter
            .WithClass(8, 3)  // Rogue
            .Build();

        // Blind-Fight (5) is in Fighter table, Dodge (11) is in Rogue table
        // Both should be available
        var unavailable = _featService.GetUnavailableFeatIds(creature, new[] { 5, 11 });
        Assert.Empty(unavailable);
    }

    [Fact]
    public void GetCombinedGrantedFeatIds_Multiclass_UnionOfAllClasses()
    {
        // Set up Rogue with an auto-granted feat
        _mockGameData.Set2DAValue("cls_feat_rog", 1, "FeatIndex", "77");
        _mockGameData.Set2DAValue("cls_feat_rog", 1, "List", "3");
        _mockGameData.Set2DAValue("cls_feat_rog", 1, "GrantedOnLevel", "1");

        var creature = new CreatureBuilder()
            .WithRace(6)      // Human
            .WithClass(4, 5)  // Fighter
            .WithClass(8, 3)  // Rogue
            .Build();

        var granted = _featService.GetCombinedGrantedFeatIds(creature);

        // Fighter grants: feat 2 (Armor Prof Heavy), feat 48 (Weapon Spec)
        Assert.Contains(2, granted);
        // Rogue grants: feat 77
        Assert.Contains(77, granted);
        // Human race grants: feat 99
        Assert.Contains(99, granted);
    }

    [Fact]
    public void GetFeatGrantingClass_MulticlassOverlap_ReturnsFirstMatch()
    {
        var creature = new CreatureBuilder()
            .WithClass(4, 5)  // Fighter (has feat 2 as List=3)
            .WithClass(8, 3)  // Rogue
            .Build();

        // Feat 2 (Armor Prof Heavy) is granted by Fighter
        Assert.Equal(4, _featService.GetFeatGrantingClass(creature, 2));
    }

    #endregion

    #region HasPrerequisites Flag

    [Fact]
    public void CheckPrereqs_NoPrereqs_HasPrerequisitesFalse()
    {
        var creature = new CreatureBuilder()
            .WithClass(4, 1)
            .Build();

        // Feat 52: no prerequisites at all
        var result = _featService.CheckFeatPrerequisites(
            creature, 52, new HashSet<ushort>(),
            CalculateBab, GetClassName);

        Assert.True(result.AllMet);
        Assert.False(result.HasPrerequisites);
    }

    [Fact]
    public void CheckPrereqs_WithPrereqs_HasPrerequisitesTrue()
    {
        var creature = new CreatureBuilder()
            .WithAbilities(14, 10, 10, 10, 10, 10)
            .WithClass(4, 1)
            .Build();

        // Feat 10: requires STR 13
        var result = _featService.CheckFeatPrerequisites(
            creature, 10, new HashSet<ushort>(),
            CalculateBab, GetClassName);

        Assert.True(result.HasPrerequisites);
    }

    [Fact]
    public void CheckPrereqs_ComplexPrereqs_HasPrerequisitesTrue()
    {
        var creature = new CreatureBuilder()
            .WithAbilities(26, 10, 10, 10, 10, 10)
            .WithClass(4, 21)
            .Build();

        // Feat 51: epic + STR 25 + feat requirement — complex combination
        var result = _featService.CheckFeatPrerequisites(
            creature, 51, new HashSet<ushort> { 10 },
            CalculateBab, GetClassName);

        Assert.True(result.HasPrerequisites);
    }

    #endregion

    #region Expected Feat Count - Multiclass

    [Fact]
    public void GetExpectedFeatCount_MulticlassFighterWizard_SumsClassBonuses()
    {
        var creature = new CreatureBuilder()
            .WithRace(6) // Human
            .WithClass(4, 4)  // Fighter 4: bonus at levels 1, 2, 4 = 3
            .WithClass(10, 5) // Wizard 5: bonus at level 5 = 1
            .Build();

        var result = _featService.GetExpectedFeatCount(creature);
        Assert.Equal(4, result.BaseFeats); // 1 + floor(9/3) = 4
        Assert.Equal(1, result.RacialBonusFeats); // Human
        Assert.Equal(4, result.ClassBonusFeats); // Fighter 3 + Wizard 1
        Assert.Equal(9, result.TotalExpected);
    }

    [Fact]
    public void GetLevelUpFeatCount_MulticlassLevel6_GeneralFeatGranted()
    {
        // Fighter 3 + Rogue 2 = total 5, leveling Rogue to 3 = total 6
        var creature = new CreatureBuilder()
            .WithRace(6)
            .WithClass(4, 3)
            .WithClass(8, 2) // Rogue 2
            .Build();

        var result = _featService.GetLevelUpFeatCount(creature, 8, 3); // Rogue 3
        Assert.Equal(1, result.GeneralFeats); // Total level 6 = general feat
        Assert.Equal(0, result.RacialBonusFeats); // Not level 1
        Assert.Equal(0, result.ClassBonusFeats); // Rogue has no bonus at level 3
        Assert.Equal(1, result.TotalFeats);
    }

    #endregion

    #region Tooltip Formatting

    [Fact]
    public void FeatPrereqResult_GetTooltip_OrRequired_ShowsOneOfSection()
    {
        var creature = new CreatureBuilder()
            .WithAbilities(10, 14, 10, 10, 10, 10)
            .WithClass(4, 1)
            .Build();

        var feats = new HashSet<ushort> { 0 }; // Has Alertness

        var result = _featService.CheckFeatPrerequisites(
            creature, 40, feats, CalculateBab, GetClassName);

        var tooltip = result.GetTooltip();
        Assert.Contains("One of:", tooltip);
        Assert.Contains("Alertness", tooltip);
        Assert.Contains("Dodge", tooltip);
    }

    [Fact]
    public void FeatPrereqResult_GetTooltip_MultipleReqTypes_IncludesAll()
    {
        var creature = new CreatureBuilder()
            .WithAbilities(26, 10, 10, 10, 10, 10)
            .WithClass(4, 21)
            .Build();

        var result = _featService.CheckFeatPrerequisites(
            creature, 51, new HashSet<ushort> { 10 },
            CalculateBab, GetClassName);

        var tooltip = result.GetTooltip();
        Assert.Contains("Prerequisites:", tooltip);
        Assert.Contains("Power Attack", tooltip); // Required feat
        Assert.Contains("STR 25+", tooltip);      // Ability requirement
        Assert.Contains("Epic", tooltip);           // Epic requirement
    }

    #endregion
}
