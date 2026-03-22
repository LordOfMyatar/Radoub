using Radoub.Formats.Gff;
using Radoub.Formats.Jrl;
using Radoub.Formats.Search;
using Xunit;

namespace Radoub.Formats.Tests.Search.Providers.ReplaceTests;

public class JrlReplaceTests
{
    private static JrlFile CreateTestJrl()
    {
        return new JrlFile
        {
            Categories = new List<JournalCategory>
            {
                new JournalCategory
                {
                    Name = new CExoLocString { LocalizedStrings = new Dictionary<uint, string>
                    {
                        [0] = "The Louis Romain Quest"
                    }},
                    Tag = "q_louis",
                    Comment = "Main quest line for Louis",
                    Entries = new List<JournalEntry>
                    {
                        new JournalEntry
                        {
                            ID = 1,
                            Text = new CExoLocString { LocalizedStrings = new Dictionary<uint, string>
                            {
                                [0] = "I met Louis Romain at the docks."
                            }}
                        },
                        new JournalEntry
                        {
                            ID = 2,
                            Text = new CExoLocString { LocalizedStrings = new Dictionary<uint, string>
                            {
                                [0] = "Louis Romain asked me to find the amulet."
                            }}
                        }
                    }
                }
            }
        };
    }

    private static GffFile JrlToGff(JrlFile jrl)
    {
        var bytes = JrlWriter.Write(jrl);
        return GffReader.Read(bytes);
    }

    private static JrlFile GffToJrl(GffFile gff)
    {
        var bytes = GffWriter.Write(gff);
        return JrlReader.Read(bytes);
    }

    [Fact]
    public void Replace_CategoryName()
    {
        var provider = new JrlSearchProvider();
        var gff = JrlToGff(CreateTestJrl());
        var matches = provider.Search(gff, new SearchCriteria { Pattern = "Louis Romain" });
        var nameMatch = matches.First(m => m.Field.Name == "Category Name");

        gff = JrlToGff(CreateTestJrl());
        var results = provider.Replace(gff, new[] { new ReplaceOperation { Match = nameMatch, ReplacementText = "Marcel Iceberg" } });

        Assert.All(results, r => Assert.True(r.Success));
        var jrl = GffToJrl(gff);
        Assert.Equal("The Marcel Iceberg Quest", jrl.Categories[0].Name.GetString(0));
    }

    [Fact]
    public void Replace_CategoryTag()
    {
        var provider = new JrlSearchProvider();
        var gff = JrlToGff(CreateTestJrl());
        var matches = provider.Search(gff, new SearchCriteria { Pattern = "q_louis" });
        var tagMatch = matches.First(m => m.Field.Name == "Category Tag");

        gff = JrlToGff(CreateTestJrl());
        var results = provider.Replace(gff, new[] { new ReplaceOperation { Match = tagMatch, ReplacementText = "q_marcel" } });

        Assert.All(results, r => Assert.True(r.Success));
        var jrl = GffToJrl(gff);
        Assert.Equal("q_marcel", jrl.Categories[0].Tag);
    }

    [Fact]
    public void Replace_EntryText()
    {
        var provider = new JrlSearchProvider();
        var gff = JrlToGff(CreateTestJrl());
        var matches = provider.Search(gff, new SearchCriteria { Pattern = "Louis Romain" });

        // Get the entry text match (entry ID 1)
        var entryMatch = matches.First(m =>
            m.Field.Name == "Entry Text" &&
            m.Location is JrlMatchLocation loc && loc.EntryId == 1);

        gff = JrlToGff(CreateTestJrl());
        var results = provider.Replace(gff, new[] { new ReplaceOperation { Match = entryMatch, ReplacementText = "Marcel Iceberg" } });

        Assert.All(results, r => Assert.True(r.Success));
        var jrl = GffToJrl(gff);
        Assert.Contains("Marcel Iceberg", jrl.Categories[0].Entries[0].Text.GetString(0));
    }

    [Fact]
    public void Replace_MultipleEntries()
    {
        var provider = new JrlSearchProvider();
        var gff = JrlToGff(CreateTestJrl());
        var matches = provider.Search(gff, new SearchCriteria { Pattern = "Louis Romain" });

        var entryMatches = matches.Where(m => m.Field.Name == "Entry Text").ToList();
        Assert.Equal(2, entryMatches.Count);

        var ops = entryMatches.Select(m => new ReplaceOperation
        {
            Match = m,
            ReplacementText = "Marcel Iceberg"
        }).ToList();

        gff = JrlToGff(CreateTestJrl());
        var results = provider.Replace(gff, ops);

        Assert.All(results, r => Assert.True(r.Success));
        var jrl = GffToJrl(gff);
        Assert.Contains("Marcel Iceberg", jrl.Categories[0].Entries[0].Text.GetString(0));
        Assert.Contains("Marcel Iceberg", jrl.Categories[0].Entries[1].Text.GetString(0));
    }

    [Fact]
    public void Replace_Comment()
    {
        var provider = new JrlSearchProvider();
        var gff = JrlToGff(CreateTestJrl());
        var matches = provider.Search(gff, new SearchCriteria { Pattern = "Louis" });
        var commentMatch = matches.First(m => m.Field.Name == "Comment");

        gff = JrlToGff(CreateTestJrl());
        var results = provider.Replace(gff, new[] { new ReplaceOperation { Match = commentMatch, ReplacementText = "Marcel" } });

        Assert.All(results, r => Assert.True(r.Success));
        var jrl = GffToJrl(gff);
        Assert.Equal("Main quest line for Marcel", jrl.Categories[0].Comment);
    }
}
