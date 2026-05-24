using Radoub.Formats.Utm;
using Radoub.UI.Controls;
using Radoub.UI.Services;
using Xunit;

namespace Radoub.UI.Tests;

/// <summary>
/// Tests for StoreBrowserPanel.BuildPaletteItemFromUtm — the pure-logic helper
/// that produces a SharedPaletteCacheItem from a UTM byte buffer, used by
/// the HAK/BIF eager populator (#2186 Sprint 3 / #2200).
/// </summary>
public class StoreBrowserPanelCachePopulatorTests
{
    private static byte[] BuildUtm(string resRef, string tag, string name)
    {
        var utm = new UtmFile
        {
            ResRef = resRef,
            Tag = tag
        };
        utm.LocName.SetString(0, name);
        return UtmWriter.Write(utm);
    }

    [Fact]
    public void BuildPaletteItemFromUtm_PopulatesResRefTagAndDisplayName()
    {
        var bytes = BuildUtm("nw_store01", "NW_STORE_TAG", "General Store");

        var item = StoreBrowserPanel.BuildPaletteItemFromUtm(
            bytes,
            resRef: "nw_store01",
            sourceLocation: "HAK: my_module.hak");

        Assert.NotNull(item);
        Assert.Equal("nw_store01", item!.ResRef);
        Assert.Equal("NW_STORE_TAG", item.Tag);
        Assert.Equal("General Store", item.DisplayName);
        Assert.Equal("HAK: my_module.hak", item.SourceLocation);
    }

    [Fact]
    public void BuildPaletteItemFromUtm_CorruptBytes_ReturnsNull()
    {
        var item = StoreBrowserPanel.BuildPaletteItemFromUtm(
            new byte[] { 0xFF, 0xFF, 0xFF, 0xFF },
            resRef: "broken",
            sourceLocation: "HAK: broken.hak");

        Assert.Null(item);
    }

    [Fact]
    public void BuildPaletteItemFromUtm_EmptyTagAndName_ProducesEmptyStrings()
    {
        var bytes = BuildUtm("store", string.Empty, string.Empty);

        var item = StoreBrowserPanel.BuildPaletteItemFromUtm(
            bytes,
            resRef: "store",
            sourceLocation: "Base Game");

        Assert.NotNull(item);
        Assert.Equal(string.Empty, item!.Tag);
        Assert.Equal(string.Empty, item.DisplayName);
    }
}
