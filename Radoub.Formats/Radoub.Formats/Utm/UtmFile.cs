using Radoub.Formats.Gff;

namespace Radoub.Formats.Utm;

/// <summary>
/// Represents a UTM (Store/Merchant) file used by Aurora Engine games.
/// UTM files are GFF-based and store merchant blueprint data.
/// Reference: BioWare Aurora Store Format specification, neverwinter.nim
/// </summary>
public class UtmFile
{
    /// <summary>
    /// File type signature - should be "UTM "
    /// </summary>
    public string FileType { get; set; } = "UTM ";

    /// <summary>
    /// File version - typically "V3.2"
    /// </summary>
    public string FileVersion { get; set; } = "V3.2";

    // Identity fields

    /// <summary>
    /// Blueprint resource reference (should match filename)
    /// </summary>
    public string ResRef { get; set; } = string.Empty;

    /// <summary>
    /// Store tag (max 32 characters)
    /// </summary>
    public string Tag { get; set; } = string.Empty;

    /// <summary>
    /// Localized store name
    /// </summary>
    public CExoLocString LocName { get; set; } = new();

    // Pricing fields

    /// <summary>
    /// Buy price percentage (what store pays when buying from player).
    /// 100 = full price, lower = store pays less.
    /// Note: Despite name, this is actually buy price from player perspective.
    /// </summary>
    public int MarkDown { get; set; } = 50;

    /// <summary>
    /// Sell price percentage (what player pays when buying from store).
    /// 100 = full price, higher = store charges more.
    /// Note: Despite name, this is actually sell price from player perspective.
    /// </summary>
    public int MarkUp { get; set; } = 150;

    /// <summary>
    /// Store's gold reserves. -1 = infinite gold.
    /// </summary>
    public int StoreGold { get; set; } = -1;

    /// <summary>
    /// Maximum price the store will pay for an item. -1 = no limit.
    /// </summary>
    public int MaxBuyPrice { get; set; } = -1;

    /// <summary>
    /// Cost to identify an item. -1 = identification service not available.
    /// </summary>
    public int IdentifyPrice { get; set; } = 100;

    // Black market fields

    /// <summary>
    /// If true, store will buy stolen items.
    /// </summary>
    public bool BlackMarket { get; set; }

    /// <summary>
    /// Buy price percentage for stolen items (when BlackMarket is true).
    /// Usually lower than regular MarkDown.
    /// </summary>
    public int BM_MarkDown { get; set; } = 25;

    // Blueprint-only fields

    /// <summary>
    /// Module designer comment (blueprint only)
    /// </summary>
    public string Comment { get; set; } = string.Empty;

    /// <summary>
    /// Palette ID for toolset organization (0-255)
    /// </summary>
    public byte PaletteID { get; set; }

    // Script fields

    /// <summary>
    /// Script to run when store is opened
    /// </summary>
    public string OnOpenStore { get; set; } = string.Empty;

    /// <summary>
    /// Script to run when store is closed
    /// </summary>
    public string OnStoreClosed { get; set; } = string.Empty;

    // Store inventory (5 panels)

    /// <summary>
    /// Store inventory organized by panel.
    /// Index 0 = Armor, 1 = Miscellaneous, 2 = Potions, 3 = Rings/Amulets, 4 = Weapons
    /// </summary>
    public List<StorePanel> StoreList { get; set; } = new();

    // Buy restrictions

    /// <summary>
    /// List of base item types the store will ONLY buy (empty = no restriction).
    /// Values are indices into baseitems.2da.
    /// </summary>
    public List<int> WillOnlyBuy { get; set; } = new();

    /// <summary>
    /// List of base item types the store will NOT buy.
    /// Values are indices into baseitems.2da.
    /// </summary>
    public List<int> WillNotBuy { get; set; } = new();
}

/// <summary>
/// Represents a store inventory panel (one of 5 categories).
/// </summary>
public class StorePanel
{
    /// <summary>
    /// Panel ID (0=Armor, 1=Misc, 2=Potions, 3=Rings, 4=Weapons).
    /// Corresponds to StoreList struct ID in UTM file.
    /// </summary>
    public int PanelId { get; set; }

    /// <summary>
    /// Items in this panel.
    /// </summary>
    public List<StoreItem> Items { get; set; } = new();
}

/// <summary>
/// Represents an item for sale in a store.
/// </summary>
public class StoreItem
{
    /// <summary>
    /// ResRef of the item blueprint (.uti)
    /// </summary>
    public string InventoryRes { get; set; } = string.Empty;

    /// <summary>
    /// If true, item has infinite stock (never runs out).
    /// </summary>
    public bool Infinite { get; set; }

    /// <summary>
    /// X position in inventory grid (used by toolset, usually 0xFFFF).
    /// </summary>
    public ushort Repos_PosX { get; set; } = 0xFFFF;

    /// <summary>
    /// Y position in inventory grid (used by toolset, usually 0xFFFF).
    /// </summary>
    public ushort Repos_PosY { get; set; } = 0xFFFF;
}

/// <summary>
/// Store panel ID constants.
/// These correspond to StoreList struct IDs in the UTM file format.
/// </summary>
public static class StorePanels
{
    public const int Armor = 0;
    public const int Miscellaneous = 1;
    public const int Potions = 2;
    public const int RingsAmulets = 3;
    public const int Weapons = 4;

    /// <summary>
    /// Get human-readable name for a store panel.
    /// </summary>
    public static string GetPanelName(int panelId)
    {
        return panelId switch
        {
            Armor => "Armor",
            Miscellaneous => "Miscellaneous",
            Potions => "Potions/Scrolls",
            RingsAmulets => "Rings/Amulets",
            Weapons => "Weapons",
            _ => $"Unknown ({panelId})"
        };
    }
}
