using System;
using Radoub.Formats.Logging;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Runtime.CompilerServices;
using Radoub.Formats.Settings;
using Radoub.Formats.Common;

namespace Quartermaster.Services;

/// <summary>
/// Settings service for Quartermaster.
/// Stores tool-specific settings in ~/Radoub/Quartermaster/QuartermasterSettings.json
/// Game paths and TLK settings are in shared RadoubSettings.
/// </summary>
public class SettingsService : INotifyPropertyChanged
{
    private static SettingsService? _instance;
    private static readonly object _lock = new();

    public static SettingsService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new SettingsService();
                }
            }
            return _instance;
        }
    }

    /// <summary>
    /// Configure test mode with isolated settings directory.
    /// MUST be called before first access to Instance.
    /// </summary>
    /// <param name="testDirectory">Temp directory for test settings</param>
    public static void ConfigureForTesting(string testDirectory)
    {
        lock (_lock)
        {
            if (_instance != null)
                throw new InvalidOperationException("ConfigureForTesting must be called before first Instance access");
            _settingsDirectory = testDirectory;
        }
    }

    /// <summary>
    /// Reset for testing - allows re-initialization with different settings.
    /// Only for use in test teardown.
    /// </summary>
    public static void ResetForTesting()
    {
        lock (_lock)
        {
            _instance = null;
            _settingsDirectory = null;
        }
    }

    /// <summary>
    /// Shared settings for game paths and TLK configuration.
    /// </summary>
    public static RadoubSettings SharedSettings => RadoubSettings.Instance;

    private static string? _settingsDirectory;
    private static string SettingsDirectory
    {
        get
        {
            if (_settingsDirectory == null)
            {
                var envDir = Environment.GetEnvironmentVariable("QUARTERMASTER_SETTINGS_DIR");
                if (!string.IsNullOrEmpty(envDir))
                {
                    _settingsDirectory = envDir;
                }
                else
                {
                    var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    _settingsDirectory = Path.Combine(userProfile, "Radoub", "Quartermaster");
                }
            }
            return _settingsDirectory;
        }
    }

    private static string SettingsFilePath => Path.Combine(SettingsDirectory, "QuartermasterSettings.json");

    // Window settings
    private double _windowLeft = 100;
    private double _windowTop = 100;
    private double _windowWidth = 1200;
    private double _windowHeight = 800;
    private bool _windowMaximized = false;
    private double _leftPanelWidth = 350;
    private double _rightPanelWidth = 400;
    private double _sidebarWidth = 200;

    // Creature browser panel settings (#1145)
    private double _creatureBrowserPanelWidth = 200;
    private bool _creatureBrowserPanelVisible = true;

    // UI settings
    private double _fontSize = 14;
    private string _fontFamily = "";
    private string _currentThemeId = "org.quartermaster.theme.light";

    // Logging settings - using shared LoggingSettings
    private readonly LoggingSettings _loggingSettings = new();

    // Level history settings
    private LevelHistoryEncoding _levelHistoryEncoding = LevelHistoryEncoding.Readable;
    private bool _recordLevelHistory = true;

    // Recent files
    private const int DefaultMaxRecentFiles = 10;
    private List<string> _recentFiles = new();
    private int _maxRecentFiles = DefaultMaxRecentFiles;

    public event PropertyChangedEventHandler? PropertyChanged;

    private SettingsService()
    {
        LoadSettings();
        UnifiedLogger.LogApplication(LogLevel.INFO, "Quartermaster SettingsService initialized");
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
        set { if (SetProperty(ref _windowWidth, Math.Max(600, value))) SaveSettings(); }
    }

    public double WindowHeight
    {
        get => _windowHeight;
        set { if (SetProperty(ref _windowHeight, Math.Max(400, value))) SaveSettings(); }
    }

    public bool WindowMaximized
    {
        get => _windowMaximized;
        set { if (SetProperty(ref _windowMaximized, value)) SaveSettings(); }
    }

    public double LeftPanelWidth
    {
        get => _leftPanelWidth;
        set { if (SetProperty(ref _leftPanelWidth, Math.Max(200, Math.Min(600, value)))) SaveSettings(); }
    }

    public double RightPanelWidth
    {
        get => _rightPanelWidth;
        set { if (SetProperty(ref _rightPanelWidth, Math.Max(250, Math.Min(800, value)))) SaveSettings(); }
    }

    public double SidebarWidth
    {
        get => _sidebarWidth;
        set { if (SetProperty(ref _sidebarWidth, Math.Max(150, Math.Min(300, value)))) SaveSettings(); }
    }

    // Creature browser panel properties (#1145)
    public double CreatureBrowserPanelWidth
    {
        get => _creatureBrowserPanelWidth;
        set { if (SetProperty(ref _creatureBrowserPanelWidth, Math.Max(150, Math.Min(400, value)))) SaveSettings(); }
    }

    public bool CreatureBrowserPanelVisible
    {
        get => _creatureBrowserPanelVisible;
        set { if (SetProperty(ref _creatureBrowserPanelVisible, value)) SaveSettings(); }
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
        set { if (SetProperty(ref _currentThemeId, value ?? "org.quartermaster.theme.light")) SaveSettings(); }
    }

    // Logging properties - delegate to shared LoggingSettings
    public int LogRetentionSessions
    {
        get => _loggingSettings.LogRetentionSessions;
        set
        {
            var clamped = Math.Max(1, Math.Min(10, value));
            if (_loggingSettings.LogRetentionSessions != clamped)
            {
                _loggingSettings.LogRetentionSessions = clamped;
                OnPropertyChanged();
                SaveSettings();
                UnifiedLogger.LogSettings(LogLevel.INFO, $"Log retention set to {clamped} sessions");
            }
        }
    }

    public LogLevel CurrentLogLevel
    {
        get => _loggingSettings.LogLevel;
        set
        {
            if (_loggingSettings.LogLevel != value)
            {
                _loggingSettings.LogLevel = value;
                _loggingSettings.ApplyToLogger();
                OnPropertyChanged();
                SaveSettings();
            }
        }
    }

    // Level history properties
    public LevelHistoryEncoding LevelHistoryEncoding
    {
        get => _levelHistoryEncoding;
        set { if (SetProperty(ref _levelHistoryEncoding, value)) SaveSettings(); }
    }

    public bool RecordLevelHistory
    {
        get => _recordLevelHistory;
        set { if (SetProperty(ref _recordLevelHistory, value)) SaveSettings(); }
    }

    // Recent Files
    public List<string> RecentFiles => _recentFiles.ToList();

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

    public void AddRecentFile(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            return;

        _recentFiles.Remove(filePath);
        _recentFiles.Insert(0, filePath);
        TrimRecentFiles();
        OnPropertyChanged(nameof(RecentFiles));
        SaveSettings();
    }

    public void RemoveRecentFile(string filePath)
    {
        if (_recentFiles.Remove(filePath))
        {
            OnPropertyChanged(nameof(RecentFiles));
            SaveSettings();
        }
    }

    public void ClearRecentFiles()
    {
        if (_recentFiles.Count > 0)
        {
            _recentFiles.Clear();
            OnPropertyChanged(nameof(RecentFiles));
            SaveSettings();
        }
    }

    private void TrimRecentFiles()
    {
        while (_recentFiles.Count > MaxRecentFiles)
            _recentFiles.RemoveAt(_recentFiles.Count - 1);
    }

    private void LoadSettings()
    {
        try
        {
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
                    _windowLeft = settings.WindowLeft;
                    _windowTop = settings.WindowTop;
                    _windowWidth = Math.Max(600, settings.WindowWidth);
                    _windowHeight = Math.Max(400, settings.WindowHeight);
                    _windowMaximized = settings.WindowMaximized;
                    _leftPanelWidth = Math.Max(200, Math.Min(600, settings.LeftPanelWidth));
                    _rightPanelWidth = Math.Max(250, Math.Min(800, settings.RightPanelWidth));
                    _sidebarWidth = Math.Max(150, Math.Min(300, settings.SidebarWidth));

                    // Creature browser panel (#1145)
                    _creatureBrowserPanelWidth = Math.Max(150, Math.Min(400, settings.CreatureBrowserPanelWidth));
                    _creatureBrowserPanelVisible = settings.CreatureBrowserPanelVisible;

                    _fontSize = Math.Max(8, Math.Min(24, settings.FontSize));
                    _fontFamily = settings.FontFamily ?? "";
                    _currentThemeId = !string.IsNullOrEmpty(settings.CurrentThemeId)
                        ? settings.CurrentThemeId
                        : "org.quartermaster.theme.light";

                    // Load logging settings from shared model
                    _loggingSettings.LogRetentionSessions = settings.LogRetentionSessions;
                    _loggingSettings.LogLevel = settings.LogLevel;
                    _loggingSettings.Normalize();
                    _loggingSettings.ApplyToLogger();

                    _levelHistoryEncoding = settings.LevelHistoryEncoding;
                    _recordLevelHistory = settings.RecordLevelHistory;

                    // Load recent files (expand ~ to full path for runtime use)
                    _recentFiles = PathHelper.ExpandPaths(settings.RecentFiles ?? new List<string>()).ToList();
                    // Use default if MaxRecentFiles is 0 (corrupt/old file) or out of range
                    _maxRecentFiles = settings.MaxRecentFiles > 0 && settings.MaxRecentFiles <= 20
                        ? settings.MaxRecentFiles
                        : DefaultMaxRecentFiles;

                    // Remove files that no longer exist (with logging)
                    var removedCount = _recentFiles.RemoveAll(f => !File.Exists(f));
                    if (removedCount > 0)
                    {
                        UnifiedLogger.LogApplication(LogLevel.INFO, $"Removed {removedCount} missing files from recent files list");
                    }

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
                WindowLeft = WindowLeft,
                WindowTop = WindowTop,
                WindowWidth = WindowWidth,
                WindowHeight = WindowHeight,
                WindowMaximized = WindowMaximized,
                LeftPanelWidth = LeftPanelWidth,
                RightPanelWidth = RightPanelWidth,
                SidebarWidth = SidebarWidth,
                CreatureBrowserPanelWidth = CreatureBrowserPanelWidth,
                CreatureBrowserPanelVisible = CreatureBrowserPanelVisible,
                FontSize = FontSize,
                FontFamily = FontFamily,
                CurrentThemeId = CurrentThemeId,
                LogRetentionSessions = _loggingSettings.LogRetentionSessions,
                LogLevel = _loggingSettings.LogLevel,
                LevelHistoryEncoding = LevelHistoryEncoding,
                RecordLevelHistory = RecordLevelHistory,
                RecentFiles = PathHelper.ContractPaths(_recentFiles).ToList(),  // Use ~ for privacy
                MaxRecentFiles = MaxRecentFiles
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
        public double WindowLeft { get; set; } = 100;
        public double WindowTop { get; set; } = 100;
        public double WindowWidth { get; set; } = 1200;
        public double WindowHeight { get; set; } = 800;
        public bool WindowMaximized { get; set; } = false;
        public double LeftPanelWidth { get; set; } = 350;
        public double RightPanelWidth { get; set; } = 400;
        public double SidebarWidth { get; set; } = 200;

        // Creature browser panel (#1145)
        public double CreatureBrowserPanelWidth { get; set; } = 200;
        public bool CreatureBrowserPanelVisible { get; set; } = true;

        public double FontSize { get; set; } = 14;
        public string FontFamily { get; set; } = "";
        public string CurrentThemeId { get; set; } = "org.quartermaster.theme.light";

        public int LogRetentionSessions { get; set; } = 3;
        public LogLevel LogLevel { get; set; } = LogLevel.INFO;

        public LevelHistoryEncoding LevelHistoryEncoding { get; set; } = LevelHistoryEncoding.Readable;
        public bool RecordLevelHistory { get; set; } = true;

        public List<string> RecentFiles { get; set; } = new();
        public int MaxRecentFiles { get; set; } = DefaultMaxRecentFiles;
    }
}
