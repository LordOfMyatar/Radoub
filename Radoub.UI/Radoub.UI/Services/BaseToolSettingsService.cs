using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Radoub.Formats.Common;
using Radoub.Formats.Logging;
using Radoub.Formats.Settings;

namespace Radoub.UI.Services;

/// <summary>
/// Base class for tool-specific settings services.
/// Provides the common singleton infrastructure, JSON persistence,
/// INotifyPropertyChanged, window/UI/logging properties, and recent files.
///
/// Subclasses provide:
/// - TSettings: The JSON-serializable data class
/// - Tool name and environment variable name
/// - Tool-specific properties and load/save mapping
///
/// Note: Parley uses a DI-based SettingsService with sub-services and does NOT
/// use this base class. This is designed for the monolithic settings pattern
/// used by Quartermaster, Manifest, Fence, and Trebuchet.
/// </summary>
/// <typeparam name="TSettings">The JSON-serializable settings data class</typeparam>
public abstract class BaseToolSettingsService<TSettings> : INotifyPropertyChanged, IWindowSettings
    where TSettings : BaseToolSettingsService<TSettings>.BaseSettingsData, new()
{
    // Window settings
    private double _windowLeft = 100;
    private double _windowTop = 100;
    private double _windowWidth;
    private double _windowHeight;
    private bool _windowMaximized;

    // UI settings
    private double _fontSize = 14;
    private string _fontFamily = "";
    private string _currentThemeId = "org.radoub.theme.light";

    // Logging settings
    private readonly LoggingSettings _loggingSettings = new();

    // Recent files
    private const int DefaultMaxRecentFiles = 10;
    private List<string> _recentFiles = new();
    private int _maxRecentFiles = DefaultMaxRecentFiles;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Tool name used for directory and logging (e.g., "Quartermaster", "Fence").
    /// </summary>
    protected abstract string ToolName { get; }

    /// <summary>
    /// Environment variable name for settings directory override (e.g., "QUARTERMASTER_SETTINGS_DIR").
    /// </summary>
    protected abstract string SettingsEnvironmentVariable { get; }

    /// <summary>
    /// Settings JSON filename (e.g., "QuartermasterSettings.json").
    /// </summary>
    protected abstract string SettingsFileName { get; }

    /// <summary>
    /// Default window width for this tool.
    /// </summary>
    protected virtual double DefaultWindowWidth => 1200;

    /// <summary>
    /// Default window height for this tool.
    /// </summary>
    protected virtual double DefaultWindowHeight => 800;

    /// <summary>
    /// Minimum window width constraint.
    /// </summary>
    protected virtual double MinWindowWidth => 600;

    /// <summary>
    /// Minimum window height constraint.
    /// </summary>
    protected virtual double MinWindowHeight => 400;

    /// <summary>
    /// Shared settings for game paths and TLK configuration.
    /// </summary>
    public static RadoubSettings SharedSettings => RadoubSettings.Instance;

    private string? _settingsDirectory;

    /// <summary>
    /// Gets the settings directory, supporting environment variable override for testing.
    /// </summary>
    protected string SettingsDirectory
    {
        get
        {
            if (_settingsDirectory == null)
            {
                var envDir = Environment.GetEnvironmentVariable(SettingsEnvironmentVariable);
                if (!string.IsNullOrEmpty(envDir))
                {
                    _settingsDirectory = envDir;
                }
                else
                {
                    var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    _settingsDirectory = Path.Combine(userProfile, "Radoub", ToolName);
                }
            }
            return _settingsDirectory;
        }
        set => _settingsDirectory = value;
    }

    private string SettingsFilePath => Path.Combine(SettingsDirectory, SettingsFileName);

    protected BaseToolSettingsService()
    {
        _windowWidth = DefaultWindowWidth;
        _windowHeight = DefaultWindowHeight;
    }

    /// <summary>
    /// Call from subclass constructor to load settings. Not called automatically
    /// because the subclass fields must be initialized before loading.
    /// </summary>
    protected void Initialize()
    {
        LoadSettings();
        UnifiedLogger.LogApplication(LogLevel.INFO, $"{ToolName} SettingsService initialized");
    }

    #region Window Properties

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
        set { if (SetProperty(ref _windowWidth, Math.Max(MinWindowWidth, value))) SaveSettings(); }
    }

    public double WindowHeight
    {
        get => _windowHeight;
        set { if (SetProperty(ref _windowHeight, Math.Max(MinWindowHeight, value))) SaveSettings(); }
    }

    public bool WindowMaximized
    {
        get => _windowMaximized;
        set { if (SetProperty(ref _windowMaximized, value)) SaveSettings(); }
    }

    #endregion

    #region UI Properties

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
        set { if (SetProperty(ref _currentThemeId, value ?? "org.radoub.theme.light")) SaveSettings(); }
    }

    #endregion

    #region Logging Properties

    public int LogRetentionSessions
    {
        get => _loggingSettings.LogRetentionSessions;
        set
        {
            var clamped = Math.Max(1, Math.Min(10, value));
            if (_loggingSettings.LogRetentionSessions != clamped)
            {
                _loggingSettings.LogRetentionSessions = clamped;
                OnLoggingRetentionChanged(clamped);
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
                OnLoggingLevelChanged(value);
                OnPropertyChanged();
                SaveSettings();
            }
        }
    }

    /// <summary>
    /// Override to perform additional actions when log retention changes.
    /// Trebuchet uses this to sync to RadoubSettings.
    /// </summary>
    protected virtual void OnLoggingRetentionChanged(int sessions) { }

    /// <summary>
    /// Override to perform additional actions when log level changes.
    /// Trebuchet uses this to sync to RadoubSettings.
    /// </summary>
    protected virtual void OnLoggingLevelChanged(LogLevel level) { }

    #endregion

    #region Recent Files

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

    /// <summary>
    /// Override to customize file existence validation during load.
    /// Default removes files that don't exist on disk synchronously.
    /// Fence overrides this to defer validation for network paths.
    /// </summary>
    protected virtual void ValidateRecentFilesOnLoad()
    {
        var removedCount = _recentFiles.RemoveAll(f => !File.Exists(f));
        if (removedCount > 0)
        {
            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"Removed {removedCount} missing files from recent files list");
        }
    }

    #endregion

    #region Load/Save Infrastructure

    private void LoadSettings()
    {
        try
        {
            if (!Directory.Exists(SettingsDirectory))
            {
                UnifiedLogger.LogApplication(LogLevel.INFO,
                    $"Creating settings directory: {UnifiedLogger.SanitizePath(SettingsDirectory)}");
                Directory.CreateDirectory(SettingsDirectory);
            }

            if (File.Exists(SettingsFilePath))
            {
                var json = File.ReadAllText(SettingsFilePath);
                var settings = JsonSerializer.Deserialize<TSettings>(json);

                if (settings != null)
                {
                    // Load common properties
                    _windowLeft = settings.WindowLeft;
                    _windowTop = settings.WindowTop;
                    _windowWidth = Math.Max(MinWindowWidth, settings.WindowWidth);
                    _windowHeight = Math.Max(MinWindowHeight, settings.WindowHeight);
                    _windowMaximized = settings.WindowMaximized;

                    _fontSize = Math.Max(8, Math.Min(24, settings.FontSize));
                    _fontFamily = settings.FontFamily ?? "";
                    _currentThemeId = !string.IsNullOrEmpty(settings.CurrentThemeId)
                        ? settings.CurrentThemeId
                        : "org.radoub.theme.light";

                    _loggingSettings.LogRetentionSessions = settings.LogRetentionSessions;
                    _loggingSettings.LogLevel = settings.LogLevel;
                    _loggingSettings.Normalize();
                    _loggingSettings.ApplyToLogger();

                    // Load recent files
                    _recentFiles = PathHelper.ExpandPaths(settings.RecentFiles ?? new List<string>()).ToList();
                    _maxRecentFiles = settings.MaxRecentFiles > 0 && settings.MaxRecentFiles <= 20
                        ? settings.MaxRecentFiles
                        : DefaultMaxRecentFiles;

                    ValidateRecentFilesOnLoad();

                    // Load tool-specific properties
                    LoadToolSettings(settings);

                    UnifiedLogger.LogApplication(LogLevel.INFO,
                        $"Loaded settings: {_recentFiles.Count} recent files, max={_maxRecentFiles}");
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

    /// <summary>
    /// Saves all settings to the JSON file.
    /// Marked as protected so subclass property setters can also trigger saves
    /// (though the recommended pattern is to use SetProperty + SaveSettings in the setter).
    /// </summary>
    protected void SaveSettings()
    {
        try
        {
            var settings = new TSettings();

            // Save common properties
            settings.WindowLeft = WindowLeft;
            settings.WindowTop = WindowTop;
            settings.WindowWidth = WindowWidth;
            settings.WindowHeight = WindowHeight;
            settings.WindowMaximized = WindowMaximized;
            settings.FontSize = FontSize;
            settings.FontFamily = FontFamily;
            settings.CurrentThemeId = CurrentThemeId;
            settings.LogRetentionSessions = _loggingSettings.LogRetentionSessions;
            settings.LogLevel = _loggingSettings.LogLevel;
            settings.RecentFiles = PathHelper.ContractPaths(_recentFiles).ToList();
            settings.MaxRecentFiles = MaxRecentFiles;

            // Save tool-specific properties
            SaveToolSettings(settings);

            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(SettingsFilePath, json);
            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                $"Settings saved to {UnifiedLogger.SanitizePath(SettingsFilePath)}");
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error saving settings: {ex.Message}");
        }
    }

    /// <summary>
    /// Load tool-specific properties from the deserialized settings data.
    /// Called during LoadSettings() after common properties are loaded.
    /// </summary>
    protected abstract void LoadToolSettings(TSettings settings);

    /// <summary>
    /// Save tool-specific properties to the settings data object.
    /// Called during SaveSettings() after common properties are saved.
    /// </summary>
    protected abstract void SaveToolSettings(TSettings settings);

    #endregion

    #region INotifyPropertyChanged Infrastructure

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    #endregion

    #region Base Settings Data

    /// <summary>
    /// Base class for the JSON-serializable settings data.
    /// Contains all common properties. Tool-specific data classes
    /// should inherit from this and add their own properties.
    /// </summary>
    public class BaseSettingsData
    {
        public double WindowLeft { get; set; } = 100;
        public double WindowTop { get; set; } = 100;
        public double WindowWidth { get; set; } = 1200;
        public double WindowHeight { get; set; } = 800;
        public bool WindowMaximized { get; set; }

        public double FontSize { get; set; } = 14;
        public string FontFamily { get; set; } = "";
        public string CurrentThemeId { get; set; } = "org.radoub.theme.light";

        public int LogRetentionSessions { get; set; } = 3;
        public LogLevel LogLevel { get; set; } = LogLevel.INFO;

        public List<string> RecentFiles { get; set; } = new();
        public int MaxRecentFiles { get; set; } = 10;
    }

    #endregion
}
