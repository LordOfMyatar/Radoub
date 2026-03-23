using Radoub.Formats.Gff;

namespace Radoub.Formats.Utp;

/// <summary>
/// Represents a UTP (Placeable) file used by Aurora Engine games.
/// UTP files are GFF-based and store placeable blueprint data.
/// Reference: BioWare Aurora Situated Object Format specification, neverwinter.nim
/// </summary>
public class UtpFile
{
    /// <summary>
    /// File type signature - should be "UTP "
    /// </summary>
    public string FileType { get; set; } = "UTP ";

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
    /// Placeable tag (max 32 characters)
    /// </summary>
    public string Tag { get; set; } = string.Empty;

    /// <summary>
    /// Localized placeable name
    /// </summary>
    public CExoLocString LocName { get; set; } = new();

    /// <summary>
    /// Localized description
    /// </summary>
    public CExoLocString Description { get; set; } = new();

    // Appearance

    /// <summary>
    /// Appearance ID - index into placeables.2da
    /// </summary>
    public uint Appearance { get; set; }

    /// <summary>
    /// Current animation state (0=default, 1=open, 2=closed, 3=destroyed, 4=activated, 5=deactivated)
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
    /// If true, placeable is a plot object (cannot be damaged/destroyed)
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
    /// DC to unlock this object
    /// </summary>
    public byte OpenLockDC { get; set; }

    /// <summary>
    /// DC to lock the object
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
    /// If true, object is trapped
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

    // Placeable-specific fields

    /// <summary>
    /// If true, placeable has inventory
    /// </summary>
    public bool HasInventory { get; set; }

    /// <summary>
    /// If true, can be used by player
    /// </summary>
    public bool Useable { get; set; } = true;

    /// <summary>
    /// If true, static (client-side only, no scripting)
    /// </summary>
    public bool Static { get; set; }

    /// <summary>
    /// Obsolete type field, always 0
    /// </summary>
    public byte Type { get; set; }

    /// <summary>
    /// Index into bodybag.2da (body bag left when destroyed with inventory)
    /// </summary>
    public byte BodyBag { get; set; }

    /// <summary>
    /// Items in placeable (only if HasInventory)
    /// </summary>
    public List<PlaceableItem> ItemList { get; set; } = new();

    // Script fields

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
    /// OnHeartbeat event script
    /// </summary>
    public string OnHeartbeat { get; set; } = string.Empty;

    /// <summary>
    /// OnInventoryDisturbed event script
    /// </summary>
    public string OnInvDisturbed { get; set; } = string.Empty;

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

    /// <summary>
    /// OnUsed event script
    /// </summary>
    public string OnUsed { get; set; } = string.Empty;

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
    /// Local variables stored on this placeable.
    /// Set via SetLocalInt/SetLocalFloat/SetLocalString script functions.
    /// </summary>
    public List<Variable> VarTable { get; set; } = new();
}

/// <summary>
/// Represents an item in a placeable's inventory.
/// </summary>
public class PlaceableItem
{
    /// <summary>
    /// ResRef of the item blueprint (.uti)
    /// </summary>
    public string InventoryRes { get; set; } = string.Empty;

    /// <summary>
    /// X position in inventory grid
    /// </summary>
    public ushort Repos_PosX { get; set; }

    /// <summary>
    /// Y position in inventory grid
    /// </summary>
    public ushort Repos_PosY { get; set; }
}
