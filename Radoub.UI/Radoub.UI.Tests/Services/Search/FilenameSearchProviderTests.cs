using Radoub.Formats.Common;
using Radoub.Formats.Search;
using Radoub.UI.Services.Search;
using Xunit;

namespace Radoub.UI.Tests.Services.Search;

public class FilenameSearchProviderTests : IDisposable
{
    private readonly string _tempDir;

    public FilenameSearchProviderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"filename-search-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private void Touch(string fileName)
    {
        File.WriteAllText(Path.Combine(_tempDir, fileName), "stub");
    }

    [Fact]
    public void Search_BasicMatch_ReturnsFilenameResult()
    {
        Touch("louis_roumain.dlg");
        Touch("alice.utc");
        Touch("bob.utc");

        var provider = new FilenameSearchProvider();
        var criteria = new SearchCriteria { Pattern = "louis", IncludeFilenameResRef = true };

        var results = provider.Search(_tempDir, criteria);

        Assert.Single(results);
        Assert.Equal("louis_roumain.dlg", results[0].FileName);
        Assert.Equal(ResourceTypes.Dlg, results[0].ResourceType);
    }

    [Fact]
    public void Search_CaseInsensitive_FindsMixedCaseFilenames()
    {
        Touch("Louis_Roumain.dlg");
        Touch("LOUIS_ALT.utc");
        Touch("alice.dlg");

        var provider = new FilenameSearchProvider();
        var criteria = new SearchCriteria { Pattern = "LOUIS", IncludeFilenameResRef = true };

        var results = provider.Search(_tempDir, criteria);

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public void Search_CaseSensitive_OnlyMatchesExactCase()
    {
        Touch("Louis_Roumain.dlg");
        Touch("louis_alt.utc");

        var provider = new FilenameSearchProvider();
        var criteria = new SearchCriteria
        {
            Pattern = "Louis",
            CaseSensitive = true,
            IncludeFilenameResRef = true
        };

        var results = provider.Search(_tempDir, criteria);

        Assert.Single(results);
        Assert.Equal("Louis_Roumain.dlg", results[0].FileName);
    }

    [Fact]
    public void Search_WholeWord_DoesNotMatchSubstring()
    {
        Touch("louis_roumain.dlg");  // contains "louis" as a prefix-with-underscore-after
        Touch("louis.utc");           // is exactly "louis"

        var provider = new FilenameSearchProvider();
        var criteria = new SearchCriteria
        {
            Pattern = "louis",
            WholeWord = true,
            IncludeFilenameResRef = true
        };

        var results = provider.Search(_tempDir, criteria);

        // \blouis\b: \b treats _ as a word character, so "louis_roumain" has \b "louis" \b false.
        // Only "louis.utc" (where the whole filename without extension IS "louis") should match.
        Assert.Single(results);
        Assert.Equal("louis.utc", results[0].FileName);
    }

    [Fact]
    public void Search_FileTypeFilter_DoesNotGateFilenameSearch()
    {
        // Filename search is independent of the 18 file-type content checkboxes.
        // Those checkboxes gate file-CONTENT searching; filename matches are their
        // own search domain enabled by SearchFilenameResRef. A user typing "louis"
        // with only the filename/ResRef checkbox on should get matches across all
        // file types — that's the surgical workflow.
        Touch("louis.dlg");
        Touch("louis.utc");
        Touch("louis.git");

        var provider = new FilenameSearchProvider();
        var criteria = new SearchCriteria
        {
            Pattern = "louis",
            IncludeFilenameResRef = true,
            FileTypeFilter = new[] { ResourceTypes.Dlg }  // narrowed content filter
        };

        var results = provider.Search(_tempDir, criteria);

        // All three filenames match — the file-type filter does NOT constrain
        // filename search results.
        Assert.Equal(3, results.Count);
    }

    [Fact]
    public void Search_ExcludesSubdirectoryFiles()
    {
        Touch("louis.dlg");
        var subDir = Path.Combine(_tempDir, "backup");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(subDir, "louis_old.dlg"), "stub");

        var provider = new FilenameSearchProvider();
        var criteria = new SearchCriteria { Pattern = "louis", IncludeFilenameResRef = true };

        var results = provider.Search(_tempDir, criteria);

        Assert.Single(results);
        Assert.Equal("louis.dlg", results[0].FileName);
    }

    [Fact]
    public void Search_EmptyDirectory_ReturnsNoResults()
    {
        var provider = new FilenameSearchProvider();
        var criteria = new SearchCriteria { Pattern = "louis", IncludeFilenameResRef = true };

        var results = provider.Search(_tempDir, criteria);

        Assert.Empty(results);
    }

    [Fact]
    public void Search_NonexistentDirectory_ReturnsNoResults()
    {
        var provider = new FilenameSearchProvider();
        var criteria = new SearchCriteria { Pattern = "louis", IncludeFilenameResRef = true };

        var results = provider.Search(Path.Combine(_tempDir, "does-not-exist"), criteria);

        Assert.Empty(results);
    }

    [Fact]
    public void Search_InvalidCriteria_ReturnsNoResults()
    {
        Touch("louis.dlg");

        var provider = new FilenameSearchProvider();
        // Invalid: empty pattern
        var criteria = new SearchCriteria { Pattern = "", IncludeFilenameResRef = true };

        var results = provider.Search(_tempDir, criteria);

        Assert.Empty(results);
    }
}
