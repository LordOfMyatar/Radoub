namespace Radoub.UI.Models;

/// <summary>
/// Represents an item type from baseitems.2da for filtering.
/// </summary>
public class ItemTypeInfo
{
    /// <summary>
    /// Special instance representing "All Types" (no filter).
    /// </summary>
    public static readonly ItemTypeInfo AllTypes = new(-1, "All Types", "*");

    /// <summary>
    /// Row index in baseitems.2da.
    /// </summary>
    public int BaseItemIndex { get; }

    /// <summary>
    /// Display name (resolved from TLK or label).
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Label from baseitems.2da.
    /// </summary>
    public string Label { get; }

    /// <summary>
    /// True if this is the "All Types" option.
    /// </summary>
    public bool IsAllTypes => BaseItemIndex == -1;

    public ItemTypeInfo(int baseItemIndex, string name, string label)
    {
        BaseItemIndex = baseItemIndex;
        Name = name;
        Label = label;
    }

    public override string ToString() => Name;
}
