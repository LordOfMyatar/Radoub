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
    /// ContractPath("C:\Users\John\Documents\file.txt") returns "~\Documents\file.txt"
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
    /// SanitizePathForDisplay("C:\Users\John\Documents\file.txt") returns "~\Documents\file.txt"
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
        catch
        {
            return path;
        }
    }

    /// <summary>
    /// Expands a path from storage - replaces ~ with user home directory.
    /// </summary>
    /// <param name="path">The contracted path to expand.</param>
    /// <returns>Path with ~ replaced by user home directory, or original path if no ~.</returns>
    /// <example>
    /// ExpandPath("~\Documents\file.txt") returns "C:\Users\John\Documents\file.txt"
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
