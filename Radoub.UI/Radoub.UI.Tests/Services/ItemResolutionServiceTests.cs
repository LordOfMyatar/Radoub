using Radoub.UI.Services;
using Xunit;

namespace Radoub.UI.Tests.Services;

public class ItemResolutionServiceTests
{
    [Fact]
    public void ResolveItem_NullResRef_ReturnsNull()
    {
        var service = new ItemResolutionService(null);

        var result = service.ResolveItem(null!);

        Assert.Null(result);
    }

    [Fact]
    public void ResolveItem_EmptyResRef_ReturnsNull()
    {
        var service = new ItemResolutionService(null);

        var result = service.ResolveItem(string.Empty);

        Assert.Null(result);
    }

    [Fact]
    public void ResolveItem_NonExistentItem_ReturnsFallbackData()
    {
        var service = new ItemResolutionService(null);

        var result = service.ResolveItem("nonexistent_item");

        Assert.NotNull(result);
        Assert.Equal("nonexistent_item", result.ResRef);
        Assert.Equal("nonexistent_item", result.Tag);
        Assert.Equal("nonexistent_item", result.DisplayName);
        Assert.Equal(-1, result.BaseItemType);
        Assert.Equal("Unknown", result.BaseItemTypeName);
        Assert.Equal(0, result.BaseCost);
    }

    [Fact]
    public void ResolveItem_SameResRefTwice_ReturnsCachedData()
    {
        var service = new ItemResolutionService(null);

        var result1 = service.ResolveItem("test_item");
        var result2 = service.ResolveItem("test_item");

        Assert.Same(result1, result2);
    }

    [Fact]
    public void ResolveItem_CaseInsensitive_ReturnsCachedData()
    {
        var service = new ItemResolutionService(null);

        var result1 = service.ResolveItem("Test_Item");
        var result2 = service.ResolveItem("TEST_ITEM");
        var result3 = service.ResolveItem("test_item");

        Assert.Same(result1, result2);
        Assert.Same(result2, result3);
    }

    [Fact]
    public void ClearCache_AfterResolve_ForcesFreshLoad()
    {
        var service = new ItemResolutionService(null);
        var result1 = service.ResolveItem("test_item");

        service.ClearCache();
        var result2 = service.ResolveItem("test_item");

        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.NotSame(result1, result2);
        Assert.Equal(result1.ResRef, result2.ResRef);
    }

    [Fact]
    public void SetCurrentFilePath_Null_ClearsModuleDirectory()
    {
        var service = new ItemResolutionService(null);

        service.SetCurrentFilePath(null);
    }

    [Fact]
    public void SetCurrentFilePath_ValidPath_ClearsCacheAndSetsDirectory()
    {
        var service = new ItemResolutionService(null);
        var result1 = service.ResolveItem("test_item");

        service.SetCurrentFilePath(@"C:\test\module\store.utm");
        var result2 = service.ResolveItem("test_item");

        Assert.NotSame(result1, result2);
    }

    [Fact]
    public void SetModuleDirectory_SetsDirectoryAndClearsCache()
    {
        var service = new ItemResolutionService(null);
        var result1 = service.ResolveItem("test_item");

        service.SetModuleDirectory(@"C:\test\module");
        var result2 = service.ResolveItem("test_item");

        Assert.NotSame(result1, result2);
    }

    [Fact]
    public void SetModuleDirectory_Null_DoesNotThrow()
    {
        var service = new ItemResolutionService(null);

        service.SetModuleDirectory(null);

        var result = service.ResolveItem("test_item");
        Assert.NotNull(result);
    }

    [Fact]
    public void ResolveItems_MultipleResRefs_ResolvesAll()
    {
        var service = new ItemResolutionService(null);
        var resRefs = new[] { "item1", "item2", "item3" };

        var results = service.ResolveItems(resRefs);

        Assert.Equal(3, results.Count);
        Assert.True(results.ContainsKey("item1"));
        Assert.True(results.ContainsKey("item2"));
        Assert.True(results.ContainsKey("item3"));
    }

    [Fact]
    public void ResolveItems_EmptyList_ReturnsEmptyDictionary()
    {
        var service = new ItemResolutionService(null);

        var results = service.ResolveItems(Array.Empty<string>());

        Assert.Empty(results);
    }

    [Fact]
    public void ResolveItems_WithNullResRefs_SkipsNulls()
    {
        var service = new ItemResolutionService(null);
        var resRefs = new[] { "item1", null!, "", "item2" };

        var results = service.ResolveItems(resRefs);

        Assert.Equal(2, results.Count);
        Assert.True(results.ContainsKey("item1"));
        Assert.True(results.ContainsKey("item2"));
    }

    #region ResolvedItemData Tests

    [Fact]
    public void ResolvedItemData_CalculateSellPrice_100Markup_ReturnsBaseCost()
    {
        var data = CreateResolvedItemData(baseCost: 100);

        var sellPrice = data.CalculateSellPrice(100);

        Assert.Equal(100, sellPrice);
    }

    [Fact]
    public void ResolvedItemData_CalculateSellPrice_150Markup_Returns150Percent()
    {
        var data = CreateResolvedItemData(baseCost: 100);

        var sellPrice = data.CalculateSellPrice(150);

        Assert.Equal(150, sellPrice);
    }

    [Fact]
    public void ResolvedItemData_CalculateSellPrice_RoundsUp()
    {
        var data = CreateResolvedItemData(baseCost: 10);

        var sellPrice = data.CalculateSellPrice(115);

        Assert.Equal(12, sellPrice);
    }

    [Fact]
    public void ResolvedItemData_CalculateBuyPrice_100Markdown_ReturnsBaseCost()
    {
        var data = CreateResolvedItemData(baseCost: 100);

        var buyPrice = data.CalculateBuyPrice(100);

        Assert.Equal(100, buyPrice);
    }

    [Fact]
    public void ResolvedItemData_CalculateBuyPrice_50Markdown_ReturnsHalf()
    {
        var data = CreateResolvedItemData(baseCost: 100);

        var buyPrice = data.CalculateBuyPrice(50);

        Assert.Equal(50, buyPrice);
    }

    [Fact]
    public void ResolvedItemData_CalculateBuyPrice_RoundsDown()
    {
        var data = CreateResolvedItemData(baseCost: 10);

        var buyPrice = data.CalculateBuyPrice(55);

        Assert.Equal(5, buyPrice);
    }

    [Fact]
    public void ResolvedItemData_CalculateBuyPrice_ZeroBaseCost_ReturnsZero()
    {
        var data = CreateResolvedItemData(baseCost: 0);

        var buyPrice = data.CalculateBuyPrice(50);

        Assert.Equal(0, buyPrice);
    }

    [Theory]
    [InlineData(100, 125, 125)]
    [InlineData(100, 200, 200)]
    [InlineData(50, 150, 75)]
    [InlineData(1000, 100, 1000)]
    public void ResolvedItemData_CalculateSellPrice_VariousScenarios(int baseCost, int markup, int expected)
    {
        var data = CreateResolvedItemData(baseCost: baseCost);

        var sellPrice = data.CalculateSellPrice(markup);

        Assert.Equal(expected, sellPrice);
    }

    [Theory]
    [InlineData(100, 50, 50)]
    [InlineData(100, 25, 25)]
    [InlineData(1000, 75, 750)]
    [InlineData(100, 100, 100)]
    public void ResolvedItemData_CalculateBuyPrice_VariousScenarios(int baseCost, int markdown, int expected)
    {
        var data = CreateResolvedItemData(baseCost: baseCost);

        var buyPrice = data.CalculateBuyPrice(markdown);

        Assert.Equal(expected, buyPrice);
    }

    #endregion

    #region PropertiesDisplay Tests

    [Fact]
    public void ResolvedItemData_PropertiesDisplay_DefaultsToEmpty()
    {
        var data = CreateResolvedItemData(baseCost: 100);

        Assert.Equal(string.Empty, data.PropertiesDisplay);
    }

    [Fact]
    public void ResolvedItemData_PropertiesDisplay_CanBeSet()
    {
        var data = new ResolvedItemData
        {
            ResRef = "test_item",
            Tag = "TEST_TAG",
            DisplayName = "Test Item",
            BaseItemType = 4,
            BaseItemTypeName = "Longsword",
            BaseCost = 100,
            StackSize = 1,
            Plot = false,
            Cursed = false,
            PropertiesDisplay = "Enhancement Bonus +1; Damage Bonus Fire 1d6"
        };

        Assert.Equal("Enhancement Bonus +1; Damage Bonus Fire 1d6", data.PropertiesDisplay);
    }

    [Fact]
    public void ResolveItem_FallbackData_HasEmptyPropertiesDisplay()
    {
        var service = new ItemResolutionService(null);

        var result = service.ResolveItem("nonexistent_item");

        Assert.NotNull(result);
        Assert.Equal(string.Empty, result.PropertiesDisplay);
    }

    #endregion

    #region Test Helpers

    private static ResolvedItemData CreateResolvedItemData(int baseCost)
    {
        return new ResolvedItemData
        {
            ResRef = "test_item",
            Tag = "TEST_TAG",
            DisplayName = "Test Item",
            BaseItemType = 4,
            BaseItemTypeName = "Longsword",
            BaseCost = baseCost,
            StackSize = 1,
            Plot = false,
            Cursed = false
        };
    }

    #endregion
}
