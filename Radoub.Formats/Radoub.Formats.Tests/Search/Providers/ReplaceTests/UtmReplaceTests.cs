using Radoub.Formats.Gff;
using Radoub.Formats.Search;
using Radoub.Formats.Utm;
using Xunit;

namespace Radoub.Formats.Tests.Search.Providers.ReplaceTests;

public class UtmReplaceTests
{
    private static UtmFile CreateTestUtm()
    {
        return new UtmFile
        {
            LocName = new CExoLocString { LocalizedStrings = new Dictionary<uint, string>
            {
                [0] = "Louis's General Store"
            }},
            Tag = "LOUIS_STORE",
            ResRef = "louis_store",
            Comment = "Main merchant for Louis quest",
            OnOpenStore = "gc_open_store",
            OnStoreClosed = "gc_close_store"
        };
    }

    private static GffFile UtmToGff(UtmFile utm)
    {
        var bytes = UtmWriter.Write(utm);
        return GffReader.Read(bytes);
    }

    [Fact]
    public void Replace_LocString_Name()
    {
        var provider = new UtmSearchProvider();
        var gff = UtmToGff(CreateTestUtm());
        var matches = provider.Search(gff, new SearchCriteria { Pattern = "Louis" });
        var nameMatch = matches.First(m => m.Field.Name == "Name");

        gff = UtmToGff(CreateTestUtm());
        var results = provider.Replace(gff, new[] { new ReplaceOperation { Match = nameMatch, ReplacementText = "Marcel" } });

        Assert.All(results, r => Assert.True(r.Success));
        var bytes = GffWriter.Write(gff);
        var utm = UtmReader.Read(bytes);
        Assert.Equal("Marcel's General Store", utm.LocName.GetString(0));
    }

    [Fact]
    public void Replace_Tag()
    {
        var provider = new UtmSearchProvider();
        var gff = UtmToGff(CreateTestUtm());
        var matches = provider.Search(gff, new SearchCriteria { Pattern = "LOUIS_STORE" });
        var tagMatch = matches.First(m => m.Field.Name == "Tag");

        gff = UtmToGff(CreateTestUtm());
        var results = provider.Replace(gff, new[] { new ReplaceOperation { Match = tagMatch, ReplacementText = "MARCEL_STORE" } });

        Assert.All(results, r => Assert.True(r.Success));
        var bytes = GffWriter.Write(gff);
        var utm = UtmReader.Read(bytes);
        Assert.Equal("MARCEL_STORE", utm.Tag);
    }

    [Fact]
    public void Replace_Script_OnOpenStore()
    {
        var provider = new UtmSearchProvider();
        var gff = UtmToGff(CreateTestUtm());
        var matches = provider.Search(gff, new SearchCriteria { Pattern = "gc_open_store" });
        var scriptMatch = matches.First(m => m.Field.Name == "OnOpenStore");

        gff = UtmToGff(CreateTestUtm());
        var results = provider.Replace(gff, new[] { new ReplaceOperation { Match = scriptMatch, ReplacementText = "gc_open_new" } });

        Assert.All(results, r => Assert.True(r.Success));
        var bytes = GffWriter.Write(gff);
        var utm = UtmReader.Read(bytes);
        Assert.Equal("gc_open_new", utm.OnOpenStore);
    }
}
