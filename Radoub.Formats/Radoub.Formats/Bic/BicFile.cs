using Radoub.Formats.Gff;
using Radoub.Formats.Utc;

namespace Radoub.Formats.Bic;

/// <summary>
/// Represents a BIC (Player Character) file used by Aurora Engine games.
/// BIC files extend UTC format with player-specific fields.
/// Reference: BioWare Aurora Creature Format specification (Section 2.6), neverwinter.nim
/// </summary>
public class BicFile : UtcFile
{
    /// <summary>
    /// Creates a new BicFile with default values.
    /// </summary>
    public BicFile()
    {
        FileType = "BIC ";
        IsPC = true;
    }

    // Player-specific fields (Table 2.6.1)

    /// <summary>
    /// Character's age (entered during character creation).
    /// </summary>
    public int Age { get; set; }

    /// <summary>
    /// Total experience points earned.
    /// </summary>
    public uint Experience { get; set; }

    /// <summary>
    /// Amount of gold carried by the character.
    /// </summary>
    public uint Gold { get; set; }

    /// <summary>
    /// QuickBar slot assignments (36 slots).
    /// Elements 0-11: Normal QuickBar
    /// Elements 12-23: Shift-QuickBar
    /// Elements 24-35: Control-QuickBar
    /// </summary>
    public List<QuickBarSlot> QBList { get; set; } = new();

    /// <summary>
    /// Faction reputation amounts.
    /// Each entry is the player's rating (0-100) with a faction,
    /// in the same order as the module's repute.fac FactionList.
    /// </summary>
    public List<int> ReputationList { get; set; } = new();
}

/// <summary>
/// Represents a QuickBar slot assignment (StructID 0).
/// </summary>
public class QuickBarSlot
{
    /// <summary>
    /// Type of object in this quickbar slot.
    /// 0=empty, 1=item, 2=spell, 3=skill, 4=feat, 5=script,
    /// 6=dialog, 7=attack, 8=emote, 9=castspell, 10=mode toggle,
    /// 38=possess familiar, 39=associate command, 40=examine,
    /// 41=barter, 42=quickchat, 43=cancel polymorph, 44=spell-like ability
    /// </summary>
    public byte ObjectType { get; set; }

    // Item-specific fields (ObjectType = 1)

    /// <summary>
    /// Object ID of the item in inventory (items only).
    /// </summary>
    public uint ItemInvSlot { get; set; }

    /// <summary>
    /// X position of item in inventory (items only).
    /// </summary>
    public byte ItemReposX { get; set; }

    /// <summary>
    /// Y position of item in inventory (items only).
    /// </summary>
    public byte ItemReposY { get; set; }

    /// <summary>
    /// X position in container (0xFF if not in container).
    /// </summary>
    public byte ContReposX { get; set; } = 0xFF;

    /// <summary>
    /// Y position in container (0xFF if not in container).
    /// </summary>
    public byte ContReposY { get; set; } = 0xFF;

    /// <summary>
    /// Cast property index (0xFF if no cast property).
    /// </summary>
    public byte CastPropIndex { get; set; } = 0xFF;

    /// <summary>
    /// Cast sub-property index (0xFF if no subproperty).
    /// </summary>
    public byte CastSubPropIdx { get; set; } = 0xFF;

    // Spell/Skill/Feat/Mode fields (ObjectType = 2, 3, 4, 10)

    /// <summary>
    /// Index into spells.2da, skills.2da, feat.2da, or mode type.
    /// </summary>
    public int INTParam1 { get; set; }

    /// <summary>
    /// Index into creature's ClassList (spells only).
    /// </summary>
    public byte MultiClass { get; set; }

    /// <summary>
    /// MetaMagic flags on a spell (spells only).
    /// </summary>
    public byte MetaType { get; set; }

    /// <summary>
    /// Domain level for cleric domain spells (0 for most spells).
    /// </summary>
    public byte DomainLevel { get; set; }

    /// <summary>
    /// Secondary parameter (varies by object type).
    /// </summary>
    public int SecondaryItem { get; set; }

    /// <summary>
    /// Command sub-type for associate commands.
    /// </summary>
    public int CommandSubType { get; set; }

    /// <summary>
    /// Command label for associate commands.
    /// </summary>
    public string CommandLabel { get; set; } = string.Empty;

    /// <summary>
    /// Returns true if slot is empty.
    /// </summary>
    public bool IsEmpty => ObjectType == 0;
}

/// <summary>
/// QuickBar object type constants.
/// </summary>
public static class QuickBarObjectType
{
    public const byte Empty = 0;
    public const byte Item = 1;
    public const byte Spell = 2;
    public const byte Skill = 3;
    public const byte Feat = 4;
    public const byte Script = 5;
    public const byte Dialog = 6;
    public const byte Attack = 7;
    public const byte Emote = 8;
    public const byte CastSpell = 9;
    public const byte ModeToggle = 10;
    public const byte PossessFamiliar = 38;
    public const byte AssociateCommand = 39;
    public const byte Examine = 40;
    public const byte Barter = 41;
    public const byte QuickChat = 42;
    public const byte CancelPolymorph = 43;
    public const byte SpellLikeAbility = 44;

    /// <summary>
    /// Get human-readable name for a quickbar object type.
    /// </summary>
    public static string GetTypeName(byte type) => type switch
    {
        Empty => "Empty",
        Item => "Item",
        Spell => "Spell",
        Skill => "Skill",
        Feat => "Feat",
        Script => "Script",
        Dialog => "Dialog",
        Attack => "Attack",
        Emote => "Emote",
        CastSpell => "Cast Spell Property",
        ModeToggle => "Mode Toggle",
        PossessFamiliar => "Possess Familiar",
        AssociateCommand => "Associate Command",
        Examine => "Examine",
        Barter => "Barter",
        QuickChat => "Quick Chat",
        CancelPolymorph => "Cancel Polymorph",
        SpellLikeAbility => "Spell-Like Ability",
        _ => $"Unknown ({type})"
    };
}
