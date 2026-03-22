using Radoub.Formats.Gff;
using Radoub.Formats.Search;
using Radoub.Formats.Utm;
using Xunit;

namespace Radoub.Formats.Tests.Search.Providers;

public class UtmSearchProviderTests
{
    private static UtmFile CreateTestUtm()
    {
        return new UtmFile
        {
            LocName = new CExoLocString { LocalizedStrings = new Dictionary<uint, string>
            {
                [0] = "Louis Romain's Emporium"
            }},
            Tag = "LOUIS_STORE",
            ResRef = "louis_store",
            Comment = "Main merchant for western district",
            OnOpenStore = "gc_open_store",
            OnStoreClosed = "gc_close_store",
            VarTable = new List<Variable>
            {
                new Variable { Name = "nDiscount", Type = VariableType.Int, Value = 10 },
                new Variable { Name = "sGreeting", Type = VariableType.String, Value = "Welcome to the emporium" }
            }
        };
    }

    private static GffFile UtmToGff(UtmFile utm)
    {
        var bytes = UtmWriter.Write(utm);
        return GffReader.Read(bytes);
    }

    [Fact]
    public void Search_FindsLocName()
    {
        var provider = new UtmSearchProvider();
        var gff = UtmToGff(CreateTestUtm());
        var criteria = new SearchCriteria { Pattern = "Louis Romain" };

        var matches = provider.Search(gff, criteria);

        Assert.Contains(matches, m => m.Field.Name == "Name" && m.MatchedText == "Louis Romain");
    }

    [Fact]
    public void Search_FindsTag()
    {
        var provider = new UtmSearchProvider();
        var gff = UtmToGff(CreateTestUtm());
        var criteria = new SearchCriteria { Pattern = "LOUIS_STORE" };

        var matches = provider.Search(gff, criteria);

        Assert.Contains(matches, m => m.Field.Name == "Tag");
    }

    [Fact]
    public void Search_FindsResRef()
    {
        var provider = new UtmSearchProvider();
        var gff = UtmToGff(CreateTestUtm());
        var criteria = new SearchCriteria { Pattern = "louis_store" };

        var matches = provider.Search(gff, criteria);

        Assert.Contains(matches, m => m.Field.Name == "ResRef");
    }

    [Fact]
    public void Search_FindsComment()
    {
        var provider = new UtmSearchProvider();
        var gff = UtmToGff(CreateTestUtm());
        var criteria = new SearchCriteria { Pattern = "western district" };

        var matches = provider.Search(gff, criteria);

        Assert.Contains(matches, m => m.Field.Name == "Comment");
    }

    [Fact]
    public void Search_FindsOnOpenStore()
    {
        var provider = new UtmSearchProvider();
        var gff = UtmToGff(CreateTestUtm());
        var criteria = new SearchCriteria { Pattern = "gc_open_store" };

        var matches = provider.Search(gff, criteria);

        Assert.Contains(matches, m => m.Field.Name == "OnOpenStore");
    }

    [Fact]
    public void Search_FindsOnStoreClosed()
    {
        var provider = new UtmSearchProvider();
        var gff = UtmToGff(CreateTestUtm());
        var criteria = new SearchCriteria { Pattern = "gc_close_store" };

        var matches = provider.Search(gff, criteria);

        Assert.Contains(matches, m => m.Field.Name == "OnStoreClosed");
    }

    [Fact]
    public void Search_FindsVarTableName()
    {
        var provider = new UtmSearchProvider();
        var gff = UtmToGff(CreateTestUtm());
        var criteria = new SearchCriteria { Pattern = "nDiscount" };

        var matches = provider.Search(gff, criteria);

        Assert.Contains(matches, m => m.Field.Name == "Local Variables");
    }

    [Fact]
    public void Search_FindsVarTableStringValue()
    {
        var provider = new UtmSearchProvider();
        var gff = UtmToGff(CreateTestUtm());
        var criteria = new SearchCriteria { Pattern = "emporium" };

        var matches = provider.Search(gff, criteria);

        // Should find in both LocName and VarTable string value
        Assert.Contains(matches, m => m.Field.Name == "Local Variables");
    }

    [Fact]
    public void Search_ScriptFilter()
    {
        var provider = new UtmSearchProvider();
        var gff = UtmToGff(CreateTestUtm());
        var criteria = new SearchCriteria
        {
            Pattern = "gc_",
            FieldTypeFilter = new[] { SearchFieldType.Script }
        };

        var matches = provider.Search(gff, criteria);

        Assert.Equal(2, matches.Count);
        Assert.All(matches, m => Assert.Equal(SearchFieldType.Script, m.Field.FieldType));
    }

    [Fact]
    public void Search_LocationIsFieldName()
    {
        var provider = new UtmSearchProvider();
        var gff = UtmToGff(CreateTestUtm());
        var criteria = new SearchCriteria { Pattern = "gc_open_store" };

        var matches = provider.Search(gff, criteria);

        var match = Assert.Single(matches);
        Assert.Equal("OnOpenStore", match.Location as string);
    }

    [Fact]
    public void FileType_IsUtm()
    {
        var provider = new UtmSearchProvider();
        Assert.Equal(Radoub.Formats.Common.ResourceTypes.Utm, provider.FileType);
    }

    [Fact]
    public void Extensions_ContainsUtm()
    {
        var provider = new UtmSearchProvider();
        Assert.Contains(".utm", provider.Extensions);
    }
}
