using Radoub.Formats.Logging;

namespace Radoub.Formats.Common;

/// <summary>
/// Utility methods for path manipulation, particularly for privacy-safe settings storage.
/// Provides ContractPath/ExpandPath for replacing user home directory with ~.
/// </summary>
public static class PathHelper
{
    /// <summary>
    /// Contracts a path for storage - replaces user home directory with ~.
    /// This makes settings files portable and privacy-safe for sharing.
    /// </summary>
    /// <param name="path">The full path to contract.</param>
    /// <returns>Path with user home directory replaced by ~, or original path if not under home.</returns>
    /// <example>
    /// ContractPath("{UserProfile}\Documents\file.txt") returns "~\Documents\file.txt"
    /// ContractPath("D:\Games\NWN") returns "D:\Games\NWN" (unchanged)
    /// </example>
    public static string ContractPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return path;

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(userProfile) && path.StartsWith(userProfile, StringComparison.OrdinalIgnoreCase))
        {
            return "~" + path.Substring(userProfile.Length);
        }

        return path;
    }

    /// <summary>
    /// Sanitize path for display by replacing user profile path with ~.
    /// Similar to ContractPath but ensures a clean display format with proper separators.
    /// </summary>
    /// <param name="path">The full path to sanitize.</param>
    /// <returns>Path with user home directory replaced by ~, or original path if not under home.</returns>
    /// <example>
    /// SanitizePathForDisplay("{UserProfile}\Documents\file.txt") returns "~\Documents\file.txt"
    /// </example>
    public static string SanitizePathForDisplay(string path)
    {
        if (string.IsNullOrEmpty(path))
            return string.Empty;

        try
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            if (!string.IsNullOrEmpty(userProfile) && path.StartsWith(userProfile, StringComparison.OrdinalIgnoreCase))
            {
                var relativePath = path.Substring(userProfile.Length);

                // Remove leading path separator if present
                if (relativePath.StartsWith(Path.DirectorySeparatorChar.ToString()) ||
                    relativePath.StartsWith(Path.AltDirectorySeparatorChar.ToString()))
                {
                    relativePath = relativePath.Substring(1);
                }

                return $"~{Path.DirectorySeparatorChar}{relativePath}";
            }

            return path;
        }
        catch (Exception ex)
        {
            UnifiedLogger.Log(LogLevel.DEBUG, $"Path sanitization failed: {ex.Message}", "PathHelper", "Common");
            return path;
        }
    }

    /// <summary>
    /// Expands a path from storage - replaces ~ with user home directory.
    /// </summary>
    /// <param name="path">The contracted path to expand.</param>
    /// <returns>Path with ~ replaced by user home directory, or original path if no ~.</returns>
    /// <example>
    /// ExpandPath("~\Documents\file.txt") returns "{UserProfile}\Documents\file.txt"
    /// ExpandPath("D:\Games\NWN") returns "D:\Games\NWN" (unchanged)
    /// </example>
    public static string ExpandPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return path;

        if (path.StartsWith("~"))
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return userProfile + path.Substring(1);
        }

        return path;
    }

    /// <summary>
    /// Find a file in a directory using case-insensitive matching.
    /// First tries the exact path (fast path), then falls back to case-insensitive
    /// directory enumeration. Required for cross-platform support - Linux filesystems
    /// are case-sensitive but Aurora Engine ResRefs may have mixed case. (#1384)
    /// </summary>
    /// <param name="directory">The directory to search in.</param>
    /// <param name="fileName">The filename to find (e.g., "module.ifo").</param>
    /// <returns>The actual file path with correct casing, or null if not found.</returns>
    public static string? FindFileInDirectory(string directory, string fileName)
    {
        if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(fileName))
            return null;

        // Fast path: exact match (always works on Windows, works on Linux if case matches)
        var exactPath = Path.Combine(directory, fileName);
        if (File.Exists(exactPath))
            return exactPath;

        // Slow path: case-insensitive search (needed on Linux)
        try
        {
            if (!Directory.Exists(directory))
                return null;

            var match = Directory.GetFiles(directory, fileName,
                new EnumerationOptions { MatchCasing = MatchCasing.CaseInsensitive })
                .FirstOrDefault();
            return match;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Check if a file exists in a directory using case-insensitive matching.
    /// Convenience wrapper around FindFileInDirectory. (#1384)
    /// </summary>
    public static bool FileExistsInDirectory(string directory, string fileName)
    {
        return FindFileInDirectory(directory, fileName) != null;
    }

    /// <summary>
    /// Find the unpacked working directory for a module path, checking fallback
    /// candidates. For a .mod file, checks (in priority order) a sibling directory
    /// named after the module, then "temp0", then "temp1". For an existing directory
    /// path, returns it directly. Returns null when nothing matches. (#2355)
    /// </summary>
    /// <param name="modulePath">A .mod file path or a directory path.</param>
    /// <param name="requireModuleIfo">
    /// When true, a candidate directory only matches if it contains a module.ifo
    /// (case-insensitive). Parley uses this stricter check; other tools do not.
    /// </param>
    /// <returns>The working directory path, or null if none found.</returns>
    public static string? FindWorkingDirectoryWithFallbacks(string? modulePath, bool requireModuleIfo = false)
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
                if (!Directory.Exists(candidate))
                    continue;
                if (requireModuleIfo && !FileExistsInDirectory(candidate, "module.ifo"))
                    continue;
                return candidate;
            }

            return null;
        }

        if (Directory.Exists(modulePath))
            return modulePath;

        return null;
    }

    /// <summary>
    /// Contracts a list of paths for storage.
    /// </summary>
    /// <param name="paths">List of paths to contract.</param>
    /// <returns>New list with all paths contracted.</returns>
    public static List<string> ContractPaths(IEnumerable<string> paths)
    {
        return paths.Select(ContractPath).ToList();
    }

    /// <summary>
    /// Expands a list of paths from storage.
    /// </summary>
    /// <param name="paths">List of paths to expand.</param>
    /// <returns>New list with all paths expanded.</returns>
    public static List<string> ExpandPaths(IEnumerable<string> paths)
    {
        return paths.Select(ExpandPath).ToList();
    }
}
