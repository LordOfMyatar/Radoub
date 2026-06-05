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
/// Fix (#2341): FilenameSearchProvider now honors SearchCriteria.FileTypeFilter
/// with the same semantics as content search — null = all types, non-empty =
/// only-listed. A search scoped to .uti must NOT return .nss scripts.
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
    public void FilenameSearch_ScopedToUti_DoesNotMatchScripts()
    {
        // User wants to remove items tagged "quest". They search "quest" in
        // filename/resref mode with only .uti checked. Scripts share the token but
        // must NOT be swept into the rename.
        Touch("quest_reward.uti");   // the item they meant to find
        Touch("_quest_open.nss");    // an unrelated script — same token, leading underscore
        Touch("_quest_close.nss");   // another

        var provider = new FilenameSearchProvider();

        var criteria = new SearchCriteria
        {
            Pattern = "quest",
            IncludeFilenameResRef = true,
            FileTypeFilter = new[] { ResourceTypes.Uti }  // user only wanted items
        };

        var results = provider.Search(_root, criteria);

        Assert.DoesNotContain(results, r => r.ResourceType == ResourceTypes.Nss);
        Assert.Contains(results, r => r.ResourceType == ResourceTypes.Uti);
    }

    [Fact]
    public void FilenameSearch_NullFilter_MatchesAllTypes()
    {
        // No filter (null) = surgical filename-only workflow: every searchable type
        // is still matched, preserving the original design intent.
        Touch("quest_reward.uti");
        Touch("_quest_open.nss");

        var provider = new FilenameSearchProvider();

        var criteria = new SearchCriteria
        {
            Pattern = "quest",
            IncludeFilenameResRef = true,
            FileTypeFilter = null
        };

        var results = provider.Search(_root, criteria);

        Assert.Contains(results, r => r.ResourceType == ResourceTypes.Uti);
        Assert.Contains(results, r => r.ResourceType == ResourceTypes.Nss);
    }

    [Fact]
    public void FilenameSearch_EmptyFilter_MatchesAllTypes()
    {
        // Empty filter (all 18 content checkboxes unticked) must still run the
        // filename-only workflow — same convention as RenameDispatchHelpers
        // (Count > 0 gates the filter). Empty != "match nothing".
        Touch("quest_reward.uti");
        Touch("_quest_open.nss");

        var provider = new FilenameSearchProvider();

        var criteria = new SearchCriteria
        {
            Pattern = "quest",
            IncludeFilenameResRef = true,
            FileTypeFilter = System.Array.Empty<ushort>()
        };

        var results = provider.Search(_root, criteria);

        Assert.Contains(results, r => r.ResourceType == ResourceTypes.Uti);
        Assert.Contains(results, r => r.ResourceType == ResourceTypes.Nss);
    }
}
