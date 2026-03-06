namespace Radoub.UI.Models;

/// <summary>
/// Represents an equipment slot filter option for the item palette filter.
/// </summary>
public class SlotFilterInfo
{
    /// <summary>
    /// Special instance representing "All Slots" (no slot filter).
    /// </summary>
    public static readonly SlotFilterInfo AllSlots = new(0, "All Slots");

    /// <summary>
    /// Special instance for showing only non-equipable items.
    /// </summary>
    public static readonly SlotFilterInfo NonEquipable = new(-1, "Non-Equipable");

    /// <summary>
    /// Slot bit flag from baseitems.2da EquipableSlots.
    /// 0 = all slots, -1 = non-equipable only.
    /// </summary>
    public int SlotFlag { get; }

    /// <summary>
    /// Display name for the filter dropdown.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// True if this is the "All Slots" option.
    /// </summary>
    public bool IsAllSlots => SlotFlag == 0;

    /// <summary>
    /// True if this filters to non-equipable items only.
    /// </summary>
    public bool IsNonEquipable => SlotFlag == -1;

    public SlotFilterInfo(int slotFlag, string name)
    {
        SlotFlag = slotFlag;
        Name = name;
    }

    public override string ToString() => Name;
}
