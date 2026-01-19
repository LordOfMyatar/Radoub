using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Radoub.Formats.Logging;
using Radoub.Formats.Settings;

namespace Radoub.UI.Services;

/// <summary>
/// Generic service for caching game data to improve startup performance.
/// Cache is automatically invalidated when game paths change or version mismatches.
///
/// Usage:
///   var cacheService = new GameDataCacheService&lt;FeatCacheEntry&gt;("Quartermaster", "feats", 1);
///   if (cacheService.HasValidCache()) { ... }
/// </summary>
/// <typeparam name="T">The type of items to cache</typeparam>
public class GameDataCacheService<T> where T : class
{
    private readonly string _cacheDirectory;
    private readonly string _cacheFilePath;
    private readonly string _cacheName;
    private readonly int _version;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false // Compact for faster read/write
    };

    /// <summary>
    /// Create a cache service for a specific tool and cache type.
    /// </summary>
    /// <param name="toolName">Tool name (e.g., "Quartermaster", "Fence")</param>
    /// <param name="cacheName">Cache identifier (e.g., "palette", "feats")</param>
    /// <param name="version">Cache version - bump to invalidate existing caches</param>
    public GameDataCacheService(string toolName, string cacheName, int version)
    {
        _cacheName = cacheName;
        _version = version;
        _cacheDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Radoub", toolName);
        _cacheFilePath = Path.Combine(_cacheDirectory, $"{cacheName}_cache.json");
    }

    /// <summary>
    /// Check if a valid cache exists for the current game paths.
    /// </summary>
    public bool HasValidCache()
    {
        if (!File.Exists(_cacheFilePath))
            return false;

        try
        {
            var json = File.ReadAllText(_cacheFilePath);
            var cache = JsonSerializer.Deserialize<CacheWrapper<T>>(json, JsonOptions);

            if (cache == null)
                return false;

            // Validate game paths match current settings
            var settings = RadoubSettings.Instance;
            if (cache.BaseGameInstallPath != settings.BaseGameInstallPath ||
                cache.NeverwinterNightsPath != settings.NeverwinterNightsPath)
            {
                UnifiedLogger.LogApplication(LogLevel.INFO, $"{_cacheName} cache invalidated - game paths changed");
                return false;
            }

            // Check cache version
            if (cache.Version != _version)
            {
                UnifiedLogger.LogApplication(LogLevel.INFO, $"{_cacheName} cache invalidated - version mismatch");
                return false;
            }

            UnifiedLogger.LogApplication(LogLevel.INFO, $"Valid {_cacheName} cache found: {cache.Items.Count} items");
            return true;
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, $"Failed to validate {_cacheName} cache: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Load cached items.
    /// </summary>
    public List<T>? LoadCache()
    {
        try
        {
            var json = File.ReadAllText(_cacheFilePath);
            var cache = JsonSerializer.Deserialize<CacheWrapper<T>>(json, JsonOptions);

            if (cache?.Items != null)
            {
                UnifiedLogger.LogApplication(LogLevel.INFO, $"Loaded {cache.Items.Count} items from {_cacheName} cache");
                return cache.Items;
            }
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to load {_cacheName} cache: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// Save items to cache.
    /// </summary>
    public async Task SaveCacheAsync(List<T> items)
    {
        try
        {
            // Ensure directory exists
            if (!Directory.Exists(_cacheDirectory))
            {
                Directory.CreateDirectory(_cacheDirectory);
            }

            var settings = RadoubSettings.Instance;
            var cache = new CacheWrapper<T>
            {
                Version = _version,
                CreatedAt = DateTime.UtcNow,
                BaseGameInstallPath = settings.BaseGameInstallPath,
                NeverwinterNightsPath = settings.NeverwinterNightsPath,
                Items = items
            };

            var json = JsonSerializer.Serialize(cache, JsonOptions);
            await File.WriteAllTextAsync(_cacheFilePath, json);

            UnifiedLogger.LogApplication(LogLevel.INFO, $"Saved {items.Count} items to {_cacheName} cache");
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to save {_cacheName} cache: {ex.Message}");
        }
    }

    /// <summary>
    /// Delete the cache file.
    /// </summary>
    public void ClearCache()
    {
        try
        {
            if (File.Exists(_cacheFilePath))
            {
                File.Delete(_cacheFilePath);
                UnifiedLogger.LogApplication(LogLevel.INFO, $"{_cacheName} cache cleared");
            }
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to clear {_cacheName} cache: {ex.Message}");
        }
    }

    /// <summary>
    /// Get cache file info for display.
    /// </summary>
    public CacheInfo? GetCacheInfo()
    {
        if (!File.Exists(_cacheFilePath))
            return null;

        try
        {
            var fileInfo = new FileInfo(_cacheFilePath);
            var json = File.ReadAllText(_cacheFilePath);
            var cache = JsonSerializer.Deserialize<CacheWrapper<T>>(json, JsonOptions);

            if (cache != null)
            {
                return new CacheInfo
                {
                    CacheName = _cacheName,
                    ItemCount = cache.Items.Count,
                    FileSizeKB = fileInfo.Length / 1024.0,
                    CreatedAt = cache.CreatedAt
                };
            }
        }
        catch
        {
            // Ignore errors reading cache info
        }

        return null;
    }

    /// <summary>
    /// Get the cache file path (for debugging/display).
    /// </summary>
    public string CacheFilePath => _cacheFilePath;
}

/// <summary>
/// Generic cache wrapper that stores metadata alongside items.
/// </summary>
/// <typeparam name="T">The type of items being cached</typeparam>
public class CacheWrapper<T>
{
    public int Version { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? BaseGameInstallPath { get; set; }
    public string? NeverwinterNightsPath { get; set; }
    public List<T> Items { get; set; } = new();
}

/// <summary>
/// Cache metadata for display in settings UI.
/// </summary>
public class CacheInfo
{
    public string CacheName { get; set; } = string.Empty;
    public int ItemCount { get; set; }
    public double FileSizeKB { get; set; }
    public DateTime CreatedAt { get; set; }
}
