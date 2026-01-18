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
    /// Index into portraits.2da.
    /// When non-zero, takes precedence over Portrait string field.
    /// </summary>
    public ushort PortraitId { get; set; }

    /// <summary>
    /// Portrait ResRef (base name without size suffix or po_ prefix).
    /// Used when PortraitId is 0. Common format: "hu_m_99_" for human male #99.
    /// </summary>
    public string Portrait { get; set; } = string.Empty;

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

    // Body part fields (for dynamic/part-based appearances)
    // These are only meaningful when appearance.2da MODELTYPE is "P" (part-based)
    // Values index into model_*.2da files (e.g., model_heads, model_torso, etc.)
    // CEP/PRC can add custom parts, so don't hardcode limits

    /// <summary>
    /// Head appearance index (Appearance_Head field)
    /// </summary>
    public byte AppearanceHead { get; set; }

    /// <summary>
    /// Body part: Belt
    /// </summary>
    public byte BodyPart_Belt { get; set; }

    /// <summary>
    /// Body part: Left Bicep
    /// </summary>
    public byte BodyPart_LBicep { get; set; }

    /// <summary>
    /// Body part: Right Bicep
    /// </summary>
    public byte BodyPart_RBicep { get; set; }

    /// <summary>
    /// Body part: Left Forearm
    /// </summary>
    public byte BodyPart_LFArm { get; set; }

    /// <summary>
    /// Body part: Right Forearm
    /// </summary>
    public byte BodyPart_RFArm { get; set; }

    /// <summary>
    /// Body part: Left Foot
    /// </summary>
    public byte BodyPart_LFoot { get; set; }

    /// <summary>
    /// Body part: Right Foot
    /// </summary>
    public byte BodyPart_RFoot { get; set; }

    /// <summary>
    /// Body part: Left Hand
    /// </summary>
    public byte BodyPart_LHand { get; set; }

    /// <summary>
    /// Body part: Right Hand
    /// </summary>
    public byte BodyPart_RHand { get; set; }

    /// <summary>
    /// Body part: Left Shin
    /// </summary>
    public byte BodyPart_LShin { get; set; }

    /// <summary>
    /// Body part: Right Shin
    /// </summary>
    public byte BodyPart_RShin { get; set; }

    /// <summary>
    /// Body part: Left Shoulder
    /// </summary>
    public byte BodyPart_LShoul { get; set; }

    /// <summary>
    /// Body part: Right Shoulder
    /// </summary>
    public byte BodyPart_RShoul { get; set; }

    /// <summary>
    /// Body part: Left Thigh
    /// </summary>
    public byte BodyPart_LThigh { get; set; }

    /// <summary>
    /// Body part: Right Thigh
    /// </summary>
    public byte BodyPart_RThigh { get; set; }

    /// <summary>
    /// Body part: Neck
    /// </summary>
    public byte BodyPart_Neck { get; set; }

    /// <summary>
    /// Body part: Pelvis
    /// </summary>
    public byte BodyPart_Pelvis { get; set; }

    /// <summary>
    /// Body part: Torso
    /// </summary>
    public byte BodyPart_Torso { get; set; }

    // Colors (for part-based appearances)

    /// <summary>
    /// Skin color index into pal_skin01.tga palette.
    /// </summary>
    public byte Color_Skin { get; set; }

    /// <summary>
    /// Hair color index into pal_hair01.tga palette.
    /// </summary>
    public byte Color_Hair { get; set; }

    /// <summary>
    /// Tattoo 1 color index into pal_tattoo01.tga palette.
    /// </summary>
    public byte Color_Tattoo1 { get; set; }

    /// <summary>
    /// Tattoo 2 color index into pal_tattoo01.tga palette.
    /// </summary>
    public byte Color_Tattoo2 { get; set; }

    // Armor Part Appearance (game instance fields - copied from equipped armor)
    // These override BodyPart fields when armor is equipped

    /// <summary>
    /// Armor appearance: Belt (from equipped armor)
    /// </summary>
    public byte ArmorPart_Belt { get; set; }

    /// <summary>
    /// Armor appearance: Left Bicep (from equipped armor)
    /// </summary>
    public byte ArmorPart_LBicep { get; set; }

    /// <summary>
    /// Armor appearance: Right Bicep (from equipped armor)
    /// </summary>
    public byte ArmorPart_RBicep { get; set; }

    /// <summary>
    /// Armor appearance: Left Forearm (from equipped armor)
    /// </summary>
    public byte ArmorPart_LFArm { get; set; }

    /// <summary>
    /// Armor appearance: Right Forearm (from equipped armor)
    /// </summary>
    public byte ArmorPart_RFArm { get; set; }

    /// <summary>
    /// Armor appearance: Left Foot (from equipped armor)
    /// </summary>
    public byte ArmorPart_LFoot { get; set; }

    /// <summary>
    /// Armor appearance: Right Foot (from equipped armor)
    /// </summary>
    public byte ArmorPart_RFoot { get; set; }

    /// <summary>
    /// Armor appearance: Left Hand (from equipped armor)
    /// </summary>
    public byte ArmorPart_LHand { get; set; }

    /// <summary>
    /// Armor appearance: Right Hand (from equipped armor)
    /// </summary>
    public byte ArmorPart_RHand { get; set; }

    /// <summary>
    /// Armor appearance: Left Shin (from equipped armor)
    /// </summary>
    public byte ArmorPart_LShin { get; set; }

    /// <summary>
    /// Armor appearance: Right Shin (from equipped armor)
    /// </summary>
    public byte ArmorPart_RShin { get; set; }

    /// <summary>
    /// Armor appearance: Left Shoulder (from equipped armor)
    /// </summary>
    public byte ArmorPart_LShoul { get; set; }

    /// <summary>
    /// Armor appearance: Right Shoulder (from equipped armor)
    /// </summary>
    public byte ArmorPart_RShoul { get; set; }

    /// <summary>
    /// Armor appearance: Left Thigh (from equipped armor)
    /// </summary>
    public byte ArmorPart_LThigh { get; set; }

    /// <summary>
    /// Armor appearance: Right Thigh (from equipped armor)
    /// </summary>
    public byte ArmorPart_RThigh { get; set; }

    /// <summary>
    /// Armor appearance: Neck (from equipped armor)
    /// </summary>
    public byte ArmorPart_Neck { get; set; }

    /// <summary>
    /// Armor appearance: Pelvis (from equipped armor)
    /// </summary>
    public byte ArmorPart_Pelvis { get; set; }

    /// <summary>
    /// Armor appearance: Torso (from equipped armor)
    /// </summary>
    public byte ArmorPart_Torso { get; set; }

    /// <summary>
    /// Armor appearance: Robe (from equipped armor)
    /// </summary>
    public byte ArmorPart_Robe { get; set; }

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

    /// <summary>
    /// Known spells by spell level (0-9).
    /// Used by Bards, Sorcerers, and PC Wizards (spellbook).
    /// </summary>
    public List<KnownSpell>[] KnownSpells { get; set; } = CreateKnownSpellArrays();

    /// <summary>
    /// Memorized spells by spell level (0-9).
    /// Used by Wizards and divine casters (prepared spellcasters).
    /// </summary>
    public List<MemorizedSpell>[] MemorizedSpells { get; set; } = CreateMemorizedSpellArrays();

    private static List<KnownSpell>[] CreateKnownSpellArrays()
    {
        var arrays = new List<KnownSpell>[10];
        for (int i = 0; i < 10; i++)
            arrays[i] = new List<KnownSpell>();
        return arrays;
    }

    private static List<MemorizedSpell>[] CreateMemorizedSpellArrays()
    {
        var arrays = new List<MemorizedSpell>[10];
        for (int i = 0; i < 10; i++)
            arrays[i] = new List<MemorizedSpell>();
        return arrays;
    }
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
/// Represents a known spell in a KnownList (StructID 3).
/// Used by Bards, Sorcerers, and PC Wizard spellbooks.
/// </summary>
public class KnownSpell
{
    /// <summary>
    /// Index into spells.2da
    /// </summary>
    public ushort Spell { get; set; }

    /// <summary>
    /// Bit flags: 0x01=readied (always set), 0x02=spontaneous, 0x04=unlimited
    /// </summary>
    public byte SpellFlags { get; set; } = 0x01;

    /// <summary>
    /// Metamagic type applied: 0x00=none, 0x01=empower, 0x02=extend,
    /// 0x04=maximize, 0x08=quicken, 0x10=silent, 0x20=still
    /// </summary>
    public byte SpellMetaMagic { get; set; }
}

/// <summary>
/// Represents a memorized (prepared) spell in a MemorizedList (StructID 3).
/// Used by Wizards and divine casters.
/// </summary>
public class MemorizedSpell
{
    /// <summary>
    /// Index into spells.2da
    /// </summary>
    public ushort Spell { get; set; }

    /// <summary>
    /// Bit flags: 0x01=readied (always set), 0x02=spontaneous, 0x04=unlimited
    /// </summary>
    public byte SpellFlags { get; set; } = 0x01;

    /// <summary>
    /// Metamagic type applied: 0x00=none, 0x01=empower, 0x02=extend,
    /// 0x04=maximize, 0x08=quicken, 0x10=silent, 0x20=still
    /// </summary>
    public byte SpellMetaMagic { get; set; }

    /// <summary>
    /// 1 if the spell is readied for casting (game instance field).
    /// This is separate from SpellFlags.
    /// </summary>
    public int Ready { get; set; } = 1;
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
