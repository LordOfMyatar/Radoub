using Quartermaster.Services;
using Radoub.TestUtilities.Mocks;
using Xunit;

namespace Quartermaster.Tests;

/// <summary>
/// Tests for ClassHasDomains and ClassGrantsFamiliar on CreatureDisplayService,
/// and GetAllFamiliars for familiar dropdown population.
/// </summary>
public class ClassDomainAndFamiliarTests
{
    private readonly CreatureDisplayService _displayService;

    public ClassDomainAndFamiliarTests()
    {
        var mockGameData = new MockGameDataService(includeSampleData: true);
        _displayService = new CreatureDisplayService(mockGameData);
    }

    #region ClassHasDomains

    [Fact]
    public void ClassHasDomains_Cleric_ReturnsTrue()
    {
        // Class 2 = Cleric, which has a package with Domain1 set
        Assert.True(_displayService.ClassHasDomains(2));
    }

    [Fact]
    public void ClassHasDomains_Fighter_ReturnsFalse()
    {
        // Class 4 = Fighter, package has Domain1 = "****"
        Assert.False(_displayService.ClassHasDomains(4));
    }

    [Fact]
    public void ClassHasDomains_Wizard_ReturnsFalse()
    {
        Assert.False(_displayService.ClassHasDomains(10));
    }

    [Fact]
    public void ClassHasDomains_InvalidClass_ReturnsFalse()
    {
        Assert.False(_displayService.ClassHasDomains(999));
    }

    #endregion

    #region ClassGrantsFamiliar

    [Fact]
    public void ClassGrantsFamiliar_Wizard_ReturnsTrue()
    {
        Assert.True(_displayService.ClassGrantsFamiliar(10));
    }

    [Fact]
    public void ClassGrantsFamiliar_Sorcerer_ReturnsTrue()
    {
        // Class 9 = Sorcerer in mock data
        // Note: NWN Sorcerer is class 37 in game data, but the mock uses label "Sorcerer" at row 9
        Assert.True(_displayService.ClassGrantsFamiliar(9));
    }

    [Fact]
    public void ClassGrantsFamiliar_Fighter_ReturnsFalse()
    {
        Assert.False(_displayService.ClassGrantsFamiliar(4));
    }

    [Fact]
    public void ClassGrantsFamiliar_Cleric_ReturnsFalse()
    {
        Assert.False(_displayService.ClassGrantsFamiliar(2));
    }

    [Fact]
    public void ClassGrantsFamiliar_Druid_ReturnsTrue()
    {
        // Class 3 = Druid, which has a package with Associate set (animal companion)
        Assert.True(_displayService.ClassGrantsFamiliar(3));
    }

    [Fact]
    public void ClassGrantsFamiliar_InvalidClass_ReturnsFalse()
    {
        Assert.False(_displayService.ClassGrantsFamiliar(999));
    }

    #endregion

    #region GetAllFamiliars

    [Fact]
    public void GetAllFamiliars_ReturnsEntries()
    {
        var familiars = _displayService.GetAllFamiliars();
        Assert.True(familiars.Count > 0);
    }

    [Fact]
    public void GetAllFamiliars_HasExpectedCount()
    {
        var familiars = _displayService.GetAllFamiliars();
        Assert.Equal(11, familiars.Count);
    }

    [Fact]
    public void GetAllFamiliars_HasCorrectNames()
    {
        var familiars = _displayService.GetAllFamiliars();
        Assert.Contains(familiars, f => f.Name == "Bat");
        Assert.Contains(familiars, f => f.Name == "Imp");
        Assert.Contains(familiars, f => f.Name == "Faerie Dragon");
        Assert.Contains(familiars, f => f.Name == "Eyeball");
    }

    [Fact]
    public void GetAllFamiliars_HasCorrectIds()
    {
        var familiars = _displayService.GetAllFamiliars();
        var bat = familiars.Find(f => f.Name == "Bat");
        Assert.Equal(0, bat.Id);
        var imp = familiars.Find(f => f.Name == "Imp");
        Assert.Equal(3, imp.Id);
    }

    #endregion
}
