namespace Radoub.UI.Services;

/// <summary>
/// Cross-tool shared item palette cache service.
/// Provides modular per-source caching (BIF, Override, per-HAK) in a shared location
/// so multiple tools can read the same cached data without rebuilding.
///
/// Cache location: ~/Radoub/Cache/ItemPalette/
/// Thread-safe for concurrent reads from multiple tools.
/// </summary>
public interface ISharedPaletteCacheService
{
    /// <summary>
    /// Check if a valid cache exists for a specific source.
    /// Validates version, game paths, and HAK modification times.
    /// </summary>
    /// <param name="source">Source identifier: "bif", "override", or "hak"</param>
    /// <param name="sourcePath">For HAK sources, the full path to the HAK file</param>
    bool HasValidSourceCache(string source, string? sourcePath = null);

    /// <summary>
    /// Load cached items for a specific source.
    /// Returns null if cache doesn't exist or is invalid.
    /// </summary>
    List<SharedPaletteCacheItem>? LoadSourceCache(string source, string? sourcePath = null);

    /// <summary>
    /// Save items to a source-specific cache file.
    /// </summary>
    Task SaveSourceCacheAsync(
        string source,
        List<SharedPaletteCacheItem> items,
        string? validationPath = null,
        DateTime? sourceModified = null);

    /// <summary>
    /// Get aggregated items from all valid source caches.
    /// Returns null if no caches exist.
    /// Thread-safe for concurrent reads.
    /// </summary>
    List<SharedPaletteCacheItem>? GetAggregatedCache();

    /// <summary>
    /// Clear the in-memory aggregated cache (forces reload from disk).
    /// </summary>
    void InvalidateAggregatedCache();

    /// <summary>
    /// Clear a specific source cache file.
    /// </summary>
    void ClearSourceCache(string source, string? sourcePath = null);

    /// <summary>
    /// Clear all cache files.
    /// </summary>
    void ClearAllCaches();

    /// <summary>
    /// Get cache statistics for display.
    /// </summary>
    SharedPaletteCacheStatistics GetCacheStatistics();

    /// <summary>
    /// Get the cache directory path.
    /// </summary>
    string CacheDirectory { get; }
}
