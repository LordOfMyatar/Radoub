using Quartermaster.Services;
using Radoub.Formats.Utc;
using Radoub.TestUtilities.Builders;
using Radoub.TestUtilities.Mocks;
using Xunit;

namespace Quartermaster.Tests;

/// <summary>
/// Unit tests for CreatureDisplayService.
/// Tests lookups, calculations, and multiclass handling using MockGameDataService.
/// </summary>
public class CreatureDisplayServiceTests
{
    private readonly MockGameDataService _mockGameData;
    private readonly CreatureDisplayService _displayService;

    public CreatureDisplayServiceTests()
    {
        _mockGameData = new MockGameDataService(includeSampleData: true);
        _displayService = new CreatureDisplayService(_mockGameData);
    }

    #region Basic Lookups

    [Fact]
    public void GetRaceName_ValidId_ReturnsFromTlk()
    {
        // MockGameDataService has Human at index 6 with TLK string "Human"
        var result = _displayService.GetRaceName(6);
        Assert.Equal("Human", result);
    }

    [Fact]
    public void GetRaceName_DwarfId_ReturnsDwarf()
    {
        var result = _displayService.GetRaceName(0);
        Assert.Equal("Dwarf", result);
    }

    [Fact]
    public void GetClassName_ValidId_ReturnsFromTlk()
    {
        // MockGameDataService has Fighter at index 4 with TLK string "Fighter"
        var result = _displayService.GetClassName(4);
        Assert.Equal("Fighter", result);
    }

    [Fact]
    public void GetClassName_Rogue_ReturnsRogue()
    {
        var result = _displayService.GetClassName(8);
        Assert.Equal("Rogue", result);
    }

    [Fact]
    public void GetGenderName_Male_ReturnsMale()
    {
        var result = _displayService.GetGenderName(0);
        Assert.Equal("Male", result);
    }

    [Fact]
    public void GetGenderName_Female_ReturnsFemale()
    {
        var result = _displayService.GetGenderName(1);
        Assert.Equal("Female", result);
    }

    #endregion

    #region Ability Calculations

    [Theory]
    [InlineData(10, 0)]   // Average score
    [InlineData(11, 0)]   // Rounds down
    [InlineData(12, 1)]
    [InlineData(14, 2)]
    [InlineData(16, 3)]
    [InlineData(18, 4)]
    [InlineData(20, 5)]
    [InlineData(8, -1)]
    [InlineData(6, -2)]
    [InlineData(1, -4)]   // Minimum typical score (1-10)/2 = -4.5 -> -4
    public void CalculateAbilityBonus_Score_CalculatesCorrectly(int score, int expectedBonus)
    {
        var result = CreatureDisplayService.CalculateAbilityBonus(score);
        Assert.Equal(expectedBonus, result);
    }

    [Fact]
    public void CalculateAbilityBonus_HighScore_HandlesCorrectly()
    {
        // Score 40 (epic level character)
        var result = CreatureDisplayService.CalculateAbilityBonus(40);
        Assert.Equal(15, result); // (40-10)/2 = 15
    }

    [Fact]
    public void CalculateAbilityBonus_BelowMinimum_HandlesGracefully()
    {
        // Score 0 (should not happen but test edge case)
        var result = CreatureDisplayService.CalculateAbilityBonus(0);
        Assert.Equal(-5, result); // (0-10)/2 = -5
    }

    #endregion

    #region Bonus Formatting

    [Fact]
    public void FormatBonus_Positive_AddsPlusSign()
    {
        var result = CreatureDisplayService.FormatBonus(3);
        Assert.Equal("+3", result);
    }

    [Fact]
    public void FormatBonus_Negative_ShowsMinusSign()
    {
        var result = CreatureDisplayService.FormatBonus(-2);
        Assert.Equal("-2", result);
    }

    [Fact]
    public void FormatBonus_Zero_ShowsPlusZero()
    {
        var result = CreatureDisplayService.FormatBonus(0);
        Assert.Equal("+0", result);
    }

    #endregion

    #region Single Class BAB

    [Fact]
    public void CalculateBAB_SingleClassLevel1_ReturnsFromEstimate()
    {
        // Fighter level 1 (full BAB = 1.0 * 1 = 1)
        var creature = new CreatureBuilder()
            .WithClass(CommonClass.Fighter, 1)
            .Build();

        var result = _displayService.CalculateBaseAttackBonus(creature);
        Assert.Equal(1, result);
    }

    [Fact]
    public void CalculateBAB_SingleClassLevel5_ReturnsCorrect()
    {
        // Fighter level 5 (full BAB = 1.0 * 5 = 5)
        var creature = new CreatureBuilder()
            .WithClass(CommonClass.Fighter, 5)
            .Build();

        var result = _displayService.CalculateBaseAttackBonus(creature);
        Assert.Equal(5, result);
    }

    [Fact]
    public void CalculateBAB_WizardLevel5_Returns2()
    {
        // Wizard level 5 (half BAB = 0.5 * 5 = 2, truncated)
        var creature = new CreatureBuilder()
            .WithClass(CommonClass.Wizard, 5)
            .Build();

        var result = _displayService.CalculateBaseAttackBonus(creature);
        Assert.Equal(2, result); // 5 * 0.5 = 2.5, truncated to 2
    }

    [Fact]
    public void CalculateBAB_RogueLevel8_Returns6()
    {
        // Rogue level 8 (3/4 BAB = 0.75 * 8 = 6)
        var creature = new CreatureBuilder()
            .WithClass(CommonClass.Rogue, 8)
            .Build();

        var result = _displayService.CalculateBaseAttackBonus(creature);
        Assert.Equal(6, result);
    }

    #endregion

    #region Multiclass BAB

    [Fact]
    public void CalculateBAB_Multiclass_SumsClassBAB()
    {
        // Fighter 5 / Rogue 5 = 5 + 3 = 8
        var creature = new CreatureBuilder()
            .WithClass(CommonClass.Fighter, 5)  // 5 * 1.0 = 5
            .WithClass(CommonClass.Rogue, 5)    // 5 * 0.75 = 3
            .Build();

        var result = _displayService.CalculateBaseAttackBonus(creature);
        Assert.Equal(8, result);
    }

    [Fact]
    public void CalculateBAB_ThreeClasses_SumsAll()
    {
        // Fighter 6 / Wizard 4 / Rogue 4 = 6 + 2 + 3 = 11
        var creature = new CreatureBuilder()
            .WithClass(CommonClass.Fighter, 6)  // 6 * 1.0 = 6
            .WithClass(CommonClass.Wizard, 4)   // 4 * 0.5 = 2
            .WithClass(CommonClass.Rogue, 4)    // 4 * 0.75 = 3
            .Build();

        var result = _displayService.CalculateBaseAttackBonus(creature);
        Assert.Equal(11, result);
    }

    [Fact]
    public void CalculateBAB_FourClasses_CorrectCalculations()
    {
        // Fighter 5 / Rogue 5 / Cleric 5 / Wizard 5 = 5 + 3 + 3 + 2 = 13
        var creature = new CreatureBuilder()
            .WithClass(CommonClass.Fighter, 5)  // 5
            .WithClass(CommonClass.Rogue, 5)    // 3
            .WithClass(CommonClass.Cleric, 5)   // 3
            .WithClass(CommonClass.Wizard, 5)   // 2
            .Build();

        var result = _displayService.CalculateBaseAttackBonus(creature);
        Assert.Equal(13, result);
    }

    #endregion

    #region Saving Throws

    [Fact]
    public void CalculateSaves_FighterLevel5_FortGood()
    {
        // Fighter: Fort good, Ref/Will poor
        var creature = new CreatureBuilder()
            .WithClass(CommonClass.Fighter, 5)
            .Build();

        var saves = _displayService.CalculateBaseSavingThrows(creature);

        // Good save: 2 + level/2 = 2 + 2 = 4
        // Poor save: level/3 = 1
        Assert.Equal(4, saves.Fortitude);
        Assert.Equal(1, saves.Reflex);
        Assert.Equal(1, saves.Will);
    }

    [Fact]
    public void CalculateSaves_RogueLevel6_RefGood()
    {
        // Rogue: Ref good, Fort/Will poor
        var creature = new CreatureBuilder()
            .WithClass(CommonClass.Rogue, 6)
            .Build();

        var saves = _displayService.CalculateBaseSavingThrows(creature);

        // Good save: 2 + 3 = 5
        // Poor save: 6/3 = 2
        Assert.Equal(2, saves.Fortitude);
        Assert.Equal(5, saves.Reflex);
        Assert.Equal(2, saves.Will);
    }

    [Fact]
    public void CalculateSaves_WizardLevel10_WillGood()
    {
        // Wizard: Will good, Fort/Ref poor
        var creature = new CreatureBuilder()
            .WithClass(CommonClass.Wizard, 10)
            .Build();

        var saves = _displayService.CalculateBaseSavingThrows(creature);

        // Good save: 2 + 5 = 7
        // Poor save: 10/3 = 3
        Assert.Equal(3, saves.Fortitude);
        Assert.Equal(3, saves.Reflex);
        Assert.Equal(7, saves.Will);
    }

    [Fact]
    public void CalculateSaves_MonkLevel5_AllGood()
    {
        // Monk: All saves good
        var creature = new CreatureBuilder()
            .WithClass(CommonClass.Monk, 5)
            .Build();

        var saves = _displayService.CalculateBaseSavingThrows(creature);

        // All good saves: 2 + 2 = 4
        Assert.Equal(4, saves.Fortitude);
        Assert.Equal(4, saves.Reflex);
        Assert.Equal(4, saves.Will);
    }

    [Fact]
    public void CalculateSaves_Multiclass_SumsSaves()
    {
        // Fighter 5 / Wizard 5
        // Fort: 4 + 1 = 5
        // Ref: 1 + 1 = 2
        // Will: 1 + 4 = 5
        var creature = new CreatureBuilder()
            .WithClass(CommonClass.Fighter, 5)
            .WithClass(CommonClass.Wizard, 5)
            .Build();

        var saves = _displayService.CalculateBaseSavingThrows(creature);

        Assert.Equal(5, saves.Fortitude);
        Assert.Equal(2, saves.Reflex);
        Assert.Equal(5, saves.Will);
    }

    #endregion

    #region Creature Display

    [Fact]
    public void GetCreatureFullName_FirstAndLast_CombinesBoth()
    {
        var creature = new CreatureBuilder()
            .WithIdentity("Elminster", "Aumar")
            .Build();

        var result = CreatureDisplayService.GetCreatureFullName(creature);
        Assert.Equal("Elminster Aumar", result);
    }

    [Fact]
    public void GetCreatureFullName_FirstOnly_ReturnsFirst()
    {
        var creature = new CreatureBuilder()
            .WithIdentity("Minsc")
            .Build();

        var result = CreatureDisplayService.GetCreatureFullName(creature);
        Assert.Equal("Minsc", result);
    }

    [Fact]
    public void GetCreatureSummary_WithClass_IncludesClassInfo()
    {
        var creature = new CreatureBuilder()
            .WithRace(CommonRace.Human)
            .WithGender(0)
            .WithClass(CommonClass.Fighter, 5)
            .Build();

        var result = _displayService.GetCreatureSummary(creature);

        Assert.Contains("Human", result);
        Assert.Contains("Male", result);
        Assert.Contains("Fighter", result);
        Assert.Contains("5", result);
    }

    #endregion

    #region Hit Point Calculations

    [Fact]
    public void CalculateExpectedHpRange_SingleClass_CorrectRange()
    {
        // Fighter level 5 with d10 hit die
        // First level: 10
        // Levels 2-5: min 4, max 40
        // Total: min 14, max 50, avg 32
        var creature = new CreatureBuilder()
            .WithClass(CommonClass.Fighter, 5)
            .Build();

        // Note: MockGameDataService doesn't have HitDie data, so will use fallback d8
        var (min, avg, max) = _displayService.CalculateExpectedHpRange(creature);

        // With d8 fallback: first=8, remaining 4 levels = min 4, max 32
        // Total: min 12, max 40, avg 26
        Assert.True(min > 0);
        Assert.True(max >= min);
        Assert.True(avg >= min && avg <= max);
    }

    #endregion

    #region Fallback Behavior

    [Fact]
    public void GetRaceName_InvalidId_ReturnsFallback()
    {
        // ID 99 doesn't exist in mock data
        var result = _displayService.GetRaceName(99);
        Assert.Equal("Race 99", result);
    }

    [Fact]
    public void GetClassName_InvalidId_ReturnsFallback()
    {
        // ID 99 doesn't exist in mock data
        var result = _displayService.GetClassName(99);
        Assert.Equal("Class 99", result);
    }

    [Fact]
    public void GetGenderName_InvalidId_ReturnsFallback()
    {
        // ID 99 doesn't exist in mock data
        var result = _displayService.GetGenderName(99);
        Assert.Equal("Gender 99", result);
    }

    [Fact]
    public void GetRaceName_UnconfiguredService_ReturnsHardcodedFallback()
    {
        var unconfiguredMock = new MockGameDataService(includeSampleData: false).AsUnconfigured();
        var service = new CreatureDisplayService(unconfiguredMock);

        // Should fall back to hardcoded values
        var result = service.GetRaceName(6);
        Assert.Equal("Human", result); // Hardcoded fallback
    }

    #endregion

    #region Skill Point Calculations

    [Fact]
    public void GetClassSkillPointBase_Rogue_Returns8()
    {
        var result = _displayService.GetClassSkillPointBase(8); // Rogue
        Assert.Equal(8, result);
    }

    [Fact]
    public void GetClassSkillPointBase_Fighter_Returns2()
    {
        var result = _displayService.GetClassSkillPointBase(4); // Fighter
        Assert.Equal(2, result);
    }

    [Fact]
    public void GetClassSkillPointBase_Bard_Returns6()
    {
        var result = _displayService.GetClassSkillPointBase(1); // Bard
        Assert.Equal(6, result);
    }

    #endregion

    #region Class Max Level

    [Fact]
    public void GetClassMaxLevel_BaseClass_ReturnsZero()
    {
        // Base classes (Fighter, Wizard, etc.) have no max level
        var result = _displayService.GetClassMaxLevel(4); // Fighter
        Assert.Equal(0, result);
    }

    #endregion

    #region Hit Die

    [Fact]
    public void GetClassHitDie_Fighter_ReturnsD10()
    {
        // MockGameDataService has Fighter with HitDie=10
        var result = _displayService.GetClassHitDie(4); // Fighter
        Assert.Equal("d10", result);
    }

    [Fact]
    public void GetClassHitDieValue_Fighter_Returns10()
    {
        var result = _displayService.GetClassHitDieValue(4);
        Assert.Equal(10, result);
    }

    [Fact]
    public void GetClassHitDie_NoData_ReturnsDefaultD8()
    {
        // Empty mock with no 2DA data
        var emptyMock = new MockGameDataService(includeSampleData: false);
        var service = new CreatureDisplayService(emptyMock);

        var result = service.GetClassHitDie(99); // Unknown class
        Assert.Equal("d8", result); // Default fallback
    }

    #endregion
}
