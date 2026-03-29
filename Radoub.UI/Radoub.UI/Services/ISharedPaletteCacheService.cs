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
    /// Get aggregated items, filtering HAK caches to only those in the active HAK list.
    /// BIF and Override caches are always included. HAK caches not in the filter are excluded.
    /// Pass null to include all HAKs (same as parameterless overload).
    /// Pass empty to exclude all HAKs.
    /// Filtered results are NOT cached in memory (different modules = different filters).
    /// </summary>
    List<SharedPaletteCacheItem>? GetAggregatedCache(IEnumerable<string>? activeHakPaths);

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

    /// <summary>
    /// Acquire a build lock for a source cache. Creates a .building sentinel file with PID + timestamp.
    /// Returns true if lock acquired, false if another live process holds it.
    /// Stale locks (dead PID or older than 5 minutes) are automatically taken over.
    /// </summary>
    bool AcquireBuildLock(string source, string? sourcePath = null);

    /// <summary>
    /// Release a build lock for a source cache. Deletes the .building sentinel file.
    /// </summary>
    void ReleaseBuildLock(string source, string? sourcePath = null);

    /// <summary>
    /// Wait for an existing build lock to clear. If no sentinel exists, returns false immediately
    /// (no lock to wait for — caller should build). If sentinel exists, polls until cleared or timeout.
    /// Returns true if cache became available, false on timeout or no sentinel.
    /// </summary>
    Task<bool> WaitForBuildLock(string source, string? sourcePath = null, int timeout = 60000, CancellationToken cancellationToken = default);
}
