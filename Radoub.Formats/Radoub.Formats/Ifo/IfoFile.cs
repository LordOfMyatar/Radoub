using Radoub.Formats.Gff;

namespace Radoub.Formats.Ifo;

/// <summary>
/// Represents an IFO (module.ifo) file used by Aurora Engine games.
/// IFO files are GFF-based and store module configuration data.
/// Reference: BioWare Aurora IFO Format specification
/// </summary>
public class IfoFile
{
    /// <summary>
    /// File type signature - should be "IFO "
    /// </summary>
    public string FileType { get; set; } = "IFO ";

    /// <summary>
    /// File version - typically "V3.2"
    /// </summary>
    public string FileVersion { get; set; } = "V3.2";

    // Module Metadata

    /// <summary>
    /// Localized module name.
    /// </summary>
    public CExoLocString ModuleName { get; set; } = new();

    /// <summary>
    /// Localized module description.
    /// </summary>
    public CExoLocString ModuleDescription { get; set; } = new();

    /// <summary>
    /// Module tag (max 32 characters).
    /// </summary>
    public string Tag { get; set; } = string.Empty;

    /// <summary>
    /// Module ID - unique identifier.
    /// </summary>
    public string ModuleId { get; set; } = string.Empty;

    /// <summary>
    /// Custom TLK file reference (without .tlk extension).
    /// </summary>
    public string CustomTlk { get; set; } = string.Empty;

    // Version/Requirements

    /// <summary>
    /// Minimum game version required (e.g., "1.69", "1.89").
    /// WARNING: Changing this affects compatibility.
    /// </summary>
    public string MinGameVersion { get; set; } = "1.69";

    /// <summary>
    /// Expansion pack requirements (bit flags).
    /// 0 = Base game, 1 = SoU, 2 = HotU, 3 = Both.
    /// </summary>
    public ushort ExpansionPack { get; set; }

    // HAK Configuration

    /// <summary>
    /// List of HAK files required by this module.
    /// Ordered by priority (first has highest priority).
    /// HAK names without .hak extension.
    /// </summary>
    public List<string> HakList { get; set; } = new();

    // Time Settings

    /// <summary>
    /// Hour when dawn begins (0-23).
    /// </summary>
    public byte DawnHour { get; set; } = 6;

    /// <summary>
    /// Hour when dusk begins (0-23).
    /// </summary>
    public byte DuskHour { get; set; } = 18;

    /// <summary>
    /// Real-time minutes per in-game hour (1-255).
    /// </summary>
    public byte MinutesPerHour { get; set; } = 2;

    /// <summary>
    /// Starting year for new games.
    /// </summary>
    public uint StartYear { get; set; } = 1372;

    /// <summary>
    /// Starting month for new games (1-12).
    /// </summary>
    public byte StartMonth { get; set; } = 1;

    /// <summary>
    /// Starting day for new games (1-28).
    /// </summary>
    public byte StartDay { get; set; } = 1;

    /// <summary>
    /// Starting hour for new games (0-23).
    /// </summary>
    public byte StartHour { get; set; } = 13;

    // Entry Point

    /// <summary>
    /// Area ResRef where players start.
    /// </summary>
    public string EntryArea { get; set; } = string.Empty;

    /// <summary>
    /// Entry point X coordinate.
    /// </summary>
    public float EntryX { get; set; }

    /// <summary>
    /// Entry point Y coordinate.
    /// </summary>
    public float EntryY { get; set; }

    /// <summary>
    /// Entry point Z coordinate.
    /// </summary>
    public float EntryZ { get; set; }

    /// <summary>
    /// Entry point facing direction X component.
    /// </summary>
    public float EntryDirX { get; set; }

    /// <summary>
    /// Entry point facing direction Y component.
    /// </summary>
    public float EntryDirY { get; set; } = 1.0f;

    // Module Scripts

    /// <summary>
    /// Script run when module loads.
    /// </summary>
    public string OnModuleLoad { get; set; } = string.Empty;

    /// <summary>
    /// Script run when client enters.
    /// </summary>
    public string OnClientEnter { get; set; } = string.Empty;

    /// <summary>
    /// Script run when client leaves.
    /// </summary>
    public string OnClientLeave { get; set; } = string.Empty;

    /// <summary>
    /// Module heartbeat script.
    /// </summary>
    public string OnHeartbeat { get; set; } = string.Empty;

    /// <summary>
    /// Script run when item is acquired.
    /// </summary>
    public string OnAcquireItem { get; set; } = string.Empty;

    /// <summary>
    /// Script run when item is activated.
    /// </summary>
    public string OnActivateItem { get; set; } = string.Empty;

    /// <summary>
    /// Script run when item is unacquired.
    /// </summary>
    public string OnUnacquireItem { get; set; } = string.Empty;

    /// <summary>
    /// Script run when player dies.
    /// </summary>
    public string OnPlayerDeath { get; set; } = string.Empty;

    /// <summary>
    /// Script run when player is dying.
    /// </summary>
    public string OnPlayerDying { get; set; } = string.Empty;

    /// <summary>
    /// Script run when player rests.
    /// </summary>
    public string OnPlayerRest { get; set; } = string.Empty;

    /// <summary>
    /// Script run when player equips an item.
    /// </summary>
    public string OnPlayerEquipItem { get; set; } = string.Empty;

    /// <summary>
    /// Script run when player unequips an item.
    /// </summary>
    public string OnPlayerUnequipItem { get; set; } = string.Empty;

    /// <summary>
    /// Script run when player levels up.
    /// </summary>
    public string OnPlayerLevelUp { get; set; } = string.Empty;

    /// <summary>
    /// Script run on user-defined event.
    /// </summary>
    public string OnUserDefined { get; set; } = string.Empty;

    /// <summary>
    /// Script run when respawn button is clicked.
    /// </summary>
    public string OnSpawnButtonDown { get; set; } = string.Empty;

    /// <summary>
    /// Script run when cutscene is aborted.
    /// </summary>
    public string OnCutsceneAbort { get; set; } = string.Empty;

    // Other Settings

    /// <summary>
    /// XP scale percentage (0-200, 100 = normal).
    /// </summary>
    public byte XPScale { get; set; } = 100;

    /// <summary>
    /// Module creator's name (from toolset).
    /// </summary>
    public string Creator { get; set; } = string.Empty;

    /// <summary>
    /// Module version set by creator.
    /// </summary>
    public uint ModuleVersion { get; set; }

    // Area List (read-only for editing)

    /// <summary>
    /// List of area ResRefs in the module (read-only reference).
    /// </summary>
    public List<string> AreaList { get; set; } = new();

    // Local Variables

    /// <summary>
    /// Local variables stored on the module.
    /// Set via SetLocalInt/SetLocalFloat/SetLocalString script functions.
    /// </summary>
    public List<Variable> VarTable { get; set; } = new();
}

/// <summary>
/// Expansion pack flags for IFO files.
/// </summary>
[Flags]
public enum ExpansionPackFlags : ushort
{
    /// <summary>Base NWN only.</summary>
    None = 0,

    /// <summary>Shadows of Undrentide required.</summary>
    ShadowsOfUndrentide = 1,

    /// <summary>Hordes of the Underdark required.</summary>
    HordesOfTheUnderdark = 2,

    /// <summary>Both expansions required.</summary>
    Both = ShadowsOfUndrentide | HordesOfTheUnderdark
}
