using Quartermaster.Services;
using Radoub.TestUtilities.Mocks;
using Xunit;

namespace Quartermaster.Tests;

/// <summary>
/// Tests for AppearanceService lookups using mock 2DA/TLK data.
/// Covers appearance, phenotype, portrait, wing, tail, sound set, faction, and package methods.
/// </summary>
public class AppearanceServiceTests
{
    private readonly AppearanceService _service;
    private readonly MockGameDataService _mockGameData;

    public AppearanceServiceTests()
    {
        _mockGameData = new MockGameDataService(includeSampleData: true);
        _service = new AppearanceService(_mockGameData);
    }

    #region GetAppearanceName

    [Fact]
    public void GetAppearanceName_WithTlkString_ReturnsTlkName()
    {
        // Row 0: STRING_REF=6798 → TLK "Badger"
        var name = _service.GetAppearanceName(0);
        Assert.Equal("Badger", name);
    }

    [Fact]
    public void GetAppearanceName_WithoutTlkString_FallsBackToLabel()
    {
        // Set up an appearance with no TLK string
        _mockGameData.Set2DAValue("appearance", 10, "LABEL", "Custom_Creature");
        _mockGameData.Set2DAValue("appearance", 10, "STRING_REF", "****");
        _mockGameData.Set2DAValue("appearance", 10, "MODELTYPE", "F");

        var name = _service.GetAppearanceName(10);
        Assert.Equal("Custom_Creature", name);
    }

    [Fact]
    public void GetAppearanceName_NoData_ReturnsFallback()
    {
        var name = _service.GetAppearanceName(999);
        Assert.Equal("Appearance 999", name);
    }

    #endregion

    #region IsPartBasedAppearance

    [Fact]
    public void IsPartBasedAppearance_WithModelTypeP_ReturnsTrue()
    {
        // Row index 2: MODELTYPE=P (Halfling female, label "6")
        Assert.True(_service.IsPartBasedAppearance(2));
    }

    [Fact]
    public void IsPartBasedAppearance_WithModelTypeF_ReturnsFalse()
    {
        // Row index 0: MODELTYPE=F (Badger)
        Assert.False(_service.IsPartBasedAppearance(0));
    }

    [Fact]
    public void IsPartBasedAppearance_MissingRow_ReturnsFalse()
    {
        Assert.False(_service.IsPartBasedAppearance(999));
    }

    #endregion

    #region GetSizeAcModifier

    [Theory]
    [InlineData(0, 2)]   // Row 0: SIZECATEGORY=1 (Tiny) → +2
    [InlineData(2, 1)]   // Row index 2: SIZECATEGORY=2 (Small) → +1
    [InlineData(3, 0)]   // Row index 3: SIZECATEGORY=3 (Medium) → 0
    public void GetSizeAcModifier_ReturnsCorrectModifier(ushort row, int expected)
    {
        Assert.Equal(expected, _service.GetSizeAcModifier(row));
    }

    [Fact]
    public void GetSizeAcModifier_MissingRow_ReturnsZero()
    {
        Assert.Equal(0, _service.GetSizeAcModifier(999));
    }

    [Fact]
    public void GetSizeAcModifier_LargeSize_ReturnsNegative()
    {
        _mockGameData.Set2DAValue("appearance", 20, "SIZECATEGORY", "4");
        _mockGameData.Set2DAValue("appearance", 20, "LABEL", "Giant");
        Assert.Equal(-1, _service.GetSizeAcModifier(20));
    }

    [Fact]
    public void GetSizeAcModifier_HugeSize_ReturnsMinusTwo()
    {
        _mockGameData.Set2DAValue("appearance", 21, "SIZECATEGORY", "5");
        _mockGameData.Set2DAValue("appearance", 21, "LABEL", "HugeCreature");
        Assert.Equal(-2, _service.GetSizeAcModifier(21));
    }

    #endregion

    #region GetAllAppearances

    [Fact]
    public void GetAllAppearances_ReturnsNonEmptyRows()
    {
        var appearances = _service.GetAllAppearances();
        Assert.True(appearances.Count >= 4); // 4 rows with labels in mock data
    }

    [Fact]
    public void GetAllAppearances_IncludesPartBasedFlag()
    {
        var appearances = _service.GetAllAppearances();
        // Row index 2 = Halfling female (MODELTYPE=P)
        var halfling = appearances.FirstOrDefault(a => a.AppearanceId == 2);
        Assert.NotNull(halfling);
        Assert.True(halfling!.IsPartBased);

        // Row index 0 = Badger (MODELTYPE=F)
        var badger = appearances.FirstOrDefault(a => a.AppearanceId == 0);
        Assert.NotNull(badger);
        Assert.False(badger!.IsPartBased);
    }

    #endregion

    #region Phenotype

    [Fact]
    public void GetPhenotypeName_WithTlkString_ReturnsTlkName()
    {
        Assert.Equal("Normal", _service.GetPhenotypeName(0));
    }

    [Fact]
    public void GetPhenotypeName_NoTlk_ReturnsFallbackName()
    {
        // Phenotype 0 and 2 have hardcoded fallbacks
        var service = new AppearanceService(new MockGameDataService(includeSampleData: false));
        Assert.Equal("Normal", service.GetPhenotypeName(0));
        Assert.Equal("Large", service.GetPhenotypeName(2));
    }

    [Fact]
    public void GetPhenotypeName_UnknownId_ReturnsFallback()
    {
        Assert.Equal("Phenotype 99", _service.GetPhenotypeName(99));
    }

    [Fact]
    public void GetAllPhenotypes_ReturnsValidEntries()
    {
        var phenotypes = _service.GetAllPhenotypes();
        Assert.Equal(2, phenotypes.Count);
        Assert.Equal(0, phenotypes[0].PhenotypeId);
        Assert.Equal(1, phenotypes[1].PhenotypeId); // Index 1 in packed mock data
    }

    [Fact]
    public void GetAllPhenotypes_NoData_ReturnsFallbackDefaults()
    {
        var service = new AppearanceService(new MockGameDataService(includeSampleData: false));
        var phenotypes = service.GetAllPhenotypes();
        Assert.Equal(2, phenotypes.Count);
        Assert.Equal("Normal", phenotypes[0].Name);
        Assert.Equal("Large", phenotypes[1].Name);
    }

    #endregion

    #region Portrait

    [Fact]
    public void GetPortraitName_ValidId_ReturnsBaseResRef()
    {
        Assert.Equal("hu_m_01_", _service.GetPortraitName(0));
    }

    [Fact]
    public void GetPortraitName_InvalidId_ReturnsFallback()
    {
        Assert.Equal("Portrait 999", _service.GetPortraitName(999));
    }

    [Fact]
    public void GetPortraitResRef_ValidId_ReturnsResRef()
    {
        Assert.Equal("hu_m_01_", _service.GetPortraitResRef(0));
    }

    [Fact]
    public void GetPortraitResRef_EmptyRow_ReturnsNull()
    {
        Assert.Null(_service.GetPortraitResRef(2)); // Row 2 is ****
    }

    [Fact]
    public void GetAllPortraits_ReturnsNonEmptyRows()
    {
        var portraits = _service.GetAllPortraits();
        Assert.Equal(3, portraits.Count); // Rows 0, 1, 3 have data
        Assert.Contains(portraits, p => p.Name == "hu_m_01_");
        Assert.Contains(portraits, p => p.Name == "hu_f_01_");
        Assert.Contains(portraits, p => p.Name == "el_m_01_");
    }

    [Fact]
    public void FindPortraitIdByResRef_ExactMatch_ReturnsId()
    {
        var id = _service.FindPortraitIdByResRef("hu_m_01_");
        Assert.Equal((ushort)0, id);
    }

    [Fact]
    public void FindPortraitIdByResRef_CaseInsensitive_ReturnsId()
    {
        var id = _service.FindPortraitIdByResRef("HU_M_01_");
        Assert.Equal((ushort)0, id);
    }

    [Fact]
    public void FindPortraitIdByResRef_WithPoPrefix_StripsAndMatches()
    {
        // BIC files store "po_hu_m_01_", 2DA stores "hu_m_01_"
        var id = _service.FindPortraitIdByResRef("po_hu_m_01_");
        Assert.Equal((ushort)0, id);
    }

    [Fact]
    public void FindPortraitIdByResRef_NotFound_ReturnsNull()
    {
        Assert.Null(_service.FindPortraitIdByResRef("nonexistent_"));
    }

    [Fact]
    public void FindPortraitIdByResRef_Null_ReturnsNull()
    {
        Assert.Null(_service.FindPortraitIdByResRef(null));
    }

    [Fact]
    public void FindPortraitIdByResRef_Empty_ReturnsNull()
    {
        Assert.Null(_service.FindPortraitIdByResRef(""));
    }

    #endregion

    #region Wings and Tails

    [Fact]
    public void GetWingName_Zero_ReturnsNone()
    {
        Assert.Equal("None", _service.GetWingName(0));
    }

    [Fact]
    public void GetWingName_ValidId_ReturnsLabel()
    {
        Assert.Equal("Angel", _service.GetWingName(1));
        Assert.Equal("Demon", _service.GetWingName(2));
    }

    [Fact]
    public void GetWingName_InvalidId_ReturnsFallback()
    {
        Assert.Equal("Wings 250", _service.GetWingName(250));
    }

    [Fact]
    public void GetTailName_Zero_ReturnsNone()
    {
        Assert.Equal("None", _service.GetTailName(0));
    }

    [Fact]
    public void GetTailName_ValidId_ReturnsLabel()
    {
        Assert.Equal("Lizard", _service.GetTailName(1));
        Assert.Equal("Bone", _service.GetTailName(2));
    }

    [Fact]
    public void GetAllWings_IncludesNoneAndValidEntries()
    {
        var wings = _service.GetAllWings();
        Assert.True(wings.Count >= 4); // None + Angel + Demon + Butterfly
        Assert.Equal((byte)0, wings[0].Id);
        Assert.Equal("None", wings[0].Name);
    }

    [Fact]
    public void GetAllTails_IncludesNoneAndValidEntries()
    {
        var tails = _service.GetAllTails();
        Assert.True(tails.Count >= 3); // None + Lizard + Bone
        Assert.Equal((byte)0, tails[0].Id);
        Assert.Equal("None", tails[0].Name);
    }

    [Fact]
    public void GetAllWings_StopsAfterConsecutiveEmptyRows()
    {
        // The mock has 3 entries (rows 1-3), then all empty.
        // Should stop after 10 consecutive empty rows and not scan to 255.
        var wings = _service.GetAllWings();
        Assert.True(wings.Count < 255);
    }

    #endregion

    #region Sound Sets

    [Fact]
    public void GetSoundSetName_WithTlk_ReturnsTlkName()
    {
        Assert.Equal("Male Voice 1", _service.GetSoundSetName(0));
    }

    [Fact]
    public void GetSoundSetName_NoTlk_FallsBackToLabel()
    {
        // Set up a soundset with no TLK
        _mockGameData.Set2DAValue("soundset", 10, "LABEL", "Custom_Voice");
        _mockGameData.Set2DAValue("soundset", 10, "STRREF", "****");

        Assert.Equal("Custom_Voice", _service.GetSoundSetName(10));
    }

    [Fact]
    public void GetSoundSetName_NoData_ReturnsFallback()
    {
        Assert.Equal("Sound Set 999", _service.GetSoundSetName(999));
    }

    [Fact]
    public void GetAllSoundSets_ReturnsNonEmptyRows()
    {
        var soundSets = _service.GetAllSoundSets();
        Assert.Equal(3, soundSets.Count); // Rows 0, 1, 3 (row 2 is ****)
    }

    #endregion

    #region Factions

    [Fact]
    public void GetAllFactions_NoModuleDir_ReturnsDefaults()
    {
        var factions = _service.GetAllFactions();
        Assert.Equal(5, factions.Count);
        Assert.Equal("PC", factions[0].Name);
        Assert.Equal("Hostile", factions[1].Name);
        Assert.Equal("Commoner", factions[2].Name);
        Assert.Equal("Merchant", factions[3].Name);
        Assert.Equal("Defender", factions[4].Name);
    }

    [Fact]
    public void GetAllFactions_InvalidPath_ReturnsDefaults()
    {
        var factions = _service.GetAllFactions("/nonexistent/path");
        Assert.Equal(5, factions.Count);
        Assert.Equal("PC", factions[0].Name);
    }

    #endregion

    #region Packages

    [Fact]
    public void GetPackageName_WithTlk_ReturnsTlkName()
    {
        // Row 0: Name=100 → TLK "Cleric Default"
        Assert.Equal("Cleric Default", _service.GetPackageName(0));
    }

    [Fact]
    public void GetPackageName_NoTlk_FallsBackToLabel()
    {
        _mockGameData.Set2DAValue("packages", 50, "Label", "Custom_Package");
        _mockGameData.Set2DAValue("packages", 50, "Name", "****");

        Assert.Equal("Custom_Package", _service.GetPackageName(50));
    }

    [Fact]
    public void GetAllPackages_ReturnsSortedByName()
    {
        var packages = _service.GetAllPackages();
        Assert.True(packages.Count >= 5);

        // Verify sorted order
        for (int i = 1; i < packages.Count; i++)
        {
            Assert.True(
                string.Compare(packages[i - 1].Name, packages[i].Name, StringComparison.OrdinalIgnoreCase) <= 0,
                $"Packages not sorted: '{packages[i - 1].Name}' should be before '{packages[i].Name}'");
        }
    }

    [Fact]
    public void GetPackagesForClass_FiltersCorrectly()
    {
        // ClassID=2 is Cleric → should return row 0 (Cleric_Default)
        var clericPackages = _service.GetPackagesForClass(2);
        Assert.Single(clericPackages);
        Assert.Equal("Cleric Default", clericPackages[0].Name);
    }

    [Fact]
    public void GetPackagesForClass_NoMatch_ReturnsEmpty()
    {
        var packages = _service.GetPackagesForClass(999);
        Assert.Empty(packages);
    }

    [Fact]
    public void GetPackagesForClass_MultipleMatches_ReturnsSorted()
    {
        // Add a second wizard package
        _mockGameData.Set2DAValue("packages", 10, "Label", "Wizard_Alternate");
        _mockGameData.Set2DAValue("packages", 10, "Name", "105");
        _mockGameData.Set2DAValue("packages", 10, "ClassID", "10");
        _mockGameData.SetTlkString(105, "Abjurer");

        var wizardPackages = _service.GetPackagesForClass(10);
        Assert.Equal(2, wizardPackages.Count);
        // "Abjurer" < "Wizard Default" alphabetically
        Assert.Equal("Abjurer", wizardPackages[0].Name);
    }

    #endregion
}
