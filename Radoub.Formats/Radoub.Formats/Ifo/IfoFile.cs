using Radoub.Formats.Gff;

namespace Radoub.Formats.Ifo;

/// <summary>
/// Represents an IFO (module.ifo) file used by Aurora Engine games.
/// IFO files are GFF-based and store module configuration data.
/// Reference: BioWare Aurora IFO Format specification
/// </summary>
public class IfoFile
{
    /// <summary>File type signature - should be "IFO "</summary>
    public string FileType { get; set; } = "IFO ";

    /// <summary>File version - typically "V3.2"</summary>
    public string FileVersion { get; set; } = "V3.2";

    public CExoLocString ModuleName { get; set; } = new();

    public CExoLocString ModuleDescription { get; set; } = new();

    /// <summary>Module tag (max 32 characters).</summary>
    public string Tag { get; set; } = string.Empty;

    public string ModuleId { get; set; } = string.Empty;

    /// <summary>Custom TLK file reference (without .tlk extension).</summary>
    public string CustomTlk { get; set; } = string.Empty;

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

    /// <summary>
    /// HAK files required by this module, ordered by priority (first = highest).
    /// Names carry no .hak extension.
    /// </summary>
    public List<string> HakList { get; set; } = new();

    /// <summary>Hour when dawn begins (0-23).</summary>
    public byte DawnHour { get; set; } = 6;

    /// <summary>Hour when dusk begins (0-23).</summary>
    public byte DuskHour { get; set; } = 18;

    /// <summary>Real-time minutes per in-game hour (1-255).</summary>
    public byte MinutesPerHour { get; set; } = 2;

    /// <summary>Starting year for new games.</summary>
    public uint StartYear { get; set; } = 1372;

    /// <summary>Starting month for new games (1-12).</summary>
    public byte StartMonth { get; set; } = 1;

    /// <summary>Starting day for new games (1-28).</summary>
    public byte StartDay { get; set; } = 1;

    /// <summary>Starting hour for new games (0-23).</summary>
    public byte StartHour { get; set; } = 13;

    /// <summary>Area ResRef where players start.</summary>
    public string EntryArea { get; set; } = string.Empty;

    public float EntryX { get; set; }

    public float EntryY { get; set; }

    public float EntryZ { get; set; }

    /// <summary>Entry point facing direction X component.</summary>
    public float EntryDirX { get; set; }

    /// <summary>Entry point facing direction Y component.</summary>
    public float EntryDirY { get; set; } = 1.0f;

    // Module Scripts

    public string OnModuleLoad { get; set; } = string.Empty;

    public string OnClientEnter { get; set; } = string.Empty;

    public string OnClientLeave { get; set; } = string.Empty;

    public string OnHeartbeat { get; set; } = string.Empty;

    public string OnAcquireItem { get; set; } = string.Empty;

    public string OnActivateItem { get; set; } = string.Empty;

    public string OnUnacquireItem { get; set; } = string.Empty;

    public string OnPlayerDeath { get; set; } = string.Empty;

    public string OnPlayerDying { get; set; } = string.Empty;

    public string OnPlayerRest { get; set; } = string.Empty;

    public string OnPlayerEquipItem { get; set; } = string.Empty;

    public string OnPlayerUnequipItem { get; set; } = string.Empty;

    public string OnPlayerLevelUp { get; set; } = string.Empty;

    public string OnUserDefined { get; set; } = string.Empty;

    /// <summary>Script run when respawn button is clicked.</summary>
    public string OnSpawnButtonDown { get; set; } = string.Empty;

    public string OnCutsceneAbort { get; set; } = string.Empty;

    // NWN:EE Extended Scripts (1.69+)

    /// <summary>Script run when module starts (after OnModuleLoad).</summary>
    public string OnModuleStart { get; set; } = string.Empty;

    public string OnPlayerChat { get; set; } = string.Empty;

    public string OnPlayerTarget { get; set; } = string.Empty;

    public string OnPlayerGuiEvent { get; set; } = string.Empty;

    public string OnPlayerTileAction { get; set; } = string.Empty;

    /// <summary>Script run on NUI (New User Interface) events. (NWN:EE 1.80+)</summary>
    public string OnNuiEvent { get; set; } = string.Empty;

    /// <summary>XP scale percentage (0-200, 100 = normal).</summary>
    public byte XPScale { get; set; } = 100;

    /// <summary>Module creator's name (from toolset).</summary>
    public string Creator { get; set; } = string.Empty;

    /// <summary>Module version set by creator.</summary>
    public uint ModuleVersion { get; set; }

    /// <summary>Whether this is a savegame (not a module).</summary>
    public byte IsSaveGame { get; set; }

    public string StartMovie { get; set; } = string.Empty;

    /// <summary>Default BIC (character) file for new characters.</summary>
    public string DefaultBic { get; set; } = string.Empty;

    public string ModuleUuid { get; set; } = string.Empty;

    /// <summary>Party control setting (0 = DM control, 1 = Player control).</summary>
    public byte PartyControl { get; set; }

    /// <summary>Area ResRefs in the module (read-only reference).</summary>
    public List<string> AreaList { get; set; } = new();

    /// <summary>
    /// Local variables stored on the module.
    /// Set via SetLocalInt/SetLocalFloat/SetLocalString script functions.
    /// </summary>
    public List<Variable> VarTable { get; set; } = new();

    // Additional Lists (preserved for round-trip)

    public List<GffStruct> ExpansionList { get; set; } = new();

    public List<GffStruct> CutSceneList { get; set; } = new();

    /// <summary>Global variable list (savegame only).</summary>
    public List<GffStruct> GlobalVarList { get; set; } = new();
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
