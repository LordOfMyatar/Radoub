using System.IO;
using Radoub.Formats.Settings;

namespace Radoub.UI.Services;

/// <summary>
/// Resolves --project module name and --file relative path to absolute file paths.
/// Used by all tools to support short CLI invocations like:
///   Tool --project LNS --file dialog.dlg
/// which resolves to ~/Documents/Neverwinter Nights/modules/LNS/dialog.dlg
/// </summary>
public static class ProjectPathResolver
{
    /// <summary>
    /// Resolve a project name and optional file path to an absolute file path.
    /// </summary>
    /// <param name="projectName">Module/project name (e.g., "LNS")</param>
    /// <param name="filePath">File path — if absolute, returned as-is. If relative, resolved under project.</param>
    /// <param name="modulesDirectory">Base modules directory. If null, uses RadoubSettings.</param>
    /// <returns>Resolved absolute file path, or null if no resolution possible.</returns>
    public static string? ResolveFilePath(string? projectName, string? filePath, string? modulesDirectory = null)
    {
        // If file path is absolute, return as-is regardless of --project
        if (!string.IsNullOrEmpty(filePath) && Path.IsPathRooted(filePath))
            return filePath;

        // Need a project name to resolve
        if (string.IsNullOrEmpty(projectName))
            return null;

        // Need a file path to resolve
        if (string.IsNullOrEmpty(filePath))
            return null;

        var modulesDir = modulesDirectory ?? GetModulesDirectory();
        if (string.IsNullOrEmpty(modulesDir))
            return null;

        return Path.Combine(modulesDir, projectName, filePath);
    }

    /// <summary>
    /// Resolve a project name to a module directory path.
    /// </summary>
    /// <param name="projectName">Module/project name (e.g., "LNS")</param>
    /// <param name="modulesDirectory">Base modules directory. If null, uses RadoubSettings.</param>
    /// <returns>Absolute path to the module directory, or null.</returns>
    public static string? ResolveModulePath(string? projectName, string? modulesDirectory = null)
    {
        if (string.IsNullOrEmpty(projectName))
            return null;

        var modulesDir = modulesDirectory ?? GetModulesDirectory();
        if (string.IsNullOrEmpty(modulesDir))
            return null;

        return Path.Combine(modulesDir, projectName);
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
