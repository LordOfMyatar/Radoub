using Radoub.Formats.Common;
using Radoub.Formats.Services;
using Radoub.TestUtilities.Mocks;
using Radoub.UI.Controls;
using Xunit;

namespace Radoub.UI.Tests;

/// <summary>
/// Tests for ItemBrowserPanel base-game (BIF) item support (#2106).
/// Mirrors the BIF-aware patterns in StoreBrowserPanel / CreatureBrowserPanel —
/// entry flag, display formatting, archive routing, and the IsArchiveEntry override.
/// </summary>
public class ItemBrowserPanelBifTests
{
    [Fact]
    public void ItemBrowserEntry_DefaultsIsFromBifFalse()
    {
        var entry = new ItemBrowserEntry();
        Assert.False(entry.IsFromBif);
    }

    [Fact]
    public void ItemBrowserEntry_DisplayName_IncludesSource_WhenFromBif()
    {
        var entry = new ItemBrowserEntry
        {
            Name = "nw_wblcl001",
            Source = "Base Game",
            IsFromBif = true
        };

        Assert.Equal("nw_wblcl001 (Base Game)", entry.DisplayName);
    }

    [Fact]
    public void ItemBrowserEntry_DisplayName_PlainName_WhenNotFromBif()
    {
        var entry = new ItemBrowserEntry
        {
            Name = "my_module_item",
            Source = "Module",
            IsFromBif = false
        };

        Assert.Equal("my_module_item", entry.DisplayName);
    }

    [Fact]
    public void IsItemArchiveEntry_ReturnsTrue_ForBifEntry()
    {
        var entry = new ItemBrowserEntry { Name = "x", IsFromBif = true };
        Assert.True(ItemBrowserPanel.IsItemArchiveEntry(entry));
    }

    [Fact]
    public void IsItemArchiveEntry_ReturnsTrue_ForHakEntry()
    {
        var entry = new ItemBrowserEntry { Name = "x", IsFromHak = true };
        Assert.True(ItemBrowserPanel.IsItemArchiveEntry(entry));
    }

    [Fact]
    public void IsItemArchiveEntry_ReturnsFalse_ForModuleEntry()
    {
        var entry = new ItemBrowserEntry { Name = "x", IsFromHak = false, IsFromBif = false };
        Assert.False(ItemBrowserPanel.IsItemArchiveEntry(entry));
    }

    [Fact]
    public void IsItemArchiveEntry_ReturnsFalse_ForNonItemEntry()
    {
        var entry = new FileBrowserEntry { Name = "x", IsFromHak = true };
        Assert.False(ItemBrowserPanel.IsItemArchiveEntry(entry));
    }

    [Fact]
    public void ExtractItemArchiveBytes_RoutesBifEntry_ThroughGameDataService()
    {
        var bifBytes = new byte[] { 0x55, 0x54, 0x49, 0x20 }; // "UTI " marker
        var mock = new MockGameDataService(includeSampleData: false);
        mock.SetResource("nw_wblcl001", ResourceTypes.Uti, bifBytes);

        var entry = new ItemBrowserEntry
        {
            Name = "nw_wblcl001",
            IsFromBif = true
        };

        var bytes = ItemBrowserPanel.ExtractItemArchiveBytes(entry, mock);

        Assert.Equal(bifBytes, bytes);
    }

    [Fact]
    public void ExtractItemArchiveBytes_BifEntry_ReturnsNull_WhenGameDataServiceUnconfigured()
    {
        var entry = new ItemBrowserEntry { Name = "nw_wblcl001", IsFromBif = true };
        var mock = new MockGameDataService(includeSampleData: false).AsUnconfigured();

        var bytes = ItemBrowserPanel.ExtractItemArchiveBytes(entry, mock);

        Assert.Null(bytes);
    }

    [Fact]
    public void ExtractItemArchiveBytes_BifEntry_ReturnsNull_WhenGameDataServiceNull()
    {
        var entry = new ItemBrowserEntry { Name = "nw_wblcl001", IsFromBif = true };

        var bytes = ItemBrowserPanel.ExtractItemArchiveBytes(entry, gameDataService: null);

        Assert.Null(bytes);
    }

    [Fact]
    public void ExtractItemArchiveBytes_ModuleEntry_ReturnsNull()
    {
        var entry = new ItemBrowserEntry { Name = "my_item", IsFromHak = false, IsFromBif = false };
        var mock = new MockGameDataService(includeSampleData: false);

        var bytes = ItemBrowserPanel.ExtractItemArchiveBytes(entry, mock);

        Assert.Null(bytes);
    }

    [Fact]
    public void GameDataService_IsSettableAndReadable()
    {
        var mock = new MockGameDataService(includeSampleData: false);
        var panel = new ItemBrowserPanel
        {
            GameDataService = mock
        };

        Assert.Same(mock, panel.GameDataService);
    }

    // --- Filter classification (Module / HAK / BIF parity, #2106 follow-up) ---

    [Theory]
    [InlineData(true, false, false, true)]   // Module entry, only Module on → visible
    [InlineData(false, false, false, false)]  // Module entry, Module off → hidden
    [InlineData(true, true, true, true)]    // Module entry, all on → visible
    public void PassesFilter_ModuleEntry_RespectsModuleCheckbox(
        bool showModule, bool showHak, bool showBif, bool expected)
    {
        var entry = new ItemBrowserEntry { Name = "x", IsFromHak = false, IsFromBif = false };
        Assert.Equal(expected, ItemBrowserPanel.PassesItemFilter(entry, showModule, showHak, showBif));
    }

    [Theory]
    [InlineData(false, true, false, true)]   // HAK entry, HAK on → visible
    [InlineData(true, false, true, false)]   // HAK entry, HAK off → hidden (Module/BIF irrelevant)
    public void PassesFilter_HakEntry_RespectsHakCheckbox(
        bool showModule, bool showHak, bool showBif, bool expected)
    {
        var entry = new ItemBrowserEntry { Name = "x", IsFromHak = true, IsFromBif = false };
        Assert.Equal(expected, ItemBrowserPanel.PassesItemFilter(entry, showModule, showHak, showBif));
    }

    [Theory]
    [InlineData(false, false, true, true)]   // BIF entry, BIF on → visible
    [InlineData(true, true, false, false)]   // BIF entry, BIF off → hidden (Module/HAK irrelevant)
    public void PassesFilter_BifEntry_RespectsBifCheckbox(
        bool showModule, bool showHak, bool showBif, bool expected)
    {
        var entry = new ItemBrowserEntry { Name = "x", IsFromHak = false, IsFromBif = true };
        Assert.Equal(expected, ItemBrowserPanel.PassesItemFilter(entry, showModule, showHak, showBif));
    }
}
