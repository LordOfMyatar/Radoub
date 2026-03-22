using Radoub.Formats.Common;
using Radoub.Formats.Dlg;
using Radoub.Formats.Gff;
using Radoub.Formats.Search;
using Radoub.UI.Services.Search;
using Xunit;

namespace Radoub.UI.Tests.Services.Search;

public class ModuleSearchServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ModuleSearchService _service;

    public ModuleSearchServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "RadoubTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _service = new ModuleSearchService();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    #region Test Data Helpers

    private string WriteDlgFile(string name, string entryText, string speaker = "")
    {
        var dlg = new DlgFile
        {
            Entries = new List<DlgEntry>
            {
                new DlgEntry
                {
                    Speaker = speaker,
                    Text = new CExoLocString
                    {
                        LocalizedStrings = new Dictionary<uint, string> { [0] = entryText }
                    },
                    RepliesList = new List<DlgLink>()
                }
            },
            Replies = new List<DlgReply>(),
            StartingList = new List<DlgLink>
            {
                new DlgLink { Index = 0 }
            }
        };

        var bytes = DlgWriter.Write(dlg);
        var filePath = Path.Combine(_tempDir, name);
        File.WriteAllBytes(filePath, bytes);
        return filePath;
    }

    private string WriteEmptyFile(string name)
    {
        var filePath = Path.Combine(_tempDir, name);
        File.WriteAllBytes(filePath, new byte[] { 0, 0, 0, 0 });
        return filePath;
    }

    #endregion

    #region ScanModuleAsync — Basic

    [Fact]
    public async Task ScanModule_FindsMatchesAcrossMultipleFiles()
    {
        WriteDlgFile("merchant.dlg", "I am Louis Romain, merchant.", "LOUIS");
        WriteDlgFile("tavern.dlg", "Have you seen Louis Romain?");
        WriteDlgFile("unrelated.dlg", "The weather is fine today.");

        var criteria = new SearchCriteria { Pattern = "Louis Romain" };
        var results = await _service.ScanModuleAsync(_tempDir, criteria);

        Assert.Equal(2, results.FilesWithMatches);
        Assert.Equal(2, results.TotalMatches);
        Assert.Equal(3, results.TotalFilesScanned);
        Assert.False(results.WasCancelled);
    }

    [Fact]
    public async Task ScanModule_EmptyDirectory_ReturnsEmptyResults()
    {
        var criteria = new SearchCriteria { Pattern = "test" };
        var results = await _service.ScanModuleAsync(_tempDir, criteria);

        Assert.Equal(0, results.TotalFilesScanned);
        Assert.Equal(0, results.TotalMatches);
        Assert.Empty(results.Files);
    }

    [Fact]
    public async Task ScanModule_NoMatches_ReturnsEmptyFiles()
    {
        WriteDlgFile("test.dlg", "Nothing to see here.");

        var criteria = new SearchCriteria { Pattern = "NONEXISTENT_STRING" };
        var results = await _service.ScanModuleAsync(_tempDir, criteria);

        Assert.Equal(1, results.TotalFilesScanned);
        Assert.Equal(0, results.TotalMatches);
        Assert.Equal(0, results.FilesWithMatches);
    }

    #endregion

    #region ScanModuleAsync — File Type Filtering

    [Fact]
    public async Task ScanModule_FileTypeFilter_OnlySearchesFilteredTypes()
    {
        WriteDlgFile("merchant.dlg", "Louis Romain sells things.");

        // Create a non-DLG GFF file (invalid but with .utc extension)
        WriteEmptyFile("creature.utc");

        var criteria = new SearchCriteria
        {
            Pattern = "Louis",
            FileTypeFilter = new[] { ResourceTypes.Dlg }
        };
        var results = await _service.ScanModuleAsync(_tempDir, criteria);

        // Only the DLG file should be scanned
        Assert.Equal(1, results.TotalFilesScanned);
        Assert.Equal(1, results.TotalMatches);
    }

    [Fact]
    public async Task ScanModule_IgnoresNonGffFiles()
    {
        WriteDlgFile("test.dlg", "Searchable text.");
        File.WriteAllText(Path.Combine(_tempDir, "readme.txt"), "Searchable text.");
        File.WriteAllText(Path.Combine(_tempDir, "script.nss"), "Searchable text.");

        var criteria = new SearchCriteria { Pattern = "Searchable" };
        var results = await _service.ScanModuleAsync(_tempDir, criteria);

        // Only the DLG file should be scanned (txt and nss are not GFF)
        Assert.Equal(1, results.TotalFilesScanned);
    }

    #endregion

    #region ScanModuleAsync — Progress and Cancellation

    [Fact]
    public async Task ScanModule_ReportsProgress()
    {
        WriteDlgFile("first.dlg", "Hello world.");
        WriteDlgFile("second.dlg", "Goodbye world.");

        var progressReports = new List<ScanProgress>();
        var progress = new Progress<ScanProgress>(p => progressReports.Add(p));

        var criteria = new SearchCriteria { Pattern = "world" };
        await _service.ScanModuleAsync(_tempDir, criteria, progress);

        // Allow async progress callbacks to complete
        await Task.Delay(100);

        Assert.True(progressReports.Count >= 2, "Expected at least discovery + search progress reports");
        Assert.Contains(progressReports, p => p.Phase == "Discovering files");
        Assert.Contains(progressReports, p => p.Phase == "Complete");
    }

    [Fact]
    public async Task ScanModule_Cancellation_StopsEarly()
    {
        // Create enough files to make cancellation observable
        for (int i = 0; i < 10; i++)
            WriteDlgFile($"file{i:D2}.dlg", $"Text content {i}.");

        var cts = new CancellationTokenSource();

        var progressReports = new List<ScanProgress>();
        var progress = new Progress<ScanProgress>(p =>
        {
            progressReports.Add(p);
            // Cancel after first file is searched
            if (p.FilesScanned >= 1 && p.Phase == "Searching")
                cts.Cancel();
        });

        var criteria = new SearchCriteria { Pattern = "Text content" };

        var ex = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _service.ScanModuleAsync(_tempDir, criteria, progress, cts.Token));
        Assert.True(ex is OperationCanceledException);
    }

    #endregion

    #region ScanModuleAsync — Error Handling

    [Fact]
    public async Task ScanModule_CorruptFile_ReportsParseError()
    {
        WriteDlgFile("good.dlg", "Findable text here.");
        WriteEmptyFile("corrupt.dlg");

        var criteria = new SearchCriteria { Pattern = "Findable" };
        var results = await _service.ScanModuleAsync(_tempDir, criteria);

        // Good file should still be found
        Assert.Equal(1, results.FilesWithMatches);
        // Corrupt file should be reported as parse error
        Assert.Equal(1, results.ParseErrors);
        Assert.Equal(2, results.TotalFilesScanned);
    }

    [Fact]
    public async Task ScanModule_InvalidDirectory_Throws()
    {
        var criteria = new SearchCriteria { Pattern = "test" };
        await Assert.ThrowsAsync<DirectoryNotFoundException>(
            () => _service.ScanModuleAsync(Path.Combine(_tempDir, "nonexistent"), criteria));
    }

    [Fact]
    public async Task ScanModule_InvalidPattern_Throws()
    {
        var criteria = new SearchCriteria { Pattern = "[invalid", IsRegex = true };
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.ScanModuleAsync(_tempDir, criteria));
    }

    #endregion

    #region SearchSingleFile

    [Fact]
    public void SearchSingleFile_FindsMatches()
    {
        var filePath = WriteDlgFile("test.dlg", "Louis Romain is here.");
        var criteria = new SearchCriteria { Pattern = "Louis Romain" };

        var result = _service.SearchSingleFile(filePath, criteria);

        Assert.Equal(1, result.MatchCount);
        Assert.Equal("dlg", result.Extension);
        Assert.Equal(ResourceTypes.Dlg, result.ResourceType);
        Assert.Equal("parley", result.ToolId);
        Assert.False(result.HadParseError);
    }

    [Fact]
    public void SearchSingleFile_NoMatches_ReturnsEmptyResult()
    {
        var filePath = WriteDlgFile("test.dlg", "Nothing interesting.");
        var criteria = new SearchCriteria { Pattern = "NONEXISTENT" };

        var result = _service.SearchSingleFile(filePath, criteria);

        Assert.Equal(0, result.MatchCount);
        Assert.False(result.HadParseError);
    }

    [Fact]
    public void SearchSingleFile_FileNotFound_Throws()
    {
        var criteria = new SearchCriteria { Pattern = "test" };
        Assert.Throws<FileNotFoundException>(
            () => _service.SearchSingleFile(Path.Combine(_tempDir, "nope.dlg"), criteria));
    }

    #endregion

    #region FileSearchResult Model

    [Fact]
    public void FileSearchResult_FileName_ExtractsFromPath()
    {
        var result = new FileSearchResult
        {
            FilePath = @"C:\modules\test\merchant.dlg",
            ResourceType = ResourceTypes.Dlg,
            Matches = Array.Empty<SearchMatch>()
        };

        Assert.Equal("merchant.dlg", result.FileName);
        Assert.Equal("dlg", result.Extension);
    }

    #endregion

    #region ModuleSearchResults Model

    [Fact]
    public void ModuleSearchResults_GroupByExtension_GroupsCorrectly()
    {
        var match = CreateDummyMatch();

        var results = new ModuleSearchResults
        {
            Files = new List<FileSearchResult>
            {
                new FileSearchResult
                {
                    FilePath = "a.dlg", ResourceType = ResourceTypes.Dlg,
                    ToolId = "parley", Matches = new[] { match }
                },
                new FileSearchResult
                {
                    FilePath = "b.dlg", ResourceType = ResourceTypes.Dlg,
                    ToolId = "parley", Matches = new[] { match }
                },
                new FileSearchResult
                {
                    FilePath = "c.utc", ResourceType = ResourceTypes.Utc,
                    ToolId = "quartermaster", Matches = new[] { match }
                }
            },
            TotalFilesScanned = 5
        };

        var groups = results.GroupByExtension();
        Assert.Equal(2, groups.Count);
        Assert.Equal(2, groups["dlg"].Count);
        Assert.Single(groups["utc"]);
    }

    [Fact]
    public void ModuleSearchResults_GroupByTool_GroupsCorrectly()
    {
        var match = CreateDummyMatch();

        var results = new ModuleSearchResults
        {
            Files = new List<FileSearchResult>
            {
                new FileSearchResult
                {
                    FilePath = "a.dlg", ResourceType = ResourceTypes.Dlg,
                    ToolId = "parley", Matches = new[] { match }
                },
                new FileSearchResult
                {
                    FilePath = "b.utc", ResourceType = ResourceTypes.Utc,
                    ToolId = "quartermaster", Matches = new[] { match }
                }
            },
            TotalFilesScanned = 2
        };

        var groups = results.GroupByTool();
        Assert.Equal(2, groups.Count);
        Assert.True(groups.ContainsKey("parley"));
        Assert.True(groups.ContainsKey("quartermaster"));
    }

    #endregion

    #region ToolId Mapping

    [Theory]
    [InlineData(ResourceTypes.Dlg, "parley")]
    [InlineData(ResourceTypes.Utc, "quartermaster")]
    [InlineData(ResourceTypes.Bic, "quartermaster")]
    [InlineData(ResourceTypes.Uti, "relique")]
    [InlineData(ResourceTypes.Utm, "fence")]
    [InlineData(ResourceTypes.Jrl, "manifest")]
    [InlineData(ResourceTypes.Are, "")]
    public void GetToolId_ReturnsCorrectMapping(ushort resourceType, string expectedToolId)
    {
        Assert.Equal(expectedToolId, ModuleSearchService.GetToolId(resourceType));
    }

    #endregion

    #region Duration

    [Fact]
    public async Task ScanModule_RecordsDuration()
    {
        WriteDlgFile("test.dlg", "Some text.");
        var criteria = new SearchCriteria { Pattern = "text" };

        var results = await _service.ScanModuleAsync(_tempDir, criteria);

        Assert.True(results.Duration > TimeSpan.Zero);
    }

    #endregion

    #region Helpers

    private static SearchMatch CreateDummyMatch()
    {
        return new SearchMatch
        {
            Field = new FieldDefinition
            {
                Name = "Text",
                GffPath = "Text",
                FieldType = SearchFieldType.LocString,
                Category = SearchFieldCategory.Content
            },
            MatchedText = "test",
            FullFieldValue = "test value"
        };
    }

    #endregion
}
