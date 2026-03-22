using Radoub.Formats.Gff;
using Radoub.Formats.Jrl;
using Radoub.Formats.Search;
using Xunit;

namespace Radoub.Formats.Tests.Search.Providers;

public class JrlSearchProviderTests
{
    private static JrlFile CreateTestJrl()
    {
        return new JrlFile
        {
            Categories = new List<JournalCategory>
            {
                new JournalCategory
                {
                    Tag = "q_main_plot",
                    Name = new CExoLocString { LocalizedStrings = new Dictionary<uint, string>
                    {
                        [0] = "The Lost Amulet of Waukeen"
                    }},
                    Comment = "Main quest line for Act I",
                    Entries = new List<JournalEntry>
                    {
                        new JournalEntry
                        {
                            ID = 1,
                            Text = new CExoLocString { LocalizedStrings = new Dictionary<uint, string>
                            {
                                [0] = "Louis Romain asked me to find the lost amulet."
                            }}
                        },
                        new JournalEntry
                        {
                            ID = 2,
                            Text = new CExoLocString { LocalizedStrings = new Dictionary<uint, string>
                            {
                                [0] = "I found the amulet in the ruins beneath the merchant district."
                            }},
                            End = true
                        }
                    }
                },
                new JournalCategory
                {
                    Tag = "q_side_escort",
                    Name = new CExoLocString { LocalizedStrings = new Dictionary<uint, string>
                    {
                        [0] = "Escort the Merchant"
                    }},
                    Comment = "Side quest escort mission",
                    Entries = new List<JournalEntry>
                    {
                        new JournalEntry
                        {
                            ID = 1,
                            Text = new CExoLocString { LocalizedStrings = new Dictionary<uint, string>
                            {
                                [0] = "A merchant needs an escort through the forest."
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

    [Fact]
    public void Search_FindsCategoryName()
    {
        var provider = new JrlSearchProvider();
        var gff = JrlToGff(CreateTestJrl());
        var criteria = new SearchCriteria { Pattern = "Lost Amulet" };

        var matches = provider.Search(gff, criteria);

        Assert.Contains(matches, m => m.Field.Name == "Category Name");
    }

    [Fact]
    public void Search_FindsCategoryTag()
    {
        var provider = new JrlSearchProvider();
        var gff = JrlToGff(CreateTestJrl());
        var criteria = new SearchCriteria { Pattern = "q_main_plot" };

        var matches = provider.Search(gff, criteria);

        Assert.Contains(matches, m => m.Field.Name == "Category Tag");
    }

    [Fact]
    public void Search_FindsEntryText()
    {
        var provider = new JrlSearchProvider();
        var gff = JrlToGff(CreateTestJrl());
        var criteria = new SearchCriteria { Pattern = "Louis Romain" };

        var matches = provider.Search(gff, criteria);

        Assert.Contains(matches, m => m.Field.Name == "Entry Text" && m.MatchedText == "Louis Romain");
    }

    [Fact]
    public void Search_FindsComment()
    {
        var provider = new JrlSearchProvider();
        var gff = JrlToGff(CreateTestJrl());
        var criteria = new SearchCriteria { Pattern = "Act I" };

        var matches = provider.Search(gff, criteria);

        Assert.Contains(matches, m => m.Field.Name == "Comment");
    }

    [Fact]
    public void Search_FindsAcrossMultipleCategories()
    {
        var provider = new JrlSearchProvider();
        var gff = JrlToGff(CreateTestJrl());
        var criteria = new SearchCriteria { Pattern = "merchant", CaseSensitive = false };

        var matches = provider.Search(gff, criteria);

        // "merchant district" in entry, "Escort the Merchant" in category name, "A merchant needs" in entry
        Assert.True(matches.Count >= 3);
    }

    [Fact]
    public void Search_Location_CategoryHasIndex()
    {
        var provider = new JrlSearchProvider();
        var gff = JrlToGff(CreateTestJrl());
        var criteria = new SearchCriteria { Pattern = "q_main_plot" };

        var matches = provider.Search(gff, criteria);

        var match = Assert.Single(matches);
        var loc = Assert.IsType<JrlMatchLocation>(match.Location);
        Assert.Equal(0, loc.CategoryIndex);
        Assert.Null(loc.EntryId);
    }

    [Fact]
    public void Search_Location_EntryHasEntryId()
    {
        var provider = new JrlSearchProvider();
        var gff = JrlToGff(CreateTestJrl());
        var criteria = new SearchCriteria { Pattern = "ruins beneath" };

        var matches = provider.Search(gff, criteria);

        var match = Assert.Single(matches);
        var loc = Assert.IsType<JrlMatchLocation>(match.Location);
        Assert.Equal(0, loc.CategoryIndex);
        Assert.Equal((uint)2, loc.EntryId);
    }

    [Fact]
    public void Search_Location_DisplayPath_Category()
    {
        var provider = new JrlSearchProvider();
        var gff = JrlToGff(CreateTestJrl());
        var criteria = new SearchCriteria { Pattern = "q_side_escort" };

        var matches = provider.Search(gff, criteria);

        var match = Assert.Single(matches);
        var loc = Assert.IsType<JrlMatchLocation>(match.Location);
        Assert.Contains("Category #1", loc.DisplayPath);
    }

    [Fact]
    public void Search_Location_DisplayPath_Entry()
    {
        var provider = new JrlSearchProvider();
        var gff = JrlToGff(CreateTestJrl());
        var criteria = new SearchCriteria { Pattern = "lost amulet", CaseSensitive = false };

        var matches = provider.Search(gff, criteria);

        // Should find in category name and entry text
        var entryMatch = matches.FirstOrDefault(m => m.Field.Name == "Entry Text");
        Assert.NotNull(entryMatch);
        var loc = Assert.IsType<JrlMatchLocation>(entryMatch.Location);
        Assert.Contains("Entry #1", loc.DisplayPath);
    }

    [Fact]
    public void Search_ContentCategoryFilter()
    {
        var provider = new JrlSearchProvider();
        var gff = JrlToGff(CreateTestJrl());
        var criteria = new SearchCriteria
        {
            Pattern = "amulet",
            CategoryFilter = new[] { SearchFieldCategory.Content },
            CaseSensitive = false
        };

        var matches = provider.Search(gff, criteria);

        Assert.All(matches, m => Assert.Equal(SearchFieldCategory.Content, m.Field.Category));
    }

    [Fact]
    public void Search_FieldNameFilter()
    {
        var provider = new JrlSearchProvider();
        var gff = JrlToGff(CreateTestJrl());
        var criteria = new SearchCriteria
        {
            Pattern = "q_",
            FieldFilter = new[] { "Category Tag" }
        };

        var matches = provider.Search(gff, criteria);

        Assert.Equal(2, matches.Count); // q_main_plot and q_side_escort
        Assert.All(matches, m => Assert.Equal("Category Tag", m.Field.Name));
    }

    [Fact]
    public void FileType_IsJrl()
    {
        var provider = new JrlSearchProvider();
        Assert.Equal(Radoub.Formats.Common.ResourceTypes.Jrl, provider.FileType);
    }

    [Fact]
    public void Extensions_ContainsJrl()
    {
        var provider = new JrlSearchProvider();
        Assert.Contains(".jrl", provider.Extensions);
    }
}
