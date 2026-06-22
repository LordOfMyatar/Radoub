using System.Text.Json.Serialization;
using Radoub.Formats.Services;

namespace Radoub.UI.Services;

/// <summary>
/// Minimal item data for cross-tool palette caching.
/// Stores only what's needed for display and filtering, not the full UTI.
/// Shared across all tools (Quartermaster, Fence, future tools).
/// </summary>
public class SharedPaletteCacheItem
{
    public string ResRef { get; set; } = string.Empty;
    public string Tag { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string BaseItemTypeName { get; set; } = string.Empty;
    public int BaseItemType { get; set; }
    public uint BaseValue { get; set; }

    /// <summary>
    /// Leaf palette category id (PaletteID from the blueprint). Null when the
    /// UTI parse failed or the cache predates v5. Category name/path are resolved
    /// at read time from GameDataService, not stored here. (#987)
    /// </summary>
    public byte? PaletteId { get; set; }

    /// <summary>
    /// Which game resource bucket this item came from (Bif/Override/Hak/Module). Persisted so the
    /// palette filter can distinguish all four sources (#1995). Replaces the former bool IsStandard.
    /// </summary>
    public GameResourceSource Source { get; set; } = GameResourceSource.Bif;

    public string PropertiesDisplay { get; set; } = string.Empty;
    public string SourceLocation { get; set; } = string.Empty;

    /// <summary>
    /// Convenience: true when this item is base-game (BIF). Not serialized — derived from Source.
    /// Retained so existing call sites and tests that only need the standard-vs-custom split keep working.
    /// </summary>
    [JsonIgnore]
    public bool IsStandard => Source == GameResourceSource.Bif;
}

/// <summary>
/// Wrapper for per-source cache files with validation metadata.
/// </summary>
public class SourcePaletteCacheWrapper
{
    public int Version { get; set; }
    public string Source { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string? ValidationPath { get; set; }
    public DateTime? SourceModified { get; set; }
    public List<SharedPaletteCacheItem> Items { get; set; } = new();
}

/// <summary>
/// Cache statistics for display in settings or diagnostics.
/// </summary>
public class SharedPaletteCacheStatistics
{
    public int TotalItems { get; set; }
    public double TotalSizeKB { get; set; }
    public Dictionary<string, int> SourceCounts { get; } = new();
}
