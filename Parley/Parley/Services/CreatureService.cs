using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DialogEditor.Models;
using DialogEditor.Parsers;

namespace DialogEditor.Services
{
    /// <summary>
    /// Service for scanning, parsing, and caching creature (UTC) files.
    /// Provides tag-based lookup for Speaker/Listener field population.
    /// </summary>
    public class CreatureService
    {
        private Dictionary<string, CreatureInfo> _creatureCache = new();
        private string? _cachedDirectory;
        private readonly CreatureParser _parser = new();
        private readonly TwoDAService _twoDAService = new();

        /// <summary>
        /// Scan directory for *.utc files and cache creature data.
        /// Also loads classes.2da for class name resolution.
        /// Returns list sorted by Tag for UI display.
        /// </summary>
        public async Task<List<CreatureInfo>> ScanCreaturesAsync(string moduleDirectory, string? gameDataDirectory = null)
        {
            // Return cached if same directory
            if (_creatureCache.Count > 0 && _cachedDirectory == moduleDirectory)
            {
                UnifiedLogger.LogParser(LogLevel.DEBUG, $"Returning cached creature data for {UnifiedLogger.SanitizePath(moduleDirectory)}");
                return _creatureCache.Values.OrderBy(c => c.Tag).ToList();
            }

            ClearCache();

            if (!Directory.Exists(moduleDirectory))
            {
                UnifiedLogger.LogParser(LogLevel.WARN, $"Creature directory not found: {UnifiedLogger.SanitizePath(moduleDirectory)}");
                return new List<CreatureInfo>();
            }

            // Load classes.2da if game data directory provided
            if (!string.IsNullOrEmpty(gameDataDirectory))
            {
                _twoDAService.SetGameDataDirectory(gameDataDirectory);
            }

            try
            {
                UnifiedLogger.LogParser(LogLevel.INFO, $"Scanning creatures in: {UnifiedLogger.SanitizePath(moduleDirectory)}");

                var utcFiles = Directory.GetFiles(moduleDirectory, "*.utc", SearchOption.AllDirectories);
                UnifiedLogger.LogParser(LogLevel.INFO, $"Found {utcFiles.Length} UTC files");

                int successCount = 0;
                int failCount = 0;

                foreach (var utcFile in utcFiles)
                {
                    try
                    {
                        var creature = await _parser.ParseFromFileAsync(utcFile);
                        if (creature != null && !string.IsNullOrEmpty(creature.Tag))
                        {
                            // Populate class names from classes.2da
                            PopulateClassNames(creature);

                            // Cache by Tag (case-insensitive lookup)
                            var key = creature.Tag.ToLowerInvariant();

                            if (!_creatureCache.ContainsKey(key))
                            {
                                _creatureCache[key] = creature;
                                successCount++;
                            }
                            // Skip duplicates silently (normal - modules often have multiple instances)
                        }
                        else
                        {
                            failCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        UnifiedLogger.LogParser(LogLevel.ERROR,
                            $"Error parsing UTC file {Path.GetFileName(utcFile)}: {ex.Message}");
                        failCount++;
                    }
                }

                _cachedDirectory = moduleDirectory;

                UnifiedLogger.LogParser(LogLevel.INFO,
                    $"Creature scan complete: {successCount} loaded, {failCount} failed");

                return _creatureCache.Values.OrderBy(c => c.Tag).ToList();
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogParser(LogLevel.ERROR, $"Error scanning creature directory: {ex.Message}");
                return new List<CreatureInfo>();
            }
        }

        /// <summary>
        /// Get creature by Tag (case-insensitive).
        /// Returns null if not found.
        /// </summary>
        public CreatureInfo? GetCreatureByTag(string tag)
        {
            if (string.IsNullOrEmpty(tag))
                return null;

            var key = tag.ToLowerInvariant();
            return _creatureCache.TryGetValue(key, out var creature) ? creature : null;
        }

        /// <summary>
        /// Get all cached creatures sorted by Tag.
        /// </summary>
        public List<CreatureInfo> GetAllCreatures()
        {
            return _creatureCache.Values.OrderBy(c => c.Tag).ToList();
        }

        /// <summary>
        /// Clear creature cache (e.g., when switching modules).
        /// </summary>
        public void ClearCache()
        {
            _creatureCache.Clear();
            _cachedDirectory = null;
            UnifiedLogger.LogParser(LogLevel.DEBUG, "Creature cache cleared");
        }

        /// <summary>
        /// Check if cache is populated.
        /// </summary>
        public bool HasCachedCreatures => _creatureCache.Count > 0;

        /// <summary>
        /// Get count of cached creatures.
        /// </summary>
        public int CachedCreatureCount => _creatureCache.Count;

        /// <summary>
        /// Populate class names for a creature from classes.2da.
        /// Falls back to "Class{id}" if name not found.
        /// </summary>
        private void PopulateClassNames(CreatureInfo creature)
        {
            foreach (var creatureClass in creature.Classes)
            {
                var className = _twoDAService.GetClassName(creatureClass.ClassId);
                creatureClass.ClassName = className; // Will be null if not found (DisplayText handles fallback)
            }
        }

        /// <summary>
        /// Set game data directory for classes.2da lookup.
        /// Call before ScanCreaturesAsync to enable class name resolution.
        /// </summary>
        public void SetGameDataDirectory(string directory)
        {
            _twoDAService.SetGameDataDirectory(directory);
        }

        /// <summary>
        /// Check if class names are available from classes.2da.
        /// </summary>
        public bool HasClassData => _twoDAService.HasClassData;
    }
}
