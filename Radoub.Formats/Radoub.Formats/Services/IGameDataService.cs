using Radoub.Formats.Itp;
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
    /// Searches Override → HAK → BIF.
    /// </summary>
    /// <param name="resRef">Resource reference name</param>
    /// <param name="resourceType">Resource type ID</param>
    /// <returns>Resource data, or null if not found</returns>
    byte[]? FindResource(string resRef, ushort resourceType);

    /// <summary>
    /// Find a resource in Override and BIF only, skipping HAK files.
    /// Use for resources that must come from the base game regardless of HAK overrides
    /// (e.g., standard race skeletons that CEP HAKs may replace with incompatible versions).
    /// </summary>
    /// <param name="resRef">Resource reference name</param>
    /// <param name="resourceType">Resource type ID</param>
    /// <returns>Resource data, or null if not found</returns>
    byte[]? FindBaseResource(string resRef, ushort resourceType);

    /// <summary>
    /// Find a resource including source information (which file it came from).
    /// Resolution order: Override → HAK → BIF.
    /// </summary>
    Radoub.Formats.Resolver.ResourceResult? FindResourceWithSource(string resRef, ushort resourceType);

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

    #region Palette Access

    /// <summary>
    /// Get palette categories for a specific resource type.
    /// Loads from skeleton palette (e.g., creaturepal.itp for UTC).
    /// </summary>
    /// <param name="resourceType">Resource type ID (e.g., 2027 for UTC)</param>
    /// <returns>List of palette categories with IDs and names</returns>
    IEnumerable<PaletteCategory> GetPaletteCategories(ushort resourceType);

    /// <summary>
    /// Get palette category name by ID.
    /// </summary>
    /// <param name="resourceType">Resource type ID</param>
    /// <param name="categoryId">Category ID (PaletteID from blueprint)</param>
    /// <returns>Category name, or null if not found</returns>
    string? GetPaletteCategoryName(ushort resourceType, byte categoryId);

    #endregion

    #region Game Rules

    // These rules accessors source NWN game mechanics from 2DA so that BIC/UTC
    // conversion and similar logic does not bake stock-game constants into the
    // format layer (#2481). They are default-implemented in terms of the 2DA
    // primitives above; an implementation may override them for caching or for
    // rulesets the stock tables do not describe. When the backing table is
    // missing they fall back to the stock NWN value so callers always get a
    // usable answer.

    /// <summary>
    /// Minimum experience required to be a given total character level, read from
    /// exptable.2da (row = level - 1, column "XP"). Falls back to the stock NWN
    /// formula (N-1)*N/2*1000 when the table or cell is unavailable.
    /// </summary>
    /// <param name="level">Total character level (1-based).</param>
    uint GetXpForLevel(int level)
    {
        if (level <= 1)
            return Has2DA("exptable")
                ? ParseUInt(Get2DAValue("exptable", 0, "XP")) ?? 0u
                : 0u;

        var cell = Get2DAValue("exptable", level - 1, "XP");
        var parsed = ParseUInt(cell);
        if (parsed.HasValue)
            return parsed.Value;

        // Stock NWN: level N requires (N-1)*N/2 * 1000 XP.
        return (uint)((long)(level - 1) * level / 2 * 1000);
    }

    /// <summary>
    /// Hit die size (e.g. 8 for d8) for a class, read from classes.2da column
    /// "HitDie" at the class row. Returns 0 when the table or cell is unavailable
    /// (the stock conversion path treats 0 as "average/unrolled").
    /// </summary>
    /// <param name="classId">Class index into classes.2da.</param>
    int GetClassHitDie(int classId)
    {
        if (classId < 0)
            return 0;
        var cell = Get2DAValue("classes", classId, "HitDie");
        return ParseInt(cell) ?? 0;
    }

    /// <summary>
    /// Number of skills defined in skills.2da. Custom content (PRC, etc.) adds
    /// rows past the stock 28. Falls back to 28 when the table is unavailable.
    /// </summary>
    int GetSkillCount()
    {
        var skills = Get2DA("skills");
        return skills != null && skills.RowCount > 0 ? skills.RowCount : 28;
    }

    /// <summary>
    /// Lowest character level considered epic. Fixed at 21 in stock NWN (epic =
    /// level 21+); exposed as an accessor so custom rulesets can override it.
    /// </summary>
    int GetEpicLevelThreshold() => 21;

    /// <summary>Parse a 2DA cell as an unsigned integer; null if empty/invalid.</summary>
    private static uint? ParseUInt(string? cell)
        => uint.TryParse(cell, out var v) ? v : (uint?)null;

    /// <summary>Parse a 2DA cell as a signed integer; null if empty/invalid.</summary>
    private static int? ParseInt(string? cell)
        => int.TryParse(cell, out var v) ? v : (int?)null;

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

    /// <summary>
    /// Configure module-aware HAK scanning by reading the module's IFO HakList.
    /// Only HAK files referenced by the module will be loaded into the resolver,
    /// avoiding the performance penalty of scanning all HAK files (80+ files, 15+ seconds).
    /// Clears all caches (2DA, SSF, palette) since resource resolution order changes.
    /// </summary>
    /// <param name="moduleDirectory">Path to the unpacked module directory containing module.ifo.</param>
    void ConfigureModuleHaks(string moduleDirectory);

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

/// <summary>
/// Represents a palette category from an ITP file.
/// </summary>
public class PaletteCategory
{
    /// <summary>
    /// Category ID (matches PaletteID in blueprints).
    /// </summary>
    public byte Id { get; init; }

    /// <summary>
    /// Display name (resolved from TLK or direct name).
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Parent branch path for tree display (e.g., "Animals/Domestic").
    /// </summary>
    public string? ParentPath { get; init; }

    /// <summary>
    /// Full display path (Parent + Name).
    /// </summary>
    public string FullPath => string.IsNullOrEmpty(ParentPath) ? Name : $"{ParentPath}/{Name}";
}
