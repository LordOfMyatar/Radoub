using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using DialogEditor.Utils;

namespace DialogEditor.Services
{
    /// <summary>
    /// Manages plugin-specific settings separate from application settings
    /// </summary>
    public class PluginSettingsService
    {
        private static PluginSettingsService? _instance;
        public static PluginSettingsService Instance => _instance ??= new PluginSettingsService();

        private readonly string _settingsPath;
        private PluginSettingsData _settings = new();

        private PluginSettingsService()
        {
            var userDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Parley"
            );
            Directory.CreateDirectory(userDataDir);
            _settingsPath = Path.Combine(userDataDir, "PluginSettings.json");

            LoadSettings();
            MigrateFromParleySettings();
        }

        private void LoadSettings()
        {
            if (File.Exists(_settingsPath))
            {
                try
                {
                    var json = File.ReadAllText(_settingsPath);
                    _settings = JsonSerializer.Deserialize<PluginSettingsData>(json) ?? new();
                }
                catch (Exception ex)
                {
                    UnifiedLogger.LogPlugin(LogLevel.ERROR, $"Failed to load plugin settings: {ex.Message}");
                    _settings = new();
                }
            }
        }

        private void SaveSettings()
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(_settings, options);
                File.WriteAllText(_settingsPath, json);
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogPlugin(LogLevel.ERROR, $"Failed to save plugin settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Migrate plugin settings from ParleySettings.json (one-time migration)
        /// </summary>
        private void MigrateFromParleySettings()
        {
            // Check if we've already migrated
            if (_settings.MigrationCompleted)
                return;

            try
            {
                // Read plugin settings directly from ParleySettings.json
                var parleySettingsPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Parley",
                    "ParleySettings.json"
                );

                if (File.Exists(parleySettingsPath))
                {
                    var json = File.ReadAllText(parleySettingsPath);
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    // Migrate plugin lists
                    if (root.TryGetProperty("EnabledPlugins", out var enabledPlugins))
                    {
                        _settings.EnabledPlugins = JsonSerializer.Deserialize<List<string>>(enabledPlugins.GetRawText())
                            ?? new List<string>();
                    }

                    if (root.TryGetProperty("DisabledPlugins", out var disabledPlugins))
                    {
                        _settings.DisabledPlugins = JsonSerializer.Deserialize<List<string>>(disabledPlugins.GetRawText())
                            ?? new List<string>();
                    }

                    // Migrate crash tracking
                    if (root.TryGetProperty("PluginSafeMode", out var safeMode))
                    {
                        _settings.SafeMode = safeMode.GetBoolean();
                    }

                    if (root.TryGetProperty("LastSessionCrashed", out var lastCrashed))
                    {
                        _settings.LastSessionCrashed = lastCrashed.GetBoolean();
                    }

                    if (root.TryGetProperty("PluginsLoadedDuringCrash", out var loadedPlugins))
                    {
                        _settings.PluginsLoadedDuringCrash = JsonSerializer.Deserialize<List<string>>(loadedPlugins.GetRawText())
                            ?? new List<string>();
                    }

                    if (root.TryGetProperty("PluginCrashHistory", out var crashHistory))
                    {
                        _settings.CrashHistory = JsonSerializer.Deserialize<Dictionary<string, PluginCrashInfo>>(crashHistory.GetRawText())
                            ?? new Dictionary<string, PluginCrashInfo>();
                    }

                    UnifiedLogger.LogPlugin(LogLevel.INFO, "Migrated plugin settings from ParleySettings.json");
                }

                _settings.MigrationCompleted = true;
                SaveSettings();
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogPlugin(LogLevel.ERROR, $"Failed to migrate plugin settings: {ex.Message}");
            }
        }

        // Plugin enable/disable
        public bool IsPluginEnabled(string pluginId)
        {
            return _settings.EnabledPlugins.Contains(pluginId);
        }

        public void SetPluginEnabled(string pluginId, bool enabled)
        {
            if (enabled)
            {
                if (!_settings.EnabledPlugins.Contains(pluginId))
                    _settings.EnabledPlugins.Add(pluginId);
                _settings.DisabledPlugins.Remove(pluginId);
            }
            else
            {
                _settings.EnabledPlugins.Remove(pluginId);
                if (!_settings.DisabledPlugins.Contains(pluginId))
                    _settings.DisabledPlugins.Add(pluginId);
            }
            SaveSettings();
        }

        public List<string> EnabledPlugins => new(_settings.EnabledPlugins);
        public List<string> DisabledPlugins => new(_settings.DisabledPlugins);

        // Safe mode
        public bool SafeMode
        {
            get => _settings.SafeMode;
            set
            {
                _settings.SafeMode = value;
                SaveSettings();
            }
        }

        // Session crash tracking
        public void SetSessionStarted()
        {
            _settings.LastSessionCrashed = false;
            _settings.PluginsLoadedDuringCrash.Clear();
            SaveSettings();
        }

        public void SetSessionEnded()
        {
            _settings.LastSessionCrashed = false;
            SaveSettings();
        }

        public bool LastSessionCrashed => _settings.LastSessionCrashed;
        public List<string> PluginsLoadedDuringCrash => new(_settings.PluginsLoadedDuringCrash);

        // Crash tracking
        public void RecordPluginCrash(string pluginId)
        {
            if (!_settings.CrashHistory.ContainsKey(pluginId))
            {
                _settings.CrashHistory[pluginId] = new PluginCrashInfo
                {
                    CrashCount = 0,
                    LastCrash = DateTime.MinValue
                };
            }

            var crashInfo = _settings.CrashHistory[pluginId];
            crashInfo.CrashCount++;
            crashInfo.LastCrash = DateTime.Now;

            UnifiedLogger.LogPlugin(LogLevel.WARN,
                $"Plugin {pluginId} crashed (total crashes: {crashInfo.CrashCount})");

            SaveSettings();

            // Auto-disable after 3 crashes
            if (crashInfo.CrashCount >= 3)
            {
                SetPluginEnabled(pluginId, false);
                UnifiedLogger.LogPlugin(LogLevel.ERROR,
                    $"Plugin {pluginId} auto-disabled after {crashInfo.CrashCount} crashes");
            }
        }

        public Dictionary<string, PluginCrashInfo> CrashHistory => new(_settings.CrashHistory);
    }

    /// <summary>
    /// Plugin settings data structure
    /// </summary>
    public class PluginSettingsData
    {
        public List<string> EnabledPlugins { get; set; } = new();
        public List<string> DisabledPlugins { get; set; } = new();
        public bool SafeMode { get; set; } = false;
        public bool LastSessionCrashed { get; set; } = false;
        public List<string> PluginsLoadedDuringCrash { get; set; } = new();
        public Dictionary<string, PluginCrashInfo> CrashHistory { get; set; } = new();
        public bool MigrationCompleted { get; set; } = false;
    }

    /// <summary>
    /// Tracks crash information for a single plugin
    /// </summary>
    public class PluginCrashInfo
    {
        public int CrashCount { get; set; }
        public DateTime LastCrash { get; set; }
    }
}
