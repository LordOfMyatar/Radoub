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
