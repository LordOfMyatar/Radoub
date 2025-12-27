using System;
using Radoub.Formats.Logging;
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

        #region Base Game Installation Path (#345)

        /// <summary>
        /// Validates that a path is a valid NWN base game installation (Steam/GOG/etc).
        /// Base game installation should have data\ folder.
        /// </summary>
        public static bool ValidateBaseGamePath(string path)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
                return false;

            var dataPath = Path.Combine(path, "data");
            bool valid = Directory.Exists(dataPath);

            if (valid)
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Base game path validated: found data\\ folder");
            }
            else
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Base game path validation failed: missing data\\ folder");
            }

            return valid;
        }

        /// <summary>
        /// Returns validation result with message for UI display.
        /// </summary>
        public static PathValidationResult ValidateBaseGamePathWithMessage(string path)
        {
            if (string.IsNullOrEmpty(path))
                return new PathValidationResult(false, "");

            if (ValidateBaseGamePath(path))
                return new PathValidationResult(true, "✅ Valid base game installation (contains data\\ folder)");

            return new PathValidationResult(false, "❌ Invalid path - missing data\\ folder");
        }

        /// <summary>
        /// Returns validation result with message for UI display.
        /// </summary>
        public static PathValidationResult ValidateGamePathWithMessage(string path)
        {
            if (string.IsNullOrEmpty(path))
                return new PathValidationResult(false, "");

            if (ValidateGamePath(path))
                return new PathValidationResult(true, "✅ Valid game installation path");

            return new PathValidationResult(false, "❌ Invalid path - missing required directories (ambient, music)");
        }

        /// <summary>
        /// Returns validation result with message for UI display.
        /// </summary>
        public static PathValidationResult ValidateModulePathWithMessage(string path)
        {
            if (string.IsNullOrEmpty(path))
                return new PathValidationResult(false, "");

            if (ValidateModulePath(path))
                return new PathValidationResult(true, "✅ Valid module directory");

            return new PathValidationResult(false, "❌ Invalid path - no .mod files or module directories found");
        }

        /// <summary>
        /// Attempts to auto-detect the base game installation path (Steam/GOG).
        /// </summary>
        public static string? AutoDetectBaseGamePath()
        {
            var possiblePaths = GetPlatformBaseGamePaths();

            foreach (var path in possiblePaths)
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Checking base game path: {path}");
                if (ValidateBaseGamePath(path))
                {
                    UnifiedLogger.LogApplication(LogLevel.INFO, $"Auto-detected base game path: {UnifiedLogger.SanitizePath(path)}");
                    return path;
                }
            }

            UnifiedLogger.LogApplication(LogLevel.WARN, "Could not auto-detect base game installation");
            return null;
        }

        /// <summary>
        /// Gets platform-specific base game installation paths (Steam, GOG, etc.)
        /// </summary>
        private static List<string> GetPlatformBaseGamePaths()
        {
            var paths = new List<string>();
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Try Steam registry first
                try
                {
                    using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
                    {
                        var steamPath = key?.GetValue("SteamPath") as string;
                        if (!string.IsNullOrEmpty(steamPath))
                        {
                            var nwnPath = Path.Combine(steamPath, "steamapps", "common", "Neverwinter Nights");
                            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Found Steam path from registry: {steamPath}");
                            paths.Add(nwnPath);
                        }
                    }
                }
                catch (Exception ex)
                {
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Could not read Steam registry: {ex.Message}");
                }

                // Common Steam locations
                paths.AddRange(new[]
                {
                    @"C:\Program Files (x86)\Steam\steamapps\common\Neverwinter Nights",
                    @"C:\Program Files\Steam\steamapps\common\Neverwinter Nights",
                    @"D:\SteamLibrary\steamapps\common\Neverwinter Nights",
                    @"E:\SteamLibrary\steamapps\common\Neverwinter Nights"
                });

                // GOG paths
                paths.AddRange(new[]
                {
                    @"C:\Program Files (x86)\GOG Galaxy\Games\Neverwinter Nights Enhanced Edition",
                    @"C:\GOG Games\Neverwinter Nights Enhanced Edition"
                });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                paths.Add("/Applications/Neverwinter Nights.app/Contents/Resources");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                paths.Add(Path.Combine(home, ".steam", "steam", "steamapps", "common", "Neverwinter Nights"));
            }

            return paths;
        }

        #endregion
    }

    /// <summary>
    /// Result of a path validation with message for UI display (#345)
    /// </summary>
    public record PathValidationResult(bool IsValid, string Message);
}
