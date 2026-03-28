using MerchantEditor.Services;

namespace Fence.Tests;

public class ItemResolutionServiceTests
{
    [Fact]
    public void ResolveItem_NullResRef_ReturnsNull()
    {
        // Arrange
        var service = new ItemResolutionService(null);

        // Act
        var result = service.ResolveItem(null!);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void ResolveItem_EmptyResRef_ReturnsNull()
    {
        // Arrange
        var service = new ItemResolutionService(null);

        // Act
        var result = service.ResolveItem(string.Empty);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void ResolveItem_NonExistentItem_ReturnsFallbackData()
    {
        // Arrange
        var service = new ItemResolutionService(null);

        // Act
        var result = service.ResolveItem("nonexistent_item");

        // Assert
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
        // Arrange
        var service = new ItemResolutionService(null);

        // Act
        var result1 = service.ResolveItem("test_item");
        var result2 = service.ResolveItem("test_item");

        // Assert
        Assert.Same(result1, result2); // Same instance = cached
    }

    [Fact]
    public void ResolveItem_CaseInsensitive_ReturnsCachedData()
    {
        // Arrange
        var service = new ItemResolutionService(null);

        // Act
        var result1 = service.ResolveItem("Test_Item");
        var result2 = service.ResolveItem("TEST_ITEM");
        var result3 = service.ResolveItem("test_item");

        // Assert
        Assert.Same(result1, result2);
        Assert.Same(result2, result3);
    }

    [Fact]
    public void ClearCache_AfterResolve_ForcesFreshLoad()
    {
        // Arrange
        var service = new ItemResolutionService(null);
        var result1 = service.ResolveItem("test_item");

        // Act
        service.ClearCache();
        var result2 = service.ResolveItem("test_item");

        // Assert
        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.NotSame(result1, result2); // Different instance after cache clear
        Assert.Equal(result1.ResRef, result2.ResRef); // Same data
    }

    [Fact]
    public void SetCurrentFilePath_Null_ClearsModuleDirectory()
    {
        // Arrange
        var service = new ItemResolutionService(null);

        // Act & Assert - should not throw
        service.SetCurrentFilePath(null);
    }

    [Fact]
    public void SetCurrentFilePath_ValidPath_ClearsCacheAndSetsDirectory()
    {
        // Arrange
        var service = new ItemResolutionService(null);
        var result1 = service.ResolveItem("test_item");

        // Act
        service.SetCurrentFilePath(@"C:\test\module\store.utm");
        var result2 = service.ResolveItem("test_item");

        // Assert
        Assert.NotSame(result1, result2); // Cache was cleared
    }

    [Fact]
    public void ResolveItems_MultipleResRefs_ResolvesAll()
    {
        // Arrange
        var service = new ItemResolutionService(null);
        var resRefs = new[] { "item1", "item2", "item3" };

        // Act
        var results = service.ResolveItems(resRefs);

        // Assert
        Assert.Equal(3, results.Count);
        Assert.True(results.ContainsKey("item1"));
        Assert.True(results.ContainsKey("item2"));
        Assert.True(results.ContainsKey("item3"));
    }

    [Fact]
    public void ResolveItems_EmptyList_ReturnsEmptyDictionary()
    {
        // Arrange
        var service = new ItemResolutionService(null);

        // Act
        var results = service.ResolveItems(Array.Empty<string>());

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public void ResolveItems_WithNullResRefs_SkipsNulls()
    {
        // Arrange
        var service = new ItemResolutionService(null);
        var resRefs = new[] { "item1", null!, "", "item2" };

        // Act
        var results = service.ResolveItems(resRefs);

        // Assert
        Assert.Equal(2, results.Count);
        Assert.True(results.ContainsKey("item1"));
        Assert.True(results.ContainsKey("item2"));
    }

    #region ResolvedItemData Tests

    [Fact]
    public void ResolvedItemData_CalculateSellPrice_100Markup_ReturnsBaseCost()
    {
        // Arrange
        var data = CreateResolvedItemData(baseCost: 100);

        // Act
        var sellPrice = data.CalculateSellPrice(100);

        // Assert
        Assert.Equal(100, sellPrice);
    }

    [Fact]
    public void ResolvedItemData_CalculateSellPrice_150Markup_Returns150Percent()
    {
        // Arrange
        var data = CreateResolvedItemData(baseCost: 100);

        // Act
        var sellPrice = data.CalculateSellPrice(150);

        // Assert
        Assert.Equal(150, sellPrice);
    }

    [Fact]
    public void ResolvedItemData_CalculateSellPrice_RoundsUp()
    {
        // Arrange
        var data = CreateResolvedItemData(baseCost: 10);

        // Act
        var sellPrice = data.CalculateSellPrice(115); // 10 * 1.15 = 11.5

        // Assert
        Assert.Equal(12, sellPrice); // Ceiling rounds up
    }

    [Fact]
    public void ResolvedItemData_CalculateBuyPrice_100Markdown_ReturnsBaseCost()
    {
        // Arrange
        var data = CreateResolvedItemData(baseCost: 100);

        // Act
        var buyPrice = data.CalculateBuyPrice(100);

        // Assert
        Assert.Equal(100, buyPrice);
    }

    [Fact]
    public void ResolvedItemData_CalculateBuyPrice_50Markdown_ReturnsHalf()
    {
        // Arrange
        var data = CreateResolvedItemData(baseCost: 100);

        // Act
        var buyPrice = data.CalculateBuyPrice(50);

        // Assert
        Assert.Equal(50, buyPrice);
    }

    [Fact]
    public void ResolvedItemData_CalculateBuyPrice_RoundsDown()
    {
        // Arrange
        var data = CreateResolvedItemData(baseCost: 10);

        // Act
        var buyPrice = data.CalculateBuyPrice(55); // 10 * 0.55 = 5.5

        // Assert
        Assert.Equal(5, buyPrice); // Floor rounds down
    }

    [Fact]
    public void ResolvedItemData_CalculateBuyPrice_ZeroBaseCost_ReturnsZero()
    {
        // Arrange
        var data = CreateResolvedItemData(baseCost: 0);

        // Act
        var buyPrice = data.CalculateBuyPrice(50);

        // Assert
        Assert.Equal(0, buyPrice);
    }

    [Theory]
    [InlineData(100, 125, 125)]  // Standard markup
    [InlineData(100, 200, 200)]  // Double price
    [InlineData(50, 150, 75)]    // 150% of 50
    [InlineData(1000, 100, 1000)] // No markup
    public void ResolvedItemData_CalculateSellPrice_VariousScenarios(int baseCost, int markup, int expected)
    {
        // Arrange
        var data = CreateResolvedItemData(baseCost: baseCost);

        // Act
        var sellPrice = data.CalculateSellPrice(markup);

        // Assert
        Assert.Equal(expected, sellPrice);
    }

    [Theory]
    [InlineData(100, 50, 50)]   // Half price
    [InlineData(100, 25, 25)]   // Quarter price
    [InlineData(1000, 75, 750)] // 75% of 1000
    [InlineData(100, 100, 100)] // Full price
    public void ResolvedItemData_CalculateBuyPrice_VariousScenarios(int baseCost, int markdown, int expected)
    {
        // Arrange
        var data = CreateResolvedItemData(baseCost: baseCost);

        // Act
        var buyPrice = data.CalculateBuyPrice(markdown);

        // Assert
        Assert.Equal(expected, buyPrice);
    }

    #endregion

    #region PropertiesDisplay Tests

    [Fact]
    public void ResolvedItemData_PropertiesDisplay_DefaultsToEmpty()
    {
        // Arrange & Act
        var data = CreateResolvedItemData(baseCost: 100);

        // Assert
        Assert.Equal(string.Empty, data.PropertiesDisplay);
    }

    [Fact]
    public void ResolvedItemData_PropertiesDisplay_CanBeSet()
    {
        // Arrange & Act
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

        // Assert
        Assert.Equal("Enhancement Bonus +1; Damage Bonus Fire 1d6", data.PropertiesDisplay);
    }

    [Fact]
    public void ResolveItem_FallbackData_HasEmptyPropertiesDisplay()
    {
        // Arrange
        var service = new ItemResolutionService(null);

        // Act
        var result = service.ResolveItem("nonexistent_item");

        // Assert
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
            BaseItemType = 4, // Longsword
            BaseItemTypeName = "Longsword",
            BaseCost = baseCost,
            StackSize = 1,
            Plot = false,
            Cursed = false
        };
    }

    #endregion
}
