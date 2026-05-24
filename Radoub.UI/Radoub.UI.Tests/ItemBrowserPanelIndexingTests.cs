using Radoub.UI.Controls;
using Radoub.UI.Services;
using Xunit;

namespace Radoub.UI.Tests;

/// <summary>
/// Tests for ItemBrowserPanel.TryFillFromCache — the pure-logic helper that
/// hydrates FileBrowserEntry.Tag/DisplayLabel from the shared palette cache
/// during background indexing (#2186 / #2198).
/// </summary>
public class ItemBrowserPanelIndexingTests
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
        var entry = new ItemBrowserEntry { Name = "nw_wblsw001" };
        var lookup = Lookup(new SharedPaletteCacheItem
        {
            ResRef = "nw_wblsw001",
            Tag = "NW_IT_SWORD001",
            DisplayName = "Short Sword"
        });

        var hit = ItemBrowserPanel.TryFillFromCache(entry, lookup);

        Assert.True(hit);
        Assert.Equal("NW_IT_SWORD001", entry.Tag);
        Assert.Equal("Short Sword", entry.DisplayLabel);
    }

    [Fact]
    public void TryFillFromCache_ReturnsFalse_OnMiss()
    {
        var entry = new ItemBrowserEntry { Name = "missing_item" };
        var lookup = Lookup(new SharedPaletteCacheItem
        {
            ResRef = "different_item",
            Tag = "X",
            DisplayName = "Y"
        });

        var hit = ItemBrowserPanel.TryFillFromCache(entry, lookup);

        Assert.False(hit);
        Assert.Null(entry.Tag);
        Assert.Null(entry.DisplayLabel);
    }

    [Fact]
    public void TryFillFromCache_EmptyLookup_ReturnsFalse()
    {
        var entry = new ItemBrowserEntry { Name = "anything" };
        var lookup = new Dictionary<string, SharedPaletteCacheItem>(StringComparer.OrdinalIgnoreCase);

        Assert.False(ItemBrowserPanel.TryFillFromCache(entry, lookup));
    }

    [Fact]
    public void TryFillFromCache_CaseInsensitiveLookup()
    {
        var entry = new ItemBrowserEntry { Name = "NW_WBLSW001" }; // uppercase
        var lookup = Lookup(new SharedPaletteCacheItem
        {
            ResRef = "nw_wblsw001", // lowercase cache key
            Tag = "TAG",
            DisplayName = "Name"
        });

        Assert.True(ItemBrowserPanel.TryFillFromCache(entry, lookup));
        Assert.Equal("TAG", entry.Tag);
    }

    [Fact]
    public void TryFillFromCache_NullTagOrName_ResolveToEmpty()
    {
        var entry = new ItemBrowserEntry { Name = "item" };
        var lookup = Lookup(new SharedPaletteCacheItem
        {
            ResRef = "item",
            Tag = null!,
            DisplayName = null!
        });

        Assert.True(ItemBrowserPanel.TryFillFromCache(entry, lookup));
        Assert.Equal(string.Empty, entry.Tag);
        Assert.Equal(string.Empty, entry.DisplayLabel);
    }
}
