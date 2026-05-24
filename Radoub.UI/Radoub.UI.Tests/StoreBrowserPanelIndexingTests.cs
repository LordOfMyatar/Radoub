using Radoub.UI.Controls;
using Radoub.UI.Services;
using Xunit;

namespace Radoub.UI.Tests;

/// <summary>
/// Tests for StoreBrowserPanel.TryFillFromCache — the pure-logic helper that
/// hydrates FileBrowserEntry.Tag/DisplayLabel from the shared palette cache
/// during background indexing (#2186 Sprint 3 / #2200).
/// </summary>
public class StoreBrowserPanelIndexingTests
{
    private static Dictionary<string, SharedPaletteCacheItem> Lookup(
        params SharedPaletteCacheItem[] items)
    {
        var dict = new Dictionary<string, SharedPaletteCacheItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
            dict.TryAdd(item.ResRef, item);
        return dict;
    }

    [Fact]
    public void TryFillFromCache_PopulatesTagAndDisplayLabel_OnHit()
    {
        var entry = new StoreBrowserEntry { Name = "nw_store01" };
        var lookup = Lookup(new SharedPaletteCacheItem
        {
            ResRef = "nw_store01",
            Tag = "NW_STORE_GENERAL",
            DisplayName = "General Store"
        });

        var hit = StoreBrowserPanel.TryFillFromCache(entry, lookup);

        Assert.True(hit);
        Assert.Equal("NW_STORE_GENERAL", entry.Tag);
        Assert.Equal("General Store", entry.DisplayLabel);
    }

    [Fact]
    public void TryFillFromCache_ReturnsFalse_OnMiss()
    {
        var entry = new StoreBrowserEntry { Name = "missing_store" };
        var lookup = Lookup(new SharedPaletteCacheItem
        {
            ResRef = "different_store",
            Tag = "X",
            DisplayName = "Y"
        });

        var hit = StoreBrowserPanel.TryFillFromCache(entry, lookup);

        Assert.False(hit);
        Assert.Null(entry.Tag);
        Assert.Null(entry.DisplayLabel);
    }

    [Fact]
    public void TryFillFromCache_EmptyLookup_ReturnsFalse()
    {
        var entry = new StoreBrowserEntry { Name = "anything" };
        var lookup = new Dictionary<string, SharedPaletteCacheItem>(StringComparer.OrdinalIgnoreCase);

        Assert.False(StoreBrowserPanel.TryFillFromCache(entry, lookup));
    }

    [Fact]
    public void TryFillFromCache_CaseInsensitiveLookup()
    {
        var entry = new StoreBrowserEntry { Name = "NW_STORE01" }; // uppercase
        var lookup = Lookup(new SharedPaletteCacheItem
        {
            ResRef = "nw_store01", // lowercase cache key
            Tag = "TAG",
            DisplayName = "Name"
        });

        Assert.True(StoreBrowserPanel.TryFillFromCache(entry, lookup));
        Assert.Equal("TAG", entry.Tag);
    }

    [Fact]
    public void TryFillFromCache_NullTagOrName_ResolveToEmpty()
    {
        var entry = new StoreBrowserEntry { Name = "store" };
        var lookup = Lookup(new SharedPaletteCacheItem
        {
            ResRef = "store",
            Tag = null!,
            DisplayName = null!
        });

        Assert.True(StoreBrowserPanel.TryFillFromCache(entry, lookup));
        Assert.Equal(string.Empty, entry.Tag);
        Assert.Equal(string.Empty, entry.DisplayLabel);
    }
}
