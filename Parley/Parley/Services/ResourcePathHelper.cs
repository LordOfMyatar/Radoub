using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace DialogEditor.Services
{
    /// <summary>
    /// Helper class for detecting and validating Neverwinter Nights resource paths across platforms
    /// </summary>
    public static class ResourcePathHelper
    {
        /// <summary>
        /// Attempts to auto-detect the Neverwinter Nights game installation path
        /// </summary>
        public static string? AutoDetectGamePath()
        {
            var possiblePaths = GetPlatformGamePaths();

            foreach (var path in possiblePaths)
            {
                if (ValidateGamePath(path))
                {
                    UnifiedLogger.LogApplication(LogLevel.INFO, $"Auto-detected game path: {UnifiedLogger.SanitizePath(path)}");
                    return path;
                }
            }

            UnifiedLogger.LogApplication(LogLevel.WARN, "Could not auto-detect game path");
            return null;
        }

        /// <summary>
        /// Attempts to auto-detect the module directory path
        /// </summary>
        public static string? AutoDetectModulePath(string? gamePath = null)
        {
            // If game path provided, check for modules subdirectory
            if (!string.IsNullOrEmpty(gamePath))
            {
                var modulePath = Path.Combine(gamePath, "modules");
                if (ValidateModulePath(modulePath))
                {
                    UnifiedLogger.LogApplication(LogLevel.INFO, $"Found module path in game directory: {UnifiedLogger.SanitizePath(modulePath)}");
                    return modulePath;
                }
            }

            // Try platform-specific module paths
            var possiblePaths = GetPlatformModulePaths();
            foreach (var path in possiblePaths)
            {
                if (ValidateModulePath(path))
                {
                    UnifiedLogger.LogApplication(LogLevel.INFO, $"Auto-detected module path: {path}");
                    return path;
                }
            }

            UnifiedLogger.LogApplication(LogLevel.WARN, "Could not auto-detect module path");
            return null;
        }

        /// <summary>
        /// Validates that a path is a valid NWN game installation
        /// </summary>
        public static bool ValidateGamePath(string path)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
                return false;

            // Check for characteristic NWN subdirectories
            var requiredDirs = new[] { "ambient", "music" };
            foreach (var dir in requiredDirs)
            {
                var fullPath = Path.Combine(path, dir);
                if (!Directory.Exists(fullPath))
                {
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Game path validation failed: missing '{dir}' directory");
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Validates that a path is a valid module directory
        /// Looks for .mod files - subdirectories are common but optional
        /// </summary>
        public static bool ValidateModulePath(string path)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
                return false;

            // Check for .mod files (subdirectories are common but not required)
            var modFiles = Directory.GetFiles(path, "*.mod", SearchOption.TopDirectoryOnly);

            if (modFiles.Length > 0)
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Module path validated: found {modFiles.Length} .mod file(s)");
                return true;
            }

            // Check subdirectories for .mod files
            try
            {
                foreach (var subDir in Directory.GetDirectories(path))
                {
                    var subModFiles = Directory.GetFiles(subDir, "*.mod", SearchOption.TopDirectoryOnly);
                    if (subModFiles.Length > 0)
                    {
                        UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Module path validated: found .mod files in subdirectory {Path.GetFileName(subDir)}");
                        return true;
                    }
                }
            }
            catch
            {
                // Ignore subdirectory access errors
            }

            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Module path validation failed: no .mod files found in '{path}' or subdirectories");
            return false;
        }

        /// <summary>
        /// Gets platform-specific default game installation paths
        /// </summary>
        private static List<string> GetPlatformGamePaths()
        {
            var paths = new List<string>();
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Windows: Documents\Neverwinter Nights
                var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                paths.Add(Path.Combine(documents, "Neverwinter Nights"));

                // Alternative: User profile
                paths.Add(Path.Combine(userProfile, "Documents", "Neverwinter Nights"));
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // macOS: ~/Library/Application Support/Neverwinter Nights
                paths.Add(Path.Combine(userProfile, "Library", "Application Support", "Neverwinter Nights"));
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // Linux: ~/.local/share/Neverwinter Nights
                paths.Add(Path.Combine(userProfile, ".local", "share", "Neverwinter Nights"));
            }

            return paths;
        }

        /// <summary>
        /// Gets platform-specific default module paths
        /// </summary>
        private static List<string> GetPlatformModulePaths()
        {
            var paths = new List<string>();

            // Module paths are typically game_path/modules
            foreach (var gamePath in GetPlatformGamePaths())
            {
                paths.Add(Path.Combine(gamePath, "modules"));
            }

            return paths;
        }

        /// <summary>
        /// Gets the sound directories relative to game path
        /// </summary>
        public static List<string> GetSoundDirectories(string gamePath)
        {
            if (string.IsNullOrEmpty(gamePath))
                return new List<string>();

            var soundDirs = new List<string>();
            var categories = new[] { "ambient", "dialog", "music", "soundset" };

            foreach (var category in categories)
            {
                var path = Path.Combine(gamePath, category);
                if (Directory.Exists(path))
                {
                    soundDirs.Add(path);
                }
            }

            return soundDirs;
        }
    }
}
