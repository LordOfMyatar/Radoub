using CommunityToolkit.Mvvm.ComponentModel;
using Radoub.Formats.Services;
using Radoub.Formats.Uti;
using Radoub.UI.Services;

namespace Radoub.UI.ViewModels;

/// <summary>
/// ViewModel for displaying an item in a DataGrid.
/// Wraps UtiFile data with display-friendly properties.
/// </summary>
public partial class ItemViewModel : ObservableObject
{
    private readonly UtiFile _item;

    /// <summary>
    /// Creates a new ItemViewModel wrapping a UtiFile.
    /// </summary>
    /// <param name="item">The underlying item data.</param>
    /// <param name="resolvedName">Display name (from TLK or LocalizedName).</param>
    /// <param name="baseItemName">Name of base item type (from baseitems.2da).</param>
    /// <param name="propertiesDisplay">Formatted properties string.</param>
    /// <param name="source">Source of the item (BIF = Standard, others = Custom).</param>
    public ItemViewModel(
        UtiFile item,
        string resolvedName,
        string baseItemName,
        string propertiesDisplay,
        GameResourceSource source = GameResourceSource.Bif)
    {
        _item = item;
        Name = resolvedName;
        BaseItemName = baseItemName;
        PropertiesDisplay = propertiesDisplay;
        Source = source;
    }

    /// <summary>
    /// Creates a new ItemViewModel for a backpack item with inventory metadata.
    /// </summary>
    public ItemViewModel(
        UtiFile item,
        string resolvedName,
        string baseItemName,
        string propertiesDisplay,
        ushort gridPositionX,
        ushort gridPositionY,
        bool isDropable,
        bool isPickpocketable,
        GameResourceSource source = GameResourceSource.Bif)
        : this(item, resolvedName, baseItemName, propertiesDisplay, source)
    {
        _gridPositionX = gridPositionX;
        _gridPositionY = gridPositionY;
        _isDropable = isDropable;
        _isPickpocketable = isPickpocketable;
    }

    /// <summary>
    /// The underlying item data.
    /// </summary>
    public UtiFile Item => _item;

    /// <summary>
    /// Display name resolved from TLK or LocalizedName.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Blueprint resource reference (filename without extension).
    /// </summary>
    public string ResRef => _item.TemplateResRef;

    /// <summary>
    /// Item tag for scripting reference.
    /// </summary>
    public string Tag => _item.Tag;

    /// <summary>
    /// Base item type name (e.g., "Longsword", "Ring", "Amulet").
    /// </summary>
    public string BaseItemName { get; }

    /// <summary>
    /// Base item type index into baseitems.2da.
    /// </summary>
    public int BaseItem => _item.BaseItem;

    /// <summary>
    /// Item value (Cost + AddCost).
    /// </summary>
    public uint Value => _item.Cost + _item.AddCost;

    /// <summary>
    /// Formatted display of item properties.
    /// </summary>
    public string PropertiesDisplay { get; }

    /// <summary>
    /// Number of item properties.
    /// </summary>
    public int PropertyCount => _item.Properties.Count;

    /// <summary>
    /// Selection state for checkbox column.
    /// </summary>
    [ObservableProperty]
    private bool _isSelected;

    /// <summary>
    /// Stack size for stackable items.
    /// </summary>
    public ushort StackSize => _item.StackSize;

    /// <summary>
    /// True if item is a plot item.
    /// </summary>
    public bool IsPlot => _item.Plot;

    /// <summary>
    /// True if item is cursed (undroppable).
    /// </summary>
    public bool IsCursed => _item.Cursed;

    /// <summary>
    /// Source of the item resource.
    /// </summary>
    public GameResourceSource Source { get; }

    /// <summary>
    /// True if item is from base game (BIF).
    /// </summary>
    public bool IsStandard => Source == GameResourceSource.Bif;

    /// <summary>
    /// True if item is from custom content (Override, HAK, or Module).
    /// </summary>
    public bool IsCustom => Source != GameResourceSource.Bif;

    /// <summary>
    /// Path to placeholder icon based on item type.
    /// Icons from game-icons.net (CC BY 3.0).
    /// </summary>
    public string IconPath => ItemIconHelper.GetIconPath(BaseItem);

    #region Inventory Metadata (for backpack items)

    /// <summary>
    /// X position in inventory grid (0-based). Used for backpack items.
    /// </summary>
    [ObservableProperty]
    private ushort _gridPositionX;

    /// <summary>
    /// Y position in inventory grid (0-based). Used for backpack items.
    /// </summary>
    [ObservableProperty]
    private ushort _gridPositionY;

    /// <summary>
    /// If true, creature drops this item on death. Default true.
    /// </summary>
    [ObservableProperty]
    private bool _isDropable = true;

    /// <summary>
    /// If true, item can be pickpocketed from creature.
    /// </summary>
    [ObservableProperty]
    private bool _isPickpocketable;

    #endregion
}
