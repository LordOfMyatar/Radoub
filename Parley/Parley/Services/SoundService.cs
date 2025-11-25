using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DialogEditor.Services
{
    /// <summary>
    /// Service for discovering and managing sound files from NWN installation.
    /// Supports .wav and .bmu (music) files from ambient, dialog, music, and soundset folders.
    /// </summary>
    public class SoundService
    {
        private readonly SettingsService _settingsService;

        public SoundService(SettingsService settingsService)
        {
            _settingsService = settingsService;
        }

        /// <summary>
        /// Get all sound files organized by category.
        /// Scans both game installation (Steam/Beamdog) and user Documents paths.
        /// </summary>
        public Dictionary<string, List<string>> GetSoundsByCategory()
        {
            var result = new Dictionary<string, List<string>>
            {
                ["Ambient"] = new List<string>(),
                ["Dialog"] = new List<string>(),
                ["Music"] = new List<string>(),
                ["Soundset"] = new List<string>()
            };

            var pathsScanned = new List<string>();

            // Scan user Documents path first (custom sounds - full names)
            var userPath = _settingsService.NeverwinterNightsPath;
            if (!string.IsNullOrEmpty(userPath) && Directory.Exists(userPath))
            {
                ScanCategoryWithFallback(userPath, "ambient", null, result["Ambient"]);
                ScanCategoryWithFallback(userPath, "dialog", null, result["Dialog"]);
                ScanCategoryWithFallback(userPath, "music", null, result["Music"]);
                ScanCategoryWithFallback(userPath, "soundset", null, result["Soundset"]);
                pathsScanned.Add(userPath);
            }

            // Scan game installation path (base game sounds)
            var installPath = _settingsService.BaseGameInstallPath;
            if (!string.IsNullOrEmpty(installPath) && Directory.Exists(installPath))
            {
                // Try root level first (full names)
                ScanCategoryWithFallback(installPath, "ambient", null, result["Ambient"]);
                ScanCategoryWithFallback(installPath, "dialog", null, result["Dialog"]);
                ScanCategoryWithFallback(installPath, "music", null, result["Music"]);
                ScanCategoryWithFallback(installPath, "soundset", null, result["Soundset"]);

                // Also try data\ subdirectory (abbreviated names: amb, mus, etc.)
                var dataPath = Path.Combine(installPath, "data");
                if (Directory.Exists(dataPath))
                {
                    ScanCategoryWithFallback(dataPath, "ambient", "amb", result["Ambient"]);
                    ScanCategoryWithFallback(dataPath, "dialog", "dlg", result["Dialog"]);
                    ScanCategoryWithFallback(dataPath, "music", "mus", result["Music"]);
                    ScanCategoryWithFallback(dataPath, "soundset", "sts", result["Soundset"]);
                }

                pathsScanned.Add(installPath);
            }

            // Remove duplicates and sort each category
            foreach (var category in result.Keys.ToList())
            {
                result[category] = result[category].Distinct().OrderBy(s => s).ToList();
            }

            var totalSounds = result.Values.Sum(list => list.Count);
            if (pathsScanned.Count == 0)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN, "No valid paths configured for sound scanning");
            }
            else
            {
                UnifiedLogger.LogApplication(LogLevel.INFO,
                    $"Found {totalSounds} sound files across {result.Count} categories (scanned {pathsScanned.Count} paths)");
            }

            return result;
        }

        /// <summary>
        /// Scan a category folder with fallback to abbreviated name.
        /// User paths: "ambient", "dialog", "music", "soundset"
        /// Game paths: "amb", "dlg", "mus", "sts"
        /// </summary>
        private void ScanCategoryWithFallback(string basePath, string fullName, string? abbreviatedName, List<string> soundList)
        {
            // Try full name first
            ScanCategory(basePath, fullName, soundList);

            // If abbreviated name provided and full name didn't find anything, try abbreviated
            if (abbreviatedName != null)
            {
                ScanCategory(basePath, abbreviatedName, soundList);
            }
        }

        /// <summary>
        /// Scan a specific category folder for sound files.
        /// </summary>
        private void ScanCategory(string basePath, string category, List<string> soundList)
        {
            var categoryPath = Path.Combine(basePath, category);
            if (!Directory.Exists(categoryPath))
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Sound category folder not found: {UnifiedLogger.SanitizePath(categoryPath)}");
                return;
            }

            try
            {
                // Get .wav files
                var wavFiles = Directory.GetFiles(categoryPath, "*.wav", SearchOption.TopDirectoryOnly);
                foreach (var file in wavFiles)
                {
                    soundList.Add(Path.GetFileName(file));
                }

                // Get .bmu files (music format)
                var bmuFiles = Directory.GetFiles(categoryPath, "*.bmu", SearchOption.TopDirectoryOnly);
                foreach (var file in bmuFiles)
                {
                    soundList.Add(Path.GetFileName(file));
                }

                // Sort alphabetically
                soundList.Sort();

                UnifiedLogger.LogApplication(LogLevel.DEBUG,
                    $"Found {soundList.Count} sound files in {category}");
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR,
                    $"Error scanning {category} folder: {ex.Message}");
            }
        }

        /// <summary>
        /// Add sound to recent sounds list.
        /// </summary>
        public void AddRecentSound(string soundName)
        {
            // This will be persisted through SettingsService
            // For now, just log it
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Added recent sound: {soundName}");

            // FUTURE Enhancement: Add to SettingsService.RecentSounds list and persist
        }

        /// <summary>
        /// Get list of recently used sounds.
        /// </summary>
        public List<string> GetRecentSounds()
        {
            // FUTURE Enhancement: Retrieve from SettingsService.RecentSounds
            return new List<string>();
        }
    }
}
