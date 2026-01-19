using System.Collections.Generic;
using System.Threading.Tasks;
using Radoub.UI.Services;

namespace MerchantEditor.Services;

/// <summary>
/// Service for caching item palette data to improve startup performance.
/// Wraps the shared GameDataCacheService with Fence-specific configuration.
/// </summary>
public class PaletteCacheService
{
    private const int CacheVersion = 1;
    private readonly GameDataCacheService<CachedPaletteItem> _cacheService;

    public PaletteCacheService()
    {
        _cacheService = new GameDataCacheService<CachedPaletteItem>("Fence", "palette", CacheVersion);
    }

    /// <summary>
    /// Check if a valid cache exists for the current game paths.
    /// </summary>
    public bool HasValidCache() => _cacheService.HasValidCache();

    /// <summary>
    /// Load cached palette items.
    /// </summary>
    public List<CachedPaletteItem>? LoadCache() => _cacheService.LoadCache();

    /// <summary>
    /// Save palette items to cache.
    /// </summary>
    public Task SaveCacheAsync(List<CachedPaletteItem> items) => _cacheService.SaveCacheAsync(items);

    /// <summary>
    /// Delete the cache file.
    /// </summary>
    public void ClearCache() => _cacheService.ClearCache();

    /// <summary>
    /// Get cache file info for display.
    /// </summary>
    public CacheInfo? GetCacheInfo() => _cacheService.GetCacheInfo();
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
