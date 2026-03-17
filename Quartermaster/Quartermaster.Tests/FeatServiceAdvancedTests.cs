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
///
/// Split into partial files:
/// - .cs (this file): Fields, constructor, test data setup, helpers
/// - .Prerequisites.cs: OR-Required, Epic, Spell Level, Dual Skill
/// - .LevelAndAbility.cs: Level Requirements, Multiple Ability Prerequisites
/// - .AutoAssign.cs: AutoAssign, Multiclass, HasPrereqs, ExpectedCount, Tooltip
/// </summary>
public partial class FeatServiceAdvancedTests
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
}
