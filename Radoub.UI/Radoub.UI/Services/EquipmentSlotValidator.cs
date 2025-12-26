using Radoub.Formats.Services;
using Radoub.UI.ViewModels;

namespace Radoub.UI.Services;

/// <summary>
/// Validates equipment slot assignments using baseitems.2da.
/// </summary>
public class EquipmentSlotValidator
{
    private readonly IGameDataService _gameData;

    /// <summary>
    /// Creates a new equipment slot validator.
    /// </summary>
    /// <param name="gameData">Game data service for 2DA access.</param>
    public EquipmentSlotValidator(IGameDataService gameData)
    {
        _gameData = gameData;
    }

    /// <summary>
    /// Validates an item in a slot and returns a warning message if invalid.
    /// </summary>
    /// <param name="slot">The equipment slot.</param>
    /// <returns>Warning message if invalid, null if valid or no item equipped.</returns>
    public string? ValidateSlot(EquipmentSlotViewModel slot)
    {
        if (slot.EquippedItem == null)
            return null;

        var baseItem = slot.EquippedItem.BaseItem;
        var equipableSlots = GetEquipableSlots(baseItem);

        if (equipableSlots == null)
            return null; // Can't validate without 2DA data

        // Check if the slot flag is allowed for this base item
        if ((equipableSlots.Value & slot.SlotFlag) == 0)
        {
            var itemName = slot.EquippedItem.BaseItemName;
            return $"{itemName} cannot be equipped in {slot.Name}";
        }

        return null;
    }

    /// <summary>
    /// Validates all slots and sets their warning messages.
    /// </summary>
    /// <param name="slots">Equipment slots to validate.</param>
    public void ValidateAllSlots(IEnumerable<EquipmentSlotViewModel> slots)
    {
        foreach (var slot in slots)
        {
            slot.ValidationWarning = ValidateSlot(slot);
        }
    }

    /// <summary>
    /// Checks if an item can be equipped in a specific slot.
    /// </summary>
    /// <param name="baseItem">Base item index from UTI.</param>
    /// <param name="slotFlag">Slot bit flag to check.</param>
    /// <returns>True if item can be equipped in the slot.</returns>
    public bool CanEquipInSlot(int baseItem, int slotFlag)
    {
        var equipableSlots = GetEquipableSlots(baseItem);
        if (equipableSlots == null)
            return true; // Allow if we can't validate

        return (equipableSlots.Value & slotFlag) != 0;
    }

    /// <summary>
    /// Gets the valid slot flags for a base item type.
    /// </summary>
    /// <param name="baseItem">Base item index from UTI.</param>
    /// <returns>Bit flags of valid equipment slots, or null if not found.</returns>
    public int? GetEquipableSlots(int baseItem)
    {
        var value = _gameData.Get2DAValue("baseitems", baseItem, "EquipableSlots");

        if (string.IsNullOrEmpty(value) || value == "****")
            return null;

        if (int.TryParse(value, out var slots))
            return slots;

        // Try hex format (0x...)
        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            if (int.TryParse(value[2..], System.Globalization.NumberStyles.HexNumber, null, out slots))
                return slots;
        }

        return null;
    }

    /// <summary>
    /// Gets the list of valid slot names for a base item type.
    /// </summary>
    /// <param name="baseItem">Base item index from UTI.</param>
    /// <returns>List of valid slot names.</returns>
    public IReadOnlyList<string> GetValidSlotNames(int baseItem)
    {
        var equipableSlots = GetEquipableSlots(baseItem);
        if (equipableSlots == null)
            return Array.Empty<string>();

        var names = new List<string>();
        var slots = EquipmentSlotFactory.CreateAllSlots();

        foreach (var slot in slots)
        {
            if ((equipableSlots.Value & slot.SlotFlag) != 0)
            {
                names.Add(slot.Name);
            }
        }

        return names;
    }
}
