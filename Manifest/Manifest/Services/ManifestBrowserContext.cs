using System.Collections.Generic;
using System.IO;
using Radoub.Formats.Common;
using Radoub.Formats.Settings;
using Radoub.UI.Services;

namespace Manifest.Services;

/// <summary>
/// Manifest's implementation of IScriptBrowserContext.
/// Provides journal-specific paths for the JournalBrowserWindow.
/// </summary>
public class ManifestBrowserContext : IScriptBrowserContext
{
    private readonly string? _journalFilePath;

    public ManifestBrowserContext(string? journalFilePath)
    {
        _journalFilePath = journalFilePath;
    }

    public string? CurrentFileDirectory
    {
        get
        {
            // First try the open file's directory
            if (!string.IsNullOrEmpty(_journalFilePath))
            {
                var dir = Path.GetDirectoryName(_journalFilePath);
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                    return dir;
            }

            // Fall back to RadoubSettings.CurrentModulePath (set by Trebuchet)
            var modulePath = RadoubSettings.Instance.CurrentModulePath;
            if (!string.IsNullOrEmpty(modulePath))
            {
                // If it's a directory, use it directly
                if (Directory.Exists(modulePath))
                    return modulePath;

                // If it's a .mod file, find the working directory
                if (File.Exists(modulePath) && modulePath.EndsWith(".mod", System.StringComparison.OrdinalIgnoreCase))
                {
                    var workingDir = FindWorkingDirectory(modulePath);
                    if (workingDir != null)
                        return workingDir;
                }
            }

            return null;
        }
    }

    /// <summary>
    /// Find the unpacked working directory for a .mod file.
    /// Checks for module name folder, temp0, or temp01.
    /// </summary>
    private static string? FindWorkingDirectory(string modFilePath)
    {
        var moduleName = Path.GetFileNameWithoutExtension(modFilePath);
        var moduleDir = Path.GetDirectoryName(modFilePath);

        if (string.IsNullOrEmpty(moduleDir))
            return null;

        // Check in priority order (same as Trebuchet)
        var candidates = new[]
        {
            Path.Combine(moduleDir, moduleName),
            Path.Combine(moduleDir, "temp0"),
            Path.Combine(moduleDir, "temp1")
        };

        foreach (var candidate in candidates)
        {
            if (Directory.Exists(candidate))
                return candidate;
        }

        return null;
    }

    public string? NeverwinterNightsPath => RadoubSettings.Instance.NeverwinterNightsPath;

    public string? ExternalEditorPath => null;

    public bool GameResourcesAvailable => false;

    public IEnumerable<(string ResRef, string SourcePath)> ListBuiltInScripts()
    {
        // Journals don't use built-in scripts
        yield break;
    }

    public byte[]? FindBuiltInResource(string resRef, ushort resourceType)
    {
        // Not used for journal browsing
        return null;
    }
}
