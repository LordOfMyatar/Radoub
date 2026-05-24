using System;
using RadoubLauncher.Services;

namespace Trebuchet.Tests;

/// <summary>
/// Unit tests for SearchIndexStaleness — pure decision logic that decides
/// whether the cached Marlinspike search/item-resolution services should be
/// invalidated based on the module working directory's last-write time.
///
/// Covers #2072: search index never refreshes after ERF import / external
/// file changes.
/// </summary>
public class SearchIndexStalenessTests
{
    [Fact]
    public void IsStale_NoPriorIndex_ReturnsTrue()
    {
        // Never indexed before — must build the index now.
        Assert.True(SearchIndexStaleness.IsStale(
            currentMtime: new DateTime(2026, 5, 24, 12, 0, 0, DateTimeKind.Utc),
            lastIndexedMtime: null));
    }

    [Fact]
    public void IsStale_DirectoryMtimeUnchanged_ReturnsFalse()
    {
        var t = new DateTime(2026, 5, 24, 12, 0, 0, DateTimeKind.Utc);
        Assert.False(SearchIndexStaleness.IsStale(currentMtime: t, lastIndexedMtime: t));
    }

    [Fact]
    public void IsStale_DirectoryMtimeNewer_ReturnsTrue()
    {
        var t0 = new DateTime(2026, 5, 24, 12, 0, 0, DateTimeKind.Utc);
        var t1 = t0.AddSeconds(5);
        Assert.True(SearchIndexStaleness.IsStale(currentMtime: t1, lastIndexedMtime: t0));
    }

    [Fact]
    public void IsStale_DirectoryMtimeOlder_ReturnsFalse()
    {
        // Pathological — directory mtime went backward (clock skew, file restore).
        // Don't invalidate; the existing index is at least as fresh.
        var t0 = new DateTime(2026, 5, 24, 12, 0, 0, DateTimeKind.Utc);
        var t1 = t0.AddSeconds(-5);
        Assert.False(SearchIndexStaleness.IsStale(currentMtime: t1, lastIndexedMtime: t0));
    }

    [Fact]
    public void IsStale_CurrentMtimeNull_ReturnsTrue()
    {
        // Directory disappeared between indexing and check — force re-init.
        Assert.True(SearchIndexStaleness.IsStale(
            currentMtime: null,
            lastIndexedMtime: new DateTime(2026, 5, 24, 12, 0, 0, DateTimeKind.Utc)));
    }
}
