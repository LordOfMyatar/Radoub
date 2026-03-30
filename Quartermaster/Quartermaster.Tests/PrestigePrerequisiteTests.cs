using Quartermaster.Services;
using Radoub.Formats.Utc;
using Radoub.TestUtilities.Mocks;
using Xunit;

namespace Quartermaster.Tests;

/// <summary>
/// Tests for prestige class prerequisite checking in ClassService.
///
/// NWN prestige classes have prerequisites defined in cls_pres_*.2da tables.
/// Each row specifies: ReqType (FEAT, SKILL, BAB, RACE, ARCSPELL, etc.),
/// ReqParam1 (ID/value), ReqParam2 (threshold for SKILL type).
///
/// Desired behaviors:
///   - Base classes return no prerequisites
///   - Prestige classes parse their cls_pres_*.2da table correctly
///   - Each prerequisite type checks the correct creature property
///   - FEATOR groups require at least one feat from the group
///   - CheckPrerequisites correctly aggregates all requirement results
///   - GetAvailableClasses marks unqualified prestige classes
/// </summary>
public class PrestigePrerequisiteTests
{
    private readonly MockGameDataService _mockGameData;
    private readonly ClassService _classService;

    public PrestigePrerequisiteTests()
    {
        _mockGameData = new MockGameDataService(includeSampleData: true);
        SetupPrestigeClassData();
        var displayService = new CreatureDisplayService(_mockGameData);
        _classService = displayService.Classes;
    }

    /// <summary>
    /// Sets up a mock prestige class (Blackguard = class 19) with prerequisites:
    ///   - BAB +6
    ///   - Feat: Cleave (feat 6)
    ///   - Skill: Hide 5+ ranks (skill 7)
    ///
    /// And a second prestige class (Pale Master = class 24) with:
    ///   - ARCSPELL level 3 (can cast 3rd-level arcane spells)
    ///
    /// And a third (Assassin = class 17) with FEATOR:
    ///   - FEATOR: Alertness (feat 0) OR Stealthy (feat 1)
    /// </summary>
    private void SetupPrestigeClassData()
    {
        // Add prestige classes to classes.2da
        _mockGameData.Set2DAValue("classes", 19, "Label", "Blackguard");
        _mockGameData.Set2DAValue("classes", 19, "Name", "8000");
        _mockGameData.Set2DAValue("classes", 19, "HitDie", "10");
        _mockGameData.Set2DAValue("classes", 19, "AttackBonusTable", "CLS_ATK_1");
        _mockGameData.Set2DAValue("classes", 19, "AlignRestrict", "0x00");
        _mockGameData.Set2DAValue("classes", 19, "AlignRstrctType", "0x00");
        _mockGameData.Set2DAValue("classes", 19, "InvertRestrict", "0");
        _mockGameData.Set2DAValue("classes", 19, "PlayerClass", "1");
        _mockGameData.Set2DAValue("classes", 19, "PreReqTable", "cls_pres_bg");
        _mockGameData.Set2DAValue("classes", 19, "MaxLevel", "10");

        // cls_pres_bg.2da - Blackguard prereqs
        _mockGameData.Set2DAValue("cls_pres_bg", 0, "LABEL", "BAB6");
        _mockGameData.Set2DAValue("cls_pres_bg", 0, "ReqType", "BAB");
        _mockGameData.Set2DAValue("cls_pres_bg", 0, "ReqParam1", "6");
        _mockGameData.Set2DAValue("cls_pres_bg", 0, "ReqParam2", "****");

        _mockGameData.Set2DAValue("cls_pres_bg", 1, "LABEL", "Cleave");
        _mockGameData.Set2DAValue("cls_pres_bg", 1, "ReqType", "FEAT");
        _mockGameData.Set2DAValue("cls_pres_bg", 1, "ReqParam1", "6"); // Cleave feat ID
        _mockGameData.Set2DAValue("cls_pres_bg", 1, "ReqParam2", "****");

        _mockGameData.Set2DAValue("cls_pres_bg", 2, "LABEL", "Hide5");
        _mockGameData.Set2DAValue("cls_pres_bg", 2, "ReqType", "SKILL");
        _mockGameData.Set2DAValue("cls_pres_bg", 2, "ReqParam1", "7"); // Hide skill ID
        _mockGameData.Set2DAValue("cls_pres_bg", 2, "ReqParam2", "5"); // 5 ranks needed

        // Pale Master (class 24) - arcane spell prereq
        _mockGameData.Set2DAValue("classes", 24, "Label", "PaleMaster");
        _mockGameData.Set2DAValue("classes", 24, "Name", "8001");
        _mockGameData.Set2DAValue("classes", 24, "HitDie", "8");
        _mockGameData.Set2DAValue("classes", 24, "AttackBonusTable", "CLS_ATK_2");
        _mockGameData.Set2DAValue("classes", 24, "AlignRestrict", "0x00");
        _mockGameData.Set2DAValue("classes", 24, "AlignRstrctType", "0x00");
        _mockGameData.Set2DAValue("classes", 24, "InvertRestrict", "0");
        _mockGameData.Set2DAValue("classes", 24, "PlayerClass", "1");
        _mockGameData.Set2DAValue("classes", 24, "PreReqTable", "cls_pres_pm");
        _mockGameData.Set2DAValue("classes", 24, "MaxLevel", "10");

        _mockGameData.Set2DAValue("cls_pres_pm", 0, "LABEL", "ArcSpell3");
        _mockGameData.Set2DAValue("cls_pres_pm", 0, "ReqType", "ARCSPELL");
        _mockGameData.Set2DAValue("cls_pres_pm", 0, "ReqParam1", "3");
        _mockGameData.Set2DAValue("cls_pres_pm", 0, "ReqParam2", "****");

        // Assassin (class 17) - FEATOR prereq
        _mockGameData.Set2DAValue("classes", 17, "Label", "Assassin");
        _mockGameData.Set2DAValue("classes", 17, "Name", "8002");
        _mockGameData.Set2DAValue("classes", 17, "HitDie", "6");
        _mockGameData.Set2DAValue("classes", 17, "AttackBonusTable", "CLS_ATK_2");
        _mockGameData.Set2DAValue("classes", 17, "AlignRestrict", "0x00");
        _mockGameData.Set2DAValue("classes", 17, "AlignRstrctType", "0x00");
        _mockGameData.Set2DAValue("classes", 17, "InvertRestrict", "0");
        _mockGameData.Set2DAValue("classes", 17, "PlayerClass", "1");
        _mockGameData.Set2DAValue("classes", 17, "PreReqTable", "cls_pres_asn");
        _mockGameData.Set2DAValue("classes", 17, "MaxLevel", "10");

        _mockGameData.Set2DAValue("cls_pres_asn", 0, "LABEL", "or_stealth");
        _mockGameData.Set2DAValue("cls_pres_asn", 0, "ReqType", "FEATOR");
        _mockGameData.Set2DAValue("cls_pres_asn", 0, "ReqParam1", "0"); // Alertness
        _mockGameData.Set2DAValue("cls_pres_asn", 0, "ReqParam2", "****");

        _mockGameData.Set2DAValue("cls_pres_asn", 1, "LABEL", "or_stealth");
        _mockGameData.Set2DAValue("cls_pres_asn", 1, "ReqType", "FEATOR");
        _mockGameData.Set2DAValue("cls_pres_asn", 1, "ReqParam1", "1"); // Stealthy
        _mockGameData.Set2DAValue("cls_pres_asn", 1, "ReqParam2", "****");

        // Feat names for prereq descriptions
        _mockGameData.Set2DAValue("feat", 6, "Label", "Cleave");
        _mockGameData.Set2DAValue("feat", 6, "FEAT", "3006");
        _mockGameData.SetTlkString(3006, "Cleave");

        // Skill names
        _mockGameData.Set2DAValue("skills", 7, "Label", "Hide");
        _mockGameData.Set2DAValue("skills", 7, "Name", "9007");
        _mockGameData.SetTlkString(9007, "Hide");

        // TLK for prestige class names
        _mockGameData.SetTlkString(8000, "Blackguard");
        _mockGameData.SetTlkString(8001, "Pale Master");
        _mockGameData.SetTlkString(8002, "Assassin");

        // Spell gain tables for Wizard (class 10) — needed for ARCSPELL prereq checks
        // Wizard gets SpellGainTable "cls_spgn_wiz"
        _mockGameData.Set2DAValue("classes", 10, "SpellGainTable", "cls_spgn_wiz");
        // Level 5 Wizard has 3rd-level spell slots (row 4 = level 5, SpellLevel3 > 0)
        _mockGameData.Set2DAValue("cls_spgn_wiz", 0, "SpellLevel1", "1"); // Level 1
        _mockGameData.Set2DAValue("cls_spgn_wiz", 1, "SpellLevel1", "2"); // Level 2
        _mockGameData.Set2DAValue("cls_spgn_wiz", 2, "SpellLevel2", "1"); // Level 3
        _mockGameData.Set2DAValue("cls_spgn_wiz", 3, "SpellLevel2", "1"); // Level 4
        _mockGameData.Set2DAValue("cls_spgn_wiz", 4, "SpellLevel3", "1"); // Level 5 — gets 3rd level spells
    }

    private UtcFile CreateCreature(int classId = 4, int classLevel = 1)
    {
        var creature = new UtcFile
        {
            GoodEvil = 50,
            LawfulChaotic = 50
        };
        creature.ClassList.Add(new CreatureClass { Class = classId, ClassLevel = (byte)classLevel });
        return creature;
    }

    #region GetPrestigePrerequisites

    [Fact]
    public void GetPrestigePrerequisites_BaseClass_ReturnsEmptyList()
    {
        // Fighter (class 4) is not a prestige class
        var prereqs = _classService.GetPrestigePrerequisites(4);
        Assert.Empty(prereqs);
    }

    [Fact]
    public void GetPrestigePrerequisites_PrestigeClass_ReturnsPrereqs()
    {
        var prereqs = _classService.GetPrestigePrerequisites(19); // Blackguard
        Assert.Equal(3, prereqs.Count);
    }

    [Fact]
    public void GetPrestigePrerequisites_ParsesBABType()
    {
        var prereqs = _classService.GetPrestigePrerequisites(19);
        var babPrereq = prereqs.First(p => p.Type == PrereqType.Bab);
        Assert.Equal(6, babPrereq.Param1);
    }

    [Fact]
    public void GetPrestigePrerequisites_ParsesFeatType()
    {
        var prereqs = _classService.GetPrestigePrerequisites(19);
        var featPrereq = prereqs.First(p => p.Type == PrereqType.Feat);
        Assert.Equal(6, featPrereq.Param1); // Cleave feat ID
    }

    [Fact]
    public void GetPrestigePrerequisites_ParsesSkillType()
    {
        var prereqs = _classService.GetPrestigePrerequisites(19);
        var skillPrereq = prereqs.First(p => p.Type == PrereqType.Skill);
        Assert.Equal(7, skillPrereq.Param1); // Hide skill ID
        Assert.Equal(5, skillPrereq.Param2); // 5 ranks
    }

    [Fact]
    public void GetPrestigePrerequisites_ParsesFeatOrType()
    {
        var prereqs = _classService.GetPrestigePrerequisites(17); // Assassin
        Assert.Equal(2, prereqs.Count);
        Assert.All(prereqs, p => Assert.Equal(PrereqType.FeatOr, p.Type));
    }

    [Fact]
    public void GetPrestigePrerequisites_ParsesArcSpellType()
    {
        var prereqs = _classService.GetPrestigePrerequisites(24); // Pale Master
        Assert.Single(prereqs);
        Assert.Equal(PrereqType.ArcaneSpell, prereqs[0].Type);
        Assert.Equal(3, prereqs[0].Param1);
    }

    #endregion

    #region CheckPrerequisites — Base Classes

    [Fact]
    public void CheckPrerequisites_BaseClass_AllMet_NoPrereqs()
    {
        var creature = CreateCreature();
        var result = _classService.CheckPrerequisites(4, creature); // Fighter

        Assert.True(result.AllMet);
        Assert.False(result.HasPrerequisites);
    }

    #endregion

    #region CheckPrerequisites — BAB

    [Fact]
    public void CheckPrerequisites_BabMet_PassesCheck()
    {
        // Fighter level 6 = BAB 6 (full progression)
        var creature = CreateCreature(classId: 4, classLevel: 6);
        var result = _classService.CheckPrerequisites(19, creature);

        // BAB met, but still missing feat and skill
        var babReq = result.OtherRequirements.FirstOrDefault(r => r.Description.Contains("BAB"));
        Assert.NotEmpty(babReq.Description);
        Assert.True(babReq.Met);
    }

    [Fact]
    public void CheckPrerequisites_BabNotMet_FailsCheck()
    {
        // Wizard level 3 = BAB 1 (half progression)
        var creature = CreateCreature(classId: 10, classLevel: 3);
        var result = _classService.CheckPrerequisites(19, creature);

        var babReq = result.OtherRequirements.FirstOrDefault(r => r.Description.Contains("BAB"));
        Assert.NotEmpty(babReq.Description);
        Assert.False(babReq.Met);
        Assert.False(result.AllMet);
    }

    #endregion

    #region CheckPrerequisites — Feat

    [Fact]
    public void CheckPrerequisites_FeatPresent_PassesFeatCheck()
    {
        var creature = CreateCreature(classId: 4, classLevel: 6);
        creature.FeatList.Add(6); // Has Cleave

        var result = _classService.CheckPrerequisites(19, creature);

        var featReq = result.RequiredFeats.FirstOrDefault(f => f.Description.Contains("Cleave"));
        Assert.True(featReq.Met);
    }

    [Fact]
    public void CheckPrerequisites_FeatMissing_FailsFeatCheck()
    {
        var creature = CreateCreature(classId: 4, classLevel: 6);
        // No Cleave feat

        var result = _classService.CheckPrerequisites(19, creature);

        var featReq = result.RequiredFeats.FirstOrDefault(f => f.Description.Contains("Cleave"));
        Assert.False(featReq.Met);
        Assert.False(result.AllMet);
    }

    #endregion

    #region CheckPrerequisites — Skill

    [Fact]
    public void CheckPrerequisites_SkillRanksMet_PassesCheck()
    {
        var creature = CreateCreature(classId: 4, classLevel: 6);
        creature.FeatList.Add(6); // Cleave
        // Ensure skill list has enough entries and 5+ ranks in Hide (skill 7)
        while (creature.SkillList.Count <= 7)
            creature.SkillList.Add(0);
        creature.SkillList[7] = 5;

        var result = _classService.CheckPrerequisites(19, creature);

        var skillReq = result.SkillRequirements.FirstOrDefault(s => s.Description.Contains("Hide"));
        Assert.NotEmpty(skillReq.Description);
        Assert.True(skillReq.Met);
    }

    [Fact]
    public void CheckPrerequisites_SkillRanksInsufficient_FailsCheck()
    {
        var creature = CreateCreature(classId: 4, classLevel: 6);
        creature.FeatList.Add(6);
        while (creature.SkillList.Count <= 7)
            creature.SkillList.Add(0);
        creature.SkillList[7] = 3; // Only 3 ranks, need 5

        var result = _classService.CheckPrerequisites(19, creature);

        var skillReq = result.SkillRequirements.FirstOrDefault(s => s.Description.Contains("Hide"));
        Assert.NotEmpty(skillReq.Description);
        Assert.False(skillReq.Met);
    }

    #endregion

    #region CheckPrerequisites — FEATOR (OR-required feats)

    [Fact]
    public void CheckPrerequisites_FeatOrFirstPresent_PassesGroup()
    {
        var creature = CreateCreature();
        creature.FeatList.Add(0); // Alertness (first of OR group)

        var result = _classService.CheckPrerequisites(17, creature); // Assassin

        // At least one OR feat met → group passes
        Assert.Contains(result.OrRequiredFeats, f => f.Met);
    }

    [Fact]
    public void CheckPrerequisites_FeatOrSecondPresent_PassesGroup()
    {
        var creature = CreateCreature();
        creature.FeatList.Add(1); // Stealthy (second of OR group)

        var result = _classService.CheckPrerequisites(17, creature);

        Assert.Contains(result.OrRequiredFeats, f => f.Met);
    }

    [Fact]
    public void CheckPrerequisites_FeatOrBothPresent_StillPasses()
    {
        var creature = CreateCreature();
        creature.FeatList.Add(0);
        creature.FeatList.Add(1);

        var result = _classService.CheckPrerequisites(17, creature);

        Assert.Equal(2, result.OrRequiredFeats.Count(f => f.Met));
    }

    [Fact]
    public void CheckPrerequisites_FeatOrNonePresent_FailsGroup()
    {
        var creature = CreateCreature();
        // No feats at all

        var result = _classService.CheckPrerequisites(17, creature);

        Assert.False(result.AllMet);
        Assert.True(result.OrRequiredFeats.All(f => !f.Met));
    }

    #endregion

    #region CheckPrerequisites — Arcane Spell

    [Fact]
    public void CheckPrerequisites_ArcaneCaster_PassesArcSpellCheck()
    {
        // Wizard level 5 can cast 3rd-level arcane spells
        var creature = CreateCreature(classId: 10, classLevel: 5);

        var result = _classService.CheckPrerequisites(24, creature); // Pale Master

        var arcReq = result.OtherRequirements.FirstOrDefault(r => r.Description.Contains("arcane"));
        Assert.NotEmpty(arcReq.Description);
        Assert.True(arcReq.Met);
    }

    [Fact]
    public void CheckPrerequisites_NonCaster_FailsArcSpellCheck()
    {
        // Fighter cannot cast arcane spells
        var creature = CreateCreature(classId: 4, classLevel: 10);

        var result = _classService.CheckPrerequisites(24, creature);

        var arcReq = result.OtherRequirements.FirstOrDefault(r => r.Description.Contains("arcane"));
        Assert.NotEmpty(arcReq.Description);
        Assert.False(arcReq.Met);
        Assert.False(result.AllMet);
    }

    #endregion

    #region CheckPrerequisites — All Met

    [Fact]
    public void CheckPrerequisites_AllBlackguardPrereqsMet_AllMetTrue()
    {
        // Fighter level 6 (BAB 6), has Cleave, Hide 5 ranks
        var creature = CreateCreature(classId: 4, classLevel: 6);
        creature.FeatList.Add(6); // Cleave
        while (creature.SkillList.Count <= 7)
            creature.SkillList.Add(0);
        creature.SkillList[7] = 5; // Hide 5

        var result = _classService.CheckPrerequisites(19, creature);

        Assert.True(result.HasPrerequisites);
        Assert.True(result.AllMet);
    }

    [Fact]
    public void CheckPrerequisites_SomeMissing_AllMetFalse()
    {
        // Fighter level 6 (BAB 6) but missing Cleave and Hide
        var creature = CreateCreature(classId: 4, classLevel: 6);

        var result = _classService.CheckPrerequisites(19, creature);

        Assert.True(result.HasPrerequisites);
        Assert.False(result.AllMet);
    }

    #endregion

    #region GetAvailableClasses — Prestige Filtering

    [Fact]
    public void GetAvailableClasses_UnqualifiedPrestige_MarkedCorrectly()
    {
        // Low-level Fighter — doesn't meet Blackguard prereqs
        var creature = CreateCreature(classId: 4, classLevel: 1);

        var available = _classService.GetAvailableClasses(creature, includeUnqualified: true);

        var blackguard = available.FirstOrDefault(c => c.ClassId == 19);
        Assert.NotNull(blackguard);
        Assert.Equal(ClassQualification.PrerequisitesNotMet, blackguard!.Qualification);
    }

    [Fact]
    public void GetAvailableClasses_QualifiedPrestige_MarkedQualified()
    {
        var creature = CreateCreature(classId: 4, classLevel: 6);
        creature.FeatList.Add(6); // Cleave
        while (creature.SkillList.Count <= 7)
            creature.SkillList.Add(0);
        creature.SkillList[7] = 5;

        var available = _classService.GetAvailableClasses(creature, includeUnqualified: true);

        var blackguard = available.FirstOrDefault(c => c.ClassId == 19);
        Assert.NotNull(blackguard);
        Assert.Equal(ClassQualification.Qualified, blackguard!.Qualification);
    }

    [Fact]
    public void GetAvailableClasses_ExcludeUnqualified_OmitsPrestige()
    {
        var creature = CreateCreature(classId: 4, classLevel: 1);

        var available = _classService.GetAvailableClasses(creature, includeUnqualified: false);

        // Should not contain unqualified prestige classes
        Assert.DoesNotContain(available, c => c.ClassId == 19);
    }

    #endregion

    #region IsPrestigeClass

    [Fact]
    public void IsPrestigeClass_WithPreReqTable_ReturnsTrue()
    {
        Assert.True(_classService.IsPrestigeClass(19)); // Blackguard
    }

    [Fact]
    public void IsPrestigeClass_BaseClass_ReturnsFalse()
    {
        Assert.False(_classService.IsPrestigeClass(4)); // Fighter
    }

    [Fact]
    public void IsPrestigeClass_UnknownClass_ReturnsFalse()
    {
        Assert.False(_classService.IsPrestigeClass(999));
    }

    #endregion
}
