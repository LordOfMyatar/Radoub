namespace Radoub.UI.Services;

/// <summary>
/// Maps NWN base item types to placeholder icons.
/// Icons from game-icons.net (CC BY 3.0).
/// </summary>
public static class ItemIconHelper
{
    // Base item indices from baseitems.2da
    // See: https://nwn.wiki/display/NWN1/baseitems.2da
    private const string IconsPath = "avares://Radoub.UI/Assets/Icons/";

    /// <summary>
    /// Gets the icon path for a base item type.
    /// </summary>
    public static string GetIconPath(int baseItem)
    {
        return baseItem switch
        {
            // Weapons - use sword icon
            0 => $"{IconsPath}righthand.svg",   // Shortsword
            1 => $"{IconsPath}righthand.svg",   // Longsword
            2 => $"{IconsPath}righthand.svg",   // Battleaxe
            3 => $"{IconsPath}righthand.svg",   // Bastard Sword
            4 => $"{IconsPath}righthand.svg",   // Light Flail
            5 => $"{IconsPath}righthand.svg",   // Warhammer
            6 => $"{IconsPath}righthand.svg",   // Heavy Crossbow
            7 => $"{IconsPath}righthand.svg",   // Light Crossbow
            8 => $"{IconsPath}righthand.svg",   // Longbow
            9 => $"{IconsPath}righthand.svg",   // Light Mace
            10 => $"{IconsPath}righthand.svg",  // Halberd
            11 => $"{IconsPath}righthand.svg",  // Shortbow
            12 => $"{IconsPath}righthand.svg",  // Twobladed Sword
            13 => $"{IconsPath}righthand.svg",  // Greatsword
            14 => $"{IconsPath}righthand.svg",  // Small Shield
            15 => $"{IconsPath}lefthand.svg",   // Torch (off-hand)

            // Armor - use chest icon
            16 => $"{IconsPath}chest.svg",      // Armor
            17 => $"{IconsPath}head.svg",       // Helmet
            18 => $"{IconsPath}arms.svg",       // Gloves
            19 => $"{IconsPath}feet.svg",       // Boots
            20 => $"{IconsPath}belt.svg",       // Belt
            21 => $"{IconsPath}cloak.svg",      // Cloak

            // Jewelry
            22 => $"{IconsPath}righthand.svg",  // Greatsword (duplicate check)
            24 => $"{IconsPath}ring.svg",       // Ring
            25 => $"{IconsPath}neck.svg",       // Amulet

            // Ammunition
            26 => $"{IconsPath}arrows.svg",     // Arrow
            27 => $"{IconsPath}bolts.svg",      // Bolt
            28 => $"{IconsPath}bolts.svg",      // Bullet

            // More weapons
            35 => $"{IconsPath}righthand.svg",  // Dagger
            36 => $"{IconsPath}righthand.svg",  // Club
            37 => $"{IconsPath}righthand.svg",  // Dart
            38 => $"{IconsPath}righthand.svg",  // Diremace
            39 => $"{IconsPath}righthand.svg",  // Double Axe
            40 => $"{IconsPath}righthand.svg",  // Heavy Flail
            41 => $"{IconsPath}righthand.svg",  // Light Hammer
            42 => $"{IconsPath}righthand.svg",  // Handaxe
            43 => $"{IconsPath}righthand.svg",  // Kama
            44 => $"{IconsPath}righthand.svg",  // Katana
            45 => $"{IconsPath}righthand.svg",  // Kukri
            47 => $"{IconsPath}righthand.svg",  // Morningstar
            49 => $"{IconsPath}righthand.svg",  // Quarterstaff
            50 => $"{IconsPath}righthand.svg",  // Rapier
            51 => $"{IconsPath}righthand.svg",  // Scimitar
            52 => $"{IconsPath}righthand.svg",  // Scythe
            53 => $"{IconsPath}lefthand.svg",   // Large Shield
            56 => $"{IconsPath}righthand.svg",  // Shuriken
            57 => $"{IconsPath}righthand.svg",  // Sickle
            58 => $"{IconsPath}righthand.svg",  // Sling
            59 => $"{IconsPath}righthand.svg",  // Throwing Axe
            60 => $"{IconsPath}lefthand.svg",   // Tower Shield

            // Creature weapons - use creature icon
            72 => $"{IconsPath}creature.svg",   // Creature Bite
            73 => $"{IconsPath}creature.svg",   // Creature Claw
            74 => $"{IconsPath}creature.svg",   // Creature Gore
            75 => $"{IconsPath}creature.svg",   // Creature Slam

            // Bracers
            78 => $"{IconsPath}arms.svg",       // Bracer

            // Default - use chest for miscellaneous items
            _ => $"{IconsPath}chest.svg"
        };
    }

    /// <summary>
    /// Gets the icon path for an equipment slot flag.
    /// </summary>
    public static string GetSlotIconPath(int slotFlag)
    {
        return slotFlag switch
        {
            0x1 => $"{IconsPath}head.svg",       // Head
            0x2 => $"{IconsPath}chest.svg",      // Chest
            0x4 => $"{IconsPath}feet.svg",       // Boots
            0x8 => $"{IconsPath}arms.svg",       // Arms
            0x10 => $"{IconsPath}righthand.svg", // Right Hand
            0x20 => $"{IconsPath}lefthand.svg",  // Left Hand
            0x40 => $"{IconsPath}cloak.svg",     // Cloak
            0x80 => $"{IconsPath}ring.svg",      // Left Ring
            0x100 => $"{IconsPath}ring.svg",     // Right Ring
            0x200 => $"{IconsPath}neck.svg",     // Neck
            0x400 => $"{IconsPath}belt.svg",     // Belt
            0x800 => $"{IconsPath}arrows.svg",   // Arrows
            0x1000 => $"{IconsPath}bolts.svg",   // Bullets
            0x2000 => $"{IconsPath}bolts.svg",   // Bolts
            0x4000 => $"{IconsPath}creature.svg",// Claw 1
            0x8000 => $"{IconsPath}creature.svg",// Claw 2
            0x10000 => $"{IconsPath}creature.svg",// Claw 3
            0x20000 => $"{IconsPath}creature.svg",// Skin
            _ => $"{IconsPath}chest.svg"         // Default
        };
    }
}
