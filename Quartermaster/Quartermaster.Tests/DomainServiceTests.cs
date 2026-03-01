using Quartermaster.Services;
using Radoub.TestUtilities.Mocks;
using Xunit;

namespace Quartermaster.Tests;

/// <summary>
/// Tests for DomainService which reads domains.2da for spell lists, granted feats, and descriptions.
/// </summary>
public class DomainServiceTests
{
    private readonly DomainService _domainService;

    public DomainServiceTests()
    {
        var mockGameData = new MockGameDataService(includeSampleData: true);
        var displayService = new CreatureDisplayService(mockGameData);
        _domainService = displayService.Domains;
    }

    #region Domain Lookup

    [Fact]
    public void GetDomainInfo_ValidDomain_ReturnsInfo()
    {
        var info = _domainService.GetDomainInfo(0);
        Assert.NotNull(info);
    }

    [Fact]
    public void GetDomainInfo_InvalidDomain_ReturnsNull()
    {
        // Row 3 has "****" label — should return null
        var info = _domainService.GetDomainInfo(3);
        Assert.Null(info);
    }

    [Fact]
    public void GetDomainInfo_OutOfRange_ReturnsNull()
    {
        var info = _domainService.GetDomainInfo(999);
        Assert.Null(info);
    }

    #endregion

    #region Domain Names and Descriptions

    [Fact]
    public void AirDomain_HasCorrectName()
    {
        var info = _domainService.GetDomainInfo(0);
        Assert.Equal("Air", info!.Name);
    }

    [Fact]
    public void AnimalDomain_HasCorrectName()
    {
        var info = _domainService.GetDomainInfo(1);
        Assert.Equal("Animal", info!.Name);
    }

    [Fact]
    public void DeathDomain_HasCorrectName()
    {
        var info = _domainService.GetDomainInfo(2);
        Assert.Equal("Death", info!.Name);
    }

    [Fact]
    public void AirDomain_HasDescription()
    {
        var info = _domainService.GetDomainInfo(0);
        Assert.Contains("wind and lightning", info!.Description);
    }

    #endregion

    #region Domain Spells

    [Fact]
    public void AirDomain_Has9Spells()
    {
        var info = _domainService.GetDomainInfo(0);
        Assert.Equal(9, info!.DomainSpells.Count);
    }

    [Fact]
    public void AirDomain_SpellsOrderedByLevel()
    {
        var info = _domainService.GetDomainInfo(0);
        for (int i = 0; i < info!.DomainSpells.Count; i++)
        {
            Assert.Equal(i + 1, info.DomainSpells[i].Level);
        }
    }

    [Fact]
    public void AirDomain_Level1Spell_IsCallLightning()
    {
        var info = _domainService.GetDomainInfo(0);
        Assert.Equal("Call Lightning", info!.DomainSpells[0].Name);
    }

    [Fact]
    public void AirDomain_Level9Spell_IsGate()
    {
        var info = _domainService.GetDomainInfo(0);
        Assert.Equal("Gate", info!.DomainSpells[8].Name);
    }

    [Fact]
    public void AnimalDomain_Has9Spells()
    {
        var info = _domainService.GetDomainInfo(1);
        Assert.Equal(9, info!.DomainSpells.Count);
    }

    [Fact]
    public void DeathDomain_Has7Spells_MissingLevel8And9()
    {
        var info = _domainService.GetDomainInfo(2);
        // Death domain has Level_8 and Level_9 as "****"
        Assert.Equal(7, info!.DomainSpells.Count);
        Assert.DoesNotContain(info.DomainSpells, s => s.Level == 8);
        Assert.DoesNotContain(info.DomainSpells, s => s.Level == 9);
    }

    [Fact]
    public void DomainSpells_HaveCorrectSpellIds()
    {
        var info = _domainService.GetDomainInfo(0);
        // Air domain level 1 = spell 0, level 2 = spell 1, etc.
        Assert.Equal(0, info!.DomainSpells[0].SpellId);
        Assert.Equal(1, info.DomainSpells[1].SpellId);
        Assert.Equal(8, info.DomainSpells[8].SpellId);
    }

    #endregion

    #region Granted Feats

    [Fact]
    public void AirDomain_HasGrantedFeat()
    {
        var info = _domainService.GetDomainInfo(0);
        Assert.True(info!.GrantedFeatId >= 0);
    }

    [Fact]
    public void AirDomain_GrantedFeatName_IsElementalTurning()
    {
        var info = _domainService.GetDomainInfo(0);
        Assert.Equal("Elemental Turning", info!.GrantedFeatName);
    }

    [Fact]
    public void AnimalDomain_GrantedFeatName_IsAnimalCompanion()
    {
        var info = _domainService.GetDomainInfo(1);
        Assert.Equal("Animal Companion", info!.GrantedFeatName);
    }

    [Fact]
    public void DeathDomain_NoGrantedFeat()
    {
        var info = _domainService.GetDomainInfo(2);
        Assert.Equal(-1, info!.GrantedFeatId);
    }

    #endregion

    #region Caching

    [Fact]
    public void GetDomainInfo_CalledTwice_ReturnsSameInstance()
    {
        var info1 = _domainService.GetDomainInfo(0);
        var info2 = _domainService.GetDomainInfo(0);
        Assert.Same(info1, info2);
    }

    #endregion

    #region FormatDomainSummary

    [Fact]
    public void FormatDomainSummary_IncludesGrantedFeat()
    {
        var info = _domainService.GetDomainInfo(0)!;
        var summary = DomainService.FormatDomainSummary(info);
        Assert.Contains("Granted Feat: Elemental Turning", summary);
    }

    [Fact]
    public void FormatDomainSummary_IncludesDomainSpells()
    {
        var info = _domainService.GetDomainInfo(0)!;
        var summary = DomainService.FormatDomainSummary(info);
        Assert.Contains("Domain Spells:", summary);
        Assert.Contains("Level 1: Call Lightning", summary);
    }

    [Fact]
    public void FormatDomainSummary_NoGrantedFeat_OmitsLine()
    {
        var info = _domainService.GetDomainInfo(2)!;
        var summary = DomainService.FormatDomainSummary(info);
        Assert.DoesNotContain("Granted Feat:", summary);
    }

    #endregion

    #region GetAllDomains

    [Fact]
    public void GetAllDomains_ReturnsValidDomains()
    {
        var domains = _domainService.GetAllDomains();
        Assert.True(domains.Count >= 3); // Air, Animal, Death at minimum
    }

    [Fact]
    public void GetAllDomains_ExcludesInvalidRows()
    {
        var domains = _domainService.GetAllDomains();
        // Row 3 has "****" label and should be excluded
        Assert.DoesNotContain(domains, d => d.Id == 3);
    }

    [Fact]
    public void GetAllDomains_IncludesCorrectNames()
    {
        var domains = _domainService.GetAllDomains();
        Assert.Contains(domains, d => d.Name == "Air");
        Assert.Contains(domains, d => d.Name == "Animal");
        Assert.Contains(domains, d => d.Name == "Death");
    }

    [Fact]
    public void GetAllDomains_HasCorrectIds()
    {
        var domains = _domainService.GetAllDomains();
        var air = domains.Find(d => d.Name == "Air");
        Assert.Equal(0, air.Id);
        var animal = domains.Find(d => d.Name == "Animal");
        Assert.Equal(1, animal.Id);
    }

    #endregion
}
