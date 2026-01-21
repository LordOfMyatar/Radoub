using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using Radoub.Formats.Services;
using Radoub.Formats.Uti;
using Radoub.UI.Services;

namespace Radoub.UI.ViewModels;

/// <summary>
/// ViewModel for displaying an item in a DataGrid.
/// Wraps UtiFile data with display-friendly properties.
/// Can be created from cached data (without full UtiFile) for performance.
/// </summary>
public partial class ItemViewModel : ObservableObject
{
    private readonly UtiFile? _item;

    // For cache-loaded items that don't have full UtiFile data
    private readonly string? _cachedResRef;
    private readonly string? _cachedTag;
    private readonly int _cachedBaseItem;
    private readonly uint _cachedValue;

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
    /// Creates a cache-loaded ItemViewModel without full UtiFile data.
    /// Used for displaying items in palettes where full data isn't needed.
    /// </summary>
    public ItemViewModel()
    {
        Name = string.Empty;
        BaseItemName = string.Empty;
        PropertiesDisplay = string.Empty;
        Source = GameResourceSource.Bif;
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
    /// The underlying item data. May be null for cache-loaded items.
    /// </summary>
    public UtiFile? Item => _item;

    /// <summary>
    /// Display name resolved from TLK or LocalizedName.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Blueprint resource reference (filename without extension).
    /// </summary>
    public string ResRef
    {
        get => _item?.TemplateResRef ?? _cachedResRef ?? string.Empty;
        init => _cachedResRef = value;
    }

    /// <summary>
    /// Item tag for scripting reference.
    /// </summary>
    public string Tag
    {
        get => _item?.Tag ?? _cachedTag ?? string.Empty;
        init => _cachedTag = value;
    }

    /// <summary>
    /// Base item type name (e.g., "Longsword", "Ring", "Amulet").
    /// </summary>
    public string BaseItemName { get; set; }

    /// <summary>
    /// Base item type index into baseitems.2da.
    /// </summary>
    public int BaseItem
    {
        get => _item?.BaseItem ?? _cachedBaseItem;
        init => _cachedBaseItem = value;
    }

    /// <summary>
    /// Item value (Cost + AddCost).
    /// </summary>
    public uint Value
    {
        get => _item != null ? _item.Cost + _item.AddCost : _cachedValue;
        init => _cachedValue = value;
    }

    /// <summary>
    /// Formatted display of item properties.
    /// </summary>
    public string PropertiesDisplay { get; set; }

    /// <summary>
    /// Number of item properties.
    /// </summary>
    public int PropertyCount => _item?.Properties.Count ?? 0;

    /// <summary>
    /// Selection state for checkbox column.
    /// </summary>
    [ObservableProperty]
    private bool _isSelected;

    /// <summary>
    /// Stack size for stackable items.
    /// </summary>
    public ushort StackSize => _item?.StackSize ?? 1;

    /// <summary>
    /// True if item is a plot item.
    /// </summary>
    public bool IsPlot => _item?.Plot ?? false;

    /// <summary>
    /// True if item is cursed (undroppable).
    /// </summary>
    public bool IsCursed => _item?.Cursed ?? false;

    /// <summary>
    /// Source of the item resource.
    /// </summary>
    public GameResourceSource Source { get; init; }

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

    /// <summary>
    /// Delegate for lazy loading icon bitmaps.
    /// </summary>
    public delegate Bitmap? IconLoader(UtiFile item);

    private Bitmap? _iconBitmap;
    private bool _iconLoaded;
    private IconLoader? _iconLoader;

    /// <summary>
    /// Sets the icon loader for lazy loading.
    /// Call this once at construction time.
    /// </summary>
    public void SetIconLoader(IconLoader? loader)
    {
        _iconLoader = loader;
    }

    /// <summary>
    /// Actual item icon bitmap loaded from game files.
    /// Loaded lazily on first access for virtualized UI performance.
    /// Null if game data not available (use IconPath placeholder).
    /// </summary>
    public Bitmap? IconBitmap
    {
        get
        {
            // Lazy load on first access
            if (!_iconLoaded && _iconLoader != null && _item != null)
            {
                _iconLoaded = true;
                try
                {
                    _iconBitmap = _iconLoader(_item);
                    OnPropertyChanged(nameof(HasGameIcon));
                }
                catch
                {
                    // Silently fail - use placeholder
                }
            }
            return _iconBitmap;
        }
        set
        {
            if (_iconBitmap != value)
            {
                _iconBitmap = value;
                _iconLoaded = true;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasGameIcon));
            }
        }
    }

    /// <summary>
    /// True if we have an actual game icon (not placeholder).
    /// Returns true if icon loader is available (assumes icon exists to avoid triggering load).
    /// </summary>
    public bool HasGameIcon => _iconLoader != null || _iconBitmap != null;

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
