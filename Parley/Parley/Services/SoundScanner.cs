using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DialogEditor.Models.Sound;
using Radoub.Formats.Common;
using Radoub.Formats.Erf;
using Radoub.Formats.Logging;

namespace DialogEditor.Services
{
    /// <summary>
    /// Scans file system, HAK files, and BIF archives for WAV sound resources.
    /// </summary>
    public class SoundScanner
    {
        private readonly SoundCache _cache;
        private readonly SettingsService _settings;

        public SoundScanner(SettingsService settings)
        {
            _cache = SoundCache.Instance;
            _settings = settings;
        }

        /// <summary>
        /// Scans a directory for loose WAV files.
        /// </summary>
        /// <param name="path">Directory to scan.</param>
        /// <param name="source">Source label (e.g., "Override", folder name).</param>
        /// <param name="existingSounds">List to check for duplicates (first found wins per NWN priority).</param>
        /// <returns>Found sounds.</returns>
        public List<SoundFileInfo> ScanPathForSounds(string path, string source, IReadOnlyList<SoundFileInfo> existingSounds)
        {
            var results = new List<SoundFileInfo>();

            try
            {
                var wavFiles = Directory.GetFiles(path, "*.wav", SearchOption.TopDirectoryOnly);
                foreach (var file in wavFiles)
                {
                    var fileName = Path.GetFileName(file);
                    var isMono = SoundValidator.IsMonoWav(file);

                    // Skip if already exists (avoid duplicates - first found wins, per NWN resource priority)
                    if (!existingSounds.Any(s => s.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase)) &&
                        !results.Any(s => s.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase)))
                    {
                        results.Add(new SoundFileInfo
                        {
                            FileName = fileName,
                            FullPath = file,
                            IsMono = isMono,
                            Source = string.IsNullOrEmpty(source) ? Path.GetFileName(path) : source
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error scanning {path}: {ex.Message}");
            }

            return results;
        }

        /// <summary>
        /// Scans known NWN sound folders (ambient, dialog, music, soundset).
        /// </summary>
        public List<SoundFileInfo> ScanAllSoundFolders(string basePath, IReadOnlyList<SoundFileInfo> existingSounds)
        {
            var results = new List<SoundFileInfo>();
            var soundFolders = new[] { "ambient", "amb", "dialog", "dlg", "music", "mus", "soundset", "sts" };

            foreach (var folder in soundFolders)
            {
                var fullPath = Path.Combine(basePath, folder);
                if (Directory.Exists(fullPath))
                {
                    var combined = existingSounds.Concat(results).ToList();
                    results.AddRange(ScanPathForSounds(fullPath, "", combined));
                }
            }

            return results;
        }

        /// <summary>
        /// Scans a directory for HAK files and extracts WAV resources from them.
        /// </summary>
        /// <param name="path">Directory to scan for .hak files.</param>
        /// <param name="existingSounds">List to check for duplicates.</param>
        /// <param name="progressCallback">Optional callback for progress updates (hakName, current, total).</param>
        public async Task<List<SoundFileInfo>> ScanPathForHaksAsync(
            string path,
            IReadOnlyList<SoundFileInfo> existingSounds,
            Action<string, int, int>? progressCallback = null)
        {
            var results = new List<SoundFileInfo>();

            try
            {
                if (!Directory.Exists(path))
                    return results;

                var hakFiles = Directory.GetFiles(path, "*.hak", SearchOption.TopDirectoryOnly);

                for (int i = 0; i < hakFiles.Length; i++)
                {
                    var hakFile = hakFiles[i];
                    var hakName = Path.GetFileName(hakFile);

                    progressCallback?.Invoke(hakName, i + 1, hakFiles.Length);

                    var combined = existingSounds.Concat(results).ToList();
                    var hakSounds = await Task.Run(() => ScanHakForSounds(hakFile, combined));
                    results.AddRange(hakSounds);
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error scanning for HAKs in {path}: {ex.Message}");
            }

            return results;
        }

        /// <summary>
        /// Scans a single HAK file for WAV resources.
        /// </summary>
        public List<SoundFileInfo> ScanHakForSounds(string hakPath, IReadOnlyList<SoundFileInfo> existingSounds)
        {
            var results = new List<SoundFileInfo>();

            try
            {
                var hakFileName = Path.GetFileName(hakPath);
                var lastModified = File.GetLastWriteTimeUtc(hakPath);

                // Check cache first
                var cached = _cache.GetHakCache(hakPath);
                if (cached != null)
                {
                    // Use cached sounds - deep copy to avoid shared state issues
                    foreach (var sound in cached.Sounds)
                    {
                        if (!existingSounds.Any(s => s.FileName.Equals(sound.FileName, StringComparison.OrdinalIgnoreCase)) &&
                            !results.Any(s => s.FileName.Equals(sound.FileName, StringComparison.OrdinalIgnoreCase)))
                        {
                            results.Add(new SoundFileInfo
                            {
                                FileName = sound.FileName,
                                FullPath = sound.FullPath,
                                IsMono = sound.IsMono,
                                Source = sound.Source,
                                HakPath = sound.HakPath,
                                ErfEntry = sound.ErfEntry
                            });
                        }
                    }
                    UnifiedLogger.LogApplication(LogLevel.DEBUG,
                        $"Sound Browser: Used cached {cached.Sounds.Count} sounds from {hakFileName}");
                    return results;
                }

                // Not cached or outdated - scan HAK
                var erf = ErfReader.ReadMetadataOnly(hakPath);
                var wavResources = erf.GetResourcesByType(ResourceTypes.Wav).ToList();
                var newCacheEntry = new HakCacheEntry
                {
                    HakPath = hakPath,
                    LastModified = lastModified,
                    Sounds = new List<SoundFileInfo>()
                };

                foreach (var resource in wavResources)
                {
                    var fileName = $"{resource.ResRef}.wav";
                    var soundInfo = new SoundFileInfo
                    {
                        FileName = fileName,
                        FullPath = hakPath,
                        IsMono = true, // Assume mono until verified
                        Source = hakFileName,
                        HakPath = hakPath,
                        ErfEntry = resource
                    };

                    // Add to cache
                    newCacheEntry.Sounds.Add(soundInfo);

                    // Add to results if not duplicate
                    if (!existingSounds.Any(s => s.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase)) &&
                        !results.Any(s => s.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase)))
                    {
                        results.Add(soundInfo);
                    }
                }

                // Update cache
                _cache.SetHakCache(hakPath, newCacheEntry);

                UnifiedLogger.LogApplication(LogLevel.INFO,
                    $"Sound Browser: Scanned and cached {wavResources.Count} WAV resources in {hakFileName}");
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error scanning HAK {hakPath}: {ex.Message}");
            }

            return results;
        }

        /// <summary>
        /// Scans BIF archives for WAV resources using KEY file index.
        /// </summary>
        /// <param name="basePath">Game base path.</param>
        /// <param name="existingSounds">List to check for duplicates.</param>
        /// <param name="progressCallback">Optional callback for progress updates (bifName).</param>
        public async Task<List<SoundFileInfo>> ScanBifArchivesAsync(
            string basePath,
            IReadOnlyList<SoundFileInfo> existingSounds,
            Action<string>? progressCallback = null)
        {
            var results = new List<SoundFileInfo>();

            try
            {
                // Find KEY file - NWN:EE uses nwn_base.key in data/ folder
                var keyPaths = new[]
                {
                    Path.Combine(basePath, "data", "nwn_base.key"),
                    Path.Combine(basePath, "nwn_base.key"),
                    Path.Combine(basePath, "chitin.key") // Classic NWN
                };

                string? keyPath = null;
                foreach (var path in keyPaths)
                {
                    if (File.Exists(path))
                    {
                        keyPath = path;
                        break;
                    }
                }

                if (keyPath == null)
                {
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, "Sound Browser: No KEY file found for BIF scanning");
                    return results;
                }

                // Load or get cached KEY file
                var keyFile = await Task.Run(() => _cache.GetOrLoadKeyFile(keyPath));
                if (keyFile == null)
                {
                    UnifiedLogger.LogApplication(LogLevel.WARN, $"Sound Browser: Could not load KEY file: {UnifiedLogger.SanitizePath(keyPath)}");
                    return results;
                }

                // Get all WAV resources from KEY
                var wavResources = keyFile.GetResourcesByType(ResourceTypes.Wav).ToList();
                UnifiedLogger.LogApplication(LogLevel.INFO, $"Sound Browser: Found {wavResources.Count} WAV resources in KEY file");

                // Group resources by BIF file for efficient loading
                var resourcesByBif = wavResources.GroupBy(r => r.BifIndex).ToList();

                foreach (var bifGroup in resourcesByBif)
                {
                    var bifIndex = bifGroup.Key;
                    if (bifIndex >= keyFile.BifEntries.Count)
                    {
                        UnifiedLogger.LogApplication(LogLevel.WARN, $"Sound Browser: Invalid BIF index {bifIndex}");
                        continue;
                    }

                    var bifEntry = keyFile.BifEntries[bifIndex];
                    var bifPath = ResolveBifPath(basePath, bifEntry.Filename);

                    if (bifPath == null || !File.Exists(bifPath))
                    {
                        UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Sound Browser: BIF not found: {bifEntry.Filename}");
                        continue;
                    }

                    var bifName = Path.GetFileName(bifPath);
                    progressCallback?.Invoke(bifName);

                    // Load BIF file (cached)
                    var bifFile = await Task.Run(() => _cache.GetOrLoadBifFile(bifPath));
                    if (bifFile == null)
                        continue;

                    foreach (var resource in bifGroup)
                    {
                        var fileName = $"{resource.ResRef}.wav";

                        // Skip if already found from higher priority source
                        if (existingSounds.Any(s => s.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase)) ||
                            results.Any(s => s.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase)))
                            continue;

                        results.Add(new SoundFileInfo
                        {
                            FileName = fileName,
                            FullPath = bifPath,
                            IsMono = true, // Assume mono until verified
                            Source = $"BIF:{bifName}",
                            BifInfo = new BifSoundInfo
                            {
                                ResRef = resource.ResRef,
                                BifPath = bifPath,
                                VariableTableIndex = resource.VariableTableIndex,
                                FileSize = 0 // Will be determined on extraction
                            }
                        });
                    }
                }

                UnifiedLogger.LogApplication(LogLevel.INFO, $"Sound Browser: Added {results.Count} WAV resources from BIF archives");
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error scanning BIF archives: {ex.Message}");
            }

            return results;
        }

        /// <summary>
        /// Resolve a BIF filename from KEY to an actual file path.
        /// </summary>
        private string? ResolveBifPath(string basePath, string bifFilename)
        {
            // Normalize path separators
            var normalized = bifFilename.Replace("\\", "/").Replace("/", Path.DirectorySeparatorChar.ToString());

            // Try relative to base path (for "data\file.bif" paths)
            var fullPath = Path.Combine(basePath, normalized);
            if (File.Exists(fullPath))
                return fullPath;

            // Try just the filename in data folder
            var dataPath = Path.Combine(basePath, "data", Path.GetFileName(normalized));
            if (File.Exists(dataPath))
                return dataPath;

            // Try in lang folders (for language-specific BIFs)
            var langPath = Path.Combine(basePath, "lang");
            if (Directory.Exists(langPath))
            {
                foreach (var langDir in Directory.GetDirectories(langPath))
                {
                    var langBifPath = Path.Combine(langDir, normalized);
                    if (File.Exists(langBifPath))
                        return langBifPath;

                    langBifPath = Path.Combine(langDir, "data", Path.GetFileName(normalized));
                    if (File.Exists(langBifPath))
                        return langBifPath;
                }
            }

            return null;
        }
    }
}
