using System;
using System.IO;
using Radoub.Formats.Common;
using Radoub.Formats.Settings;

namespace RadoubLauncher.Services;

/// <summary>
/// Centralized helper for resolving module working directories and .mod file paths.
/// Consolidates logic previously duplicated across GameLaunch, DefaultBic, FactionEditor,
/// and TrebuchetScriptBrowserContext.
/// </summary>
public static class ModulePathHelper
{
    /// <summary>
    /// Get the unpacked working directory for a module path.
    /// For .mod files: looks for a sibling directory with the module name.
    /// For directories: returns the path directly if it exists.
    /// </summary>
    public static string? GetWorkingDirectory(string? modulePath)
    {
        if (string.IsNullOrEmpty(modulePath))
            return null;

        if (modulePath.EndsWith(".mod", StringComparison.OrdinalIgnoreCase) && File.Exists(modulePath))
        {
            var moduleName = Path.GetFileNameWithoutExtension(modulePath);
            var moduleDir = Path.GetDirectoryName(modulePath);
            if (string.IsNullOrEmpty(moduleDir))
                return null;

            var workingDir = Path.Combine(moduleDir, moduleName);
            if (Directory.Exists(workingDir))
                return workingDir;
        }
        else if (Directory.Exists(modulePath))
        {
            return modulePath;
        }

        return null;
    }

    /// <summary>
    /// Check if an unpacked working directory exists and contains module.ifo.
    /// </summary>
    public static bool HasUnpackedWorkingDirectory(string? modulePath)
    {
        var workingDir = GetWorkingDirectory(modulePath);
        if (workingDir == null)
            return false;

        // Case-insensitive for Linux (#1384)
        return PathHelper.FileExistsInDirectory(workingDir, "module.ifo");
    }

    /// <summary>
    /// Get the .mod file path for a module path.
    /// For .mod files: returns the path directly.
    /// For directories: looks for a sibling .mod file with the same name.
    /// </summary>
    public static string? GetModFilePath(string? modulePath)
    {
        if (string.IsNullOrEmpty(modulePath))
            return null;

        if (modulePath.EndsWith(".mod", StringComparison.OrdinalIgnoreCase))
            return modulePath;

        if (Directory.Exists(modulePath))
        {
            var dirName = Path.GetFileName(modulePath);
            var parentDir = Path.GetDirectoryName(modulePath);
            if (!string.IsNullOrEmpty(parentDir))
            {
                var modPath = Path.Combine(parentDir, dirName + ".mod");
                if (File.Exists(modPath))
                    return modPath;
            }
        }

        return null;
    }

    /// <summary>
    /// Find the working directory for a .mod file, checking additional candidates (temp0, temp1).
    /// Used by FactionEditor and ScriptBrowser which need broader search.
    /// </summary>
    public static string? FindWorkingDirectoryWithFallbacks(string? modulePath)
    {
        if (string.IsNullOrEmpty(modulePath))
            return null;

        if (modulePath.EndsWith(".mod", StringComparison.OrdinalIgnoreCase) && File.Exists(modulePath))
        {
            var moduleName = Path.GetFileNameWithoutExtension(modulePath);
            var moduleDir = Path.GetDirectoryName(modulePath);
            if (string.IsNullOrEmpty(moduleDir))
                return null;

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

        if (Directory.Exists(modulePath))
            return modulePath;

        return null;
    }
}
