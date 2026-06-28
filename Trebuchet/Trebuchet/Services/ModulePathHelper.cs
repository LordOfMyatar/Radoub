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
    /// Delegates to the shared <see cref="PathHelper.FindWorkingDirectoryWithFallbacks"/> (#2355).
    /// </summary>
    public static string? FindWorkingDirectoryWithFallbacks(string? modulePath)
        => PathHelper.FindWorkingDirectoryWithFallbacks(modulePath);

    /// <summary>
    /// Returns true when <paramref name="archivePath"/> is the currently-open module's own .mod
    /// (#2268). Adding a module's contents back to itself via Add-to-ERF is a confusing no-op —
    /// "Save Module" is what repacks the working files. <paramref name="currentModulePath"/> may
    /// be either the .mod or its unpacked working directory.
    /// </summary>
    public static bool IsCurrentModuleArchive(string? archivePath, string? currentModulePath)
    {
        if (string.IsNullOrEmpty(archivePath) || string.IsNullOrEmpty(currentModulePath))
            return false;

        var currentMod = GetModFilePath(currentModulePath);
        if (string.IsNullOrEmpty(currentMod))
            return false;

        return string.Equals(
            Path.GetFullPath(archivePath),
            Path.GetFullPath(currentMod),
            StringComparison.OrdinalIgnoreCase);
    }
}
