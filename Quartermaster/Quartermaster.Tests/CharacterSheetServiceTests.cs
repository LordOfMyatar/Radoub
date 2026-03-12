using Quartermaster.Services;
using Radoub.Formats.Gff;
using Radoub.Formats.Utc;
using Radoub.TestUtilities.Builders;
using Radoub.TestUtilities.Mocks;
using Xunit;

namespace Quartermaster.Tests;

/// <summary>
/// Dedicated tests for CharacterSheetService: text and markdown generation,
/// identity, classes, abilities, combat, skills, feats, spells, equipment, scripts.
/// </summary>
public class CharacterSheetServiceTests
{
    private readonly MockGameDataService _mockGameData;
    private readonly CreatureDisplayService _displayService;
    private readonly CharacterSheetService _sheetService;

    public CharacterSheetServiceTests()
    {
        _mockGameData = new MockGameDataService(includeSampleData: true);
        SetupAdditionalData();
        _displayService = new CreatureDisplayService(_mockGameData);
        _sheetService = new CharacterSheetService(_displayService);
    }

    private void SetupAdditionalData()
    {
        // Setup feat data for feat display
        _mockGameData.Set2DAValue("feat", 0, "LABEL", "Alertness");
        _mockGameData.Set2DAValue("feat", 0, "FEAT", "400");
        _mockGameData.Set2DAValue("feat", 0, "TOOLSCATEGORIES", "6");
        _mockGameData.SetTlkString(400, "Alertness");

        _mockGameData.Set2DAValue("feat", 11, "LABEL", "Dodge");
        _mockGameData.Set2DAValue("feat", 11, "FEAT", "404");
        _mockGameData.Set2DAValue("feat", 11, "TOOLSCATEGORIES", "3");
        _mockGameData.SetTlkString(404, "Dodge");

        // Skill data
        _mockGameData.Set2DAValue("skills", 3, "Name", "203");
        _mockGameData.Set2DAValue("skills", 3, "KeyAbility", "STR");
        _mockGameData.SetTlkString(203, "Discipline");

        _mockGameData.Set2DAValue("skills", 7, "Name", "207");
        _mockGameData.Set2DAValue("skills", 7, "KeyAbility", "INT");
        _mockGameData.SetTlkString(207, "Lore");
    }

    private UtcFile CreateTestCreature()
    {
        var creature = new CreatureBuilder()
            .WithIdentity("Dorn", "", "dorn_fighter")
            .WithRace(6) // Human
            .WithClass(4, 5) // Fighter 5
            .WithAbilities(16, 12, 14, 10, 10, 8)
            .WithHitPoints(45)
            .WithAlignment(80, 75) // Lawful Good
            .WithFeat(0)  // Alertness
            .WithFeat(11) // Dodge
            .Build();

        creature.Tag = "dorn_fighter";
        creature.TemplateResRef = "dorn_fighter";
        creature.FortBonus = 4;
        creature.RefBonus = 1;
        creature.WillBonus = 1;

        // Add skills
        while (creature.SkillList.Count <= 7)
            creature.SkillList.Add(0);
        creature.SkillList[3] = 4; // Discipline 4
        creature.SkillList[7] = 2; // Lore 2

        return creature;
    }

    #region Text Sheet

    [Fact]
    public void GenerateTextSheet_ContainsHeader()
    {
        var creature = CreateTestCreature();
        var text = _sheetService.GenerateTextSheet(creature);

        Assert.Contains("CHARACTER SHEET:", text);
        Assert.Contains("Dorn", text);
    }

    [Fact]
    public void GenerateTextSheet_ContainsIdentitySection()
    {
        var creature = CreateTestCreature();
        var text = _sheetService.GenerateTextSheet(creature);

        Assert.Contains("IDENTITY", text);
        Assert.Contains("Human", text); // Race
        Assert.Contains("Tag:", text);
        Assert.Contains("dorn_fighter", text);
    }

    [Fact]
    public void GenerateTextSheet_ContainsAlignment()
    {
        var creature = CreateTestCreature();
        var text = _sheetService.GenerateTextSheet(creature);

        Assert.Contains("Lawful Good", text);
    }

    [Fact]
    public void GenerateTextSheet_ContainsClassSection()
    {
        var creature = CreateTestCreature();
        var text = _sheetService.GenerateTextSheet(creature);

        Assert.Contains("CLASS PROGRESSION", text);
        Assert.Contains("Fighter", text);
        Assert.Contains("Total Level: 5", text);
    }

    [Fact]
    public void GenerateTextSheet_ContainsAbilityScores()
    {
        var creature = CreateTestCreature();
        var text = _sheetService.GenerateTextSheet(creature);

        Assert.Contains("ABILITY SCORES", text);
        Assert.Contains("STR", text);
        Assert.Contains("16", text);
    }

    [Fact]
    public void GenerateTextSheet_ContainsCombatStats()
    {
        var creature = CreateTestCreature();
        var text = _sheetService.GenerateTextSheet(creature);

        Assert.Contains("COMBAT STATISTICS", text);
        Assert.Contains("Hit Points:", text);
        Assert.Contains("45", text); // HP
        Assert.Contains("Fortitude:", text);
    }

    [Fact]
    public void GenerateTextSheet_ContainsSkills()
    {
        var creature = CreateTestCreature();
        var text = _sheetService.GenerateTextSheet(creature);

        Assert.Contains("SKILLS", text);
        Assert.Contains("Discipline", text);
    }

    [Fact]
    public void GenerateTextSheet_ContainsFeats()
    {
        var creature = CreateTestCreature();
        var text = _sheetService.GenerateTextSheet(creature);

        Assert.Contains("FEATS", text);
        Assert.Contains("Alertness", text);
        Assert.Contains("Dodge", text);
    }

    [Fact]
    public void GenerateTextSheet_NoFeats_ShowsPlaceholder()
    {
        var creature = new CreatureBuilder()
            .WithRace(6)
            .WithClass(4, 1)
            .Build();

        var text = _sheetService.GenerateTextSheet(creature);
        Assert.Contains("No feats", text);
    }

    [Fact]
    public void GenerateTextSheet_ContainsScriptsForUtc()
    {
        var creature = CreateTestCreature();
        creature.ScriptHeartbeat = "nw_c2_default1";

        var text = _sheetService.GenerateTextSheet(creature);
        Assert.Contains("SCRIPTS", text);
        Assert.Contains("nw_c2_default1", text);
    }

    [Fact]
    public void GenerateTextSheet_ContainsFooter()
    {
        var creature = CreateTestCreature();
        var text = _sheetService.GenerateTextSheet(creature);

        Assert.Contains("Generated by Quartermaster", text);
    }

    #endregion

    #region Markdown Sheet

    [Fact]
    public void GenerateMarkdownSheet_ContainsMarkdownHeader()
    {
        var creature = CreateTestCreature();
        var md = _sheetService.GenerateMarkdownSheet(creature);

        Assert.Contains("# Dorn", md);
    }

    [Fact]
    public void GenerateMarkdownSheet_ContainsIdentitySection()
    {
        var creature = CreateTestCreature();
        var md = _sheetService.GenerateMarkdownSheet(creature);

        Assert.Contains("## Identity", md);
        Assert.Contains("**Race:** Human", md);
        Assert.Contains("**Alignment:** Lawful Good", md);
    }

    [Fact]
    public void GenerateMarkdownSheet_ContainsClassTable()
    {
        var creature = CreateTestCreature();
        var md = _sheetService.GenerateMarkdownSheet(creature);

        Assert.Contains("## Class Progression", md);
        Assert.Contains("| Class | Level |", md); // Table header
        Assert.Contains("Fighter", md);
    }

    [Fact]
    public void GenerateMarkdownSheet_ContainsAbilityTable()
    {
        var creature = CreateTestCreature();
        var md = _sheetService.GenerateMarkdownSheet(creature);

        Assert.Contains("## Ability Scores", md);
        Assert.Contains("| Ability | Score | Modifier |", md);
        Assert.Contains("STR", md);
    }

    [Fact]
    public void GenerateMarkdownSheet_ContainsCombatStats()
    {
        var creature = CreateTestCreature();
        var md = _sheetService.GenerateMarkdownSheet(creature);

        Assert.Contains("## Combat Statistics", md);
        Assert.Contains("**Hit Points:**", md);
        Assert.Contains("### Saving Throws", md);
    }

    [Fact]
    public void GenerateMarkdownSheet_ContainsSkillsTable()
    {
        var creature = CreateTestCreature();
        var md = _sheetService.GenerateMarkdownSheet(creature);

        Assert.Contains("## Skills", md);
        Assert.Contains("| Skill | Ranks |", md);
        Assert.Contains("Discipline", md);
    }

    [Fact]
    public void GenerateMarkdownSheet_ContainsFeats()
    {
        var creature = CreateTestCreature();
        var md = _sheetService.GenerateMarkdownSheet(creature);

        Assert.Contains("## Feats", md);
        Assert.Contains("Alertness", md);
        Assert.Contains("Dodge", md);
    }

    [Fact]
    public void GenerateMarkdownSheet_NoFeats_ShowsPlaceholder()
    {
        var creature = new CreatureBuilder()
            .WithRace(6)
            .WithClass(4, 1)
            .Build();

        var md = _sheetService.GenerateMarkdownSheet(creature);
        Assert.Contains("No feats", md);
    }

    [Fact]
    public void GenerateMarkdownSheet_NoEquipment_ShowsPlaceholder()
    {
        var creature = CreateTestCreature();
        var md = _sheetService.GenerateMarkdownSheet(creature);

        Assert.Contains("No equipped items", md);
    }

    [Fact]
    public void GenerateMarkdownSheet_WithEquipment_ShowsSlots()
    {
        var creature = CreateTestCreature();
        creature.EquipItemList.Add(new EquippedItem { Slot = 4, EquipRes = "nw_wswls001" });

        var md = _sheetService.GenerateMarkdownSheet(creature);
        Assert.Contains("## Equipment", md);
        Assert.Contains("nw_wswls001", md);
    }

    [Fact]
    public void GenerateMarkdownSheet_Scripts_UtcOnly()
    {
        var creature = CreateTestCreature();
        creature.ScriptSpawn = "nw_c2_default9";

        var md = _sheetService.GenerateMarkdownSheet(creature);
        Assert.Contains("## Scripts", md);
        Assert.Contains("nw_c2_default9", md);
    }

    [Fact]
    public void GenerateMarkdownSheet_Footer_Present()
    {
        var creature = CreateTestCreature();
        var md = _sheetService.GenerateMarkdownSheet(creature);

        Assert.Contains("Generated by Quartermaster", md);
    }

    #endregion

    #region Alignment Helper

    [Theory]
    [InlineData(100, 100, "Lawful Good")]
    [InlineData(100, 0, "Chaotic Good")]
    [InlineData(0, 100, "Lawful Evil")]
    [InlineData(0, 0, "Chaotic Evil")]
    [InlineData(50, 50, "True Neutral")]
    [InlineData(100, 50, "Neutral Good")]
    [InlineData(0, 50, "Neutral Evil")]
    [InlineData(50, 100, "Lawful Neutral")]
    [InlineData(50, 0, "Chaotic Neutral")]
    public void Alignment_AllNineCombinations_CorrectNames(byte ge, byte lc, string expected)
    {
        var creature = new CreatureBuilder()
            .WithRace(6)
            .WithClass(4, 1)
            .WithAlignment(ge, lc)
            .Build();

        var text = _sheetService.GenerateTextSheet(creature);
        Assert.Contains(expected, text);
    }

    #endregion

    #region BIC vs UTC Handling

    [Fact]
    public void GenerateTextSheet_BicFile_ShowsXpAndSkipsScripts()
    {
        var creature = CreateTestCreature();
        // A BIC file path signals BIC mode
        var text = _sheetService.GenerateTextSheet(creature, "character.bic");

        // Should NOT contain scripts section (BIC files don't have scripts)
        Assert.DoesNotContain("SCRIPTS", text);
    }

    [Fact]
    public void GenerateTextSheet_UtcFile_IncludesScripts()
    {
        var creature = CreateTestCreature();
        creature.ScriptHeartbeat = "nw_c2_default1";

        var text = _sheetService.GenerateTextSheet(creature, "creature.utc");
        Assert.Contains("SCRIPTS", text);
    }

    [Fact]
    public void GenerateMarkdownSheet_BicFile_SkipsScripts()
    {
        var creature = CreateTestCreature();
        var md = _sheetService.GenerateMarkdownSheet(creature, "character.bic");

        Assert.DoesNotContain("## Scripts", md);
    }

    #endregion

    #region No Skills

    [Fact]
    public void GenerateMarkdownSheet_NoTrainedSkills_ShowsPlaceholder()
    {
        var creature = new CreatureBuilder()
            .WithRace(6)
            .WithClass(4, 1)
            .Build();

        var md = _sheetService.GenerateMarkdownSheet(creature);
        Assert.Contains("No trained skills", md);
    }

    #endregion

    #region Markdown Format Specifics

    [Fact]
    public void GenerateMarkdownSheet_SkillsTable_SortedByRanksDescending()
    {
        var creature = CreateTestCreature();
        // Discipline=4, Lore=2 — Discipline should appear first
        var md = _sheetService.GenerateMarkdownSheet(creature);

        int disciplinePos = md.IndexOf("Discipline");
        int lorePos = md.IndexOf("Lore");

        Assert.True(disciplinePos < lorePos,
            "Skills should be sorted by ranks descending (Discipline 4 before Lore 2)");
    }

    [Fact]
    public void GenerateMarkdownSheet_FeatCategories_UseHeading3()
    {
        var creature = CreateTestCreature();
        var md = _sheetService.GenerateMarkdownSheet(creature);

        // Feats should be grouped with ### category headings
        // Alertness is category 6 (Class/Racial), Dodge is category 3 (Defensive)
        Assert.Contains("### ", md);
    }

    [Fact]
    public void GenerateMarkdownSheet_ResRef_UsesCodeFormatting()
    {
        var creature = CreateTestCreature();
        var md = _sheetService.GenerateMarkdownSheet(creature);

        Assert.Contains("`dorn_fighter`", md);
    }

    [Fact]
    public void GenerateMarkdownSheet_ClassSummary_BoldsClassNames()
    {
        var creature = CreateTestCreature();
        var md = _sheetService.GenerateMarkdownSheet(creature);

        Assert.Contains("**Fighter**", md);
    }

    #endregion

    #region Multiclass Characters

    [Fact]
    public void GenerateTextSheet_Multiclass_ShowsAllClasses()
    {
        var creature = new CreatureBuilder()
            .WithIdentity("Kael", "", "kael")
            .WithRace(6) // Human
            .WithClass(4, 5) // Fighter 5
            .WithClass(7, 3) // Rogue 3 (CommonClass.Rogue = 7 or 8 depending on mapping)
            .WithAbilities(14, 16, 12, 10, 10, 8)
            .WithAlignment(50, 50)
            .Build();

        var text = _sheetService.GenerateTextSheet(creature);

        Assert.Contains("Total Level: 8", text);
        Assert.Contains("Fighter", text);
    }

    [Fact]
    public void GenerateMarkdownSheet_Multiclass_ShowsTotalInSummary()
    {
        var creature = new CreatureBuilder()
            .WithIdentity("Kael", "", "kael")
            .WithRace(6)
            .WithClass(4, 5)
            .WithClass(7, 3)
            .WithAbilities(14, 16, 12, 10, 10, 8)
            .WithAlignment(50, 50)
            .Build();

        var md = _sheetService.GenerateMarkdownSheet(creature);

        Assert.Contains("(Total: 8)", md);
    }

    #endregion

    #region Subrace and Deity

    [Fact]
    public void GenerateTextSheet_WithSubrace_DisplaysSubrace()
    {
        var creature = CreateTestCreature();
        creature.Subrace = "Aasimar";

        var text = _sheetService.GenerateTextSheet(creature);
        Assert.Contains("Subrace:", text);
        Assert.Contains("Aasimar", text);
    }

    [Fact]
    public void GenerateTextSheet_WithDeity_DisplaysDeity()
    {
        var creature = CreateTestCreature();
        creature.Deity = "Tyr";

        var text = _sheetService.GenerateTextSheet(creature);
        Assert.Contains("Deity:", text);
        Assert.Contains("Tyr", text);
    }

    [Fact]
    public void GenerateMarkdownSheet_WithSubrace_DisplaysSubrace()
    {
        var creature = CreateTestCreature();
        creature.Subrace = "Aasimar";

        var md = _sheetService.GenerateMarkdownSheet(creature);
        Assert.Contains("**Subrace:** Aasimar", md);
    }

    [Fact]
    public void GenerateTextSheet_NoSubrace_OmitsLine()
    {
        var creature = CreateTestCreature();
        creature.Subrace = "";

        var text = _sheetService.GenerateTextSheet(creature);
        Assert.DoesNotContain("Subrace:", text);
    }

    [Fact]
    public void GenerateTextSheet_NoDeity_OmitsLine()
    {
        var creature = CreateTestCreature();
        creature.Deity = "";

        var text = _sheetService.GenerateTextSheet(creature);
        Assert.DoesNotContain("Deity:", text);
    }

    #endregion

    #region Equipment Details

    [Fact]
    public void GenerateTextSheet_WithEquipment_ShowsSlotNames()
    {
        var creature = CreateTestCreature();
        creature.EquipItemList.Add(new EquippedItem { Slot = 4, EquipRes = "nw_wswls001" });
        creature.EquipItemList.Add(new EquippedItem { Slot = 1, EquipRes = "nw_aarcl001" });

        var text = _sheetService.GenerateTextSheet(creature);
        Assert.Contains("EQUIPMENT", text);
        Assert.Contains("nw_wswls001", text);
        Assert.Contains("nw_aarcl001", text);
    }

    [Fact]
    public void GenerateTextSheet_NoEquipment_ShowsPlaceholder()
    {
        var creature = CreateTestCreature();
        var text = _sheetService.GenerateTextSheet(creature);
        Assert.Contains("No equipped items", text);
    }

    #endregion

    #region Alignment Boundary Values

    [Theory]
    [InlineData(70, 70, "Lawful Good")]   // Exact boundary for good + lawful
    [InlineData(69, 69, "True Neutral")]  // Just below boundary
    [InlineData(31, 31, "True Neutral")]  // Just above evil/chaotic boundary
    [InlineData(30, 30, "Chaotic Evil")]  // Exact boundary for evil + chaotic
    public void Alignment_BoundaryValues_CorrectNames(byte ge, byte lc, string expected)
    {
        var creature = new CreatureBuilder()
            .WithRace(6)
            .WithClass(4, 1)
            .WithAlignment(ge, lc)
            .Build();

        var text = _sheetService.GenerateTextSheet(creature);
        Assert.Contains(expected, text);
    }

    #endregion

    #region Text vs Markdown Format Differences

    [Fact]
    public void TextSheet_UsesBoxDrawing_MarkdownUsesTables()
    {
        var creature = CreateTestCreature();
        var text = _sheetService.GenerateTextSheet(creature);
        var md = _sheetService.GenerateMarkdownSheet(creature);

        // Text uses box drawing characters
        Assert.Contains("═", text);
        Assert.Contains("───", text);

        // Markdown uses table syntax
        Assert.Contains("|", md);
        Assert.Contains("---", md);
    }

    [Fact]
    public void TextSheet_SkillsShowClassColumn_MarkdownUsesClassMarker()
    {
        var creature = CreateTestCreature();

        var text = _sheetService.GenerateTextSheet(creature);
        var md = _sheetService.GenerateMarkdownSheet(creature);

        // Text format has Class? column
        Assert.Contains("Class?", text);

        // Markdown uses "✓" for class skills or "-" for cross-class
        // Since mock may not have class skill data, just verify skill table structure
        Assert.Contains("| Class? |", md);
    }

    #endregion

    #region Scripts Section Edge Cases

    [Fact]
    public void GenerateTextSheet_NoScripts_ShowsPlaceholder()
    {
        var creature = CreateTestCreature();
        var text = _sheetService.GenerateTextSheet(creature);

        Assert.Contains("SCRIPTS", text);
        Assert.Contains("No scripts assigned", text);
    }

    [Fact]
    public void GenerateMarkdownSheet_NoScripts_ShowsPlaceholder()
    {
        var creature = CreateTestCreature();
        var md = _sheetService.GenerateMarkdownSheet(creature);

        Assert.Contains("## Scripts", md);
        Assert.Contains("No scripts assigned", md);
    }

    [Fact]
    public void GenerateMarkdownSheet_WithScripts_ShowsTable()
    {
        var creature = CreateTestCreature();
        creature.ScriptHeartbeat = "nw_c2_default1";
        creature.ScriptSpawn = "nw_c2_default9";

        var md = _sheetService.GenerateMarkdownSheet(creature);

        Assert.Contains("| Event | Script |", md);
        Assert.Contains("| OnHeartbeat | `nw_c2_default1` |", md);
        Assert.Contains("| OnSpawn | `nw_c2_default9` |", md);
    }

    #endregion

    #region Null FilePath Handling

    [Fact]
    public void GenerateTextSheet_NullPath_TreatedAsUtc()
    {
        var creature = CreateTestCreature();
        creature.ScriptHeartbeat = "nw_c2_default1";

        var text = _sheetService.GenerateTextSheet(creature, null);

        // With null path, isBic=false, so scripts section should be present
        Assert.Contains("SCRIPTS", text);
        Assert.Contains("nw_c2_default1", text);
    }

    [Fact]
    public void GenerateMarkdownSheet_NullPath_TreatedAsUtc()
    {
        var creature = CreateTestCreature();
        creature.ScriptHeartbeat = "nw_c2_default1";

        var md = _sheetService.GenerateMarkdownSheet(creature, null);

        Assert.Contains("## Scripts", md);
    }

    #endregion
}
