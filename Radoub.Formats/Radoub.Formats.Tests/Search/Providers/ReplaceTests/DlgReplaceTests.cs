using Radoub.Formats.Dlg;
using Radoub.Formats.Gff;
using Radoub.Formats.Search;
using Xunit;

namespace Radoub.Formats.Tests.Search.Providers.ReplaceTests;

/// <summary>
/// Tests for DlgSearchProvider.Replace() — replaces in dialog entries, replies, and links.
/// </summary>
public class DlgReplaceTests
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
                    Comment = "Opening line for Louis",
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
                    Script = "gc_give_gold",
                    Sound = "vo_pc_thanks",
                    Quest = "q_main_plot",
                    EntriesList = new List<DlgLink>()
                }
            },
            StartingList = new List<DlgLink>
            {
                new DlgLink
                {
                    Index = 0,
                    Active = "gc_global_int"
                }
            }
        };
    }

    private static GffFile DlgToGff(DlgFile dlg)
    {
        var bytes = DlgWriter.Write(dlg);
        return GffReader.Read(bytes);
    }

    private static DlgFile GffToDlg(GffFile gff)
    {
        var bytes = GffWriter.Write(gff);
        return DlgReader.Read(bytes);
    }

    [Fact]
    public void Replace_EntryText()
    {
        var provider = new DlgSearchProvider();
        var gff = DlgToGff(CreateTestDlg());
        var criteria = new SearchCriteria { Pattern = "Louis Romain" };
        var matches = provider.Search(gff, criteria);

        var entryMatch = matches.First(m =>
            m.Field.Name == "Text" &&
            m.Location is DlgMatchLocation loc && loc.NodeType == DlgNodeType.Entry);

        var ops = new[] { new ReplaceOperation { Match = entryMatch, ReplacementText = "Marcel Iceberg" } };

        gff = DlgToGff(CreateTestDlg());
        var results = provider.Replace(gff, ops);

        Assert.All(results, r => Assert.True(r.Success));

        var dlg = GffToDlg(gff);
        Assert.Contains("Marcel Iceberg", dlg.Entries[0].Text.GetString(0));
        Assert.DoesNotContain("Louis Romain", dlg.Entries[0].Text.GetString(0));
    }

    [Fact]
    public void Replace_ReplyText()
    {
        var provider = new DlgSearchProvider();
        var gff = DlgToGff(CreateTestDlg());
        var criteria = new SearchCriteria { Pattern = "Louis Romain" };
        var matches = provider.Search(gff, criteria);

        var replyMatch = matches.First(m =>
            m.Field.Name == "Text" &&
            m.Location is DlgMatchLocation loc && loc.NodeType == DlgNodeType.Reply);

        var ops = new[] { new ReplaceOperation { Match = replyMatch, ReplacementText = "Marcel Iceberg" } };

        gff = DlgToGff(CreateTestDlg());
        var results = provider.Replace(gff, ops);

        Assert.All(results, r => Assert.True(r.Success));

        var dlg = GffToDlg(gff);
        Assert.Equal("Thank you, Marcel Iceberg.", dlg.Replies[0].Text.GetString(0));
    }

    [Fact]
    public void Replace_Speaker()
    {
        var provider = new DlgSearchProvider();
        var gff = DlgToGff(CreateTestDlg());
        var criteria = new SearchCriteria { Pattern = "LOUIS_ROMAIN" };
        var matches = provider.Search(gff, criteria);

        var speakerMatch = matches.First(m => m.Field.Name == "Speaker");
        var ops = new[] { new ReplaceOperation { Match = speakerMatch, ReplacementText = "MARCEL_ICEBERG" } };

        gff = DlgToGff(CreateTestDlg());
        var results = provider.Replace(gff, ops);

        Assert.All(results, r => Assert.True(r.Success));

        var dlg = GffToDlg(gff);
        Assert.Equal("MARCEL_ICEBERG", dlg.Entries[0].Speaker);
    }

    [Fact]
    public void Replace_ActionScript()
    {
        var provider = new DlgSearchProvider();
        var gff = DlgToGff(CreateTestDlg());
        var criteria = new SearchCriteria { Pattern = "gc_check_quest" };
        var matches = provider.Search(gff, criteria);

        var scriptMatch = matches.First(m => m.Field.Name == "Action Script");
        var ops = new[] { new ReplaceOperation { Match = scriptMatch, ReplacementText = "gc_new_quest" } };

        gff = DlgToGff(CreateTestDlg());
        var results = provider.Replace(gff, ops);

        Assert.All(results, r => Assert.True(r.Success));

        var dlg = GffToDlg(gff);
        Assert.Equal("gc_new_quest", dlg.Entries[0].Script);
    }

    [Fact]
    public void Replace_ConditionScript_OnLink()
    {
        var provider = new DlgSearchProvider();
        var gff = DlgToGff(CreateTestDlg());
        var criteria = new SearchCriteria { Pattern = "gc_is_male" };
        var matches = provider.Search(gff, criteria);

        var condMatch = matches.First(m => m.Field.Name == "Condition Script");
        var ops = new[] { new ReplaceOperation { Match = condMatch, ReplacementText = "gc_is_female" } };

        gff = DlgToGff(CreateTestDlg());
        var results = provider.Replace(gff, ops);

        Assert.All(results, r => Assert.True(r.Success));

        var dlg = GffToDlg(gff);
        Assert.Equal("gc_is_female", dlg.Entries[0].RepliesList[0].Active);
    }

    [Fact]
    public void Replace_EndConversationScript()
    {
        var provider = new DlgSearchProvider();
        var gff = DlgToGff(CreateTestDlg());
        var criteria = new SearchCriteria { Pattern = "gc_end_conv" };
        var matches = provider.Search(gff, criteria);

        var endMatch = matches.First(m => m.Field.Name == "End Conversation");
        var ops = new[] { new ReplaceOperation { Match = endMatch, ReplacementText = "gc_end_new" } };

        gff = DlgToGff(CreateTestDlg());
        var results = provider.Replace(gff, ops);

        Assert.All(results, r => Assert.True(r.Success));

        var dlg = GffToDlg(gff);
        Assert.Equal("gc_end_new", dlg.EndConversation);
    }

    [Fact]
    public void Replace_Sound_ResRef()
    {
        var provider = new DlgSearchProvider();
        var gff = DlgToGff(CreateTestDlg());
        var criteria = new SearchCriteria { Pattern = "vo_louis_01" };
        var matches = provider.Search(gff, criteria);

        var soundMatch = matches.First(m => m.Field.Name == "Sound");
        var ops = new[] { new ReplaceOperation { Match = soundMatch, ReplacementText = "vo_marcel_01" } };

        gff = DlgToGff(CreateTestDlg());
        var results = provider.Replace(gff, ops);

        Assert.All(results, r => Assert.True(r.Success));

        var dlg = GffToDlg(gff);
        Assert.Equal("vo_marcel_01", dlg.Entries[0].Sound);
    }

    [Fact]
    public void Replace_Quest_Tag()
    {
        var provider = new DlgSearchProvider();
        var gff = DlgToGff(CreateTestDlg());
        var criteria = new SearchCriteria { Pattern = "q_main_plot" };
        var matches = provider.Search(gff, criteria);

        // Replace only the entry quest (first match on Entry)
        var questMatch = matches.First(m =>
            m.Field.Name == "Quest" &&
            m.Location is DlgMatchLocation loc && loc.NodeType == DlgNodeType.Entry);

        var ops = new[] { new ReplaceOperation { Match = questMatch, ReplacementText = "q_side_plot" } };

        gff = DlgToGff(CreateTestDlg());
        var results = provider.Replace(gff, ops);

        Assert.All(results, r => Assert.True(r.Success));

        var dlg = GffToDlg(gff);
        Assert.Equal("q_side_plot", dlg.Entries[0].Quest);
    }

    [Fact]
    public void Replace_Comment()
    {
        var provider = new DlgSearchProvider();
        var gff = DlgToGff(CreateTestDlg());
        var criteria = new SearchCriteria { Pattern = "Louis" };
        var matches = provider.Search(gff, criteria);

        var commentMatch = matches.First(m => m.Field.Name == "Comment");
        var ops = new[] { new ReplaceOperation { Match = commentMatch, ReplacementText = "Marcel" } };

        gff = DlgToGff(CreateTestDlg());
        var results = provider.Replace(gff, ops);

        Assert.All(results, r => Assert.True(r.Success));

        var dlg = GffToDlg(gff);
        Assert.Equal("Opening line for Marcel", dlg.Entries[0].Comment);
    }

    [Fact]
    public void Replace_StartingListConditionScript()
    {
        var provider = new DlgSearchProvider();
        var gff = DlgToGff(CreateTestDlg());
        var criteria = new SearchCriteria { Pattern = "gc_global_int" };
        var matches = provider.Search(gff, criteria);

        var startMatch = matches.First(m =>
            m.Field.Name == "Condition Script" &&
            m.Location is DlgMatchLocation loc && loc.NodeType == DlgNodeType.StartingLink);

        var ops = new[] { new ReplaceOperation { Match = startMatch, ReplacementText = "gc_local_int" } };

        gff = DlgToGff(CreateTestDlg());
        var results = provider.Replace(gff, ops);

        Assert.All(results, r => Assert.True(r.Success));

        var dlg = GffToDlg(gff);
        Assert.Equal("gc_local_int", dlg.StartingList[0].Active);
    }

    [Fact]
    public void Replace_MultipleMatches_AcrossEntryAndReply()
    {
        var provider = new DlgSearchProvider();
        var gff = DlgToGff(CreateTestDlg());
        var criteria = new SearchCriteria { Pattern = "Louis Romain" };
        var matches = provider.Search(gff, criteria);

        // Should have matches in both entry and reply text
        var textMatches = matches.Where(m => m.Field.Name == "Text").ToList();
        Assert.Equal(2, textMatches.Count);

        var ops = textMatches.Select(m => new ReplaceOperation
        {
            Match = m,
            ReplacementText = "Marcel Iceberg"
        }).ToList();

        gff = DlgToGff(CreateTestDlg());
        var results = provider.Replace(gff, ops);

        Assert.All(results, r => Assert.True(r.Success));

        var dlg = GffToDlg(gff);
        Assert.Contains("Marcel Iceberg", dlg.Entries[0].Text.GetString(0));
        Assert.Contains("Marcel Iceberg", dlg.Replies[0].Text.GetString(0));
    }

    [Fact]
    public void Replace_EmptyOperations_ReturnsEmpty()
    {
        var gff = DlgToGff(CreateTestDlg());
        var provider = new DlgSearchProvider();

        var results = provider.Replace(gff, Array.Empty<ReplaceOperation>());

        Assert.Empty(results);
    }
}
