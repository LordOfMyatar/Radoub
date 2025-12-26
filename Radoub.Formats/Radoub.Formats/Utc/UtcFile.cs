using Radoub.Formats.Gff;

namespace Radoub.Formats.Utc;

/// <summary>
/// Represents a UTC (Creature Blueprint) file used by Aurora Engine games.
/// UTC files are GFF-based and store creature template data.
/// Reference: BioWare Aurora Creature Format specification, neverwinter.nim
/// </summary>
public class UtcFile
{
    /// <summary>
    /// File type signature - should be "UTC "
    /// </summary>
    public string FileType { get; set; } = "UTC ";

    /// <summary>
    /// File version - typically "V3.2"
    /// </summary>
    public string FileVersion { get; set; } = "V3.2";

    // Blueprint-only fields (Table 2.2)

    /// <summary>
    /// Blueprint resource reference (should match filename)
    /// </summary>
    public string TemplateResRef { get; set; } = string.Empty;

    /// <summary>
    /// Module designer comment (blueprint only)
    /// </summary>
    public string Comment { get; set; } = string.Empty;

    /// <summary>
    /// Palette ID for toolset organization (blueprint only)
    /// </summary>
    public byte PaletteID { get; set; }

    // Identity fields (Table 2.1.1)

    /// <summary>
    /// First name (localized)
    /// </summary>
    public CExoLocString FirstName { get; set; } = new();

    /// <summary>
    /// Last name (localized)
    /// </summary>
    public CExoLocString LastName { get; set; } = new();

    /// <summary>
    /// Tag of this creature
    /// </summary>
    public string Tag { get; set; } = string.Empty;

    /// <summary>
    /// Description of the creature (Examine action)
    /// </summary>
    public CExoLocString Description { get; set; } = new();

    // Basic info fields

    /// <summary>
    /// Index into racialtypes.2da
    /// </summary>
    public byte Race { get; set; }

    /// <summary>
    /// Index into gender.2da (0 = male, 1 = female)
    /// </summary>
    public byte Gender { get; set; }

    /// <summary>
    /// Subrace string (not used by game, scripts can check)
    /// </summary>
    public string Subrace { get; set; } = string.Empty;

    /// <summary>
    /// Name of the creature's deity
    /// </summary>
    public string Deity { get; set; } = string.Empty;

    // Appearance fields

    /// <summary>
    /// Index into appearance.2da
    /// </summary>
    public ushort AppearanceType { get; set; }

    /// <summary>
    /// Phenotype (0 = normal, 1 = fat) - only for MODELTYPE "P" in appearance.2da
    /// </summary>
    public int Phenotype { get; set; }

    /// <summary>
    /// Index into portraits.2da
    /// </summary>
    public ushort PortraitId { get; set; }

    /// <summary>
    /// Index into tailmodel.2da
    /// </summary>
    public byte Tail { get; set; }

    /// <summary>
    /// Index into wingmodel.2da
    /// </summary>
    public byte Wings { get; set; }

    /// <summary>
    /// Index into bodybag.2da
    /// </summary>
    public byte BodyBag { get; set; }

    // Ability scores

    /// <summary>
    /// Strength ability score (before bonuses)
    /// </summary>
    public byte Str { get; set; } = 10;

    /// <summary>
    /// Dexterity ability score (before bonuses)
    /// </summary>
    public byte Dex { get; set; } = 10;

    /// <summary>
    /// Constitution ability score (before bonuses)
    /// </summary>
    public byte Con { get; set; } = 10;

    /// <summary>
    /// Intelligence ability score (before bonuses)
    /// </summary>
    public byte Int { get; set; } = 10;

    /// <summary>
    /// Wisdom ability score (before bonuses)
    /// </summary>
    public byte Wis { get; set; } = 10;

    /// <summary>
    /// Charisma ability score (before bonuses)
    /// </summary>
    public byte Cha { get; set; } = 10;

    // Hit points

    /// <summary>
    /// Base maximum hit points (before bonuses)
    /// </summary>
    public short HitPoints { get; set; }

    /// <summary>
    /// Current hit points
    /// </summary>
    public short CurrentHitPoints { get; set; }

    /// <summary>
    /// Maximum hit points (after all bonuses)
    /// </summary>
    public short MaxHitPoints { get; set; }

    // Combat

    /// <summary>
    /// Natural AC bonus
    /// </summary>
    public byte NaturalAC { get; set; }

    /// <summary>
    /// Calculated challenge rating (see Section 3.1)
    /// </summary>
    public float ChallengeRating { get; set; }

    /// <summary>
    /// Adjustment to challenge rating
    /// </summary>
    public int CRAdjust { get; set; }

    // Saving throw bonuses

    /// <summary>
    /// Fortitude save bonus
    /// </summary>
    public short FortBonus { get; set; }

    /// <summary>
    /// Reflex save bonus
    /// </summary>
    public short RefBonus { get; set; }

    /// <summary>
    /// Will save bonus
    /// </summary>
    public short WillBonus { get; set; }

    // Alignment

    /// <summary>
    /// Good-Evil axis (0 = most Evil, 100 = most Good)
    /// </summary>
    public byte GoodEvil { get; set; } = 50;

    /// <summary>
    /// Law-Chaos axis (0 = most Chaotic, 100 = most Lawful)
    /// </summary>
    public byte LawfulChaotic { get; set; } = 50;

    // Flags

    /// <summary>
    /// True if creature is a plot creature
    /// </summary>
    public bool Plot { get; set; }

    /// <summary>
    /// True if creature can never die
    /// </summary>
    public bool IsImmortal { get; set; }

    /// <summary>
    /// True if creature cannot permanently die
    /// </summary>
    public bool NoPermDeath { get; set; }

    /// <summary>
    /// True if creature is a player character
    /// </summary>
    public bool IsPC { get; set; }

    /// <summary>
    /// True if creature can be disarmed
    /// </summary>
    public bool Disarmable { get; set; }

    /// <summary>
    /// True if creature leaves lootable corpse (vs bodybag)
    /// </summary>
    public bool Lootable { get; set; }

    /// <summary>
    /// True if conversation can be interrupted
    /// </summary>
    public bool Interruptable { get; set; } = true;

    // Behavior

    /// <summary>
    /// Faction ID (index into repute.fac FactionList)
    /// </summary>
    public ushort FactionID { get; set; }

    /// <summary>
    /// Index into ranges.2da (perception range, 9-13)
    /// </summary>
    public byte PerceptionRange { get; set; } = 11;

    /// <summary>
    /// Index into creaturespeed.2da
    /// </summary>
    public int WalkRate { get; set; }

    /// <summary>
    /// Index into soundset.2da
    /// </summary>
    public ushort SoundSetFile { get; set; }

    /// <summary>
    /// Milliseconds before corpse fades
    /// </summary>
    public uint DecayTime { get; set; } = 5000;

    /// <summary>
    /// Index into packages.2da (for LevelUpHenchman)
    /// </summary>
    public byte StartingPackage { get; set; }

    // Conversation

    /// <summary>
    /// ResRef of conversation file (.dlg)
    /// </summary>
    public string Conversation { get; set; } = string.Empty;

    // Scripts

    /// <summary>
    /// OnPhysicalAttacked event script
    /// </summary>
    public string ScriptAttacked { get; set; } = string.Empty;

    /// <summary>
    /// OnDamaged event script
    /// </summary>
    public string ScriptDamaged { get; set; } = string.Empty;

    /// <summary>
    /// OnDeath event script
    /// </summary>
    public string ScriptDeath { get; set; } = string.Empty;

    /// <summary>
    /// OnConversation event script
    /// </summary>
    public string ScriptDialogue { get; set; } = string.Empty;

    /// <summary>
    /// OnInventoryDisturbed event script
    /// </summary>
    public string ScriptDisturbed { get; set; } = string.Empty;

    /// <summary>
    /// OnEndCombatRound event script
    /// </summary>
    public string ScriptEndRound { get; set; } = string.Empty;

    /// <summary>
    /// OnHeartbeat event script
    /// </summary>
    public string ScriptHeartbeat { get; set; } = string.Empty;

    /// <summary>
    /// OnBlocked event script
    /// </summary>
    public string ScriptOnBlocked { get; set; } = string.Empty;

    /// <summary>
    /// OnPerception event script
    /// </summary>
    public string ScriptOnNotice { get; set; } = string.Empty;

    /// <summary>
    /// OnRested event script
    /// </summary>
    public string ScriptRested { get; set; } = string.Empty;

    /// <summary>
    /// OnSpawnIn event script
    /// </summary>
    public string ScriptSpawn { get; set; } = string.Empty;

    /// <summary>
    /// OnSpellCastAt event script
    /// </summary>
    public string ScriptSpellAt { get; set; } = string.Empty;

    /// <summary>
    /// OnUserDefined event script
    /// </summary>
    public string ScriptUserDefine { get; set; } = string.Empty;

    // Lists

    /// <summary>
    /// List of creature classes (1-3 elements)
    /// </summary>
    public List<CreatureClass> ClassList { get; set; } = new();

    /// <summary>
    /// List of feats (indexes into feat.2da)
    /// </summary>
    public List<ushort> FeatList { get; set; } = new();

    /// <summary>
    /// List of skill ranks (one per row in skills.2da)
    /// </summary>
    public List<byte> SkillList { get; set; } = new();

    /// <summary>
    /// List of special abilities
    /// </summary>
    public List<SpecialAbility> SpecAbilityList { get; set; } = new();

    /// <summary>
    /// Inventory items (backpack contents)
    /// </summary>
    public List<InventoryItem> ItemList { get; set; } = new();

    /// <summary>
    /// Equipped items (mapped to equipment slots)
    /// </summary>
    public List<EquippedItem> EquipItemList { get; set; } = new();
}

/// <summary>
/// Represents a creature class entry (StructID 2).
/// </summary>
public class CreatureClass
{
    /// <summary>
    /// Index into classes.2da
    /// </summary>
    public int Class { get; set; }

    /// <summary>
    /// Level in this class
    /// </summary>
    public short ClassLevel { get; set; }
}

/// <summary>
/// Represents a special ability (StructID 4).
/// </summary>
public class SpecialAbility
{
    /// <summary>
    /// Index into spells.2da
    /// </summary>
    public ushort Spell { get; set; }

    /// <summary>
    /// Caster level to use this spell as
    /// </summary>
    public byte SpellCasterLevel { get; set; }

    /// <summary>
    /// Bit flags: 0x01=readied, 0x02=spontaneous, 0x04=unlimited
    /// </summary>
    public byte SpellFlags { get; set; } = 0x01;
}

/// <summary>
/// Represents an item in the creature's inventory (backpack).
/// Uses InventoryObject struct from Items GFF document Section 3.
/// </summary>
public class InventoryItem
{
    /// <summary>
    /// ResRef of the item blueprint (.uti)
    /// </summary>
    public string InventoryRes { get; set; } = string.Empty;

    /// <summary>
    /// X position in inventory grid (0-based)
    /// </summary>
    public ushort Repos_PosX { get; set; }

    /// <summary>
    /// Y position in inventory grid (0-based)
    /// </summary>
    public ushort Repos_PosY { get; set; }

    /// <summary>
    /// If true, this is a dropable item (creature drops on death)
    /// </summary>
    public bool Dropable { get; set; } = true;

    /// <summary>
    /// If true, item can be pickpocketed
    /// </summary>
    public bool Pickpocketable { get; set; }
}

/// <summary>
/// Represents an equipped item.
/// Blueprint uses EquipRes field; instances embed full item data.
/// </summary>
public class EquippedItem
{
    /// <summary>
    /// Equipment slot (bit flag from Equip_ItemList struct ID).
    /// HEAD=0x1, CHEST=0x2, BOOTS=0x4, ARMS=0x8, RIGHTHAND=0x10, LEFTHAND=0x20,
    /// CLOAK=0x40, LEFTRING=0x80, RIGHTRING=0x100, NECK=0x200, BELT=0x400,
    /// ARROWS=0x800, BULLETS=0x1000, BOLTS=0x2000
    /// </summary>
    public int Slot { get; set; }

    /// <summary>
    /// ResRef of the equipped item blueprint (blueprint mode)
    /// </summary>
    public string EquipRes { get; set; } = string.Empty;
}

/// <summary>
/// Equipment slot constants (bit flags from Equip_ItemList struct IDs).
/// </summary>
public static class EquipmentSlots
{
    public const int Head = 0x1;
    public const int Chest = 0x2;
    public const int Boots = 0x4;
    public const int Arms = 0x8;
    public const int RightHand = 0x10;
    public const int LeftHand = 0x20;
    public const int Cloak = 0x40;
    public const int LeftRing = 0x80;
    public const int RightRing = 0x100;
    public const int Neck = 0x200;
    public const int Belt = 0x400;
    public const int Arrows = 0x800;
    public const int Bullets = 0x1000;
    public const int Bolts = 0x2000;

    /// <summary>
    /// Get human-readable name for equipment slot.
    /// </summary>
    public static string GetSlotName(int slot)
    {
        return slot switch
        {
            Head => "Head",
            Chest => "Chest",
            Boots => "Boots",
            Arms => "Arms",
            RightHand => "Right Hand",
            LeftHand => "Left Hand",
            Cloak => "Cloak",
            LeftRing => "Left Ring",
            RightRing => "Right Ring",
            Neck => "Neck",
            Belt => "Belt",
            Arrows => "Arrows",
            Bullets => "Bullets",
            Bolts => "Bolts",
            _ => $"Unknown ({slot:X})"
        };
    }
}
