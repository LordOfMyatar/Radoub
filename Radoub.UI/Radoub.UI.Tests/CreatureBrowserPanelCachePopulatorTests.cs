using Radoub.Formats.Utc;
using Radoub.UI.Controls;
using Radoub.UI.Services;
using Xunit;

namespace Radoub.UI.Tests;

/// <summary>
/// Tests for CreatureBrowserPanel.BuildPaletteItemFromUtc — the pure-logic
/// helper that produces a SharedPaletteCacheItem from a UTC byte buffer,
/// used by the HAK/BIF eager populator (#2186 Sprint 4 / #2201).
/// </summary>
public class CreatureBrowserPanelCachePopulatorTests
{
    private static byte[] BuildUtc(string resRef, string tag, string firstName, string lastName)
    {
        var utc = new UtcFile
        {
            TemplateResRef = resRef,
            Tag = tag
        };
        utc.FirstName.SetString(0, firstName);
        utc.LastName.SetString(0, lastName);
        return UtcWriter.Write(utc);
    }

    [Fact]
    public void BuildPaletteItemFromUtc_PopulatesResRefTagAndFullName()
    {
        var bytes = BuildUtc("nw_orc01", "NW_ORC_GRUNT", "Orog", "Stoneskull");

        var item = CreatureBrowserPanel.BuildPaletteItemFromUtc(
            bytes,
            resRef: "nw_orc01",
            sourceLocation: "HAK: my_module.hak");

        Assert.NotNull(item);
        Assert.Equal("nw_orc01", item!.ResRef);
        Assert.Equal("NW_ORC_GRUNT", item.Tag);
        Assert.Equal("Orog Stoneskull", item.DisplayName);
        Assert.Equal("HAK: my_module.hak", item.SourceLocation);
    }

    [Fact]
    public void BuildPaletteItemFromUtc_EmptyLastName_DisplayNameIsFirstNameOnly()
    {
        var bytes = BuildUtc("nw_villager", "TAG", "Bartholomew", string.Empty);

        var item = CreatureBrowserPanel.BuildPaletteItemFromUtc(
            bytes,
            resRef: "nw_villager",
            sourceLocation: "Base Game");

        Assert.NotNull(item);
        Assert.Equal("Bartholomew", item!.DisplayName);
    }

    [Fact]
    public void BuildPaletteItemFromUtc_EmptyFirstName_DisplayNameIsLastNameOnly()
    {
        var bytes = BuildUtc("nw_lord", "TAG", string.Empty, "Greyhawk");

        var item = CreatureBrowserPanel.BuildPaletteItemFromUtc(
            bytes,
            resRef: "nw_lord",
            sourceLocation: "Base Game");

        Assert.NotNull(item);
        Assert.Equal("Greyhawk", item!.DisplayName);
    }

    [Fact]
    public void BuildPaletteItemFromUtc_BothNamesEmpty_DisplayNameIsEmpty()
    {
        var bytes = BuildUtc("nw_anon", string.Empty, string.Empty, string.Empty);

        var item = CreatureBrowserPanel.BuildPaletteItemFromUtc(
            bytes,
            resRef: "nw_anon",
            sourceLocation: "HAK: anon.hak");

        Assert.NotNull(item);
        Assert.Equal(string.Empty, item!.DisplayName);
        Assert.Equal(string.Empty, item.Tag);
    }

    [Fact]
    public void BuildPaletteItemFromUtc_CorruptBytes_ReturnsNull()
    {
        var item = CreatureBrowserPanel.BuildPaletteItemFromUtc(
            new byte[] { 0xFF, 0xFF, 0xFF, 0xFF },
            resRef: "broken",
            sourceLocation: "HAK: broken.hak");

        Assert.Null(item);
    }
}
