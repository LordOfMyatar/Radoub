using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Radoub.Formats.Logging;

namespace Radoub.UI.Services;

/// <summary>
/// Caches discovered theme catalog to avoid rescanning theme directories on every startup.
/// Each tool gets its own cache file since each tool scans different directories.
/// </summary>
public class ThemeCatalogCache
{
    private readonly string _cachePath;

    /// <summary>
    /// Creates a cache instance for the specified tool.
    /// Cache file stored at ~/Radoub/{toolName}/ThemeCatalog.json
    /// </summary>
    public ThemeCatalogCache(string cachePath)
    {
        _cachePath = cachePath;
    }

    /// <summary>
    /// Creates a cache for the given tool name using the standard path.
    /// </summary>
    public static ThemeCatalogCache ForTool(string toolName)
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var cachePath = Path.Combine(userProfile, "Radoub", toolName, "ThemeCatalog.json");
        return new ThemeCatalogCache(cachePath);
    }

    /// <summary>
    /// Check if the cache is valid for the given directories.
    /// Returns true if all directory timestamps match the cached values.
    /// </summary>
    public bool IsValid(IReadOnlyList<string> directories)
    {
        if (!File.Exists(_cachePath))
            return false;

        try
        {
            var cached = Load();
            if (cached == null || cached.CacheVersion != 1)
                return false;

            return TimestampsMatch(cached.DirectoryTimestamps, directories);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Compare directory timestamps against cached values.
    /// Returns true only if all directories have matching timestamps.
    /// </summary>
    public static bool TimestampsMatch(
        Dictionary<string, string?> cachedTimestamps,
        IReadOnlyList<string> directories)
    {
        if (cachedTimestamps.Count != directories.Count)
            return false;

        foreach (var dir in directories)
        {
            if (!cachedTimestamps.TryGetValue(dir, out var cachedTimestamp))
                return false;

            var currentTimestamp = GetDirectoryTimestamp(dir);
            if (currentTimestamp != cachedTimestamp)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Get the last write timestamp for a directory, or null if it doesn't exist.
    /// </summary>
    public static string? GetDirectoryTimestamp(string directory)
    {
        if (!Directory.Exists(directory))
            return null;

        return Directory.GetLastWriteTimeUtc(directory).ToString("O");
    }

    /// <summary>
    /// Build the current timestamps dictionary for the given directories.
    /// </summary>
    public static Dictionary<string, string?> BuildTimestamps(IReadOnlyList<string> directories)
    {
        var timestamps = new Dictionary<string, string?>();
        foreach (var dir in directories)
        {
            timestamps[dir] = GetDirectoryTimestamp(dir);
        }
        return timestamps;
    }

    /// <summary>
    /// Load cached catalog from disk. Returns null if file doesn't exist or is corrupt.
    /// </summary>
    public ThemeCatalogData? Load()
    {
        if (!File.Exists(_cachePath))
            return null;

        try
        {
            var json = File.ReadAllText(_cachePath);
            return JsonSerializer.Deserialize<ThemeCatalogData>(json, CacheJsonOptions);
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                $"Theme cache load failed (will rescan): {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Save catalog data to cache file.
    /// </summary>
    public void Save(ThemeCatalogData data)
    {
        try
        {
            var directory = Path.GetDirectoryName(_cachePath);
            if (directory != null && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            var json = JsonSerializer.Serialize(data, CacheJsonOptions);
            File.WriteAllText(_cachePath, json);
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN,
                $"Failed to write theme cache (non-fatal): {ex.Message}");
        }
    }

    private static readonly JsonSerializerOptions CacheJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };
}

/// <summary>
/// Serializable cache data for discovered themes.
/// </summary>
public class ThemeCatalogData
{
    [JsonPropertyName("cacheVersion")]
    public int CacheVersion { get; set; } = 1;

    [JsonPropertyName("directoryTimestamps")]
    public Dictionary<string, string?> DirectoryTimestamps { get; set; } = new();

    [JsonPropertyName("themeFiles")]
    public Dictionary<string, string> ThemeFiles { get; set; } = new();
}
