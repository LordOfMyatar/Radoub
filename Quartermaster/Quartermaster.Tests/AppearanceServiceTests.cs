using System;
using System.IO;
using System.Linq;
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

    [Fact]
    public void GetAppearanceName_StarStringRef_FallsBackToLabel()
    {
        _mockGameData.Set2DAValue("appearance", 15, "STRING_REF", "****");
        _mockGameData.Set2DAValue("appearance", 15, "LABEL", "MyCreature");
        _mockGameData.Set2DAValue("appearance", 15, "MODELTYPE", "F");

        Assert.Equal("MyCreature", _service.GetAppearanceName(15));
    }

    [Fact]
    public void GetAppearanceName_EmptyStringRef_FallsBackToLabel()
    {
        _mockGameData.Set2DAValue("appearance", 16, "STRING_REF", "");
        _mockGameData.Set2DAValue("appearance", 16, "LABEL", "EmptyRefCreature");
        _mockGameData.Set2DAValue("appearance", 16, "MODELTYPE", "S");

        Assert.Equal("EmptyRefCreature", _service.GetAppearanceName(16));
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

    [Fact]
    public void IsPartBasedAppearance_LowercaseP_ReturnsTrue()
    {
        _mockGameData.Set2DAValue("appearance", 17, "MODELTYPE", "p");
        _mockGameData.Set2DAValue("appearance", 17, "LABEL", "LowercaseTest");

        Assert.True(_service.IsPartBasedAppearance(17));
    }

    [Fact]
    public void IsPartBasedAppearance_NullModelType_ReturnsFalse()
    {
        // Row with no MODELTYPE set
        _mockGameData.Set2DAValue("appearance", 18, "LABEL", "NoModelType");

        Assert.False(_service.IsPartBasedAppearance(18));
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

    [Fact]
    public void GetSizeAcModifier_Gargantuan_ReturnsMinusFour()
    {
        _mockGameData.Set2DAValue("appearance", 22, "SIZECATEGORY", "6");
        _mockGameData.Set2DAValue("appearance", 22, "LABEL", "GargantuanCreature");
        Assert.Equal(-4, _service.GetSizeAcModifier(22));
    }

    [Fact]
    public void GetSizeAcModifier_Colossal_ReturnsMinusEight()
    {
        _mockGameData.Set2DAValue("appearance", 23, "SIZECATEGORY", "7");
        _mockGameData.Set2DAValue("appearance", 23, "LABEL", "ColossalCreature");
        Assert.Equal(-8, _service.GetSizeAcModifier(23));
    }

    [Fact]
    public void GetSizeAcModifier_InvalidString_ReturnsZero()
    {
        _mockGameData.Set2DAValue("appearance", 24, "SIZECATEGORY", "notanumber");
        _mockGameData.Set2DAValue("appearance", 24, "LABEL", "BadSize");
        Assert.Equal(0, _service.GetSizeAcModifier(24));
    }

    [Fact]
    public void GetSizeAcModifier_StarValue_ReturnsZero()
    {
        _mockGameData.Set2DAValue("appearance", 25, "SIZECATEGORY", "****");
        _mockGameData.Set2DAValue("appearance", 25, "LABEL", "StarSize");
        Assert.Equal(0, _service.GetSizeAcModifier(25));
    }

    [Fact]
    public void GetSizeAcModifier_UnknownSizeCategory_ReturnsZero()
    {
        // Size category 8 doesn't exist in D&D → should return 0
        _mockGameData.Set2DAValue("appearance", 26, "SIZECATEGORY", "8");
        _mockGameData.Set2DAValue("appearance", 26, "LABEL", "UnknownSize");
        Assert.Equal(0, _service.GetSizeAcModifier(26));
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

    [Fact]
    public void GetAllAppearances_SkipsStarLabelRows()
    {
        _mockGameData.Set2DAValue("appearance", 30, "LABEL", "****");
        _mockGameData.Set2DAValue("appearance", 30, "STRING_REF", "6798");
        _mockGameData.Set2DAValue("appearance", 30, "MODELTYPE", "F");

        var appearances = _service.GetAllAppearances();
        Assert.DoesNotContain(appearances, a => a.AppearanceId == 30);
    }

    [Fact]
    public void GetAllAppearances_SkipsEmptyLabelRows()
    {
        _mockGameData.Set2DAValue("appearance", 31, "LABEL", "");
        _mockGameData.Set2DAValue("appearance", 31, "STRING_REF", "6798");

        var appearances = _service.GetAllAppearances();
        Assert.DoesNotContain(appearances, a => a.AppearanceId == 31);
    }

    [Fact]
    public void GetAllAppearances_NoData_ReturnsEmptyList()
    {
        var emptyMock = new MockGameDataService(includeSampleData: false);
        var service = new AppearanceService(emptyMock);
        var appearances = service.GetAllAppearances();
        Assert.Empty(appearances);
    }

    [Fact]
    public void GetAllAppearances_PopulatesAllFields()
    {
        var appearances = _service.GetAllAppearances();
        var badger = appearances.First(a => a.AppearanceId == 0);
        Assert.Equal("Badger", badger.Name); // TLK name
        Assert.Equal("A_Badger", badger.Label); // 2DA LABEL
        Assert.False(badger.IsPartBased);
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

    [Fact]
    public void GetAllPhenotypes_FallbackDefaults_HaveCorrectIds()
    {
        var service = new AppearanceService(new MockGameDataService(includeSampleData: false));
        var phenotypes = service.GetAllPhenotypes();
        Assert.Equal(0, phenotypes[0].PhenotypeId);
        Assert.Equal(2, phenotypes[1].PhenotypeId);
    }

    [Fact]
    public void GetAllPhenotypes_BreaksOnEmptyAfterData()
    {
        // After data rows, an empty row should trigger break
        // Mock has rows 0 and 1 with data, row 2+ empty → should stop
        var phenotypes = _service.GetAllPhenotypes();
        // Should have exactly 2 entries (rows 0 and 1 from mock)
        Assert.Equal(2, phenotypes.Count);
    }

    [Fact]
    public void GetAllPhenotypes_SkipsLeadingEmptyRows()
    {
        // When row 0 is empty but row 1 has data, it should skip and continue
        var mock = new MockGameDataService(includeSampleData: false);
        mock.Set2DAValue("phenotype", 1, "Label", "TestPheno");
        mock.Set2DAValue("phenotype", 1, "Name", "****");
        var service = new AppearanceService(mock);

        var phenotypes = service.GetAllPhenotypes();
        Assert.Single(phenotypes);
        Assert.Equal(1, phenotypes[0].PhenotypeId);
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

    [Fact]
    public void GetPortraitResRef_StarValue_ReturnsNull()
    {
        // Row 2 in mock has "****" for BaseResRef
        Assert.Null(_service.GetPortraitResRef(2));
    }

    [Fact]
    public void GetAllPortraits_SkipsStarAndEmptyRows()
    {
        var portraits = _service.GetAllPortraits();
        // No portrait should have "****" as name
        Assert.DoesNotContain(portraits, p => p.Name == "****");
        Assert.DoesNotContain(portraits, p => string.IsNullOrEmpty(p.Name));
    }

    [Fact]
    public void GetAllPortraits_SecondCall_ReturnsCachedResult()
    {
        // First call populates cache from mock 2DA
        var first = _service.GetAllPortraits();
        var firstCount = first.Count;

        // Mutate underlying 2DA after first call
        _mockGameData.Set2DAValue("portraits", 99, "BaseResRef", "added_after_cache_");

        // Second call should return cached snapshot, not see the new row
        var second = _service.GetAllPortraits();
        Assert.Equal(firstCount, second.Count);
        Assert.DoesNotContain(second, p => p.Name == "added_after_cache_");
    }

    [Fact]
    public void GetAllPortraits_AfterInvalidate_ReturnsFreshResult()
    {
        // Prime the cache
        _ = _service.GetAllPortraits();

        // Mutate the underlying 2DA, then invalidate
        _mockGameData.Set2DAValue("portraits", 99, "BaseResRef", "added_after_invalidate_");
        _service.InvalidateCaches();

        // After invalidation, the new row should appear
        var fresh = _service.GetAllPortraits();
        Assert.Contains(fresh, p => p.Name == "added_after_invalidate_");
    }

    [Fact]
    public void FindPortraitIdByResRef_PoPrefixCaseInsensitive_Matches()
    {
        // "PO_HU_M_01_" should match "hu_m_01_"
        var id = _service.FindPortraitIdByResRef("PO_HU_M_01_");
        Assert.Equal((ushort)0, id);
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

    [Fact]
    public void GetAllWings_IncludesEntriesAfterSmallGap()
    {
        // Add an entry after a gap of 5 empty rows (< 10 threshold)
        _mockGameData.Set2DAValue("wingmodel", 9, "LABEL", "Dragon");

        var wings = _service.GetAllWings();
        Assert.Contains(wings, w => w.Name == "Dragon");
    }

    [Fact]
    public void GetAllTails_StopsAfterConsecutiveEmptyRows()
    {
        var tails = _service.GetAllTails();
        Assert.True(tails.Count < 255);
    }

    [Fact]
    public void GetAllTails_IncludesEntriesAfterSmallGap()
    {
        _mockGameData.Set2DAValue("tailmodel", 8, "LABEL", "Scorpion");

        var tails = _service.GetAllTails();
        Assert.Contains(tails, t => t.Name == "Scorpion");
    }

    [Fact]
    public void GetTailName_InvalidId_ReturnsFallback()
    {
        Assert.Equal("Tail 250", _service.GetTailName(250));
    }

    [Fact]
    public void GetWingName_StarLabel_ReturnsFallback()
    {
        // Row with only **** label
        _mockGameData.Set2DAValue("wingmodel", 50, "LABEL", "****");
        Assert.Equal("Wings 50", _service.GetWingName(50));
    }

    [Fact]
    public void GetTailName_StarLabel_ReturnsFallback()
    {
        _mockGameData.Set2DAValue("tailmodel", 50, "LABEL", "****");
        Assert.Equal("Tail 50", _service.GetTailName(50));
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

    [Fact]
    public void GetAllFactions_DefaultFactionIds_AreSequential()
    {
        var factions = _service.GetAllFactions();
        for (int i = 0; i < factions.Count; i++)
        {
            Assert.Equal((ushort)i, factions[i].Id);
        }
    }

    [Fact]
    public void GetAllFactions_NullModuleDir_ReturnsDefaults()
    {
        var factions = _service.GetAllFactions(null);
        Assert.Equal(5, factions.Count);
    }

    [Fact]
    public void GetAllFactions_EmptyModuleDir_ReturnsDefaults()
    {
        var factions = _service.GetAllFactions("");
        Assert.Equal(5, factions.Count);
    }

    [Fact]
    public void GetAllFactions_DirWithoutFacFile_ReturnsDefaults()
    {
        // Use a real directory that exists but has no repute.fac
        var tempDir = Path.Combine(Path.GetTempPath(), "radoub_test_nofac_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);
        try
        {
            var factions = _service.GetAllFactions(tempDir);
            Assert.Equal(5, factions.Count);
            Assert.Equal("PC", factions[0].Name);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
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

    [Fact]
    public void GetPackageName_NoData_ReturnsFallback()
    {
        Assert.Equal("Package 200", _service.GetPackageName(200));
    }

    [Fact]
    public void GetPackagesForClass_StarClassId_SkipsRow()
    {
        _mockGameData.Set2DAValue("packages", 11, "Label", "NoClass");
        _mockGameData.Set2DAValue("packages", 11, "Name", "****");
        _mockGameData.Set2DAValue("packages", 11, "ClassID", "****");

        var packages = _service.GetPackagesForClass(0);
        Assert.DoesNotContain(packages, p => p.Name == "NoClass");
    }

    [Fact]
    public void GetPackagesForClass_EmptyClassId_SkipsRow()
    {
        _mockGameData.Set2DAValue("packages", 12, "Label", "EmptyClass");
        _mockGameData.Set2DAValue("packages", 12, "Name", "****");
        _mockGameData.Set2DAValue("packages", 12, "ClassID", "");

        var packages = _service.GetPackagesForClass(0);
        Assert.DoesNotContain(packages, p => p.Name == "EmptyClass");
    }

    [Fact]
    public void GetAllPackages_NoData_ReturnsEmptyList()
    {
        var emptyMock = new MockGameDataService(includeSampleData: false);
        var service = new AppearanceService(emptyMock);
        var packages = service.GetAllPackages();
        Assert.Empty(packages);
    }

    [Fact]
    public void GetAllSoundSets_NoData_ReturnsEmptyList()
    {
        var emptyMock = new MockGameDataService(includeSampleData: false);
        var service = new AppearanceService(emptyMock);
        var soundSets = service.GetAllSoundSets();
        Assert.Empty(soundSets);
    }

    [Fact]
    public void GetAllSoundSets_UsesDisplayNameNotLabel()
    {
        var soundSets = _service.GetAllSoundSets();
        // Row 0 has STRREF=7000 → TLK "Male Voice 1", LABEL="Male_1"
        var first = soundSets.FirstOrDefault(s => s.Id == 0);
        Assert.Equal("Male Voice 1", first.Name);
    }

    [Fact]
    public void GetAllSoundSets_SecondCall_ReturnsCachedResult()
    {
        var first = _service.GetAllSoundSets();
        var firstCount = first.Count;

        _mockGameData.Set2DAValue("soundset", 99, "LABEL", "AddedAfterCache");

        var second = _service.GetAllSoundSets();
        Assert.Equal(firstCount, second.Count);
        Assert.DoesNotContain(second, s => s.Name == "AddedAfterCache" || s.Name == "Sound Set 99");
    }

    [Fact]
    public void GetAllSoundSets_AfterInvalidate_ReturnsFreshResult()
    {
        _ = _service.GetAllSoundSets();

        _mockGameData.Set2DAValue("soundset", 99, "LABEL", "AddedAfterInvalidate");
        _service.InvalidateCaches();

        var fresh = _service.GetAllSoundSets();
        Assert.Contains(fresh, s => s.Id == 99);
    }

    #endregion

    #region ResolveAppearanceSources

    [Fact]
    public void ResolveAppearanceSources_SimpleModel_ResolvesToBif()
    {
        // Simple model: Race = "BADGER" → MDL resref "badger"
        _mockGameData.AddResourceInfo("badger", Radoub.Formats.Common.ResourceTypes.Mdl); // MDL type, BIF source

        var appearances = new System.Collections.Generic.List<AppearanceInfo>
        {
            new() { AppearanceId = 0, Label = "Badger", Race = "BADGER", IsPartBased = false }
        };

        _service.ResolveAppearanceSources(appearances);

        Assert.Equal(AppearanceSource.Bif, appearances[0].Source);
    }

    [Fact]
    public void ResolveAppearanceSources_SimpleModelWithCPrefix_ResolvesToBif()
    {
        // Some creature models use "c_" prefix
        _mockGameData.AddResourceInfo("c_dragon", Radoub.Formats.Common.ResourceTypes.Mdl);

        var appearances = new System.Collections.Generic.List<AppearanceInfo>
        {
            new() { AppearanceId = 0, Label = "Dragon", Race = "DRAGON", IsPartBased = false }
        };

        _service.ResolveAppearanceSources(appearances);

        Assert.Equal(AppearanceSource.Bif, appearances[0].Source);
    }

    [Fact]
    public void ResolveAppearanceSources_PartBasedModel_ResolvesViaPrefixMatch()
    {
        // Part-based model: Race = "H" (single letter for Human)
        // MDL lookup should find models starting with "p" + race letter
        _mockGameData.AddResourceInfo("pmh0", Radoub.Formats.Common.ResourceTypes.Mdl); // Male human default skeleton

        var appearances = new System.Collections.Generic.List<AppearanceInfo>
        {
            new() { AppearanceId = 0, Label = "Human", Race = "H", IsPartBased = true }
        };

        _service.ResolveAppearanceSources(appearances);

        Assert.Equal(AppearanceSource.Bif, appearances[0].Source);
    }

    [Fact]
    public void ResolveAppearanceSources_PartBasedDwarf_ResolvesViaPrefixMatch()
    {
        // Part-based model: Race = "D" (Dwarf)
        _mockGameData.AddResourceInfo("pmd0", Radoub.Formats.Common.ResourceTypes.Mdl);

        var appearances = new System.Collections.Generic.List<AppearanceInfo>
        {
            new() { AppearanceId = 0, Label = "Dwarf", Race = "D", IsPartBased = true }
        };

        _service.ResolveAppearanceSources(appearances);

        Assert.Equal(AppearanceSource.Bif, appearances[0].Source);
    }

    [Fact]
    public void ResolveAppearanceSources_PartBasedNoMatch_ResolvesToUnknown()
    {
        // Part-based model with no matching MDL
        var appearances = new System.Collections.Generic.List<AppearanceInfo>
        {
            new() { AppearanceId = 0, Label = "Weird", Race = "Z", IsPartBased = true }
        };

        _service.ResolveAppearanceSources(appearances);

        Assert.Equal(AppearanceSource.Unknown, appearances[0].Source);
    }

    [Fact]
    public void ResolveAppearanceSources_HakSource_MapsCorrectly()
    {
        _mockGameData.AddResourceInfo("cep_model", Radoub.Formats.Common.ResourceTypes.Mdl,
            Radoub.Formats.Services.GameResourceSource.Hak);

        var appearances = new System.Collections.Generic.List<AppearanceInfo>
        {
            new() { AppearanceId = 0, Label = "CEP_Thing", Race = "CEP_MODEL", IsPartBased = false }
        };

        _service.ResolveAppearanceSources(appearances);

        Assert.Equal(AppearanceSource.Hak, appearances[0].Source);
    }

    #endregion
}
