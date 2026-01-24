using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Radoub.Formats.Logging;

namespace RadoubLauncher.Services;

/// <summary>
/// Service for reading recent files from each tool's settings.
/// Reads from ~/Radoub/{ToolName}/{ToolName}Settings.json
/// </summary>
public class ToolRecentFilesService
{
    private static ToolRecentFilesService? _instance;
    private static readonly object _lock = new();

    public static ToolRecentFilesService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new ToolRecentFilesService();
                }
            }
            return _instance;
        }
    }

    private readonly string _radoubSettingsDir;
    private readonly Dictionary<string, List<RecentFileInfo>> _cache = new();
    private readonly Dictionary<string, DateTime> _cacheTimestamps = new();

    private ToolRecentFilesService()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _radoubSettingsDir = Path.Combine(userProfile, "Radoub");
    }

    /// <summary>
    /// Get recent files for a tool, validating that files still exist.
    /// </summary>
    /// <param name="toolName">Tool name (Parley, Quartermaster, etc.)</param>
    /// <param name="maxFiles">Maximum number of files to return</param>
    /// <returns>List of recent files with display names</returns>
    public List<RecentFileInfo> GetRecentFiles(string toolName, int maxFiles = 10)
    {
        var settingsPath = GetSettingsPath(toolName);
        if (string.IsNullOrEmpty(settingsPath) || !File.Exists(settingsPath))
        {
            return new List<RecentFileInfo>();
        }

        // Check cache freshness (1 minute cache)
        if (_cache.TryGetValue(toolName, out var cached) &&
            _cacheTimestamps.TryGetValue(toolName, out var timestamp) &&
            DateTime.UtcNow - timestamp < TimeSpan.FromMinutes(1))
        {
            return cached.Take(maxFiles).ToList();
        }

        try
        {
            var json = File.ReadAllText(settingsPath);
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var recentFiles = new List<RecentFileInfo>();

            if (root.TryGetProperty("RecentFiles", out var filesElement) &&
                filesElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var fileElement in filesElement.EnumerateArray())
                {
                    var filePath = fileElement.GetString();
                    if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
                    {
                        recentFiles.Add(new RecentFileInfo
                        {
                            FullPath = filePath,
                            DisplayName = Path.GetFileName(filePath)
                        });
                    }
                }
            }

            // Update cache
            _cache[toolName] = recentFiles;
            _cacheTimestamps[toolName] = DateTime.UtcNow;

            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                $"Loaded {recentFiles.Count} recent files for {toolName}");

            return recentFiles.Take(maxFiles).ToList();
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN,
                $"Failed to read recent files for {toolName}: {ex.Message}");
            return new List<RecentFileInfo>();
        }
    }

    /// <summary>
    /// Check if a tool has any recent files.
    /// </summary>
    public bool HasRecentFiles(string toolName)
    {
        return GetRecentFiles(toolName, 1).Count > 0;
    }

    /// <summary>
    /// Clear the cache for a specific tool or all tools.
    /// </summary>
    public void ClearCache(string? toolName = null)
    {
        if (toolName != null)
        {
            _cache.Remove(toolName);
            _cacheTimestamps.Remove(toolName);
        }
        else
        {
            _cache.Clear();
            _cacheTimestamps.Clear();
        }
    }

    private string? GetSettingsPath(string toolName)
    {
        // Map tool names to their settings file paths
        var settingsFileName = toolName switch
        {
            "Parley" => "ParleySettings.json",
            "Quartermaster" => "QuartermasterSettings.json",
            "Manifest" => "ManifestSettings.json",
            "Fence" => "FenceSettings.json",
            _ => null
        };

        if (settingsFileName == null)
            return null;

        return Path.Combine(_radoubSettingsDir, toolName, settingsFileName);
    }
}

/// <summary>
/// Information about a recent file for display.
/// </summary>
public class RecentFileInfo
{
    /// <summary>
    /// Full path to the file.
    /// </summary>
    public required string FullPath { get; init; }

    /// <summary>
    /// Display name (filename only, no path).
    /// </summary>
    public required string DisplayName { get; init; }
}
