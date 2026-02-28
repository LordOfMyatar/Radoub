using Quartermaster.Services;
using Radoub.Formats.Utc;
using Radoub.TestUtilities.Builders;
using Radoub.TestUtilities.Mocks;
using Xunit;

namespace Quartermaster.Tests;

/// <summary>
/// Tests for CharacterCreationService — character building and helper methods.
/// </summary>
public class CharacterCreationServiceTests
{
    private readonly MockGameDataService _mockGameData;
    private readonly CreatureDisplayService _displayService;
    private readonly CharacterCreationService _service;

    public CharacterCreationServiceTests()
    {
        _mockGameData = new MockGameDataService(includeSampleData: true);
        ConfigureCreationData();
        _displayService = new CreatureDisplayService(_mockGameData);
        _service = new CharacterCreationService(_displayService, _mockGameData);
    }

    #region SanitizeForResRef

    [Theory]
    [InlineData("TestName", "testname")]
    [InlineData("My Character", "my_character")]
    [InlineData("UPPERCASE", "uppercase")]
    [InlineData("with-dashes", "withdashes")]
    [InlineData("with.dots", "withdots")]
    [InlineData("special!@#$chars", "specialchars")]
    [InlineData("", "")]
    [InlineData("already_valid", "already_valid")]
    public void SanitizeForResRef_ProducesExpectedOutput(string input, string expected)
    {
        Assert.Equal(expected, CharacterCreationService.SanitizeForResRef(input));
    }

    [Fact]
    public void SanitizeForResRef_TruncatesTo16Chars()
    {
        var longName = "AVeryLongCharacterNameThatExceedsSixteenCharacters";
        var result = CharacterCreationService.SanitizeForResRef(longName);
        Assert.True(result.Length <= 16, $"ResRef '{result}' exceeds 16 chars (length={result.Length})");
    }

    [Fact]
    public void SanitizeForResRef_AllowsUnderscores()
    {
        var result = CharacterCreationService.SanitizeForResRef("test_name");
        Assert.Equal("test_name", result);
    }

    [Fact]
    public void SanitizeForResRef_ReplacesSpacesWithUnderscores()
    {
        var result = CharacterCreationService.SanitizeForResRef("my char");
        Assert.Equal("my_char", result);
    }

    #endregion

    #region GetAlignmentName

    [Theory]
    [InlineData(100, 100, "Lawful Good")]
    [InlineData(100, 50, "Neutral Good")]
    [InlineData(100, 0, "Chaotic Good")]
    [InlineData(50, 100, "Lawful Neutral")]
    [InlineData(50, 50, "True Neutral")]
    [InlineData(50, 0, "Chaotic Neutral")]
    [InlineData(0, 100, "Lawful Evil")]
    [InlineData(0, 50, "Neutral Evil")]
    [InlineData(0, 0, "Chaotic Evil")]
    public void GetAlignmentName_ReturnsExpectedAlignment(byte goodEvil, byte lawChaos, string expected)
    {
        Assert.Equal(expected, CharacterCreationService.GetAlignmentName(goodEvil, lawChaos));
    }

    [Fact]
    public void GetAlignmentName_BoundaryValues_Good()
    {
        // Good = strictly > 70
        Assert.Contains("Good", CharacterCreationService.GetAlignmentName(71, 50));
        Assert.DoesNotContain("Good", CharacterCreationService.GetAlignmentName(70, 50)); // 70 is Neutral
    }

    [Fact]
    public void GetAlignmentName_BoundaryValues_Evil()
    {
        // Evil = strictly < 30
        Assert.Contains("Evil", CharacterCreationService.GetAlignmentName(29, 50));
        Assert.DoesNotContain("Evil", CharacterCreationService.GetAlignmentName(30, 50)); // 30 is Neutral
    }

    [Fact]
    public void GetAlignmentName_BoundaryValues_Lawful()
    {
        // Lawful = strictly > 70
        Assert.Contains("Lawful", CharacterCreationService.GetAlignmentName(50, 71));
        Assert.DoesNotContain("Lawful", CharacterCreationService.GetAlignmentName(50, 70)); // 70 is Neutral
    }

    [Fact]
    public void GetAlignmentName_BoundaryValues_Chaotic()
    {
        // Chaotic = strictly < 30
        Assert.Contains("Chaotic", CharacterCreationService.GetAlignmentName(50, 29));
        Assert.DoesNotContain("Chaotic", CharacterCreationService.GetAlignmentName(50, 30)); // 30 is Neutral
    }

    #endregion

    #region CalculateMaxSkillRanks

    [Theory]
    [InlineData(true, 1, 4)]   // Class: 1+3 = 4
    [InlineData(true, 5, 8)]   // Class: 5+3 = 8
    [InlineData(false, 1, 2)]  // Cross-class: (1+3)/2 = 2
    [InlineData(false, 5, 4)]  // Cross-class: (5+3)/2 = 4
    public void CalculateMaxSkillRanks_ReturnsExpected(bool isClassSkill, int level, int expected)
    {
        Assert.Equal(expected, CharacterCreationService.CalculateMaxSkillRanks(isClassSkill, level));
    }

    #endregion

    #region CalculateLevel1HP

    [Fact]
    public void CalculateLevel1HP_FighterWithHighCon_GetsBonus()
    {
        // Fighter hit die = 10, CON 14 = +2 modifier
        int hp = _service.CalculateLevel1HP((int)CommonClass.Fighter, 6, 14);
        // d10 max + 2 = 12
        Assert.Equal(12, hp);
    }

    [Fact]
    public void CalculateLevel1HP_WizardWithLowCon_GetsMinimum()
    {
        // Wizard hit die = 4, CON 8 = -1 modifier
        int hp = _service.CalculateLevel1HP((int)CommonClass.Wizard, 6, 8);
        // d4 max + (-1) = 3, minimum should be 1
        Assert.True(hp >= 1, $"HP should be at least 1, got {hp}");
    }

    #endregion

    #region CalculateLevel1SkillPoints

    [Fact]
    public void CalculateLevel1SkillPoints_Fighter_Gets4xMultiplier()
    {
        int points = _service.CalculateLevel1SkillPoints(
            (int)CommonClass.Fighter, (byte)CommonRace.Human, 10);
        Assert.True(points > 0, "Should have skill points at level 1");
        Assert.True(points >= 4, $"Level 1 with 4x multiplier should give >= 4 points, got {points}");
    }

    [Fact]
    public void CalculateLevel1SkillPoints_Rogue_GetsHigherBase()
    {
        int roguePoints = _service.CalculateLevel1SkillPoints(
            (int)CommonClass.Rogue, (byte)CommonRace.Human, 10);
        int fighterPoints = _service.CalculateLevel1SkillPoints(
            (int)CommonClass.Fighter, (byte)CommonRace.Human, 10);

        Assert.True(roguePoints > fighterPoints,
            $"Rogue ({roguePoints}) should get more skill points than Fighter ({fighterPoints})");
    }

    [Fact]
    public void CalculateLevel1SkillPoints_HighInt_MorePoints()
    {
        int lowIntPoints = _service.CalculateLevel1SkillPoints(
            (int)CommonClass.Fighter, (byte)CommonRace.Human, 10);
        int highIntPoints = _service.CalculateLevel1SkillPoints(
            (int)CommonClass.Fighter, (byte)CommonRace.Human, 16);

        Assert.True(highIntPoints > lowIntPoints,
            $"High INT ({highIntPoints}) should give more points than low INT ({lowIntPoints})");
    }

    #endregion

    #region ApplyDefaultScripts

    [Fact]
    public void ApplyDefaultScripts_SetsAllEventScripts()
    {
        var utc = new UtcFile();
        CharacterCreationService.ApplyDefaultScripts(utc);

        Assert.Equal("nw_c2_default9", utc.ScriptSpawn);
        Assert.Equal("nw_c2_default1", utc.ScriptHeartbeat);
        Assert.Equal("nw_c2_default7", utc.ScriptDeath);
        Assert.Equal("nw_c2_default5", utc.ScriptAttacked);
        Assert.Equal("nw_c2_default6", utc.ScriptDamaged);
        Assert.Equal("nw_c2_default4", utc.ScriptDialogue);
        Assert.Equal("nw_c2_default8", utc.ScriptDisturbed);
        Assert.Equal("nw_c2_default3", utc.ScriptEndRound);
        Assert.Equal("nw_c2_defaulte", utc.ScriptOnBlocked);
        Assert.Equal("nw_c2_default2", utc.ScriptOnNotice);
        Assert.Equal("nw_c2_defaulta", utc.ScriptRested);
        Assert.Equal("nw_c2_defaultd", utc.ScriptUserDefine);
    }

    [Fact]
    public void ApplyDefaultScripts_OverwritesExistingScripts()
    {
        var utc = new UtcFile
        {
            ScriptSpawn = "custom_spawn",
            ScriptDeath = "custom_death"
        };

        CharacterCreationService.ApplyDefaultScripts(utc);

        Assert.Equal("nw_c2_default9", utc.ScriptSpawn);
        Assert.Equal("nw_c2_default7", utc.ScriptDeath);
    }

    #endregion

    #region BuildEquipmentLists

    [Fact]
    public void BuildEquipmentLists_SeparatesEquippedAndBackpack()
    {
        var items = new List<CharacterCreationService.EquipmentItem>
        {
            new() { ResRef = "sword01", Name = "Longsword", SlotFlags = EquipmentSlots.RightHand },
            new() { ResRef = "shield01", Name = "Shield", SlotFlags = EquipmentSlots.LeftHand },
            new() { ResRef = "potion01", Name = "Potion", SlotFlags = 0 } // Backpack item
        };

        var (equipped, backpack) = CharacterCreationService.BuildEquipmentLists(items);

        Assert.Equal(2, equipped.Count);
        Assert.Single(backpack);
        Assert.Equal("potion01", backpack[0].InventoryRes);
    }

    [Fact]
    public void BuildEquipmentLists_EmptyList_ReturnsBothEmpty()
    {
        var (equipped, backpack) = CharacterCreationService.BuildEquipmentLists(
            new List<CharacterCreationService.EquipmentItem>());

        Assert.Empty(equipped);
        Assert.Empty(backpack);
    }

    [Fact]
    public void BuildEquipmentLists_AllEquipped_NoBackpack()
    {
        var items = new List<CharacterCreationService.EquipmentItem>
        {
            new() { ResRef = "helm01", Name = "Helmet", SlotFlags = EquipmentSlots.Head },
            new() { ResRef = "armor01", Name = "Armor", SlotFlags = EquipmentSlots.Chest }
        };

        var (equipped, backpack) = CharacterCreationService.BuildEquipmentLists(items);

        Assert.Equal(2, equipped.Count);
        Assert.Empty(backpack);
    }

    #endregion

    #region BuildCreature

    [Fact]
    public void BuildCreature_SetsBasicProperties()
    {
        var input = CreateBasicInput();

        var creature = _service.BuildCreature(input);

        Assert.NotNull(creature);
        Assert.Equal("Test Character", creature.FirstName.LocalizedStrings[0]);
        Assert.Equal(6, creature.Race); // Human
        Assert.Equal((byte)0, creature.Gender);
    }

    [Fact]
    public void BuildCreature_SetsAbilityScores()
    {
        var input = CreateBasicInput();
        input.AbilityBaseScores = new Dictionary<string, int>
        {
            { "STR", 16 }, { "DEX", 14 }, { "CON", 14 },
            { "INT", 10 }, { "WIS", 12 }, { "CHA", 8 }
        };

        var creature = _service.BuildCreature(input);

        Assert.Equal(16, creature.Str);
        Assert.Equal(14, creature.Dex);
        Assert.Equal(14, creature.Con);
        Assert.Equal(10, creature.Int);
        Assert.Equal(12, creature.Wis);
        Assert.Equal(8, creature.Cha);
    }

    [Fact]
    public void BuildCreature_AddsClassEntry()
    {
        var input = CreateBasicInput();
        input.ClassId = 4; // Fighter

        var creature = _service.BuildCreature(input);

        Assert.Single(creature.ClassList);
        Assert.Equal(4, creature.ClassList[0].Class);
        Assert.Equal(1, creature.ClassList[0].ClassLevel);
    }

    [Fact]
    public void BuildCreature_SetsAlignment()
    {
        var input = CreateBasicInput();
        input.GoodEvil = 100;
        input.LawChaos = 100;

        var creature = _service.BuildCreature(input);

        Assert.Equal(100, creature.GoodEvil);
        Assert.Equal(100, creature.LawfulChaotic);
    }

    [Fact]
    public void BuildCreature_SetsSkillRanks()
    {
        var input = CreateBasicInput();
        input.SkillRanksAllocated = new Dictionary<int, int>
        {
            { 0, 4 },
            { 5, 2 }
        };

        var creature = _service.BuildCreature(input);

        Assert.True(creature.SkillList.Count > 5);
        Assert.Equal(4, creature.SkillList[0]);
        Assert.Equal(2, creature.SkillList[5]);
    }

    [Fact]
    public void BuildCreature_AddsChosenFeats()
    {
        var input = CreateBasicInput();
        input.ChosenFeatIds = new List<int> { 10, 20 };

        var creature = _service.BuildCreature(input);

        Assert.Contains((ushort)10, creature.FeatList);
        Assert.Contains((ushort)20, creature.FeatList);
    }

    [Fact]
    public void BuildCreature_AppliesDefaultScripts_WhenRequested()
    {
        var input = CreateBasicInput();
        input.IsBicFile = false; // UTC
        input.ApplyDefaultScripts = true;

        var creature = _service.BuildCreature(input);

        Assert.Equal("nw_c2_default9", creature.ScriptSpawn);
    }

    [Fact]
    public void BuildCreature_NoScripts_WhenBicFile()
    {
        var input = CreateBasicInput();
        input.IsBicFile = true;
        input.ApplyDefaultScripts = true; // Should be ignored for BIC

        var creature = _service.BuildCreature(input);

        // BIC files shouldn't get default scripts
        Assert.True(string.IsNullOrEmpty(creature.ScriptSpawn) ||
                     creature.ScriptSpawn != "nw_c2_default9",
            "BIC files should not get UTC default scripts unless explicitly set");
    }

    #endregion

    #region Test Data Setup

    private void ConfigureCreationData()
    {
        // Add SkillPointBase column
        _mockGameData.Set2DAValue("classes", 4, "SkillPointBase", "2");  // Fighter
        _mockGameData.Set2DAValue("classes", 8, "SkillPointBase", "8");  // Rogue
        _mockGameData.Set2DAValue("classes", 10, "SkillPointBase", "2"); // Wizard

        // Racial extra skill points
        _mockGameData.Set2DAValue("racialtypes", 6, "ExtraSkillPointsPerLvl", "1"); // Human
        _mockGameData.Set2DAValue("racialtypes", 1, "ExtraSkillPointsPerLvl", "0"); // Elf

        // Add Feats column
        _mockGameData.Set2DAValue("racialtypes", 6, "FeatsTable", "****"); // Human
    }

    private static CharacterCreationService.CharacterCreationInput CreateBasicInput()
    {
        return new CharacterCreationService.CharacterCreationInput
        {
            CharacterName = "Test Character",
            RaceId = 6, // Human
            Gender = 0, // Male
            ClassId = 4, // Fighter
            GoodEvil = 50,
            LawChaos = 50,
            AppearanceId = 0,
            PortraitId = 0,
            VoiceSetId = 0,
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

    #endregion
}
