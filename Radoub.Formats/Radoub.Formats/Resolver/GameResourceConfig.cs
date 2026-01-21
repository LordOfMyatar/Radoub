namespace Radoub.Formats.Resolver;

/// <summary>
/// Configuration for GameResourceResolver specifying game paths and resource locations.
/// </summary>
public class GameResourceConfig
{
    /// <summary>
    /// Path to the NWN installation data directory (contains .key files and data/).
    /// </summary>
    public string? GameDataPath { get; set; }

    /// <summary>
    /// Path to the override folder for loose resource files.
    /// If null, defaults to GameDataPath/override.
    /// </summary>
    public string? OverridePath { get; set; }

    /// <summary>
    /// Paths to HAK files to search, in priority order (first = highest priority).
    /// </summary>
    public List<string> HakPaths { get; set; } = new();

    /// <summary>
    /// Path to the base dialog.tlk file.
    /// If null, defaults to GameDataPath/dialog.tlk.
    /// </summary>
    public string? TlkPath { get; set; }

    /// <summary>
    /// Path to the module's custom TLK file (for StrRefs >= 16777216).
    /// </summary>
    public string? CustomTlkPath { get; set; }

    /// <summary>
    /// Path to the base KEY file for BIF resource indexing.
    /// If null, defaults to GameDataPath/nwn_base.key.
    /// </summary>
    public string? KeyFilePath { get; set; }

    /// <summary>
    /// Whether to cache loaded archives (KEY, BIF, HAK) for performance.
    /// Default: true.
    /// </summary>
    public bool CacheArchives { get; set; } = true;

    /// <summary>
    /// Whether to scan HAK files for resources.
    /// Default: false (disabled for performance - scanning 80+ HAKs takes 15+ seconds).
    /// Future: Trebuchet will read module.ifo for specific HAK list.
    /// </summary>
    public bool EnableHakScanning { get; set; } = false;

    /// <summary>
    /// Create a config for a standard NWN:EE installation.
    /// </summary>
    public static GameResourceConfig ForNwnEE(string installPath)
    {
        var dataPath = Path.Combine(installPath, "data");
        return new GameResourceConfig
        {
            GameDataPath = dataPath,
            OverridePath = Path.Combine(installPath, "ovr"),
            TlkPath = Path.Combine(dataPath, "dialog.tlk"),
            KeyFilePath = Path.Combine(dataPath, "nwn_base.key")
        };
    }

    /// <summary>
    /// Create a config for classic NWN (Diamond/GoG).
    /// </summary>
    public static GameResourceConfig ForNwnClassic(string installPath)
    {
        return new GameResourceConfig
        {
            GameDataPath = installPath,
            OverridePath = Path.Combine(installPath, "override"),
            TlkPath = Path.Combine(installPath, "dialog.tlk"),
            KeyFilePath = Path.Combine(installPath, "chitin.key")
        };
    }
}
