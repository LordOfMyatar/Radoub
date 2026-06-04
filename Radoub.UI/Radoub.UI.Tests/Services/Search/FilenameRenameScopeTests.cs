using Radoub.Formats.Common;
using Radoub.Formats.Search;
using Radoub.UI.Services.Search;
using Xunit;

namespace Radoub.UI.Tests.Services.Search;

/// <summary>
/// Reproduction for the DM report: a Marlinspike resref/filename search aimed at
/// items also swept .nss scripts into the rename, so scripts "went missing"
/// (they were silently RENAMED, not deleted — their old names vanished).
///
/// Root cause under test: FilenameSearchProvider matches ALL searchable file
/// types — including .nss — independent of the file-type checkboxes, and the
/// rename dispatch then builds rename plans for those scripts.
/// </summary>
public class FilenameRenameScopeTests : IDisposable
{
    private readonly string _root;

    public FilenameRenameScopeTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"scope-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private void Touch(string name) => File.WriteAllText(Path.Combine(_root, name), "stub");

    [Fact]
    public void FilenameSearch_WithNssCheckboxUnticked_StillMatchesScripts()
    {
        // User wants to remove items tagged "quest". They search "quest" in
        // filename/resref mode. They did NOT check the .nss file type — they only
        // care about items. But scripts share the token.
        Touch("quest_reward.uti");   // the item they meant to find
        Touch("_quest_open.nss");    // an unrelated script — same token, leading underscore
        Touch("_quest_close.nss");   // another

        var provider = new FilenameSearchProvider();

        // Simulate "only .uti checked" by filtering to UTI. FilenameSearchProvider
        // documents that it ignores the file-type filter for filename matches.
        var criteria = new SearchCriteria
        {
            Pattern = "quest",
            IncludeFilenameResRef = true,
            FileTypeFilter = new[] { ResourceTypes.Uti }  // user only wanted items
        };

        var results = provider.Search(_root, criteria);

        var nssHits = results.Where(r => r.ResourceType == ResourceTypes.Nss).ToList();

        // Documents current (buggy) behavior: scripts are pulled in even though the
        // user scoped the search to items only.
        Assert.NotEmpty(nssHits);
    }
}
