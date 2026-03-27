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
        if (!string.IsNullOrEmpty(currentModulePath) && Directory.Exists(currentModulePath))
            return currentModulePath;

        if (!string.IsNullOrEmpty(currentFileName))
            return Path.GetDirectoryName(currentFileName);

        return null;
    }
}
