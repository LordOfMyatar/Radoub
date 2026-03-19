using System;
using System.Collections.Generic;
using System.Globalization;
using Radoub.Formats.Services;

namespace ItemEditor.Services;

/// <summary>
/// Categories for filtering base item types in the New Item wizard.
/// Derived at runtime from baseitems.2da EquipableSlots bitmask — never hardcoded.
/// </summary>
public enum ItemCategory
{
    Weapons,
    ArmorAndClothing,
    Shields,
    Headwear,
    JewelryAndAccessories,
    PotionsAndScrolls,
    Containers,
    Miscellaneous
}

/// <summary>
/// Categorizes base item types from baseitems.2da EquipableSlots bitmask.
/// </summary>
public class BaseItemCategoryService
{
    // EquipableSlots bit positions from baseitems.2da
    private const int SlotHead     = 0x0001;
    private const int SlotChest    = 0x0002;
    private const int SlotBoots    = 0x0004;
    private const int SlotArms     = 0x0008;
    private const int SlotRightHand = 0x0010;
    private const int SlotLeftHand  = 0x0020;
    private const int SlotBothHands = SlotRightHand | SlotLeftHand;

    private const int SlotArmorMask = SlotChest | SlotBoots | SlotArms;
    private const int SlotHandMask  = SlotRightHand | SlotLeftHand;

    /// <summary>
    /// Categorizes a base item type by reading EquipableSlots from baseitems.2da.
    /// </summary>
    public ItemCategory CategorizeBaseItem(int baseItemIndex, IGameDataService gameData)
    {
        var raw = gameData.Get2DAValue("baseitems", baseItemIndex, "EquipableSlots");
        int slots = ParseEquipableSlots(raw);

        if (slots == 0)
        {
            // Non-equippable: check Container column
            var container = gameData.Get2DAValue("baseitems", baseItemIndex, "Container");
            if (container == "1")
                return ItemCategory.Containers;
            return ItemCategory.Miscellaneous;
        }

        // Head slot
        if ((slots & SlotHead) != 0)
            return ItemCategory.Headwear;

        // Armor slots (chest, boots, arms)
        if ((slots & SlotArmorMask) != 0)
            return ItemCategory.ArmorAndClothing;

        // Hand slots (right, left, or both)
        if ((slots & SlotHandMask) != 0)
        {
            // TODO (#1729): Distinguish shields (left-hand only) from left-hand weapons.
            // Needs a spike to determine the best heuristic (e.g., check label or separate column).
            // Default to Weapons for now.
            return ItemCategory.Weapons;
        }

        // All other equippable slots (neck, rings, belt, cloak, ammo, etc.)
        return ItemCategory.JewelryAndAccessories;
    }

    /// <summary>
    /// Returns all ItemCategory enum values.
    /// </summary>
    public List<ItemCategory> GetAllCategories()
        => new(Enum.GetValues<ItemCategory>());

    /// <summary>
    /// Returns a human-readable display name for an ItemCategory.
    /// </summary>
    public string GetCategoryDisplayName(ItemCategory category) => category switch
    {
        ItemCategory.Weapons              => "Weapons",
        ItemCategory.ArmorAndClothing     => "Armor & Clothing",
        ItemCategory.Shields              => "Shields",
        ItemCategory.Headwear             => "Headwear",
        ItemCategory.JewelryAndAccessories => "Jewelry & Accessories",
        ItemCategory.PotionsAndScrolls    => "Potions & Scrolls",
        ItemCategory.Containers           => "Containers",
        ItemCategory.Miscellaneous        => "Miscellaneous",
        _                                 => category.ToString()
    };

    /// <summary>
    /// Returns true if the base item index is outside the vanilla NWN range (> 112).
    /// Heuristic only — custom content packs (CEP, PRC) add rows beyond the vanilla set.
    /// </summary>
    public bool IsCustomContent(int baseItemIndex) => baseItemIndex > 112;

    // --- Private helpers ---

    /// <summary>
    /// Parses the EquipableSlots cell value from baseitems.2da.
    /// Supports decimal, "0x" hex prefix, "****" (= 0), and empty string (= 0).
    /// </summary>
    private static int ParseEquipableSlots(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw == "****")
            return 0;

        if (raw.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            if (int.TryParse(raw[2..], NumberStyles.HexNumber, null, out var hex))
                return hex;
            return 0;
        }

        if (int.TryParse(raw, out var dec))
            return dec;

        return 0;
    }
}
