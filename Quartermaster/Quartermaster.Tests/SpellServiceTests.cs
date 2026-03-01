using Quartermaster.Services;
using Radoub.TestUtilities.Mocks;
using Xunit;

namespace Quartermaster.Tests;

/// <summary>
/// Dedicated tests for SpellService: name lookups, spell info, caster class detection,
/// spell slots, spells known, and spell list generation.
/// </summary>
public class SpellServiceTests
{
    private readonly MockGameDataService _mockGameData;
    private readonly SpellService _spellService;

    public SpellServiceTests()
    {
        _mockGameData = new MockGameDataService(includeSampleData: true);
        SetupSpellData();
        _spellService = new SpellService(_mockGameData);
    }

    private void SetupSpellData()
    {
        // spells.2da: Label, Name (TLK strRef), Innate, School, Bard, Cleric, Druid, Paladin, Ranger, Wiz_Sorc
        // Spell 0: Acid Fog (Wizard/Sorc level 6, school C=Conjuration)
        _mockGameData.Set2DAValue("spells", 0, "Label", "Acid_Fog");
        _mockGameData.Set2DAValue("spells", 0, "Name", "300");
        _mockGameData.Set2DAValue("spells", 0, "Innate", "6");
        _mockGameData.Set2DAValue("spells", 0, "School", "C");
        _mockGameData.Set2DAValue("spells", 0, "Wiz_Sorc", "6");

        // Spell 1: Aid (Cleric level 2, school E=Enchantment)
        _mockGameData.Set2DAValue("spells", 1, "Label", "Aid");
        _mockGameData.Set2DAValue("spells", 1, "Name", "301");
        _mockGameData.Set2DAValue("spells", 1, "Innate", "2");
        _mockGameData.Set2DAValue("spells", 1, "School", "E");
        _mockGameData.Set2DAValue("spells", 1, "Cleric", "2");

        // Spell 2: Bless (Cleric level 1, Paladin level 1, school E=Enchantment)
        _mockGameData.Set2DAValue("spells", 2, "Label", "Bless");
        _mockGameData.Set2DAValue("spells", 2, "Name", "302");
        _mockGameData.Set2DAValue("spells", 2, "Innate", "1");
        _mockGameData.Set2DAValue("spells", 2, "School", "E");
        _mockGameData.Set2DAValue("spells", 2, "Cleric", "1");
        _mockGameData.Set2DAValue("spells", 2, "Paladin", "1");

        // Spell 3: Magic Missile (Wizard/Sorc level 1, school V=Evocation)
        _mockGameData.Set2DAValue("spells", 3, "Label", "Magic_Missile");
        _mockGameData.Set2DAValue("spells", 3, "Name", "303");
        _mockGameData.Set2DAValue("spells", 3, "Innate", "1");
        _mockGameData.Set2DAValue("spells", 3, "School", "V");
        _mockGameData.Set2DAValue("spells", 3, "Wiz_Sorc", "1");

        // Spell 4: Grease (Bard level 1, Wizard/Sorc level 1, school C=Conjuration)
        _mockGameData.Set2DAValue("spells", 4, "Label", "Grease");
        _mockGameData.Set2DAValue("spells", 4, "Name", "304");
        _mockGameData.Set2DAValue("spells", 4, "Innate", "1");
        _mockGameData.Set2DAValue("spells", 4, "School", "C");
        _mockGameData.Set2DAValue("spells", 4, "Bard", "1");
        _mockGameData.Set2DAValue("spells", 4, "Wiz_Sorc", "1");

        _mockGameData.SetTlkString(300, "Acid Fog");
        _mockGameData.SetTlkString(301, "Aid");
        _mockGameData.SetTlkString(302, "Bless");
        _mockGameData.SetTlkString(303, "Magic Missile");
        _mockGameData.SetTlkString(304, "Grease");

        // classes.2da: SpellGainTable, SpellKnownTable, MemorizesSpells
        // Wizard (10): memorizes spells
        _mockGameData.Set2DAValue("classes", 10, "SpellGainTable", "cls_spgn_wiz");
        _mockGameData.Set2DAValue("classes", 10, "SpellKnownTable", "****");
        _mockGameData.Set2DAValue("classes", 10, "MemorizesSpells", "1");

        // Sorcerer (9): spontaneous caster
        _mockGameData.Set2DAValue("classes", 9, "SpellGainTable", "cls_spgn_sorc");
        _mockGameData.Set2DAValue("classes", 9, "SpellKnownTable", "cls_spkn_sorc");
        _mockGameData.Set2DAValue("classes", 9, "MemorizesSpells", "0");

        // Fighter (4): not a caster
        // (no SpellGainTable set)

        // cls_spgn_wiz: SpellLevel0..SpellLevel9 per class level (row = level-1)
        // Wizard level 1: 3 cantrips, 1 level-1 slot
        _mockGameData.Set2DAValue("cls_spgn_wiz", 0, "SpellLevel0", "3");
        _mockGameData.Set2DAValue("cls_spgn_wiz", 0, "SpellLevel1", "1");
        _mockGameData.Set2DAValue("cls_spgn_wiz", 0, "SpellLevel2", "****");
        // Wizard level 3: 3 cantrips, 2 level-1, 1 level-2
        _mockGameData.Set2DAValue("cls_spgn_wiz", 2, "SpellLevel0", "3");
        _mockGameData.Set2DAValue("cls_spgn_wiz", 2, "SpellLevel1", "2");
        _mockGameData.Set2DAValue("cls_spgn_wiz", 2, "SpellLevel2", "1");

        // cls_spgn_sorc: Sorcerer level 1
        _mockGameData.Set2DAValue("cls_spgn_sorc", 0, "SpellLevel0", "5");
        _mockGameData.Set2DAValue("cls_spgn_sorc", 0, "SpellLevel1", "3");

        // cls_spkn_sorc: Sorcerer spells known at level 1
        _mockGameData.Set2DAValue("cls_spkn_sorc", 0, "SpellLevel0", "4");
        _mockGameData.Set2DAValue("cls_spkn_sorc", 0, "SpellLevel1", "2");
    }

    #region Spell Name Lookups

    [Fact]
    public void GetSpellName_ValidId_ReturnsFromTlk()
    {
        Assert.Equal("Acid Fog", _spellService.GetSpellName(0));
    }

    [Fact]
    public void GetSpellName_AnotherSpell_ReturnsCorrectName()
    {
        Assert.Equal("Magic Missile", _spellService.GetSpellName(3));
    }

    [Fact]
    public void GetSpellName_UnknownId_ReturnsFallback()
    {
        Assert.Equal("Spell 999", _spellService.GetSpellName(999));
    }

    #endregion

    #region Spell Info

    [Fact]
    public void GetSpellInfo_ValidSpell_ReturnsCompleteInfo()
    {
        var info = _spellService.GetSpellInfo(0);
        Assert.NotNull(info);
        Assert.Equal(0, info.SpellId);
        Assert.Equal("Acid Fog", info.Name);
        Assert.Equal(6, info.InnateLevel);
        Assert.Equal(SpellSchool.Conjuration, info.School);
    }

    [Fact]
    public void GetSpellInfo_WizSorc_SetsClassLevelsForBoth()
    {
        var info = _spellService.GetSpellInfo(0);
        Assert.NotNull(info);
        Assert.Equal(6, info.GetLevelForClass(9));  // Sorcerer
        Assert.Equal(6, info.GetLevelForClass(10)); // Wizard
    }

    [Fact]
    public void GetSpellInfo_Bless_AvailableToClericAndPaladin()
    {
        var info = _spellService.GetSpellInfo(2);
        Assert.NotNull(info);
        Assert.Equal(1, info.GetLevelForClass(2)); // Cleric
        Assert.Equal(1, info.GetLevelForClass(6)); // Paladin
        Assert.Equal(-1, info.GetLevelForClass(10)); // Not a Wizard spell
    }

    [Fact]
    public void GetSpellInfo_InvalidSpell_ReturnsNull()
    {
        Assert.Null(_spellService.GetSpellInfo(999));
    }

    [Theory]
    [InlineData(SpellSchool.Abjuration, "Abjuration")]
    [InlineData(SpellSchool.Conjuration, "Conjuration")]
    [InlineData(SpellSchool.Divination, "Divination")]
    [InlineData(SpellSchool.Enchantment, "Enchantment")]
    [InlineData(SpellSchool.Evocation, "Evocation")]
    [InlineData(SpellSchool.Illusion, "Illusion")]
    [InlineData(SpellSchool.Necromancy, "Necromancy")]
    [InlineData(SpellSchool.Transmutation, "Transmutation")]
    public void GetSpellSchoolName_AllSchools_ReturnsCorrectName(SpellSchool school, string expected)
    {
        var name = SpellService.GetSpellSchoolName(school);
        Assert.Equal(expected, name);
    }

    #endregion

    #region All Spell IDs

    [Fact]
    public void GetAllSpellIds_ReturnsAllConfiguredSpells()
    {
        var ids = _spellService.GetAllSpellIds();
        // 25 spells: 5 from SpellServiceTests setup + 20 additional from domain mock data
        Assert.Equal(25, ids.Count);
        Assert.Contains(0, ids);
        Assert.Contains(4, ids);
        Assert.Contains(24, ids);
    }

    #endregion

    #region Caster Class Detection

    [Fact]
    public void IsCasterClass_Wizard_True()
    {
        Assert.True(_spellService.IsCasterClass(10));
    }

    [Fact]
    public void IsCasterClass_Sorcerer_True()
    {
        Assert.True(_spellService.IsCasterClass(9));
    }

    [Fact]
    public void IsCasterClass_Fighter_False()
    {
        Assert.False(_spellService.IsCasterClass(4));
    }

    [Fact]
    public void IsSpontaneousCaster_Sorcerer_True()
    {
        Assert.True(_spellService.IsSpontaneousCaster(9));
    }

    [Fact]
    public void IsSpontaneousCaster_Wizard_False()
    {
        Assert.False(_spellService.IsSpontaneousCaster(10));
    }

    #endregion

    #region Spell Slots and Known Limits

    [Fact]
    public void GetSpellSlots_WizardLevel1_ReturnsCorrectSlots()
    {
        var slots = _spellService.GetSpellSlots(10, 1);
        Assert.NotNull(slots);
        Assert.Equal(3, slots[0]); // cantrips
        Assert.Equal(1, slots[1]); // level 1
        Assert.Equal(0, slots[2]); // no level 2 at level 1
    }

    [Fact]
    public void GetSpellSlots_WizardLevel3_IncludesLevel2()
    {
        var slots = _spellService.GetSpellSlots(10, 3);
        Assert.NotNull(slots);
        Assert.Equal(3, slots[0]); // cantrips
        Assert.Equal(2, slots[1]); // level 1
        Assert.Equal(1, slots[2]); // level 2
    }

    [Fact]
    public void GetSpellSlots_Fighter_ReturnsNull()
    {
        Assert.Null(_spellService.GetSpellSlots(4, 1));
    }

    [Fact]
    public void GetSpellsKnownLimit_Sorcerer_ReturnsLimits()
    {
        var limits = _spellService.GetSpellsKnownLimit(9, 1);
        Assert.NotNull(limits);
        Assert.Equal(4, limits[0]); // cantrips known
        Assert.Equal(2, limits[1]); // level 1 known
    }

    [Fact]
    public void GetSpellsKnownLimit_Wizard_ReturnsNull()
    {
        // Wizard uses SpellKnownTable = "****" (memorizes instead)
        Assert.Null(_spellService.GetSpellsKnownLimit(10, 1));
    }

    #endregion

    #region Max Spell Level

    [Fact]
    public void GetMaxSpellLevel_WizardLevel1_Returns1()
    {
        Assert.Equal(1, _spellService.GetMaxSpellLevel(10, 1));
    }

    [Fact]
    public void GetMaxSpellLevel_WizardLevel3_Returns2()
    {
        Assert.Equal(2, _spellService.GetMaxSpellLevel(10, 3));
    }

    [Fact]
    public void GetMaxSpellLevel_Fighter_ReturnsMinus1()
    {
        Assert.Equal(-1, _spellService.GetMaxSpellLevel(4, 1));
    }

    #endregion

    #region Spells for Class at Level

    [Fact]
    public void GetSpellsForClassAtLevel_WizardLevel1_ReturnsLevel1Spells()
    {
        var spells = _spellService.GetSpellsForClassAtLevel(10, 1);
        // Magic Missile (3) and Grease (4) are Wiz_Sorc level 1
        Assert.Contains(3, spells);
        Assert.Contains(4, spells);
        Assert.DoesNotContain(0, spells); // Acid Fog is level 6
    }

    [Fact]
    public void GetSpellsForClassAtLevel_ClericLevel1_ReturnsBless()
    {
        var spells = _spellService.GetSpellsForClassAtLevel(2, 1);
        Assert.Contains(2, spells); // Bless
        Assert.DoesNotContain(1, spells); // Aid is Cleric level 2
    }

    #endregion

    #region Auto-Assign Spells

    [Fact]
    public void AutoAssignSpells_NoPackage_FillsAlphabetically()
    {
        var result = _spellService.AutoAssignSpells(
            classId: 10, // Wizard
            packageId: 255,
            maxSpellLevel: 1,
            maxPerLevel: level => level == 1 ? 2 : 0,
            existingSpells: null);

        Assert.True(result.ContainsKey(1));
        Assert.Equal(2, result[1].Count); // Should pick 2 level-1 spells
    }

    [Fact]
    public void AutoAssignSpells_ExcludesExistingSpells()
    {
        var existing = new HashSet<int> { 3 }; // Already knows Magic Missile

        var result = _spellService.AutoAssignSpells(
            classId: 10,
            packageId: 255,
            maxSpellLevel: 1,
            maxPerLevel: level => level == 1 ? 2 : 0,
            existingSpells: existing);

        if (result.ContainsKey(1))
        {
            Assert.DoesNotContain(3, result[1]); // Should not include Magic Missile
        }
    }

    #endregion
}
