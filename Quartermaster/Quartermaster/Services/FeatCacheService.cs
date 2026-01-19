using System.Collections.Generic;
using System.Threading.Tasks;
using Radoub.Formats.Logging;
using Radoub.UI.Services;

namespace Quartermaster.Services;

/// <summary>
/// Caches feat data to improve startup and panel load performance.
/// Uses file-based persistence via GameDataCacheService.
/// </summary>
public class FeatCacheService
{
    private const int CacheVersion = 1;
    private readonly GameDataCacheService<CachedFeatEntry> _cacheService;

    // In-memory caches for fast repeated lookups
    private Dictionary<int, CachedFeatEntry>? _featCache;
    private List<int>? _allFeatIds;
    private Dictionary<int, HashSet<int>>? _classGrantedFeats;
    private Dictionary<int, HashSet<int>>? _raceGrantedFeats;

    public FeatCacheService()
    {
        _cacheService = new GameDataCacheService<CachedFeatEntry>("Quartermaster", "feats", CacheVersion);
    }

    /// <summary>
    /// Check if a valid cache exists on disk.
    /// </summary>
    public bool HasValidCache() => _cacheService.HasValidCache();

    /// <summary>
    /// Load cached feat data from disk into memory.
    /// Returns true if cache was loaded, false if cache doesn't exist or is invalid.
    /// </summary>
    public bool LoadCacheFromDisk()
    {
        if (_featCache != null)
            return true; // Already loaded

        var cachedEntries = _cacheService.LoadCache();
        if (cachedEntries == null || cachedEntries.Count == 0)
            return false;

        // Build in-memory lookup dictionaries
        _featCache = new Dictionary<int, CachedFeatEntry>();
        _allFeatIds = new List<int>();

        foreach (var entry in cachedEntries)
        {
            _featCache[entry.FeatId] = entry;
            _allFeatIds.Add(entry.FeatId);
        }

        UnifiedLogger.LogApplication(LogLevel.INFO, $"Loaded {_featCache.Count} feats from cache");
        return true;
    }

    /// <summary>
    /// Save current in-memory cache to disk.
    /// </summary>
    public async Task SaveCacheToDiskAsync()
    {
        if (_featCache == null || _featCache.Count == 0)
            return;

        var entries = new List<CachedFeatEntry>(_featCache.Values);
        await _cacheService.SaveCacheAsync(entries);
    }

    /// <summary>
    /// Clear both in-memory and disk caches.
    /// </summary>
    public void ClearCache()
    {
        _featCache = null;
        _allFeatIds = null;
        _classGrantedFeats = null;
        _raceGrantedFeats = null;
        _cacheService.ClearCache();
    }

    /// <summary>
    /// Get cache info for display in settings.
    /// </summary>
    public CacheInfo? GetCacheInfo() => _cacheService.GetCacheInfo();

    /// <summary>
    /// Check if a feat is cached in memory.
    /// </summary>
    public bool TryGetFeat(int featId, out CachedFeatEntry? entry)
    {
        entry = null;
        if (_featCache == null)
            return false;
        return _featCache.TryGetValue(featId, out entry);
    }

    /// <summary>
    /// Add a feat entry to the in-memory cache.
    /// </summary>
    public void CacheFeat(CachedFeatEntry entry)
    {
        _featCache ??= new Dictionary<int, CachedFeatEntry>();
        _featCache[entry.FeatId] = entry;
    }

    /// <summary>
    /// Get all cached feat IDs.
    /// </summary>
    public List<int>? GetAllFeatIds() => _allFeatIds;

    /// <summary>
    /// Set the all feat IDs list (from initial scan).
    /// </summary>
    public void SetAllFeatIds(List<int> featIds)
    {
        _allFeatIds = featIds;
    }

    /// <summary>
    /// Check if class feat grants are cached.
    /// </summary>
    public bool TryGetClassGrantedFeats(int classId, out HashSet<int>? feats)
    {
        feats = null;
        if (_classGrantedFeats == null)
            return false;
        return _classGrantedFeats.TryGetValue(classId, out feats);
    }

    /// <summary>
    /// Cache class granted feats.
    /// </summary>
    public void CacheClassGrantedFeats(int classId, HashSet<int> feats)
    {
        _classGrantedFeats ??= new Dictionary<int, HashSet<int>>();
        _classGrantedFeats[classId] = feats;
    }

    /// <summary>
    /// Check if race feat grants are cached.
    /// </summary>
    public bool TryGetRaceGrantedFeats(int raceId, out HashSet<int>? feats)
    {
        feats = null;
        if (_raceGrantedFeats == null)
            return false;
        return _raceGrantedFeats.TryGetValue(raceId, out feats);
    }

    /// <summary>
    /// Cache race granted feats.
    /// </summary>
    public void CacheRaceGrantedFeats(int raceId, HashSet<int> feats)
    {
        _raceGrantedFeats ??= new Dictionary<int, HashSet<int>>();
        _raceGrantedFeats[raceId] = feats;
    }

    /// <summary>
    /// Check if the in-memory cache is populated.
    /// </summary>
    public bool IsMemoryCacheLoaded => _featCache != null && _featCache.Count > 0;
}

/// <summary>
/// Cached feat entry for persistent storage.
/// </summary>
public class CachedFeatEntry
{
    public int FeatId { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int Category { get; set; } // FeatCategory as int for JSON serialization
    public bool IsUniversal { get; set; }
}
