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
    /// Validates creature compatibility for an equipped item (weapon size vs creature size).
    /// NWN rule: creature can wield weapons up to 1 size larger (two-handed).
    /// Weapons 2+ sizes larger than the creature cannot be wielded.
    /// </summary>
    /// <param name="slot">The equipment slot to check.</param>
    /// <param name="creatureSize">Creature size category (1=Tiny, 2=Small, 3=Medium, 4=Large, 5=Huge).</param>
    /// <returns>Warning message if weapon is too large, null otherwise.</returns>
    public string? ValidateCreatureCompatibility(EquipmentSlotViewModel slot, int creatureSize)
    {
        if (slot.EquippedItem == null)
            return null;

        var weaponSize = GetWeaponSize(slot.EquippedItem.BaseItem);
        if (weaponSize == null)
            return null;

        // Creature can wield weapons up to 1 size larger (two-handed)
        var sizeDiff = weaponSize.Value - creatureSize;
        if (sizeDiff > 1)
        {
            var sizeName = GetSizeName(weaponSize.Value);
            var creatureSizeName = GetSizeName(creatureSize);
            return $"{slot.EquippedItem.BaseItemName} is too large ({sizeName}) for a {creatureSizeName} creature";
        }

        return null;
    }

    /// <summary>
    /// Validates all slots for creature compatibility and sets their warning messages.
    /// </summary>
    /// <param name="slots">Equipment slots to validate.</param>
    /// <param name="creatureSize">Creature size category.</param>
    public void ValidateAllCreatureCompatibility(IEnumerable<EquipmentSlotViewModel> slots, int creatureSize)
    {
        foreach (var slot in slots)
        {
            slot.ValidationWarning = ValidateCreatureCompatibility(slot, creatureSize);
        }
    }

    /// <summary>
    /// Gets the weapon size for a base item type from baseitems.2da WeaponSize column.
    /// </summary>
    /// <param name="baseItem">Base item index from UTI.</param>
    /// <returns>Weapon size (1-5), or null if not a weapon or data unavailable.</returns>
    public int? GetWeaponSize(int baseItem)
    {
        var value = _gameData.Get2DAValue("baseitems", baseItem, "WeaponSize");
        if (string.IsNullOrEmpty(value) || value == "****")
            return null;

        if (int.TryParse(value, out var size))
            return size;

        return null;
    }

    /// <summary>
    /// Gets a human-readable name for a creature/weapon size category.
    /// </summary>
    private static string GetSizeName(int size) => size switch
    {
        1 => "Tiny",
        2 => "Small",
        3 => "Medium",
        4 => "Large",
        5 => "Huge",
        _ => $"Size {size}"
    };

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
