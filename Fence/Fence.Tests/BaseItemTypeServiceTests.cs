using MerchantEditor.Services;

namespace Fence.Tests;

public class BaseItemTypeServiceTests
{
    [Fact]
    public void GetBaseItemTypes_WithoutGameData_ReturnsHardcodedTypes()
    {
        // Arrange
        var service = new BaseItemTypeService(null);

        // Act
        var types = service.GetBaseItemTypes();

        // Assert
        Assert.NotEmpty(types);
        Assert.True(types.Count > 50, "Should have many hardcoded base item types");
    }

    [Fact]
    public void GetBaseItemTypes_CalledTwice_ReturnsSameCount()
    {
        // Arrange
        var service = new BaseItemTypeService(null);

        // Act
        var types1 = service.GetBaseItemTypes();
        var types2 = service.GetBaseItemTypes();

        // Assert - should return same number of items
        Assert.Equal(types1.Count, types2.Count);
    }

    [Fact]
    public void GetBaseItemTypes_AfterClearCache_ReturnsNewList()
    {
        // Arrange
        var service = new BaseItemTypeService(null);
        var types1 = service.GetBaseItemTypes();

        // Act
        service.ClearCache();
        var types2 = service.GetBaseItemTypes();

        // Assert
        Assert.NotSame(types1, types2);
        Assert.Equal(types1.Count, types2.Count);
    }

    [Fact]
    public void GetBaseItemTypes_HardcodedTypes_AreSortedByDisplayName()
    {
        // Arrange
        var service = new BaseItemTypeService(null);

        // Act
        var types = service.GetBaseItemTypes();

        // Assert
        var sortedNames = types.Select(t => t.DisplayName).OrderBy(n => n).ToList();
        var actualNames = types.Select(t => t.DisplayName).ToList();
        Assert.Equal(sortedNames, actualNames);
    }

    [Fact]
    public void GetBaseItemTypes_ContainsCommonTypes()
    {
        // Arrange
        var service = new BaseItemTypeService(null);

        // Act
        var types = service.GetBaseItemTypes();

        // Assert
        Assert.Contains(types, t => t.DisplayName == "Longsword");
        Assert.Contains(types, t => t.DisplayName == "Armor");
        Assert.Contains(types, t => t.DisplayName == "Potions");
        Assert.Contains(types, t => t.DisplayName == "Ring");
    }

    [Fact]
    public void BaseItemTypeInfo_ToString_ReturnsDisplayName()
    {
        // Arrange
        var info = new BaseItemTypeInfo(1, "Test Sword", "BASE_ITEM_TESTSWORD");

        // Act & Assert
        Assert.Equal("Test Sword", info.ToString());
    }

    [Fact]
    public void BaseItemTypeInfo_Properties_SetCorrectly()
    {
        // Arrange & Act
        var info = new BaseItemTypeInfo(42, "Magic Wand", "BASE_ITEM_MAGICWAND");

        // Assert
        Assert.Equal(42, info.BaseItemIndex);
        Assert.Equal("Magic Wand", info.DisplayName);
        Assert.Equal("BASE_ITEM_MAGICWAND", info.Label);
    }
}
