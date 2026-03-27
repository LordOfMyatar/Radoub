using Radoub.Formats.Are;
using Radoub.Formats.Gff;
using Radoub.Formats.Search;
using Xunit;

namespace Radoub.Formats.Tests.Search.Providers;

public class AreSearchProviderTests
{
    private static AreFile CreateTestAre()
    {
        return new AreFile
        {
            Name = new CExoLocString { LocalizedStrings = new Dictionary<uint, string>
            {
                [0] = "Waterdeep Market District"
            }},
            Tag = "AR_MARKET",
            ResRef = "ar_market",
            Comments = "Main marketplace area for Chapter 2",
            OnEnter = "ar_enter_mrkt",
            OnExit = "ar_exit_mrkt",
            OnHeartbeat = "ar_hb_mrkt",
            OnUserDefined = "ar_ud_mrkt",
            Width = 8,
            Height = 8
        };
    }

    private static GffFile AreToGff(AreFile are)
    {
        var bytes = AreWriter.Write(are);
        return GffReader.Read(bytes);
    }

    [Fact]
    public void Search_FindsLocName()
    {
        var provider = new AreSearchProvider();
        var gff = AreToGff(CreateTestAre());
        var criteria = new SearchCriteria { Pattern = "Waterdeep" };

        var matches = provider.Search(gff, criteria);

        Assert.Contains(matches, m => m.Field.Name == "Name" && m.MatchedText == "Waterdeep");
    }

    [Fact]
    public void Search_FindsTag()
    {
        var provider = new AreSearchProvider();
        var gff = AreToGff(CreateTestAre());
        var criteria = new SearchCriteria { Pattern = "AR_MARKET" };

        var matches = provider.Search(gff, criteria);

        Assert.Contains(matches, m => m.Field.Name == "Tag");
    }

    [Fact]
    public void Search_FindsResRef()
    {
        var provider = new AreSearchProvider();
        var gff = AreToGff(CreateTestAre());
        var criteria = new SearchCriteria { Pattern = "ar_market" };

        var matches = provider.Search(gff, criteria);

        Assert.Contains(matches, m => m.Field.Name == "ResRef");
    }

    [Fact]
    public void Search_FindsComments()
    {
        var provider = new AreSearchProvider();
        var gff = AreToGff(CreateTestAre());
        var criteria = new SearchCriteria { Pattern = "Chapter 2" };

        var matches = provider.Search(gff, criteria);

        Assert.Contains(matches, m => m.Field.Name == "Comments");
    }

    [Fact]
    public void Search_FindsOnEnter()
    {
        var provider = new AreSearchProvider();
        var gff = AreToGff(CreateTestAre());
        var criteria = new SearchCriteria { Pattern = "ar_enter_mrkt" };

        var matches = provider.Search(gff, criteria);

        Assert.Contains(matches, m => m.Field.Name == "OnEnter");
    }

    [Fact]
    public void Search_FindsOnExit()
    {
        var provider = new AreSearchProvider();
        var gff = AreToGff(CreateTestAre());
        var criteria = new SearchCriteria { Pattern = "ar_exit_mrkt" };

        var matches = provider.Search(gff, criteria);

        Assert.Contains(matches, m => m.Field.Name == "OnExit");
    }

    [Fact]
    public void Search_FindsOnHeartbeat()
    {
        var provider = new AreSearchProvider();
        var gff = AreToGff(CreateTestAre());
        var criteria = new SearchCriteria { Pattern = "ar_hb_mrkt" };

        var matches = provider.Search(gff, criteria);

        Assert.Contains(matches, m => m.Field.Name == "OnHeartbeat");
    }

    [Fact]
    public void Search_FindsOnUserDefined()
    {
        var provider = new AreSearchProvider();
        var gff = AreToGff(CreateTestAre());
        var criteria = new SearchCriteria { Pattern = "ar_ud_mrkt" };

        var matches = provider.Search(gff, criteria);

        Assert.Contains(matches, m => m.Field.Name == "OnUserDefined");
    }

    [Fact]
    public void Search_ScriptFilter()
    {
        var provider = new AreSearchProvider();
        var gff = AreToGff(CreateTestAre());
        var criteria = new SearchCriteria
        {
            Pattern = "mrkt",
            FieldTypeFilter = new[] { SearchFieldType.Script }
        };

        var matches = provider.Search(gff, criteria);

        Assert.Equal(4, matches.Count);
        Assert.All(matches, m => Assert.Equal(SearchFieldType.Script, m.Field.FieldType));
    }

    [Fact]
    public void Search_LocationIsFieldName()
    {
        var provider = new AreSearchProvider();
        var gff = AreToGff(CreateTestAre());
        var criteria = new SearchCriteria { Pattern = "ar_enter_mrkt" };

        var matches = provider.Search(gff, criteria);

        var match = Assert.Single(matches);
        Assert.Equal("OnEnter", match.Location as string);
    }

    [Fact]
    public void FileType_IsAre()
    {
        var provider = new AreSearchProvider();
        Assert.Equal(Radoub.Formats.Common.ResourceTypes.Are, provider.FileType);
    }

    [Fact]
    public void Extensions_ContainsAre()
    {
        var provider = new AreSearchProvider();
        Assert.Contains(".are", provider.Extensions);
    }

    [Fact]
    public void Replace_UpdatesTag()
    {
        var provider = new AreSearchProvider();
        var gff = AreToGff(CreateTestAre());
        var criteria = new SearchCriteria { Pattern = "AR_MARKET" };

        var matches = provider.Search(gff, criteria);
        var tagMatch = matches.First(m => m.Field.Name == "Tag");

        var operations = new List<ReplaceOperation>
        {
            new ReplaceOperation { Match = tagMatch, ReplacementText = "AR_BAZAAR" }
        };

        var results = provider.Replace(gff, operations);

        Assert.Single(results);
        Assert.True(results[0].Success);

        // Verify the replacement stuck by re-searching
        var verifyMatches = provider.Search(gff, new SearchCriteria { Pattern = "AR_BAZAAR" });
        Assert.Contains(verifyMatches, m => m.Field.Name == "Tag");
    }

    [Fact]
    public void Replace_UpdatesLocName()
    {
        var provider = new AreSearchProvider();
        var gff = AreToGff(CreateTestAre());
        var criteria = new SearchCriteria { Pattern = "Waterdeep" };

        var matches = provider.Search(gff, criteria);
        var nameMatch = matches.First(m => m.Field.Name == "Name");

        var operations = new List<ReplaceOperation>
        {
            new ReplaceOperation { Match = nameMatch, ReplacementText = "Neverwinter" }
        };

        var results = provider.Replace(gff, operations);

        Assert.Single(results);
        Assert.True(results[0].Success);

        var verifyMatches = provider.Search(gff, new SearchCriteria { Pattern = "Neverwinter" });
        Assert.Contains(verifyMatches, m => m.Field.Name == "Name");
    }
}
