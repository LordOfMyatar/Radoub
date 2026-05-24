using Radoub.UI.Controls;
using Radoub.UI.Services;
using Xunit;

namespace Radoub.UI.Tests;

/// <summary>
/// Tests for CreatureBrowserPanel.TryFillFromCache — the pure-logic helper
/// that hydrates FileBrowserEntry.Tag/DisplayLabel from the shared palette
/// cache during background indexing (#2186 Sprint 4 / #2201).
/// </summary>
public class CreatureBrowserPanelIndexingTests
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
        var entry = new CreatureBrowserEntry { Name = "nw_orc01" };
        var lookup = Lookup(new SharedPaletteCacheItem
        {
            ResRef = "nw_orc01",
            Tag = "NW_ORC_GRUNT",
            DisplayName = "Orog Stoneskull"
        });

        var hit = CreatureBrowserPanel.TryFillFromCache(entry, lookup);

        Assert.True(hit);
        Assert.Equal("NW_ORC_GRUNT", entry.Tag);
        Assert.Equal("Orog Stoneskull", entry.DisplayLabel);
    }

    [Fact]
    public void TryFillFromCache_ReturnsFalse_OnMiss()
    {
        var entry = new CreatureBrowserEntry { Name = "missing_creature" };
        var lookup = Lookup(new SharedPaletteCacheItem
        {
            ResRef = "different_creature",
            Tag = "X",
            DisplayName = "Y"
        });

        var hit = CreatureBrowserPanel.TryFillFromCache(entry, lookup);

        Assert.False(hit);
        Assert.Null(entry.Tag);
        Assert.Null(entry.DisplayLabel);
    }

    [Fact]
    public void TryFillFromCache_EmptyLookup_ReturnsFalse()
    {
        var entry = new CreatureBrowserEntry { Name = "anything" };
        var lookup = new Dictionary<string, SharedPaletteCacheItem>(StringComparer.OrdinalIgnoreCase);

        Assert.False(CreatureBrowserPanel.TryFillFromCache(entry, lookup));
    }

    [Fact]
    public void TryFillFromCache_CaseInsensitiveLookup()
    {
        var entry = new CreatureBrowserEntry { Name = "NW_ORC01" }; // uppercase
        var lookup = Lookup(new SharedPaletteCacheItem
        {
            ResRef = "nw_orc01", // lowercase cache key
            Tag = "TAG",
            DisplayName = "Name"
        });

        Assert.True(CreatureBrowserPanel.TryFillFromCache(entry, lookup));
        Assert.Equal("TAG", entry.Tag);
    }

    [Fact]
    public void TryFillFromCache_NullTagOrName_ResolveToEmpty()
    {
        var entry = new CreatureBrowserEntry { Name = "creature" };
        var lookup = Lookup(new SharedPaletteCacheItem
        {
            ResRef = "creature",
            Tag = null!,
            DisplayName = null!
        });

        Assert.True(CreatureBrowserPanel.TryFillFromCache(entry, lookup));
        Assert.Equal(string.Empty, entry.Tag);
        Assert.Equal(string.Empty, entry.DisplayLabel);
    }
}
