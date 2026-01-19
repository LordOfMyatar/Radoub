using Radoub.Formats.Gff;
using Radoub.Formats.Utm;
using Xunit;

namespace Radoub.Formats.Tests;

public class UtmReaderTests
{
    [Fact]
    public void Read_ValidMinimalUtm_ParsesCorrectly()
    {
        var buffer = CreateMinimalUtmFile();

        var utm = UtmReader.Read(buffer);

        Assert.Equal("UTM ", utm.FileType);
        Assert.Equal("V3.2", utm.FileVersion);
    }

    [Fact]
    public void Read_UtmWithIdentityFields_ParsesAllFields()
    {
        var buffer = CreateUtmWithIdentityFields();

        var utm = UtmReader.Read(buffer);

        Assert.Equal("test_store", utm.ResRef);
        Assert.Equal("STORE_TAG", utm.Tag);
    }

    [Fact]
    public void Read_UtmWithPricingFields_ParsesAllFields()
    {
        var buffer = CreateUtmWithPricingFields();

        var utm = UtmReader.Read(buffer);

        Assert.Equal(75, utm.MarkDown);
        Assert.Equal(125, utm.MarkUp);
        Assert.Equal(5000, utm.StoreGold);
        Assert.Equal(10000, utm.MaxBuyPrice);
        Assert.Equal(50, utm.IdentifyPrice);
    }

    [Fact]
    public void Read_UtmWithBlackMarket_ParsesBlackMarketFields()
    {
        var buffer = CreateUtmWithBlackMarket();

        var utm = UtmReader.Read(buffer);

        Assert.True(utm.BlackMarket);
        Assert.Equal(15, utm.BM_MarkDown);
    }

    [Fact]
    public void Read_UtmWithLocalizedName_ParsesLocString()
    {
        var buffer = CreateUtmWithLocalizedName("Ye Olde Shoppe");

        var utm = UtmReader.Read(buffer);

        Assert.False(utm.LocName.IsEmpty);
        Assert.Equal("Ye Olde Shoppe", utm.LocName.GetDefault());
    }

    [Fact]
    public void Read_UtmWithScripts_ParsesScriptFields()
    {
        var buffer = CreateUtmWithScripts();

        var utm = UtmReader.Read(buffer);

        Assert.Equal("nw_o2_openstore", utm.OnOpenStore);
        Assert.Equal("nw_o2_closestore", utm.OnStoreClosed);
    }

    [Fact]
    public void Read_UtmWithStoreList_ParsesPanels()
    {
        var buffer = CreateUtmWithStoreList();

        var utm = UtmReader.Read(buffer);

        Assert.Equal(2, utm.StoreList.Count);
        Assert.Equal(StorePanels.Armor, utm.StoreList[0].PanelId);
        Assert.Equal(StorePanels.Weapons, utm.StoreList[1].PanelId);
    }

    [Fact]
    public void Read_UtmWithItems_ParsesItemList()
    {
        var buffer = CreateUtmWithItems();

        var utm = UtmReader.Read(buffer);

        Assert.Single(utm.StoreList);
        Assert.Equal(2, utm.StoreList[0].Items.Count);
        Assert.Equal("nw_wswls001", utm.StoreList[0].Items[0].InventoryRes);
        Assert.True(utm.StoreList[0].Items[0].Infinite);
        Assert.Equal("nw_wswls002", utm.StoreList[0].Items[1].InventoryRes);
        Assert.False(utm.StoreList[0].Items[1].Infinite);
    }

    [Fact]
    public void Read_UtmWithWillOnlyBuy_ParsesRestrictions()
    {
        var buffer = CreateUtmWithWillOnlyBuy();

        var utm = UtmReader.Read(buffer);

        Assert.Equal(2, utm.WillOnlyBuy.Count);
        Assert.Contains(4, utm.WillOnlyBuy);  // Longsword
        Assert.Contains(5, utm.WillOnlyBuy);  // Shortsword
    }

    [Fact]
    public void Read_UtmWithWillNotBuy_ParsesRestrictions()
    {
        var buffer = CreateUtmWithWillNotBuy();

        var utm = UtmReader.Read(buffer);

        Assert.Equal(2, utm.WillNotBuy.Count);
        Assert.Contains(49, utm.WillNotBuy); // Potion
        Assert.Contains(74, utm.WillNotBuy); // Scroll
    }

    [Fact]
    public void Read_InvalidFileType_ThrowsException()
    {
        var gff = CreateGffFileWithType("DLG ");
        var buffer = GffWriter.Write(gff);

        var ex = Assert.Throws<InvalidDataException>(() => UtmReader.Read(buffer));
        Assert.Contains("Invalid UTM file type", ex.Message);
    }

    [Fact]
    public void RoundTrip_MinimalUtm_PreservesData()
    {
        var original = CreateMinimalUtmFile();

        var utm = UtmReader.Read(original);
        var written = UtmWriter.Write(utm);
        var utm2 = UtmReader.Read(written);

        Assert.Equal(utm.FileType, utm2.FileType);
        Assert.Equal(utm.FileVersion, utm2.FileVersion);
    }

    [Fact]
    public void RoundTrip_UtmWithIdentityFields_PreservesData()
    {
        var original = CreateUtmWithIdentityFields();

        var utm = UtmReader.Read(original);
        var written = UtmWriter.Write(utm);
        var utm2 = UtmReader.Read(written);

        Assert.Equal(utm.ResRef, utm2.ResRef);
        Assert.Equal(utm.Tag, utm2.Tag);
    }

    [Fact]
    public void RoundTrip_UtmWithPricingFields_PreservesData()
    {
        var original = CreateUtmWithPricingFields();

        var utm = UtmReader.Read(original);
        var written = UtmWriter.Write(utm);
        var utm2 = UtmReader.Read(written);

        Assert.Equal(utm.MarkDown, utm2.MarkDown);
        Assert.Equal(utm.MarkUp, utm2.MarkUp);
        Assert.Equal(utm.StoreGold, utm2.StoreGold);
        Assert.Equal(utm.MaxBuyPrice, utm2.MaxBuyPrice);
        Assert.Equal(utm.IdentifyPrice, utm2.IdentifyPrice);
    }

    [Fact]
    public void RoundTrip_UtmWithItems_PreservesItems()
    {
        var original = CreateUtmWithItems();

        var utm = UtmReader.Read(original);
        var written = UtmWriter.Write(utm);
        var utm2 = UtmReader.Read(written);

        Assert.Equal(utm.StoreList.Count, utm2.StoreList.Count);
        Assert.Equal(utm.StoreList[0].Items.Count, utm2.StoreList[0].Items.Count);
        Assert.Equal(utm.StoreList[0].Items[0].InventoryRes, utm2.StoreList[0].Items[0].InventoryRes);
        Assert.Equal(utm.StoreList[0].Items[0].Infinite, utm2.StoreList[0].Items[0].Infinite);
    }

    [Fact]
    public void RoundTrip_UtmWithRestrictions_PreservesRestrictions()
    {
        var original = CreateUtmWithBothRestrictions();

        var utm = UtmReader.Read(original);
        var written = UtmWriter.Write(utm);
        var utm2 = UtmReader.Read(written);

        Assert.Equal(utm.WillOnlyBuy.Count, utm2.WillOnlyBuy.Count);
        Assert.Equal(utm.WillNotBuy.Count, utm2.WillNotBuy.Count);
        foreach (var item in utm.WillOnlyBuy)
        {
            Assert.Contains(item, utm2.WillOnlyBuy);
        }
        foreach (var item in utm.WillNotBuy)
        {
            Assert.Contains(item, utm2.WillNotBuy);
        }
    }

    [Fact]
    public void StorePanels_GetPanelName_ReturnsCorrectNames()
    {
        Assert.Equal("Armor", StorePanels.GetPanelName(StorePanels.Armor));
        Assert.Equal("Miscellaneous", StorePanels.GetPanelName(StorePanels.Miscellaneous));
        Assert.Equal("Potions/Scrolls", StorePanels.GetPanelName(StorePanels.Potions));
        Assert.Equal("Rings/Amulets", StorePanels.GetPanelName(StorePanels.RingsAmulets));
        Assert.Equal("Weapons", StorePanels.GetPanelName(StorePanels.Weapons));
        Assert.Equal("Unknown (99)", StorePanels.GetPanelName(99));
    }

    [Fact]
    public void Read_UtmWithVarTable_ParsesVariables()
    {
        var buffer = CreateUtmWithVariables();

        var utm = UtmReader.Read(buffer);

        Assert.Equal(3, utm.VarTable.Count);
        Assert.Contains(utm.VarTable, v => v.Name == "TestInt" && v.Type == VariableType.Int && v.GetInt() == 42);
        Assert.Contains(utm.VarTable, v => v.Name == "TestFloat" && v.Type == VariableType.Float);
        Assert.Contains(utm.VarTable, v => v.Name == "TestString" && v.Type == VariableType.String && v.GetString() == "Hello");
    }

    [Fact]
    public void RoundTrip_UtmWithVariables_PreservesVariables()
    {
        var original = CreateUtmWithVariables();

        var utm = UtmReader.Read(original);
        var written = UtmWriter.Write(utm);
        var utm2 = UtmReader.Read(written);

        Assert.Equal(utm.VarTable.Count, utm2.VarTable.Count);
        foreach (var v1 in utm.VarTable)
        {
            var v2 = utm2.VarTable.FirstOrDefault(v => v.Name == v1.Name);
            Assert.NotNull(v2);
            Assert.Equal(v1.Type, v2.Type);
        }
    }

    #region Test Helpers

    private static byte[] CreateMinimalUtmFile()
    {
        var gff = CreateGffFileWithType("UTM ");
        return GffWriter.Write(gff);
    }

    private static byte[] CreateUtmWithIdentityFields()
    {
        var gff = CreateGffFileWithType("UTM ");
        var root = gff.RootStruct;

        AddCResRefField(root, "ResRef", "test_store");
        AddCExoStringField(root, "Tag", "STORE_TAG");

        return GffWriter.Write(gff);
    }

    private static byte[] CreateUtmWithPricingFields()
    {
        var gff = CreateGffFileWithType("UTM ");
        var root = gff.RootStruct;

        AddCResRefField(root, "ResRef", "test_store");
        AddIntField(root, "MarkDown", 75);
        AddIntField(root, "MarkUp", 125);
        AddIntField(root, "StoreGold", 5000);
        AddIntField(root, "MaxBuyPrice", 10000);
        AddIntField(root, "IdentifyPrice", 50);

        return GffWriter.Write(gff);
    }

    private static byte[] CreateUtmWithBlackMarket()
    {
        var gff = CreateGffFileWithType("UTM ");
        var root = gff.RootStruct;

        AddCResRefField(root, "ResRef", "test_store");
        AddByteField(root, "BlackMarket", 1);
        AddIntField(root, "BM_MarkDown", 15);

        return GffWriter.Write(gff);
    }

    private static byte[] CreateUtmWithLocalizedName(string name)
    {
        var gff = CreateGffFileWithType("UTM ");
        var root = gff.RootStruct;

        AddCResRefField(root, "ResRef", "test_store");

        var locString = new CExoLocString { StrRef = 0xFFFFFFFF };
        locString.LocalizedStrings[0] = name;
        AddLocStringField(root, "LocName", locString);

        return GffWriter.Write(gff);
    }

    private static byte[] CreateUtmWithScripts()
    {
        var gff = CreateGffFileWithType("UTM ");
        var root = gff.RootStruct;

        AddCResRefField(root, "ResRef", "test_store");
        AddCResRefField(root, "OnOpenStore", "nw_o2_openstore");
        AddCResRefField(root, "OnStoreClosed", "nw_o2_closestore");

        return GffWriter.Write(gff);
    }

    private static byte[] CreateUtmWithStoreList()
    {
        var gff = CreateGffFileWithType("UTM ");
        var root = gff.RootStruct;

        AddCResRefField(root, "ResRef", "test_store");

        // Create StoreList with two empty panels
        var storeList = new GffList();

        var armorPanel = new GffStruct { Type = (uint)StorePanels.Armor };
        AddEmptyItemList(armorPanel);
        storeList.Elements.Add(armorPanel);

        var weaponsPanel = new GffStruct { Type = (uint)StorePanels.Weapons };
        AddEmptyItemList(weaponsPanel);
        storeList.Elements.Add(weaponsPanel);

        storeList.Count = 2;

        root.Fields.Add(new GffField
        {
            Type = GffField.List,
            Label = "StoreList",
            Value = storeList
        });

        return GffWriter.Write(gff);
    }

    private static byte[] CreateUtmWithItems()
    {
        var gff = CreateGffFileWithType("UTM ");
        var root = gff.RootStruct;

        AddCResRefField(root, "ResRef", "test_store");

        // Create StoreList with one panel containing two items
        var storeList = new GffList();

        var weaponsPanel = new GffStruct { Type = (uint)StorePanels.Weapons };
        var itemList = new GffList();

        // First item - infinite stock
        var item1 = new GffStruct { Type = 0 };
        AddCResRefField(item1, "InventoryRes", "nw_wswls001");
        AddByteField(item1, "Infinite", 1);
        AddWordField(item1, "Repos_PosX", 0xFFFF);
        AddWordField(item1, "Repos_PosY", 0xFFFF);
        itemList.Elements.Add(item1);

        // Second item - finite stock
        var item2 = new GffStruct { Type = 0 };
        AddCResRefField(item2, "InventoryRes", "nw_wswls002");
        AddByteField(item2, "Infinite", 0);
        AddWordField(item2, "Repos_PosX", 0xFFFF);
        AddWordField(item2, "Repos_PosY", 0xFFFF);
        itemList.Elements.Add(item2);

        itemList.Count = 2;

        weaponsPanel.Fields.Add(new GffField
        {
            Type = GffField.List,
            Label = "ItemList",
            Value = itemList
        });

        storeList.Elements.Add(weaponsPanel);
        storeList.Count = 1;

        root.Fields.Add(new GffField
        {
            Type = GffField.List,
            Label = "StoreList",
            Value = storeList
        });

        return GffWriter.Write(gff);
    }

    private static byte[] CreateUtmWithWillOnlyBuy()
    {
        var gff = CreateGffFileWithType("UTM ");
        var root = gff.RootStruct;

        AddCResRefField(root, "ResRef", "test_store");
        AddBaseItemList(root, "WillOnlyBuy", new[] { 4, 5 }); // Longsword, Shortsword

        return GffWriter.Write(gff);
    }

    private static byte[] CreateUtmWithWillNotBuy()
    {
        var gff = CreateGffFileWithType("UTM ");
        var root = gff.RootStruct;

        AddCResRefField(root, "ResRef", "test_store");
        AddBaseItemList(root, "WillNotBuy", new[] { 49, 74 }); // Potion, Scroll

        return GffWriter.Write(gff);
    }

    private static byte[] CreateUtmWithBothRestrictions()
    {
        var gff = CreateGffFileWithType("UTM ");
        var root = gff.RootStruct;

        AddCResRefField(root, "ResRef", "test_store");
        AddBaseItemList(root, "WillOnlyBuy", new[] { 4, 5 });
        AddBaseItemList(root, "WillNotBuy", new[] { 49, 74 });

        return GffWriter.Write(gff);
    }

    private static byte[] CreateUtmWithVariables()
    {
        var gff = CreateGffFileWithType("UTM ");
        var root = gff.RootStruct;

        AddCResRefField(root, "ResRef", "test_store");

        // Add VarTable with 3 variables
        var varTable = new GffList();

        // Int variable
        var intVar = new GffStruct { Type = 0 };
        AddCExoStringField(intVar, "Name", "TestInt");
        AddDwordField(intVar, "Type", 1); // VariableType.Int
        AddIntField(intVar, "Value", 42);
        varTable.Elements.Add(intVar);

        // Float variable
        var floatVar = new GffStruct { Type = 0 };
        AddCExoStringField(floatVar, "Name", "TestFloat");
        AddDwordField(floatVar, "Type", 2); // VariableType.Float
        AddFloatField(floatVar, "Value", 3.14f);
        varTable.Elements.Add(floatVar);

        // String variable
        var stringVar = new GffStruct { Type = 0 };
        AddCExoStringField(stringVar, "Name", "TestString");
        AddDwordField(stringVar, "Type", 3); // VariableType.String
        AddCExoStringField(stringVar, "Value", "Hello");
        varTable.Elements.Add(stringVar);

        varTable.Count = 3;
        root.Fields.Add(new GffField
        {
            Type = GffField.List,
            Label = "VarTable",
            Value = varTable
        });

        return GffWriter.Write(gff);
    }

    private static void AddEmptyItemList(GffStruct panelStruct)
    {
        var itemList = new GffList { Count = 0 };
        panelStruct.Fields.Add(new GffField
        {
            Type = GffField.List,
            Label = "ItemList",
            Value = itemList
        });
    }

    private static void AddBaseItemList(GffStruct parent, string label, int[] baseItems)
    {
        var list = new GffList();

        foreach (var baseItem in baseItems)
        {
            var itemStruct = new GffStruct { Type = 0 };
            AddIntField(itemStruct, "BaseItem", baseItem);
            list.Elements.Add(itemStruct);
        }

        list.Count = (uint)baseItems.Length;

        parent.Fields.Add(new GffField
        {
            Type = GffField.List,
            Label = label,
            Value = list
        });
    }

    private static GffFile CreateGffFileWithType(string fileType)
    {
        return new GffFile
        {
            FileType = fileType,
            FileVersion = "V3.2",
            RootStruct = new GffStruct { Type = 0xFFFFFFFF }
        };
    }

    private static void AddByteField(GffStruct parent, string label, byte value)
    {
        parent.Fields.Add(new GffField
        {
            Type = GffField.BYTE,
            Label = label,
            Value = value
        });
    }

    private static void AddWordField(GffStruct parent, string label, ushort value)
    {
        parent.Fields.Add(new GffField
        {
            Type = GffField.WORD,
            Label = label,
            Value = value
        });
    }

    private static void AddIntField(GffStruct parent, string label, int value)
    {
        parent.Fields.Add(new GffField
        {
            Type = GffField.INT,
            Label = label,
            Value = value
        });
    }

    private static void AddDwordField(GffStruct parent, string label, uint value)
    {
        parent.Fields.Add(new GffField
        {
            Type = GffField.DWORD,
            Label = label,
            Value = value
        });
    }

    private static void AddFloatField(GffStruct parent, string label, float value)
    {
        parent.Fields.Add(new GffField
        {
            Type = GffField.FLOAT,
            Label = label,
            Value = value
        });
    }

    private static void AddCExoStringField(GffStruct parent, string label, string value)
    {
        parent.Fields.Add(new GffField
        {
            Type = GffField.CExoString,
            Label = label,
            Value = value
        });
    }

    private static void AddCResRefField(GffStruct parent, string label, string value)
    {
        parent.Fields.Add(new GffField
        {
            Type = GffField.CResRef,
            Label = label,
            Value = value
        });
    }

    private static void AddLocStringField(GffStruct parent, string label, CExoLocString value)
    {
        parent.Fields.Add(new GffField
        {
            Type = GffField.CExoLocString,
            Label = label,
            Value = value
        });
    }

    #endregion
}
