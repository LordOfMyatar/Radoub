using System.Collections.Generic;
using System.Linq;
using Quartermaster.Services;
using Radoub.Formats.Utc;
using Radoub.TestUtilities.Builders;
using Radoub.TestUtilities.Mocks;
using Xunit;

namespace Quartermaster.Tests;

/// <summary>
/// Round-trip validation tests for character creation and level-up.
/// Verifies: create → serialize → deserialize → verify fields match.
/// </summary>
public class RoundTripValidationTests
{
    private readonly MockGameDataService _mockGameData;
    private readonly CreatureDisplayService _displayService;
    private readonly CharacterCreationService _creationService;
    private readonly LevelUpApplicationService _levelUpService;

    public RoundTripValidationTests()
    {
        _mockGameData = new MockGameDataService(includeSampleData: true);
        ConfigureMockData();
        _displayService = new CreatureDisplayService(_mockGameData);
        _creationService = new CharacterCreationService(_displayService, _mockGameData);
        _levelUpService = new LevelUpApplicationService(_displayService);
    }

    #region Character Creation Round-Trip

    [Fact]
    public void BuildCreature_RoundTrip_PreservesIdentity()
    {
        var input = CreateFighterInput();
        input.CharacterName = "Gorion's Ward";
        input.LastName = "Bhaalspawn";

        var original = _creationService.BuildCreature(input);
        var restored = RoundTrip(original);

        Assert.Equal(original.FirstName.LocalizedStrings[0], restored.FirstName.LocalizedStrings[0]);
        Assert.Equal(original.LastName.LocalizedStrings[0], restored.LastName.LocalizedStrings[0]);
        Assert.Equal(original.Tag, restored.Tag);
        Assert.Equal(original.TemplateResRef, restored.TemplateResRef);
    }

    [Fact]
    public void BuildCreature_RoundTrip_PreservesRaceAndGender()
    {
        var input = CreateFighterInput();
        input.RaceId = (byte)CommonRace.Elf;
        input.Gender = 1; // Female

        var original = _creationService.BuildCreature(input);
        var restored = RoundTrip(original);

        Assert.Equal(original.Race, restored.Race);
        Assert.Equal(original.Gender, restored.Gender);
    }

    [Fact]
    public void BuildCreature_RoundTrip_PreservesAbilityScores()
    {
        var input = CreateFighterInput();
        input.AbilityBaseScores = new Dictionary<string, int>
        {
            { "STR", 16 }, { "DEX", 14 }, { "CON", 14 },
            { "INT", 10 }, { "WIS", 12 }, { "CHA", 8 }
        };

        var original = _creationService.BuildCreature(input);
        var restored = RoundTrip(original);

        Assert.Equal(original.Str, restored.Str);
        Assert.Equal(original.Dex, restored.Dex);
        Assert.Equal(original.Con, restored.Con);
        Assert.Equal(original.Int, restored.Int);
        Assert.Equal(original.Wis, restored.Wis);
        Assert.Equal(original.Cha, restored.Cha);
    }

    [Fact]
    public void BuildCreature_RoundTrip_PreservesClassAndLevel()
    {
        var input = CreateFighterInput();

        var original = _creationService.BuildCreature(input);
        var restored = RoundTrip(original);

        Assert.Equal(original.ClassList.Count, restored.ClassList.Count);
        Assert.Equal(original.ClassList[0].Class, restored.ClassList[0].Class);
        Assert.Equal(original.ClassList[0].ClassLevel, restored.ClassList[0].ClassLevel);
    }

    [Fact]
    public void BuildCreature_RoundTrip_PreservesAlignment()
    {
        var input = CreateFighterInput();
        input.GoodEvil = 100; // Good
        input.LawChaos = 100; // Lawful

        var original = _creationService.BuildCreature(input);
        var restored = RoundTrip(original);

        Assert.Equal(original.GoodEvil, restored.GoodEvil);
        Assert.Equal(original.LawfulChaotic, restored.LawfulChaotic);
    }

    [Fact]
    public void BuildCreature_RoundTrip_PreservesHitPoints()
    {
        var input = CreateFighterInput();

        var original = _creationService.BuildCreature(input);
        var restored = RoundTrip(original);

        Assert.Equal(original.HitPoints, restored.HitPoints);
        Assert.Equal(original.CurrentHitPoints, restored.CurrentHitPoints);
        Assert.Equal(original.MaxHitPoints, restored.MaxHitPoints);
    }

    [Fact]
    public void BuildCreature_RoundTrip_PreservesSkills()
    {
        var input = CreateFighterInput();
        input.SkillRanksAllocated = new Dictionary<int, int>
        {
            { 0, 4 },
            { 3, 2 },
            { 7, 1 }
        };

        var original = _creationService.BuildCreature(input);
        var restored = RoundTrip(original);

        Assert.Equal(original.SkillList.Count, restored.SkillList.Count);
        for (int i = 0; i < original.SkillList.Count; i++)
        {
            Assert.Equal(original.SkillList[i], restored.SkillList[i]);
        }
    }

    [Fact]
    public void BuildCreature_RoundTrip_PreservesFeats()
    {
        var input = CreateFighterInput();
        input.ChosenFeatIds = new List<int> { 1, 2, 45 };

        var original = _creationService.BuildCreature(input);
        var restored = RoundTrip(original);

        Assert.Equal(original.FeatList.Count, restored.FeatList.Count);
        foreach (var feat in original.FeatList)
        {
            Assert.Contains(feat, restored.FeatList);
        }
    }

    [Fact]
    public void BuildCreature_RoundTrip_PreservesScripts()
    {
        var input = CreateFighterInput();
        input.IsBicFile = false;
        input.ApplyDefaultScripts = true;

        var original = _creationService.BuildCreature(input);
        var restored = RoundTrip(original);

        Assert.Equal(original.ScriptSpawn, restored.ScriptSpawn);
        Assert.Equal(original.ScriptHeartbeat, restored.ScriptHeartbeat);
        Assert.Equal(original.ScriptDeath, restored.ScriptDeath);
        Assert.Equal(original.ScriptAttacked, restored.ScriptAttacked);
        Assert.Equal(original.ScriptDamaged, restored.ScriptDamaged);
        Assert.Equal(original.ScriptDialogue, restored.ScriptDialogue);
        Assert.Equal(original.ScriptDisturbed, restored.ScriptDisturbed);
        Assert.Equal(original.ScriptEndRound, restored.ScriptEndRound);
        Assert.Equal(original.ScriptOnBlocked, restored.ScriptOnBlocked);
        Assert.Equal(original.ScriptOnNotice, restored.ScriptOnNotice);
        Assert.Equal(original.ScriptRested, restored.ScriptRested);
        Assert.Equal(original.ScriptUserDefine, restored.ScriptUserDefine);
    }

    [Fact]
    public void BuildCreature_RoundTrip_PreservesAppearance()
    {
        var input = CreateFighterInput();
        input.AppearanceId = 6;
        input.PortraitId = 42;

        var original = _creationService.BuildCreature(input);
        var restored = RoundTrip(original);

        Assert.Equal(original.AppearanceType, restored.AppearanceType);
        Assert.Equal(original.PortraitId, restored.PortraitId);
    }

    #endregion

    #region Level-Up Round-Trip

    [Fact]
    public void LevelUp_RoundTrip_PreservesClassLevel()
    {
        var creature = new CreatureBuilder()
            .WithIdentity("TestFighter")
            .WithRace(CommonRace.Human)
            .WithClass(CommonClass.Fighter, 3)
            .WithAbilities(16, 14, 14, 10, 10, 8)
            .WithHitPoints(30)
            .Build();

        _levelUpService.ApplyLevelUp(creature, new LevelUpApplicationService.LevelUpInput
        {
            SelectedClassId = (int)CommonClass.Fighter,
            NewClassLevel = 4,
            SelectedFeats = new List<int>(),
            SkillPointsAdded = new Dictionary<int, int>(),
            SelectedSpellsByLevel = new Dictionary<int, List<int>>(),
            RecordHistory = false
        });

        var restored = RoundTrip(creature);

        Assert.Single(restored.ClassList);
        Assert.Equal(4, restored.ClassList[0].ClassLevel);
    }

    [Fact]
    public void LevelUp_RoundTrip_PreservesNewFeats()
    {
        var creature = new CreatureBuilder()
            .WithIdentity("TestFighter")
            .WithRace(CommonRace.Human)
            .WithClass(CommonClass.Fighter, 2)
            .WithAbilities(16, 14, 14, 10, 10, 8)
            .WithFeat(1)
            .Build();

        _levelUpService.ApplyLevelUp(creature, new LevelUpApplicationService.LevelUpInput
        {
            SelectedClassId = (int)CommonClass.Fighter,
            NewClassLevel = 3,
            SelectedFeats = new List<int> { 42, 99 },
            SkillPointsAdded = new Dictionary<int, int>(),
            SelectedSpellsByLevel = new Dictionary<int, List<int>>(),
            RecordHistory = false
        });

        var restored = RoundTrip(creature);

        Assert.Contains((ushort)1, restored.FeatList);  // Original feat preserved
        Assert.Contains((ushort)42, restored.FeatList);  // New feat
        Assert.Contains((ushort)99, restored.FeatList);  // New feat
    }

    [Fact]
    public void LevelUp_RoundTrip_PreservesSkillChanges()
    {
        var creature = new CreatureBuilder()
            .WithIdentity("TestRogue")
            .WithRace(CommonRace.Human)
            .WithClass(CommonClass.Rogue, 1)
            .WithAbilities(10, 16, 12, 14, 10, 14)
            .WithSkillRanks(4, 0, 0, 4, 0, 0, 2) // 7 skills
            .Build();

        _levelUpService.ApplyLevelUp(creature, new LevelUpApplicationService.LevelUpInput
        {
            SelectedClassId = (int)CommonClass.Rogue,
            NewClassLevel = 2,
            SelectedFeats = new List<int>(),
            SkillPointsAdded = new Dictionary<int, int>
            {
                { 0, 1 }, // +1 to skill 0
                { 3, 1 }, // +1 to skill 3
                { 6, 1 }  // +1 to skill 6
            },
            SelectedSpellsByLevel = new Dictionary<int, List<int>>(),
            RecordHistory = false
        });

        var restored = RoundTrip(creature);

        Assert.Equal(5, restored.SkillList[0]); // 4 + 1
        Assert.Equal(0, restored.SkillList[1]); // Unchanged
        Assert.Equal(5, restored.SkillList[3]); // 4 + 1
        Assert.Equal(3, restored.SkillList[6]); // 2 + 1
    }

    [Fact]
    public void LevelUp_RoundTrip_PreservesMulticlass()
    {
        var creature = new CreatureBuilder()
            .WithIdentity("TestMulticlass")
            .WithRace(CommonRace.Human)
            .WithClass(CommonClass.Fighter, 5)
            .WithAbilities(16, 14, 14, 10, 10, 8)
            .Build();

        // Add a new class
        _levelUpService.ApplyLevelUp(creature, new LevelUpApplicationService.LevelUpInput
        {
            SelectedClassId = (int)CommonClass.Rogue,
            NewClassLevel = 1,
            IsNewClass = true,
            SelectedFeats = new List<int>(),
            SkillPointsAdded = new Dictionary<int, int>(),
            SelectedSpellsByLevel = new Dictionary<int, List<int>>(),
            RecordHistory = false
        });

        var restored = RoundTrip(creature);

        Assert.Equal(2, restored.ClassList.Count);
        Assert.Equal((int)CommonClass.Fighter, restored.ClassList[0].Class);
        Assert.Equal(5, restored.ClassList[0].ClassLevel);
        Assert.Equal((int)CommonClass.Rogue, restored.ClassList[1].Class);
        Assert.Equal(1, restored.ClassList[1].ClassLevel);
    }

    [Fact]
    public void LevelUp_RoundTrip_PreservesLevelHistory()
    {
        var creature = new CreatureBuilder()
            .WithIdentity("TestHistoried")
            .WithRace(CommonRace.Human)
            .WithClass(CommonClass.Fighter, 1)
            .WithAbilities(16, 14, 14, 10, 10, 8)
            .Build();

        _levelUpService.ApplyLevelUp(creature, new LevelUpApplicationService.LevelUpInput
        {
            SelectedClassId = (int)CommonClass.Fighter,
            NewClassLevel = 2,
            SelectedFeats = new List<int> { 10 },
            SkillPointsAdded = new Dictionary<int, int> { { 0, 2 } },
            SelectedSpellsByLevel = new Dictionary<int, List<int>>(),
            RecordHistory = true,
            HistoryEncoding = LevelHistoryEncoding.Readable
        });

        var restored = RoundTrip(creature);

        // History is stored in Comment field
        Assert.True(LevelHistoryService.HasLevelHistory(restored.Comment),
            "Level history should survive round-trip");

        var history = LevelHistoryService.Decode(restored.Comment);
        Assert.NotNull(history);
        Assert.Single(history);
        Assert.Equal(2, history[0].TotalLevel);
        Assert.Equal((int)CommonClass.Fighter, history[0].ClassId);
    }

    #endregion

    #region Multi-Step Round-Trip

    [Fact]
    public void CreateThenLevelUp_RoundTrip_PreservesAllData()
    {
        // Step 1: Create a character
        var input = CreateFighterInput();
        input.AbilityBaseScores = new Dictionary<string, int>
        {
            { "STR", 16 }, { "DEX", 14 }, { "CON", 14 },
            { "INT", 10 }, { "WIS", 12 }, { "CHA", 8 }
        };
        input.ChosenFeatIds = new List<int> { 1, 2 };
        input.SkillRanksAllocated = new Dictionary<int, int>
        {
            { 0, 4 },
            { 3, 2 }
        };

        var creature = _creationService.BuildCreature(input);

        // Round-trip after creation
        creature = RoundTrip(creature);

        // Step 2: Level up the created character
        _levelUpService.ApplyLevelUp(creature, new LevelUpApplicationService.LevelUpInput
        {
            SelectedClassId = (int)CommonClass.Fighter,
            NewClassLevel = 2,
            SelectedFeats = new List<int> { 50 },
            SkillPointsAdded = new Dictionary<int, int> { { 0, 1 } },
            SelectedSpellsByLevel = new Dictionary<int, List<int>>(),
            RecordHistory = false
        });

        // Round-trip after level-up
        var final = RoundTrip(creature);

        // Verify everything survived both round-trips
        Assert.Equal(16, final.Str);
        Assert.Equal(14, final.Dex);
        Assert.Equal(2, final.ClassList[0].ClassLevel);
        Assert.Equal(5, final.SkillList[0]); // 4 + 1
        Assert.Contains((ushort)1, final.FeatList);
        Assert.Contains((ushort)2, final.FeatList);
        Assert.Contains((ushort)50, final.FeatList);
    }

    [Fact]
    public void MultipleLevelUps_RoundTrip_PreservesAll()
    {
        var creature = new CreatureBuilder()
            .WithIdentity("Veteran")
            .WithRace(CommonRace.Human)
            .WithClass(CommonClass.Fighter, 1)
            .WithAbilities(16, 14, 14, 10, 10, 8)
            .WithHitPoints(12)
            .WithSkillRanks(4, 2, 0, 0, 0)
            .Build();

        // Level up 3 times with round-trips between each
        for (int level = 2; level <= 4; level++)
        {
            _levelUpService.ApplyLevelUp(creature, new LevelUpApplicationService.LevelUpInput
            {
                SelectedClassId = (int)CommonClass.Fighter,
                NewClassLevel = level,
                SelectedFeats = level == 3 ? new List<int> { 100 } : new List<int>(),
                SkillPointsAdded = new Dictionary<int, int> { { 0, 1 } },
                SelectedSpellsByLevel = new Dictionary<int, List<int>>(),
                RecordHistory = false
            });

            creature = RoundTrip(creature);
        }

        Assert.Equal(4, creature.ClassList[0].ClassLevel);
        Assert.Equal(7, creature.SkillList[0]); // 4 + 1 + 1 + 1
        Assert.Contains((ushort)100, creature.FeatList); // Feat from level 3
    }

    #endregion

    #region Helpers

    private static UtcFile RoundTrip(UtcFile original)
    {
        byte[] serialized = UtcWriter.Write(original);
        return UtcReader.Read(serialized);
    }

    private static CharacterCreationService.CharacterCreationInput CreateFighterInput()
    {
        return new CharacterCreationService.CharacterCreationInput
        {
            CharacterName = "TestFighter",
            RaceId = (byte)CommonRace.Human,
            Gender = 0,
            ClassId = (int)CommonClass.Fighter,
            GoodEvil = 50,
            LawChaos = 50,
            AppearanceId = 0,
            PortraitId = 1,
            IsBicFile = false,
            ApplyDefaultScripts = false,
            AbilityBaseScores = new Dictionary<string, int>
            {
                { "STR", 14 }, { "DEX", 12 }, { "CON", 14 },
                { "INT", 10 }, { "WIS", 10 }, { "CHA", 10 }
            },
            SkillRanksAllocated = new Dictionary<int, int>(),
            ChosenFeatIds = new List<int>(),
            SelectedSpellsByLevel = new Dictionary<int, List<int>>(),
            EquipmentItems = new List<CharacterCreationService.EquipmentItem>()
        };
    }

    private void ConfigureMockData()
    {
        _mockGameData.Set2DAValue("classes", (int)CommonClass.Fighter, "SkillPointBase", "2");
        _mockGameData.Set2DAValue("classes", (int)CommonClass.Rogue, "SkillPointBase", "8");
        _mockGameData.Set2DAValue("racialtypes", (int)CommonRace.Human, "ExtraSkillPointsPerLvl", "1");
    }

    #endregion
}
