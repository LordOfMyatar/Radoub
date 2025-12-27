using System;
using Radoub.Formats.Logging;
using System.Collections.Generic;
using System.IO;
using DialogEditor.Parsers;

namespace DialogEditor.Services
{
    /// <summary>
    /// Service for loading and caching 2DA files (game data tables).
    /// Provides lookups for classes, portraits, etc.
    /// </summary>
    public class TwoDAService
    {
        private readonly TwoDAParser _parser = new();
        private Dictionary<int, string>? _classNames;
        private string? _gameDataDirectory;

        /// <summary>
        /// Set the game data directory containing 2DA files.
        /// Typically: [NWN Install]/data or module override directory.
        /// </summary>
        public void SetGameDataDirectory(string directory)
        {
            if (_gameDataDirectory != directory)
            {
                _gameDataDirectory = directory;
                ClearCache();
                UnifiedLogger.LogParser(LogLevel.INFO, $"Game data directory set: {directory}");
            }
        }

        /// <summary>
        /// Get class name from classes.2da by class ID.
        /// Returns null if not found or file not loaded.
        /// </summary>
        public string? GetClassName(int classId)
        {
            LoadClassNamesIfNeeded();

            if (_classNames != null && _classNames.TryGetValue(classId, out var name))
            {
                return name;
            }

            return null;
        }

        /// <summary>
        /// Check if classes.2da is loaded.
        /// </summary>
        public bool HasClassData => _classNames != null && _classNames.Count > 0;

        /// <summary>
        /// Load classes.2da from game data directory (if not already loaded).
        /// </summary>
        private void LoadClassNamesIfNeeded()
        {
            if (_classNames != null)
                return; // Already loaded

            if (string.IsNullOrEmpty(_gameDataDirectory))
            {
                UnifiedLogger.LogParser(LogLevel.DEBUG, "Game data directory not set, cannot load classes.2da");
                return;
            }

            var classesPath = Path.Combine(_gameDataDirectory, "classes.2da");

            if (!File.Exists(classesPath))
            {
                UnifiedLogger.LogParser(LogLevel.WARN, $"classes.2da not found: {classesPath}");
                _classNames = new Dictionary<int, string>(); // Empty cache to avoid repeated attempts
                return;
            }

            try
            {
                UnifiedLogger.LogParser(LogLevel.INFO, $"Loading classes.2da from: {classesPath}");

                // Parse as lookup: classId → "Name" column (or "Label" as fallback)
                _classNames = _parser.ParseAsLookup(classesPath, "Name");

                // If "Name" column doesn't exist, try "Label"
                if (_classNames.Count == 0)
                {
                    _classNames = _parser.ParseAsLookup(classesPath, "Label");
                }

                UnifiedLogger.LogParser(LogLevel.INFO, $"Loaded {_classNames.Count} class names from classes.2da");
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogParser(LogLevel.ERROR, $"Error loading classes.2da: {ex.Message}");
                _classNames = new Dictionary<int, string>(); // Empty cache to avoid repeated attempts
            }
        }

        /// <summary>
        /// Clear all cached 2DA data (e.g., when switching modules).
        /// </summary>
        public void ClearCache()
        {
            _classNames = null;
            UnifiedLogger.LogParser(LogLevel.DEBUG, "2DA cache cleared");
        }

        /// <summary>
        /// Get count of loaded class names.
        /// </summary>
        public int ClassNameCount => _classNames?.Count ?? 0;
    }
}
