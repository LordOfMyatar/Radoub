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
            // Swords
            0 => $"{IconsPath}righthand.svg",   // Shortsword
            1 => $"{IconsPath}righthand.svg",   // Longsword
            3 => $"{IconsPath}righthand.svg",   // Bastard Sword
            12 => $"{IconsPath}righthand.svg",  // Twobladed Sword
            13 => $"{IconsPath}righthand.svg",  // Greatsword
            22 => $"{IconsPath}righthand.svg",  // Dagger
            41 => $"{IconsPath}righthand.svg",  // Katana
            42 => $"{IconsPath}righthand.svg",  // Kukri
            51 => $"{IconsPath}righthand.svg",  // Rapier
            53 => $"{IconsPath}righthand.svg",  // Scimitar

            // Axes
            2 => $"{IconsPath}righthand.svg",   // Battleaxe
            18 => $"{IconsPath}righthand.svg",  // Greataxe
            33 => $"{IconsPath}righthand.svg",  // Double Axe
            38 => $"{IconsPath}righthand.svg",  // Handaxe
            63 => $"{IconsPath}righthand.svg",  // Throwing Axe
            108 => $"{IconsPath}righthand.svg", // Dwarven Waraxe

            // Hammers/Maces/Flails
            4 => $"{IconsPath}righthand.svg",   // Light Flail
            5 => $"{IconsPath}righthand.svg",   // Warhammer
            9 => $"{IconsPath}righthand.svg",   // Light Mace
            28 => $"{IconsPath}righthand.svg",  // Club
            32 => $"{IconsPath}righthand.svg",  // Diremace
            35 => $"{IconsPath}righthand.svg",  // Heavy Flail
            37 => $"{IconsPath}righthand.svg",  // Light Hammer
            47 => $"{IconsPath}righthand.svg",  // Morningstar

            // Polearms/Spears
            10 => $"{IconsPath}righthand.svg",  // Halberd
            50 => $"{IconsPath}righthand.svg",  // Quarterstaff
            55 => $"{IconsPath}righthand.svg",  // Scythe
            58 => $"{IconsPath}righthand.svg",  // Short Spear

            // Exotic weapons
            40 => $"{IconsPath}righthand.svg",  // Kama
            60 => $"{IconsPath}righthand.svg",  // Sickle
            111 => $"{IconsPath}righthand.svg", // Whip

            // Ranged weapons
            6 => $"{IconsPath}righthand.svg",   // Heavy Crossbow
            7 => $"{IconsPath}righthand.svg",   // Light Crossbow
            8 => $"{IconsPath}righthand.svg",   // Longbow
            11 => $"{IconsPath}righthand.svg",  // Shortbow
            61 => $"{IconsPath}righthand.svg",  // Sling

            // Thrown weapons
            31 => $"{IconsPath}righthand.svg",  // Dart
            59 => $"{IconsPath}righthand.svg",  // Shuriken

            // Shields (left hand)
            14 => $"{IconsPath}lefthand.svg",   // Small Shield
            56 => $"{IconsPath}lefthand.svg",   // Large Shield
            57 => $"{IconsPath}lefthand.svg",   // Tower Shield

            // Torch
            15 => $"{IconsPath}lefthand.svg",   // Torch

            // Armor
            16 => $"{IconsPath}chest.svg",      // Armor

            // Headwear
            17 => $"{IconsPath}head.svg",       // Helmet

            // Handwear
            36 => $"{IconsPath}arms.svg",       // Gloves
            78 => $"{IconsPath}arms.svg",       // Bracer

            // Footwear
            26 => $"{IconsPath}feet.svg",       // Boots

            // Waist
            21 => $"{IconsPath}belt.svg",       // Belt

            // Back
            80 => $"{IconsPath}cloak.svg",      // Cloak

            // Jewelry
            19 => $"{IconsPath}neck.svg",       // Amulet
            52 => $"{IconsPath}ring.svg",       // Ring

            // Ammunition
            20 => $"{IconsPath}arrows.svg",     // Arrow
            25 => $"{IconsPath}bolts.svg",      // Bolt
            27 => $"{IconsPath}bolts.svg",      // Bullet

            // Magic items (wands, staffs, rods) - use righthand for now
            44 => $"{IconsPath}righthand.svg",  // Magic Rod
            45 => $"{IconsPath}righthand.svg",  // Magic Staff
            46 => $"{IconsPath}righthand.svg",  // Magic Wand

            // Potions - use neck as stand-in for bottle shape
            49 => $"{IconsPath}neck.svg",       // Potions

            // Books and Scrolls - use chest as generic container
            74 => $"{IconsPath}chest.svg",      // Book
            75 => $"{IconsPath}chest.svg",      // Spell Scroll

            // Gold and Gems - use ring as small sparkly item
            76 => $"{IconsPath}ring.svg",       // Gold
            77 => $"{IconsPath}ring.svg",       // Gem

            // Miscellaneous items - use chest (container)
            24 => $"{IconsPath}chest.svg",      // Misc Small
            29 => $"{IconsPath}chest.svg",      // Misc Medium
            34 => $"{IconsPath}chest.svg",      // Misc Large
            79 => $"{IconsPath}chest.svg",      // Misc Thin
            66 => $"{IconsPath}chest.svg",      // Large Box

            // Tools and kits
            39 => $"{IconsPath}chest.svg",      // Healer's Kit
            62 => $"{IconsPath}chest.svg",      // Thieves' Tools
            64 => $"{IconsPath}chest.svg",      // Trap Kit
            65 => $"{IconsPath}chest.svg",      // Key

            // Grenades
            81 => $"{IconsPath}bolts.svg",      // Grenade (round throwable)

            // Crafting components
            109 => $"{IconsPath}chest.svg",     // Craft Component Base
            110 => $"{IconsPath}chest.svg",     // Craft Component Small
            112 => $"{IconsPath}chest.svg",     // Craft Base

            // Creature weapons - use creature icon
            69 => $"{IconsPath}creature.svg",   // Creature Slash Weapon
            70 => $"{IconsPath}creature.svg",   // Creature Pierce Weapon
            71 => $"{IconsPath}creature.svg",   // Creature Bludgeon Weapon
            72 => $"{IconsPath}creature.svg",   // Creature Slash/Pierce Weapon
            73 => $"{IconsPath}creature.svg",   // Creature Item (hide, etc.)

            // Default - use chest for unknown items
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
