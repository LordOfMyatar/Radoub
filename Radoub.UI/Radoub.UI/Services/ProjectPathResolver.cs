using System.IO;
using Radoub.Formats.Settings;

namespace Radoub.UI.Services;

/// <summary>
/// Resolves --mod module name and --file relative path to absolute file paths.
/// Used by all tools to support short CLI invocations like:
///   Tool --mod LNS --file dialog.dlg
/// which resolves to ~/Documents/Neverwinter Nights/modules/LNS/dialog.dlg
/// </summary>
public static class ProjectPathResolver
{
    /// <summary>
    /// Resolve a module name and optional file path to an absolute file path.
    /// </summary>
    /// <param name="moduleName">Module name (e.g., "LNS")</param>
    /// <param name="filePath">File path — if absolute, returned as-is. If relative, resolved under module.</param>
    /// <param name="modulesDirectory">Base modules directory. If null, uses RadoubSettings.</param>
    /// <returns>Resolved absolute file path, or null if no resolution possible.</returns>
    public static string? ResolveFilePath(string? moduleName, string? filePath, string? modulesDirectory = null)
    {
        // If file path is absolute, return as-is regardless of --mod
        if (!string.IsNullOrEmpty(filePath) && Path.IsPathRooted(filePath))
            return filePath;

        // Need a module name to resolve
        if (string.IsNullOrEmpty(moduleName))
            return null;

        // Need a file path to resolve
        if (string.IsNullOrEmpty(filePath))
            return null;

        var modulesDir = modulesDirectory ?? GetModulesDirectory();
        if (string.IsNullOrEmpty(modulesDir))
            return null;

        return Path.Combine(modulesDir, moduleName, filePath);
    }

    /// <summary>
    /// Resolve a module name to a module directory path.
    /// </summary>
    /// <param name="moduleName">Module name (e.g., "LNS")</param>
    /// <param name="modulesDirectory">Base modules directory. If null, uses RadoubSettings.</param>
    /// <returns>Absolute path to the module directory, or null.</returns>
    public static string? ResolveModulePath(string? moduleName, string? modulesDirectory = null)
    {
        if (string.IsNullOrEmpty(moduleName))
            return null;

        var modulesDir = modulesDirectory ?? GetModulesDirectory();
        if (string.IsNullOrEmpty(modulesDir))
            return null;

        return Path.Combine(modulesDir, moduleName);
    }

    /// <summary>
    /// Get the NWN modules directory from RadoubSettings.
    /// </summary>
    private static string? GetModulesDirectory()
    {
        var nwnPath = RadoubSettings.Instance.NeverwinterNightsPath;
        if (string.IsNullOrEmpty(nwnPath))
            return null;

        return Path.Combine(nwnPath, "modules");
    }
}
