using Quartermaster.Services;
using Radoub.TestUtilities.Builders;
using Radoub.TestUtilities.Mocks;
using Xunit;

namespace Quartermaster.Tests;

/// <summary>
/// Dedicated tests for FeatService: name lookups, categories, prerequisites,
/// class/race feat grants, availability, expected counts, and auto-assign.
/// </summary>
public class FeatServiceTests
{
    private readonly MockGameDataService _mockGameData;
    private readonly SkillService _skillService;
    private readonly FeatCacheService _cacheService;
    private readonly FeatService _featService;

    public FeatServiceTests()
    {
        _mockGameData = new MockGameDataService(includeSampleData: true);
        SetupFeatData();
        _skillService = new SkillService(_mockGameData);
        _cacheService = new FeatCacheService();
        _featService = new FeatService(_mockGameData, _skillService, _cacheService);
    }

    private void SetupFeatData()
    {
        // feat.2da: LABEL, FEAT (TLK strRef), DESCRIPTION (TLK strRef), TOOLSCATEGORIES, ALLCLASSESCANUSE
        // Feat 0: Alertness (Other, universal)
        _mockGameData.Set2DAValue("feat", 0, "LABEL", "Alertness");
        _mockGameData.Set2DAValue("feat", 0, "FEAT", "400");
        _mockGameData.Set2DAValue("feat", 0, "DESCRIPTION", "450");
        _mockGameData.Set2DAValue("feat", 0, "TOOLSCATEGORIES", "6"); // Other
        _mockGameData.Set2DAValue("feat", 0, "ALLCLASSESCANUSE", "1");

        // Feat 5: Blind-Fight (Combat, not universal)
        _mockGameData.Set2DAValue("feat", 5, "LABEL", "Blind_Fight");
        _mockGameData.Set2DAValue("feat", 5, "FEAT", "401");
        _mockGameData.Set2DAValue("feat", 5, "DESCRIPTION", "451");
        _mockGameData.Set2DAValue("feat", 5, "TOOLSCATEGORIES", "1"); // Combat
        _mockGameData.Set2DAValue("feat", 5, "ALLCLASSESCANUSE", "0");

        // Feat 7: Cleave (Combat, requires Power Attack=10)
        _mockGameData.Set2DAValue("feat", 7, "LABEL", "Cleave");
        _mockGameData.Set2DAValue("feat", 7, "FEAT", "402");
        _mockGameData.Set2DAValue("feat", 7, "DESCRIPTION", "452");
        _mockGameData.Set2DAValue("feat", 7, "TOOLSCATEGORIES", "1");
        _mockGameData.Set2DAValue("feat", 7, "ALLCLASSESCANUSE", "1");
        _mockGameData.Set2DAValue("feat", 7, "PREREQFEAT1", "10"); // Power Attack

        // Feat 10: Power Attack (Combat, requires STR 13)
        _mockGameData.Set2DAValue("feat", 10, "LABEL", "Power_Attack");
        _mockGameData.Set2DAValue("feat", 10, "FEAT", "403");
        _mockGameData.Set2DAValue("feat", 10, "DESCRIPTION", "453");
        _mockGameData.Set2DAValue("feat", 10, "TOOLSCATEGORIES", "1");
        _mockGameData.Set2DAValue("feat", 10, "ALLCLASSESCANUSE", "1");
        _mockGameData.Set2DAValue("feat", 10, "MINSTR", "13");

        // Feat 11: Dodge (Defensive, requires DEX 13)
        _mockGameData.Set2DAValue("feat", 11, "LABEL", "Dodge");
        _mockGameData.Set2DAValue("feat", 11, "FEAT", "404");
        _mockGameData.Set2DAValue("feat", 11, "DESCRIPTION", "454");
        _mockGameData.Set2DAValue("feat", 11, "TOOLSCATEGORIES", "3"); // Defensive
        _mockGameData.Set2DAValue("feat", 11, "ALLCLASSESCANUSE", "1");
        _mockGameData.Set2DAValue("feat", 11, "MINDEX", "13");

        // Feat 20: Epic feat with BAB requirement
        _mockGameData.Set2DAValue("feat", 20, "LABEL", "Epic_Prowess");
        _mockGameData.Set2DAValue("feat", 20, "FEAT", "405");
        _mockGameData.Set2DAValue("feat", 20, "DESCRIPTION", "455");
        _mockGameData.Set2DAValue("feat", 20, "TOOLSCATEGORIES", "1");
        _mockGameData.Set2DAValue("feat", 20, "ALLCLASSESCANUSE", "1");
        _mockGameData.Set2DAValue("feat", 20, "MINATTACKBONUS", "5");
        _mockGameData.Set2DAValue("feat", 20, "PreReqEpic", "1");

        // Feat 30: Requires skill ranks (Discipline 5)
        _mockGameData.Set2DAValue("feat", 30, "LABEL", "Whirlwind");
        _mockGameData.Set2DAValue("feat", 30, "FEAT", "406");
        _mockGameData.Set2DAValue("feat", 30, "DESCRIPTION", "456");
        _mockGameData.Set2DAValue("feat", 30, "TOOLSCATEGORIES", "2"); // ActiveCombat
        _mockGameData.Set2DAValue("feat", 30, "ALLCLASSESCANUSE", "1");
        _mockGameData.Set2DAValue("feat", 30, "REQSKILL", "3"); // Discipline
        _mockGameData.Set2DAValue("feat", 30, "ReqSkillMinRanks", "5");

        // Feat 40: OR-required feats (need Dodge OR Alertness)
        _mockGameData.Set2DAValue("feat", 40, "LABEL", "Mobility_Alt");
        _mockGameData.Set2DAValue("feat", 40, "FEAT", "407");
        _mockGameData.Set2DAValue("feat", 40, "TOOLSCATEGORIES", "3");
        _mockGameData.Set2DAValue("feat", 40, "ALLCLASSESCANUSE", "1");
        _mockGameData.Set2DAValue("feat", 40, "OrReqFeat0", "11"); // Dodge
        _mockGameData.Set2DAValue("feat", 40, "OrReqFeat1", "0");  // Alertness

        _mockGameData.SetTlkString(400, "Alertness");
        _mockGameData.SetTlkString(401, "Blind-Fight");
        _mockGameData.SetTlkString(402, "Cleave");
        _mockGameData.SetTlkString(403, "Power Attack");
        _mockGameData.SetTlkString(404, "Dodge");
        _mockGameData.SetTlkString(405, "Epic Prowess");
        _mockGameData.SetTlkString(406, "Whirlwind Attack");
        _mockGameData.SetTlkString(407, "Mobility (Alt)");
        _mockGameData.SetTlkString(450, "You get a +2 bonus on Listen and Spot checks.");
        _mockGameData.SetTlkString(451, "Blind-Fight desc.");
        _mockGameData.SetTlkString(452, "Cleave desc.");
        _mockGameData.SetTlkString(453, "Power Attack desc.");
        _mockGameData.SetTlkString(454, "Dodge desc.");
        _mockGameData.SetTlkString(455, "Epic Prowess desc.");
        _mockGameData.SetTlkString(456, "Whirlwind desc.");

        // cls_feat_fight (Fighter feat table)
        // Row 0: Blind-Fight (5), List=1 (bonus feat pool)
        _mockGameData.Set2DAValue("cls_feat_fight", 0, "FeatIndex", "5");
        _mockGameData.Set2DAValue("cls_feat_fight", 0, "List", "1");
        _mockGameData.Set2DAValue("cls_feat_fight", 0, "GrantedOnLevel", "****");
        // Row 1: Power Attack (10), List=1 (bonus feat pool)
        _mockGameData.Set2DAValue("cls_feat_fight", 1, "FeatIndex", "10");
        _mockGameData.Set2DAValue("cls_feat_fight", 1, "List", "1");
        _mockGameData.Set2DAValue("cls_feat_fight", 1, "GrantedOnLevel", "****");
        // Row 2: Armor Proficiency Heavy (2), List=3 (auto-granted), level 1
        _mockGameData.Set2DAValue("cls_feat_fight", 2, "FeatIndex", "2");
        _mockGameData.Set2DAValue("cls_feat_fight", 2, "List", "3");
        _mockGameData.Set2DAValue("cls_feat_fight", 2, "GrantedOnLevel", "1");
        // Row 3: Weapon Specialization (48), List=3, level 4
        _mockGameData.Set2DAValue("cls_feat_fight", 3, "FeatIndex", "48");
        _mockGameData.Set2DAValue("cls_feat_fight", 3, "List", "3");
        _mockGameData.Set2DAValue("cls_feat_fight", 3, "GrantedOnLevel", "4");
        // Row 4: Simple Weapons (45), List=-1 (creation grant)
        _mockGameData.Set2DAValue("cls_feat_fight", 4, "FeatIndex", "45");
        _mockGameData.Set2DAValue("cls_feat_fight", 4, "List", "-1");

        _mockGameData.Set2DAValue("classes", 4, "FeatsTable", "cls_feat_fight");

        // cls_bfeat_fight: Fighter bonus feats at level 1, 2, 4...
        _mockGameData.Set2DAValue("cls_bfeat_fight", 0, "Bonus", "1"); // Level 1
        _mockGameData.Set2DAValue("cls_bfeat_fight", 1, "Bonus", "1"); // Level 2
        _mockGameData.Set2DAValue("cls_bfeat_fight", 2, "Bonus", "0"); // Level 3
        _mockGameData.Set2DAValue("cls_bfeat_fight", 3, "Bonus", "1"); // Level 4
        _mockGameData.Set2DAValue("classes", 4, "BonusFeatsTable", "cls_bfeat_fight");

        // Race feat table for Human (6)
        _mockGameData.Set2DAValue("racialtypes", 6, "FeatsTable", "race_feat_human");
        _mockGameData.Set2DAValue("racialtypes", 6, "ExtraFeatsAtFirstLevel", "1");
        // Human gets Quick to Master (feat 258 in NWN, using 99 here)
        _mockGameData.Set2DAValue("race_feat_human", 0, "FeatIndex", "99");

        // Dwarf (0): no extra feats, but gets Stonecunning (feat 88)
        _mockGameData.Set2DAValue("racialtypes", 0, "FeatsTable", "race_feat_dwarf");
        _mockGameData.Set2DAValue("racialtypes", 0, "ExtraFeatsAtFirstLevel", "0");
        _mockGameData.Set2DAValue("race_feat_dwarf", 0, "FeatIndex", "88");
        _mockGameData.Set2DAValue("race_feat_dwarf", 1, "FeatIndex", "89");

        // Setup skill data for skill prerequisite tests
        _mockGameData.Set2DAValue("skills", 3, "Name", "203");
        _mockGameData.SetTlkString(203, "Discipline");
    }

    #region Name Lookups

    [Fact]
    public void GetFeatName_FromTlk_ReturnsResolvedName()
    {
        Assert.Equal("Alertness", _featService.GetFeatName(0));
    }

    [Fact]
    public void GetFeatName_BlindFight_ReturnsCorrectName()
    {
        Assert.Equal("Blind-Fight", _featService.GetFeatName(5));
    }

    [Fact]
    public void GetFeatName_UnknownFeat_ReturnsFallbackFormat()
    {
        Assert.Equal("Feat 999", _featService.GetFeatName(999));
    }

    #endregion

    #region Category

    [Fact]
    public void GetFeatCategory_Combat_ReturnsCombat()
    {
        Assert.Equal(FeatCategory.Combat, _featService.GetFeatCategory(5));
    }

    [Fact]
    public void GetFeatCategory_Defensive_ReturnsDefensive()
    {
        Assert.Equal(FeatCategory.Defensive, _featService.GetFeatCategory(11));
    }

    [Fact]
    public void GetFeatCategory_ActiveCombat_ReturnsActiveCombat()
    {
        Assert.Equal(FeatCategory.ActiveCombat, _featService.GetFeatCategory(30));
    }

    [Fact]
    public void GetFeatCategory_Other_ReturnsOther()
    {
        Assert.Equal(FeatCategory.Other, _featService.GetFeatCategory(0));
    }

    #endregion

    #region Description

    [Fact]
    public void GetFeatDescription_ValidFeat_ReturnsDescription()
    {
        var desc = _featService.GetFeatDescription(0);
        Assert.Equal("You get a +2 bonus on Listen and Spot checks.", desc);
    }

    [Fact]
    public void GetFeatDescription_MissingFeat_ReturnsEmpty()
    {
        Assert.Equal("", _featService.GetFeatDescription(999));
    }

    #endregion

    #region Universal

    [Fact]
    public void IsFeatUniversal_Alertness_True()
    {
        Assert.True(_featService.IsFeatUniversal(0));
    }

    [Fact]
    public void IsFeatUniversal_BlindFight_False()
    {
        Assert.False(_featService.IsFeatUniversal(5));
    }

    #endregion

    #region Feat Info

    [Fact]
    public void GetFeatInfo_ReturnsCompleteInfo()
    {
        var info = _featService.GetFeatInfo(11);
        Assert.Equal(11, info.FeatId);
        Assert.Equal("Dodge", info.Name);
        Assert.Equal(FeatCategory.Defensive, info.Category);
        Assert.True(info.IsUniversal);
    }

    #endregion

    #region All Feat IDs

    [Fact]
    public void GetAllFeatIds_ReturnsConfiguredFeats()
    {
        var ids = _featService.GetAllFeatIds();
        Assert.Contains(0, ids);  // Alertness
        Assert.Contains(5, ids);  // Blind-Fight
        Assert.Contains(7, ids);  // Cleave
        Assert.Contains(10, ids); // Power Attack
        Assert.Contains(11, ids); // Dodge
    }

    #endregion

    #region Class Feat Grants

    [Fact]
    public void GetClassFeatsGrantedAtLevel_FighterLevel1_IncludesCreationAndAutoGrants()
    {
        var feats = _featService.GetClassFeatsGrantedAtLevel(4, 1);
        // Armor Prof Heavy (2) is List=3 + GrantedOnLevel=1
        Assert.Contains(2, feats);
        // Simple Weapons (45) is List=-1 (creation grant, level 1 only)
        Assert.Contains(45, feats);
        // Weapon Specialization (48) is level 4 — not at level 1
        Assert.DoesNotContain(48, feats);
    }

    [Fact]
    public void GetClassFeatsGrantedAtLevel_FighterLevel4_IncludesWeaponSpec()
    {
        var feats = _featService.GetClassFeatsGrantedAtLevel(4, 4);
        Assert.Contains(48, feats); // Weapon Specialization
        // Creation-only feats (List=-1) NOT granted at level 4
        Assert.DoesNotContain(45, feats);
    }

    [Fact]
    public void GetClassGrantedFeatIds_Fighter_ReturnsAutoGrantedFeats()
    {
        var feats = _featService.GetClassGrantedFeatIds(4);
        // Only List=3 feats (auto-granted at various levels)
        Assert.Contains(2, feats);  // Armor Prof Heavy
        Assert.Contains(48, feats); // Weapon Specialization
        // List=1 (bonus pool) and List=-1 (creation) not included
        Assert.DoesNotContain(5, feats);  // Blind-Fight (bonus pool)
        Assert.DoesNotContain(45, feats); // Simple Weapons (creation)
    }

    #endregion

    #region Race Feat Grants

    [Fact]
    public void GetRaceGrantedFeatIds_Human_ReturnsHumanFeats()
    {
        var feats = _featService.GetRaceGrantedFeatIds(6);
        Assert.Contains(99, feats); // Quick to Master
    }

    [Fact]
    public void GetRaceGrantedFeatIds_Dwarf_ReturnsDwarfFeats()
    {
        var feats = _featService.GetRaceGrantedFeatIds(0);
        Assert.Contains(88, feats); // Stonecunning
        Assert.Contains(89, feats); // Another dwarf feat
    }

    [Fact]
    public void IsFeatGrantedByRace_DwarfFeat_True()
    {
        var creature = new CreatureBuilder()
            .WithRace((byte)0) // Dwarf
            .WithClass(4, 1)
            .Build();

        Assert.True(_featService.IsFeatGrantedByRace(creature, 88));
    }

    [Fact]
    public void IsFeatGrantedByRace_NonRacialFeat_False()
    {
        var creature = new CreatureBuilder()
            .WithRace((byte)0) // Dwarf
            .WithClass(4, 1)
            .Build();

        Assert.False(_featService.IsFeatGrantedByRace(creature, 0)); // Alertness
    }

    #endregion

    #region Combined Granted Feats

    [Fact]
    public void GetCombinedGrantedFeatIds_IncludesClassAndRace()
    {
        var creature = new CreatureBuilder()
            .WithRace((byte)0) // Dwarf
            .WithClass(4, 1) // Fighter
            .Build();

        var feats = _featService.GetCombinedGrantedFeatIds(creature);
        // Class granted (List=3)
        Assert.Contains(2, feats);  // Armor Prof Heavy (Fighter)
        Assert.Contains(48, feats); // Weapon Spec (Fighter)
        // Race granted
        Assert.Contains(88, feats); // Dwarf feat
        Assert.Contains(89, feats); // Dwarf feat
    }

    #endregion

    #region Feat Availability

    [Fact]
    public void IsFeatAvailable_UniversalFeat_AlwaysAvailable()
    {
        var creature = new CreatureBuilder()
            .WithClass(4, 1) // Fighter
            .Build();

        Assert.True(_featService.IsFeatAvailable(creature, 0)); // Alertness (universal)
    }

    [Fact]
    public void IsFeatAvailable_InClassTable_Available()
    {
        var creature = new CreatureBuilder()
            .WithClass(4, 1) // Fighter
            .Build();

        Assert.True(_featService.IsFeatAvailable(creature, 5)); // Blind-Fight (in fighter table)
    }

    [Fact]
    public void GetUnavailableFeatIds_NonUniversalNotInTable_Unavailable()
    {
        var creature = new CreatureBuilder()
            .WithClass(4, 1) // Fighter
            .Build();

        // Blind-Fight (5) is in fighter table, so it's available
        // Create a fake non-universal feat not in any table
        _mockGameData.Set2DAValue("feat", 50, "LABEL", "ClassSpecific");
        _mockGameData.Set2DAValue("feat", 50, "FEAT", "408");
        _mockGameData.Set2DAValue("feat", 50, "ALLCLASSESCANUSE", "0");
        _mockGameData.SetTlkString(408, "Class Specific Feat");

        var unavailable = _featService.GetUnavailableFeatIds(creature, new[] { 0, 5, 50 });
        Assert.DoesNotContain(0, unavailable);  // Universal
        Assert.DoesNotContain(5, unavailable);  // In fighter table
        Assert.Contains(50, unavailable);        // Not in any table, not universal
    }

    #endregion

    #region Feat Granting Class

    [Fact]
    public void GetFeatGrantingClass_FighterGrantedFeat_ReturnsFighter()
    {
        var creature = new CreatureBuilder()
            .WithClass(4, 1) // Fighter
            .Build();

        Assert.Equal(4, _featService.GetFeatGrantingClass(creature, 2)); // Armor Prof Heavy
    }

    [Fact]
    public void GetFeatGrantingClass_NoClassGrants_ReturnsMinus1()
    {
        var creature = new CreatureBuilder()
            .WithClass(4, 1) // Fighter
            .Build();

        Assert.Equal(-1, _featService.GetFeatGrantingClass(creature, 0)); // Alertness
    }

    #endregion

    #region Prerequisites

    [Fact]
    public void GetFeatPrerequisites_PowerAttack_RequiresStr13()
    {
        var prereqs = _featService.GetFeatPrerequisites(10);
        Assert.Equal(13, prereqs.MinStr);
        Assert.Empty(prereqs.RequiredFeats);
    }

    [Fact]
    public void GetFeatPrerequisites_Cleave_RequiresPowerAttack()
    {
        var prereqs = _featService.GetFeatPrerequisites(7);
        Assert.Single(prereqs.RequiredFeats);
        Assert.Contains(10, prereqs.RequiredFeats); // Power Attack
    }

    [Fact]
    public void GetFeatPrerequisites_Dodge_RequiresDex13()
    {
        var prereqs = _featService.GetFeatPrerequisites(11);
        Assert.Equal(13, prereqs.MinDex);
    }

    [Fact]
    public void GetFeatPrerequisites_Epic_RequiresEpic()
    {
        var prereqs = _featService.GetFeatPrerequisites(20);
        Assert.True(prereqs.RequiresEpic);
        Assert.Equal(5, prereqs.MinBab);
    }

    [Fact]
    public void GetFeatPrerequisites_SkillRequirement_ParsesCorrectly()
    {
        var prereqs = _featService.GetFeatPrerequisites(30);
        Assert.Single(prereqs.RequiredSkills);
        Assert.Equal(3, prereqs.RequiredSkills[0].SkillId); // Discipline
        Assert.Equal(5, prereqs.RequiredSkills[0].MinRanks);
    }

    [Fact]
    public void GetFeatPrerequisites_OrRequired_ParsesCorrectly()
    {
        var prereqs = _featService.GetFeatPrerequisites(40);
        Assert.Equal(2, prereqs.OrRequiredFeats.Count);
        Assert.Contains(11, prereqs.OrRequiredFeats); // Dodge
        Assert.Contains(0, prereqs.OrRequiredFeats);  // Alertness
    }

    #endregion

    #region Check Prerequisites

    [Fact]
    public void CheckFeatPrerequisites_MeetsStr_AllMet()
    {
        var creature = new CreatureBuilder()
            .WithAbilities(14, 10, 10, 10, 10, 10) // STR 14 >= 13
            .WithClass(4, 1)
            .Build();

        var result = _featService.CheckFeatPrerequisites(
            creature, 10, new HashSet<ushort>(),
            c => 1, classId => "Fighter");

        Assert.True(result.AllMet);
    }

    [Fact]
    public void CheckFeatPrerequisites_FailsStr_NotMet()
    {
        var creature = new CreatureBuilder()
            .WithAbilities(10, 10, 10, 10, 10, 10) // STR 10 < 13
            .WithClass(4, 1)
            .Build();

        var result = _featService.CheckFeatPrerequisites(
            creature, 10, new HashSet<ushort>(),
            c => 1, classId => "Fighter");

        Assert.False(result.AllMet);
        Assert.Single(result.AbilityRequirements);
        Assert.False(result.AbilityRequirements[0].Met);
    }

    [Fact]
    public void CheckFeatPrerequisites_RequiredFeat_HasIt_AllMet()
    {
        var creature = new CreatureBuilder()
            .WithAbilities(14, 10, 10, 10, 10, 10)
            .WithClass(4, 1)
            .Build();

        var feats = new HashSet<ushort> { 10 }; // Has Power Attack

        var result = _featService.CheckFeatPrerequisites(
            creature, 7, feats,
            c => 1, classId => "Fighter");

        Assert.True(result.AllMet);
    }

    [Fact]
    public void CheckFeatPrerequisites_RequiredFeat_Missing_NotMet()
    {
        var creature = new CreatureBuilder()
            .WithAbilities(14, 10, 10, 10, 10, 10)
            .WithClass(4, 1)
            .Build();

        var feats = new HashSet<ushort>(); // Missing Power Attack

        var result = _featService.CheckFeatPrerequisites(
            creature, 7, feats,
            c => 1, classId => "Fighter");

        Assert.False(result.AllMet);
    }

    [Fact]
    public void CheckFeatPrerequisites_OrRequired_HasOne_AllMet()
    {
        var creature = new CreatureBuilder()
            .WithAbilities(10, 14, 10, 10, 10, 10)
            .WithClass(4, 1)
            .Build();

        var feats = new HashSet<ushort> { 0 }; // Has Alertness (one of OR requirements)

        var result = _featService.CheckFeatPrerequisites(
            creature, 40, feats,
            c => 1, classId => "Fighter");

        Assert.True(result.AllMet);
    }

    [Fact]
    public void CheckFeatPrerequisites_OrRequired_HasNone_NotMet()
    {
        var creature = new CreatureBuilder()
            .WithAbilities(10, 14, 10, 10, 10, 10)
            .WithClass(4, 1)
            .Build();

        var feats = new HashSet<ushort>(); // Has neither Dodge nor Alertness

        var result = _featService.CheckFeatPrerequisites(
            creature, 40, feats,
            c => 1, classId => "Fighter");

        Assert.False(result.AllMet);
    }

    [Fact]
    public void CheckFeatPrerequisites_EpicRequired_LowLevel_NotMet()
    {
        var creature = new CreatureBuilder()
            .WithAbilities(14, 10, 10, 10, 10, 10)
            .WithClass(4, 5) // Level 5
            .Build();

        var result = _featService.CheckFeatPrerequisites(
            creature, 20, new HashSet<ushort>(),
            c => 5, classId => "Fighter");

        Assert.False(result.AllMet); // Epic requires level 21+
    }

    [Fact]
    public void CheckFeatPrerequisites_SkillReq_HasEnoughRanks_AllMet()
    {
        var creature = new CreatureBuilder()
            .WithAbilities(14, 10, 10, 10, 10, 10)
            .WithClass(4, 10)
            .Build();
        // Give 5+ ranks in Discipline (skill 3)
        while (creature.SkillList.Count <= 3)
            creature.SkillList.Add(0);
        creature.SkillList[3] = 6;

        var result = _featService.CheckFeatPrerequisites(
            creature, 30, new HashSet<ushort>(),
            c => 10, classId => "Fighter");

        Assert.True(result.AllMet);
    }

    [Fact]
    public void CheckFeatPrerequisites_SkillReq_NotEnoughRanks_NotMet()
    {
        var creature = new CreatureBuilder()
            .WithAbilities(14, 10, 10, 10, 10, 10)
            .WithClass(4, 10)
            .Build();
        while (creature.SkillList.Count <= 3)
            creature.SkillList.Add(0);
        creature.SkillList[3] = 2; // Only 2 ranks, need 5

        var result = _featService.CheckFeatPrerequisites(
            creature, 30, new HashSet<ushort>(),
            c => 10, classId => "Fighter");

        Assert.False(result.AllMet);
    }

    #endregion

    #region Expected Feat Counts

    [Fact]
    public void GetExpectedFeatCount_Level1_ReturnsCorrectBreakdown()
    {
        var creature = new CreatureBuilder()
            .WithRace(6) // Human
            .WithClass(4, 1) // Fighter 1
            .Build();

        var result = _featService.GetExpectedFeatCount(creature);
        Assert.Equal(1, result.BaseFeats); // 1 + floor(1/3) = 1
        Assert.Equal(1, result.RacialBonusFeats); // Human
        Assert.Equal(1, result.ClassBonusFeats); // Fighter level 1 bonus
        Assert.Equal(3, result.TotalExpected); // 1 + 1 + 1
    }

    [Fact]
    public void GetExpectedFeatCount_DwarfFighter3_CorrectBreakdown()
    {
        var creature = new CreatureBuilder()
            .WithRace((byte)0) // Dwarf (no extra feats)
            .WithClass(4, 3) // Fighter 3
            .Build();

        var result = _featService.GetExpectedFeatCount(creature);
        Assert.Equal(2, result.BaseFeats); // 1 + floor(3/3) = 2
        Assert.Equal(0, result.RacialBonusFeats); // Dwarf gets 0
        Assert.Equal(2, result.ClassBonusFeats); // Fighter: levels 1, 2 (not 3)
        Assert.Equal(4, result.TotalExpected);
    }

    #endregion

    #region Level Up Feat Count

    [Fact]
    public void GetLevelUpFeatCount_Level1Human_Gets3Feats()
    {
        var creature = new CreatureBuilder()
            .WithRace(6) // Human
            .Build();
        creature.ClassList.Clear(); // Pre-level state

        var result = _featService.GetLevelUpFeatCount(creature, 4, 1); // Fighter 1
        Assert.Equal(1, result.GeneralFeats);
        Assert.Equal(1, result.RacialBonusFeats); // Human
        Assert.Equal(1, result.ClassBonusFeats); // Fighter level 1
        Assert.Equal(3, result.TotalFeats);
    }

    [Fact]
    public void GetLevelUpFeatCount_Level2_NoGeneralFeat()
    {
        var creature = new CreatureBuilder()
            .WithRace(6)
            .WithClass(4, 1) // Fighter 1
            .Build();

        var result = _featService.GetLevelUpFeatCount(creature, 4, 2); // Fighter 2
        Assert.Equal(0, result.GeneralFeats); // Level 2 doesn't grant general feat
        Assert.Equal(0, result.RacialBonusFeats); // Only at level 1
        Assert.Equal(1, result.ClassBonusFeats); // Fighter level 2 bonus
        Assert.Equal(1, result.TotalFeats);
    }

    [Fact]
    public void GetLevelUpFeatCount_Level3_GeneralFeatGranted()
    {
        var creature = new CreatureBuilder()
            .WithRace(6)
            .WithClass(4, 2) // Fighter 2
            .Build();

        var result = _featService.GetLevelUpFeatCount(creature, 4, 3); // Fighter 3
        Assert.Equal(1, result.GeneralFeats); // Level 3 grants general feat
        Assert.Equal(0, result.ClassBonusFeats); // Fighter level 3 = no bonus
        Assert.Equal(1, result.TotalFeats);
    }

    #endregion

    #region Class Bonus Feat Pool

    [Fact]
    public void GetClassBonusFeatPool_Fighter_ReturnsList1Feats()
    {
        var pool = _featService.GetClassBonusFeatPool(4);
        Assert.Contains(5, pool);  // Blind-Fight (List=1)
        Assert.Contains(10, pool); // Power Attack (List=1)
        Assert.DoesNotContain(2, pool);  // Armor Prof (List=3, auto-granted)
        Assert.DoesNotContain(45, pool); // Simple Weapons (List=-1, creation)
    }

    #endregion

    #region Tooltip

    [Fact]
    public void FeatPrereqResult_GetTooltip_NoPrereqs_ReturnsSimpleMessage()
    {
        var result = new FeatPrereqResult { HasPrerequisites = false };
        Assert.Equal("No prerequisites", result.GetTooltip());
    }

    [Fact]
    public void FeatPrereqResult_GetTooltip_WithPrereqs_FormatsCorrectly()
    {
        var result = new FeatPrereqResult
        {
            HasPrerequisites = true,
            RequiredFeatsMet = { (10, "Power Attack", true) },
            AbilityRequirements = { ("STR 13+", true) }
        };

        var tooltip = result.GetTooltip();
        Assert.Contains("Prerequisites:", tooltip);
        Assert.Contains("Power Attack", tooltip);
        Assert.Contains("STR 13+", tooltip);
    }

    #endregion

    #region MASTERFEAT Subtype Grouping (#1734)

    [Fact]
    public void GetMasterFeatId_FeatWithMasterFeat_ReturnsParentId()
    {
        // Feat 100: Weapon Focus Club — child of master feat 1051
        _mockGameData.Set2DAValue("feat", 100, "LABEL", "WeapFocus_Club");
        _mockGameData.Set2DAValue("feat", 100, "FEAT", "500");
        _mockGameData.Set2DAValue("feat", 100, "MASTERFEAT", "1051");

        Assert.Equal(1051, _featService.GetMasterFeatId(100));
    }

    [Fact]
    public void GetMasterFeatId_FeatWithoutMasterFeat_ReturnsNull()
    {
        Assert.Null(_featService.GetMasterFeatId(0)); // Alertness has no MASTERFEAT
    }

    [Fact]
    public void GetMasterFeatId_FeatWithInvalidMasterFeat_ReturnsNull()
    {
        _mockGameData.Set2DAValue("feat", 101, "LABEL", "Foo");
        _mockGameData.Set2DAValue("feat", 101, "FEAT", "500");
        _mockGameData.Set2DAValue("feat", 101, "MASTERFEAT", "****");

        Assert.Null(_featService.GetMasterFeatId(101));
    }

    [Fact]
    public void GetSubtypeFeatIds_MasterFeat_ReturnsAllChildren()
    {
        // Set up a master feat (1051) with 3 subtypes
        _mockGameData.Set2DAValue("feat", 1051, "LABEL", "WeapFocus");
        _mockGameData.Set2DAValue("feat", 1051, "FEAT", "600");

        _mockGameData.Set2DAValue("feat", 200, "LABEL", "WeapFocus_Club");
        _mockGameData.Set2DAValue("feat", 200, "FEAT", "601");
        _mockGameData.Set2DAValue("feat", 200, "MASTERFEAT", "1051");

        _mockGameData.Set2DAValue("feat", 201, "LABEL", "WeapFocus_Dagger");
        _mockGameData.Set2DAValue("feat", 201, "FEAT", "602");
        _mockGameData.Set2DAValue("feat", 201, "MASTERFEAT", "1051");

        _mockGameData.Set2DAValue("feat", 202, "LABEL", "WeapFocus_Bastard");
        _mockGameData.Set2DAValue("feat", 202, "FEAT", "603");
        _mockGameData.Set2DAValue("feat", 202, "MASTERFEAT", "1051");

        var children = _featService.GetSubtypeFeatIds(1051);

        Assert.Equal(3, children.Count);
        Assert.Contains(200, children);
        Assert.Contains(201, children);
        Assert.Contains(202, children);
    }

    [Fact]
    public void GetSubtypeFeatIds_FeatWithNoChildren_ReturnsEmpty()
    {
        Assert.Empty(_featService.GetSubtypeFeatIds(0));
    }

    [Fact]
    public void GroupFeatsByMaster_SingletonFeats_KeptAsIs()
    {
        // Feats 0, 5, 10 have no MASTERFEAT — should appear as singleton groups
        var groups = _featService.GroupFeatsByMaster(new[] { 0, 5, 10 });

        Assert.Equal(3, groups.Count);
        Assert.All(groups, g => Assert.False(g.IsMasterFeat));
    }

    [Fact]
    public void GroupFeatsByMaster_SubtypeFeats_CollapsedToMaster()
    {
        // Set up master + children
        _mockGameData.Set2DAValue("feat", 1051, "LABEL", "WeapFocus");
        _mockGameData.Set2DAValue("feat", 1051, "FEAT", "600");

        _mockGameData.Set2DAValue("feat", 200, "LABEL", "WeapFocus_Club");
        _mockGameData.Set2DAValue("feat", 200, "FEAT", "601");
        _mockGameData.Set2DAValue("feat", 200, "MASTERFEAT", "1051");

        _mockGameData.Set2DAValue("feat", 201, "LABEL", "WeapFocus_Dagger");
        _mockGameData.Set2DAValue("feat", 201, "FEAT", "602");
        _mockGameData.Set2DAValue("feat", 201, "MASTERFEAT", "1051");

        // Input contains two children of the same master, plus a singleton (feat 0)
        var groups = _featService.GroupFeatsByMaster(new[] { 200, 201, 0 });

        // Expect 2 groups: master (1051) + singleton (0)
        Assert.Equal(2, groups.Count);
        var master = groups.Single(g => g.IsMasterFeat);
        Assert.Equal(1051, master.FeatId);
        Assert.Equal(2, master.SubtypeIds.Count);
        Assert.Contains(200, master.SubtypeIds);
        Assert.Contains(201, master.SubtypeIds);
    }

    [Fact]
    public void GetMasterFeatName_ReadsFromMasterfeats2da_NotFeat2da()
    {
        // masterfeats.2da row 5 has STRREF 900 -> "Weapon Focus" in TLK
        _mockGameData.Set2DAValue("masterfeats", 5, "LABEL", "WeapFocus");
        _mockGameData.Set2DAValue("masterfeats", 5, "STRREF", "900");
        _mockGameData.WithString(900, "Weapon Focus");

        // feat.2da row 5 would resolve to something else — confirm we DON'T use that
        _mockGameData.Set2DAValue("feat", 5, "LABEL", "Blind_Fight"); // already set in fixture
        _mockGameData.WithString(401, "Blind-Fight");

        var name = _featService.GetMasterFeatName(5);
        Assert.Equal("Weapon Focus", name);
    }

    [Fact]
    public void GetMasterFeatName_MissingRow_ReturnsFallbackLabel()
    {
        // When STRREF lookup fails, fall back to LABEL
        _mockGameData.Set2DAValue("masterfeats", 7, "LABEL", "ImprCrit");
        _mockGameData.Set2DAValue("masterfeats", 7, "STRREF", "****");

        var name = _featService.GetMasterFeatName(7);
        Assert.Equal("ImprCrit", name);
    }

    [Fact]
    public void GroupFeatsByMaster_MixedMastersAndOrphans_GroupsIndependently()
    {
        _mockGameData.Set2DAValue("feat", 1051, "LABEL", "WeapFocus");
        _mockGameData.Set2DAValue("feat", 1051, "FEAT", "600");
        _mockGameData.Set2DAValue("feat", 1052, "LABEL", "WeapSpec");
        _mockGameData.Set2DAValue("feat", 1052, "FEAT", "601");

        _mockGameData.Set2DAValue("feat", 200, "LABEL", "WFC");
        _mockGameData.Set2DAValue("feat", 200, "FEAT", "700");
        _mockGameData.Set2DAValue("feat", 200, "MASTERFEAT", "1051");

        _mockGameData.Set2DAValue("feat", 300, "LABEL", "WSpec_Club");
        _mockGameData.Set2DAValue("feat", 300, "FEAT", "800");
        _mockGameData.Set2DAValue("feat", 300, "MASTERFEAT", "1052");

        var groups = _featService.GroupFeatsByMaster(new[] { 200, 300, 0 });

        // Expect 3 groups: master 1051, master 1052, singleton 0
        Assert.Equal(3, groups.Count);
        Assert.Equal(2, groups.Count(g => g.IsMasterFeat));
    }

    #endregion
}
