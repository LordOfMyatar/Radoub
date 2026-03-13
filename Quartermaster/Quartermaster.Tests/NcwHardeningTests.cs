using Quartermaster.Services;
using Radoub.Formats.Utc;
using Radoub.TestUtilities.Mocks;
using Xunit;

namespace Quartermaster.Tests;

/// <summary>
/// Tests for NCW Hardening sprint (#1651).
/// Covers: race-specific point buy (#1628), MinSpellLevel filtering (#1639),
/// and package feat class validation (#1639).
/// </summary>
public class NcwHardeningTests
{
    private readonly MockGameDataService _mockGameData;
    private readonly SkillService _skillService;
    private readonly FeatCacheService _cacheService;
    private readonly FeatService _featService;
    private readonly CreatureDisplayService _displayService;

    public NcwHardeningTests()
    {
        _mockGameData = new MockGameDataService(includeSampleData: true);
        SetupTestData();
        _skillService = new SkillService(_mockGameData);
        _cacheService = new FeatCacheService();
        _featService = new FeatService(_mockGameData, _skillService, _cacheService);
        _displayService = new CreatureDisplayService(_mockGameData);
    }

    private void SetupTestData()
    {
        // --- Race-specific point buy (#1628) ---
        // Set up AbilitiesPointBuyNumber column for races
        _mockGameData.Set2DAValue("racialtypes", 0, "AbilitiesPointBuyNumber", "30"); // Dwarf: 30
        _mockGameData.Set2DAValue("racialtypes", 6, "AbilitiesPointBuyNumber", "30"); // Human: 30
        _mockGameData.Set2DAValue("racialtypes", 7, "CustomRace", "27");              // Custom race row 7: no AbilitiesPointBuyNumber
        _mockGameData.Set2DAValue("racialtypes", 8, "AbilitiesPointBuyNumber", "27"); // Custom race row 8: 27 points

        // --- Feat data for #1639 ---

        // Fighter class feat table
        _mockGameData.Set2DAValue("classes", 4, "FeatsTable", "cls_feat_fight");

        // Wizard class feat table + spell gain table
        _mockGameData.Set2DAValue("classes", 10, "FeatsTable", "cls_feat_wiz");
        _mockGameData.Set2DAValue("classes", 10, "SpellGainTable", "cls_spgn_wiz");

        // Fighter has NO SpellGainTable (not a caster)
        // (classes row 4 already exists without SpellGainTable)

        // cls_feat_fight.2da - Fighter feat table
        // Feat 100: Weapon Focus (Fighter bonus feat, List=1)
        _mockGameData.Set2DAValue("cls_feat_fight", 0, "FeatIndex", "100");
        _mockGameData.Set2DAValue("cls_feat_fight", 0, "List", "1");
        _mockGameData.Set2DAValue("cls_feat_fight", 0, "GrantedOnLevel", "****");

        // Feat 101: Armor Proficiency Heavy (auto-granted, List=3, level 1)
        _mockGameData.Set2DAValue("cls_feat_fight", 1, "FeatIndex", "101");
        _mockGameData.Set2DAValue("cls_feat_fight", 1, "List", "3");
        _mockGameData.Set2DAValue("cls_feat_fight", 1, "GrantedOnLevel", "1");

        // cls_feat_wiz.2da - Wizard feat table
        // Feat 200: Empower Spell (metamagic, in Wizard table, List=1)
        _mockGameData.Set2DAValue("cls_feat_wiz", 0, "FeatIndex", "200");
        _mockGameData.Set2DAValue("cls_feat_wiz", 0, "List", "1");
        _mockGameData.Set2DAValue("cls_feat_wiz", 0, "GrantedOnLevel", "****");

        // cls_spgn_wiz.2da - Wizard spell gain table
        // Row 0 (level 1): has SpellLevel0=3, SpellLevel1=1
        _mockGameData.Set2DAValue("cls_spgn_wiz", 0, "SpellLevel0", "3");
        _mockGameData.Set2DAValue("cls_spgn_wiz", 0, "SpellLevel1", "1");
        _mockGameData.Set2DAValue("cls_spgn_wiz", 0, "SpellLevel2", "0");
        // Row 2 (level 3): has SpellLevel2=1
        _mockGameData.Set2DAValue("cls_spgn_wiz", 2, "SpellLevel0", "4");
        _mockGameData.Set2DAValue("cls_spgn_wiz", 2, "SpellLevel1", "2");
        _mockGameData.Set2DAValue("cls_spgn_wiz", 2, "SpellLevel2", "1");

        // Feat definitions in feat.2da

        // Feat 100: Weapon Focus (no prereqs, universal = 0, in Fighter table)
        _mockGameData.Set2DAValue("feat", 100, "LABEL", "WeaponFocus");
        _mockGameData.Set2DAValue("feat", 100, "FEAT", "5100");
        _mockGameData.Set2DAValue("feat", 100, "TOOLSCATEGORIES", "1");
        _mockGameData.Set2DAValue("feat", 100, "ALLCLASSESCANUSE", "0");
        _mockGameData.SetTlkString(5100, "Weapon Focus");

        // Feat 101: Armor Proficiency Heavy (auto-granted)
        _mockGameData.Set2DAValue("feat", 101, "LABEL", "ArmorProfHeavy");
        _mockGameData.Set2DAValue("feat", 101, "FEAT", "5101");
        _mockGameData.Set2DAValue("feat", 101, "TOOLSCATEGORIES", "3");
        _mockGameData.Set2DAValue("feat", 101, "ALLCLASSESCANUSE", "0");
        _mockGameData.SetTlkString(5101, "Armor Proficiency (Heavy)");

        // Feat 200: Empower Spell (metamagic, requires MinSpellLevel 2)
        _mockGameData.Set2DAValue("feat", 200, "LABEL", "EmpowerSpell");
        _mockGameData.Set2DAValue("feat", 200, "FEAT", "5200");
        _mockGameData.Set2DAValue("feat", 200, "TOOLSCATEGORIES", "4");
        _mockGameData.Set2DAValue("feat", 200, "ALLCLASSESCANUSE", "1");
        _mockGameData.Set2DAValue("feat", 200, "MINSPELLLVL", "2");
        _mockGameData.SetTlkString(5200, "Empower Spell");

        // Feat 201: Spell Focus (universal, requires MinSpellLevel 1)
        _mockGameData.Set2DAValue("feat", 201, "LABEL", "SpellFocus");
        _mockGameData.Set2DAValue("feat", 201, "FEAT", "5201");
        _mockGameData.Set2DAValue("feat", 201, "TOOLSCATEGORIES", "4");
        _mockGameData.Set2DAValue("feat", 201, "ALLCLASSESCANUSE", "1");
        _mockGameData.Set2DAValue("feat", 201, "MINSPELLLVL", "1");
        _mockGameData.SetTlkString(5201, "Spell Focus");

        // Feat 202: Artist (universal, no prereqs — this is the problematic feat from #1639)
        _mockGameData.Set2DAValue("feat", 202, "LABEL", "Artist");
        _mockGameData.Set2DAValue("feat", 202, "FEAT", "5202");
        _mockGameData.Set2DAValue("feat", 202, "TOOLSCATEGORIES", "6");
        _mockGameData.Set2DAValue("feat", 202, "ALLCLASSESCANUSE", "1");
        _mockGameData.SetTlkString(5202, "Artist");

        // Feat 203: Toughness (universal, no prereqs)
        _mockGameData.Set2DAValue("feat", 203, "LABEL", "Toughness");
        _mockGameData.Set2DAValue("feat", 203, "FEAT", "5203");
        _mockGameData.Set2DAValue("feat", 203, "TOOLSCATEGORIES", "3");
        _mockGameData.Set2DAValue("feat", 203, "ALLCLASSESCANUSE", "1");
        _mockGameData.SetTlkString(5203, "Toughness");

        // Package for Fighter (package ID 1) with FeatPref2DA
        _mockGameData.Set2DAValue("packages", 1, "FeatPref2DA", "pkg_feat_fight");

        // pkg_feat_fight.2da - Fighter package feat preferences
        // Intentionally includes Artist (202) and Empower Spell (200) — problematic feats
        _mockGameData.Set2DAValue("pkg_feat_fight", 0, "FeatIndex", "202");  // Artist — not a Fighter feat
        _mockGameData.Set2DAValue("pkg_feat_fight", 1, "FeatIndex", "200");  // Empower Spell — requires MinSpellLevel
        _mockGameData.Set2DAValue("pkg_feat_fight", 2, "FeatIndex", "203");  // Toughness — valid
    }

    #region #1628 - Race-specific point buy totals

    [Fact]
    public void GetRacialAbilitiesPointBuyNumber_StandardRace_Returns30()
    {
        Assert.Equal(30, _displayService.GetRacialAbilitiesPointBuyNumber(6)); // Human
    }

    [Fact]
    public void GetRacialAbilitiesPointBuyNumber_CustomRaceWith27_Returns27()
    {
        Assert.Equal(27, _displayService.GetRacialAbilitiesPointBuyNumber(8)); // Custom race with 27
    }

    [Fact]
    public void GetRacialAbilitiesPointBuyNumber_MissingColumn_ReturnsDefault30()
    {
        // Race 7 has no AbilitiesPointBuyNumber column value
        Assert.Equal(30, _displayService.GetRacialAbilitiesPointBuyNumber(7));
    }

    [Fact]
    public void GetRacialAbilitiesPointBuyNumber_NonexistentRace_ReturnsDefault30()
    {
        Assert.Equal(30, _displayService.GetRacialAbilitiesPointBuyNumber(255));
    }

    [Fact]
    public void AutoAssign_With27Budget_NeverOverspends()
    {
        var result = AbilityPointBuyService.AutoAssign(27, "STR");
        int spent = AbilityPointBuyService.CalculatePointsSpent(result);
        Assert.True(spent <= 27, $"Spent {spent} > 27 budget");
    }

    [Fact]
    public void AutoAssign_With27Budget_PrimaryStillGets16()
    {
        var result = AbilityPointBuyService.AutoAssign(27, "STR");
        Assert.Equal(16, result["STR"]);
    }

    [Fact]
    public void AutoAssign_With27Budget_BalancedNeverOverspends()
    {
        var result = AbilityPointBuyService.AutoAssign(27, null);
        int spent = AbilityPointBuyService.CalculatePointsSpent(result);
        Assert.True(spent <= 27, $"Balanced spread spent {spent} > 27 budget");
    }

    [Fact]
    public void CalculatePointsRemaining_27Budget_AllAt8_Returns27()
    {
        var scores = new Dictionary<string, int>();
        foreach (var ability in AbilityPointBuyService.AbilityNames)
            scores[ability] = 8;
        Assert.Equal(27, AbilityPointBuyService.CalculatePointsRemaining(27, scores));
    }

    #endregion

    #region #1639 - MinSpellLevel prerequisite check

    [Fact]
    public void CheckFeatPrerequisites_MinSpellLevel_FighterFails()
    {
        // Fighter (class 4) has no SpellGainTable — cannot cast any spells
        var fighter = new UtcFile
        {
            Str = 16, Dex = 12, Con = 14, Int = 10, Wis = 10, Cha = 8,
            ClassList = new List<CreatureClass>
            {
                new CreatureClass { Class = 4, ClassLevel = 1 }
            },
            SkillList = new List<byte>()
        };

        var result = _featService.CheckFeatPrerequisites(
            fighter, 200, // Empower Spell (MinSpellLevel=2)
            new HashSet<ushort>(),
            c => 1, // BAB
            id => "Fighter");

        Assert.False(result.AllMet, "Fighter should not meet MinSpellLevel=2 prerequisite");
    }

    [Fact]
    public void CheckFeatPrerequisites_MinSpellLevel_WizardLevel1FailsForLevel2()
    {
        // Wizard level 1 has SpellLevel0 and SpellLevel1, but NOT SpellLevel2
        var wizard = new UtcFile
        {
            Str = 8, Dex = 12, Con = 10, Int = 16, Wis = 10, Cha = 8,
            ClassList = new List<CreatureClass>
            {
                new CreatureClass { Class = 10, ClassLevel = 1 }
            },
            SkillList = new List<byte>()
        };

        var result = _featService.CheckFeatPrerequisites(
            wizard, 200, // Empower Spell (MinSpellLevel=2)
            new HashSet<ushort>(),
            c => 0, // BAB
            id => "Wizard");

        Assert.False(result.AllMet, "Wizard level 1 should not meet MinSpellLevel=2");
    }

    [Fact]
    public void CheckFeatPrerequisites_MinSpellLevel_WizardLevel3PassesForLevel2()
    {
        // Wizard level 3 has SpellLevel2=1
        var wizard = new UtcFile
        {
            Str = 8, Dex = 12, Con = 10, Int = 16, Wis = 10, Cha = 8,
            ClassList = new List<CreatureClass>
            {
                new CreatureClass { Class = 10, ClassLevel = 3 }
            },
            SkillList = new List<byte>()
        };

        var result = _featService.CheckFeatPrerequisites(
            wizard, 200, // Empower Spell (MinSpellLevel=2)
            new HashSet<ushort>(),
            c => 1, // BAB
            id => "Wizard");

        Assert.True(result.AllMet, "Wizard level 3 should meet MinSpellLevel=2");
    }

    [Fact]
    public void CheckFeatPrerequisites_MinSpellLevel1_WizardLevel1Passes()
    {
        // Wizard level 1 has SpellLevel1=1
        var wizard = new UtcFile
        {
            Str = 8, Dex = 12, Con = 10, Int = 16, Wis = 10, Cha = 8,
            ClassList = new List<CreatureClass>
            {
                new CreatureClass { Class = 10, ClassLevel = 1 }
            },
            SkillList = new List<byte>()
        };

        var result = _featService.CheckFeatPrerequisites(
            wizard, 201, // Spell Focus (MinSpellLevel=1)
            new HashSet<ushort>(),
            c => 0,
            id => "Wizard");

        Assert.True(result.AllMet, "Wizard level 1 should meet MinSpellLevel=1");
    }

    [Fact]
    public void CheckFeatPrerequisites_MinSpellLevel1_FighterFails()
    {
        var fighter = new UtcFile
        {
            Str = 16, Dex = 12, Con = 14, Int = 10, Wis = 10, Cha = 8,
            ClassList = new List<CreatureClass>
            {
                new CreatureClass { Class = 4, ClassLevel = 1 }
            },
            SkillList = new List<byte>()
        };

        var result = _featService.CheckFeatPrerequisites(
            fighter, 201, // Spell Focus (MinSpellLevel=1)
            new HashSet<ushort>(),
            c => 1,
            id => "Fighter");

        Assert.False(result.AllMet, "Fighter should not meet MinSpellLevel=1");
    }

    #endregion

    #region #1639 - AutoAssignFeats package validation

    [Fact]
    public void AutoAssignFeats_FighterPackage_SkipsSpellFeats()
    {
        // Fighter with Generalist package — package prefs include Empower Spell (200)
        var fighter = new UtcFile
        {
            Race = 6, // Human
            ClassList = new List<CreatureClass>
            {
                new CreatureClass { Class = 4, ClassLevel = 1 }
            }
        };

        var currentFeats = new HashSet<int> { 101 }; // Armor Proficiency (granted)

        var assigned = _featService.AutoAssignFeats(
            fighter,
            classId: 4,
            packageId: 1,
            currentFeats: currentFeats,
            maxCount: 2,
            bonusFeatPool: null,
            prereqChecker: featId =>
            {
                var prereqs = _featService.GetFeatPrerequisites(featId);
                // Simulate NCW prereq checker: check MinSpellLevel against Fighter
                if (prereqs.MinSpellLevel > 0)
                {
                    // Fighter has no SpellGainTable — fails all spell level checks
                    return false;
                }
                return true;
            });

        // Should NOT include Empower Spell (200) — requires MinSpellLevel
        Assert.DoesNotContain(200, assigned);
    }

    [Fact]
    public void AutoAssignFeats_FighterPackage_AssignsValidFeats()
    {
        var fighter = new UtcFile
        {
            Race = 6,
            ClassList = new List<CreatureClass>
            {
                new CreatureClass { Class = 4, ClassLevel = 1 }
            }
        };

        var currentFeats = new HashSet<int> { 101 };

        var assigned = _featService.AutoAssignFeats(
            fighter,
            classId: 4,
            packageId: 1,
            currentFeats: currentFeats,
            maxCount: 2,
            bonusFeatPool: null,
            prereqChecker: featId =>
            {
                var prereqs = _featService.GetFeatPrerequisites(featId);
                if (prereqs.MinSpellLevel > 0) return false;
                return true;
            });

        // Should include Toughness (203) — valid feat from package prefs
        // Artist (202) should also be assigned since it has no prereqs and is universal
        Assert.True(assigned.Count <= 2, "Should assign at most 2 feats");
        Assert.True(assigned.Count > 0, "Should assign at least 1 feat");
    }

    [Fact]
    public void GetFeatPrerequisites_MinSpellLevel_ParsedCorrectly()
    {
        var prereqs = _featService.GetFeatPrerequisites(200); // Empower Spell
        Assert.Equal(2, prereqs.MinSpellLevel);
    }

    [Fact]
    public void GetFeatPrerequisites_NoMinSpellLevel_ReturnsZero()
    {
        var prereqs = _featService.GetFeatPrerequisites(203); // Toughness
        Assert.Equal(0, prereqs.MinSpellLevel);
    }

    #endregion
}
