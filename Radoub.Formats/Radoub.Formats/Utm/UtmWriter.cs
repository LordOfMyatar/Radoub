using Radoub.Formats.Gff;
using static Radoub.Formats.Gff.GffFieldBuilder;

namespace Radoub.Formats.Utm;

/// <summary>
/// Writes UTM (Store/Merchant) files to binary format.
/// UTM files are GFF-based with file type "UTM ".
/// Reference: BioWare Aurora Store Format specification, neverwinter.nim
/// </summary>
public static class UtmWriter
{
    /// <summary>
    /// Write a UTM file to a file path.
    /// </summary>
    public static void Write(UtmFile utm, string filePath)
    {
        var buffer = Write(utm);
        File.WriteAllBytes(filePath, buffer);
    }

    /// <summary>
    /// Write a UTM file to a stream.
    /// </summary>
    public static void Write(UtmFile utm, Stream stream)
    {
        var buffer = Write(utm);
        stream.Write(buffer, 0, buffer.Length);
    }

    /// <summary>
    /// Write a UTM file to a byte buffer.
    /// </summary>
    public static byte[] Write(UtmFile utm)
    {
        var gff = BuildGffFile(utm);
        return GffWriter.Write(gff);
    }

    private static GffFile BuildGffFile(UtmFile utm)
    {
        var gff = new GffFile
        {
            FileType = utm.FileType,
            FileVersion = utm.FileVersion,
            RootStruct = BuildRootStruct(utm)
        };

        return gff;
    }

    private static GffStruct BuildRootStruct(UtmFile utm)
    {
        var root = new GffStruct { Type = 0xFFFFFFFF };

        // Identity fields
        AddCResRefField(root, "ResRef", utm.ResRef);
        AddCExoStringField(root, "Tag", utm.Tag);
        AddLocStringField(root, "LocName", utm.LocName);

        // Pricing fields
        AddIntField(root, "MarkDown", utm.MarkDown);
        AddIntField(root, "MarkUp", utm.MarkUp);
        AddIntField(root, "StoreGold", utm.StoreGold);
        AddIntField(root, "MaxBuyPrice", utm.MaxBuyPrice);
        AddIntField(root, "IdentifyPrice", utm.IdentifyPrice);

        // Black market
        AddByteField(root, "BlackMarket", (byte)(utm.BlackMarket ? 1 : 0));
        AddIntField(root, "BM_MarkDown", utm.BM_MarkDown);

        // Blueprint fields
        if (!string.IsNullOrEmpty(utm.Comment))
            AddCExoStringField(root, "Comment", utm.Comment);
        AddByteField(root, "ID", utm.PaletteID);  // GFF field is "ID", maps to PaletteID in our model

        // Scripts
        if (!string.IsNullOrEmpty(utm.OnOpenStore))
            AddCResRefField(root, "OnOpenStore", utm.OnOpenStore);
        if (!string.IsNullOrEmpty(utm.OnStoreClosed))
            AddCResRefField(root, "OnStoreClosed", utm.OnStoreClosed);

        // Store inventory panels
        AddStoreList(root, utm.StoreList);

        // Buy restrictions
        if (utm.WillOnlyBuy.Count > 0)
            AddBaseItemList(root, "WillOnlyBuy", utm.WillOnlyBuy);
        if (utm.WillNotBuy.Count > 0)
            AddBaseItemList(root, "WillNotBuy", utm.WillNotBuy);

        return root;
    }

    private static void AddStoreList(GffStruct parent, List<StorePanel> panels)
    {
        var list = new GffList();
        foreach (var panel in panels)
        {
            var panelStruct = new GffStruct { Type = (uint)panel.PanelId };
            AddPanelItemList(panelStruct, panel.Items);
            list.Elements.Add(panelStruct);
        }
        AddListField(parent, "StoreList", list);
    }

    private static void AddPanelItemList(GffStruct panelStruct, List<StoreItem> items)
    {
        var list = new GffList();
        foreach (var item in items)
        {
            var itemStruct = new GffStruct { Type = 0 };
            AddCResRefField(itemStruct, "InventoryRes", item.InventoryRes);
            AddByteField(itemStruct, "Infinite", (byte)(item.Infinite ? 1 : 0));
            AddWordField(itemStruct, "Repos_PosX", item.Repos_PosX);
            AddWordField(itemStruct, "Repos_PosY", item.Repos_PosY);
            list.Elements.Add(itemStruct);
        }
        AddListField(panelStruct, "ItemList", list);
    }

    private static void AddBaseItemList(GffStruct parent, string label, List<int> baseItems)
    {
        var list = new GffList();
        foreach (var baseItem in baseItems)
        {
            var itemStruct = new GffStruct { Type = 0 };
            AddIntField(itemStruct, "BaseItem", baseItem);
            list.Elements.Add(itemStruct);
        }
        AddListField(parent, label, list);
    }
}
