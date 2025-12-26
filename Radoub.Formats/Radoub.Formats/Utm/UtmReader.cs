using Radoub.Formats.Gff;

namespace Radoub.Formats.Utm;

/// <summary>
/// Reads UTM (Store/Merchant) files from binary format.
/// UTM files are GFF-based with file type "UTM ".
/// Reference: BioWare Aurora Store Format specification, neverwinter.nim
/// </summary>
public static class UtmReader
{
    /// <summary>
    /// Read a UTM file from a file path.
    /// </summary>
    public static UtmFile Read(string filePath)
    {
        var buffer = File.ReadAllBytes(filePath);
        return Read(buffer);
    }

    /// <summary>
    /// Read a UTM file from a stream.
    /// </summary>
    public static UtmFile Read(Stream stream)
    {
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return Read(ms.ToArray());
    }

    /// <summary>
    /// Read a UTM file from a byte buffer.
    /// </summary>
    public static UtmFile Read(byte[] buffer)
    {
        // Parse as GFF first
        var gff = GffReader.Read(buffer);

        // Validate file type
        if (gff.FileType.TrimEnd() != "UTM")
        {
            throw new InvalidDataException(
                $"Invalid UTM file type: '{gff.FileType}' (expected 'UTM ')");
        }

        return ParseUtmFile(gff);
    }

    private static UtmFile ParseUtmFile(GffFile gff)
    {
        var root = gff.RootStruct;

        var utm = new UtmFile
        {
            FileType = gff.FileType,
            FileVersion = gff.FileVersion,

            // Identity fields
            ResRef = root.GetFieldValue<string>("ResRef", string.Empty),
            Tag = root.GetFieldValue<string>("Tag", string.Empty),

            // Pricing fields
            MarkDown = root.GetFieldValue<int>("MarkDown", 50),
            MarkUp = root.GetFieldValue<int>("MarkUp", 150),
            StoreGold = root.GetFieldValue<int>("StoreGold", -1),
            MaxBuyPrice = root.GetFieldValue<int>("MaxBuyPrice", -1),
            IdentifyPrice = root.GetFieldValue<int>("IdentifyPrice", 100),

            // Black market
            BlackMarket = root.GetFieldValue<byte>("BlackMarket", 0) != 0,
            BM_MarkDown = root.GetFieldValue<int>("BM_MarkDown", 25),

            // Blueprint fields
            Comment = root.GetFieldValue<string>("Comment", string.Empty),
            PaletteID = root.GetFieldValue<byte>("PaletteID", 0),

            // Scripts
            OnOpenStore = root.GetFieldValue<string>("OnOpenStore", string.Empty),
            OnStoreClosed = root.GetFieldValue<string>("OnStoreClosed", string.Empty)
        };

        // Localized name
        utm.LocName = ParseLocString(root, "LocName") ?? new CExoLocString();

        // Store inventory panels
        ParseStoreList(root, utm);

        // Buy restrictions
        ParseWillOnlyBuy(root, utm);
        ParseWillNotBuy(root, utm);

        return utm;
    }

    private static CExoLocString? ParseLocString(GffStruct root, string fieldName)
    {
        var field = root.GetField(fieldName);
        if (field == null || !field.IsCExoLocString || field.Value is not CExoLocString locString)
            return null;

        return locString;
    }

    private static void ParseStoreList(GffStruct root, UtmFile utm)
    {
        var storeListField = root.GetField("StoreList");
        if (storeListField == null || !storeListField.IsList || storeListField.Value is not GffList storeList)
            return;

        foreach (var panelStruct in storeList.Elements)
        {
            var panel = new StorePanel
            {
                PanelId = (int)panelStruct.Type
            };

            // Parse ItemList within each panel
            ParsePanelItems(panelStruct, panel);

            utm.StoreList.Add(panel);
        }
    }

    private static void ParsePanelItems(GffStruct panelStruct, StorePanel panel)
    {
        var itemListField = panelStruct.GetField("ItemList");
        if (itemListField == null || !itemListField.IsList || itemListField.Value is not GffList itemList)
            return;

        foreach (var itemStruct in itemList.Elements)
        {
            var item = new StoreItem
            {
                InventoryRes = itemStruct.GetFieldValue<string>("InventoryRes", string.Empty),
                Infinite = itemStruct.GetFieldValue<byte>("Infinite", 0) != 0,
                Repos_PosX = itemStruct.GetFieldValue<ushort>("Repos_PosX", 0xFFFF),
                Repos_PosY = itemStruct.GetFieldValue<ushort>("Repos_PosY", 0xFFFF)
            };

            panel.Items.Add(item);
        }
    }

    private static void ParseWillOnlyBuy(GffStruct root, UtmFile utm)
    {
        var field = root.GetField("WillOnlyBuy");
        if (field == null || !field.IsList || field.Value is not GffList list)
            return;

        foreach (var itemStruct in list.Elements)
        {
            // Each struct has a BaseItem field containing the base item type index
            var baseItem = itemStruct.GetFieldValue<int>("BaseItem", -1);
            if (baseItem >= 0)
            {
                utm.WillOnlyBuy.Add(baseItem);
            }
        }
    }

    private static void ParseWillNotBuy(GffStruct root, UtmFile utm)
    {
        var field = root.GetField("WillNotBuy");
        if (field == null || !field.IsList || field.Value is not GffList list)
            return;

        foreach (var itemStruct in list.Elements)
        {
            // Each struct has a BaseItem field containing the base item type index
            var baseItem = itemStruct.GetFieldValue<int>("BaseItem", -1);
            if (baseItem >= 0)
            {
                utm.WillNotBuy.Add(baseItem);
            }
        }
    }
}
