using System;
using System.IO;

namespace DialogEditor.Services;

/// <summary>
/// Resolves the module directory for module-wide search operations.
/// Prioritizes explicit module path (from --mod flag) over file-derived path.
/// </summary>
public static class ModuleDirectoryResolver
{
    /// <summary>
    /// Resolve the module directory with priority:
    /// 1. currentModulePath (from RadoubSettings, set by --mod or Trebuchet) if valid
    /// 2. Parent directory of currentFileName (fallback)
    /// 3. null if neither is available
    /// </summary>
    public static string? Resolve(string? currentModulePath, string? currentFileName)
    {
        if (!string.IsNullOrEmpty(currentModulePath))
        {
            // Direct directory path (from --mod or unpacked module)
            if (Directory.Exists(currentModulePath))
                return currentModulePath;

            // .mod file path (from Trebuchet) — look for unpacked working directory
            if (currentModulePath.EndsWith(".mod", StringComparison.OrdinalIgnoreCase)
                && File.Exists(currentModulePath))
            {
                var moduleName = Path.GetFileNameWithoutExtension(currentModulePath);
                var parentDir = Path.GetDirectoryName(currentModulePath);
                if (!string.IsNullOrEmpty(parentDir))
                {
                    var workingDir = Path.Combine(parentDir, moduleName);
                    if (Directory.Exists(workingDir))
                        return workingDir;
                }
            }
        }

        if (!string.IsNullOrEmpty(currentFileName))
            return Path.GetDirectoryName(currentFileName);

        return null;
    }
}
