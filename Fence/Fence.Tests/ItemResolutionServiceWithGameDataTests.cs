using MerchantEditor.Services;
using Radoub.TestUtilities.Mocks;

namespace Fence.Tests;

/// <summary>
/// Tests for ItemResolutionService with a configured MockGameDataService.
/// Verifies the GetBaseItemTypeName 2DA resolution path and behavior
/// when GameDataService is configured vs unconfigured.
/// Note: FindResource() always returns null in MockGameDataService,
/// so UTI loading from game data can't be tested here — only the
/// base item type name resolution and fallback paths.
/// </summary>
public class ItemResolutionServiceWithGameDataTests
{
    private readonly MockGameDataService _mockGameData;

    public ItemResolutionServiceWithGameDataTests()
    {
        _mockGameData = new MockGameDataService(includeSampleData: true);
        SetupBaseItemsData();
    }

    private void SetupBaseItemsData()
    {
        // baseitems.2da for GetBaseItemTypeName resolution
        // Row 1: Longsword with valid TLK
        _mockGameData.Set2DAValue("baseitems", 1, "label", "BASE_ITEM_LONGSWORD");
        _mockGameData.Set2DAValue("baseitems", 1, "Name", "600");

        // Row 16: Armor with valid TLK
        _mockGameData.Set2DAValue("baseitems", 16, "label", "BASE_ITEM_ARMOR");
        _mockGameData.Set2DAValue("baseitems", 16, "Name", "601");

        // Row 24: Ring with no TLK string (falls back to label)
        _mockGameData.Set2DAValue("baseitems", 24, "label", "BASE_ITEM_RING");
        _mockGameData.Set2DAValue("baseitems", 24, "Name", "602");

        // Row 46: Potions with BadStrRef TLK (falls back to label)
        _mockGameData.Set2DAValue("baseitems", 46, "label", "BASE_ITEM_POTIONS");
        _mockGameData.Set2DAValue("baseitems", 46, "Name", "603");

        // Row 100: Custom item, Name = "****" (falls back to label)
        _mockGameData.Set2DAValue("baseitems", 100, "label", "BASE_ITEM_CUSTOM_SWORD");
        _mockGameData.Set2DAValue("baseitems", 100, "Name", "****");

        // TLK strings
        _mockGameData.SetTlkString(600, "Long Sword");
        _mockGameData.SetTlkString(601, "Armor");
        // 602 not set — returns null
        _mockGameData.SetTlkString(603, "BadStrRef"); // Invalid TLK value
    }

    #region GetBaseItemTypeName via Fallback Data

    [Fact]
    public void ResolveItem_WithGameData_FallbackIncludesBaseItemTypeName()
    {
        // When UTI is not found (FindResource returns null), we still get fallback data
        // The base item type name should still be resolved from 2DA when possible
        var service = new ItemResolutionService(_mockGameData);

        var result = service.ResolveItem("nonexistent_sword");

        Assert.NotNull(result);
        Assert.Equal("nonexistent_sword", result.ResRef);
        Assert.Equal(-1, result.BaseItemType);
        // Fallback uses "Unknown" regardless of game data
        Assert.Equal("Unknown", result.BaseItemTypeName);
    }

    #endregion

    #region Configured vs Null GameDataService

    [Fact]
    public void Constructor_WithConfiguredGameData_DoesNotThrow()
    {
        var service = new ItemResolutionService(_mockGameData);
        Assert.NotNull(service);
    }

    [Fact]
    public void Constructor_WithUnconfiguredGameData_DoesNotThrow()
    {
        var unconfigured = new MockGameDataService(includeSampleData: false).AsUnconfigured();
        var service = new ItemResolutionService(unconfigured);
        Assert.NotNull(service);
    }

    [Fact]
    public void ResolveItem_WithConfiguredGameData_StillReturnsFallbackWhenNotFound()
    {
        // MockGameDataService.FindResource always returns null
        // So even with configured game data, items not found on disk get fallback
        var service = new ItemResolutionService(_mockGameData);

        var result = service.ResolveItem("missing_item");

        Assert.NotNull(result);
        Assert.Equal("missing_item", result.DisplayName);
        Assert.Equal("Unknown", result.BaseItemTypeName);
        Assert.Equal(0, result.BaseCost);
    }

    [Fact]
    public void ResolveItem_WithUnconfiguredGameData_ReturnsFallback()
    {
        var unconfigured = new MockGameDataService(includeSampleData: false).AsUnconfigured();
        var service = new ItemResolutionService(unconfigured);

        var result = service.ResolveItem("test_item");

        Assert.NotNull(result);
        Assert.Equal("test_item", result.ResRef);
        Assert.Equal("Unknown", result.BaseItemTypeName);
    }

    #endregion

    #region Caching with GameData

    [Fact]
    public void ResolveItem_WithGameData_CachesResults()
    {
        var service = new ItemResolutionService(_mockGameData);

        var result1 = service.ResolveItem("cached_item");
        var result2 = service.ResolveItem("cached_item");

        Assert.Same(result1, result2);
    }

    [Fact]
    public void ClearCache_WithGameData_ForcesFreshResolve()
    {
        var service = new ItemResolutionService(_mockGameData);
        var result1 = service.ResolveItem("cached_item");

        service.ClearCache();
        var result2 = service.ResolveItem("cached_item");

        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.NotSame(result1, result2);
        Assert.Equal(result1.ResRef, result2.ResRef);
    }

    [Fact]
    public void ResolveItem_WithGameData_CaseInsensitiveCache()
    {
        var service = new ItemResolutionService(_mockGameData);

        var result1 = service.ResolveItem("Test_Item");
        var result2 = service.ResolveItem("test_item");
        var result3 = service.ResolveItem("TEST_ITEM");

        Assert.Same(result1, result2);
        Assert.Same(result2, result3);
    }

    #endregion

    #region ResolveItems with GameData

    [Fact]
    public void ResolveItems_WithGameData_ResolvesMultiple()
    {
        var service = new ItemResolutionService(_mockGameData);
        var resRefs = new[] { "sword_01", "armor_01", "ring_01" };

        var results = service.ResolveItems(resRefs);

        Assert.Equal(3, results.Count);
        Assert.All(results.Values, r => Assert.Equal("Unknown", r.BaseItemTypeName));
    }

    [Fact]
    public void ResolveItems_WithGameData_SkipsNullAndEmpty()
    {
        var service = new ItemResolutionService(_mockGameData);
        var resRefs = new[] { "item1", null!, "", "item2" };

        var results = service.ResolveItems(resRefs);

        Assert.Equal(2, results.Count);
    }

    #endregion

    #region SetCurrentFilePath with GameData

    [Fact]
    public void SetCurrentFilePath_WithGameData_ClearsCacheOnChange()
    {
        var service = new ItemResolutionService(_mockGameData);
        var result1 = service.ResolveItem("test_item");

        service.SetCurrentFilePath(@"C:\module\store.utm");
        var result2 = service.ResolveItem("test_item");

        Assert.NotSame(result1, result2);
    }

    [Fact]
    public void SetCurrentFilePath_Null_WithGameData_DoesNotThrow()
    {
        var service = new ItemResolutionService(_mockGameData);
        service.SetCurrentFilePath(null);
        // Should work fine
        var result = service.ResolveItem("test_item");
        Assert.NotNull(result);
    }

    #endregion
}
