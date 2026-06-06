using Radoub.Formats.Utp;
using Radoub.UI.Controls;
using Radoub.UI.Services;
using PlaceableEditor.Views.Panels;

namespace PlaceableEditor.Tests.Panels;

/// <summary>
/// PlaceableBrowserPanel reads Tag + Name from UTP bytes for the browser's
/// Name/Tag columns (#2294). Tests the pure metadata-read seam without spinning
/// up Avalonia.
/// </summary>
public class PlaceableBrowserPanelIndexingTests
{
    private static byte[] BuildUtp(string resRef, string tag, string name)
    {
        var utp = new UtpFile
        {
            TemplateResRef = resRef,
            Tag = tag
        };
        utp.LocName.SetString(0, name);
        return UtpWriter.Write(utp);
    }

    [Fact]
    public void ReadUtpMetadata_ReturnsTagAndName()
    {
        var bytes = BuildUtp("boulder001", "BOULDER_TAG", "Granite Boulder");

        var (tag, name) = PlaceableBrowserPanel.ReadUtpMetadata(bytes);

        Assert.Equal("BOULDER_TAG", tag);
        Assert.Equal("Granite Boulder", name);
    }

    [Fact]
    public void ReadUtpMetadata_EmptyName_ReturnsEmptyString()
    {
        var bytes = BuildUtp("plain001", "PLAIN_TAG", "");

        var (tag, name) = PlaceableBrowserPanel.ReadUtpMetadata(bytes);

        Assert.Equal("PLAIN_TAG", tag);
        Assert.Equal("", name);
    }

    [Fact]
    public void ReadUtpMetadata_GarbageBytes_ReturnsEmptyPair()
    {
        var (tag, name) = PlaceableBrowserPanel.ReadUtpMetadata(new byte[] { 0x00, 0x01, 0x02 });

        Assert.Equal("", tag);
        Assert.Equal("", name);
    }

    // --- TryFillFromCache (cache fast-path during background indexing, design §5.5) ---

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
        var entry = new PlaceableBrowserEntry { Name = "boulder001" };
        var lookup = Lookup(new SharedPaletteCacheItem
        {
            ResRef = "boulder001",
            Tag = "BOULDER_TAG",
            DisplayName = "Granite Boulder"
        });

        var hit = PlaceableBrowserPanel.TryFillFromCache(entry, lookup);

        Assert.True(hit);
        Assert.Equal("BOULDER_TAG", entry.Tag);
        Assert.Equal("Granite Boulder", entry.DisplayLabel);
    }

    [Fact]
    public void TryFillFromCache_ReturnsFalse_OnMiss()
    {
        var entry = new PlaceableBrowserEntry { Name = "missing_plc" };
        var lookup = Lookup(new SharedPaletteCacheItem
        {
            ResRef = "different_plc",
            Tag = "X",
            DisplayName = "Y"
        });

        var hit = PlaceableBrowserPanel.TryFillFromCache(entry, lookup);

        Assert.False(hit);
        Assert.Null(entry.Tag);
        Assert.Null(entry.DisplayLabel);
    }

    [Fact]
    public void TryFillFromCache_EmptyLookup_ReturnsFalse()
    {
        var entry = new PlaceableBrowserEntry { Name = "anything" };
        var lookup = new Dictionary<string, SharedPaletteCacheItem>(StringComparer.OrdinalIgnoreCase);

        Assert.False(PlaceableBrowserPanel.TryFillFromCache(entry, lookup));
    }

    [Fact]
    public void TryFillFromCache_CaseInsensitiveLookup()
    {
        var entry = new PlaceableBrowserEntry { Name = "BOULDER001" }; // uppercase
        var lookup = Lookup(new SharedPaletteCacheItem
        {
            ResRef = "boulder001", // lowercase cache key
            Tag = "TAG",
            DisplayName = "Name"
        });

        Assert.True(PlaceableBrowserPanel.TryFillFromCache(entry, lookup));
        Assert.Equal("TAG", entry.Tag);
    }

    [Fact]
    public void TryFillFromCache_NullTagOrName_ResolveToEmpty()
    {
        var entry = new PlaceableBrowserEntry { Name = "plc" };
        var lookup = Lookup(new SharedPaletteCacheItem
        {
            ResRef = "plc",
            Tag = null!,
            DisplayName = null!
        });

        Assert.True(PlaceableBrowserPanel.TryFillFromCache(entry, lookup));
        Assert.Equal(string.Empty, entry.Tag);
        Assert.Equal(string.Empty, entry.DisplayLabel);
    }
}
