using Radoub.Formats.Ssf;
using Radoub.Formats.TwoDA;

namespace Radoub.Formats.Services;

/// <summary>
/// Core game data access service for the Radoub toolset.
/// Provides cached access to 2DA tables, TLK strings, and game resources.
///
/// Resolution priority:
/// 1. Override folder (loose files)
/// 2. HAK files (in configured order)
/// 3. Module resources
/// 4. Base game BIF files (via KEY index)
/// </summary>
public interface IGameDataService : IDisposable
{
    #region 2DA Access

    /// <summary>
    /// Get a 2DA file by name (without extension).
    /// Returns cached instance if previously loaded.
    /// </summary>
    /// <param name="name">2DA name, e.g., "baseitems", "itempropdef"</param>
    /// <returns>Parsed 2DA file, or null if not found</returns>
    TwoDAFile? Get2DA(string name);

    /// <summary>
    /// Get a cell value from a 2DA file.
    /// Convenience method combining Get2DA + GetValue.
    /// </summary>
    /// <param name="twoDAName">2DA name, e.g., "baseitems"</param>
    /// <param name="rowIndex">Row index (0-based)</param>
    /// <param name="columnName">Column name (case-insensitive)</param>
    /// <returns>Cell value, or null if not found/empty</returns>
    string? Get2DAValue(string twoDAName, int rowIndex, string columnName);

    /// <summary>
    /// Check if a 2DA file exists.
    /// </summary>
    bool Has2DA(string name);

    /// <summary>
    /// Clear all cached 2DA files.
    /// Call when resource paths change.
    /// </summary>
    void ClearCache();

    #endregion

    #region TLK String Resolution

    /// <summary>
    /// Resolve a TLK string reference to text.
    /// Automatically handles custom TLK (StrRef >= 16777216).
    /// </summary>
    /// <param name="strRef">String reference</param>
    /// <returns>Resolved string, or null if not found</returns>
    string? GetString(uint strRef);

    /// <summary>
    /// Resolve a TLK string reference from a string value.
    /// Handles "****" and invalid values gracefully.
    /// </summary>
    /// <param name="strRefStr">String containing a numeric StrRef</param>
    /// <returns>Resolved string, or null if invalid/not found</returns>
    string? GetString(string? strRefStr);

    /// <summary>
    /// Check if a custom TLK is loaded.
    /// </summary>
    bool HasCustomTlk { get; }

    /// <summary>
    /// Set the custom TLK path for module-specific strings.
    /// </summary>
    /// <param name="path">Path to custom TLK file, or null to clear</param>
    void SetCustomTlk(string? path);

    #endregion

    #region Resource Access

    /// <summary>
    /// Find a resource by ResRef and type.
    /// </summary>
    /// <param name="resRef">Resource reference name</param>
    /// <param name="resourceType">Resource type ID</param>
    /// <returns>Resource data, or null if not found</returns>
    byte[]? FindResource(string resRef, ushort resourceType);

    /// <summary>
    /// List all resources of a specific type.
    /// </summary>
    /// <param name="resourceType">Resource type ID</param>
    /// <returns>Enumerable of resource info</returns>
    IEnumerable<GameResourceInfo> ListResources(ushort resourceType);

    #endregion

    #region Soundset Access

    /// <summary>
    /// Get a soundset file by its ID from soundset.2da.
    /// </summary>
    /// <param name="soundsetId">Soundset ID (row index in soundset.2da)</param>
    /// <returns>Parsed SSF file, or null if not found</returns>
    SsfFile? GetSoundset(int soundsetId);

    /// <summary>
    /// Get a soundset file by its ResRef.
    /// </summary>
    /// <param name="resRef">SSF ResRef (without extension)</param>
    /// <returns>Parsed SSF file, or null if not found</returns>
    SsfFile? GetSoundsetByResRef(string resRef);

    /// <summary>
    /// Get the ResRef for a soundset from soundset.2da.
    /// </summary>
    /// <param name="soundsetId">Soundset ID</param>
    /// <returns>SSF ResRef, or null if not found</returns>
    string? GetSoundsetResRef(int soundsetId);

    #endregion

    #region Configuration

    /// <summary>
    /// Whether the service is configured with valid game paths.
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Reload configuration from settings.
    /// Call after settings change.
    /// </summary>
    void ReloadConfiguration();

    #endregion
}

/// <summary>
/// Information about a game resource.
/// </summary>
public class GameResourceInfo
{
    /// <summary>
    /// Resource reference name (without extension).
    /// </summary>
    public required string ResRef { get; init; }

    /// <summary>
    /// Resource type ID.
    /// </summary>
    public required ushort ResourceType { get; init; }

    /// <summary>
    /// Source of the resource (Override, HAK, Module, BIF).
    /// </summary>
    public required GameResourceSource Source { get; init; }

    /// <summary>
    /// Path to the source file (for override files) or archive.
    /// </summary>
    public string? SourcePath { get; init; }
}

/// <summary>
/// Source of a game resource in the resolution chain.
/// </summary>
public enum GameResourceSource
{
    /// <summary>Override folder (highest priority)</summary>
    Override,

    /// <summary>HAK file</summary>
    Hak,

    /// <summary>Module resource</summary>
    Module,

    /// <summary>Base game BIF file (lowest priority)</summary>
    Bif
}
