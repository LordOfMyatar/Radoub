using Radoub.Formats.Gff;

namespace Radoub.Formats.Uti;

/// <summary>
/// Represents a UTI (Item Blueprint) file used by Aurora Engine games.
/// UTI files are GFF-based and store item template data.
/// Reference: BioWare Aurora Item Format specification, neverwinter.nim
/// </summary>
public class UtiFile
{
    /// <summary>
    /// File type signature - should be "UTI "
    /// </summary>
    public string FileType { get; set; } = "UTI ";

    /// <summary>
    /// File version - typically "V3.2"
    /// </summary>
    public string FileVersion { get; set; } = "V3.2";

    /// <summary>
    /// Blueprint resource reference (should match filename)
    /// </summary>
    public string TemplateResRef { get; set; } = string.Empty;

    /// <summary>
    /// Item tag (max 32 characters)
    /// </summary>
    public string Tag { get; set; } = string.Empty;

    /// <summary>
    /// Localized item name (appears in game when identified)
    /// </summary>
    public CExoLocString LocalizedName { get; set; } = new();

    /// <summary>
    /// Unidentified description
    /// </summary>
    public CExoLocString Description { get; set; } = new();

    /// <summary>
    /// Identified description
    /// </summary>
    public CExoLocString DescIdentified { get; set; } = new();

    /// <summary>
    /// Index into baseitems.2da - determines item type
    /// </summary>
    public int BaseItem { get; set; }

    /// <summary>
    /// Stack size (1 for unstackable items)
    /// </summary>
    public ushort StackSize { get; set; } = 1;

    /// <summary>
    /// Number of charges remaining
    /// </summary>
    public byte Charges { get; set; }

    /// <summary>
    /// Base cost of item
    /// </summary>
    public uint Cost { get; set; }

    /// <summary>
    /// Additional cost added to calculated cost
    /// </summary>
    public uint AddCost { get; set; }

    /// <summary>
    /// True if item cannot be removed from inventory (undroppable, unsellable)
    /// </summary>
    public bool Cursed { get; set; }

    /// <summary>
    /// True if item is a plot item (cannot be sold)
    /// </summary>
    public bool Plot { get; set; }

    /// <summary>
    /// True if item was stolen
    /// </summary>
    public bool Stolen { get; set; }

    /// <summary>
    /// List of item properties (enchantments, abilities)
    /// </summary>
    public List<ItemProperty> Properties { get; set; } = new();

    // Model fields - present depending on ModelType in baseitems.2da

    /// <summary>
    /// Part number for Simple (ModelType 0) and Layered (ModelType 1) items
    /// </summary>
    public byte ModelPart1 { get; set; }

    /// <summary>
    /// Part number 2 for Composite (ModelType 2) items
    /// </summary>
    public byte ModelPart2 { get; set; }

    /// <summary>
    /// Part number 3 for Composite (ModelType 2) items
    /// </summary>
    public byte ModelPart3 { get; set; }

    // Color fields - present for Layered (ModelType 1) and Armor (ModelType 3)

    /// <summary>
    /// Cloth color 1 (PLT layer index)
    /// </summary>
    public byte Cloth1Color { get; set; }

    /// <summary>
    /// Cloth color 2 (PLT layer index)
    /// </summary>
    public byte Cloth2Color { get; set; }

    /// <summary>
    /// Leather color 1 (PLT layer index)
    /// </summary>
    public byte Leather1Color { get; set; }

    /// <summary>
    /// Leather color 2 (PLT layer index)
    /// </summary>
    public byte Leather2Color { get; set; }

    /// <summary>
    /// Metal color 1 (PLT layer index)
    /// </summary>
    public byte Metal1Color { get; set; }

    /// <summary>
    /// Metal color 2 (PLT layer index)
    /// </summary>
    public byte Metal2Color { get; set; }

    // Armor part fields - present for Armor (ModelType 3) only

    /// <summary>
    /// Armor parts indexed by body part name.
    /// Keys: Belt, LBicep, RBicep, LFArm, RFArm, LFoot, RFoot, LHand, RHand,
    /// LShin, RShin, LShoul, RShoul, LThigh, RThigh, Neck, Pelvis, Robe, Torso
    /// Values: Index into corresponding parts_*.2da
    /// </summary>
    public Dictionary<string, byte> ArmorParts { get; set; } = new();

    // Blueprint-only fields

    /// <summary>
    /// Module designer comment (blueprint only)
    /// </summary>
    public string Comment { get; set; } = string.Empty;

    /// <summary>
    /// Palette ID for toolset organization (blueprint only)
    /// </summary>
    public byte PaletteID { get; set; }
}

/// <summary>
/// Represents an item property (enchantment, ability, etc.)
/// Reference: BioWare Aurora Item Format, Section 2.1.3
/// </summary>
public class ItemProperty
{
    /// <summary>
    /// Index into itempropdef.2da - identifies the property type
    /// </summary>
    public ushort PropertyName { get; set; }

    /// <summary>
    /// Index into subtype table (determined by PropertyName)
    /// </summary>
    public ushort Subtype { get; set; }

    /// <summary>
    /// Index into iprp_costtable.2da
    /// </summary>
    public byte CostTable { get; set; }

    /// <summary>
    /// Index into cost table (determined by CostTable)
    /// </summary>
    public ushort CostValue { get; set; }

    /// <summary>
    /// Index into iprp_paramtable.2da (-1 if no params)
    /// </summary>
    public byte Param1 { get; set; } = 0xFF;

    /// <summary>
    /// Index into param table (determined by Param1)
    /// </summary>
    public byte Param1Value { get; set; }

    /// <summary>
    /// Obsolete - always 100
    /// </summary>
    public byte ChanceAppear { get; set; } = 100;

    /// <summary>
    /// Obsolete param2
    /// </summary>
    public byte Param2 { get; set; } = 0xFF;

    /// <summary>
    /// Obsolete param2 value
    /// </summary>
    public byte Param2Value { get; set; }
}
