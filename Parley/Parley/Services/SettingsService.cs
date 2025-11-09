using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Runtime.CompilerServices;

namespace DialogEditor.Services
{
    /// <summary>
    /// Plugin crash information for recovery
    /// </summary>
    public class PluginCrashInfo
    {
        public int CrashCount { get; set; }
        public DateTime LastCrash { get; set; }
    }

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
                    _settingsDirectory = Path.Combine(userProfile, "Parley");
                }
                return _settingsDirectory;
            }
        }

        private static string SettingsFilePath => Path.Combine(SettingsDirectory, "ParleySettings.json");
        private const int DefaultMaxRecentFiles = 10;
        
        // Recent files
        private List<string> _recentFiles = new List<string>();
        private int _maxRecentFiles = DefaultMaxRecentFiles;
        
        // Window settings
        private double _windowLeft = 100;
        private double _windowTop = 100;
        private double _windowWidth = 1200;
        private double _windowHeight = 800;
        private bool _windowMaximized = false;
        
        // UI settings
        private double _fontSize = 14;
        private bool _isDarkTheme = false;
        
        // Game settings
        private string _neverwinterNightsPath = "";
        private string _baseGameInstallPath = ""; // Phase 2: Base game installation (Steam/GOG)
        private string _currentModulePath = "";
        private List<string> _modulePaths = new List<string>();

        // Logging settings
        private int _logRetentionSessions = 3; // Default: keep 3 most recent sessions
        private LogLevel _logLevel = LogLevel.INFO;

        // Auto-save settings - Phase 1 Step 6
        private bool _autoSaveEnabled = true; // Default: ON
        private int _autoSaveDelayMs = 2000; // Default: 2 seconds

        // Plugin settings
        private List<string> _enabledPlugins = new List<string>();
        private List<string> _disabledPlugins = new List<string>();
        private bool _pluginSafeMode = false;

        // Crash recovery
        private bool _lastSessionCrashed = false;
        private List<string> _pluginsLoadedDuringCrash = new List<string>();
        private Dictionary<string, PluginCrashInfo> _pluginCrashHistory = new Dictionary<string, PluginCrashInfo>();

        public event PropertyChangedEventHandler? PropertyChanged;

        private SettingsService()
        {
            LoadSettings();

            // Phase 2: Auto-detect resource paths on first run
            if (string.IsNullOrEmpty(_neverwinterNightsPath) || string.IsNullOrEmpty(_currentModulePath))
            {
                AutoDetectResourcePaths();
            }

            UnifiedLogger.LogApplication(LogLevel.INFO, "Parley SettingsService initialized");
        }

        /// <summary>
        /// Phase 2: Auto-detect Neverwinter Nights resource paths
        /// </summary>
        private void AutoDetectResourcePaths()
        {
            // Try to detect game path
            if (string.IsNullOrEmpty(_neverwinterNightsPath))
            {
                var gamePath = ResourcePathHelper.AutoDetectGamePath();
                if (!string.IsNullOrEmpty(gamePath))
                {
                    _neverwinterNightsPath = gamePath;

                    // Try to detect module path based on game path
                    var modulePath = ResourcePathHelper.AutoDetectModulePath(_neverwinterNightsPath);
                    if (!string.IsNullOrEmpty(modulePath))
                    {
                        _currentModulePath = modulePath;
                    }

                    SaveSettings();
                }
            }
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
        
        public bool IsDarkTheme
        {
            get => _isDarkTheme;
            set { if (SetProperty(ref _isDarkTheme, value)) SaveSettings(); }
        }

        // Game Settings Properties
        public string NeverwinterNightsPath
        {
            get => _neverwinterNightsPath;
            set { if (SetProperty(ref _neverwinterNightsPath, value ?? "")) SaveSettings(); }
        }

        /// <summary>
        /// Phase 2: Base game installation path (Steam/GOG - contains data\ folder)
        /// </summary>
        public string BaseGameInstallPath
        {
            get => _baseGameInstallPath;
            set { if (SetProperty(ref _baseGameInstallPath, value ?? "")) SaveSettings(); }
        }

        public string CurrentModulePath
        {
            get => _currentModulePath;
            set { if (SetProperty(ref _currentModulePath, value ?? "")) SaveSettings(); }
        }

        public List<string> ModulePaths
        {
            get => _modulePaths.ToList(); // Return a copy to prevent external modification
        }

        public void AddModulePath(string path)
        {
            if (!string.IsNullOrWhiteSpace(path) && !_modulePaths.Contains(path))
            {
                _modulePaths.Add(path);
                OnPropertyChanged(nameof(ModulePaths));
                SaveSettings();
            }
        }

        public void RemoveModulePath(string path)
        {
            if (_modulePaths.Remove(path))
            {
                OnPropertyChanged(nameof(ModulePaths));
                SaveSettings();
            }
        }

        public void ClearModulePaths()
        {
            if (_modulePaths.Count > 0)
            {
                _modulePaths.Clear();
                OnPropertyChanged(nameof(ModulePaths));
                SaveSettings();
                UnifiedLogger.LogApplication(LogLevel.INFO, "Cleared all recent module paths");
            }
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

        // Auto-Save Settings Properties - Phase 1 Step 6
        public bool AutoSaveEnabled
        {
            get => _autoSaveEnabled;
            set
            {
                if (SetProperty(ref _autoSaveEnabled, value))
                {
                    SaveSettings();
                    UnifiedLogger.LogApplication(LogLevel.INFO, $"Auto-save {(value ? "enabled" : "disabled")}");
                }
            }
        }

        public int AutoSaveDelayMs
        {
            get => _autoSaveDelayMs;
            set
            {
                // Clamp between 1-10 seconds (1000-10000 ms)
                if (SetProperty(ref _autoSaveDelayMs, Math.Max(1000, Math.Min(10000, value))))
                {
                    SaveSettings();
                    UnifiedLogger.LogApplication(LogLevel.INFO, $"Auto-save delay set to {value}ms");
                }
            }
        }

        // Plugin Settings Properties
        public List<string> EnabledPlugins
        {
            get => _enabledPlugins.ToList(); // Return a copy
        }

        public List<string> DisabledPlugins
        {
            get => _disabledPlugins.ToList(); // Return a copy
        }

        public bool PluginSafeMode
        {
            get => _pluginSafeMode;
            set
            {
                if (SetProperty(ref _pluginSafeMode, value))
                {
                    SaveSettings();
                    UnifiedLogger.LogApplication(LogLevel.INFO, $"Plugin safe mode {(value ? "enabled" : "disabled")}");
                }
            }
        }

        public void SetPluginEnabled(string pluginId, bool enabled)
        {
            if (enabled)
            {
                if (!_enabledPlugins.Contains(pluginId))
                {
                    _enabledPlugins.Add(pluginId);
                }
                _disabledPlugins.Remove(pluginId);
            }
            else
            {
                if (!_disabledPlugins.Contains(pluginId))
                {
                    _disabledPlugins.Add(pluginId);
                }
                _enabledPlugins.Remove(pluginId);
            }

            OnPropertyChanged(nameof(EnabledPlugins));
            OnPropertyChanged(nameof(DisabledPlugins));
            SaveSettings();
        }

        public bool IsPluginEnabled(string pluginId)
        {
            // If in disabled list, it's disabled
            if (_disabledPlugins.Contains(pluginId))
                return false;

            // If in enabled list or not in any list (default enabled), it's enabled
            return true;
        }

        // Crash Recovery Properties and Methods
        public bool LastSessionCrashed
        {
            get => _lastSessionCrashed;
            set
            {
                if (SetProperty(ref _lastSessionCrashed, value))
                {
                    SaveSettings();
                }
            }
        }

        public List<string> PluginsLoadedDuringCrash => _pluginsLoadedDuringCrash.ToList();

        public void SetSessionStarted(List<string> loadedPlugins)
        {
            _lastSessionCrashed = true; // Will be cleared on clean shutdown
            _pluginsLoadedDuringCrash = loadedPlugins.ToList();
            SaveSettings();
        }

        public void SetSessionEnded()
        {
            _lastSessionCrashed = false;
            _pluginsLoadedDuringCrash.Clear();
            SaveSettings();
        }

        public void RecordPluginCrash(string pluginId)
        {
            if (!_pluginCrashHistory.ContainsKey(pluginId))
            {
                _pluginCrashHistory[pluginId] = new PluginCrashInfo
                {
                    CrashCount = 0,
                    LastCrash = DateTime.MinValue
                };
            }

            var crashInfo = _pluginCrashHistory[pluginId];
            crashInfo.CrashCount++;
            crashInfo.LastCrash = DateTime.Now;

            SaveSettings();
            UnifiedLogger.LogPlugin(LogLevel.WARN, $"Plugin {pluginId} crashed (total crashes: {crashInfo.CrashCount})");

            // Auto-disable plugin after 3 crashes
            if (crashInfo.CrashCount >= 3)
            {
                SetPluginEnabled(pluginId, false);
                UnifiedLogger.LogPlugin(LogLevel.ERROR, $"Plugin {pluginId} auto-disabled after {crashInfo.CrashCount} crashes");
            }
        }

        public PluginCrashInfo? GetPluginCrashInfo(string pluginId)
        {
            return _pluginCrashHistory.TryGetValue(pluginId, out var info) ? info : null;
        }

        public Dictionary<string, PluginCrashInfo> GetAllCrashHistory()
        {
            return new Dictionary<string, PluginCrashInfo>(_pluginCrashHistory);
        }

        private void LoadSettings()
        {
            try
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"LoadSettings: SettingsDirectory={UnifiedLogger.SanitizePath(SettingsDirectory)}, SettingsFilePath={UnifiedLogger.SanitizePath(SettingsFilePath)}");

                // Ensure settings directory exists
                if (!Directory.Exists(SettingsDirectory))
                {
                    UnifiedLogger.LogApplication(LogLevel.INFO, $"Creating settings directory: {SettingsDirectory}");
                    Directory.CreateDirectory(SettingsDirectory);
                }

                if (File.Exists(SettingsFilePath))
                {
                    var json = File.ReadAllText(SettingsFilePath);
                    var settings = JsonSerializer.Deserialize<SettingsData>(json);
                    
                    if (settings != null)
                    {
                        _recentFiles = settings.RecentFiles?.ToList() ?? new List<string>();
                        _maxRecentFiles = Math.Max(1, Math.Min(20, settings.MaxRecentFiles));
                        
                        // Load window settings
                        _windowLeft = settings.WindowLeft;
                        _windowTop = settings.WindowTop;
                        _windowWidth = Math.Max(400, settings.WindowWidth);
                        _windowHeight = Math.Max(300, settings.WindowHeight);
                        _windowMaximized = settings.WindowMaximized;
                        
                        // Load UI settings
                        _fontSize = Math.Max(8, Math.Min(24, settings.FontSize));
                        _isDarkTheme = settings.IsDarkTheme;
                        
                        // Load game settings
                        _neverwinterNightsPath = settings.NeverwinterNightsPath ?? "";
                        _baseGameInstallPath = settings.BaseGameInstallPath ?? ""; // Phase 2
                        _currentModulePath = settings.CurrentModulePath ?? "";
                        _modulePaths = settings.ModulePaths?.ToList() ?? new List<string>();

                        // Load logging settings (backwards compatible with old LogRetentionDays)
                        if (settings.LogRetentionSessions > 0)
                        {
                            _logRetentionSessions = Math.Max(1, Math.Min(10, settings.LogRetentionSessions));
                        }
                        else
                        {
                            // Backwards compatibility: convert old days setting to sessions (rough estimate)
                            _logRetentionSessions = 3; // Default
                        }
                        _logLevel = settings.LogLevel;
                        // Only set log level if it hasn't been explicitly set already
                        // (MainWindow may have set DEBUG for development)
                        // UnifiedLogger.SetLogLevel(_logLevel); // Commented out - don't override

                        // Load auto-save settings - Phase 1 Step 6
                        _autoSaveEnabled = settings.AutoSaveEnabled;
                        _autoSaveDelayMs = Math.Max(1000, Math.Min(10000, settings.AutoSaveDelayMs));

                        // Load plugin settings
                        _enabledPlugins = settings.EnabledPlugins?.ToList() ?? new List<string>();
                        _disabledPlugins = settings.DisabledPlugins?.ToList() ?? new List<string>();
                        _pluginSafeMode = settings.PluginSafeMode;

                        // Load crash recovery
                        _lastSessionCrashed = settings.LastSessionCrashed;
                        _pluginsLoadedDuringCrash = settings.PluginsLoadedDuringCrash?.ToList() ?? new List<string>();
                        _pluginCrashHistory = settings.PluginCrashHistory ?? new Dictionary<string, PluginCrashInfo>();

                        UnifiedLogger.LogApplication(LogLevel.INFO, $"Loaded settings: {_recentFiles.Count} recent files, max={_maxRecentFiles}, theme={(_isDarkTheme ? "dark" : "light")}, logLevel={_logLevel}, retention={_logRetentionSessions} sessions, autoSave={_autoSaveEnabled}, delay={_autoSaveDelayMs}ms, plugins={_enabledPlugins.Count} enabled, {_disabledPlugins.Count} disabled, safeMode={_pluginSafeMode}, lastCrashed={_lastSessionCrashed}");
                    }
                    else
                    {
                        UnifiedLogger.LogApplication(LogLevel.WARN, "Failed to deserialize settings, using defaults");
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
                    RecentFiles = _recentFiles.ToList(),
                    MaxRecentFiles = MaxRecentFiles,
                    WindowLeft = WindowLeft,
                    WindowTop = WindowTop,
                    WindowWidth = WindowWidth,
                    WindowHeight = WindowHeight,
                    WindowMaximized = WindowMaximized,
                    FontSize = FontSize,
                    IsDarkTheme = IsDarkTheme,
                    NeverwinterNightsPath = NeverwinterNightsPath,
                    BaseGameInstallPath = BaseGameInstallPath, // Phase 2
                    CurrentModulePath = CurrentModulePath,
                    ModulePaths = _modulePaths.ToList(),
                    LogRetentionSessions = LogRetentionSessions,
                    LogLevel = CurrentLogLevel,
                    AutoSaveEnabled = AutoSaveEnabled,
                    AutoSaveDelayMs = AutoSaveDelayMs,
                    EnabledPlugins = _enabledPlugins.ToList(),
                    DisabledPlugins = _disabledPlugins.ToList(),
                    PluginSafeMode = PluginSafeMode,
                    LastSessionCrashed = LastSessionCrashed,
                    PluginsLoadedDuringCrash = _pluginsLoadedDuringCrash.ToList(),
                    PluginCrashHistory = _pluginCrashHistory
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
            public double WindowWidth { get; set; } = 1200;
            public double WindowHeight { get; set; } = 800;
            public bool WindowMaximized { get; set; } = false;
            
            // UI settings
            public double FontSize { get; set; } = 14;
            public bool IsDarkTheme { get; set; } = false;
            
            // Game settings
            public string NeverwinterNightsPath { get; set; } = "";
            public string BaseGameInstallPath { get; set; } = ""; // Phase 2: Base game installation
            public string CurrentModulePath { get; set; } = "";
            public List<string> ModulePaths { get; set; } = new List<string>();

            // Logging settings
            public int LogRetentionSessions { get; set; } = 3; // Keep 3 most recent sessions
            public LogLevel LogLevel { get; set; } = LogLevel.INFO;

            // Auto-save settings - Phase 1 Step 6
            public bool AutoSaveEnabled { get; set; } = true;
            public int AutoSaveDelayMs { get; set; } = 2000;

            // Plugin settings
            public List<string> EnabledPlugins { get; set; } = new List<string>();
            public List<string> DisabledPlugins { get; set; } = new List<string>();
            public bool PluginSafeMode { get; set; } = false;

            // Crash recovery
            public bool LastSessionCrashed { get; set; } = false;
            public List<string> PluginsLoadedDuringCrash { get; set; } = new List<string>();
            public Dictionary<string, PluginCrashInfo> PluginCrashHistory { get; set; } = new Dictionary<string, PluginCrashInfo>();
        }
    }
}