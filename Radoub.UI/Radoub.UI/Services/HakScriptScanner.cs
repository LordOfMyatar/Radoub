using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Radoub.Formats.Common;
using Radoub.Formats.Erf;
using Radoub.Formats.Logging;

namespace Radoub.UI.Services;

/// <summary>
/// Cached HAK file script data to avoid re-scanning on each browser open.
/// </summary>
internal class ScriptHakCacheEntry
{
    public string HakPath { get; set; } = "";
    public DateTime LastModified { get; set; }
    public List<ScriptEntry> Scripts { get; set; } = new();
}

/// <summary>
/// Service for scanning HAK files for NWScript source files (.nss).
/// Uses caching to avoid re-scanning unchanged HAK files.
/// </summary>
public class HakScriptScanner
{
    // Static cache for HAK file contents - persists across scanner instances
    private static readonly Dictionary<string, ScriptHakCacheEntry> _hakCache = new();

    /// <summary>
    /// Scans multiple HAK files for scripts, respecting module override priority.
    /// </summary>
    /// <param name="hakPaths">List of HAK file paths to scan</param>
    /// <param name="moduleScripts">Module scripts (highest priority - these override HAK scripts)</param>
    /// <param name="progressCallback">Optional callback for progress updates (hakIndex, totalHaks, hakName)</param>
    /// <returns>List of scripts found in HAK files (excluding duplicates)</returns>
    public async Task<List<ScriptEntry>> ScanHaksForScriptsAsync(
        IEnumerable<string> hakPaths,
        IReadOnlyList<ScriptEntry> moduleScripts,
        Action<int, int, string>? progressCallback = null)
    {
        var hakScripts = new List<ScriptEntry>();
        var pathList = hakPaths.ToList();

        if (pathList.Count == 0)
        {
            UnifiedLogger.LogApplication(LogLevel.INFO, "HakScriptScanner: No HAK files to scan");
            return hakScripts;
        }

        UnifiedLogger.LogApplication(LogLevel.INFO, $"HakScriptScanner: Scanning {pathList.Count} HAK files for scripts");

        for (int i = 0; i < pathList.Count; i++)
        {
            var hakPath = pathList[i];
            var hakName = Path.GetFileName(hakPath);
            progressCallback?.Invoke(i + 1, pathList.Count, hakName);

            await Task.Run(() => ScanHakForScripts(hakPath, moduleScripts, hakScripts));
        }

        hakScripts = hakScripts.OrderBy(s => s.Name).ToList();
        UnifiedLogger.LogApplication(LogLevel.INFO, $"HakScriptScanner: Found {hakScripts.Count} scripts in HAK files");

        return hakScripts;
    }

    /// <summary>
    /// Scans a single HAK file for scripts, using cache when available.
    /// </summary>
    private void ScanHakForScripts(
        string hakPath,
        IReadOnlyList<ScriptEntry> moduleScripts,
        List<ScriptEntry> hakScripts)
    {
        try
        {
            var hakFileName = Path.GetFileName(hakPath);
            var lastModified = File.GetLastWriteTimeUtc(hakPath);

            // Check cache first
            if (_hakCache.TryGetValue(hakPath, out var cached) && cached.LastModified == lastModified)
            {
                // Use cached scripts - filter for duplicates
                foreach (var script in cached.Scripts)
                {
                    if (!IsDuplicate(script.Name, moduleScripts, hakScripts))
                    {
                        hakScripts.Add(CloneScriptEntry(script));
                    }
                }
                UnifiedLogger.LogApplication(LogLevel.DEBUG,
                    $"HakScriptScanner: Used cached {cached.Scripts.Count} scripts from {hakFileName}");
                return;
            }

            // Not cached or outdated - scan HAK
            // Use ReadMetadataOnly to avoid loading entire file into memory (large HAKs can be 800MB+)
            var erf = ErfReader.ReadMetadataOnly(hakPath);
            var nssResources = erf.GetResourcesByType(ResourceTypes.Nss).ToList();
            var newCacheEntry = new ScriptHakCacheEntry
            {
                HakPath = hakPath,
                LastModified = lastModified,
                Scripts = new List<ScriptEntry>()
            };

            foreach (var resource in nssResources)
            {
                var scriptEntry = new ScriptEntry
                {
                    Name = resource.ResRef,
                    IsBuiltIn = false,
                    Source = $"HAK: {hakFileName}",
                    HakPath = hakPath,
                    ErfEntry = resource
                };

                // Add to cache (all scripts, not filtered)
                newCacheEntry.Scripts.Add(scriptEntry);

                // Add to results if not a duplicate
                if (!IsDuplicate(scriptEntry.Name, moduleScripts, hakScripts))
                {
                    hakScripts.Add(scriptEntry);
                }
            }

            // Update cache
            _hakCache[hakPath] = newCacheEntry;

            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"HakScriptScanner: Scanned and cached {nssResources.Count} scripts in {hakFileName}");
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR,
                $"HakScriptScanner: Error scanning HAK {UnifiedLogger.SanitizePath(hakPath)}: {ex.Message}");
        }
    }

    /// <summary>
    /// Checks if a script name already exists in module scripts or HAK scripts.
    /// </summary>
    private static bool IsDuplicate(
        string scriptName,
        IReadOnlyList<ScriptEntry> moduleScripts,
        List<ScriptEntry> hakScripts)
    {
        // Module overrides HAK
        if (moduleScripts.Any(s => s.Name.Equals(scriptName, StringComparison.OrdinalIgnoreCase)))
            return true;
        // Already found in another HAK
        if (hakScripts.Any(s => s.Name.Equals(scriptName, StringComparison.OrdinalIgnoreCase)))
            return true;
        return false;
    }

    /// <summary>
    /// Creates a copy of a script entry for use outside the cache.
    /// </summary>
    private static ScriptEntry CloneScriptEntry(ScriptEntry source)
    {
        return new ScriptEntry
        {
            Name = source.Name,
            IsBuiltIn = source.IsBuiltIn,
            Source = source.Source,
            HakPath = source.HakPath,
            ErfEntry = source.ErfEntry,
            FilePath = source.FilePath
        };
    }

    /// <summary>
    /// Extracts script content from a HAK file.
    /// </summary>
    /// <param name="scriptEntry">Script entry with HAK path and ERF entry</param>
    /// <returns>Script content with source header, or null if extraction fails</returns>
    public async Task<string?> ExtractScriptFromHakAsync(ScriptEntry scriptEntry)
    {
        if (!scriptEntry.IsFromHak || scriptEntry.HakPath == null || scriptEntry.ErfEntry == null)
            return null;

        try
        {
            // Extract script data from HAK on background thread
            var scriptData = await Task.Run(() =>
                ErfReader.ExtractResource(scriptEntry.HakPath, scriptEntry.ErfEntry));

            if (scriptData == null || scriptData.Length == 0)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN,
                    $"HakScriptScanner: Failed to extract script '{scriptEntry.Name}' from HAK: empty data");
                return null;
            }

            // NWScript source files are plain text
            var content = System.Text.Encoding.UTF8.GetString(scriptData);

            // Add source header for context
            var header = $"// Script from HAK: {Path.GetFileName(scriptEntry.HakPath)}\n" +
                        $"// ResRef: {scriptEntry.Name}\n" +
                        "//\n";

            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                $"HakScriptScanner: Extracted script '{scriptEntry.Name}' from HAK ({scriptData.Length} bytes)");

            return header + content;
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR,
                $"HakScriptScanner: Error extracting script from HAK: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Gets HAK file paths from a directory.
    /// </summary>
    public static IEnumerable<string> GetHakFilesFromPath(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                return Directory.GetFiles(path, "*.hak", SearchOption.TopDirectoryOnly);
            }
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN,
                $"HakScriptScanner: Error scanning for HAKs in {UnifiedLogger.SanitizePath(path)}: {ex.Message}");
        }
        return Enumerable.Empty<string>();
    }

    /// <summary>
    /// Collects HAK paths from multiple locations with deduplication.
    /// </summary>
    /// <param name="currentDir">Current file directory (highest priority)</param>
    /// <param name="overridePath">Override path if set</param>
    /// <param name="userPath">NWN user documents path (for hak folder)</param>
    /// <returns>Deduplicated list of HAK file paths</returns>
    public static List<string> CollectHakPaths(string? currentDir, string? overridePath, string? userPath)
    {
        var hakPaths = new List<string>();

        // 1. Current file directory (highest priority for module HAKs)
        if (!string.IsNullOrEmpty(currentDir))
        {
            hakPaths.AddRange(GetHakFilesFromPath(currentDir));
        }

        // 2. Override path if set
        if (!string.IsNullOrEmpty(overridePath))
        {
            hakPaths.AddRange(GetHakFilesFromPath(overridePath));
        }

        // 3. NWN user hak folder
        if (!string.IsNullOrEmpty(userPath) && Directory.Exists(userPath))
        {
            var hakFolder = Path.Combine(userPath, "hak");
            if (Directory.Exists(hakFolder))
            {
                hakPaths.AddRange(GetHakFilesFromPath(hakFolder));
            }
        }

        // Deduplicate HAK paths (same file might be found in multiple locations)
        return hakPaths.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// Clears the HAK cache. Call when game paths change.
    /// </summary>
    public static void ClearCache()
    {
        _hakCache.Clear();
        UnifiedLogger.LogApplication(LogLevel.INFO, "HakScriptScanner: Cache cleared");
    }
}
