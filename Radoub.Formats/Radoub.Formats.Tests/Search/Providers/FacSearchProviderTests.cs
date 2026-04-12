using Radoub.Formats.Fac;
using Radoub.Formats.Gff;
using Radoub.Formats.Search;
using Xunit;

namespace Radoub.Formats.Tests.Search.Providers;

public class FacSearchProviderTests
{
    private static FacFile CreateTestFac()
    {
        var fac = new FacFile();
        fac.FactionList.Add(new Faction { FactionName = "PC", FactionGlobal = 0, FactionParentID = 0xFFFFFFFF });
        fac.FactionList.Add(new Faction { FactionName = "Hostile", FactionGlobal = 1, FactionParentID = 0xFFFFFFFF });
        fac.FactionList.Add(new Faction { FactionName = "Commoner", FactionGlobal = 1, FactionParentID = 0xFFFFFFFF });
        fac.FactionList.Add(new Faction { FactionName = "Merchant", FactionGlobal = 1, FactionParentID = 0xFFFFFFFF });
        fac.FactionList.Add(new Faction { FactionName = "Defender", FactionGlobal = 1, FactionParentID = 0xFFFFFFFF });
        fac.FactionList.Add(new Faction { FactionName = "Shadow Thieves", FactionGlobal = 0, FactionParentID = 1 });
        return fac;
    }

    private static GffFile FacToGff(FacFile fac)
    {
        var bytes = FacWriter.Write(fac);
        return GffReader.Read(bytes);
    }

    [Fact]
    public void Search_FindsFactionName()
    {
        var provider = new FacSearchProvider();
        var gff = FacToGff(CreateTestFac());
        var criteria = new SearchCriteria { Pattern = "Shadow Thieves" };

        var matches = provider.Search(gff, criteria);

        Assert.Contains(matches, m =>
            m.Field.Name == "Faction Name" &&
            m.MatchedText == "Shadow Thieves");
    }

    [Fact]
    public void Search_DisplayPathShowsFactionIndex()
    {
        var provider = new FacSearchProvider();
        var gff = FacToGff(CreateTestFac());
        var criteria = new SearchCriteria { Pattern = "Shadow Thieves" };

        var matches = provider.Search(gff, criteria);

        var match = Assert.Single(matches);
        var location = match.Location as FacMatchLocation;
        Assert.NotNull(location);
        Assert.Equal("Faction #5: Shadow Thieves", location.DisplayPath);
        Assert.Equal(5, location.FactionIndex);
    }

    [Fact]
    public void Search_FindsMultipleFactions()
    {
        var provider = new FacSearchProvider();
        var gff = FacToGff(CreateTestFac());
        var criteria = new SearchCriteria { Pattern = "er", IsRegex = false };

        var matches = provider.Search(gff, criteria);

        // "Commoner", "Defender" both contain "er"
        Assert.True(matches.Count >= 2);
        Assert.Contains(matches, m => m.FullFieldValue == "Commoner");
        Assert.Contains(matches, m => m.FullFieldValue == "Defender");
    }

    [Fact]
    public void Search_CaseInsensitive()
    {
        var provider = new FacSearchProvider();
        var gff = FacToGff(CreateTestFac());
        var criteria = new SearchCriteria { Pattern = "hostile", CaseSensitive = false };

        var matches = provider.Search(gff, criteria);

        Assert.Contains(matches, m => m.FullFieldValue == "Hostile");
    }

    [Fact]
    public void Search_NoMatchReturnsEmpty()
    {
        var provider = new FacSearchProvider();
        var gff = FacToGff(CreateTestFac());
        var criteria = new SearchCriteria { Pattern = "NonExistentFaction" };

        var matches = provider.Search(gff, criteria);

        Assert.Empty(matches);
    }

    [Fact]
    public void Search_FactionNameFieldIsReplaceable()
    {
        var provider = new FacSearchProvider();
        var gff = FacToGff(CreateTestFac());
        var criteria = new SearchCriteria { Pattern = "Shadow Thieves" };

        var matches = provider.Search(gff, criteria);

        var match = Assert.Single(matches);
        Assert.True(match.Field.IsReplaceable);
    }
}
