using System.Text.Json;
using Radoub.Formats.Logging;

namespace Radoub.Formats.Resolver;

/// <summary>
/// Persistent cache for KEY file index data.
/// Stores the parsed resource entries to avoid re-parsing KEY files on every startup.
/// Cache is invalidated when KEY file is modified or game paths change.
/// </summary>
public class KeyIndexCache
{
    private const int CacheVersion = 1;
    private static readonly string CacheDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Radoub", "Cache");
    private const string CacheFileName = "key_index_cache.json";

    /// <summary>
    /// Try to load cached KEY index data.
    /// Returns null if cache is missing, invalid, or stale.
    /// </summary>
    public static CachedKeyIndex? TryLoad(string keyFilePath)
    {
        try
        {
            var cachePath = Path.Combine(CacheDirectory, CacheFileName);
            if (!File.Exists(cachePath))
                return null;

            var json = File.ReadAllText(cachePath);
            var cache = JsonSerializer.Deserialize<KeyIndexCacheFile>(json);

            if (cache == null || cache.Version != CacheVersion)
            {
                UnifiedLogger.Log(LogLevel.DEBUG, "KEY cache version mismatch, rebuilding", "KeyIndexCache", "Resolver");
                return null;
            }

            // Validate against current KEY file
            if (!File.Exists(keyFilePath))
                return null;

            var keyFileInfo = new FileInfo(keyFilePath);
            if (cache.KeyFilePath != keyFilePath ||
                cache.KeyFileSize != keyFileInfo.Length ||
                cache.KeyFileModifiedUtc != keyFileInfo.LastWriteTimeUtc.Ticks)
            {
                UnifiedLogger.Log(LogLevel.DEBUG, "KEY file changed, cache invalidated", "KeyIndexCache", "Resolver");
                return null;
            }

            UnifiedLogger.Log(LogLevel.INFO, $"Loaded KEY index from cache ({cache.Resources.Count} resources, {cache.BifFiles.Count} BIFs)", "KeyIndexCache", "Resolver");

            return new CachedKeyIndex
            {
                BifFiles = cache.BifFiles,
                Resources = cache.Resources
            };
        }
        catch (Exception ex)
        {
            UnifiedLogger.Log(LogLevel.DEBUG, $"Failed to load KEY cache: {ex.Message}", "KeyIndexCache", "Resolver");
            return null;
        }
    }

    /// <summary>
    /// Save KEY index data to cache.
    /// </summary>
    public static void Save(string keyFilePath, CachedKeyIndex index)
    {
        try
        {
            Directory.CreateDirectory(CacheDirectory);

            var keyFileInfo = new FileInfo(keyFilePath);
            var cache = new KeyIndexCacheFile
            {
                Version = CacheVersion,
                KeyFilePath = keyFilePath,
                KeyFileSize = keyFileInfo.Length,
                KeyFileModifiedUtc = keyFileInfo.LastWriteTimeUtc.Ticks,
                BifFiles = index.BifFiles,
                Resources = index.Resources
            };

            var json = JsonSerializer.Serialize(cache, new JsonSerializerOptions
            {
                WriteIndented = false // Compact for faster loading
            });

            var cachePath = Path.Combine(CacheDirectory, CacheFileName);
            File.WriteAllText(cachePath, json);

            UnifiedLogger.Log(LogLevel.INFO, $"Saved KEY index to cache ({index.Resources.Count} resources)", "KeyIndexCache", "Resolver");
        }
        catch (Exception ex)
        {
            UnifiedLogger.Log(LogLevel.WARN, $"Failed to save KEY cache: {ex.Message}", "KeyIndexCache", "Resolver");
        }
    }

    /// <summary>
    /// Delete the cached KEY index.
    /// </summary>
    public static void Clear()
    {
        try
        {
            var cachePath = Path.Combine(CacheDirectory, CacheFileName);
            if (File.Exists(cachePath))
            {
                File.Delete(cachePath);
                UnifiedLogger.Log(LogLevel.INFO, "KEY index cache cleared", "KeyIndexCache", "Resolver");
            }
        }
        catch (Exception ex)
        {
            UnifiedLogger.Log(LogLevel.WARN, $"Failed to clear KEY cache: {ex.Message}", "KeyIndexCache", "Resolver");
        }
    }
}

/// <summary>
/// Internal cache file structure.
/// </summary>
internal class KeyIndexCacheFile
{
    public int Version { get; set; }
    public string KeyFilePath { get; set; } = string.Empty;
    public long KeyFileSize { get; set; }
    public long KeyFileModifiedUtc { get; set; }
    public List<CachedBifEntry> BifFiles { get; set; } = new();
    public List<CachedResourceEntry> Resources { get; set; } = new();
}

/// <summary>
/// Cached KEY index data ready for use.
/// </summary>
public class CachedKeyIndex
{
    public List<CachedBifEntry> BifFiles { get; set; } = new();
    public List<CachedResourceEntry> Resources { get; set; } = new();
}

/// <summary>
/// Cached BIF file entry.
/// </summary>
public class CachedBifEntry
{
    public string Filename { get; set; } = string.Empty;
    public uint FileSize { get; set; }
}

/// <summary>
/// Cached resource entry - minimal data needed for lookups.
/// </summary>
public class CachedResourceEntry
{
    public string ResRef { get; set; } = string.Empty;
    public ushort ResourceType { get; set; }
    public int BifIndex { get; set; }
    public int VariableTableIndex { get; set; }
}
