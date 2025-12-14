using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Runtime.CompilerServices;

namespace Manifest.Services
{
    /// <summary>
    /// Settings service for Manifest.
    /// Stores settings in ~/Radoub/Manifest/ManifestSettings.json
    /// Adapted from Parley's SettingsService pattern.
    /// </summary>
    public class SettingsService : INotifyPropertyChanged
    {
        public static SettingsService Instance { get; } = new SettingsService();

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
        private const int DefaultMaxRecentFiles = 10;

        // Recent files
        private List<string> _recentFiles = new List<string>();
        private int _maxRecentFiles = DefaultMaxRecentFiles;

        // Window settings
        private double _windowLeft = 100;
        private double _windowTop = 100;
        private double _windowWidth = 1000;
        private double _windowHeight = 700;
        private bool _windowMaximized = false;

        // UI settings
        private double _fontSize = 14;

        // Logging settings
        private int _logRetentionSessions = 3;
        private LogLevel _logLevel = LogLevel.INFO;

        public event PropertyChangedEventHandler? PropertyChanged;

        private SettingsService()
        {
            LoadSettings();
            UnifiedLogger.LogApplication(LogLevel.INFO, "Manifest SettingsService initialized");
        }

        public List<string> RecentFiles
        {
            get => _recentFiles.ToList(); // Return a copy to prevent external modification
        }

        public int MaxRecentFiles
        {
            get => _maxRecentFiles;
            set
            {
                if (SetProperty(ref _maxRecentFiles, Math.Max(1, Math.Min(20, value))))
                {
                    TrimRecentFiles();
                    SaveSettings();
                }
            }
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

        // UI properties
        public double FontSize
        {
            get => _fontSize;
            set { if (SetProperty(ref _fontSize, Math.Max(8, Math.Min(24, value)))) SaveSettings(); }
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
                        _recentFiles = ExpandPaths(settings.RecentFiles?.ToList() ?? new List<string>());
                        _maxRecentFiles = Math.Max(1, Math.Min(20, settings.MaxRecentFiles));

                        // Load window settings
                        _windowLeft = settings.WindowLeft;
                        _windowTop = settings.WindowTop;
                        _windowWidth = Math.Max(400, settings.WindowWidth);
                        _windowHeight = Math.Max(300, settings.WindowHeight);
                        _windowMaximized = settings.WindowMaximized;

                        // Load UI settings
                        _fontSize = Math.Max(8, Math.Min(24, settings.FontSize));

                        // Load logging settings
                        _logRetentionSessions = Math.Max(1, Math.Min(10, settings.LogRetentionSessions));
                        _logLevel = settings.LogLevel;

                        UnifiedLogger.LogApplication(LogLevel.INFO, $"Loaded settings: {_recentFiles.Count} recent files, max={_maxRecentFiles}");
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
                    RecentFiles = ContractPaths(_recentFiles),
                    MaxRecentFiles = MaxRecentFiles,
                    WindowLeft = WindowLeft,
                    WindowTop = WindowTop,
                    WindowWidth = WindowWidth,
                    WindowHeight = WindowHeight,
                    WindowMaximized = WindowMaximized,
                    FontSize = FontSize,
                    LogRetentionSessions = LogRetentionSessions,
                    LogLevel = CurrentLogLevel
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

        public void AddRecentFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return;

            // Remove if already exists (to move to top)
            _recentFiles.Remove(filePath);

            // Add to beginning
            _recentFiles.Insert(0, filePath);

            // Trim to max allowed
            TrimRecentFiles();

            OnPropertyChanged(nameof(RecentFiles));
            SaveSettings();

            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Added recent file: {Path.GetFileName(filePath)}");
        }

        public void RemoveRecentFile(string filePath)
        {
            if (_recentFiles.Remove(filePath))
            {
                OnPropertyChanged(nameof(RecentFiles));
                SaveSettings();
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Removed recent file: {Path.GetFileName(filePath)}");
            }
        }

        public void ClearRecentFiles()
        {
            if (_recentFiles.Count > 0)
            {
                _recentFiles.Clear();
                OnPropertyChanged(nameof(RecentFiles));
                SaveSettings();
                UnifiedLogger.LogApplication(LogLevel.INFO, "Cleared all recent files");
            }
        }

        public void CleanupRecentFiles()
        {
            var originalCount = _recentFiles.Count;
            _recentFiles.RemoveAll(file => !File.Exists(file));

            if (_recentFiles.Count != originalCount)
            {
                OnPropertyChanged(nameof(RecentFiles));
                SaveSettings();
                UnifiedLogger.LogApplication(LogLevel.INFO, $"Cleaned up {originalCount - _recentFiles.Count} non-existent recent files");
            }
        }

        private void TrimRecentFiles()
        {
            while (_recentFiles.Count > MaxRecentFiles)
            {
                _recentFiles.RemoveAt(_recentFiles.Count - 1);
            }
        }

        /// <summary>
        /// Contracts a path for storage - replaces user home directory with ~
        /// </summary>
        private static string ContractPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(userProfile) && path.StartsWith(userProfile, StringComparison.OrdinalIgnoreCase))
            {
                return "~" + path.Substring(userProfile.Length);
            }

            return path;
        }

        /// <summary>
        /// Expands a path from storage - replaces ~ with user home directory
        /// </summary>
        private static string ExpandPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            if (path.StartsWith("~"))
            {
                var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                return userProfile + path.Substring(1);
            }

            return path;
        }

        private static List<string> ContractPaths(List<string> paths)
        {
            return paths.Select(ContractPath).ToList();
        }

        private static List<string> ExpandPaths(List<string> paths)
        {
            return paths.Select(ExpandPath).ToList();
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
            public List<string> RecentFiles { get; set; } = new List<string>();
            public int MaxRecentFiles { get; set; } = DefaultMaxRecentFiles;

            // Window settings
            public double WindowLeft { get; set; } = 100;
            public double WindowTop { get; set; } = 100;
            public double WindowWidth { get; set; } = 1000;
            public double WindowHeight { get; set; } = 700;
            public bool WindowMaximized { get; set; } = false;

            // UI settings
            public double FontSize { get; set; } = 14;

            // Logging settings
            public int LogRetentionSessions { get; set; } = 3;
            public LogLevel LogLevel { get; set; } = LogLevel.INFO;
        }
    }
}
