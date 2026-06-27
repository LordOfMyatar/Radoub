using Radoub.Formats.Dlg;
using Radoub.Formats.Gff;
using Radoub.Formats.Search;
using Xunit;

namespace Radoub.Formats.Tests.Search.Providers;

public class DlgSearchProviderTests
{
    private static DlgFile CreateTestDlg()
    {
        return new DlgFile
        {
            EndConversation = "gc_end_conv",
            EndConverAbort = "gc_abort",
            Entries = new List<DlgEntry>
            {
                new DlgEntry
                {
                    Speaker = "LOUIS_ROMAIN",
                    Text = new CExoLocString { LocalizedStrings = new Dictionary<uint, string>
                    {
                        [0] = "I am Louis Romain, merchant of the western coast."
                    }},
                    Script = "gc_check_quest",
                    ActionParams = new List<DlgParam>
                    {
                        new DlgParam { Key = "sQuestTag", Value = "q_main_plot" }
                    },
                    Comment = "Opening line for merchant",
                    Sound = "vo_louis_01",
                    Quest = "q_main_plot",
                    RepliesList = new List<DlgLink>
                    {
                        new DlgLink
                        {
                            Index = 0,
                            Active = "gc_is_male",
                            ConditionParams = new List<DlgParam>
                            {
                                new DlgParam { Key = "nGender", Value = "1" }
                            }
                        }
                    }
                }
            },
            Replies = new List<DlgReply>
            {
                new DlgReply
                {
                    Text = new CExoLocString { LocalizedStrings = new Dictionary<uint, string>
                    {
                        [0] = "Thank you, Louis Romain."
                    }},
                    Script = "",
                    ActionParams = new List<DlgParam>(),
                    EntriesList = new List<DlgLink>()
                }
            },
            StartingList = new List<DlgLink>
            {
                new DlgLink
                {
                    Index = 0,
                    Active = "gc_global_int",
                    ConditionParams = new List<DlgParam>
                    {
                        new DlgParam { Key = "sVariable", Value = "MET_LOUIS" }
                    }
                }
            }
        };
    }

    /// <summary>
    /// Convert DlgFile to GffFile via DlgWriter then parse back.
    /// This ensures we test against the real GFF structure.
    /// </summary>
    private static GffFile DlgToGff(DlgFile dlg)
    {
        var bytes = DlgWriter.Write(dlg);
        return GffReader.Read(bytes);
    }

    [Fact]
    public void Search_FindsEntryText()
    {
        var provider = new DlgSearchProvider();
        var gff = DlgToGff(CreateTestDlg());
        var criteria = new SearchCriteria { Pattern = "Louis Romain" };

        var matches = provider.Search(gff, criteria);

        Assert.Contains(matches, m =>
            m.Field.Name == "Text" &&
            m.MatchedText == "Louis Romain" &&
            m.Location is DlgMatchLocation loc && loc.NodeType == DlgNodeType.Entry);
    }

    [Fact]
    public void Search_FindsReplyText()
    {
        var provider = new DlgSearchProvider();
        var gff = DlgToGff(CreateTestDlg());
        var criteria = new SearchCriteria { Pattern = "Louis Romain" };

        var matches = provider.Search(gff, criteria);

        Assert.Contains(matches, m =>
            m.Location is DlgMatchLocation loc && loc.NodeType == DlgNodeType.Reply);
    }

    [Theory]
    [InlineData("LOUIS_ROMAIN", "Speaker")]
    [InlineData("gc_check_quest", "Action Script")]
    [InlineData("q_main_plot", "Action Params")]
    [InlineData("gc_end_conv", "End Conversation")]
    [InlineData("gc_abort", "End Conversation Abort")]
    [InlineData("vo_louis", "Sound")]
    [InlineData("q_main_plot", "Quest")]
    [InlineData("Opening line", "Comment")]
    public void Search_FindsFieldByPattern(string pattern, string expectedFieldName)
    {
        var provider = new DlgSearchProvider();
        var gff = DlgToGff(CreateTestDlg());
        var criteria = new SearchCriteria { Pattern = pattern };

        var matches = provider.Search(gff, criteria);

        Assert.Contains(matches, m => m.Field.Name == expectedFieldName);
    }

    [Fact]
    public void Search_FindsConditionScriptOnLink()
    {
        var provider = new DlgSearchProvider();
        var gff = DlgToGff(CreateTestDlg());
        var criteria = new SearchCriteria { Pattern = "gc_is_male" };

        var matches = provider.Search(gff, criteria);

        Assert.Contains(matches, m =>
            m.Field.Name == "Condition Script" &&
            m.Location is DlgMatchLocation loc && loc.IsOnLink);
    }

    [Fact]
    public void Search_FindsStartingListCondition()
    {
        var provider = new DlgSearchProvider();
        var gff = DlgToGff(CreateTestDlg());
        var criteria = new SearchCriteria { Pattern = "gc_global_int" };

        var matches = provider.Search(gff, criteria);

        Assert.Contains(matches, m =>
            m.Location is DlgMatchLocation loc && loc.NodeType == DlgNodeType.StartingLink);
    }

    [Fact]
    public void Search_FindsConditionParamsOnStartingList()
    {
        var provider = new DlgSearchProvider();
        var gff = DlgToGff(CreateTestDlg());
        var criteria = new SearchCriteria { Pattern = "MET_LOUIS" };

        var matches = provider.Search(gff, criteria);

        Assert.Contains(matches, m =>
            m.Field.Name == "Condition Params" &&
            m.Location is DlgMatchLocation loc && loc.NodeType == DlgNodeType.StartingLink);
    }

    [Fact]
    public void Search_FieldTypeFilter_ScriptsOnly()
    {
        var provider = new DlgSearchProvider();
        var gff = DlgToGff(CreateTestDlg());
        var criteria = new SearchCriteria
        {
            Pattern = "gc_",
            FieldTypeFilter = new[] { SearchFieldType.Script }
        };

        var matches = provider.Search(gff, criteria);

        Assert.All(matches, m => Assert.Equal(SearchFieldType.Script, m.Field.FieldType));
        Assert.True(matches.Count >= 3); // gc_check_quest, gc_is_male, gc_global_int, gc_end_conv, gc_abort
    }

    [Fact]
    public void Search_FieldNameFilter_SpeakerOnly()
    {
        var provider = new DlgSearchProvider();
        var gff = DlgToGff(CreateTestDlg());
        var criteria = new SearchCriteria
        {
            Pattern = "LOUIS",
            FieldFilter = new[] { "Speaker" }
        };

        var matches = provider.Search(gff, criteria);

        Assert.All(matches, m => Assert.Equal("Speaker", m.Field.Name));
    }

    [Fact]
    public void Search_EntryLocation_HasCorrectNodeIndex()
    {
        var provider = new DlgSearchProvider();
        var gff = DlgToGff(CreateTestDlg());
        var criteria = new SearchCriteria { Pattern = "western coast" };

        var matches = provider.Search(gff, criteria);

        var entryMatch = Assert.Single(matches);
        var loc = Assert.IsType<DlgMatchLocation>(entryMatch.Location);
        Assert.Equal(DlgNodeType.Entry, loc.NodeType);
        Assert.Equal(0, loc.NodeIndex);
        Assert.False(loc.IsOnLink);
    }

    [Fact]
    public void Search_LinkLocation_HasIsOnLinkFlag()
    {
        var provider = new DlgSearchProvider();
        var gff = DlgToGff(CreateTestDlg());
        var criteria = new SearchCriteria { Pattern = "gc_is_male" };

        var matches = provider.Search(gff, criteria);

        var match = Assert.Single(matches);
        var loc = Assert.IsType<DlgMatchLocation>(match.Location);
        Assert.True(loc.IsOnLink);
        Assert.Equal(0, loc.LinkIndex);
    }
}
