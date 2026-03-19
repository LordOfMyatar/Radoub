using ItemEditor.Services;
using Radoub.TestUtilities.Mocks;

namespace ItemEditor.Tests.Services;

public class BaseItemCategoryServiceTests
{
    private static MockGameDataService CreateMock(int rowIndex, string equipableSlots, string container = "0")
    {
        var mock = new MockGameDataService(includeSampleData: false);
        mock.Set2DAValue("baseitems", rowIndex, "EquipableSlots", equipableSlots);
        mock.Set2DAValue("baseitems", rowIndex, "Container", container);
        return mock;
    }

    private readonly BaseItemCategoryService _service = new();

    // --- CategorizeBaseItem: Hand slots → Weapons ---

    [Theory]
    [InlineData("0x10", ItemCategory.Weapons)]   // Right hand
    [InlineData("0x20", ItemCategory.Weapons)]   // Left hand (default; shield TODO)
    [InlineData("0x30", ItemCategory.Weapons)]   // Either hand
    public void CategorizeBaseItem_HandSlots_ReturnsWeapons(string equipableSlots, ItemCategory expected)
    {
        var mock = CreateMock(0, equipableSlots);
        var result = _service.CategorizeBaseItem(0, mock);
        Assert.Equal(expected, result);
    }

    // --- CategorizeBaseItem: Armor slots → ArmorAndClothing ---

    [Theory]
    [InlineData("0x02", ItemCategory.ArmorAndClothing)]  // Chest
    [InlineData("0x04", ItemCategory.ArmorAndClothing)]  // Boots
    [InlineData("0x08", ItemCategory.ArmorAndClothing)]  // Arms
    public void CategorizeBaseItem_ArmorSlots_ReturnsArmorAndClothing(string equipableSlots, ItemCategory expected)
    {
        var mock = CreateMock(0, equipableSlots);
        var result = _service.CategorizeBaseItem(0, mock);
        Assert.Equal(expected, result);
    }

    // --- CategorizeBaseItem: Head slot → Headwear ---

    [Fact]
    public void CategorizeBaseItem_HeadSlot_ReturnsHeadwear()
    {
        var mock = CreateMock(0, "0x01");
        var result = _service.CategorizeBaseItem(0, mock);
        Assert.Equal(ItemCategory.Headwear, result);
    }

    // --- CategorizeBaseItem: Remaining equippable → JewelryAndAccessories ---

    [Theory]
    [InlineData("0x40")]   // Neck
    [InlineData("0x80")]   // Ring left
    [InlineData("0x100")]  // Ring right
    [InlineData("0x200")]  // Cloak/back
    [InlineData("0x400")]  // Belt
    [InlineData("0x800")]  // Ammo
    public void CategorizeBaseItem_OtherEquippableSlots_ReturnsJewelryAndAccessories(string equipableSlots)
    {
        var mock = CreateMock(0, equipableSlots);
        var result = _service.CategorizeBaseItem(0, mock);
        Assert.Equal(ItemCategory.JewelryAndAccessories, result);
    }

    // --- CategorizeBaseItem: Non-equippable (0) → Miscellaneous ---

    [Fact]
    public void CategorizeBaseItem_ZeroSlots_ReturnsMiscellaneous()
    {
        var mock = CreateMock(0, "0");
        var result = _service.CategorizeBaseItem(0, mock);
        Assert.Equal(ItemCategory.Miscellaneous, result);
    }

    [Fact]
    public void CategorizeBaseItem_StarStar_ReturnsMiscellaneous()
    {
        var mock = CreateMock(0, "****");
        var result = _service.CategorizeBaseItem(0, mock);
        Assert.Equal(ItemCategory.Miscellaneous, result);
    }

    [Fact]
    public void CategorizeBaseItem_EmptySlots_ReturnsMiscellaneous()
    {
        var mock = CreateMock(0, "");
        var result = _service.CategorizeBaseItem(0, mock);
        Assert.Equal(ItemCategory.Miscellaneous, result);
    }

    // --- CategorizeBaseItem: Container = "1" → Containers ---

    [Fact]
    public void CategorizeBaseItem_NonEquippableWithContainerFlag_ReturnsContainers()
    {
        var mock = CreateMock(0, "0", container: "1");
        var result = _service.CategorizeBaseItem(0, mock);
        Assert.Equal(ItemCategory.Containers, result);
    }

    [Fact]
    public void CategorizeBaseItem_StarStarWithContainerFlag_ReturnsContainers()
    {
        var mock = CreateMock(0, "****", container: "1");
        var result = _service.CategorizeBaseItem(0, mock);
        Assert.Equal(ItemCategory.Containers, result);
    }

    // --- CategorizeBaseItem: Decimal value parsing ---

    [Fact]
    public void CategorizeBaseItem_DecimalEquipableSlots_ParsesCorrectly()
    {
        // 16 decimal = 0x10 = right hand → Weapons
        var mock = CreateMock(0, "16");
        var result = _service.CategorizeBaseItem(0, mock);
        Assert.Equal(ItemCategory.Weapons, result);
    }

    // --- GetAllCategories ---

    [Fact]
    public void GetAllCategories_ReturnsAllEnumValues()
    {
        var all = _service.GetAllCategories();
        var expected = Enum.GetValues<ItemCategory>();
        Assert.Equal(expected.Length, all.Count);
        foreach (var cat in expected)
            Assert.Contains(cat, all);
    }

    // --- GetCategoryDisplayName ---

    [Theory]
    [InlineData(ItemCategory.Weapons, "Weapons")]
    [InlineData(ItemCategory.ArmorAndClothing, "Armor & Clothing")]
    [InlineData(ItemCategory.Shields, "Shields")]
    [InlineData(ItemCategory.Headwear, "Headwear")]
    [InlineData(ItemCategory.JewelryAndAccessories, "Jewelry & Accessories")]
    [InlineData(ItemCategory.PotionsAndScrolls, "Potions & Scrolls")]
    [InlineData(ItemCategory.Containers, "Containers")]
    [InlineData(ItemCategory.Miscellaneous, "Miscellaneous")]
    public void GetCategoryDisplayName_ReturnsHumanReadableName(ItemCategory category, string expected)
    {
        var result = _service.GetCategoryDisplayName(category);
        Assert.Equal(expected, result);
    }

    // --- IsCustomContent ---

    [Theory]
    [InlineData(0, false)]
    [InlineData(50, false)]
    [InlineData(112, false)]
    [InlineData(113, true)]
    [InlineData(200, true)]
    public void IsCustomContent_ReturnsCorrectResult(int index, bool expected)
    {
        var result = _service.IsCustomContent(index);
        Assert.Equal(expected, result);
    }
}
