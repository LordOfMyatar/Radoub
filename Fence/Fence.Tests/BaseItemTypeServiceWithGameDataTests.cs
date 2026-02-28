using MerchantEditor.Services;
using Radoub.TestUtilities.Mocks;

namespace Fence.Tests;

/// <summary>
/// Tests for BaseItemTypeService with a configured MockGameDataService.
/// Verifies the 2DA loading path that was untested when only null was passed.
/// </summary>
public class BaseItemTypeServiceWithGameDataTests
{
    private readonly MockGameDataService _mockGameData;

    public BaseItemTypeServiceWithGameDataTests()
    {
        _mockGameData = new MockGameDataService(includeSampleData: true);
        SetupBaseItemsData();
    }

    private void SetupBaseItemsData()
    {
        // baseitems.2da: label, Name (TLK strRef)
        // Row 0: Shortsword with TLK name
        _mockGameData.Set2DAValue("baseitems", 0, "label", "BASE_ITEM_SHORTSWORD");
        _mockGameData.Set2DAValue("baseitems", 0, "Name", "500");

        // Row 1: Longsword with TLK name
        _mockGameData.Set2DAValue("baseitems", 1, "label", "BASE_ITEM_LONGSWORD");
        _mockGameData.Set2DAValue("baseitems", 1, "Name", "501");

        // Row 2: Armor with TLK name
        _mockGameData.Set2DAValue("baseitems", 2, "label", "BASE_ITEM_ARMOR");
        _mockGameData.Set2DAValue("baseitems", 2, "Name", "502");

        // Row 3: Custom item with no TLK (Name = "****"), falls back to label formatting
        _mockGameData.Set2DAValue("baseitems", 3, "label", "BASE_ITEM_MAGIC_WAND");
        _mockGameData.Set2DAValue("baseitems", 3, "Name", "****");

        // Row 4: Deleted/garbage row - should be skipped
        _mockGameData.Set2DAValue("baseitems", 4, "label", "DELETED_ITEM");
        _mockGameData.Set2DAValue("baseitems", 4, "Name", "504");

        // Row 5: Another garbage row with padding - should be skipped
        _mockGameData.Set2DAValue("baseitems", 5, "label", "padding_row");
        _mockGameData.Set2DAValue("baseitems", 5, "Name", "505");

        // Row 6: Item with BadStrRef TLK value - falls back to label
        _mockGameData.Set2DAValue("baseitems", 6, "label", "BASE_ITEM_RING");
        _mockGameData.Set2DAValue("baseitems", 6, "Name", "506");

        // Row 7: Item with empty/null TLK string - falls back to label
        _mockGameData.Set2DAValue("baseitems", 7, "label", "BASE_ITEM_AMULET");
        _mockGameData.Set2DAValue("baseitems", 7, "Name", "507");

        // TLK strings
        _mockGameData.SetTlkString(500, "Short Sword");
        _mockGameData.SetTlkString(501, "Long Sword");
        _mockGameData.SetTlkString(502, "Armor");
        _mockGameData.SetTlkString(504, "Deleted Item");
        _mockGameData.SetTlkString(505, "Padding");
        _mockGameData.SetTlkString(506, "BadStrRef"); // Invalid TLK value
        // 507 not set — GetString returns null → falls back to label
    }

    #region 2DA Loading Path

    [Fact]
    public void GetBaseItemTypes_WithConfiguredGameData_LoadsFrom2DA()
    {
        var service = new BaseItemTypeService(_mockGameData);

        var types = service.GetBaseItemTypes();

        Assert.NotEmpty(types);
        // Should NOT be 60+ hardcoded types
        Assert.True(types.Count < 20, "Should load from 2DA, not hardcoded fallback");
    }

    [Fact]
    public void GetBaseItemTypes_ResolvesNamesFromTlk()
    {
        var service = new BaseItemTypeService(_mockGameData);

        var types = service.GetBaseItemTypes();

        Assert.Contains(types, t => t.DisplayName == "Short Sword" && t.BaseItemIndex == 0);
        Assert.Contains(types, t => t.DisplayName == "Long Sword" && t.BaseItemIndex == 1);
        Assert.Contains(types, t => t.DisplayName == "Armor" && t.BaseItemIndex == 2);
    }

    [Fact]
    public void GetBaseItemTypes_NoTlkName_FallsBackToFormattedLabel()
    {
        var service = new BaseItemTypeService(_mockGameData);

        var types = service.GetBaseItemTypes();

        // Row 3 has Name="****", so it should use FormatBaseItemLabel("BASE_ITEM_MAGIC_WAND") = "Magic Wand"
        Assert.Contains(types, t => t.DisplayName == "Magic Wand" && t.BaseItemIndex == 3);
    }

    [Fact]
    public void GetBaseItemTypes_BadStrRefTlk_FallsBackToFormattedLabel()
    {
        var service = new BaseItemTypeService(_mockGameData);

        var types = service.GetBaseItemTypes();

        // Row 6 has TLK "BadStrRef" which IsValidTlkString rejects → falls back to label
        Assert.Contains(types, t => t.DisplayName == "Ring" && t.BaseItemIndex == 6);
    }

    [Fact]
    public void GetBaseItemTypes_NullTlkString_FallsBackToFormattedLabel()
    {
        var service = new BaseItemTypeService(_mockGameData);

        var types = service.GetBaseItemTypes();

        // Row 7 has no TLK string (507 not set) → falls back to FormatBaseItemLabel
        Assert.Contains(types, t => t.DisplayName == "Amulet" && t.BaseItemIndex == 7);
    }

    [Fact]
    public void GetBaseItemTypes_SkipsGarbageLabels()
    {
        var service = new BaseItemTypeService(_mockGameData);

        var types = service.GetBaseItemTypes();

        // Rows 4 ("DELETED_ITEM") and 5 ("padding_row") should be filtered out
        Assert.DoesNotContain(types, t => t.BaseItemIndex == 4);
        Assert.DoesNotContain(types, t => t.BaseItemIndex == 5);
    }

    [Fact]
    public void GetBaseItemTypes_SortedByDisplayName()
    {
        var service = new BaseItemTypeService(_mockGameData);

        var types = service.GetBaseItemTypes();

        var names = types.Select(t => t.DisplayName).ToList();
        var sorted = names.OrderBy(n => n).ToList();
        Assert.Equal(sorted, names);
    }

    [Fact]
    public void GetBaseItemTypes_PreservesLabels()
    {
        var service = new BaseItemTypeService(_mockGameData);

        var types = service.GetBaseItemTypes();

        Assert.Contains(types, t => t.Label == "BASE_ITEM_SHORTSWORD");
        Assert.Contains(types, t => t.Label == "BASE_ITEM_LONGSWORD");
    }

    #endregion

    #region Caching with GameData

    [Fact]
    public void GetBaseItemTypes_CalledTwice_ReturnsCachedList()
    {
        var service = new BaseItemTypeService(_mockGameData);

        var types1 = service.GetBaseItemTypes();
        var types2 = service.GetBaseItemTypes();

        Assert.Same(types1, types2);
    }

    [Fact]
    public void GetBaseItemTypes_AfterClearCache_ReloadsFrom2DA()
    {
        var service = new BaseItemTypeService(_mockGameData);
        var types1 = service.GetBaseItemTypes();

        service.ClearCache();
        var types2 = service.GetBaseItemTypes();

        Assert.NotSame(types1, types2);
        Assert.Equal(types1.Count, types2.Count);
    }

    #endregion

    #region Unconfigured GameData Path

    [Fact]
    public void GetBaseItemTypes_UnconfiguredGameData_FallsBackToHardcoded()
    {
        var unconfigured = new MockGameDataService(includeSampleData: false).AsUnconfigured();
        var service = new BaseItemTypeService(unconfigured);

        var types = service.GetBaseItemTypes();

        Assert.NotEmpty(types);
        Assert.True(types.Count > 50, "Unconfigured should use hardcoded fallback");
    }

    [Fact]
    public void GetBaseItemTypes_ConfiguredButNo2DA_FallsBackToHardcoded()
    {
        // Configured but has no baseitems.2da loaded
        var emptyMock = new MockGameDataService(includeSampleData: false);
        var service = new BaseItemTypeService(emptyMock);

        var types = service.GetBaseItemTypes();

        Assert.NotEmpty(types);
        Assert.True(types.Count > 50, "Missing 2DA should use hardcoded fallback");
    }

    #endregion

    #region Row with Empty Label

    [Fact]
    public void GetBaseItemTypes_EmptyLabel_SkipsRow()
    {
        // Add a row with empty label
        _mockGameData.Set2DAValue("baseitems", 8, "label", "****");
        _mockGameData.Set2DAValue("baseitems", 8, "Name", "508");
        _mockGameData.SetTlkString(508, "Empty Label Item");

        var service = new BaseItemTypeService(_mockGameData);
        var types = service.GetBaseItemTypes();

        Assert.DoesNotContain(types, t => t.BaseItemIndex == 8);
    }

    #endregion
}
