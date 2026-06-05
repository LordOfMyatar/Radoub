using Radoub.Formats.Common;
using Radoub.Formats.Dlg;
using Radoub.Formats.Gff;
using Radoub.Formats.Search;
using Radoub.Formats.Utc;
using Radoub.UI.Services.Search;
using Xunit;

namespace Radoub.UI.Tests.Services.Search;

public class BatchReplaceServiceTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _backupDir;
    private readonly BatchReplaceService _service;

    public BatchReplaceServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"radoub_batch_test_{Guid.NewGuid():N}");
        _backupDir = Path.Combine(_testDir, "Backups");
        Directory.CreateDirectory(_testDir);

        var backupService = new BackupService(_backupDir);
        _service = new BatchReplaceService(backupService);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    private string WriteUtcFile(string name, UtcFile utc)
    {
        var dir = Path.Combine(_testDir, "module");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, name);
        var bytes = UtcWriter.Write(utc);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private string WriteDlgFile(string name, DlgFile dlg)
    {
        var dir = Path.Combine(_testDir, "module");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, name);
        var bytes = DlgWriter.Write(dlg);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private static UtcFile CreateTestUtc(string firstName = "Louis Romain")
    {
        return new UtcFile
        {
            FirstName = new CExoLocString { LocalizedStrings = new Dictionary<uint, string>
            {
                [0] = firstName
            }},
            Tag = "LOUIS_ROMAIN"
        };
    }

    // --- Preview ---

    [Fact]
    public void PreviewReplace_GeneratesPendingChanges()
    {
        var utcPath = WriteUtcFile("louis.utc", CreateTestUtc());

        // Search the file
        var searchService = new ModuleSearchService();
        var fileResult = searchService.SearchSingleFile(utcPath, new SearchCriteria { Pattern = "Louis" });

        var preview = _service.PreviewReplace(
            new[] { fileResult },
            "Marcel");

        Assert.NotEmpty(preview.Changes);
        Assert.All(preview.Changes, c =>
        {
            Assert.Equal("Marcel", c.ReplacementText);
            Assert.True(c.IsSelected);
        });
    }

    [Fact]
    public void PreviewReplace_NssContentMatch_SkippedAndCounted()
    {
        // .nss content is plain-text script — Marlinspike doesn't edit it. The match
        // is found by search but must be dropped from the preview AND counted so the
        // UI can tell the user to use a code editor (#2341). Without the count, the
        // preview is silently empty and reads as broken.
        var dir = Path.Combine(_testDir, "module");
        Directory.CreateDirectory(dir);
        var nssPath = Path.Combine(dir, "_list.nss");
        File.WriteAllText(nssPath, "void main() { ExecuteScript(\"louis\"); }");

        var searchService = new ModuleSearchService();
        var fileResult = searchService.SearchSingleFile(nssPath, new SearchCriteria { Pattern = "louis" });

        var preview = _service.PreviewReplace(new[] { fileResult }, "lewie");

        Assert.Empty(preview.Changes);              // nothing replaceable
        Assert.True(preview.SkippedNssContentMatches > 0); // but the user is told why
    }

    [Fact]
    public void PreviewReplace_GroupsByFile()
    {
        var utcPath = WriteUtcFile("louis.utc", CreateTestUtc());

        var searchService = new ModuleSearchService();
        var fileResult = searchService.SearchSingleFile(utcPath, new SearchCriteria { Pattern = "Louis" });

        var preview = _service.PreviewReplace(new[] { fileResult }, "Marcel");

        Assert.Single(preview.FileGroups);
        Assert.Equal(utcPath, preview.FileGroups[0].FilePath);
    }

    [Fact]
    public void PreviewReplace_PendingChange_HasBeforeAfter()
    {
        var utcPath = WriteUtcFile("louis.utc", CreateTestUtc());

        var searchService = new ModuleSearchService();
        var fileResult = searchService.SearchSingleFile(utcPath, new SearchCriteria { Pattern = "Louis Romain" });

        var preview = _service.PreviewReplace(new[] { fileResult }, "Marcel Iceberg");

        var change = preview.Changes.First(c => c.Match.Field.Name == "First Name");
        Assert.Equal("Louis Romain", change.Match.FullFieldValue);
        Assert.Equal("Marcel Iceberg", change.ReplacementText);
    }

    // --- Execute ---

    [Fact]
    public async Task ExecuteReplace_ModifiesFileOnDisk()
    {
        var utcPath = WriteUtcFile("louis.utc", CreateTestUtc());

        var searchService = new ModuleSearchService();
        var fileResult = searchService.SearchSingleFile(utcPath, new SearchCriteria { Pattern = "Louis Romain" });

        var preview = _service.PreviewReplace(new[] { fileResult }, "Marcel Iceberg");

        var result = await _service.ExecuteReplaceAsync(preview, "TestModule");

        Assert.True(result.Success);
        Assert.True(result.FilesModified > 0);

        // Verify file was actually modified
        var bytes = await File.ReadAllBytesAsync(utcPath);
        var gff = GffReader.Read(bytes);
        var modifiedBytes = GffWriter.Write(gff);
        var utc = UtcReader.Read(modifiedBytes);
        Assert.Equal("Marcel Iceberg", utc.FirstName.GetString(0));
    }

    [Fact]
    public async Task ExecuteReplace_CreatesBackup()
    {
        var utcPath = WriteUtcFile("louis.utc", CreateTestUtc());

        var searchService = new ModuleSearchService();
        var fileResult = searchService.SearchSingleFile(utcPath, new SearchCriteria { Pattern = "Louis Romain" });

        var preview = _service.PreviewReplace(new[] { fileResult }, "Marcel Iceberg");

        var result = await _service.ExecuteReplaceAsync(preview, "TestModule");

        Assert.NotNull(result.BackupManifest);
        Assert.NotEmpty(result.BackupManifest.Entries);
    }

    [Fact]
    public async Task ExecuteReplace_RespectsIsSelected()
    {
        var utcPath = WriteUtcFile("louis.utc", CreateTestUtc());

        var searchService = new ModuleSearchService();
        var fileResult = searchService.SearchSingleFile(utcPath, new SearchCriteria { Pattern = "Louis" });

        var preview = _service.PreviewReplace(new[] { fileResult }, "Marcel");

        // Deselect all changes
        foreach (var change in preview.Changes)
            change.IsSelected = false;

        var result = await _service.ExecuteReplaceAsync(preview, "TestModule");

        Assert.True(result.Success);
        Assert.Equal(0, result.FilesModified);
    }

    [Fact]
    public async Task ExecuteReplace_GeneratesChangeLog()
    {
        var utcPath = WriteUtcFile("louis.utc", CreateTestUtc());

        var searchService = new ModuleSearchService();
        var fileResult = searchService.SearchSingleFile(utcPath, new SearchCriteria { Pattern = "Louis Romain" });

        var preview = _service.PreviewReplace(new[] { fileResult }, "Marcel Iceberg");

        var result = await _service.ExecuteReplaceAsync(preview, "TestModule");

        Assert.NotEmpty(result.ChangeLog);
        Assert.All(result.ChangeLog, entry => Assert.True(entry.Success));
    }

    [Fact]
    public async Task ExecuteReplace_EmptyPreview_Succeeds()
    {
        var preview = new BatchReplacePreview();

        var result = await _service.ExecuteReplaceAsync(preview, "TestModule");

        Assert.True(result.Success);
        Assert.Equal(0, result.FilesModified);
    }

    // --- ResRef bypass (allowResRefReplace) ---

    [Fact]
    public void PreviewReplace_WithAllowResRefReplace_IncludesResRefFields()
    {
        // Build a FileSearchResult containing a ResRef-field match with IsReplaceable=false
        var resRefField = new FieldDefinition
        {
            Name = "TemplateResRef",
            GffPath = "TemplateResRef",
            FieldType = SearchFieldType.ResRef,
            Category = SearchFieldCategory.Identity,
            IsReplaceable = false  // ResRef fields are non-replaceable in normal mode
        };
        var match = MakeMatch(resRefField, "louis_roumain");
        var fileResult = MakeFileResult(Path.Combine(_testDir, "test.utc"), new[] { match });

        var preview = _service.PreviewReplace(
            new[] { fileResult },
            replacementText: "bob",
            allowResRefReplace: true);

        Assert.Single(preview.Changes);
        Assert.Equal("TemplateResRef", preview.Changes[0].Match.Field.Name);
        Assert.True(preview.AllowResRefReplace);
    }

    [Fact]
    public void PreviewReplace_WithoutAllowResRefReplace_SkipsResRefFields()
    {
        var resRefField = new FieldDefinition
        {
            Name = "TemplateResRef", GffPath = "TemplateResRef",
            FieldType = SearchFieldType.ResRef,
            Category = SearchFieldCategory.Identity,
            IsReplaceable = false
        };
        var match = MakeMatch(resRefField, "louis_roumain");
        var fileResult = MakeFileResult(Path.Combine(_testDir, "test.utc"), new[] { match });

        // Default (allowResRefReplace omitted, defaults to false)
        var preview = _service.PreviewReplace(new[] { fileResult }, replacementText: "bob");

        Assert.Empty(preview.Changes);
        Assert.False(preview.AllowResRefReplace);
    }

    [Fact]
    public async Task ExecuteReplace_WithAllowResRefReplace_RewritesResRefField()
    {
        // Write a real UTC with Conversation = "louis_conv" (a ResRef field)
        var utc = new UtcFile
        {
            FirstName = new CExoLocString { LocalizedStrings = new Dictionary<uint, string> { [0] = "Louis" } },
            Tag = "LOUIS",
            Conversation = "louis_conv"
        };
        var utcPath = WriteUtcFile("louis.utc", utc);

        // Search the file — Conversation field will match since pattern is "louis_conv"
        var searchService = new ModuleSearchService();
        var fileResult = searchService.SearchSingleFile(utcPath, new SearchCriteria { Pattern = "louis_conv" });

        // Confirm there's a Conversation match (ResRef field, IsReplaceable=false)
        Assert.Contains(fileResult.Matches,
            m => m.Field.Name == "Conversation" && m.Field.FieldType == SearchFieldType.ResRef);

        var preview = _service.PreviewReplace(new[] { fileResult }, "louis", allowResRefReplace: true);
        Assert.True(preview.AllowResRefReplace);

        // Find the Conversation change in the preview
        Assert.Contains(preview.Changes, c => c.Match.Field.Name == "Conversation");

        var result = await _service.ExecuteReplaceAsync(preview, "TestModule");

        Assert.True(result.Success);
        Assert.True(result.ReplacementsMade >= 1);

        // Verify the file on disk now has Conversation = "louis"
        var rewrittenBytes = await File.ReadAllBytesAsync(utcPath);
        var rewritten = UtcReader.Read(rewrittenBytes);
        Assert.Equal("louis", rewritten.Conversation);
    }

    private static SearchMatch MakeMatch(FieldDefinition field, string value) => new()
    {
        Field = field,
        MatchedText = value,
        FullFieldValue = value,
        MatchOffset = 0,
        MatchLength = value.Length,
        Location = "test"
    };

    private static FileSearchResult MakeFileResult(string path, IEnumerable<SearchMatch> matches) => new()
    {
        FilePath = path,
        ResourceType = ResourceTypes.Utc,
        ToolId = "quartermaster",
        Matches = matches.ToList()
    };
}
