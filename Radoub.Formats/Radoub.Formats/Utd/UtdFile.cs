using Radoub.Formats.Gff;

namespace Radoub.Formats.Utd;

/// <summary>
/// Represents a UTD (Door) file used by Aurora Engine games.
/// UTD files are GFF-based and store door blueprint data.
/// Reference: BioWare Aurora Situated Object Format specification, neverwinter.nim
/// </summary>
public class UtdFile
{
    /// <summary>
    /// File type signature - should be "UTD "
    /// </summary>
    public string FileType { get; set; } = "UTD ";

    /// <summary>
    /// File version - typically "V3.2"
    /// </summary>
    public string FileVersion { get; set; } = "V3.2";

    // Identity fields

    /// <summary>
    /// Blueprint resource reference (should match filename)
    /// </summary>
    public string TemplateResRef { get; set; } = string.Empty;

    /// <summary>
    /// Door tag (max 32 characters)
    /// </summary>
    public string Tag { get; set; } = string.Empty;

    /// <summary>
    /// Localized door name
    /// </summary>
    public CExoLocString LocName { get; set; } = new();

    /// <summary>
    /// Localized description
    /// </summary>
    public CExoLocString Description { get; set; } = new();

    // Appearance

    /// <summary>
    /// Appearance ID - index into doortypes.2da (if >0, use this; if 0, use GenericType)
    /// </summary>
    public uint Appearance { get; set; }

    /// <summary>
    /// Generic door type - index into genericdoors.2da (used when Appearance=0)
    /// </summary>
    public byte GenericType { get; set; }

    /// <summary>
    /// Current animation state (0=closed, 1=opened1, 2=opened2)
    /// </summary>
    public byte AnimationState { get; set; }

    /// <summary>
    /// Portrait ID - index into portraits.2da
    /// </summary>
    public ushort PortraitId { get; set; }

    // Combat / Physical

    /// <summary>
    /// Maximum hit points
    /// </summary>
    public short HP { get; set; }

    /// <summary>
    /// Current hit points
    /// </summary>
    public short CurrentHP { get; set; }

    /// <summary>
    /// Damage reduction (slashing, piercing, bludgeoning)
    /// </summary>
    public byte Hardness { get; set; }

    /// <summary>
    /// Fortitude save bonus
    /// </summary>
    public byte Fort { get; set; }

    /// <summary>
    /// Reflex save bonus
    /// </summary>
    public byte Ref { get; set; }

    /// <summary>
    /// Will save bonus
    /// </summary>
    public byte Will { get; set; }

    /// <summary>
    /// If true, door is a plot object (cannot be damaged/destroyed)
    /// </summary>
    public bool Plot { get; set; }

    /// <summary>
    /// Faction ID from repute.fac
    /// </summary>
    public uint Faction { get; set; }

    /// <summary>
    /// If true, conversation can be interrupted
    /// </summary>
    public bool Interruptable { get; set; } = true;

    // Lock fields

    /// <summary>
    /// If true, can be locked after unlock
    /// </summary>
    public bool Lockable { get; set; }

    /// <summary>
    /// If true, currently locked
    /// </summary>
    public bool Locked { get; set; }

    /// <summary>
    /// DC to unlock this door
    /// </summary>
    public byte OpenLockDC { get; set; }

    /// <summary>
    /// DC to lock the door
    /// </summary>
    public byte CloseLockDC { get; set; }

    /// <summary>
    /// If true, destroy key from inventory when used
    /// </summary>
    public bool AutoRemoveKey { get; set; }

    /// <summary>
    /// Tag of the key required to unlock
    /// </summary>
    public string KeyName { get; set; } = string.Empty;

    /// <summary>
    /// If true, key is required to unlock (KeyName must be set)
    /// </summary>
    public bool KeyRequired { get; set; }

    // Trap fields

    /// <summary>
    /// If true, door is trapped
    /// </summary>
    public bool TrapFlag { get; set; }

    /// <summary>
    /// Index into traps.2da
    /// </summary>
    public byte TrapType { get; set; }

    /// <summary>
    /// If true, trap can be detected
    /// </summary>
    public bool TrapDetectable { get; set; } = true;

    /// <summary>
    /// DC to detect trap (1-250)
    /// </summary>
    public byte TrapDetectDC { get; set; }

    /// <summary>
    /// If true, trap can be disarmed
    /// </summary>
    public bool TrapDisarmable { get; set; } = true;

    /// <summary>
    /// DC to disarm trap
    /// </summary>
    public byte DisarmDC { get; set; }

    /// <summary>
    /// If true, trap disappears after firing
    /// </summary>
    public bool TrapOneShot { get; set; } = true;

    // Door-specific fields

    /// <summary>
    /// Tag of waypoint or door for area transition
    /// </summary>
    public string LinkedTo { get; set; } = string.Empty;

    /// <summary>
    /// Link type: 0=no link, 1=links to door, 2=links to waypoint
    /// </summary>
    public byte LinkedToFlags { get; set; }

    /// <summary>
    /// Index into loadscreens.2da (0 = use destination default)
    /// </summary>
    public ushort LoadScreenID { get; set; }

    // Script fields

    /// <summary>
    /// OnAreaTransitionClick event script
    /// </summary>
    public string OnClick { get; set; } = string.Empty;

    /// <summary>
    /// OnClosed event script
    /// </summary>
    public string OnClosed { get; set; } = string.Empty;

    /// <summary>
    /// OnDamaged event script
    /// </summary>
    public string OnDamaged { get; set; } = string.Empty;

    /// <summary>
    /// OnDeath event script
    /// </summary>
    public string OnDeath { get; set; } = string.Empty;

    /// <summary>
    /// OnDisarm event script
    /// </summary>
    public string OnDisarm { get; set; } = string.Empty;

    /// <summary>
    /// OnFailToOpen event script
    /// </summary>
    public string OnFailToOpen { get; set; } = string.Empty;

    /// <summary>
    /// OnHeartbeat event script
    /// </summary>
    public string OnHeartbeat { get; set; } = string.Empty;

    /// <summary>
    /// OnLock event script
    /// </summary>
    public string OnLock { get; set; } = string.Empty;

    /// <summary>
    /// OnPhysicalAttacked event script
    /// </summary>
    public string OnMeleeAttacked { get; set; } = string.Empty;

    /// <summary>
    /// OnOpen event script
    /// </summary>
    public string OnOpen { get; set; } = string.Empty;

    /// <summary>
    /// OnSpellCastAt event script
    /// </summary>
    public string OnSpellCastAt { get; set; } = string.Empty;

    /// <summary>
    /// OnTrapTriggered event script
    /// </summary>
    public string OnTrapTriggered { get; set; } = string.Empty;

    /// <summary>
    /// OnUnlock event script
    /// </summary>
    public string OnUnlock { get; set; } = string.Empty;

    /// <summary>
    /// OnUserDefined event script
    /// </summary>
    public string OnUserDefined { get; set; } = string.Empty;

    // Metadata / Blueprint fields

    /// <summary>
    /// ResRef of .dlg file for conversations
    /// </summary>
    public string Conversation { get; set; } = string.Empty;

    /// <summary>
    /// Module designer comment (blueprint only)
    /// </summary>
    public string Comment { get; set; } = string.Empty;

    /// <summary>
    /// Palette ID for toolset organization (0-255)
    /// </summary>
    public byte PaletteID { get; set; }

    // Local variables

    /// <summary>
    /// Local variables stored on this door.
    /// Set via SetLocalInt/SetLocalFloat/SetLocalString script functions.
    /// </summary>
    public List<Variable> VarTable { get; set; } = new();
}
