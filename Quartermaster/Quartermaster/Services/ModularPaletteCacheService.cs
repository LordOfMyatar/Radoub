using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Radoub.Formats.Logging;
using Radoub.Formats.Services;
using Radoub.Formats.Settings;

namespace Quartermaster.Services;

/// <summary>
/// Service for caching item palette data with per-source granularity.
/// Each source type (BIF, Override, HAK) gets its own cache file with independent invalidation.
/// Module folder items are not cached (already unpacked files).
/// </summary>
public class ModularPaletteCacheService
{
    private const int CacheVersion = 1;
    private readonly string _cacheDirectory;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false
    };

    // In-memory cache aggregated from all sources
    private List<CachedPaletteItem>? _aggregatedCache;

    public ModularPaletteCacheService()
    {
        _cacheDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Radoub", "Quartermaster", "cache");
    }

    /// <summary>
    /// Get cache file path for a specific source.
    /// </summary>
    private string GetCacheFilePath(GameResourceSource source, string? sourcePath = null)
    {
        return source switch
        {
            GameResourceSource.Bif => Path.Combine(_cacheDirectory, "bif.json"),
            GameResourceSource.Override => Path.Combine(_cacheDirectory, "override.json"),
            GameResourceSource.Hak when sourcePath != null =>
                Path.Combine(_cacheDirectory, $"hak_{SanitizeFileName(Path.GetFileNameWithoutExtension(sourcePath))}.json"),
            _ => Path.Combine(_cacheDirectory, "other.json")
        };
    }

    /// <summary>
    /// Sanitize filename for cache file naming.
    /// </summary>
    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(name.Select(c => invalid.Contains(c) ? '_' : c)).ToLowerInvariant();
    }

    /// <summary>
    /// Check if a source cache is valid.
    /// </summary>
    public bool HasValidSourceCache(GameResourceSource source, string? sourcePath = null)
    {
        var cacheFile = GetCacheFilePath(source, sourcePath);
        if (!File.Exists(cacheFile))
            return false;

        try
        {
            var json = File.ReadAllText(cacheFile);
            var cache = JsonSerializer.Deserialize<SourceCacheWrapper>(json, JsonOptions);
            if (cache == null)
                return false;

            // Version check
            if (cache.Version != CacheVersion)
            {
                UnifiedLogger.LogInventory(LogLevel.INFO, $"Cache version mismatch for {source}");
                return false;
            }

            // Source-specific validation
            var settings = RadoubSettings.Instance;
            switch (source)
            {
                case GameResourceSource.Bif:
                    if (cache.ValidationPath != settings.BaseGameInstallPath)
                    {
                        UnifiedLogger.LogInventory(LogLevel.INFO, "BIF cache invalidated - game path changed");
                        return false;
                    }
                    break;

                case GameResourceSource.Override:
                    if (cache.ValidationPath != settings.NeverwinterNightsPath)
                    {
                        UnifiedLogger.LogInventory(LogLevel.INFO, "Override cache invalidated - NWN path changed");
                        return false;
                    }
                    break;

                case GameResourceSource.Hak:
                    // Validate HAK file hasn't changed (check modified time)
                    if (!string.IsNullOrEmpty(sourcePath) && File.Exists(sourcePath))
                    {
                        var hakModified = File.GetLastWriteTimeUtc(sourcePath);
                        if (cache.SourceModified != hakModified)
                        {
                            UnifiedLogger.LogInventory(LogLevel.INFO, $"HAK cache invalidated - {Path.GetFileName(sourcePath)} modified");
                            return false;
                        }
                    }
                    break;
            }

            return true;
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogInventory(LogLevel.WARN, $"Failed to validate {source} cache: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Load cached items for a specific source.
    /// </summary>
    public List<CachedPaletteItem>? LoadSourceCache(GameResourceSource source, string? sourcePath = null)
    {
        var cacheFile = GetCacheFilePath(source, sourcePath);
        if (!File.Exists(cacheFile))
            return null;

        try
        {
            var json = File.ReadAllText(cacheFile);
            var cache = JsonSerializer.Deserialize<SourceCacheWrapper>(json, JsonOptions);
            if (cache?.Items != null)
            {
                UnifiedLogger.LogInventory(LogLevel.DEBUG, $"Loaded {cache.Items.Count} items from {source} cache");
                return cache.Items;
            }
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogInventory(LogLevel.WARN, $"Failed to load {source} cache: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// Save items to a source-specific cache.
    /// </summary>
    public async Task SaveSourceCacheAsync(
        GameResourceSource source,
        List<CachedPaletteItem> items,
        string? sourcePath = null)
    {
        try
        {
            if (!Directory.Exists(_cacheDirectory))
                Directory.CreateDirectory(_cacheDirectory);

            var settings = RadoubSettings.Instance;
            var cache = new SourceCacheWrapper
            {
                Version = CacheVersion,
                Source = source,
                CreatedAt = DateTime.UtcNow,
                Items = items
            };

            // Set validation path based on source type
            switch (source)
            {
                case GameResourceSource.Bif:
                    cache.ValidationPath = settings.BaseGameInstallPath;
                    break;
                case GameResourceSource.Override:
                    cache.ValidationPath = settings.NeverwinterNightsPath;
                    break;
                case GameResourceSource.Hak when !string.IsNullOrEmpty(sourcePath):
                    cache.ValidationPath = sourcePath;
                    cache.SourceModified = File.GetLastWriteTimeUtc(sourcePath);
                    break;
            }

            var cacheFile = GetCacheFilePath(source, sourcePath);
            var json = JsonSerializer.Serialize(cache, JsonOptions);
            await File.WriteAllTextAsync(cacheFile, json);

            UnifiedLogger.LogInventory(LogLevel.INFO, $"Saved {items.Count} items to {source} cache");
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogInventory(LogLevel.ERROR, $"Failed to save {source} cache: {ex.Message}");
        }
    }

    /// <summary>
    /// Get aggregated cache from all valid source caches.
    /// Returns null if no caches exist.
    /// </summary>
    public List<CachedPaletteItem>? GetAggregatedCache()
    {
        if (_aggregatedCache != null)
            return _aggregatedCache;

        var allItems = new List<CachedPaletteItem>();

        // Load BIF cache
        if (HasValidSourceCache(GameResourceSource.Bif))
        {
            var bifItems = LoadSourceCache(GameResourceSource.Bif);
            if (bifItems != null)
                allItems.AddRange(bifItems);
        }

        // Load Override cache
        if (HasValidSourceCache(GameResourceSource.Override))
        {
            var overrideItems = LoadSourceCache(GameResourceSource.Override);
            if (overrideItems != null)
                allItems.AddRange(overrideItems);
        }

        // Load all HAK caches
        if (Directory.Exists(_cacheDirectory))
        {
            foreach (var hakCache in Directory.GetFiles(_cacheDirectory, "hak_*.json"))
            {
                try
                {
                    var json = File.ReadAllText(hakCache);
                    var cache = JsonSerializer.Deserialize<SourceCacheWrapper>(json, JsonOptions);
                    if (cache?.Items != null && cache.Version == CacheVersion)
                    {
                        // Validate HAK still exists and hasn't changed
                        if (!string.IsNullOrEmpty(cache.ValidationPath) && File.Exists(cache.ValidationPath))
                        {
                            var hakModified = File.GetLastWriteTimeUtc(cache.ValidationPath);
                            if (cache.SourceModified == hakModified)
                            {
                                allItems.AddRange(cache.Items);
                            }
                            else
                            {
                                UnifiedLogger.LogInventory(LogLevel.DEBUG, $"Skipping stale HAK cache: {Path.GetFileName(hakCache)}");
                            }
                        }
                    }
                }
                catch
                {
                    // Skip invalid cache files
                }
            }
        }

        if (allItems.Count > 0)
        {
            _aggregatedCache = allItems;
            UnifiedLogger.LogInventory(LogLevel.INFO, $"Aggregated cache: {allItems.Count} items from all sources");
            return _aggregatedCache;
        }

        return null;
    }

    /// <summary>
    /// Clear the in-memory aggregated cache (forces reload from disk on next access).
    /// </summary>
    public void InvalidateAggregatedCache()
    {
        _aggregatedCache = null;
    }

    /// <summary>
    /// Clear a specific source cache.
    /// </summary>
    public void ClearSourceCache(GameResourceSource source, string? sourcePath = null)
    {
        var cacheFile = GetCacheFilePath(source, sourcePath);
        if (File.Exists(cacheFile))
        {
            File.Delete(cacheFile);
            UnifiedLogger.LogInventory(LogLevel.INFO, $"Cleared {source} cache");
        }
        InvalidateAggregatedCache();
    }

    /// <summary>
    /// Clear all cache files.
    /// </summary>
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
                catch
                {
                    // Ignore deletion errors
                }
            }
            UnifiedLogger.LogInventory(LogLevel.INFO, "Cleared all palette caches");
        }
        InvalidateAggregatedCache();
    }

    /// <summary>
    /// Get cache statistics for display.
    /// </summary>
    public CacheStatistics GetCacheStatistics()
    {
        var stats = new CacheStatistics();

        if (!Directory.Exists(_cacheDirectory))
            return stats;

        foreach (var file in Directory.GetFiles(_cacheDirectory, "*.json"))
        {
            try
            {
                var fileInfo = new FileInfo(file);
                var json = File.ReadAllText(file);
                var cache = JsonSerializer.Deserialize<SourceCacheWrapper>(json, JsonOptions);

                if (cache != null)
                {
                    stats.TotalItems += cache.Items.Count;
                    stats.TotalSizeKB += fileInfo.Length / 1024.0;
                    stats.SourceCounts[cache.Source] = cache.Items.Count;
                }
            }
            catch
            {
                // Skip invalid files
            }
        }

        return stats;
    }
}

/// <summary>
/// Wrapper for per-source cache files.
/// </summary>
public class SourceCacheWrapper
{
    public int Version { get; set; }
    public GameResourceSource Source { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? ValidationPath { get; set; }
    public DateTime? SourceModified { get; set; }
    public List<CachedPaletteItem> Items { get; set; } = new();
}

/// <summary>
/// Cache statistics for display in settings.
/// </summary>
public class CacheStatistics
{
    public int TotalItems { get; set; }
    public double TotalSizeKB { get; set; }
    public Dictionary<GameResourceSource, int> SourceCounts { get; } = new();
}

/// <summary>
/// Minimal item data for caching.
/// Stores only what's needed for display and filtering, not the full UTI.
/// </summary>
public class CachedPaletteItem
{
    public string ResRef { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string BaseItemTypeName { get; set; } = string.Empty;
    public int BaseItemType { get; set; }
    public uint BaseValue { get; set; }
    public bool IsStandard { get; set; }
}
