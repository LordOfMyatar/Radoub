using Radoub.Formats.Dlg;
using Radoub.Formats.Gff;
using Radoub.Formats.Search;
using Radoub.UI.Services.Search;
using Xunit;

namespace Radoub.UI.Tests;

public class FileSearchServiceTests : IDisposable
{
    private readonly string _tempDir;

    public FileSearchServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"FileSearchServiceTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private string CreateTestDlgFile()
    {
        var dlg = new DlgFile
        {
            Entries = new List<DlgEntry>
            {
                new DlgEntry
                {
                    Speaker = "NPC_GUARD",
                    Text = new CExoLocString { LocalizedStrings = new Dictionary<uint, string>
                    {
                        [0] = "Halt! Who goes there?"
                    }},
                    Script = "",
                    ActionParams = new List<DlgParam>(),
                    RepliesList = new List<DlgLink> { new DlgLink { Index = 0 } }
                },
                new DlgEntry
                {
                    Speaker = "NPC_GUARD",
                    Text = new CExoLocString { LocalizedStrings = new Dictionary<uint, string>
                    {
                        [0] = "Very well, you may pass."
                    }},
                    Script = "gc_open_gate",
                    ActionParams = new List<DlgParam>(),
                    RepliesList = new List<DlgLink>()
                }
            },
            Replies = new List<DlgReply>
            {
                new DlgReply
                {
                    Text = new CExoLocString { LocalizedStrings = new Dictionary<uint, string>
                    {
                        [0] = "I am a friend."
                    }},
                    Script = "",
                    ActionParams = new List<DlgParam>(),
                    EntriesList = new List<DlgLink> { new DlgLink { Index = 1 } }
                }
            },
            StartingList = new List<DlgLink> { new DlgLink { Index = 0 } }
        };

        var bytes = DlgWriter.Write(dlg);
        var filePath = Path.Combine(_tempDir, "test.dlg");
        File.WriteAllBytes(filePath, bytes);
        return filePath;
    }

    [Fact]
    public void Search_FindsMatchingText()
    {
        var filePath = CreateTestDlgFile();
        var service = new FileSearchService(new DlgSearchProvider());

        var count = service.Search(filePath, new SearchCriteria { Pattern = "Halt" });

        Assert.Equal(1, count);
        Assert.Equal(1, service.MatchCount);
        Assert.NotNull(service.CurrentMatch);
        Assert.Contains("Halt", service.CurrentMatch!.MatchedText);
    }

    [Fact]
    public void Search_FindsMultipleMatches()
    {
        var filePath = CreateTestDlgFile();
        var service = new FileSearchService(new DlgSearchProvider());

        var count = service.Search(filePath, new SearchCriteria { Pattern = "NPC_GUARD" });

        Assert.Equal(2, count);
    }

    [Fact]
    public void Search_CaseInsensitiveByDefault()
    {
        var filePath = CreateTestDlgFile();
        var service = new FileSearchService(new DlgSearchProvider());

        var count = service.Search(filePath, new SearchCriteria { Pattern = "halt" });

        Assert.Equal(1, count);
    }

    [Fact]
    public void Search_CaseSensitiveRespectsFlag()
    {
        var filePath = CreateTestDlgFile();
        var service = new FileSearchService(new DlgSearchProvider());

        var count = service.Search(filePath, new SearchCriteria
        {
            Pattern = "halt",
            CaseSensitive = true
        });

        Assert.Equal(0, count);
    }

    [Fact]
    public void Search_ReturnsZeroForNoMatches()
    {
        var filePath = CreateTestDlgFile();
        var service = new FileSearchService(new DlgSearchProvider());

        var count = service.Search(filePath, new SearchCriteria { Pattern = "NONEXISTENT_TEXT" });

        Assert.Equal(0, count);
        Assert.Equal(-1, service.CurrentIndex);
        Assert.Null(service.CurrentMatch);
    }

    [Fact]
    public void Search_ReturnsZeroForNonexistentFile()
    {
        var service = new FileSearchService(new DlgSearchProvider());

        var count = service.Search("/nonexistent/path.dlg", new SearchCriteria { Pattern = "test" });

        Assert.Equal(0, count);
    }

    [Fact]
    public void NextMatch_CyclesThroughMatches()
    {
        var filePath = CreateTestDlgFile();
        var service = new FileSearchService(new DlgSearchProvider());
        service.Search(filePath, new SearchCriteria { Pattern = "NPC_GUARD" });

        Assert.Equal(0, service.CurrentIndex);

        var match1 = service.NextMatch();
        Assert.Equal(1, service.CurrentIndex);

        var match2 = service.NextMatch();
        Assert.Equal(0, service.CurrentIndex); // Wraps around
    }

    [Fact]
    public void PreviousMatch_CyclesBackward()
    {
        var filePath = CreateTestDlgFile();
        var service = new FileSearchService(new DlgSearchProvider());
        service.Search(filePath, new SearchCriteria { Pattern = "NPC_GUARD" });

        Assert.Equal(0, service.CurrentIndex);

        var match = service.PreviousMatch();
        Assert.Equal(1, service.CurrentIndex); // Wraps to last
    }

    [Fact]
    public void Clear_ResetsState()
    {
        var filePath = CreateTestDlgFile();
        var service = new FileSearchService(new DlgSearchProvider());
        service.Search(filePath, new SearchCriteria { Pattern = "Halt" });

        Assert.Equal(1, service.MatchCount);

        service.Clear();

        Assert.Equal(0, service.MatchCount);
        Assert.Equal(-1, service.CurrentIndex);
        Assert.Null(service.CurrentMatch);
    }

    [Fact]
    public void ReplaceCurrent_ModifiesFileOnDisk()
    {
        var filePath = CreateTestDlgFile();
        var service = new FileSearchService(new DlgSearchProvider());
        var criteria = new SearchCriteria { Pattern = "Halt" };
        service.Search(filePath, criteria);

        var result = service.ReplaceCurrent(filePath, "Stop", criteria);

        Assert.NotNull(result);
        Assert.True(result!.Success);

        // Verify the file was modified — search for the replacement text
        var newService = new FileSearchService(new DlgSearchProvider());
        var newCount = newService.Search(filePath, new SearchCriteria { Pattern = "Stop" });
        Assert.True(newCount >= 1);

        // Original text should be gone
        var oldCount = newService.Search(filePath, new SearchCriteria { Pattern = "Halt" });
        Assert.Equal(0, oldCount);
    }

    [Fact]
    public void ReplaceAll_ReplacesAllOccurrences()
    {
        var filePath = CreateTestDlgFile();
        var service = new FileSearchService(new DlgSearchProvider());
        var criteria = new SearchCriteria { Pattern = "NPC_GUARD" };
        service.Search(filePath, criteria);

        Assert.Equal(2, service.MatchCount);

        var count = service.ReplaceAll(filePath, "NPC_SOLDIER", criteria);

        Assert.Equal(2, count);

        // Verify replacements
        var verifyService = new FileSearchService(new DlgSearchProvider());
        Assert.Equal(2, verifyService.Search(filePath, new SearchCriteria { Pattern = "NPC_SOLDIER" }));
        Assert.Equal(0, verifyService.Search(filePath, new SearchCriteria { Pattern = "NPC_GUARD" }));
    }
}
