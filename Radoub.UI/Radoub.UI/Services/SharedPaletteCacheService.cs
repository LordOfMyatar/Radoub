using System.Diagnostics;
using System.Text.Json;
using Radoub.Formats.Logging;

namespace Radoub.UI.Services;

/// <summary>
/// Cross-tool shared item palette cache with per-source granularity.
/// Each source type (BIF, Override, per-HAK) gets its own cache file with independent validation.
/// Cache location: ~/Radoub/Cache/ItemPalette/ (shared across all tools).
/// Thread-safe for concurrent reads from multiple tools.
/// </summary>
public class SharedPaletteCacheService : ISharedPaletteCacheService
{
    private const int CacheVersion = 3; // v3: Added SourceLocation

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false
    };

    private readonly string _cacheDirectory;
    private readonly ReaderWriterLockSlim _lock = new();
    private List<SharedPaletteCacheItem>? _aggregatedCache;

    /// <summary>
    /// Create a shared palette cache service using the default shared location.
    /// </summary>
    public SharedPaletteCacheService()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Radoub", "Cache", "ItemPalette"))
    {
    }

    /// <summary>
    /// Create a shared palette cache service with a custom cache directory (for testing).
    /// </summary>
    public SharedPaletteCacheService(string cacheDirectory)
    {
        _cacheDirectory = cacheDirectory;
    }

    public string CacheDirectory => _cacheDirectory;

    private string GetCacheFilePath(string source, string? sourcePath = null)
    {
        return source.ToLowerInvariant() switch
        {
            "bif" => Path.Combine(_cacheDirectory, "bif.json"),
            "override" => Path.Combine(_cacheDirectory, "override.json"),
            "hak" when sourcePath != null =>
                Path.Combine(_cacheDirectory, $"hak_{SanitizeFileName(Path.GetFileNameWithoutExtension(sourcePath))}.json"),
            _ => Path.Combine(_cacheDirectory, $"{SanitizeFileName(source)}.json")
        };
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(name.Select(c => invalid.Contains(c) ? '_' : c)).ToLowerInvariant();
    }

    public bool HasValidSourceCache(string source, string? sourcePath = null)
    {
        var cacheFile = GetCacheFilePath(source, sourcePath);
        if (!File.Exists(cacheFile))
            return false;

        try
        {
            var json = File.ReadAllText(cacheFile);
            var cache = JsonSerializer.Deserialize<SourcePaletteCacheWrapper>(json, JsonOptions);
            if (cache == null)
                return false;

            if (cache.Version != CacheVersion)
            {
                UnifiedLogger.LogApplication(LogLevel.INFO, $"Shared palette cache version mismatch for {source}");
                return false;
            }

            // HAK-specific: validate file modification time
            if (source.Equals("hak", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrEmpty(sourcePath) && File.Exists(sourcePath))
            {
                var hakModified = File.GetLastWriteTimeUtc(sourcePath);
                if (cache.SourceModified != hakModified)
                {
                    UnifiedLogger.LogApplication(LogLevel.INFO,
                        $"Shared palette HAK cache invalidated - {Path.GetFileName(sourcePath)} modified");
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN,
                $"Failed to validate shared palette {source} cache: {ex.Message}");
            return false;
        }
    }

    public List<SharedPaletteCacheItem>? LoadSourceCache(string source, string? sourcePath = null)
    {
        var cacheFile = GetCacheFilePath(source, sourcePath);
        if (!File.Exists(cacheFile))
            return null;

        try
        {
            var json = File.ReadAllText(cacheFile);
            var cache = JsonSerializer.Deserialize<SourcePaletteCacheWrapper>(json, JsonOptions);
            if (cache?.Items != null)
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG,
                    $"Loaded {cache.Items.Count} items from shared palette {source} cache");
                return cache.Items;
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN,
                $"Failed to load shared palette {source} cache: {ex.Message}");
        }

        return null;
    }

    public async Task SaveSourceCacheAsync(
        string source,
        List<SharedPaletteCacheItem> items,
        string? validationPath = null,
        DateTime? sourceModified = null)
    {
        try
        {
            if (!Directory.Exists(_cacheDirectory))
                Directory.CreateDirectory(_cacheDirectory);

            var cache = new SourcePaletteCacheWrapper
            {
                Version = CacheVersion,
                Source = source.ToLowerInvariant(),
                CreatedAt = DateTime.UtcNow,
                ValidationPath = validationPath,
                SourceModified = sourceModified,
                Items = items
            };

            var cacheFile = GetCacheFilePath(source, validationPath);
            var tempFile = cacheFile + ".tmp";
            var json = JsonSerializer.Serialize(cache, JsonOptions);

            // Atomic write: write to temp file then move (rename is atomic on NTFS and POSIX)
            await File.WriteAllTextAsync(tempFile, json);
            File.Move(tempFile, cacheFile, overwrite: true);

            // Invalidate aggregated cache AFTER the atomic move, not after the temp write
            InvalidateAggregatedCache();

            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"Saved {items.Count} items to shared palette {source} cache");
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR,
                $"Failed to save shared palette {source} cache: {ex.Message}");
        }
    }

    public List<SharedPaletteCacheItem>? GetAggregatedCache()
    {
        _lock.EnterUpgradeableReadLock();
        try
        {
            if (_aggregatedCache != null)
                return _aggregatedCache;

            _lock.EnterWriteLock();
            try
            {
                // Double-check after acquiring write lock
                if (_aggregatedCache != null)
                    return _aggregatedCache;

                var allItems = new List<SharedPaletteCacheItem>();

                if (!Directory.Exists(_cacheDirectory))
                    return null;

                foreach (var file in Directory.GetFiles(_cacheDirectory, "*.json"))
                {
                    try
                    {
                        var json = File.ReadAllText(file);
                        var cache = JsonSerializer.Deserialize<SourcePaletteCacheWrapper>(json, JsonOptions);
                        if (cache?.Items == null || cache.Version != CacheVersion)
                            continue;

                        // HAK caches need modification time validation
                        if (cache.Source == "hak" &&
                            !string.IsNullOrEmpty(cache.ValidationPath))
                        {
                            if (!File.Exists(cache.ValidationPath))
                                continue;

                            var hakModified = File.GetLastWriteTimeUtc(cache.ValidationPath);
                            if (cache.SourceModified != hakModified)
                            {
                                UnifiedLogger.LogApplication(LogLevel.DEBUG,
                                    $"Skipping stale HAK cache: {Path.GetFileName(file)}");
                                continue;
                            }
                        }

                        allItems.AddRange(cache.Items);
                    }
                    catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
                    {
                        UnifiedLogger.LogApplication(LogLevel.DEBUG,
                            $"Skipping invalid cache file '{Path.GetFileName(file)}': {ex.Message}");
                    }
                }

                if (allItems.Count > 0)
                {
                    _aggregatedCache = allItems;
                    UnifiedLogger.LogApplication(LogLevel.INFO,
                        $"Shared palette aggregated cache: {allItems.Count} items from all sources");
                    return _aggregatedCache;
                }

                return null;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }
        finally
        {
            _lock.ExitUpgradeableReadLock();
        }
    }

    public List<SharedPaletteCacheItem>? GetAggregatedCache(IEnumerable<string>? activeHakPaths)
    {
        // null filter = include everything
        if (activeHakPaths == null)
            return GetAggregatedCache();

        // Build a set of allowed HAK paths for fast lookup
        var allowedHaks = new HashSet<string>(
            activeHakPaths.Select(p => p.ToLowerInvariant()),
            StringComparer.OrdinalIgnoreCase);

        _lock.EnterReadLock();
        try
        {
            if (!Directory.Exists(_cacheDirectory))
                return null;

            var allItems = new List<SharedPaletteCacheItem>();

            foreach (var file in Directory.GetFiles(_cacheDirectory, "*.json"))
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var cache = JsonSerializer.Deserialize<SourcePaletteCacheWrapper>(json, JsonOptions);
                    if (cache?.Items == null || cache.Version != CacheVersion)
                        continue;

                    // HAK caches: only include if in the active HAK list
                    if (cache.Source == "hak")
                    {
                        if (string.IsNullOrEmpty(cache.ValidationPath))
                            continue;

                        // Check if this HAK is in the active list
                        if (!allowedHaks.Contains(cache.ValidationPath.ToLowerInvariant()))
                            continue;

                        // Still validate modification time
                        if (File.Exists(cache.ValidationPath))
                        {
                            var hakModified = File.GetLastWriteTimeUtc(cache.ValidationPath);
                            if (cache.SourceModified != hakModified)
                            {
                                UnifiedLogger.LogApplication(LogLevel.DEBUG,
                                    $"Skipping stale HAK cache: {Path.GetFileName(file)}");
                                continue;
                            }
                        }
                        else
                        {
                            continue; // HAK file no longer exists
                        }
                    }

                    allItems.AddRange(cache.Items);
                }
                catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
                {
                    UnifiedLogger.LogApplication(LogLevel.DEBUG,
                        $"Skipping invalid cache file '{Path.GetFileName(file)}': {ex.Message}");
                }
            }

            if (allItems.Count > 0)
            {
                UnifiedLogger.LogApplication(LogLevel.INFO,
                    $"Shared palette filtered aggregation: {allItems.Count} items (active HAKs: {allowedHaks.Count})");
                return allItems;
            }

            return null;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public void InvalidateAggregatedCache()
    {
        _lock.EnterWriteLock();
        try
        {
            _aggregatedCache = null;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public void ClearSourceCache(string source, string? sourcePath = null)
    {
        var cacheFile = GetCacheFilePath(source, sourcePath);
        if (File.Exists(cacheFile))
        {
            File.Delete(cacheFile);
            UnifiedLogger.LogApplication(LogLevel.INFO, $"Cleared shared palette {source} cache");
        }
        InvalidateAggregatedCache();
    }

    public void ClearAllCaches()
    {
        if (Directory.Exists(_cacheDirectory))
        {
            foreach (var file in Directory.GetFiles(_cacheDirectory, "*.json"))
            {
                try
                {
                    File.Delete(file);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    UnifiedLogger.LogApplication(LogLevel.WARN,
                        $"Could not delete cache file '{Path.GetFileName(file)}': {ex.Message}");
                }
            }

            // Also clean up any .building sentinel files
            foreach (var file in Directory.GetFiles(_cacheDirectory, "*.building"))
            {
                try
                {
                    File.Delete(file);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    UnifiedLogger.LogApplication(LogLevel.WARN,
                        $"Could not delete sentinel file '{Path.GetFileName(file)}': {ex.Message}");
                }
            }

            UnifiedLogger.LogApplication(LogLevel.INFO, "Cleared all shared palette caches");
        }
        InvalidateAggregatedCache();
    }

    public SharedPaletteCacheStatistics GetCacheStatistics()
    {
        var stats = new SharedPaletteCacheStatistics();

        if (!Directory.Exists(_cacheDirectory))
            return stats;

        foreach (var file in Directory.GetFiles(_cacheDirectory, "*.json"))
        {
            try
            {
                var fileInfo = new FileInfo(file);
                var json = File.ReadAllText(file);
                var cache = JsonSerializer.Deserialize<SourcePaletteCacheWrapper>(json, JsonOptions);

                if (cache != null)
                {
                    stats.TotalItems += cache.Items.Count;
                    stats.TotalSizeKB += fileInfo.Length / 1024.0;

                    // Accumulate counts per source (HAK caches all use "hak" key)
                    if (stats.SourceCounts.ContainsKey(cache.Source))
                        stats.SourceCounts[cache.Source] += cache.Items.Count;
                    else
                        stats.SourceCounts[cache.Source] = cache.Items.Count;
                }
            }
            catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG,
                    $"Could not read cache file '{Path.GetFileName(file)}': {ex.Message}");
            }
        }

        return stats;
    }

    #region Build Lock Sentinels

    private string GetSentinelFilePath(string source, string? sourcePath = null)
    {
        return GetCacheFilePath(source, sourcePath) + ".building";
    }

    public bool AcquireBuildLock(string source, string? sourcePath = null)
    {
        var sentinelFile = GetSentinelFilePath(source, sourcePath);

        if (!Directory.Exists(_cacheDirectory))
            Directory.CreateDirectory(_cacheDirectory);

        // Check if sentinel already exists
        if (File.Exists(sentinelFile))
        {
            if (IsLockStale(sentinelFile))
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG,
                    $"Build lock stale for {source}, taking over");
                try { File.Delete(sentinelFile); }
                catch { /* race condition OK — another process may have cleaned it */ }
            }
            else
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG,
                    $"Build lock held by another process for {source}");
                return false;
            }
        }

        try
        {
            var sentinel = JsonSerializer.Serialize(new BuildLockSentinel
            {
                Pid = Environment.ProcessId,
                StartedAt = DateTime.UtcNow
            }, SentinelJsonOptions);
            File.WriteAllText(sentinelFile, sentinel);

            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Build lock acquired for {source}");
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN,
                $"Failed to acquire build lock for {source}: {ex.Message}");
            return false;
        }
    }

    public void ReleaseBuildLock(string source, string? sourcePath = null)
    {
        var sentinelFile = GetSentinelFilePath(source, sourcePath);
        try
        {
            if (File.Exists(sentinelFile))
            {
                File.Delete(sentinelFile);
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Build lock released for {source}");
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN,
                $"Failed to release build lock for {source}: {ex.Message}");
        }
    }

    public async Task<bool> WaitForBuildLock(string source, string? sourcePath = null, int timeout = 60000, CancellationToken cancellationToken = default)
    {
        var sentinelFile = GetSentinelFilePath(source, sourcePath);

        // No sentinel exists — return false immediately (no lock to wait for)
        if (!File.Exists(sentinelFile))
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                $"Build lock: no sentinel for {source}, no lock to wait for");
            return false;
        }

        UnifiedLogger.LogApplication(LogLevel.DEBUG,
            $"Build lock held for {source}, waiting...");

        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeout)
        {
            if (cancellationToken.IsCancellationRequested)
                return false;

            if (!File.Exists(sentinelFile))
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG,
                    $"Build lock cleared for {source}, cache available");
                return true;
            }

            // Check for stale lock
            if (IsLockStale(sentinelFile))
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG,
                    $"Build lock stale for {source}, proceeding");
                try { File.Delete(sentinelFile); }
                catch { /* OK */ }
                return false;
            }

            try
            {
                await Task.Delay(500, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        UnifiedLogger.LogApplication(LogLevel.DEBUG,
            $"Build lock wait timeout for {source}, proceeding with own build");
        return false;
    }

    private bool IsLockStale(string sentinelFile)
    {
        try
        {
            var json = File.ReadAllText(sentinelFile);
            var sentinel = JsonSerializer.Deserialize<BuildLockSentinel>(json, SentinelJsonOptions);
            if (sentinel == null)
                return true;

            // Check if PID is still alive
            try
            {
                Process.GetProcessById(sentinel.Pid);
            }
            catch (ArgumentException)
            {
                // Process not found — PID is dead
                UnifiedLogger.LogApplication(LogLevel.DEBUG,
                    $"Build lock stale (PID {sentinel.Pid} dead)");
                return true;
            }

            // Check if sentinel is older than 5 minutes
            if (DateTime.UtcNow - sentinel.StartedAt > TimeSpan.FromMinutes(5))
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG,
                    $"Build lock stale (age > 5 min, PID {sentinel.Pid})");
                return true;
            }

            return false;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return true; // Can't read sentinel — treat as stale
        }
    }

    #endregion

    private static readonly JsonSerializerOptions SentinelJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private class BuildLockSentinel
    {
        public int Pid { get; set; }
        public DateTime StartedAt { get; set; }
    }
}
