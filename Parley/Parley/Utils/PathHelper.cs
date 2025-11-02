using System;
using System.IO;

namespace DialogEditor.Utils
{
    /// <summary>
    /// Helper utilities for path manipulation and display.
    /// </summary>
    public static class PathHelper
    {
        /// <summary>
        /// Sanitize path for display by replacing user profile path with ~
        /// Example: ~\Documents\... → ~\Documents\...
        /// </summary>
        public static string SanitizePathForDisplay(string path)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;

            try
            {
                var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

                if (!string.IsNullOrEmpty(userProfile) && path.StartsWith(userProfile, StringComparison.OrdinalIgnoreCase))
                {
                    // Replace user profile path with ~
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
    }
}
