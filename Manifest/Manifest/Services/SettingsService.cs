using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Runtime.CompilerServices;
using Radoub.Formats.Settings;

namespace Manifest.Services
{
    /// <summary>
    /// Settings service for Manifest.
    /// Stores tool-specific settings in ~/Radoub/Manifest/ManifestSettings.json
    /// Game paths and TLK settings are in shared RadoubSettings.
    /// </summary>
    public class SettingsService : INotifyPropertyChanged
    {
        public static SettingsService Instance { get; } = new SettingsService();

        /// <summary>
        /// Shared settings for game paths and TLK configuration.
        /// </summary>
        public static RadoubSettings SharedSettings => RadoubSettings.Instance;

        // Lazy initialization to avoid static field initialization timing issues
        private static string? _settingsDirectory;
        private static string SettingsDirectory
        {
            get
            {
                if (_settingsDirectory == null)
                {
                    var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    _settingsDirectory = Path.Combine(userProfile, "Radoub", "Manifest");
                }
                return _settingsDirectory;
            }
        }

        private static string SettingsFilePath => Path.Combine(SettingsDirectory, "ManifestSettings.json");

        // Window settings
        private double _windowLeft = 100;
        private double _windowTop = 100;
        private double _windowWidth = 1000;
        private double _windowHeight = 700;
        private bool _windowMaximized = false;
        private double _treePanelWidth = 300;

        // UI settings
        private double _fontSize = 14;
        private string _fontFamily = "";  // Empty = use theme default
        private string _currentThemeId = "org.manifest.theme.light";

        // Logging settings
        private int _logRetentionSessions = 3;
        private LogLevel _logLevel = LogLevel.INFO;

        // Spell-check settings
        private bool _spellCheckEnabled = true;

        public event PropertyChangedEventHandler? PropertyChanged;

        private SettingsService()
        {
            LoadSettings();
            UnifiedLogger.LogApplication(LogLevel.INFO, "Manifest SettingsService initialized");
        }

        // Window properties
        public double WindowLeft
        {
            get => _windowLeft;
            set { if (SetProperty(ref _windowLeft, value)) SaveSettings(); }
        }

        public double WindowTop
        {
            get => _windowTop;
            set { if (SetProperty(ref _windowTop, value)) SaveSettings(); }
        }

        public double WindowWidth
        {
            get => _windowWidth;
            set { if (SetProperty(ref _windowWidth, Math.Max(400, value))) SaveSettings(); }
        }

        public double WindowHeight
        {
            get => _windowHeight;
            set { if (SetProperty(ref _windowHeight, Math.Max(300, value))) SaveSettings(); }
        }

        public bool WindowMaximized
        {
            get => _windowMaximized;
            set { if (SetProperty(ref _windowMaximized, value)) SaveSettings(); }
        }

        public double TreePanelWidth
        {
            get => _treePanelWidth;
            set { if (SetProperty(ref _treePanelWidth, Math.Max(150, Math.Min(600, value)))) SaveSettings(); }
        }

        // UI properties
        public double FontSize
        {
            get => _fontSize;
            set { if (SetProperty(ref _fontSize, Math.Max(8, Math.Min(24, value)))) SaveSettings(); }
        }

        public string FontFamily
        {
            get => _fontFamily;
            set { if (SetProperty(ref _fontFamily, value ?? "")) SaveSettings(); }
        }

        public string CurrentThemeId
        {
            get => _currentThemeId;
            set { if (SetProperty(ref _currentThemeId, value ?? "org.manifest.theme.light")) SaveSettings(); }
        }

        // Logging Settings Properties
        public int LogRetentionSessions
        {
            get => _logRetentionSessions;
            set
            {
                if (SetProperty(ref _logRetentionSessions, Math.Max(1, Math.Min(10, value))))
                {
                    SaveSettings();
                    UnifiedLogger.LogSettings(LogLevel.INFO, $"Log retention set to {value} sessions");
                }
            }
        }

        public LogLevel CurrentLogLevel
        {
            get => _logLevel;
            set
            {
                if (SetProperty(ref _logLevel, value))
                {
                    UnifiedLogger.SetLogLevel(value);
                    SaveSettings();
                }
            }
        }

        // Spell-check Settings
        public bool SpellCheckEnabled
        {
            get => _spellCheckEnabled;
            set
            {
                if (SetProperty(ref _spellCheckEnabled, value))
                {
                    SaveSettings();
                    UnifiedLogger.LogApplication(LogLevel.INFO, $"Spell-check {(value ? "enabled" : "disabled")}");
                }
            }
        }

        private void LoadSettings()
        {
            try
            {
                // Ensure settings directory exists
                if (!Directory.Exists(SettingsDirectory))
                {
                    UnifiedLogger.LogApplication(LogLevel.INFO, $"Creating settings directory: {UnifiedLogger.SanitizePath(SettingsDirectory)}");
                    Directory.CreateDirectory(SettingsDirectory);
                }

                if (File.Exists(SettingsFilePath))
                {
                    var json = File.ReadAllText(SettingsFilePath);
                    var settings = JsonSerializer.Deserialize<SettingsData>(json);

                    if (settings != null)
                    {
                        // Load window settings
                        _windowLeft = settings.WindowLeft;
                        _windowTop = settings.WindowTop;
                        _windowWidth = Math.Max(400, settings.WindowWidth);
                        _windowHeight = Math.Max(300, settings.WindowHeight);
                        _windowMaximized = settings.WindowMaximized;
                        _treePanelWidth = Math.Max(150, Math.Min(600, settings.TreePanelWidth));

                        // Load UI settings
                        _fontSize = Math.Max(8, Math.Min(24, settings.FontSize));
                        _fontFamily = settings.FontFamily ?? "";
                        _currentThemeId = !string.IsNullOrEmpty(settings.CurrentThemeId)
                            ? settings.CurrentThemeId
                            : "org.manifest.theme.light";

                        // Load logging settings
                        _logRetentionSessions = Math.Max(1, Math.Min(10, settings.LogRetentionSessions));
                        _logLevel = settings.LogLevel;

                        // Load spell-check settings
                        _spellCheckEnabled = settings.SpellCheckEnabled;

                        UnifiedLogger.LogApplication(LogLevel.INFO, "Loaded settings from file");
                    }
                }
                else
                {
                    UnifiedLogger.LogApplication(LogLevel.INFO, "Settings file does not exist, using defaults");
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error loading settings: {ex.Message}");
            }
        }

        private void SaveSettings()
        {
            try
            {
                var settings = new SettingsData
                {
                    WindowLeft = WindowLeft,
                    WindowTop = WindowTop,
                    WindowWidth = WindowWidth,
                    WindowHeight = WindowHeight,
                    WindowMaximized = WindowMaximized,
                    TreePanelWidth = TreePanelWidth,
                    FontSize = FontSize,
                    FontFamily = FontFamily,
                    CurrentThemeId = CurrentThemeId,
                    LogRetentionSessions = LogRetentionSessions,
                    LogLevel = CurrentLogLevel,
                    SpellCheckEnabled = SpellCheckEnabled
                };

                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(SettingsFilePath, json);
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Settings saved to {UnifiedLogger.SanitizePath(SettingsFilePath)}");
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error saving settings: {ex.Message}");
            }
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        private class SettingsData
        {
            // Window settings
            public double WindowLeft { get; set; } = 100;
            public double WindowTop { get; set; } = 100;
            public double WindowWidth { get; set; } = 1000;
            public double WindowHeight { get; set; } = 700;
            public bool WindowMaximized { get; set; } = false;
            public double TreePanelWidth { get; set; } = 300;

            // UI settings
            public double FontSize { get; set; } = 14;
            public string FontFamily { get; set; } = "";
            public string CurrentThemeId { get; set; } = "org.manifest.theme.light";

            // Logging settings
            public int LogRetentionSessions { get; set; } = 3;
            public LogLevel LogLevel { get; set; } = LogLevel.INFO;

            // Spell-check settings
            public bool SpellCheckEnabled { get; set; } = true;
        }
    }
}
