using MerchantEditor.Services;
using Radoub.Formats.Utm;

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
        Assert.Equal(4, info.StorePanel); // Default is miscellaneous
    }

    [Fact]
    public void BaseItemTypeInfo_StorePanel_SetFromConstructor()
    {
        var info = new BaseItemTypeInfo(16, "Armor", "BASE_ITEM_ARMOR", 0);
        Assert.Equal(0, info.StorePanel);
    }

    [Theory]
    [InlineData(0, StorePanels.Armor)]       // 2DA armor → UTM Armor
    [InlineData(1, StorePanels.Weapons)]      // 2DA weapons → UTM Weapons
    [InlineData(2, StorePanels.Potions)]      // 2DA potions → UTM Potions
    [InlineData(3, StorePanels.Potions)]      // 2DA scrolls → UTM Potions (shared)
    [InlineData(4, StorePanels.Miscellaneous)] // 2DA misc → UTM Miscellaneous
    [InlineData(99, StorePanels.Miscellaneous)] // Unknown → UTM Miscellaneous
    public void GetUtmStorePanel_MapsCorrectly(int twoDaValue, int expectedUtmPanel)
    {
        Assert.Equal(expectedUtmPanel, BaseItemTypeService.GetUtmStorePanel(twoDaValue));
    }

    [Fact]
    public void GetStorePanelForBaseItem_Longsword_ReturnsWeapons()
    {
        var service = new BaseItemTypeService(null);
        // Longsword is index 1, StorePanel=1 (weapons) → UTM Weapons (4)
        Assert.Equal(StorePanels.Weapons, service.GetStorePanelForBaseItem(1));
    }

    [Fact]
    public void GetStorePanelForBaseItem_Armor_ReturnsArmor()
    {
        var service = new BaseItemTypeService(null);
        // Armor is index 16, StorePanel=0 (armor) → UTM Armor (0)
        Assert.Equal(StorePanels.Armor, service.GetStorePanelForBaseItem(16));
    }

    [Fact]
    public void GetStorePanelForBaseItem_Potions_ReturnsPotions()
    {
        var service = new BaseItemTypeService(null);
        // Potions is index 46, StorePanel=2 (potions) → UTM Potions (2)
        Assert.Equal(StorePanels.Potions, service.GetStorePanelForBaseItem(46));
    }

    [Fact]
    public void GetStorePanelForBaseItem_UnknownIndex_ReturnsMiscellaneous()
    {
        var service = new BaseItemTypeService(null);
        Assert.Equal(StorePanels.Miscellaneous, service.GetStorePanelForBaseItem(9999));
    }

    [Fact]
    public void HardcodedTypes_HaveStorePanelValues()
    {
        var service = new BaseItemTypeService(null);
        var types = service.GetBaseItemTypes();

        // Verify some known types have correct StorePanel values
        var longsword = types.First(t => t.BaseItemIndex == 1);
        Assert.Equal(1, longsword.StorePanel); // weapons

        var armor = types.First(t => t.BaseItemIndex == 16);
        Assert.Equal(0, armor.StorePanel); // armor

        var potions = types.First(t => t.BaseItemIndex == 46);
        Assert.Equal(2, potions.StorePanel); // potions

        var ring = types.First(t => t.BaseItemIndex == 24);
        Assert.Equal(4, ring.StorePanel); // misc (rings don't have their own 2DA panel)
    }
}
