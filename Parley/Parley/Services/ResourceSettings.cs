using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using DialogEditor.Utils;

namespace DialogEditor.Services
{
    /// <summary>
    /// Manages resource paths for game assets (sounds, scripts, characters, journals).
    /// Provides cross-platform path detection and validation for Neverwinter Nights resources.
    /// </summary>
    public class ResourceSettings
    {
        /// <summary>
        /// Gets or sets the root Neverwinter Nights game installation path.
        /// Contains folders: ambient/, dialog/, music/, soundset/
        /// </summary>
        public string GamePath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the module directory path.
        /// Contains .nss (scripts), .utc (characters), .dlg (dialogs), .jrl (journals)
        /// </summary>
        public string ModulePath { get; set; } = string.Empty;

        /// <summary>
        /// Attempts to auto-detect Neverwinter Nights installation path based on platform.
        /// </summary>
        /// <returns>True if valid path detected, false otherwise</returns>
        public bool AutoDetectGamePath()
        {
            var possiblePaths = GetPlatformGamePaths();

            foreach (var path in possiblePaths)
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Checking game path: {path}");

                if (ValidateGamePath(path))
                {
                    GamePath = path;
                    UnifiedLogger.LogApplication(LogLevel.INFO, $"Auto-detected game path: {UnifiedLogger.SanitizePath(path)}");
                    return true;
                }
            }

            UnifiedLogger.LogApplication(LogLevel.WARN, "Failed to auto-detect game path");
            return false;
        }

        /// <summary>
        /// Attempts to auto-detect module directory path based on game path.
        /// </summary>
        /// <returns>True if valid module path detected, false otherwise</returns>
        public bool AutoDetectModulePath()
        {
            if (string.IsNullOrEmpty(GamePath))
            {
                UnifiedLogger.LogApplication(LogLevel.WARN, "Cannot detect module path - game path not set");
                return false;
            }

            // Try common module locations relative to game path
            var possibleModulePaths = new[]
            {
                Path.Combine(GamePath, "modules"),
                Path.Combine(GamePath, "Modules"),
                Path.Combine(GamePath, "..", "modules"), // Some installations
                Path.Combine(GamePath, "..", "Modules")
            };

            foreach (var path in possibleModulePaths)
            {
                var normalizedPath = Path.GetFullPath(path); // Resolve .. paths
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Checking module path: {normalizedPath}");

                if (Directory.Exists(normalizedPath))
                {
                    ModulePath = normalizedPath;
                    UnifiedLogger.LogApplication(LogLevel.INFO, $"Auto-detected module path: {UnifiedLogger.SanitizePath(normalizedPath)}");
                    return true;
                }
            }

            UnifiedLogger.LogApplication(LogLevel.WARN, "Failed to auto-detect module path");
            return false;
        }

        /// <summary>
        /// Validates that the specified path contains expected Neverwinter Nights game folders.
        /// </summary>
        /// <param name="path">Path to validate</param>
        /// <returns>True if path contains expected game folders</returns>
        public bool ValidateGamePath(string path)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
                return false;

            // Check for characteristic NWN folders
            // At least one of these should exist for a valid NWN installation
            var requiredFolders = new[] { "ambient", "dialog", "music" };
            var foundFolders = 0;

            foreach (var folder in requiredFolders)
            {
                var folderPath = Path.Combine(path, folder);
                if (Directory.Exists(folderPath))
                {
                    foundFolders++;
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Found NWN folder: {folder}");
                }
            }

            // Valid if at least 2 out of 3 folders exist
            var isValid = foundFolders >= 2;

            if (isValid)
            {
                UnifiedLogger.LogApplication(LogLevel.INFO, $"Valid game path: {path} ({foundFolders}/3 folders found)");
            }
            else
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Invalid game path: {path} ({foundFolders}/3 folders found)");
            }

            return isValid;
        }

        /// <summary>
        /// Validates that the specified path is a valid module directory or extracted module folder.
        /// </summary>
        /// <param name="path">Path to validate</param>
        /// <returns>True if path contains module files</returns>
        public bool ValidateModulePath(string path)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
                return false;

            // Check for at least one type of module file
            var hasScripts = Directory.GetFiles(path, "*.nss", SearchOption.TopDirectoryOnly).Length > 0;
            var hasCharacters = Directory.GetFiles(path, "*.utc", SearchOption.TopDirectoryOnly).Length > 0;
            var hasDialogs = Directory.GetFiles(path, "*.dlg", SearchOption.TopDirectoryOnly).Length > 0;
            var hasJournals = Directory.GetFiles(path, "*.jrl", SearchOption.TopDirectoryOnly).Length > 0;

            var isValid = hasScripts || hasCharacters || hasDialogs || hasJournals;

            if (isValid)
            {
                UnifiedLogger.LogApplication(LogLevel.INFO,
                    $"Valid module path: {path} (scripts={hasScripts}, chars={hasCharacters}, dialogs={hasDialogs}, journals={hasJournals})");
            }
            else
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Invalid module path: {path} (no module files found)");
            }

            return isValid;
        }

        /// <summary>
        /// Gets platform-specific default Neverwinter Nights installation paths.
        /// </summary>
        /// <returns>List of possible installation paths for current platform</returns>
        private List<string> GetPlatformGamePaths()
        {
            var paths = new List<string>();
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Windows paths
                paths.Add(Path.Combine(userProfile, "Documents", "Neverwinter Nights"));
                paths.Add(Path.Combine(appData, "Neverwinter Nights"));
                paths.Add(Path.Combine(localAppData, "Neverwinter Nights"));

                // Steam/GOG installations
                paths.Add("C:\\Program Files (x86)\\Steam\\steamapps\\common\\Neverwinter Nights");
                paths.Add("C:\\Program Files (x86)\\GOG Galaxy\\Games\\Neverwinter Nights Enhanced Edition");
                paths.Add("C:\\GOG Games\\Neverwinter Nights Enhanced Edition");

                // Beamdog installations
                paths.Add(Path.Combine(userProfile, "Beamdog Library", "00829"));
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // macOS paths
                paths.Add(Path.Combine(userProfile, "Library", "Application Support", "Neverwinter Nights"));
                paths.Add(Path.Combine(userProfile, "Documents", "Neverwinter Nights"));

                // Steam installation
                paths.Add(Path.Combine(userProfile, "Library", "Application Support", "Steam",
                    "steamapps", "common", "Neverwinter Nights"));
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // Linux paths
                paths.Add(Path.Combine(userProfile, ".local", "share", "Neverwinter Nights"));
                paths.Add(Path.Combine(userProfile, "Documents", "Neverwinter Nights"));

                // Steam installation
                paths.Add(Path.Combine(userProfile, ".steam", "steam", "steamapps", "common", "Neverwinter Nights"));
                paths.Add(Path.Combine(userProfile, ".local", "share", "Steam", "steamapps", "common", "Neverwinter Nights"));
            }

            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Generated {paths.Count} possible game paths for {RuntimeInformation.OSDescription}");

            return paths;
        }

        /// <summary>
        /// Gets a user-friendly validation error message if path is invalid.
        /// </summary>
        /// <param name="path">Path to check</param>
        /// <param name="isGamePath">True for game path, false for module path</param>
        /// <returns>Error message or empty string if valid</returns>
        public string GetValidationError(string path, bool isGamePath)
        {
            if (string.IsNullOrWhiteSpace(path))
                return isGamePath ? "Game path cannot be empty" : "Module path cannot be empty";

            if (!Directory.Exists(path))
                return $"Directory does not exist: {path}";

            if (isGamePath && !ValidateGamePath(path))
            {
                return $"Invalid Neverwinter Nights installation.\nExpected folders: ambient/, dialog/, music/\nPath: {path}";
            }

            if (!isGamePath && !ValidateModulePath(path))
            {
                return $"Invalid module directory.\nExpected files: *.nss, *.utc, *.dlg, or *.jrl\nPath: {path}";
            }

            return string.Empty; // Valid
        }

        /// <summary>
        /// Gets summary statistics about available resources in current paths.
        /// </summary>
        /// <returns>Dictionary of resource counts</returns>
        public Dictionary<string, int> GetResourceSummary()
        {
            var summary = new Dictionary<string, int>();

            // Count game resources
            if (!string.IsNullOrEmpty(GamePath) && Directory.Exists(GamePath))
            {
                summary["Ambient Sounds"] = CountFiles(Path.Combine(GamePath, "ambient"), "*.wav");
                summary["Dialog Sounds"] = CountFiles(Path.Combine(GamePath, "dialog"), "*.wav");
                summary["Music"] = CountFiles(Path.Combine(GamePath, "music"), "*.wav");
                summary["Soundsets"] = CountFiles(Path.Combine(GamePath, "soundset"), "*.wav");
            }

            // Count module resources
            if (!string.IsNullOrEmpty(ModulePath) && Directory.Exists(ModulePath))
            {
                summary["Scripts"] = CountFiles(ModulePath, "*.nss");
                summary["Characters"] = CountFiles(ModulePath, "*.utc");
                summary["Dialogs"] = CountFiles(ModulePath, "*.dlg");
                summary["Journals"] = CountFiles(ModulePath, "*.jrl");
            }

            return summary;
        }

        private int CountFiles(string directory, string pattern)
        {
            try
            {
                if (Directory.Exists(directory))
                {
                    return Directory.GetFiles(directory, pattern, SearchOption.TopDirectoryOnly).Length;
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN, $"Error counting files in {directory}: {ex.Message}");
            }

            return 0;
        }
    }
}
