using System.Text;
using System.Text.Json;
using Radoub.Formats.Logging;

namespace Radoub.Formats.Resolver;

/// <summary>
/// Persistent cache for KEY file index data.
/// Stores the parsed resource entries to avoid re-parsing KEY files on every startup.
/// Cache is invalidated when KEY file is modified or game paths change.
///
/// Uses binary format for fast loading (~50ms vs ~500ms for JSON with 113K entries).
/// </summary>
public class KeyIndexCache
{
    private const int BinaryCacheVersion = 2; // Bumped for binary format
    private const int JsonCacheVersion = 1;   // Old JSON format version
    private static readonly string CacheDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Radoub", "Cache");
    private const string BinaryCacheFileName = "key_index_cache.bin";
    private const string JsonCacheFileName = "key_index_cache.json"; // For cleanup

    /// <summary>
    /// Try to load cached KEY index data.
    /// Returns null if cache is missing, invalid, or stale.
    /// </summary>
    public static CachedKeyIndex? TryLoad(string keyFilePath)
    {
        // Try binary format first (faster)
        var binaryResult = TryLoadBinary(keyFilePath);
        if (binaryResult != null)
            return binaryResult;

        // Fall back to JSON format (for migration)
        var jsonResult = TryLoadJson(keyFilePath);
        if (jsonResult != null)
        {
            // Migrate to binary format
            Save(keyFilePath, jsonResult);
            // Clean up old JSON file
            CleanupJsonCache();
        }
        return jsonResult;
    }

    private static CachedKeyIndex? TryLoadBinary(string keyFilePath)
    {
        try
        {
            var cachePath = Path.Combine(CacheDirectory, BinaryCacheFileName);
            if (!File.Exists(cachePath))
                return null;

            var sw = System.Diagnostics.Stopwatch.StartNew();

            using var stream = new FileStream(cachePath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536);
            using var reader = new BinaryReader(stream, Encoding.UTF8);

            // Read header
            var version = reader.ReadInt32();
            if (version != BinaryCacheVersion)
            {
                UnifiedLogger.Log(LogLevel.DEBUG, $"Binary cache version mismatch ({version} vs {BinaryCacheVersion}), rebuilding", "KeyIndexCache", "Resolver");
                return null;
            }

            var storedKeyPath = reader.ReadString();
            var storedKeySize = reader.ReadInt64();
            var storedKeyModified = reader.ReadInt64();

            // Validate against current KEY file
            if (!File.Exists(keyFilePath))
                return null;

            var keyFileInfo = new FileInfo(keyFilePath);
            if (storedKeyPath != keyFilePath ||
                storedKeySize != keyFileInfo.Length ||
                storedKeyModified != keyFileInfo.LastWriteTimeUtc.Ticks)
            {
                UnifiedLogger.Log(LogLevel.DEBUG, "KEY file changed, cache invalidated", "KeyIndexCache", "Resolver");
                return null;
            }

            // Read BIF entries
            var bifCount = reader.ReadInt32();
            var bifFiles = new List<CachedBifEntry>(bifCount);
            for (int i = 0; i < bifCount; i++)
            {
                bifFiles.Add(new CachedBifEntry
                {
                    Filename = reader.ReadString(),
                    FileSize = reader.ReadUInt32()
                });
            }

            // Read resource entries
            var resourceCount = reader.ReadInt32();
            var resources = new List<CachedResourceEntry>(resourceCount);
            for (int i = 0; i < resourceCount; i++)
            {
                resources.Add(new CachedResourceEntry
                {
                    ResRef = reader.ReadString(),
                    ResourceType = reader.ReadUInt16(),
                    BifIndex = reader.ReadInt32(),
                    VariableTableIndex = reader.ReadInt32()
                });
            }

            UnifiedLogger.Log(LogLevel.INFO, $"Loaded KEY index from binary cache ({resourceCount} resources, {bifCount} BIFs) in {sw.ElapsedMilliseconds}ms", "KeyIndexCache", "Resolver");

            return new CachedKeyIndex
            {
                BifFiles = bifFiles,
                Resources = resources
            };
        }
        catch (Exception ex)
        {
            UnifiedLogger.Log(LogLevel.DEBUG, $"Failed to load binary KEY cache: {ex.Message}", "KeyIndexCache", "Resolver");
            return null;
        }
    }

    private static CachedKeyIndex? TryLoadJson(string keyFilePath)
    {
        try
        {
            var cachePath = Path.Combine(CacheDirectory, JsonCacheFileName);
            if (!File.Exists(cachePath))
                return null;

            var json = File.ReadAllText(cachePath);
            var cache = JsonSerializer.Deserialize<KeyIndexCacheFile>(json);

            if (cache == null || cache.Version != JsonCacheVersion)
            {
                UnifiedLogger.Log(LogLevel.DEBUG, "JSON cache version mismatch, rebuilding", "KeyIndexCache", "Resolver");
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
                UnifiedLogger.Log(LogLevel.DEBUG, "KEY file changed, JSON cache invalidated", "KeyIndexCache", "Resolver");
                return null;
            }

            UnifiedLogger.Log(LogLevel.INFO, $"Loaded KEY index from JSON cache ({cache.Resources.Count} resources, {cache.BifFiles.Count} BIFs) - migrating to binary", "KeyIndexCache", "Resolver");

            return new CachedKeyIndex
            {
                BifFiles = cache.BifFiles,
                Resources = cache.Resources
            };
        }
        catch (Exception ex)
        {
            UnifiedLogger.Log(LogLevel.DEBUG, $"Failed to load JSON KEY cache: {ex.Message}", "KeyIndexCache", "Resolver");
            return null;
        }
    }

    /// <summary>
    /// Save KEY index data to binary cache.
    /// </summary>
    public static void Save(string keyFilePath, CachedKeyIndex index)
    {
        try
        {
            Directory.CreateDirectory(CacheDirectory);

            var keyFileInfo = new FileInfo(keyFilePath);
            var cachePath = Path.Combine(CacheDirectory, BinaryCacheFileName);

            using var stream = new FileStream(cachePath, FileMode.Create, FileAccess.Write, FileShare.None, 65536);
            using var writer = new BinaryWriter(stream, Encoding.UTF8);

            // Write header
            writer.Write(BinaryCacheVersion);
            writer.Write(keyFilePath);
            writer.Write(keyFileInfo.Length);
            writer.Write(keyFileInfo.LastWriteTimeUtc.Ticks);

            // Write BIF entries
            writer.Write(index.BifFiles.Count);
            foreach (var bif in index.BifFiles)
            {
                writer.Write(bif.Filename);
                writer.Write(bif.FileSize);
            }

            // Write resource entries
            writer.Write(index.Resources.Count);
            foreach (var resource in index.Resources)
            {
                writer.Write(resource.ResRef);
                writer.Write(resource.ResourceType);
                writer.Write(resource.BifIndex);
                writer.Write(resource.VariableTableIndex);
            }

            UnifiedLogger.Log(LogLevel.INFO, $"Saved KEY index to binary cache ({index.Resources.Count} resources)", "KeyIndexCache", "Resolver");
        }
        catch (Exception ex)
        {
            UnifiedLogger.Log(LogLevel.WARN, $"Failed to save binary KEY cache: {ex.Message}", "KeyIndexCache", "Resolver");
        }
    }

    /// <summary>
    /// Delete the cached KEY index (both binary and JSON).
    /// </summary>
    public static void Clear()
    {
        try
        {
            var binaryCachePath = Path.Combine(CacheDirectory, BinaryCacheFileName);
            if (File.Exists(binaryCachePath))
            {
                File.Delete(binaryCachePath);
                UnifiedLogger.Log(LogLevel.INFO, "Binary KEY index cache cleared", "KeyIndexCache", "Resolver");
            }

            CleanupJsonCache();
        }
        catch (Exception ex)
        {
            UnifiedLogger.Log(LogLevel.WARN, $"Failed to clear KEY cache: {ex.Message}", "KeyIndexCache", "Resolver");
        }
    }

    private static void CleanupJsonCache()
    {
        try
        {
            var jsonCachePath = Path.Combine(CacheDirectory, JsonCacheFileName);
            if (File.Exists(jsonCachePath))
            {
                File.Delete(jsonCachePath);
                UnifiedLogger.Log(LogLevel.DEBUG, "Cleaned up old JSON KEY cache", "KeyIndexCache", "Resolver");
            }
        }
        catch
        {
            // Ignore cleanup failures
        }
    }
}

/// <summary>
/// Internal JSON cache file structure (for migration only).
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
