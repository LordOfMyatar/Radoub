using System;
using Radoub.Formats.Logging;
using System.Collections.Generic;
using System.IO;
using DialogEditor.Parsers;

namespace DialogEditor.Services
{
    /// <summary>
    /// Service for loading and caching 2DA files (game data tables).
    /// Provides lookups for classes, portraits, soundsets, etc.
    /// </summary>
    public class TwoDAService
    {
        private readonly TwoDAParser _parser = new();
        private Dictionary<int, string>? _classNames;
        private Dictionary<int, SoundsetInfo>? _soundsets;
        private Dictionary<int, string>? _portraits;
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
            _soundsets = null;
            _portraits = null;
            UnifiedLogger.LogParser(LogLevel.DEBUG, "2DA cache cleared");
        }

        /// <summary>
        /// Get count of loaded class names.
        /// </summary>
        public int ClassNameCount => _classNames?.Count ?? 0;

        #region Portrait Support (#915)

        /// <summary>
        /// Get portrait base ResRef from portraits.2da by portrait ID.
        /// Returns null if not found or file not loaded.
        /// </summary>
        public string? GetPortraitResRef(int portraitId)
        {
            if (portraitId <= 0)
                return null;

            LoadPortraitsIfNeeded();

            if (_portraits != null && _portraits.TryGetValue(portraitId, out var resRef))
            {
                return resRef;
            }

            return null;
        }

        /// <summary>
        /// Check if portraits.2da is loaded.
        /// </summary>
        public bool HasPortraitData => _portraits != null && _portraits.Count > 0;

        /// <summary>
        /// Load portraits.2da from game data directory (if not already loaded).
        /// </summary>
        private void LoadPortraitsIfNeeded()
        {
            if (_portraits != null)
                return; // Already loaded

            if (string.IsNullOrEmpty(_gameDataDirectory))
            {
                UnifiedLogger.LogParser(LogLevel.DEBUG, "Game data directory not set, cannot load portraits.2da");
                return;
            }

            var portraitsPath = Path.Combine(_gameDataDirectory, "portraits.2da");

            if (!File.Exists(portraitsPath))
            {
                UnifiedLogger.LogParser(LogLevel.WARN, $"portraits.2da not found: {portraitsPath}");
                _portraits = new Dictionary<int, string>(); // Empty cache to avoid repeated attempts
                return;
            }

            try
            {
                UnifiedLogger.LogParser(LogLevel.INFO, $"Loading portraits.2da from: {portraitsPath}");

                // Parse as lookup: portraitId → "BaseResRef" column
                _portraits = _parser.ParseAsLookup(portraitsPath, "BaseResRef");

                UnifiedLogger.LogParser(LogLevel.INFO, $"Loaded {_portraits.Count} portraits from portraits.2da");
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogParser(LogLevel.ERROR, $"Error loading portraits.2da: {ex.Message}");
                _portraits = new Dictionary<int, string>(); // Empty cache to avoid repeated attempts
            }
        }

        #endregion

        #region Soundset Support (#786)

        /// <summary>
        /// Get soundset info by ID from soundset.2da.
        /// Returns null if not found or file not loaded.
        /// </summary>
        public SoundsetInfo? GetSoundset(int soundsetId)
        {
            // ushort.MaxValue = not set in creature
            if (soundsetId < 0 || soundsetId == ushort.MaxValue)
                return null;

            LoadSoundsetsIfNeeded();

            if (_soundsets != null && _soundsets.TryGetValue(soundsetId, out var info))
            {
                return info;
            }

            return null;
        }

        /// <summary>
        /// Check if soundset.2da is loaded.
        /// </summary>
        public bool HasSoundsetData => _soundsets != null && _soundsets.Count > 0;

        /// <summary>
        /// Load soundset.2da from game data directory (if not already loaded).
        /// </summary>
        private void LoadSoundsetsIfNeeded()
        {
            if (_soundsets != null)
                return; // Already loaded

            if (string.IsNullOrEmpty(_gameDataDirectory))
            {
                UnifiedLogger.LogParser(LogLevel.DEBUG, "Game data directory not set, cannot load soundset.2da");
                return;
            }

            var soundsetPath = Path.Combine(_gameDataDirectory, "soundset.2da");

            if (!File.Exists(soundsetPath))
            {
                UnifiedLogger.LogParser(LogLevel.WARN, $"soundset.2da not found: {soundsetPath}");
                _soundsets = new Dictionary<int, SoundsetInfo>(); // Empty cache to avoid repeated attempts
                return;
            }

            try
            {
                UnifiedLogger.LogParser(LogLevel.INFO, $"Loading soundset.2da from: {soundsetPath}");

                var rows = _parser.ParseFile(soundsetPath);
                _soundsets = new Dictionary<int, SoundsetInfo>();

                foreach (var row in rows)
                {
                    if (!row.TryGetValue("RowIndex", out var indexStr) ||
                        !int.TryParse(indexStr, out var index))
                        continue;

                    var soundset = new SoundsetInfo
                    {
                        Id = index,
                        Label = row.GetValueOrDefault("LABEL", ""),
                        ResRef = row.GetValueOrDefault("RESREF", ""),
                        StrRef = ParseInt(row.GetValueOrDefault("STRREF", "")),
                        Gender = ParseInt(row.GetValueOrDefault("GENDER", "")) == 1 ? "Female" : "Male",
                        Type = ParseInt(row.GetValueOrDefault("TYPE", ""))
                    };

                    _soundsets[index] = soundset;
                }

                UnifiedLogger.LogParser(LogLevel.INFO, $"Loaded {_soundsets.Count} soundsets from soundset.2da");
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogParser(LogLevel.ERROR, $"Error loading soundset.2da: {ex.Message}");
                _soundsets = new Dictionary<int, SoundsetInfo>(); // Empty cache to avoid repeated attempts
            }
        }

        private static int ParseInt(string value)
        {
            if (string.IsNullOrEmpty(value) || value == "****")
                return -1;
            return int.TryParse(value, out var result) ? result : -1;
        }

        #endregion
    }

    /// <summary>
    /// Information about a soundset from soundset.2da.
    /// Used for NPC soundset preview (#786).
    /// </summary>
    public class SoundsetInfo
    {
        /// <summary>Row index in soundset.2da</summary>
        public int Id { get; set; }

        /// <summary>Text label (e.g., "vs_nwarlord_m")</summary>
        public string Label { get; set; } = "";

        /// <summary>SSF file ResRef (16-char max)</summary>
        public string ResRef { get; set; } = "";

        /// <summary>StrRef for localized name in dialog.tlk (-1 if none)</summary>
        public int StrRef { get; set; } = -1;

        /// <summary>Gender: "Male" or "Female"</summary>
        public string Gender { get; set; } = "Male";

        /// <summary>Type index into soundsettype.2da</summary>
        public int Type { get; set; } = -1;

        /// <summary>
        /// Display name for UI. Uses Label if StrRef lookup not available.
        /// </summary>
        public string DisplayName => !string.IsNullOrEmpty(Label) ? Label : $"Soundset {Id}";
    }
}
