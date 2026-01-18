using System;
using Radoub.Formats.Logging;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Runtime.CompilerServices;
using Radoub.Formats.Settings;

namespace RadoubLauncher.Services;

/// <summary>
/// Settings service for Trebuchet.
/// Stores tool-specific settings in ~/Radoub/Trebuchet/TrebuchetSettings.json
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
                var envDir = Environment.GetEnvironmentVariable("TREBUCHET_SETTINGS_DIR");
                if (!string.IsNullOrEmpty(envDir))
                {
                    _settingsDirectory = envDir;
                }
                else
                {
                    var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    _settingsDirectory = Path.Combine(userProfile, "Radoub", "Trebuchet");
                }
            }
            return _settingsDirectory;
        }
    }

    private static string SettingsFilePath => Path.Combine(SettingsDirectory, "TrebuchetSettings.json");

    // Window settings
    private double _windowLeft = 100;
    private double _windowTop = 100;
    private double _windowWidth = 900;
    private double _windowHeight = 600;
    private bool _windowMaximized = false;

    // UI settings
    private double _fontSize = 14;
    private double _fontSizeScale = 1.0;
    private string _fontFamily = "";
    private string _currentThemeId = "org.trebuchet.theme.light";

    // Logging settings
    private int _logRetentionSessions = 3;
    private LogLevel _logLevel = LogLevel.INFO;

    // Recent modules
    private const int DefaultMaxRecentModules = 10;
    private List<string> _recentModules = new();
    private int _maxRecentModules = DefaultMaxRecentModules;

    public event PropertyChangedEventHandler? PropertyChanged;

    private SettingsService()
    {
        LoadSettings();
        UnifiedLogger.LogApplication(LogLevel.INFO, "Trebuchet SettingsService initialized");
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

    // UI properties
    public double FontSize
    {
        get => _fontSize;
        set { if (SetProperty(ref _fontSize, Math.Max(8, Math.Min(24, value)))) SaveSettings(); }
    }

    public double FontSizeScale
    {
        get => _fontSizeScale;
        set { if (SetProperty(ref _fontSizeScale, Math.Max(0.8, Math.Min(1.5, value)))) SaveSettings(); }
    }

    public string FontFamily
    {
        get => _fontFamily;
        set { if (SetProperty(ref _fontFamily, value ?? "")) SaveSettings(); }
    }

    public string CurrentThemeId
    {
        get => _currentThemeId;
        set { if (SetProperty(ref _currentThemeId, value ?? "org.trebuchet.theme.light")) SaveSettings(); }
    }

    // Logging properties
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

    // Recent Modules
    public List<string> RecentModules => _recentModules.ToList();

    public int MaxRecentModules
    {
        get => _maxRecentModules;
        set
        {
            if (SetProperty(ref _maxRecentModules, Math.Max(1, Math.Min(20, value))))
            {
                TrimRecentModules();
                SaveSettings();
            }
        }
    }

    public void AddRecentModule(string modulePath)
    {
        if (string.IsNullOrEmpty(modulePath))
            return;

        // For modules, check if directory or file exists
        if (!File.Exists(modulePath) && !Directory.Exists(modulePath))
            return;

        _recentModules.Remove(modulePath);
        _recentModules.Insert(0, modulePath);
        TrimRecentModules();
        OnPropertyChanged(nameof(RecentModules));
        SaveSettings();
    }

    public void RemoveRecentModule(string modulePath)
    {
        if (_recentModules.Remove(modulePath))
        {
            OnPropertyChanged(nameof(RecentModules));
            SaveSettings();
        }
    }

    public void ClearRecentModules()
    {
        if (_recentModules.Count > 0)
        {
            _recentModules.Clear();
            OnPropertyChanged(nameof(RecentModules));
            SaveSettings();
        }
    }

    private void TrimRecentModules()
    {
        while (_recentModules.Count > MaxRecentModules)
            _recentModules.RemoveAt(_recentModules.Count - 1);
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

                    _fontSize = Math.Max(8, Math.Min(24, settings.FontSize));
                    _fontSizeScale = Math.Max(0.8, Math.Min(1.5, settings.FontSizeScale));
                    _fontFamily = settings.FontFamily ?? "";
                    _currentThemeId = !string.IsNullOrEmpty(settings.CurrentThemeId)
                        ? settings.CurrentThemeId
                        : "org.trebuchet.theme.light";

                    _logRetentionSessions = Math.Max(1, Math.Min(10, settings.LogRetentionSessions));
                    _logLevel = settings.LogLevel;
                    UnifiedLogger.SetLogLevel(_logLevel);

                    _recentModules = settings.RecentModules?.ToList() ?? new List<string>();
                    _maxRecentModules = settings.MaxRecentModules > 0 && settings.MaxRecentModules <= 20
                        ? settings.MaxRecentModules
                        : DefaultMaxRecentModules;

                    // Remove modules that no longer exist
                    var removedCount = _recentModules.RemoveAll(m => !File.Exists(m) && !Directory.Exists(m));
                    if (removedCount > 0)
                    {
                        UnifiedLogger.LogApplication(LogLevel.INFO, $"Removed {removedCount} missing modules from recent list");
                    }

                    UnifiedLogger.LogApplication(LogLevel.INFO, $"Loaded settings: {_recentModules.Count} recent modules");
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
                FontSize = FontSize,
                FontSizeScale = FontSizeScale,
                FontFamily = FontFamily,
                CurrentThemeId = CurrentThemeId,
                LogRetentionSessions = LogRetentionSessions,
                LogLevel = CurrentLogLevel,
                RecentModules = _recentModules,
                MaxRecentModules = MaxRecentModules
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
        public double WindowWidth { get; set; } = 900;
        public double WindowHeight { get; set; } = 600;
        public bool WindowMaximized { get; set; } = false;

        public double FontSize { get; set; } = 14;
        public double FontSizeScale { get; set; } = 1.0;
        public string FontFamily { get; set; } = "";
        public string CurrentThemeId { get; set; } = "org.trebuchet.theme.light";

        public int LogRetentionSessions { get; set; } = 3;
        public LogLevel LogLevel { get; set; } = LogLevel.INFO;

        public List<string> RecentModules { get; set; } = new();
        public int MaxRecentModules { get; set; } = DefaultMaxRecentModules;
    }
}
