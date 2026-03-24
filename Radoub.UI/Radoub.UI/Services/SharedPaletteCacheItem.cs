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
    public bool IsStandard { get; set; }
    public string PropertiesDisplay { get; set; } = string.Empty;
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
