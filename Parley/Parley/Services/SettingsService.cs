using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Runtime.CompilerServices;
using DialogEditor.Utils;
using Microsoft.Extensions.DependencyInjection;
using Radoub.Formats.Settings;
using SharedPathHelper = Radoub.Formats.Common.PathHelper;
using Radoub.Formats.Logging;

namespace DialogEditor.Services
{
    /// <summary>
    /// Stores speaker-specific visual preferences (color and shape)
    /// </summary>
    public class SpeakerPreferences
    {
        public string? Color { get; set; }
        public string? Shape { get; set; } // Store as string for JSON serialization
    }

    public class SettingsService : ISettingsService
    {
        /// <summary>
        /// Static accessor for XAML x:Static bindings.
        /// Resolves from DI container. Must only be called after DI is configured.
        /// </summary>
        public static SettingsService Instance => Program.Services.GetRequiredService<SettingsService>();

        // #1233: Sub-services injected via constructor (DI-managed singletons)
        private readonly RecentFilesService _recentFiles;
        private readonly UISettingsService _uiSettings;
        private readonly WindowLayoutService _windowLayout;
        private readonly SpeakerPreferencesService _speakerPreferences;
        private readonly ParameterCacheService _parameterCache;

        // #1269: Additional sub-services extracted for single responsibility
        private readonly LoggingSettingsService _loggingSettings;
        private readonly ModulePathsService _modulePaths;
        private readonly EditorPreferencesService _editorPreferences;

        // Lazy initialization to avoid static field initialization timing issues
        private static string? _settingsDirectory;
        private static string SettingsDirectory
        {
            get
            {
                if (_settingsDirectory == null)
                {
                    // Check for test override first (allows UI tests to use isolated settings)
                    var testDir = Environment.GetEnvironmentVariable("PARLEY_SETTINGS_DIR");
                    if (!string.IsNullOrEmpty(testDir))
                    {
                        _settingsDirectory = testDir;
                    }
                    else
                    {
                        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                        // New location: ~/Radoub/Parley (matches Manifest's ~/Radoub/Manifest pattern)
                        _settingsDirectory = Path.Combine(userProfile, "Radoub", "Parley");
                    }
                }
                return _settingsDirectory;
            }
        }

        private static string SettingsFilePath => Path.Combine(SettingsDirectory, "ParleySettings.json");
        private const int DefaultMaxRecentFiles = 10;

        // NPC Speaker Visual Preferences (Issue #16, #36)
        // NOTE: Speaker preferences are now stored in SpeakerPreferencesService (Issue #179)
        // This field is only used for migration from old settings files
        private Dictionary<string, SpeakerPreferences>? _legacyNpcSpeakerPreferences = null;

        /// <summary>
        /// Shared settings instance for game paths and TLK configuration.
        /// Changes here are shared with other Radoub tools (Manifest, etc.).
        /// </summary>
        public static RadoubSettings SharedSettings => RadoubSettings.Instance;

        public event PropertyChangedEventHandler? PropertyChanged;

        public SettingsService(
            RecentFilesService recentFiles,
            UISettingsService uiSettings,
            WindowLayoutService windowLayout,
            SpeakerPreferencesService speakerPreferences,
            ParameterCacheService parameterCache,
            LoggingSettingsService loggingSettings,
            ModulePathsService modulePaths,
            EditorPreferencesService editorPreferences)
        {
            _recentFiles = recentFiles;
            _uiSettings = uiSettings;
            _windowLayout = windowLayout;
            _speakerPreferences = speakerPreferences;
            _parameterCache = parameterCache;
            _loggingSettings = loggingSettings;
            _modulePaths = modulePaths;
            _editorPreferences = editorPreferences;

            // Subscribe to delegated services for save notifications (#719, #1269)
            _recentFiles.SettingsChanged += SaveSettings;
            _uiSettings.SettingsChanged += SaveSettings;
            _windowLayout.SettingsChanged += SaveSettings;
            _loggingSettings.SettingsChanged += SaveSettings;
            _modulePaths.SettingsChanged += SaveSettings;
            _editorPreferences.SettingsChanged += SaveSettings;

            LoadSettings();

            // #1961: Auto-detect moved to DeferredAutoDetectPaths() — called from OnWindowOpened

            UnifiedLogger.LogApplication(LogLevel.INFO, "Parley SettingsService initialized");
        }

        /// <summary>
        /// Auto-detect Neverwinter Nights resource paths.
        /// #1961: Deferred from constructor to OnWindowOpened to avoid blocking startup.
        /// </summary>
        public void DeferredAutoDetectPaths()
        {
            // RadoubSettings.Instance already does auto-detection on first access
            // We just need to check if module path needs additional detection
            if (string.IsNullOrEmpty(SharedSettings.CurrentModulePath) &&
                !string.IsNullOrEmpty(SharedSettings.NeverwinterNightsPath))
            {
                var modulePath = ResourcePathDetector.AutoDetectModulePath(SharedSettings.NeverwinterNightsPath);
                if (!string.IsNullOrEmpty(modulePath))
                {
                    SharedSettings.CurrentModulePath = modulePath;
                }
            }
        }

        // Recent files - DELEGATED to RecentFilesService (#719)
        public List<string> RecentFiles => _recentFiles.RecentFiles;

        public int MaxRecentFiles
        {
            get => _recentFiles.MaxRecentFiles;
            set => _recentFiles.MaxRecentFiles = value;
        }

        // Recent creature tags for character picker (#1244)
        private List<string> _recentCreatureTags = new();
        public List<string> RecentCreatureTags => _recentCreatureTags;

        public void SetRecentCreatureTags(List<string> tags)
        {
            _recentCreatureTags = tags ?? new List<string>();
            SaveSettings();
        }

        // Window properties - DELEGATED to WindowLayoutService (#719)
        public double WindowLeft
        {
            get => _windowLayout.WindowLeft;
            set => _windowLayout.WindowLeft = value;
        }

        public double WindowTop
        {
            get => _windowLayout.WindowTop;
            set => _windowLayout.WindowTop = value;
        }

        public double WindowWidth
        {
            get => _windowLayout.WindowWidth;
            set => _windowLayout.WindowWidth = value;
        }

        public double WindowHeight
        {
            get => _windowLayout.WindowHeight;
            set => _windowLayout.WindowHeight = value;
        }

        public bool WindowMaximized
        {
            get => _windowLayout.WindowMaximized;
            set => _windowLayout.WindowMaximized = value;
        }

        // Panel layout properties - DELEGATED to WindowLayoutService (#719)
        public double LeftPanelWidth
        {
            get => _windowLayout.LeftPanelWidth;
            set => _windowLayout.LeftPanelWidth = value;
        }

        public double TopLeftPanelHeight
        {
            get => _windowLayout.TopLeftPanelHeight;
            set => _windowLayout.TopLeftPanelHeight = value;
        }

        // Theme/font properties removed — now managed by RadoubSettings (Trebuchet is sole authority)

        /// <summary>
        /// Flowchart layout mode: "Floating" (separate window), "SideBySide" (split view), "Tabbed" (tab in main area)
        /// </summary>
        public string FlowchartLayout
        {
            get => _uiSettings.FlowchartLayout;
            set => _uiSettings.FlowchartLayout = value;
        }

        // Flowchart Window Properties - DELEGATED to WindowLayoutService (#719)
        public double FlowchartWindowLeft
        {
            get => _windowLayout.FlowchartWindowLeft;
            set => _windowLayout.FlowchartWindowLeft = value;
        }

        public double FlowchartWindowTop
        {
            get => _windowLayout.FlowchartWindowTop;
            set => _windowLayout.FlowchartWindowTop = value;
        }

        public double FlowchartWindowWidth
        {
            get => _windowLayout.FlowchartWindowWidth;
            set => _windowLayout.FlowchartWindowWidth = value;
        }

        public double FlowchartWindowHeight
        {
            get => _windowLayout.FlowchartWindowHeight;
            set => _windowLayout.FlowchartWindowHeight = value;
        }

        public bool FlowchartWindowOpen
        {
            get => _windowLayout.FlowchartWindowOpen;
            set => _windowLayout.FlowchartWindowOpen = value;
        }

        public double FlowchartPanelWidth
        {
            get => _windowLayout.FlowchartPanelWidth;
            set => _windowLayout.FlowchartPanelWidth = value;
        }

        public bool FlowchartVisible
        {
            get => _windowLayout.FlowchartVisible;
            set => _windowLayout.FlowchartVisible = value;
        }

        // Dialog Browser Panel Properties - DELEGATED to WindowLayoutService (#1143)
        public double DialogBrowserPanelWidth
        {
            get => _windowLayout.DialogBrowserPanelWidth;
            set => _windowLayout.DialogBrowserPanelWidth = value;
        }

        public bool DialogBrowserPanelVisible
        {
            get => _windowLayout.DialogBrowserPanelVisible;
            set => _windowLayout.DialogBrowserPanelVisible = value;
        }

        // Game Settings Properties - DELEGATED to shared RadoubSettings (#412)
        public string NeverwinterNightsPath
        {
            get => SharedSettings.NeverwinterNightsPath;
            set
            {
                if (SharedSettings.NeverwinterNightsPath != value)
                {
                    SharedSettings.NeverwinterNightsPath = value ?? "";
                    OnPropertyChanged();
                }
            }
        }

        public string BaseGameInstallPath
        {
            get => SharedSettings.BaseGameInstallPath;
            set
            {
                if (SharedSettings.BaseGameInstallPath != value)
                {
                    SharedSettings.BaseGameInstallPath = value ?? "";
                    OnPropertyChanged();
                }
            }
        }

        public string CurrentModulePath
        {
            get => SharedSettings.CurrentModulePath;
            set
            {
                if (SharedSettings.CurrentModulePath != value)
                {
                    SharedSettings.CurrentModulePath = value ?? "";
                    OnPropertyChanged();
                }
            }
        }

        public string TlkLanguage
        {
            get => SharedSettings.TlkLanguage;
            set
            {
                if (SharedSettings.TlkLanguage != value)
                {
                    SharedSettings.TlkLanguage = value ?? "";
                    OnPropertyChanged();
                }
            }
        }

        public bool TlkUseFemale
        {
            get => SharedSettings.TlkUseFemale;
            set
            {
                if (SharedSettings.TlkUseFemale != value)
                {
                    SharedSettings.TlkUseFemale = value;
                    OnPropertyChanged();
                }
            }
        }

        // Module paths - DELEGATED to ModulePathsService (#1269)
        public List<string> ModulePaths => _modulePaths.ModulePaths;
        public void AddModulePath(string path) => _modulePaths.AddModulePath(path);
        public void RemoveModulePath(string path) => _modulePaths.RemoveModulePath(path);
        public void ClearModulePaths() => _modulePaths.ClearModulePaths();

        // Logging Settings - DELEGATED to LoggingSettingsService (#1269)
        public int LogRetentionSessions
        {
            get => _loggingSettings.LogRetentionSessions;
            set => _loggingSettings.LogRetentionSessions = value;
        }

        public LogLevel CurrentLogLevel
        {
            get => _loggingSettings.CurrentLogLevel;
            set => _loggingSettings.CurrentLogLevel = value;
        }

        public LogLevel DebugLogFilterLevel
        {
            get => _loggingSettings.DebugLogFilterLevel;
            set => _loggingSettings.DebugLogFilterLevel = value;
        }

        public bool DebugWindowVisible
        {
            get => _loggingSettings.DebugWindowVisible;
            set => _loggingSettings.DebugWindowVisible = value;
        }

        // Auto-Save Settings - DELEGATED to EditorPreferencesService (#1269)
        public bool AutoSaveEnabled
        {
            get => _editorPreferences.AutoSaveEnabled;
            set => _editorPreferences.AutoSaveEnabled = value;
        }

        public int AutoSaveDelayMs
        {
            get => _editorPreferences.AutoSaveDelayMs;
            set => _editorPreferences.AutoSaveDelayMs = value;
        }

        public int AutoSaveIntervalMinutes
        {
            get => _editorPreferences.AutoSaveIntervalMinutes;
            set => _editorPreferences.AutoSaveIntervalMinutes = value;
        }

        public int EffectiveAutoSaveIntervalMs => _editorPreferences.EffectiveAutoSaveIntervalMs;

        // UI Settings Properties - DELEGATED to UISettingsService (#719)
        public bool AllowScrollbarAutoHide
        {
            get => _uiSettings.AllowScrollbarAutoHide;
            set => _uiSettings.AllowScrollbarAutoHide = value;
        }

        public int FlowchartNodeMaxLines
        {
            get => _uiSettings.FlowchartNodeMaxLines;
            set => _uiSettings.FlowchartNodeMaxLines = value;
        }

        public int FlowchartNodeWidth
        {
            get => _uiSettings.FlowchartNodeWidth;
            set => _uiSettings.FlowchartNodeWidth = value;
        }

        public bool TreeViewWordWrap
        {
            get => _uiSettings.TreeViewWordWrap;
            set => _uiSettings.TreeViewWordWrap = value;
        }

        public bool ShowNodeIndexNumbers
        {
            get => _uiSettings.ShowNodeIndexNumbers;
            set
            {
                _uiSettings.ShowNodeIndexNumbers = value;
                OnPropertyChanged();
            }
        }

        // NPC Speaker Visual Preferences - DELEGATED to SpeakerPreferencesService (#719)
        public Dictionary<string, SpeakerPreferences> NpcSpeakerPreferences => _speakerPreferences.Preferences;

        public void SetSpeakerPreference(string speakerTag, string? color, SpeakerVisualHelper.SpeakerShape? shape)
        {
            _speakerPreferences.SetPreference(speakerTag, color, shape);
            OnPropertyChanged(nameof(NpcSpeakerPreferences));
        }

        public (string? color, SpeakerVisualHelper.SpeakerShape? shape) GetSpeakerPreference(string speakerTag)
        {
            // If NPC tag coloring disabled, return null (use theme defaults only)
            if (!_editorPreferences.EnableNpcTagColoring)
                return (null, null);

            return _speakerPreferences.GetPreference(speakerTag);
        }

        // Editor Preferences - DELEGATED to EditorPreferencesService (#1269)
        public bool EnableNpcTagColoring
        {
            get => _editorPreferences.EnableNpcTagColoring;
            set => _editorPreferences.EnableNpcTagColoring = value;
        }

        public bool ShowDeleteConfirmation
        {
            get => _editorPreferences.ShowDeleteConfirmation;
            set => _editorPreferences.ShowDeleteConfirmation = value;
        }

        public bool SimulatorShowWarnings
        {
            get => _editorPreferences.SimulatorShowWarnings;
            set => _editorPreferences.SimulatorShowWarnings = value;
        }

        public string ExternalEditorPath
        {
            get => _editorPreferences.ExternalEditorPath;
            set => _editorPreferences.ExternalEditorPath = value;
        }

        public List<string> ScriptSearchPaths
        {
            get => _editorPreferences.ScriptSearchPaths;
            set => _editorPreferences.ScriptSearchPaths = value;
        }

        public string ManifestPath
        {
            get => _editorPreferences.ManifestPath;
            set => _editorPreferences.ManifestPath = value;
        }

        public bool EnableParameterCache
        {
            get => _editorPreferences.EnableParameterCache;
            set => _editorPreferences.EnableParameterCache = value;
        }

        public int MaxCachedValuesPerParameter
        {
            get => _editorPreferences.MaxCachedValuesPerParameter;
            set => _editorPreferences.MaxCachedValuesPerParameter = value;
        }

        public int MaxCachedScripts
        {
            get => _editorPreferences.MaxCachedScripts;
            set => _editorPreferences.MaxCachedScripts = value;
        }

        public bool SoundBrowserIncludeGameResources
        {
            get => _editorPreferences.SoundBrowserIncludeGameResources;
            set => _editorPreferences.SoundBrowserIncludeGameResources = value;
        }

        public bool SoundBrowserIncludeHakFiles
        {
            get => _editorPreferences.SoundBrowserIncludeHakFiles;
            set => _editorPreferences.SoundBrowserIncludeHakFiles = value;
        }

        public bool SoundBrowserIncludeBifFiles
        {
            get => _editorPreferences.SoundBrowserIncludeBifFiles;
            set => _editorPreferences.SoundBrowserIncludeBifFiles = value;
        }

        public bool SoundBrowserMonoOnly
        {
            get => _editorPreferences.SoundBrowserMonoOnly;
            set => _editorPreferences.SoundBrowserMonoOnly = value;
        }

        public bool SpellCheckEnabled
        {
            get => _editorPreferences.SpellCheckEnabled;
            set => _editorPreferences.SpellCheckEnabled = value;
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
                        // Initialize RecentFilesService (#719)
                        _recentFiles.Initialize(
                            SharedPathHelper.ExpandPaths(settings.RecentFiles?.ToList() ?? new List<string>()),
                            settings.MaxRecentFiles);

                        // Initialize WindowLayoutService (#719)
                        _windowLayout.Initialize(
                            settings.WindowLeft,
                            settings.WindowTop,
                            settings.WindowWidth,
                            settings.WindowHeight,
                            settings.WindowMaximized,
                            settings.LeftPanelWidth,
                            settings.TopLeftPanelHeight,
                            settings.FlowchartWindowLeft,
                            settings.FlowchartWindowTop,
                            settings.FlowchartWindowWidth,
                            settings.FlowchartWindowHeight,
                            settings.FlowchartWindowOpen,
                            settings.FlowchartPanelWidth,
                            settings.FlowchartVisible,
                            settings.DialogBrowserPanelWidth,
                            settings.DialogBrowserPanelVisible);

                        // Initialize UISettingsService (#719)
                        // Theme/font settings removed — now managed by RadoubSettings
                        _uiSettings.Initialize(
                            settings.FlowchartLayout ?? "Floating",
                            settings.AllowScrollbarAutoHide,
                            settings.FlowchartNodeMaxLines,
                            settings.TreeViewWordWrap,
                            settings.FlowchartNodeWidth,
                            settings.ShowNodeIndexNumbers);

                        // Issue #179: Migrate speaker preferences to separate file
                        _legacyNpcSpeakerPreferences = settings.NpcSpeakerPreferences;
                        if (_legacyNpcSpeakerPreferences != null && _legacyNpcSpeakerPreferences.Count > 0)
                        {
                            _speakerPreferences.MigrateFromSettingsData(_legacyNpcSpeakerPreferences);
                            _legacyNpcSpeakerPreferences = null;
                        }

                        // Initialize ModulePathsService (#1269)
                        _modulePaths.Initialize(
                            SharedPathHelper.ExpandPaths(settings.ModulePaths?.ToList() ?? new List<string>()));

                        // Initialize LoggingSettingsService (#1269)
                        _loggingSettings.Initialize(
                            settings.LogRetentionSessions,
                            settings.LogLevel,
                            settings.DebugLogFilterLevel,
                            settings.DebugWindowVisible);

                        // Initialize EditorPreferencesService (#1269)
                        _editorPreferences.Initialize(
                            settings.AutoSaveEnabled,
                            settings.AutoSaveDelayMs,
                            settings.AutoSaveIntervalMinutes,
                            settings.EnableNpcTagColoring,
                            settings.ShowDeleteConfirmation,
                            settings.SimulatorShowWarnings,
                            SharedPathHelper.ExpandPath(settings.ExternalEditorPath ?? ""),
                            SharedPathHelper.ExpandPaths(settings.ScriptSearchPaths?.ToList() ?? new List<string>()),
                            SharedPathHelper.ExpandPath(settings.ManifestPath ?? ""),
                            settings.EnableParameterCache,
                            settings.MaxCachedValuesPerParameter,
                            settings.MaxCachedScripts,
                            settings.SoundBrowserIncludeGameResources,
                            settings.SoundBrowserIncludeHakFiles,
                            settings.SoundBrowserIncludeBifFiles,
                            settings.SoundBrowserMonoOnly,
                            settings.SpellCheckEnabled);

                        // Initialize recent creature tags (#1244)
                        _recentCreatureTags = settings.RecentCreatureTags?.ToList() ?? new List<string>();

                        UnifiedLogger.LogApplication(LogLevel.INFO, $"Loaded settings: {_recentFiles.RecentFiles.Count} recent files, max={_recentFiles.MaxRecentFiles}, logLevel={_loggingSettings.CurrentLogLevel}, retention={_loggingSettings.LogRetentionSessions} sessions, autoSave={_editorPreferences.AutoSaveEnabled}, delay={_editorPreferences.AutoSaveDelayMs}ms, paramCache={_editorPreferences.EnableParameterCache}");
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
                    RecentFiles = SharedPathHelper.ContractPaths(_recentFiles.RecentFiles),
                    MaxRecentFiles = MaxRecentFiles,
                    WindowLeft = WindowLeft,
                    WindowTop = WindowTop,
                    WindowWidth = WindowWidth,
                    WindowHeight = WindowHeight,
                    WindowMaximized = WindowMaximized,
                    LeftPanelWidth = LeftPanelWidth,
                    TopLeftPanelHeight = TopLeftPanelHeight,
                    // FontSize, FontFamily, IsDarkTheme, CurrentThemeId, UseSharedTheme removed — now in RadoubSettings
                    FlowchartLayout = FlowchartLayout,
                    FlowchartWindowLeft = FlowchartWindowLeft,
                    FlowchartWindowTop = FlowchartWindowTop,
                    FlowchartWindowWidth = FlowchartWindowWidth,
                    FlowchartWindowHeight = FlowchartWindowHeight,
                    FlowchartWindowOpen = FlowchartWindowOpen,
                    FlowchartPanelWidth = FlowchartPanelWidth,
                    FlowchartVisible = FlowchartVisible,
                    DialogBrowserPanelWidth = DialogBrowserPanelWidth,
                    DialogBrowserPanelVisible = DialogBrowserPanelVisible,
                    AllowScrollbarAutoHide = AllowScrollbarAutoHide,
                    FlowchartNodeMaxLines = FlowchartNodeMaxLines,
                    FlowchartNodeWidth = FlowchartNodeWidth,
                    TreeViewWordWrap = TreeViewWordWrap,
                    ShowNodeIndexNumbers = ShowNodeIndexNumbers,
                    // Issue #179: NpcSpeakerPreferences moved to SpeakerPreferences.json
                    EnableNpcTagColoring = EnableNpcTagColoring,
                    ShowDeleteConfirmation = ShowDeleteConfirmation,
                    SimulatorShowWarnings = SimulatorShowWarnings,
                    // Issue #412: Game paths now stored in shared RadoubSettings
                    NeverwinterNightsPath = "",
                    BaseGameInstallPath = "",
                    CurrentModulePath = "",
                    ModulePaths = SharedPathHelper.ContractPaths(_modulePaths.ModulePathsInternal),
                    TlkLanguage = "",
                    TlkUseFemale = false,
                    LogRetentionSessions = LogRetentionSessions,
                    LogLevel = CurrentLogLevel,
                    DebugLogFilterLevel = DebugLogFilterLevel,
                    DebugWindowVisible = DebugWindowVisible,
                    AutoSaveEnabled = AutoSaveEnabled,
                    AutoSaveDelayMs = AutoSaveDelayMs,
                    AutoSaveIntervalMinutes = AutoSaveIntervalMinutes,
                    EnableParameterCache = EnableParameterCache,
                    MaxCachedValuesPerParameter = MaxCachedValuesPerParameter,
                    MaxCachedScripts = MaxCachedScripts,
                    SoundBrowserIncludeGameResources = SoundBrowserIncludeGameResources,
                    SoundBrowserIncludeHakFiles = SoundBrowserIncludeHakFiles,
                    SoundBrowserIncludeBifFiles = SoundBrowserIncludeBifFiles,
                    SoundBrowserMonoOnly = SoundBrowserMonoOnly,
                    SpellCheckEnabled = SpellCheckEnabled,
                    ManifestPath = SharedPathHelper.ContractPath(ManifestPath),
                    ExternalEditorPath = SharedPathHelper.ContractPath(ExternalEditorPath),
                    ScriptSearchPaths = SharedPathHelper.ContractPaths(_editorPreferences.ScriptSearchPathsInternal),
                    RecentCreatureTags = _recentCreatureTags.ToList()
                };

                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(SettingsFilePath, json);
                UnifiedLogger.LogApplication(LogLevel.TRACE, $"Settings saved to {UnifiedLogger.SanitizePath(SettingsFilePath)}");
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error saving settings: {ex.Message}");
            }
        }

        // Recent file methods - DELEGATED to RecentFilesService (#719)
        public void AddRecentFile(string filePath) => _recentFiles.AddRecentFile(filePath);
        public void RemoveRecentFile(string filePath) => _recentFiles.RemoveRecentFile(filePath);
        public void ClearRecentFiles() => _recentFiles.ClearRecentFiles();
        public void CleanupRecentFiles() => _recentFiles.CleanupRecentFiles();

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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

            // Panel layout settings
            public double LeftPanelWidth { get; set; } = 800;
            public double TopLeftPanelHeight { get; set; } = 400;

            // UI settings
            // FontSize, FontFamily, IsDarkTheme, CurrentThemeId, UseSharedTheme removed — now in RadoubSettings
            public string FlowchartLayout { get; set; } = "Floating";
            public double FlowchartWindowLeft { get; set; } = 100;
            public double FlowchartWindowTop { get; set; } = 100;
            public double FlowchartWindowWidth { get; set; } = 800;
            public double FlowchartWindowHeight { get; set; } = 600;
            public bool FlowchartWindowOpen { get; set; } = false;
            public double FlowchartPanelWidth { get; set; } = 400;
            public bool FlowchartVisible { get; set; } = false;
            public double DialogBrowserPanelWidth { get; set; } = 200;
            public bool DialogBrowserPanelVisible { get; set; } = true;
            public bool AllowScrollbarAutoHide { get; set; } = false;
            public int FlowchartNodeMaxLines { get; set; } = 3;
            public int FlowchartNodeWidth { get; set; } = 200;
            public bool TreeViewWordWrap { get; set; } = false;
            public bool ShowNodeIndexNumbers { get; set; } = false;
            public Dictionary<string, SpeakerPreferences>? NpcSpeakerPreferences { get; set; }
            public bool EnableNpcTagColoring { get; set; } = true;
            public bool ShowDeleteConfirmation { get; set; } = true;

            // Game settings
            public string NeverwinterNightsPath { get; set; } = "";
            public string BaseGameInstallPath { get; set; } = "";
            public string CurrentModulePath { get; set; } = "";
            public List<string> ModulePaths { get; set; } = new List<string>();
            public string TlkLanguage { get; set; } = "";
            public bool TlkUseFemale { get; set; } = false;

            // Logging settings
            public int LogRetentionSessions { get; set; } = 3;
            public LogLevel LogLevel { get; set; } = LogLevel.INFO;
            public LogLevel DebugLogFilterLevel { get; set; } = LogLevel.INFO;
            public bool DebugWindowVisible { get; set; } = false;

            // Auto-save settings
            public bool AutoSaveEnabled { get; set; } = true;
            public int AutoSaveDelayMs { get; set; } = 2000;
            public int AutoSaveIntervalMinutes { get; set; } = 0;

            // Parameter cache settings
            public bool EnableParameterCache { get; set; } = true;
            public int MaxCachedValuesPerParameter { get; set; } = 10;
            public int MaxCachedScripts { get; set; } = 1000;

            // Sound Browser settings
            public bool SoundBrowserIncludeGameResources { get; set; } = true;
            public bool SoundBrowserIncludeHakFiles { get; set; } = true;
            public bool SoundBrowserIncludeBifFiles { get; set; } = false;
            public bool SoundBrowserMonoOnly { get; set; } = true;

            // Spell Check settings
            public bool SpellCheckEnabled { get; set; } = true;

            // Conversation Simulator settings
            public bool SimulatorShowWarnings { get; set; } = true;

            // Radoub tool integration settings
            public string ManifestPath { get; set; } = "";

            // Script editor settings
            public string ExternalEditorPath { get; set; } = "";
            public List<string> ScriptSearchPaths { get; set; } = new List<string>();

            // Recent creature tags for character picker (#1244)
            public List<string> RecentCreatureTags { get; set; } = new List<string>();
        }
    }
}
