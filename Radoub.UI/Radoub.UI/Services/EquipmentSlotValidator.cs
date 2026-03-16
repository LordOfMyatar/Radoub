using System.Linq;
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

    #region Feat Requirement Validation

    /// <summary>
    /// Gets the required feats for a base item type from baseitems.2da ReqFeat0-ReqFeat4 columns.
    /// </summary>
    /// <param name="baseItem">Base item index from UTI.</param>
    /// <returns>List of (FeatId, FeatName) tuples for required feats.</returns>
    public IReadOnlyList<(int FeatId, string FeatName)> GetRequiredFeats(int baseItem)
    {
        var result = new List<(int FeatId, string FeatName)>();

        for (int i = 0; i <= 4; i++)
        {
            var value = _gameData.Get2DAValue("baseitems", baseItem, $"ReqFeat{i}");
            if (string.IsNullOrEmpty(value) || value == "****")
                continue;

            if (!int.TryParse(value, out var featId))
                continue;

            var featName = ResolveFeatName(featId);
            result.Add((featId, featName));
        }

        return result;
    }

    /// <summary>
    /// Validates that the creature has the required feats for an equipped item.
    /// Accounts for class-specific weapon proficiency feats that satisfy general
    /// proficiency requirements (e.g., Rogue proficiency covers short swords). (#1675)
    /// </summary>
    /// <param name="slot">The equipment slot to check.</param>
    /// <param name="creatureFeats">Set of feat IDs the creature possesses.</param>
    /// <returns>Warning message listing missing feats, or null if all requirements met.</returns>
    public string? ValidateFeatRequirements(EquipmentSlotViewModel slot, IReadOnlySet<int> creatureFeats)
    {
        if (slot.EquippedItem == null)
            return null;

        var baseItem = slot.EquippedItem.BaseItem;
        var requiredFeats = GetRequiredFeats(baseItem);

        if (requiredFeats.Count > 0)
        {
            // Explicit ReqFeat columns — check each with equivalence
            var missingFeats = requiredFeats
                .Where(f => !IsProficiencySatisfied(f.FeatId, baseItem, creatureFeats))
                .ToList();

            if (missingFeats.Count == 0)
                return null;

            var featNames = string.Join(", ", missingFeats.Select(f => f.FeatName));
            return $"{slot.EquippedItem.BaseItemName} requires: {featNames}";
        }

        // No ReqFeat columns — check class-specific proficiency by base item ID
        // NWN doesn't have a "proficiency category" column in baseitems.2da;
        // the engine handles this internally. We check known class weapon mappings.
        var coveringFeats = GetEquivalentProficiencyFeats(baseItem);
        if (coveringFeats.Count == 0)
            return null; // Not a known weapon or no proficiency data

        if (coveringFeats.Any(creatureFeats.Contains))
            return null;

        return $"{slot.EquippedItem.BaseItemName} requires weapon proficiency";
    }

    /// <summary>
    /// Checks if a required feat is satisfied, including class-specific proficiency equivalences.
    /// NWN class weapon proficiency feats (Rogue, Wizard, Monk, Druid) grant proficiency with
    /// specific weapons that would otherwise require general Simple/Martial proficiency.
    /// </summary>
    private bool IsProficiencySatisfied(int requiredFeatId, int baseItem, IReadOnlySet<int> creatureFeats)
    {
        // Direct match — creature has the exact required feat
        if (creatureFeats.Contains(requiredFeatId))
            return true;

        // Check if any class-specific proficiency feat covers this weapon
        // Class proficiency feats map to specific base items they cover:
        var equivalentFeats = GetEquivalentProficiencyFeats(baseItem);
        return equivalentFeats.Any(creatureFeats.Contains);
    }

    /// <summary>
    /// Gets class-specific weapon proficiency feat IDs that cover a given base item.
    /// These are read from baseitems.2da columns that map weapon types to proficiency feats,
    /// falling back to the NWN engine's standard proficiency mappings.
    /// </summary>
    private List<int> GetEquivalentProficiencyFeats(int baseItem)
    {
        var result = new List<int>();

        // NWN proficiency feat IDs (standard across all NWN versions)
        const int ProfSimple = 44;
        const int ProfMartial = 45;
        const int ProfExotic = 46;
        const int ProfCreature = 47;
        const int ProfRogue = 48;
        const int ProfWizard = 49;
        const int ProfDruid = 50;
        const int ProfMonk = 51;
        const int ProfElf = 52;

        // Derive general proficiency category from the ReqFeat columns themselves.
        // baseitems.2da WeaponType is damage type (piercing/slashing/etc), NOT proficiency
        // category, so we use ReqFeat to determine which general proficiency applies.
        var reqFeats = GetRequiredFeats(baseItem);
        foreach (var (featId, _) in reqFeats)
        {
            if (featId == ProfSimple || featId == ProfMartial || featId == ProfExotic)
                result.Add(featId);
        }

        // Class-specific proficiency feats that cover subsets of weapons.
        // These mappings are from the NWN engine — class proficiency feats grant
        // proficiency with specific base items regardless of general category.
        // Rogue (48): rapier=37, shortsword=22, handcrossbow=69, shortbow=36
        if (baseItem is 37 or 22 or 69 or 36)
            result.Add(ProfRogue);

        // Wizard (49): club=28, dagger=3, heavycrossbow=6, lightcrossbow=7, quarterstaff=10
        if (baseItem is 28 or 3 or 6 or 7 or 10)
            result.Add(ProfWizard);

        // Elf (52): longsword=1, longbow=8, rapier=37, shortbow=36
        if (baseItem is 1 or 8 or 37 or 36)
            result.Add(ProfElf);

        // Druid (50): club=28, dagger=3, dart=31, quarterstaff=10, scimitar=23,
        //             sickle=60, sling=61, spear=63
        if (baseItem is 28 or 3 or 31 or 10 or 23 or 60 or 61 or 63)
            result.Add(ProfDruid);

        // Monk (51): club=28, dagger=3, handaxe=65, kama=2, quarterstaff=10, shuriken=59
        if (baseItem is 28 or 3 or 65 or 2 or 10 or 59)
            result.Add(ProfMonk);

        // Creature (47): creature weapons
        if (baseItem is 72 or 73 or 74 or 75)
            result.Add(ProfCreature);

        return result;
    }

    /// <summary>
    /// Validates all slots for feat requirements and sets their warning messages.
    /// </summary>
    /// <param name="slots">Equipment slots to validate.</param>
    /// <param name="creatureFeats">Set of feat IDs the creature possesses.</param>
    public void ValidateAllFeatRequirements(IEnumerable<EquipmentSlotViewModel> slots, IReadOnlySet<int> creatureFeats)
    {
        foreach (var slot in slots)
        {
            slot.ValidationWarning = ValidateFeatRequirements(slot, creatureFeats);
        }
    }

    /// <summary>
    /// Resolves a feat ID to its display name via feat.2da FEAT column + TLK.
    /// Falls back to LABEL column, then "Feat {id}".
    /// </summary>
    private string ResolveFeatName(int featId)
    {
        // Try FEAT column (TLK strref)
        var featStrRef = _gameData.Get2DAValue("feat", featId, "FEAT");
        if (!string.IsNullOrEmpty(featStrRef) && featStrRef != "****")
        {
            var tlkName = _gameData.GetString(featStrRef);
            if (!string.IsNullOrEmpty(tlkName))
                return tlkName;
        }

        // Fallback to LABEL column
        var label = _gameData.Get2DAValue("feat", featId, "LABEL");
        if (!string.IsNullOrEmpty(label) && label != "****")
            return label;

        return $"Feat {featId}";
    }

    #endregion

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
