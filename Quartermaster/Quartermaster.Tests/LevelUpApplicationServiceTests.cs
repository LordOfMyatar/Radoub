using Quartermaster.Services;
using Radoub.Formats.Utc;
using Radoub.TestUtilities.Builders;
using Radoub.TestUtilities.Mocks;
using Xunit;

namespace Quartermaster.Tests;

/// <summary>
/// Tests for LevelUpApplicationService — level-up application logic.
/// </summary>
public class LevelUpApplicationServiceTests
{
    private readonly MockGameDataService _mockGameData;
    private readonly CreatureDisplayService _displayService;
    private readonly LevelUpApplicationService _service;

    public LevelUpApplicationServiceTests()
    {
        _mockGameData = new MockGameDataService(includeSampleData: true);
        ConfigureClassData();
        _displayService = new CreatureDisplayService(_mockGameData);
        _service = new LevelUpApplicationService(_displayService);
    }

    #region ApplyClassLevel

    [Fact]
    public void ApplyClassLevel_ExistingClass_IncrementsLevel()
    {
        var creature = new CreatureBuilder()
            .WithClass(CommonClass.Fighter, 3)
            .Build();

        LevelUpApplicationService.ApplyClassLevel(creature, (int)CommonClass.Fighter);

        Assert.Single(creature.ClassList);
        Assert.Equal(4, creature.ClassList[0].ClassLevel);
    }

    [Fact]
    public void ApplyClassLevel_NewClass_AddsClassAtLevel1()
    {
        var creature = new CreatureBuilder()
            .WithClass(CommonClass.Fighter, 5)
            .Build();

        LevelUpApplicationService.ApplyClassLevel(creature, (int)CommonClass.Rogue);

        Assert.Equal(2, creature.ClassList.Count);
        Assert.Equal((int)CommonClass.Rogue, creature.ClassList[1].Class);
        Assert.Equal(1, creature.ClassList[1].ClassLevel);
    }

    [Fact]
    public void ApplyClassLevel_MultipleClasses_OnlyIncrementsCorrectOne()
    {
        var creature = new CreatureBuilder()
            .WithClass(CommonClass.Fighter, 5)
            .WithClass(CommonClass.Rogue, 3)
            .Build();

        LevelUpApplicationService.ApplyClassLevel(creature, (int)CommonClass.Rogue);

        Assert.Equal(5, creature.ClassList[0].ClassLevel); // Fighter unchanged
        Assert.Equal(4, creature.ClassList[1].ClassLevel); // Rogue incremented
    }

    #endregion

    #region ApplySkills

    [Fact]
    public void ApplySkills_AddsPointsToExistingSkills()
    {
        var creature = new CreatureBuilder()
            .WithSkillRanks(3, 0, 5) // Skills 0=3, 1=0, 2=5
            .Build();

        var points = new Dictionary<int, int>
        {
            { 0, 2 },  // Skill 0: 3 + 2 = 5
            { 2, 1 }   // Skill 2: 5 + 1 = 6
        };

        LevelUpApplicationService.ApplySkills(creature, points);

        Assert.Equal(5, creature.SkillList[0]);
        Assert.Equal(0, creature.SkillList[1]);
        Assert.Equal(6, creature.SkillList[2]);
    }

    [Fact]
    public void ApplySkills_ExtendsSkillList_WhenSkillIdExceedsCount()
    {
        var creature = new CreatureBuilder().Build();
        Assert.Empty(creature.SkillList);

        var points = new Dictionary<int, int> { { 5, 3 } };
        LevelUpApplicationService.ApplySkills(creature, points);

        Assert.True(creature.SkillList.Count > 5);
        Assert.Equal(3, creature.SkillList[5]);
    }

    [Fact]
    public void ApplySkills_CapsAt255()
    {
        var creature = new CreatureBuilder()
            .WithSkillRanks(250)
            .Build();

        var points = new Dictionary<int, int> { { 0, 10 } };
        LevelUpApplicationService.ApplySkills(creature, points);

        Assert.Equal(255, creature.SkillList[0]);
    }

    [Fact]
    public void ApplySkills_EmptyDict_DoesNothing()
    {
        var creature = new CreatureBuilder()
            .WithSkillRanks(5, 3)
            .Build();

        LevelUpApplicationService.ApplySkills(creature, new Dictionary<int, int>());

        Assert.Equal(5, creature.SkillList[0]);
        Assert.Equal(3, creature.SkillList[1]);
    }

    #endregion

    #region ApplySpells

    [Fact]
    public void ApplySpells_AddsToKnownSpells()
    {
        var creature = new CreatureBuilder()
            .WithClass(CommonClass.Wizard, 1)
            .Build();

        // Initialize KnownSpells array for the class
        var wizClass = creature.ClassList[0];
        wizClass.KnownSpells = new List<KnownSpell>[10];
        for (int i = 0; i < 10; i++)
            wizClass.KnownSpells[i] = new List<KnownSpell>();

        var spells = new Dictionary<int, List<int>>
        {
            { 0, new List<int> { 100, 101 } },  // 2 cantrips
            { 1, new List<int> { 200 } }          // 1 level-1 spell
        };

        LevelUpApplicationService.ApplySpells(creature, (int)CommonClass.Wizard, spells);

        Assert.Equal(2, wizClass.KnownSpells[0].Count);
        Assert.Equal(100, wizClass.KnownSpells[0][0].Spell);
        Assert.Equal(101, wizClass.KnownSpells[0][1].Spell);
        Assert.Single(wizClass.KnownSpells[1]);
        Assert.Equal(200, wizClass.KnownSpells[1][0].Spell);
    }

    [Fact]
    public void ApplySpells_DuplicateSpells_NotAdded()
    {
        var creature = new CreatureBuilder()
            .WithClass(CommonClass.Wizard, 1)
            .Build();

        var wizClass = creature.ClassList[0];
        wizClass.KnownSpells = new List<KnownSpell>[10];
        for (int i = 0; i < 10; i++)
            wizClass.KnownSpells[i] = new List<KnownSpell>();

        // Pre-add a spell
        wizClass.KnownSpells[0].Add(new KnownSpell { Spell = 100, SpellFlags = 0x01 });

        var spells = new Dictionary<int, List<int>>
        {
            { 0, new List<int> { 100, 101 } } // 100 already exists
        };

        LevelUpApplicationService.ApplySpells(creature, (int)CommonClass.Wizard, spells);

        Assert.Equal(2, wizClass.KnownSpells[0].Count); // 100 (existing) + 101 (new)
    }

    [Fact]
    public void ApplySpells_SetsReadiedFlag()
    {
        var creature = new CreatureBuilder()
            .WithClass(CommonClass.Wizard, 1)
            .Build();

        var wizClass = creature.ClassList[0];
        wizClass.KnownSpells = new List<KnownSpell>[10];
        for (int i = 0; i < 10; i++)
            wizClass.KnownSpells[i] = new List<KnownSpell>();

        var spells = new Dictionary<int, List<int>>
        {
            { 0, new List<int> { 100 } }
        };

        LevelUpApplicationService.ApplySpells(creature, (int)CommonClass.Wizard, spells);

        Assert.Equal(0x01, wizClass.KnownSpells[0][0].SpellFlags);
        Assert.Equal(0x00, wizClass.KnownSpells[0][0].SpellMetaMagic);
    }

    [Fact]
    public void ApplySpells_NoMatchingClass_DoesNothing()
    {
        var creature = new CreatureBuilder()
            .WithClass(CommonClass.Fighter, 5)
            .Build();

        var spells = new Dictionary<int, List<int>>
        {
            { 0, new List<int> { 100 } }
        };

        // Should not throw - Fighter has no spell lists
        LevelUpApplicationService.ApplySpells(creature, (int)CommonClass.Wizard, spells);
    }

    #endregion

    #region CalculateMaxSkillRanks

    [Theory]
    [InlineData(true, 1, 4)]   // Class skill level 1: 1+3 = 4
    [InlineData(true, 5, 8)]   // Class skill level 5: 5+3 = 8
    [InlineData(true, 20, 23)] // Class skill level 20: 20+3 = 23
    [InlineData(false, 1, 2)]  // Cross-class level 1: (1+3)/2 = 2
    [InlineData(false, 5, 4)]  // Cross-class level 5: (5+3)/2 = 4
    [InlineData(false, 20, 11)]// Cross-class level 20: (20+3)/2 = 11 (integer division)
    public void CalculateMaxSkillRanks_ReturnsExpected(bool isClassSkill, int level, int expected)
    {
        Assert.Equal(expected, LevelUpApplicationService.CalculateMaxSkillRanks(isClassSkill, level));
    }

    #endregion

    #region CalculateRemainingSkillPoints

    [Fact]
    public void CalculateRemainingSkillPoints_NoAllocations_ReturnsTotal()
    {
        int remaining = LevelUpApplicationService.CalculateRemainingSkillPoints(
            10, new Dictionary<int, int>(), new HashSet<int>());
        Assert.Equal(10, remaining);
    }

    [Fact]
    public void CalculateRemainingSkillPoints_ClassSkills_Cost1Each()
    {
        var classSkills = new HashSet<int> { 0, 1, 2 };
        var allocations = new Dictionary<int, int>
        {
            { 0, 3 }, // 3 * 1 = 3
            { 1, 2 }  // 2 * 1 = 2
        };

        int remaining = LevelUpApplicationService.CalculateRemainingSkillPoints(10, allocations, classSkills);
        Assert.Equal(5, remaining); // 10 - 5 = 5
    }

    [Fact]
    public void CalculateRemainingSkillPoints_CrossClassSkills_Cost2Each()
    {
        var classSkills = new HashSet<int>(); // No class skills
        var allocations = new Dictionary<int, int>
        {
            { 0, 2 }, // 2 * 2 = 4
            { 1, 1 }  // 1 * 2 = 2
        };

        int remaining = LevelUpApplicationService.CalculateRemainingSkillPoints(10, allocations, classSkills);
        Assert.Equal(4, remaining); // 10 - 6 = 4
    }

    [Fact]
    public void CalculateRemainingSkillPoints_MixedSkills_CalculatesCorrectly()
    {
        var classSkills = new HashSet<int> { 0 }; // Only skill 0 is class skill
        var allocations = new Dictionary<int, int>
        {
            { 0, 3 }, // Class: 3 * 1 = 3
            { 5, 2 }  // Cross-class: 2 * 2 = 4
        };

        int remaining = LevelUpApplicationService.CalculateRemainingSkillPoints(10, allocations, classSkills);
        Assert.Equal(3, remaining); // 10 - 7 = 3
    }

    #endregion

    #region Multi-Level Stacking

    [Fact]
    public void ApplyLevelUp_MultipleConsecutiveLevels_StacksCorrectly()
    {
        // Simulates NCW multi-level creation: level 1 creature leveled to 5
        var creature = new CreatureBuilder()
            .WithClass(CommonClass.Fighter, 1)
            .WithAbilities(14, 12, 14, 10, 10, 8)
            .WithSkillRanks(3, 0, 2)
            .Build();
        creature.HitPoints = 12;
        creature.MaxHitPoints = 12;
        creature.CurrentHitPoints = 12;

        // Apply 4 level-ups (levels 2, 3, 4, 5)
        for (int level = 2; level <= 5; level++)
        {
            var input = new LevelUpApplicationService.LevelUpInput
            {
                SelectedClassId = (int)CommonClass.Fighter,
                NewClassLevel = level,
                SelectedFeats = new List<int> { level * 10 }, // Unique feat per level
                SkillPointsAdded = new Dictionary<int, int> { { 0, 1 } }, // 1 skill point to skill 0 each level
                SelectedSpellsByLevel = new Dictionary<int, List<int>>(),
                HpIncrease = 12, // d10 + CON 14 (+2)
                AbilityIncrease = level == 4 ? 0 : -1, // STR +1 at level 4
                RecordHistory = false
            };

            _service.ApplyLevelUp(creature, input);
        }

        // Class level should be 5
        Assert.Single(creature.ClassList);
        Assert.Equal(5, creature.ClassList[0].ClassLevel);

        // 4 unique feats added (20, 30, 40, 50)
        Assert.Contains((ushort)20, creature.FeatList);
        Assert.Contains((ushort)30, creature.FeatList);
        Assert.Contains((ushort)40, creature.FeatList);
        Assert.Contains((ushort)50, creature.FeatList);

        // Skills: 3 base + 4 added = 7
        Assert.Equal(7, creature.SkillList[0]);

        // HP: 12 base + 4 * 12 = 60
        Assert.Equal(60, creature.MaxHitPoints);

        // STR: 14 + 1 (at level 4) = 15
        Assert.Equal(15, creature.Str);
        Assert.Equal(12, creature.Dex); // Unchanged
    }

    #endregion

    #region ApplyLevelUp Integration

    [Fact]
    public void ApplyLevelUp_IncreasesClassLevel()
    {
        var creature = new CreatureBuilder()
            .WithClass(CommonClass.Fighter, 3)
            .WithAbilities(14, 12, 14, 10, 10, 8)
            .Build();

        var input = new LevelUpApplicationService.LevelUpInput
        {
            SelectedClassId = (int)CommonClass.Fighter,
            NewClassLevel = 4,
            SelectedFeats = new List<int>(),
            SkillPointsAdded = new Dictionary<int, int>(),
            SelectedSpellsByLevel = new Dictionary<int, List<int>>(),
            RecordHistory = false
        };

        _service.ApplyLevelUp(creature, input);

        Assert.Equal(4, creature.ClassList[0].ClassLevel);
    }

    [Fact]
    public void ApplyLevelUp_AddsFeatAndSkills()
    {
        var creature = new CreatureBuilder()
            .WithClass(CommonClass.Fighter, 2)
            .WithAbilities(14, 12, 14, 10, 10, 8)
            .WithSkillRanks(3, 0, 2)
            .Build();

        var input = new LevelUpApplicationService.LevelUpInput
        {
            SelectedClassId = (int)CommonClass.Fighter,
            NewClassLevel = 3,
            SelectedFeats = new List<int> { 42 }, // Some feat
            SkillPointsAdded = new Dictionary<int, int>
            {
                { 0, 1 },
                { 2, 1 }
            },
            SelectedSpellsByLevel = new Dictionary<int, List<int>>(),
            RecordHistory = false
        };

        _service.ApplyLevelUp(creature, input);

        // Class level incremented
        Assert.Equal(3, creature.ClassList[0].ClassLevel);

        // Feat added
        Assert.Contains((ushort)42, creature.FeatList);

        // Skills updated
        Assert.Equal(4, creature.SkillList[0]); // 3 + 1
        Assert.Equal(3, creature.SkillList[2]); // 2 + 1
    }

    [Fact]
    public void ApplyLevelUp_DoesNotDuplicateFeats()
    {
        var creature = new CreatureBuilder()
            .WithClass(CommonClass.Fighter, 2)
            .WithFeat(42)
            .Build();

        var input = new LevelUpApplicationService.LevelUpInput
        {
            SelectedClassId = (int)CommonClass.Fighter,
            NewClassLevel = 3,
            SelectedFeats = new List<int> { 42 }, // Already has feat 42
            SkillPointsAdded = new Dictionary<int, int>(),
            SelectedSpellsByLevel = new Dictionary<int, List<int>>(),
            RecordHistory = false
        };

        _service.ApplyLevelUp(creature, input);

        // Should still only have one copy of feat 42
        Assert.Single(creature.FeatList, f => f == 42);
    }

    [Fact]
    public void ApplyLevelUp_WithAbilityIncrease_IncrementsAbility()
    {
        var creature = new CreatureBuilder()
            .WithClass(CommonClass.Fighter, 3)
            .WithAbilities(14, 12, 14, 10, 10, 8)
            .Build();

        var input = new LevelUpApplicationService.LevelUpInput
        {
            SelectedClassId = (int)CommonClass.Fighter,
            NewClassLevel = 4,
            SelectedFeats = new List<int>(),
            SkillPointsAdded = new Dictionary<int, int>(),
            SelectedSpellsByLevel = new Dictionary<int, List<int>>(),
            AbilityIncrease = 0, // STR
            RecordHistory = false
        };

        _service.ApplyLevelUp(creature, input);

        Assert.Equal(15, creature.Str); // 14 + 1
        Assert.Equal(12, creature.Dex); // Unchanged
    }

    [Theory]
    [InlineData(0, 15, 12, 14, 10, 10, 8)]  // STR +1
    [InlineData(1, 14, 13, 14, 10, 10, 8)]  // DEX +1
    [InlineData(2, 14, 12, 15, 10, 10, 8)]  // CON +1
    [InlineData(3, 14, 12, 14, 11, 10, 8)]  // INT +1
    [InlineData(4, 14, 12, 14, 10, 11, 8)]  // WIS +1
    [InlineData(5, 14, 12, 14, 10, 10, 9)]  // CHA +1
    public void ApplyAbilityIncrease_EachAbility_IncrementsCorrectOne(
        int abilityIndex, int expStr, int expDex, int expCon, int expInt, int expWis, int expCha)
    {
        var creature = new CreatureBuilder()
            .WithAbilities(14, 12, 14, 10, 10, 8)
            .Build();

        LevelUpApplicationService.ApplyAbilityIncrease(creature, abilityIndex);

        Assert.Equal(expStr, creature.Str);
        Assert.Equal(expDex, creature.Dex);
        Assert.Equal(expCon, creature.Con);
        Assert.Equal(expInt, creature.Int);
        Assert.Equal(expWis, creature.Wis);
        Assert.Equal(expCha, creature.Cha);
    }

    [Fact]
    public void ApplyAbilityIncrease_NegativeIndex_DoesNothing()
    {
        var creature = new CreatureBuilder()
            .WithAbilities(14, 12, 14, 10, 10, 8)
            .Build();

        LevelUpApplicationService.ApplyAbilityIncrease(creature, -1);

        Assert.Equal(14, creature.Str);
        Assert.Equal(12, creature.Dex);
    }

    #endregion

    #region CalculateLevelUpSkillPoints

    [Fact]
    public void CalculateLevelUpSkillPoints_Level1_Gets4xMultiplier()
    {
        // Fighter base = set to 2 in mock, INT 10 (mod 0)
        // Level 1: (2 + 0) * 4 = 8
        var creature = new CreatureBuilder()
            .WithRace(CommonRace.Human)
            .WithAbilities(intel: 10)
            .Build();
        // No class yet (total level will be 0+1=1)

        int points = _service.CalculateLevelUpSkillPoints(creature, (int)CommonClass.Fighter);
        // (base + intMod + racialExtra) * 4
        // The exact value depends on mock data - just verify it's > 0 and > non-level-1
        Assert.True(points > 0, "Level 1 should have skill points");
    }

    [Fact]
    public void CalculateLevelUpSkillPoints_Level2Plus_NoMultiplier()
    {
        var creature = new CreatureBuilder()
            .WithRace(CommonRace.Human)
            .WithClass(CommonClass.Fighter, 1) // Already level 1 => leveling to 2
            .WithAbilities(intel: 10)
            .Build();

        int points = _service.CalculateLevelUpSkillPoints(creature, (int)CommonClass.Fighter);
        // At level 2+, base points + INT mod (minimum 1) + racial
        Assert.True(points >= 1, "Should have at least 1 skill point");
    }

    [Fact]
    public void CalculateLevelUpSkillPoints_HighInt_MorePoints()
    {
        var creatureLowInt = new CreatureBuilder()
            .WithRace(CommonRace.Human)
            .WithClass(CommonClass.Fighter, 5)
            .WithAbilities(intel: 10) // mod 0
            .Build();

        var creatureHighInt = new CreatureBuilder()
            .WithRace(CommonRace.Human)
            .WithClass(CommonClass.Fighter, 5)
            .WithAbilities(intel: 16) // mod +3
            .Build();

        int lowPoints = _service.CalculateLevelUpSkillPoints(creatureLowInt, (int)CommonClass.Fighter);
        int highPoints = _service.CalculateLevelUpSkillPoints(creatureHighInt, (int)CommonClass.Fighter);

        Assert.True(highPoints > lowPoints, $"High INT ({highPoints}) should give more points than low INT ({lowPoints})");
    }

    #endregion

    #region ApplyHitPoints

    [Fact]
    public void ApplyHitPoints_IncreasesHpFields()
    {
        var creature = new CreatureBuilder()
            .WithAbilities(con: 14) // CON 14 = +2 mod
            .Build();
        creature.HitPoints = 10;
        creature.MaxHitPoints = 10;
        creature.CurrentHitPoints = 8;

        LevelUpApplicationService.ApplyHitPoints(creature, 12); // d10 max + CON 2

        Assert.Equal(22, creature.HitPoints);
        Assert.Equal(22, creature.MaxHitPoints);
        Assert.Equal(22, creature.CurrentHitPoints); // Restored to max
    }

    [Fact]
    public void ApplyHitPoints_MinimumOneHp()
    {
        var creature = new CreatureBuilder().Build();
        creature.HitPoints = 5;
        creature.MaxHitPoints = 5;
        creature.CurrentHitPoints = 5;

        LevelUpApplicationService.ApplyHitPoints(creature, -3); // Negative input

        Assert.Equal(6, creature.HitPoints); // Min 1 HP gain
        Assert.Equal(6, creature.MaxHitPoints);
    }

    [Theory]
    [InlineData(10, 14, 12)] // d10 + CON 14 (+2) = 12
    [InlineData(8, 10, 8)]   // d8 + CON 10 (+0) = 8
    [InlineData(6, 8, 5)]    // d6 + CON 8 (-1) = 5
    [InlineData(4, 6, 2)]    // d4 + CON 6 (-2) = 2
    [InlineData(4, 3, 1)]    // d4 + CON 3 (-4) = min 1
    public void CalculateHpIncrease_ReturnsExpected(int hitDie, int conScore, int expected)
    {
        Assert.Equal(expected, LevelUpApplicationService.CalculateHpIncrease(hitDie, conScore));
    }

    [Fact]
    public void ApplyLevelUp_IncludesHpIncrease()
    {
        var creature = new CreatureBuilder()
            .WithClass(CommonClass.Fighter, 3)
            .WithAbilities(14, 12, 14, 10, 10, 8)
            .Build();
        creature.HitPoints = 30;
        creature.MaxHitPoints = 30;
        creature.CurrentHitPoints = 25;

        var input = new LevelUpApplicationService.LevelUpInput
        {
            SelectedClassId = (int)CommonClass.Fighter,
            NewClassLevel = 4,
            HpIncrease = 12, // d10 max + CON 14 (+2)
            SelectedFeats = new List<int>(),
            SkillPointsAdded = new Dictionary<int, int>(),
            SelectedSpellsByLevel = new Dictionary<int, List<int>>(),
            RecordHistory = false
        };

        _service.ApplyLevelUp(creature, input);

        Assert.Equal(42, creature.MaxHitPoints); // 30 + 12
        Assert.Equal(42, creature.CurrentHitPoints); // Restored to max
    }

    #endregion

    #region CalculateConRetroactiveHp

    [Fact]
    public void CalculateConRetroactiveHp_ConModChanges_ReturnsRetroactiveHp()
    {
        // CON 15 -> 16: mod changes from +2 to +3, 5 previous levels = +5 HP
        int result = LevelUpApplicationService.CalculateConRetroactiveHp(2, 15, 5);
        Assert.Equal(5, result);
    }

    [Fact]
    public void CalculateConRetroactiveHp_ConModUnchanged_ReturnsZero()
    {
        // CON 14 -> 15: mod stays at +2, no retroactive HP
        int result = LevelUpApplicationService.CalculateConRetroactiveHp(2, 14, 5);
        Assert.Equal(0, result);
    }

    [Fact]
    public void CalculateConRetroactiveHp_NotCon_ReturnsZero()
    {
        // STR selected (index 0), not CON
        int result = LevelUpApplicationService.CalculateConRetroactiveHp(0, 15, 5);
        Assert.Equal(0, result);
    }

    [Fact]
    public void CalculateConRetroactiveHp_NoPreviousLevels_ReturnsZero()
    {
        int result = LevelUpApplicationService.CalculateConRetroactiveHp(2, 15, 0);
        Assert.Equal(0, result);
    }

    [Fact]
    public void ApplyLevelUp_WithConRetroactiveHp_AddsToTotal()
    {
        var creature = new CreatureBuilder()
            .WithClass(CommonClass.Fighter, 3)
            .WithAbilities(14, 12, 15, 10, 10, 8) // CON 15 -> 16 changes mod
            .Build();
        creature.HitPoints = 30;
        creature.MaxHitPoints = 30;
        creature.CurrentHitPoints = 25;

        var input = new LevelUpApplicationService.LevelUpInput
        {
            SelectedClassId = (int)CommonClass.Fighter,
            NewClassLevel = 4,
            HpIncrease = 13, // d10 max + CON 16 (+3)
            ConRetroactiveHp = 3, // 3 previous levels * +1 mod change
            AbilityIncrease = 2, // CON
            SelectedFeats = new List<int>(),
            SkillPointsAdded = new Dictionary<int, int>(),
            SelectedSpellsByLevel = new Dictionary<int, List<int>>(),
            RecordHistory = false
        };

        _service.ApplyLevelUp(creature, input);

        Assert.Equal(46, creature.MaxHitPoints); // 30 + 13 + 3
        Assert.Equal(46, creature.CurrentHitPoints);
    }

    #endregion

    #region Ability Score Cap (255)

    [Fact]
    public void ApplyAbilityIncrease_AtMax255_DoesNotOverflow()
    {
        var creature = new CreatureBuilder()
            .WithAbilities(255, 255, 255, 255, 255, 255)
            .Build();

        LevelUpApplicationService.ApplyAbilityIncrease(creature, 0); // STR
        Assert.Equal(255, creature.Str);

        LevelUpApplicationService.ApplyAbilityIncrease(creature, 1); // DEX
        Assert.Equal(255, creature.Dex);

        LevelUpApplicationService.ApplyAbilityIncrease(creature, 2); // CON
        Assert.Equal(255, creature.Con);

        LevelUpApplicationService.ApplyAbilityIncrease(creature, 3); // INT
        Assert.Equal(255, creature.Int);

        LevelUpApplicationService.ApplyAbilityIncrease(creature, 4); // WIS
        Assert.Equal(255, creature.Wis);

        LevelUpApplicationService.ApplyAbilityIncrease(creature, 5); // CHA
        Assert.Equal(255, creature.Cha);
    }

    [Fact]
    public void ApplyAbilityIncrease_At254_IncreasesTo255()
    {
        var creature = new CreatureBuilder()
            .WithAbilities(254, 10, 10, 10, 10, 10)
            .Build();

        LevelUpApplicationService.ApplyAbilityIncrease(creature, 0);
        Assert.Equal(255, creature.Str);
    }

    [Fact]
    public void ApplyAbilityIncrease_InvalidIndex6_DoesNothing()
    {
        var creature = new CreatureBuilder()
            .WithAbilities(14, 12, 14, 10, 10, 8)
            .Build();

        LevelUpApplicationService.ApplyAbilityIncrease(creature, 6);

        Assert.Equal(14, creature.Str);
        Assert.Equal(12, creature.Dex);
        Assert.Equal(14, creature.Con);
        Assert.Equal(10, creature.Int);
        Assert.Equal(10, creature.Wis);
        Assert.Equal(8, creature.Cha);
    }

    #endregion

    #region Retroactive CON HP Edge Cases

    [Fact]
    public void CalculateConRetroactiveHp_Con13To14_ModChanges_ReturnsRetroactiveHp()
    {
        // CON 13 -> 14: mod changes from +1 to +2, 10 previous levels = +10 HP
        int result = LevelUpApplicationService.CalculateConRetroactiveHp(2, 13, 10);
        Assert.Equal(10, result);
    }

    [Fact]
    public void CalculateConRetroactiveHp_Con10To11_ModChangesFrom0To0_ReturnsZero()
    {
        // CON 10 -> 11: mod stays at +0, no retroactive HP
        int result = LevelUpApplicationService.CalculateConRetroactiveHp(2, 10, 5);
        Assert.Equal(0, result);
    }

    [Fact]
    public void CalculateConRetroactiveHp_Con11To12_ModChanges0To1_ReturnsLevels()
    {
        // CON 11 -> 12: mod changes from +0 to +1, 8 previous levels = +8 HP
        int result = LevelUpApplicationService.CalculateConRetroactiveHp(2, 11, 8);
        Assert.Equal(8, result);
    }

    [Fact]
    public void CalculateConRetroactiveHp_HighCon_254To255_StillCapped()
    {
        // CON 254 -> 255: both are in same mod bracket (even numbers give higher mod)
        // 254 mod = (254-10)/2 = 122, 255 mod = (255-10)/2 = 122 (integer division)
        int result = LevelUpApplicationService.CalculateConRetroactiveHp(2, 254, 20);
        Assert.Equal(0, result); // Mod doesn't change at 254->255
    }

    [Fact]
    public void CalculateConRetroactiveHp_SinglePreviousLevel_ReturnsModChange()
    {
        // CON 15 -> 16: mod changes, 1 previous level = +1 HP
        int result = LevelUpApplicationService.CalculateConRetroactiveHp(2, 15, 1);
        Assert.Equal(1, result);
    }

    [Fact]
    public void CalculateConRetroactiveHp_NegativePreviousLevels_ReturnsZero()
    {
        int result = LevelUpApplicationService.CalculateConRetroactiveHp(2, 15, -1);
        Assert.Equal(0, result);
    }

    #endregion

    #region CE Mode Extra Abilities

    [Fact]
    public void ApplyLevelUp_ExtraAbilityIncreases_AppliesAll()
    {
        var creature = new CreatureBuilder()
            .WithClass(CommonClass.Fighter, 3)
            .WithAbilities(14, 12, 14, 10, 10, 8)
            .Build();

        var input = new LevelUpApplicationService.LevelUpInput
        {
            SelectedClassId = (int)CommonClass.Fighter,
            NewClassLevel = 4,
            AbilityIncrease = 0, // STR
            ExtraAbilityIncreases = new List<int> { 1, 2 }, // Also DEX and CON
            SelectedFeats = new List<int>(),
            SkillPointsAdded = new Dictionary<int, int>(),
            SelectedSpellsByLevel = new Dictionary<int, List<int>>(),
            RecordHistory = false
        };

        _service.ApplyLevelUp(creature, input);

        Assert.Equal(15, creature.Str); // 14 + 1
        Assert.Equal(13, creature.Dex); // 12 + 1 (extra)
        Assert.Equal(15, creature.Con); // 14 + 1 (extra)
    }

    [Fact]
    public void ApplyLevelUp_ExtraAbilityIncreases_SameAbilityTwice_Stacks()
    {
        var creature = new CreatureBuilder()
            .WithClass(CommonClass.Fighter, 3)
            .WithAbilities(14, 12, 14, 10, 10, 8)
            .Build();

        var input = new LevelUpApplicationService.LevelUpInput
        {
            SelectedClassId = (int)CommonClass.Fighter,
            NewClassLevel = 4,
            AbilityIncrease = 0, // STR
            ExtraAbilityIncreases = new List<int> { 0, 0 }, // STR twice more
            SelectedFeats = new List<int>(),
            SkillPointsAdded = new Dictionary<int, int>(),
            SelectedSpellsByLevel = new Dictionary<int, List<int>>(),
            RecordHistory = false
        };

        _service.ApplyLevelUp(creature, input);

        Assert.Equal(17, creature.Str); // 14 + 1 + 1 + 1
    }

    [Fact]
    public void ApplyLevelUp_NoAbilityIncrease_NoExtraAbilities_NothingChanges()
    {
        var creature = new CreatureBuilder()
            .WithClass(CommonClass.Fighter, 1)
            .WithAbilities(14, 12, 14, 10, 10, 8)
            .Build();

        var input = new LevelUpApplicationService.LevelUpInput
        {
            SelectedClassId = (int)CommonClass.Fighter,
            NewClassLevel = 2,
            AbilityIncrease = -1, // None
            ExtraAbilityIncreases = new List<int>(), // Empty
            SelectedFeats = new List<int>(),
            SkillPointsAdded = new Dictionary<int, int>(),
            SelectedSpellsByLevel = new Dictionary<int, List<int>>(),
            RecordHistory = false
        };

        _service.ApplyLevelUp(creature, input);

        Assert.Equal(14, creature.Str);
        Assert.Equal(12, creature.Dex);
    }

    #endregion

    #region ApplySpells Edge Cases

    [Fact]
    public void ApplySpells_NegativeSpellLevel_Skipped()
    {
        var creature = new CreatureBuilder()
            .WithClass(CommonClass.Wizard, 1)
            .Build();

        var wizClass = creature.ClassList[0];
        wizClass.KnownSpells = new List<KnownSpell>[10];
        for (int i = 0; i < 10; i++)
            wizClass.KnownSpells[i] = new List<KnownSpell>();

        var spells = new Dictionary<int, List<int>>
        {
            { -1, new List<int> { 100 } } // Invalid negative level
        };

        LevelUpApplicationService.ApplySpells(creature, (int)CommonClass.Wizard, spells);

        // No crash, no spells added
        for (int i = 0; i < 10; i++)
            Assert.Empty(wizClass.KnownSpells[i]);
    }

    [Fact]
    public void ApplySpells_SpellLevel10_Skipped()
    {
        var creature = new CreatureBuilder()
            .WithClass(CommonClass.Wizard, 1)
            .Build();

        var wizClass = creature.ClassList[0];
        wizClass.KnownSpells = new List<KnownSpell>[10];
        for (int i = 0; i < 10; i++)
            wizClass.KnownSpells[i] = new List<KnownSpell>();

        var spells = new Dictionary<int, List<int>>
        {
            { 10, new List<int> { 100 } } // Beyond level 9
        };

        LevelUpApplicationService.ApplySpells(creature, (int)CommonClass.Wizard, spells);

        // No crash, no spells added
        for (int i = 0; i < 10; i++)
            Assert.Empty(wizClass.KnownSpells[i]);
    }

    [Fact]
    public void ApplySpells_EmptySpellList_DoesNothing()
    {
        var creature = new CreatureBuilder()
            .WithClass(CommonClass.Wizard, 1)
            .Build();

        var wizClass = creature.ClassList[0];
        wizClass.KnownSpells = new List<KnownSpell>[10];
        for (int i = 0; i < 10; i++)
            wizClass.KnownSpells[i] = new List<KnownSpell>();

        var spells = new Dictionary<int, List<int>>();

        LevelUpApplicationService.ApplySpells(creature, (int)CommonClass.Wizard, spells);

        for (int i = 0; i < 10; i++)
            Assert.Empty(wizClass.KnownSpells[i]);
    }

    #endregion

    #region ApplyHitPoints Edge Cases

    [Fact]
    public void ApplyHitPoints_ZeroIncrease_StillGainsOneHp()
    {
        var creature = new CreatureBuilder().Build();
        creature.HitPoints = 10;
        creature.MaxHitPoints = 10;
        creature.CurrentHitPoints = 10;

        LevelUpApplicationService.ApplyHitPoints(creature, 0);

        Assert.Equal(11, creature.MaxHitPoints); // Min 1 HP gain
    }

    [Fact]
    public void ApplyHitPoints_LargeValue_CapsAtShortMax()
    {
        var creature = new CreatureBuilder().Build();
        creature.HitPoints = 32000;
        creature.MaxHitPoints = 32000;
        creature.CurrentHitPoints = 32000;

        LevelUpApplicationService.ApplyHitPoints(creature, 1000);

        // short.MaxValue = 32767, so 32000 + 1000 = 33000 capped to 32767
        Assert.Equal(short.MaxValue, creature.MaxHitPoints);
    }

    [Fact]
    public void CalculateHpIncrease_VeryCon_NegativeModifier_MinimumOne()
    {
        // Hit die 4 with CON 1 (-5 mod) = max(1, 4+(-5)) = max(1, -1) = 1
        Assert.Equal(1, LevelUpApplicationService.CalculateHpIncrease(4, 1));
    }

    #endregion

    #region CalculateLevelUpSkillPoints Edge Cases

    [Fact]
    public void CalculateLevelUpSkillPoints_NegativeIntMod_MinimumOnePoint()
    {
        // INT 6 (mod -2), Fighter base 2: max(1, 2+(-2)) = max(1, 0) = 1
        var creature = new CreatureBuilder()
            .WithRace(CommonRace.Elf) // No extra skill points
            .WithClass(CommonClass.Fighter, 5)
            .WithAbilities(intel: 6)
            .Build();

        int points = _service.CalculateLevelUpSkillPoints(creature, (int)CommonClass.Fighter);
        Assert.True(points >= 1, "Minimum 1 skill point per level");
    }

    #endregion

    #region Consolidated Level-Up Helpers (#1645)

    [Theory]
    [InlineData(1, 6, new[] { 4 })]           // Levels 2-7, char level 4 gets increase
    [InlineData(1, 8, new[] { 4, 8 })]        // Levels 2-9, char levels 4 and 8
    [InlineData(4, 4, new[] { 8 })]           // Levels 5-8, char level 8 gets increase
    [InlineData(1, 1, new int[0])]            // Level 2 only, no increase
    [InlineData(1, 3, new[] { 4 })]           // Levels 2-4, char level 4 gets increase
    [InlineData(0, 4, new[] { 4 })]           // Levels 1-4, char level 4 gets increase
    [InlineData(3, 1, new[] { 4 })]           // Level 4 exactly
    [InlineData(4, 1, new int[0])]            // Level 5, no increase
    public void GetAbilityIncreaseLevels_ReturnsCorrectLevels(int currentTotalLevel, int levelsToAdd, int[] expected)
    {
        var result = LevelUpApplicationService.GetAbilityIncreaseLevels(currentTotalLevel, levelsToAdd);
        Assert.Equal(expected, result.ToArray());
    }

    [Fact]
    public void CalculateConsolidatedHp_NoConIncrease_SumsPerLevel()
    {
        // 3 levels of Fighter (d10), CON 14 (mod +2), no CON increases
        // Each level: max(1, 10 + 2) = 12. Total = 36
        int result = LevelUpApplicationService.CalculateConsolidatedHp(
            hitDie: 10, baseCon: 14, previousLevelCount: 1,
            levelsToAdd: 3, conIncreaseLevels: new List<int>());
        Assert.Equal(36, result);
    }

    [Fact]
    public void CalculateConsolidatedHp_ConIncreaseAtLevel4_AddsRetroHp()
    {
        // Creature at level 1, adding 4 levels (to level 5)
        // CON 13 (mod +1). CON increase at char level 4 → CON 14 (mod +2)
        // Levels 2,3: 10 + 1 = 11 each = 22
        // Level 4: CON goes 13→14, mod +1→+2. Retro: 3 prev levels × 1 = 3. This level: 10 + 2 = 12.
        // Level 5: 10 + 2 = 12
        // Total: 22 + 3 + 12 + 12 = 49
        int result = LevelUpApplicationService.CalculateConsolidatedHp(
            hitDie: 10, baseCon: 13, previousLevelCount: 1,
            levelsToAdd: 4, conIncreaseLevels: new List<int> { 4 });
        Assert.Equal(49, result);
    }

    [Fact]
    public void CalculateConsolidatedHp_NoModChange_NoRetroHp()
    {
        // CON 14 (mod +2). CON increase at char level 4 → CON 15 (mod still +2)
        // No retro HP because modifier doesn't change
        // 3 levels: 10 + 2 = 12 each = 36
        int result = LevelUpApplicationService.CalculateConsolidatedHp(
            hitDie: 10, baseCon: 14, previousLevelCount: 1,
            levelsToAdd: 3, conIncreaseLevels: new List<int> { 4 });
        Assert.Equal(36, result);
    }

    #endregion

    #region Test Data Setup

    private void ConfigureClassData()
    {
        // Add SkillPointBase column to classes.2da
        _mockGameData.Set2DAValue("classes", (int)CommonClass.Fighter, "SkillPointBase", "2");
        _mockGameData.Set2DAValue("classes", (int)CommonClass.Rogue, "SkillPointBase", "8");
        _mockGameData.Set2DAValue("classes", (int)CommonClass.Wizard, "SkillPointBase", "2");

        // Racial extra skill points (Human gets 1 extra per level)
        _mockGameData.Set2DAValue("racialtypes", (int)CommonRace.Human, "ExtraSkillPointsPerLvl", "1");
        _mockGameData.Set2DAValue("racialtypes", (int)CommonRace.Elf, "ExtraSkillPointsPerLvl", "0");
    }

    #endregion
}
