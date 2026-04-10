using Radoub.Formats.Services;
using Radoub.Formats.TwoDA;
using Radoub.Formats.Utm;
using Radoub.TestUtilities.Mocks;
using Xunit;

namespace Radoub.Formats.Tests;

/// <summary>
/// Tests for the shared BaseItemTypeService.
/// Covers both Fence concerns (StorePanel, UTM mapping) and Relique concerns (ModelType, Stacking, Charges).
/// </summary>
public class BaseItemTypeServiceTests
{
    #region Hardcoded fallback tests (from Fence)

    [Fact]
    public void GetBaseItemTypes_WithoutGameData_ReturnsHardcodedTypes()
    {
        var service = new BaseItemTypeService(null);
        var types = service.GetBaseItemTypes();

        Assert.NotEmpty(types);
        Assert.True(types.Count > 50, "Should have many hardcoded base item types");
    }

    [Fact]
    public void GetBaseItemTypes_CalledTwice_ReturnsSameCount()
    {
        var service = new BaseItemTypeService(null);
        var types1 = service.GetBaseItemTypes();
        var types2 = service.GetBaseItemTypes();

        Assert.Equal(types1.Count, types2.Count);
    }

    [Fact]
    public void GetBaseItemTypes_AfterClearCache_ReturnsNewList()
    {
        var service = new BaseItemTypeService(null);
        var types1 = service.GetBaseItemTypes();

        service.ClearCache();
        var types2 = service.GetBaseItemTypes();

        Assert.NotSame(types1, types2);
        Assert.Equal(types1.Count, types2.Count);
    }

    [Fact]
    public void GetBaseItemTypes_HardcodedTypes_AreSortedByDisplayName()
    {
        var service = new BaseItemTypeService(null);
        var types = service.GetBaseItemTypes();

        var sortedNames = types.Select(t => t.DisplayName).OrderBy(n => n).ToList();
        var actualNames = types.Select(t => t.DisplayName).ToList();
        Assert.Equal(sortedNames, actualNames);
    }

    [Fact]
    public void GetBaseItemTypes_ContainsCommonTypes()
    {
        var service = new BaseItemTypeService(null);
        var types = service.GetBaseItemTypes();

        Assert.Contains(types, t => t.DisplayName == "Longsword");
        Assert.Contains(types, t => t.DisplayName == "Armor");
        Assert.Contains(types, t => t.DisplayName == "Potions");
        Assert.Contains(types, t => t.DisplayName == "Ring");
    }

    #endregion

    #region BaseItemTypeInfo properties

    [Fact]
    public void BaseItemTypeInfo_ToString_ReturnsDisplayName()
    {
        var info = new BaseItemTypeInfo(1, "Test Sword", "BASE_ITEM_TESTSWORD");
        Assert.Equal("Test Sword", info.ToString());
    }

    [Fact]
    public void BaseItemTypeInfo_DefaultProperties()
    {
        var info = new BaseItemTypeInfo(42, "Magic Wand", "BASE_ITEM_MAGICWAND");

        Assert.Equal(42, info.BaseItemIndex);
        Assert.Equal("Magic Wand", info.DisplayName);
        Assert.Equal("BASE_ITEM_MAGICWAND", info.Label);
        Assert.Equal(4, info.StorePanel); // Default is miscellaneous
        Assert.Equal(0, info.ModelType);
        Assert.Equal(1, info.Stacking);
        Assert.Equal(0, info.ChargesStarting);
    }

    [Fact]
    public void BaseItemTypeInfo_StorePanel_SetFromConstructor()
    {
        var info = new BaseItemTypeInfo(16, "Armor", "BASE_ITEM_ARMOR", storePanel: 0);
        Assert.Equal(0, info.StorePanel);
    }

    #endregion

    #region StorePanel mapping (Fence concerns)

    [Theory]
    [InlineData(0, StorePanels.Armor)]
    [InlineData(1, StorePanels.Weapons)]
    [InlineData(2, StorePanels.Potions)]
    [InlineData(3, StorePanels.Potions)]       // Scrolls share Potions panel
    [InlineData(4, StorePanels.Miscellaneous)]
    [InlineData(99, StorePanels.Miscellaneous)] // Unknown defaults to misc
    public void GetUtmStorePanel_MapsCorrectly(int twoDaValue, int expectedUtmPanel)
    {
        Assert.Equal(expectedUtmPanel, BaseItemTypeService.GetUtmStorePanel(twoDaValue));
    }

    [Fact]
    public void GetStorePanelForBaseItem_Longsword_ReturnsWeapons()
    {
        var service = new BaseItemTypeService(null);
        Assert.Equal(StorePanels.Weapons, service.GetStorePanelForBaseItem(1));
    }

    [Fact]
    public void GetStorePanelForBaseItem_Armor_ReturnsArmor()
    {
        var service = new BaseItemTypeService(null);
        Assert.Equal(StorePanels.Armor, service.GetStorePanelForBaseItem(16));
    }

    [Fact]
    public void GetStorePanelForBaseItem_Potions_ReturnsPotions()
    {
        var service = new BaseItemTypeService(null);
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

        var longsword = types.First(t => t.BaseItemIndex == 1);
        Assert.Equal(1, longsword.StorePanel); // weapons

        var armor = types.First(t => t.BaseItemIndex == 16);
        Assert.Equal(0, armor.StorePanel); // armor

        var potions = types.First(t => t.BaseItemIndex == 46);
        Assert.Equal(2, potions.StorePanel); // potions

        var ring = types.First(t => t.BaseItemIndex == 24);
        Assert.Equal(4, ring.StorePanel); // misc
    }

    #endregion

    #region Stacking and Charges (Relique concerns)

    [Fact]
    public void IsStackable_TrueWhenStackingGreaterThan1()
    {
        var info = new BaseItemTypeInfo(25, "Arrow", "BASE_ITEM_ARROW", stacking: 99);
        Assert.True(info.IsStackable);
        Assert.False(info.HasCharges);
    }

    [Fact]
    public void HasCharges_TrueWhenChargesStartingGreaterThan0()
    {
        var info = new BaseItemTypeInfo(43, "Magic Wand", "BASE_ITEM_MAGICWAND", chargesStarting: 50);
        Assert.False(info.IsStackable);
        Assert.True(info.HasCharges);
    }

    [Fact]
    public void SingleItem_NeitherStackableNorCharges()
    {
        var info = new BaseItemTypeInfo(1, "Longsword", "BASE_ITEM_LONGSWORD");
        Assert.False(info.IsStackable);
        Assert.False(info.HasCharges);
    }

    [Fact]
    public void HardcodedTypes_HaveStackingAndChargesValues()
    {
        var service = new BaseItemTypeService(null);
        var types = service.GetBaseItemTypes();

        var arrow = types.First(t => t.BaseItemIndex == 25);
        Assert.Equal(99, arrow.Stacking);
        Assert.True(arrow.IsStackable);

        var wand = types.First(t => t.BaseItemIndex == 43);
        Assert.Equal(50, wand.ChargesStarting);
        Assert.True(wand.HasCharges);

        var sword = types.First(t => t.BaseItemIndex == 1);
        Assert.Equal(1, sword.Stacking);
        Assert.False(sword.IsStackable);
    }

    #endregion

    #region ModelType (Relique concerns)

    [Fact]
    public void HardcodedTypes_HaveModelTypeValues()
    {
        var service = new BaseItemTypeService(null);
        var types = service.GetBaseItemTypes();

        var armor = types.First(t => t.BaseItemIndex == 16);
        Assert.Equal(3, armor.ModelType); // Armor model type
        Assert.True(armor.HasArmorParts);
        Assert.True(armor.HasColorFields);

        var twoBladed = types.First(t => t.BaseItemIndex == 12);
        Assert.Equal(2, twoBladed.ModelType); // Composite
        Assert.True(twoBladed.HasMultipleModelParts);
    }

    #endregion

    #region Inventory Size (Relique icon picker)

    [Fact]
    public void BaseItemTypeInfo_DefaultInventorySize_Is1x1()
    {
        var info = new BaseItemTypeInfo(1, "Longsword", "BASE_ITEM_LONGSWORD");
        Assert.Equal(1, info.InvSlotWidth);
        Assert.Equal(1, info.InvSlotHeight);
    }

    [Fact]
    public void BaseItemTypeInfo_InventorySize_SetFromConstructor()
    {
        var info = new BaseItemTypeInfo(10, "Halberd", "BASE_ITEM_HALBERD",
            invSlotWidth: 1, invSlotHeight: 3);
        Assert.Equal(1, info.InvSlotWidth);
        Assert.Equal(3, info.InvSlotHeight);
    }

    [Fact]
    public void GetBaseItemTypes_From2DA_ParsesInventorySize()
    {
        var mockGameData = new MockGameDataService(includeSampleData: false);
        var columns = new[] { "label", "Name", "ModelType", "Stacking", "Description",
            "ChargesStarting", "StorePanel", "InvSlotWidth", "InvSlotHeight" };
        var twoDA = new TwoDAFile { Columns = new System.Collections.Generic.List<string>(columns) };

        // Row 0: Longsword (1x3)
        twoDA.Rows.Add(new TwoDARow { Values = new System.Collections.Generic.List<string?>
            { "BASE_ITEM_LONGSWORD", "****", "0", "1", "****", "****", "1", "1", "3" } });
        // Row 1: Large Shield (2x3)
        twoDA.Rows.Add(new TwoDARow { Values = new System.Collections.Generic.List<string?>
            { "BASE_ITEM_LARGESHIELD", "****", "0", "1", "****", "****", "0", "2", "3" } });
        // Row 2: Ring (missing columns → default 1x1)
        twoDA.Rows.Add(new TwoDARow { Values = new System.Collections.Generic.List<string?>
            { "BASE_ITEM_RING", "****", "0", "1", "****", "****", "4", "****", "****" } });

        mockGameData.With2DA("baseitems", twoDA);

        var service = new BaseItemTypeService(mockGameData);
        var types = service.GetBaseItemTypes();

        var longsword = types.Find(t => t.BaseItemIndex == 0);
        Assert.NotNull(longsword);
        Assert.Equal(1, longsword.InvSlotWidth);
        Assert.Equal(3, longsword.InvSlotHeight);

        var shield = types.Find(t => t.BaseItemIndex == 1);
        Assert.NotNull(shield);
        Assert.Equal(2, shield.InvSlotWidth);
        Assert.Equal(3, shield.InvSlotHeight);

        var ring = types.Find(t => t.BaseItemIndex == 2);
        Assert.NotNull(ring);
        Assert.Equal(1, ring.InvSlotWidth);
        Assert.Equal(1, ring.InvSlotHeight);
    }

    #endregion

    #region 2DA parsing tests (from Relique)

    [Fact]
    public void GetBaseItemTypes_From2DA_ParsesStackingAndCharges()
    {
        var mockGameData = new MockGameDataService(includeSampleData: false);
        var columns = new[] { "label", "Name", "ModelType", "Stacking", "Description", "ChargesStarting", "StorePanel" };
        var twoDA = new TwoDAFile { Columns = new System.Collections.Generic.List<string>(columns) };

        // Row 0: Arrow (stackable)
        twoDA.Rows.Add(new TwoDARow { Values = new System.Collections.Generic.List<string?> { "BASE_ITEM_ARROW", "****", "0", "99", "****", "****", "1" } });
        // Row 1: Wand (charges)
        twoDA.Rows.Add(new TwoDARow { Values = new System.Collections.Generic.List<string?> { "BASE_ITEM_MAGICWAND", "****", "0", "1", "****", "50", "4" } });
        // Row 2: Armor (model type 3)
        twoDA.Rows.Add(new TwoDARow { Values = new System.Collections.Generic.List<string?> { "BASE_ITEM_ARMOR", "****", "3", "1", "****", "****", "0" } });

        mockGameData.With2DA("baseitems", twoDA);

        var service = new BaseItemTypeService(mockGameData);
        var types = service.GetBaseItemTypes();

        Assert.Equal(3, types.Count);

        var arrow = types.Find(t => t.BaseItemIndex == 0);
        Assert.NotNull(arrow);
        Assert.Equal(99, arrow.Stacking);
        Assert.True(arrow.IsStackable);

        var wand = types.Find(t => t.BaseItemIndex == 1);
        Assert.NotNull(wand);
        Assert.Equal(50, wand.ChargesStarting);
        Assert.True(wand.HasCharges);

        var armor = types.Find(t => t.BaseItemIndex == 2);
        Assert.NotNull(armor);
        Assert.Equal(3, armor.ModelType);
        Assert.True(armor.HasArmorParts);
        Assert.Equal(0, armor.StorePanel);
    }

    #endregion
}
