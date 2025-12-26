using CommunityToolkit.Mvvm.ComponentModel;
using Radoub.Formats.Uti;

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
    public ItemViewModel(
        UtiFile item,
        string resolvedName,
        string baseItemName,
        string propertiesDisplay)
    {
        _item = item;
        Name = resolvedName;
        BaseItemName = baseItemName;
        PropertiesDisplay = propertiesDisplay;
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
}
