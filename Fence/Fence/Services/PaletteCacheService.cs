using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Radoub.Formats.Logging;
using Radoub.Formats.Settings;

namespace MerchantEditor.Services;

/// <summary>
/// Service for caching item palette data to improve startup performance.
/// Cache is invalidated when game paths change.
/// </summary>
public class PaletteCacheService
{
    private static readonly string CacheDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Radoub", "Fence");

    private static readonly string CacheFilePath = Path.Combine(CacheDirectory, "palette_cache.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false // Compact for faster read/write
    };

    /// <summary>
    /// Check if a valid cache exists for the current game paths.
    /// </summary>
    public bool HasValidCache()
    {
        if (!File.Exists(CacheFilePath))
            return false;

        try
        {
            var json = File.ReadAllText(CacheFilePath);
            var cache = JsonSerializer.Deserialize<PaletteCache>(json, JsonOptions);

            if (cache == null)
                return false;

            // Validate game paths match current settings
            var settings = RadoubSettings.Instance;
            if (cache.BaseGameInstallPath != settings.BaseGameInstallPath ||
                cache.NeverwinterNightsPath != settings.NeverwinterNightsPath)
            {
                UnifiedLogger.LogApplication(LogLevel.INFO, "Palette cache invalidated - game paths changed");
                return false;
            }

            // Check cache version
            if (cache.Version != PaletteCache.CurrentVersion)
            {
                UnifiedLogger.LogApplication(LogLevel.INFO, "Palette cache invalidated - version mismatch");
                return false;
            }

            UnifiedLogger.LogApplication(LogLevel.INFO, $"Valid palette cache found: {cache.Items.Count} items");
            return true;
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, $"Failed to validate palette cache: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Load cached palette items.
    /// </summary>
    public List<CachedPaletteItem>? LoadCache()
    {
        try
        {
            var json = File.ReadAllText(CacheFilePath);
            var cache = JsonSerializer.Deserialize<PaletteCache>(json, JsonOptions);

            if (cache?.Items != null)
            {
                UnifiedLogger.LogApplication(LogLevel.INFO, $"Loaded {cache.Items.Count} items from palette cache");
                return cache.Items;
            }
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to load palette cache: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// Save palette items to cache.
    /// </summary>
    public async Task SaveCacheAsync(List<CachedPaletteItem> items)
    {
        try
        {
            // Ensure directory exists
            if (!Directory.Exists(CacheDirectory))
            {
                Directory.CreateDirectory(CacheDirectory);
            }

            var settings = RadoubSettings.Instance;
            var cache = new PaletteCache
            {
                Version = PaletteCache.CurrentVersion,
                CreatedAt = DateTime.UtcNow,
                BaseGameInstallPath = settings.BaseGameInstallPath,
                NeverwinterNightsPath = settings.NeverwinterNightsPath,
                Items = items
            };

            var json = JsonSerializer.Serialize(cache, JsonOptions);
            await File.WriteAllTextAsync(CacheFilePath, json);

            UnifiedLogger.LogApplication(LogLevel.INFO, $"Saved {items.Count} items to palette cache");
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to save palette cache: {ex.Message}");
        }
    }

    /// <summary>
    /// Delete the cache file.
    /// </summary>
    public void ClearCache()
    {
        try
        {
            if (File.Exists(CacheFilePath))
            {
                File.Delete(CacheFilePath);
                UnifiedLogger.LogApplication(LogLevel.INFO, "Palette cache cleared");
            }
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to clear palette cache: {ex.Message}");
        }
    }

    /// <summary>
    /// Get cache file info for display.
    /// </summary>
    public CacheInfo? GetCacheInfo()
    {
        if (!File.Exists(CacheFilePath))
            return null;

        try
        {
            var fileInfo = new FileInfo(CacheFilePath);
            var json = File.ReadAllText(CacheFilePath);
            var cache = JsonSerializer.Deserialize<PaletteCache>(json, JsonOptions);

            if (cache != null)
            {
                return new CacheInfo
                {
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
}

/// <summary>
/// Cached palette data structure.
/// </summary>
public class PaletteCache
{
    public const int CurrentVersion = 1;

    public int Version { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? BaseGameInstallPath { get; set; }
    public string? NeverwinterNightsPath { get; set; }
    public List<CachedPaletteItem> Items { get; set; } = new();
}

/// <summary>
/// Minimal item data for caching.
/// </summary>
public class CachedPaletteItem
{
    public string ResRef { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string BaseItemType { get; set; } = string.Empty;
    public int BaseValue { get; set; }
    public bool IsStandard { get; set; }
}

/// <summary>
/// Cache info for display in settings.
/// </summary>
public class CacheInfo
{
    public int ItemCount { get; set; }
    public double FileSizeKB { get; set; }
    public DateTime CreatedAt { get; set; }
}
