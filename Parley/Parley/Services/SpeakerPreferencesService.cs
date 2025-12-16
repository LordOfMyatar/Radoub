using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using DialogEditor.Utils;

namespace DialogEditor.Services
{
    /// <summary>
    /// Manages NPC speaker visual preferences (colors and shapes) in a dedicated config file.
    /// Separating from main settings keeps ParleySettings.json clean and allows easy
    /// sharing/backup of speaker customizations.
    /// Issue #179
    /// </summary>
    public class SpeakerPreferencesService : INotifyPropertyChanged
    {
        public static SpeakerPreferencesService Instance { get; } = new SpeakerPreferencesService();

        private static string? _settingsDirectory;
        private static string SettingsDirectory
        {
            get
            {
                if (_settingsDirectory == null)
                {
                    var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    _settingsDirectory = Path.Combine(userProfile, "Parley");
                }
                return _settingsDirectory;
            }
        }

        private static string PreferencesFilePath => Path.Combine(SettingsDirectory, "SpeakerPreferences.json");

        private Dictionary<string, SpeakerPreferences> _preferences = new Dictionary<string, SpeakerPreferences>();
        private bool _isLoaded = false;

        public event PropertyChangedEventHandler? PropertyChanged;

        private SpeakerPreferencesService()
        {
            // Don't load in constructor - wait for explicit Load() or first access
            // This allows migration to happen first
        }

        /// <summary>
        /// Gets all speaker preferences. Loads from file if not already loaded.
        /// </summary>
        public Dictionary<string, SpeakerPreferences> Preferences
        {
            get
            {
                EnsureLoaded();
                return _preferences;
            }
        }

        /// <summary>
        /// Sets visual preferences for a speaker tag.
        /// </summary>
        public void SetPreference(string speakerTag, string? color, SpeakerVisualHelper.SpeakerShape? shape)
        {
            if (string.IsNullOrEmpty(speakerTag))
                return;

            EnsureLoaded();

            if (!_preferences.ContainsKey(speakerTag))
            {
                _preferences[speakerTag] = new SpeakerPreferences();
            }

            if (color != null)
                _preferences[speakerTag].Color = color;

            if (shape != null)
                _preferences[speakerTag].Shape = shape.ToString();

            Save();
            OnPropertyChanged(nameof(Preferences));
        }

        /// <summary>
        /// Gets visual preferences for a speaker tag.
        /// </summary>
        public (string? color, SpeakerVisualHelper.SpeakerShape? shape) GetPreference(string speakerTag)
        {
            EnsureLoaded();

            if (string.IsNullOrEmpty(speakerTag) || !_preferences.ContainsKey(speakerTag))
                return (null, null);

            var prefs = _preferences[speakerTag];
            SpeakerVisualHelper.SpeakerShape? shape = null;
            if (Enum.TryParse<SpeakerVisualHelper.SpeakerShape>(prefs.Shape, out var parsedShape))
            {
                shape = parsedShape;
            }
            return (prefs.Color, shape);
        }

        /// <summary>
        /// Removes preferences for a speaker tag.
        /// </summary>
        public void RemovePreference(string speakerTag)
        {
            if (string.IsNullOrEmpty(speakerTag))
                return;

            EnsureLoaded();

            if (_preferences.Remove(speakerTag))
            {
                Save();
                OnPropertyChanged(nameof(Preferences));
                UnifiedLogger.LogSettings(LogLevel.DEBUG, $"Removed speaker preference for '{speakerTag}'");
            }
        }

        /// <summary>
        /// Clears all speaker preferences.
        /// </summary>
        public void ClearAllPreferences()
        {
            EnsureLoaded();

            if (_preferences.Count > 0)
            {
                _preferences.Clear();
                Save();
                OnPropertyChanged(nameof(Preferences));
                UnifiedLogger.LogSettings(LogLevel.INFO, "Cleared all speaker preferences");
            }
        }

        /// <summary>
        /// Gets the count of stored speaker preferences.
        /// </summary>
        public int Count
        {
            get
            {
                EnsureLoaded();
                return _preferences.Count;
            }
        }

        /// <summary>
        /// Migrates speaker preferences from old SettingsData structure.
        /// Called by SettingsService during upgrade from old format.
        /// </summary>
        public void MigrateFromSettingsData(Dictionary<string, SpeakerPreferences>? oldPreferences)
        {
            if (oldPreferences == null || oldPreferences.Count == 0)
                return;

            // Only migrate if our file doesn't exist yet (first-time migration)
            if (File.Exists(PreferencesFilePath))
            {
                UnifiedLogger.LogSettings(LogLevel.DEBUG,
                    "SpeakerPreferences.json already exists, skipping migration");
                return;
            }

            _preferences = new Dictionary<string, SpeakerPreferences>(oldPreferences);
            _isLoaded = true;
            Save();

            UnifiedLogger.LogSettings(LogLevel.INFO,
                $"Migrated {oldPreferences.Count} speaker preferences from ParleySettings.json");
        }

        private void EnsureLoaded()
        {
            if (!_isLoaded)
            {
                Load();
            }
        }

        private void Load()
        {
            try
            {
                if (!Directory.Exists(SettingsDirectory))
                {
                    Directory.CreateDirectory(SettingsDirectory);
                }

                if (File.Exists(PreferencesFilePath))
                {
                    var json = File.ReadAllText(PreferencesFilePath);
                    var data = JsonSerializer.Deserialize<SpeakerPreferencesData>(json);

                    if (data?.Preferences != null)
                    {
                        _preferences = data.Preferences;
                        UnifiedLogger.LogSettings(LogLevel.INFO,
                            $"Loaded {_preferences.Count} speaker preferences from {UnifiedLogger.SanitizePath(PreferencesFilePath)}");
                    }
                }
                else
                {
                    UnifiedLogger.LogSettings(LogLevel.DEBUG,
                        "SpeakerPreferences.json does not exist, starting with empty preferences");
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogSettings(LogLevel.ERROR,
                    $"Error loading speaker preferences: {ex.Message}");
                _preferences = new Dictionary<string, SpeakerPreferences>();
            }

            _isLoaded = true;
        }

        private void Save()
        {
            try
            {
                if (!Directory.Exists(SettingsDirectory))
                {
                    Directory.CreateDirectory(SettingsDirectory);
                }

                var data = new SpeakerPreferencesData
                {
                    Version = 1,
                    Preferences = _preferences
                };

                var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(PreferencesFilePath, json);
                UnifiedLogger.LogSettings(LogLevel.DEBUG,
                    $"Saved {_preferences.Count} speaker preferences to {UnifiedLogger.SanitizePath(PreferencesFilePath)}");
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogSettings(LogLevel.ERROR,
                    $"Error saving speaker preferences: {ex.Message}");
            }
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Data structure for JSON serialization.
        /// </summary>
        private class SpeakerPreferencesData
        {
            public int Version { get; set; } = 1;
            public Dictionary<string, SpeakerPreferences> Preferences { get; set; }
                = new Dictionary<string, SpeakerPreferences>();
        }
    }
}
