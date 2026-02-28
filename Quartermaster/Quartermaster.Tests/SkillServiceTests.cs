using Quartermaster.Services;
using Radoub.TestUtilities.Builders;
using Radoub.TestUtilities.Mocks;
using Xunit;

namespace Quartermaster.Tests;

/// <summary>
/// Dedicated tests for SkillService: name lookups, class skills, availability, auto-assign.
/// </summary>
public class SkillServiceTests
{
    private readonly MockGameDataService _mockGameData;
    private readonly SkillService _skillService;

    public SkillServiceTests()
    {
        _mockGameData = new MockGameDataService(includeSampleData: true);
        SetupSkillData();
        _skillService = new SkillService(_mockGameData);
    }

    private void SetupSkillData()
    {
        // skills.2da: Name (TLK strRef), KeyAbility, AllClassesCanUse
        _mockGameData.Set2DAValue("skills", 0, "Name", "200");
        _mockGameData.Set2DAValue("skills", 0, "KeyAbility", "CHA");
        _mockGameData.Set2DAValue("skills", 0, "AllClassesCanUse", "0");

        _mockGameData.Set2DAValue("skills", 1, "Name", "201");
        _mockGameData.Set2DAValue("skills", 1, "KeyAbility", "CON");
        _mockGameData.Set2DAValue("skills", 1, "AllClassesCanUse", "1");

        _mockGameData.Set2DAValue("skills", 2, "Name", "202");
        _mockGameData.Set2DAValue("skills", 2, "KeyAbility", "INT");
        _mockGameData.Set2DAValue("skills", 2, "AllClassesCanUse", "0");

        _mockGameData.Set2DAValue("skills", 3, "Name", "203");
        _mockGameData.Set2DAValue("skills", 3, "KeyAbility", "STR");
        _mockGameData.Set2DAValue("skills", 3, "AllClassesCanUse", "0");

        _mockGameData.SetTlkString(200, "Animal Empathy");
        _mockGameData.SetTlkString(201, "Concentration");
        _mockGameData.SetTlkString(202, "Disable Trap");
        _mockGameData.SetTlkString(203, "Discipline");

        // cls_skill_fight: SkillIndex, ClassSkill
        // Fighter gets Discipline (3) as class skill, Disable Trap (2) appears but not class skill
        _mockGameData.Set2DAValue("cls_skill_fight", 0, "SkillIndex", "2");
        _mockGameData.Set2DAValue("cls_skill_fight", 0, "ClassSkill", "0");
        _mockGameData.Set2DAValue("cls_skill_fight", 1, "SkillIndex", "3");
        _mockGameData.Set2DAValue("cls_skill_fight", 1, "ClassSkill", "1");

        // cls_skill_rogue: Rogue gets Disable Trap (2) and Animal Empathy (0) as class skills
        _mockGameData.Set2DAValue("cls_skill_rogue", 0, "SkillIndex", "0");
        _mockGameData.Set2DAValue("cls_skill_rogue", 0, "ClassSkill", "1");
        _mockGameData.Set2DAValue("cls_skill_rogue", 1, "SkillIndex", "2");
        _mockGameData.Set2DAValue("cls_skill_rogue", 1, "ClassSkill", "1");
        _mockGameData.Set2DAValue("cls_skill_rogue", 2, "SkillIndex", "3");
        _mockGameData.Set2DAValue("cls_skill_rogue", 2, "ClassSkill", "0");

        // classes.2da: SkillsTable
        _mockGameData.Set2DAValue("classes", 4, "SkillsTable", "cls_skill_fight"); // Fighter
        _mockGameData.Set2DAValue("classes", 8, "SkillsTable", "cls_skill_rogue"); // Rogue
    }

    #region Name Lookups

    [Fact]
    public void GetSkillName_WithTlk_ReturnsResolvedName()
    {
        var result = _skillService.GetSkillName(0);
        Assert.Equal("Animal Empathy", result);
    }

    [Fact]
    public void GetSkillName_WithTlk_ReturnsConcentration()
    {
        var result = _skillService.GetSkillName(1);
        Assert.Equal("Concentration", result);
    }

    [Fact]
    public void GetSkillName_MissingRow_FallsBackToHardcoded()
    {
        // Skill 7 not in our mock 2DA, should use hardcoded "Lore"
        var result = _skillService.GetSkillName(7);
        Assert.Equal("Lore", result);
    }

    [Fact]
    public void GetSkillName_UnknownId_ReturnsFallbackFormat()
    {
        var result = _skillService.GetSkillName(999);
        Assert.Equal("Skill 999", result);
    }

    #endregion

    #region Key Ability

    [Fact]
    public void GetSkillKeyAbility_FromTwoDA_ReturnsCorrectAbility()
    {
        var result = _skillService.GetSkillKeyAbility(0);
        Assert.Equal("CHA", result);
    }

    [Fact]
    public void GetSkillKeyAbility_Concentration_ReturnsCon()
    {
        var result = _skillService.GetSkillKeyAbility(1);
        Assert.Equal("CON", result);
    }

    [Fact]
    public void GetSkillKeyAbility_Missing_FallsBackToHardcoded()
    {
        var result = _skillService.GetSkillKeyAbility(7); // Lore
        Assert.Equal("INT", result);
    }

    #endregion

    #region Class Skills

    [Fact]
    public void GetClassSkillsTable_Fighter_ReturnsFighterTable()
    {
        var result = _skillService.GetClassSkillsTable(4);
        Assert.Equal("cls_skill_fight", result);
    }

    [Fact]
    public void GetClassSkillsTable_InvalidClass_ReturnsNull()
    {
        var result = _skillService.GetClassSkillsTable(99);
        Assert.Null(result);
    }

    [Fact]
    public void IsClassSkill_FighterDiscipline_True()
    {
        Assert.True(_skillService.IsClassSkill(4, 3)); // Fighter + Discipline
    }

    [Fact]
    public void IsClassSkill_FighterDisableTrap_False()
    {
        Assert.False(_skillService.IsClassSkill(4, 2)); // Fighter + Disable Trap (appears but ClassSkill=0)
    }

    [Fact]
    public void IsClassSkill_RogueDisableTrap_True()
    {
        Assert.True(_skillService.IsClassSkill(8, 2)); // Rogue + Disable Trap
    }

    [Fact]
    public void GetClassSkillIds_Fighter_ReturnsDisciplineOnly()
    {
        var result = _skillService.GetClassSkillIds(4);
        Assert.Single(result);
        Assert.Contains(3, result); // Discipline
    }

    [Fact]
    public void GetClassSkillIds_Rogue_ReturnsAnimalEmpathyAndDisableTrap()
    {
        var result = _skillService.GetClassSkillIds(8);
        Assert.Equal(2, result.Count);
        Assert.Contains(0, result); // Animal Empathy
        Assert.Contains(2, result); // Disable Trap
    }

    #endregion

    #region Multiclass Combined Skills

    [Fact]
    public void GetCombinedClassSkillIds_FighterRogue_UnionOfBoth()
    {
        var creature = new CreatureBuilder()
            .WithClass(4, 5)  // Fighter 5
            .WithClass(8, 3)  // Rogue 3
            .Build();

        var result = _skillService.GetCombinedClassSkillIds(creature);
        Assert.Contains(0, result); // Animal Empathy (Rogue)
        Assert.Contains(2, result); // Disable Trap (Rogue)
        Assert.Contains(3, result); // Discipline (Fighter)
    }

    #endregion

    #region Skill Availability

    [Fact]
    public void IsSkillUniversal_ConcentrationIsUniversal_True()
    {
        Assert.True(_skillService.IsSkillUniversal(1));
    }

    [Fact]
    public void IsSkillUniversal_AnimalEmpathyNotUniversal_False()
    {
        Assert.False(_skillService.IsSkillUniversal(0));
    }

    [Fact]
    public void IsSkillAvailable_UniversalSkill_AlwaysAvailable()
    {
        var creature = new CreatureBuilder()
            .WithClass(4, 1) // Fighter
            .Build();

        Assert.True(_skillService.IsSkillAvailable(creature, 1)); // Concentration (universal)
    }

    [Fact]
    public void IsSkillAvailable_SkillInClassTable_Available()
    {
        var creature = new CreatureBuilder()
            .WithClass(4, 1) // Fighter
            .Build();

        // Disable Trap appears in cls_skill_fight (even though ClassSkill=0)
        Assert.True(_skillService.IsSkillAvailable(creature, 2));
    }

    [Fact]
    public void IsSkillAvailable_SkillNotInAnyTable_Unavailable()
    {
        var creature = new CreatureBuilder()
            .WithClass(4, 1) // Fighter (has skill 2 and 3 in table)
            .Build();

        // Animal Empathy (0) not in fighter table, not universal
        Assert.False(_skillService.IsSkillAvailable(creature, 0));
    }

    [Fact]
    public void GetUnavailableSkillIds_Fighter_ExcludesUnavailable()
    {
        var creature = new CreatureBuilder()
            .WithClass(4, 1) // Fighter
            .Build();

        var unavailable = _skillService.GetUnavailableSkillIds(creature, 4);
        // Skill 0 (Animal Empathy) is unavailable to Fighter (not in table, not universal)
        Assert.Contains(0, unavailable);
        // Skill 1 (Concentration) is universal - available
        Assert.DoesNotContain(1, unavailable);
        // Skill 2 (Disable Trap) is in fighter table
        Assert.DoesNotContain(2, unavailable);
        // Skill 3 (Discipline) is in fighter table
        Assert.DoesNotContain(3, unavailable);
    }

    #endregion

    #region Auto-Assign Skills

    [Fact]
    public void AutoAssignSkills_ClassSkillsFirst_AllocatesMaxRanks()
    {
        var classSkillIds = new HashSet<int> { 3 }; // Discipline
        var unavailable = new HashSet<int>();

        var result = _skillService.AutoAssignSkills(
            packageId: 255, // no package
            classSkillIds: classSkillIds,
            unavailableSkillIds: unavailable,
            totalPoints: 8,
            totalLevel: 1,
            existingRanks: null);

        // At level 1, max class skill ranks = 1+3 = 4
        Assert.True(result.ContainsKey(3));
        Assert.Equal(4, result[3]); // Discipline maxed at 4
    }

    [Fact]
    public void AutoAssignSkills_RespectsTotalPoints()
    {
        var classSkillIds = new HashSet<int> { 3 };
        var unavailable = new HashSet<int>();

        var result = _skillService.AutoAssignSkills(
            packageId: 255,
            classSkillIds: classSkillIds,
            unavailableSkillIds: unavailable,
            totalPoints: 2,
            totalLevel: 1,
            existingRanks: null);

        Assert.True(result.ContainsKey(3));
        Assert.Equal(2, result[3]); // Only 2 points available
    }

    [Fact]
    public void AutoAssignSkills_SkipsUnavailable()
    {
        var classSkillIds = new HashSet<int> { 0, 3 }; // Animal Empathy + Discipline
        var unavailable = new HashSet<int> { 0 }; // Animal Empathy unavailable

        var result = _skillService.AutoAssignSkills(
            packageId: 255,
            classSkillIds: classSkillIds,
            unavailableSkillIds: unavailable,
            totalPoints: 8,
            totalLevel: 1,
            existingRanks: null);

        Assert.False(result.ContainsKey(0)); // Skipped
        Assert.True(result.ContainsKey(3));   // Discipline allocated
    }

    #endregion
}
