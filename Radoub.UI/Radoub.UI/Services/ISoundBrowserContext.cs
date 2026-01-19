namespace Radoub.UI.Services;

/// <summary>
/// Represents a sound entry in the browser with metadata.
/// </summary>
public class SoundEntry
{
    /// <summary>
    /// Sound filename without extension (e.g., "vs_femelf_att1").
    /// </summary>
    public string ResRef { get; set; } = "";

    /// <summary>
    /// Source of the sound file (e.g., "Override", "BIF: sounds", "HAK: custom.hak").
    /// </summary>
    public string Source { get; set; } = "";

    /// <summary>
    /// Full path to the file (for loose files) or null (for archived).
    /// </summary>
    public string? FilePath { get; set; }

    /// <summary>
    /// Whether this is a mono sound (required for NWN voice/sfx).
    /// </summary>
    public bool IsMono { get; set; }

    /// <summary>
    /// Whether this is a valid WAV file format.
    /// </summary>
    public bool IsValidWav { get; set; } = true;

    /// <summary>
    /// Whether this sound comes from a HAK archive.
    /// </summary>
    public bool IsFromHak { get; set; }

    /// <summary>
    /// Whether this sound comes from a BIF archive.
    /// </summary>
    public bool IsFromBif { get; set; }

    /// <summary>
    /// Display name for the sound.
    /// </summary>
    public string DisplayName => ResRef;
}

/// <summary>
/// Interface for providing context to the sound browser.
/// Implementations provide tool-specific paths and audio services.
/// Issue #970 - Part of Epic #959 (UI Uniformity).
/// </summary>
public interface ISoundBrowserContext
{
    /// <summary>
    /// The current file's directory (for local sound lookup).
    /// </summary>
    string? CurrentFileDirectory { get; }

    /// <summary>
    /// The Neverwinter Nights user documents path.
    /// Used to find override sounds and HAK files.
    /// </summary>
    string? NeverwinterNightsPath { get; }

    /// <summary>
    /// The base game installation path.
    /// Used for BIF and game sound lookup.
    /// </summary>
    string? BaseGameInstallPath { get; }

    /// <summary>
    /// Whether game resources (BIF files) are available for sound lookup.
    /// </summary>
    bool GameResourcesAvailable { get; }

    /// <summary>
    /// Lists sounds from the specified sources.
    /// </summary>
    /// <param name="includeOverride">Include override folder sounds</param>
    /// <param name="includeHak">Include HAK file sounds</param>
    /// <param name="includeBif">Include BIF archive sounds</param>
    /// <returns>List of sound entries</returns>
    IEnumerable<SoundEntry> ListSounds(bool includeOverride = true, bool includeHak = true, bool includeBif = true);

    /// <summary>
    /// Extracts a sound to a temporary file for playback.
    /// </summary>
    /// <param name="entry">Sound entry to extract</param>
    /// <returns>Path to temporary file, or null if extraction failed</returns>
    string? ExtractToTemp(SoundEntry entry);

    /// <summary>
    /// Plays a sound by path.
    /// </summary>
    /// <param name="path">Path to sound file</param>
    void Play(string path);

    /// <summary>
    /// Stops any currently playing sound.
    /// </summary>
    void Stop();
}
