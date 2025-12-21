using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Runtime.CompilerServices;
using DialogEditor.Utils;

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
                    // New location: ~/Radoub/Parley (matches Manifest's ~/Radoub/Manifest pattern)
                    _settingsDirectory = Path.Combine(userProfile, "Radoub", "Parley");
                }
                return _settingsDirectory;
            }
        }

        /// <summary>
        /// Legacy settings directory (~/Parley) - used for migration
        /// </summary>
        private static string LegacySettingsDirectory
        {
            get
            {
                var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                return Path.Combine(userProfile, "Parley");
            }
        }

        private static string SettingsFilePath => Path.Combine(SettingsDirectory, "ParleySettings.json");
        private static string LegacySettingsFilePath => Path.Combine(LegacySettingsDirectory, "ParleySettings.json");
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

        // Panel layout settings - GridSplitter positions (#108)
        private double _leftPanelWidth = 800; // Tree+Text area width (default ~67% at 1200px window)
        private double _topLeftPanelHeight = 400; // Dialog tree height (default 2* of left panels)

        // UI settings
        private double _fontSize = 14;
        private string _fontFamily = ""; // Empty string = use system default
        private bool _isDarkTheme = false; // DEPRECATED: Use CurrentThemeId instead
        private string _currentThemeId = "org.parley.theme.light"; // Default theme
        private bool _useNewLayout = false; // Feature flag for new layout (#108)
        private string _flowchartLayout = "Floating"; // Flowchart layout: "Floating", "SideBySide", "Tabbed"

        // Flowchart window settings (#377)
        private double _flowchartWindowLeft = 100;
        private double _flowchartWindowTop = 100;
        private double _flowchartWindowWidth = 800;
        private double _flowchartWindowHeight = 600;
        private bool _flowchartWindowOpen = false; // Was flowchart open when app closed?
        private double _flowchartPanelWidth = 400; // Width of embedded flowchart panel (SideBySide mode)
        private bool _flowchartVisible = false; // Is flowchart visible (any mode)?
        
        // Game settings
        private string _neverwinterNightsPath = "";
        private string _baseGameInstallPath = ""; // Phase 2: Base game installation (Steam/GOG)
        private string _currentModulePath = "";
        private List<string> _modulePaths = new List<string>();
        private string _tlkLanguage = ""; // Empty = auto-detect, or "en", "de", "fr", "es", "it", "pl"
        private bool _tlkUseFemale = false; // Use dialogf.tlk (female) instead of dialog.tlk (male/default)

        // Logging settings
        private int _logRetentionSessions = 3; // Default: keep 3 most recent sessions
        private LogLevel _logLevel = LogLevel.INFO;
        private LogLevel _debugLogFilterLevel = LogLevel.INFO; // Debug window filter level
        private bool _debugWindowVisible = false; // Debug window visibility

        // Auto-save settings - Phase 1 Step 6
        private bool _autoSaveEnabled = true; // Default: ON
        private int _autoSaveDelayMs = 2000; // Default: 2 seconds (fast debounce)
        private int _autoSaveIntervalMinutes = 0; // Default: 0 = use AutoSaveDelayMs instead (Issue #62)

        // UI settings (Issue #63)
        private bool _allowScrollbarAutoHide = false; // Default: always visible

        // NPC speaker visual preferences (Issue #16, #36)
        // NOTE: Speaker preferences are now stored in SpeakerPreferencesService (Issue #179)
        // This field is only used for migration from old settings files
        private Dictionary<string, SpeakerPreferences>? _legacyNpcSpeakerPreferences = null;
        private bool _enableNpcTagColoring = true; // Default: ON (use shape/color per tag)

        // Confirmation dialog settings (Issue #14)
        private bool _showDeleteConfirmation = true; // Default: ON (show delete confirmation dialog)

        // Script editor settings
        private string _externalEditorPath = ""; // Path to external text editor (VS Code, Notepad++, etc.)
        private List<string> _scriptSearchPaths = new List<string>(); // Additional directories to search for scripts
        private bool _warnMissingScriptInDialogDirectory = true; // Warn if script not in same directory as dialog

        // Radoub tool integration settings (#416)
        private string _manifestPath = ""; // Path to Manifest.exe (journal editor)

        // Parameter cache settings
        private bool _enableParameterCache = true; // Default: ON
        private int _maxCachedValuesPerParameter = 10; // Default: 10 MRU values
        private int _maxCachedScripts = 1000; // Default: 1000 scripts

        // Sound Browser settings (#220)
        // Default to OFF - user must explicitly enable sources to scan
        private bool _soundBrowserIncludeGameResources = false;
        private bool _soundBrowserIncludeHakFiles = false;
        private bool _soundBrowserIncludeBifFiles = false;

        public event PropertyChangedEventHandler? PropertyChanged;

        private SettingsService()
        {
            // Migrate from legacy ~/Parley to new ~/Radoub/Parley location (#472)
            MigrateLegacySettingsFolder();

            LoadSettings();

            // Phase 2: Auto-detect resource paths on first run
            if (string.IsNullOrEmpty(_neverwinterNightsPath) || string.IsNullOrEmpty(_currentModulePath))
            {
                AutoDetectResourcePaths();
            }

            UnifiedLogger.LogApplication(LogLevel.INFO, "Parley SettingsService initialized");
        }

        /// <summary>
        /// Migrates settings from legacy ~/Parley folder to new ~/Radoub/Parley location.
        /// This is a one-time migration that runs on first startup after the update.
        /// </summary>
        private void MigrateLegacySettingsFolder()
        {
            try
            {
                // Check if legacy folder exists and new folder doesn't have settings yet
                if (!Directory.Exists(LegacySettingsDirectory))
                {
                    return; // No legacy settings to migrate
                }

                // Ensure new directory structure exists
                Directory.CreateDirectory(SettingsDirectory);

                // Check if migration is needed (new location doesn't have settings file)
                if (File.Exists(SettingsFilePath))
                {
                    // New settings already exist - no migration needed
                    // User may have run a newer version already
                    return;
                }

                // Log migration start (can't use UnifiedLogger yet - it depends on this service)
                Console.WriteLine($"[Parley] Migrating settings from ~/Parley to ~/Radoub/Parley...");

                // Migrate all files from legacy folder to new folder
                var filesToMigrate = new[]
                {
                    "ParleySettings.json",
                    "SpeakerPreferences.json",
                    "PluginSettings.json",
                    "parameter_cache.json",
                    "scrap.json"
                };

                foreach (var fileName in filesToMigrate)
                {
                    var legacyPath = Path.Combine(LegacySettingsDirectory, fileName);
                    var newPath = Path.Combine(SettingsDirectory, fileName);

                    if (File.Exists(legacyPath) && !File.Exists(newPath))
                    {
                        File.Copy(legacyPath, newPath);
                        Console.WriteLine($"  Migrated: {fileName}");
                    }
                }

                // Migrate Themes folder
                var legacyThemesDir = Path.Combine(LegacySettingsDirectory, "Themes");
                var newThemesDir = Path.Combine(SettingsDirectory, "Themes");
                if (Directory.Exists(legacyThemesDir) && !Directory.Exists(newThemesDir))
                {
                    CopyDirectory(legacyThemesDir, newThemesDir);
                    Console.WriteLine($"  Migrated: Themes folder");
                }

                // Don't migrate Logs folder - old logs can stay in legacy location
                // New logs will be created in new location

                // Create a marker file in legacy folder to indicate migration completed
                var markerPath = Path.Combine(LegacySettingsDirectory, ".migrated_to_radoub");
                File.WriteAllText(markerPath,
                    $"Parley settings migrated to ~/Radoub/Parley on {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                    $"This folder can be safely deleted if you don't need old log files.\n");

                Console.WriteLine($"[Parley] Settings migration complete.");
            }
            catch (Exception ex)
            {
                // Don't fail startup if migration fails - just log and continue
                Console.WriteLine($"[Parley] Warning: Settings migration failed: {ex.Message}");
                Console.WriteLine($"[Parley] Settings will be created in new location.");
            }
        }

        /// <summary>
        /// Recursively copies a directory and its contents.
        /// </summary>
        private static void CopyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var destFile = Path.Combine(destDir, Path.GetFileName(file));
                File.Copy(file, destFile, overwrite: false);
            }

            foreach (var subDir in Directory.GetDirectories(sourceDir))
            {
                var destSubDir = Path.Combine(destDir, Path.GetFileName(subDir));
                CopyDirectory(subDir, destSubDir);
            }
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

        // Panel layout properties
        public double LeftPanelWidth
        {
            get => _leftPanelWidth;
            set { if (SetProperty(ref _leftPanelWidth, Math.Max(350, value))) SaveSettings(); }
        }

        public double TopLeftPanelHeight
        {
            get => _topLeftPanelHeight;
            set { if (SetProperty(ref _topLeftPanelHeight, Math.Max(150, value))) SaveSettings(); }
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

        /// <summary>
        /// DEPRECATED: Use CurrentThemeId instead. Kept for backwards compatibility.
        /// </summary>
        public bool IsDarkTheme
        {
            get => _isDarkTheme;
            set
            {
                if (SetProperty(ref _isDarkTheme, value))
                {
                    // Auto-migrate to new theme system
                    _currentThemeId = value ? "org.parley.theme.dark" : "org.parley.theme.light";
                    OnPropertyChanged(nameof(CurrentThemeId));
                    SaveSettings();
                }
            }
        }

        /// <summary>
        /// Current theme plugin ID (e.g., "org.parley.theme.light")
        /// </summary>
        public string CurrentThemeId
        {
            get => _currentThemeId;
            set
            {
                if (SetProperty(ref _currentThemeId, value))
                {
                    // Update legacy IsDarkTheme for compatibility
                    _isDarkTheme = value.Contains("dark", StringComparison.OrdinalIgnoreCase);
                    SaveSettings();
                }
            }
        }

        public bool UseNewLayout
        {
            get => _useNewLayout;
            set { if (SetProperty(ref _useNewLayout, value)) SaveSettings(); }
        }

        /// <summary>
        /// Flowchart layout mode: "Floating" (separate window), "SideBySide" (split view), "Tabbed" (tab in main area)
        /// </summary>
        public string FlowchartLayout
        {
            get => _flowchartLayout;
            set
            {
                // Validate value
                var validValues = new[] { "Floating", "SideBySide", "Tabbed" };
                var safeValue = validValues.Contains(value) ? value : "Floating";
                if (SetProperty(ref _flowchartLayout, safeValue))
                {
                    SaveSettings();
                    UnifiedLogger.LogUI(LogLevel.INFO, $"Flowchart layout set to {safeValue}");
                }
            }
        }

        // Flowchart Window Properties (#377)
        public double FlowchartWindowLeft
        {
            get => _flowchartWindowLeft;
            set { if (SetProperty(ref _flowchartWindowLeft, value)) SaveSettings(); }
        }

        public double FlowchartWindowTop
        {
            get => _flowchartWindowTop;
            set { if (SetProperty(ref _flowchartWindowTop, value)) SaveSettings(); }
        }

        public double FlowchartWindowWidth
        {
            get => _flowchartWindowWidth;
            set { if (SetProperty(ref _flowchartWindowWidth, Math.Max(200, value))) SaveSettings(); }
        }

        public double FlowchartWindowHeight
        {
            get => _flowchartWindowHeight;
            set { if (SetProperty(ref _flowchartWindowHeight, Math.Max(150, value))) SaveSettings(); }
        }

        public bool FlowchartWindowOpen
        {
            get => _flowchartWindowOpen;
            set { if (SetProperty(ref _flowchartWindowOpen, value)) SaveSettings(); }
        }

        public double FlowchartPanelWidth
        {
            get => _flowchartPanelWidth;
            set { if (SetProperty(ref _flowchartPanelWidth, Math.Max(200, value))) SaveSettings(); }
        }

        public bool FlowchartVisible
        {
            get => _flowchartVisible;
            set { if (SetProperty(ref _flowchartVisible, value)) SaveSettings(); }
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

        /// <summary>
        /// TLK language preference. Empty = auto-detect, or specify: "en", "de", "fr", "es", "it", "pl"
        /// </summary>
        public string TlkLanguage
        {
            get => _tlkLanguage;
            set { if (SetProperty(ref _tlkLanguage, value ?? "")) SaveSettings(); }
        }

        /// <summary>
        /// Use female TLK variant (dialogf.tlk) instead of default (dialog.tlk).
        /// Some languages have gendered text variants.
        /// </summary>
        public bool TlkUseFemale
        {
            get => _tlkUseFemale;
            set { if (SetProperty(ref _tlkUseFemale, value)) SaveSettings(); }
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

        public LogLevel DebugLogFilterLevel
        {
            get => _debugLogFilterLevel;
            set
            {
                if (SetProperty(ref _debugLogFilterLevel, value))
                {
                    SaveSettings();
                    UnifiedLogger.LogSettings(LogLevel.DEBUG, $"Debug log filter level set to {value}");
                }
            }
        }

        public bool DebugWindowVisible
        {
            get => _debugWindowVisible;
            set
            {
                if (SetProperty(ref _debugWindowVisible, value))
                {
                    SaveSettings();
                    UnifiedLogger.LogSettings(LogLevel.DEBUG, $"Debug window visibility set to {value}");
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

        /// <summary>
        /// Auto-save interval in minutes (Issue #62).
        /// 0 = use AutoSaveDelayMs (fast debounce, default).
        /// 1-60 = timer-based autosave every N minutes.
        /// </summary>
        public int AutoSaveIntervalMinutes
        {
            get => _autoSaveIntervalMinutes;
            set
            {
                // Clamp to reasonable bounds (0-60 minutes, 0 = disabled/use fast debounce)
                var clampedValue = Math.Max(0, Math.Min(60, value));
                if (SetProperty(ref _autoSaveIntervalMinutes, clampedValue))
                {
                    SaveSettings();
                    UnifiedLogger.LogApplication(LogLevel.INFO, $"Auto-save interval set to {clampedValue} minutes");
                }
            }
        }

        /// <summary>
        /// Gets the effective autosave interval in milliseconds based on configuration.
        /// If AutoSaveIntervalMinutes > 0, converts to milliseconds.
        /// Otherwise, uses AutoSaveDelayMs (fast debounce).
        /// </summary>
        public int EffectiveAutoSaveIntervalMs
        {
            get
            {
                if (_autoSaveIntervalMinutes > 0)
                {
                    // Convert minutes to milliseconds
                    return _autoSaveIntervalMinutes * 60 * 1000;
                }
                return _autoSaveDelayMs; // Default: fast debounce
            }
        }

        // UI Settings Properties (Issue #63)
        public bool AllowScrollbarAutoHide
        {
            get => _allowScrollbarAutoHide;
            set
            {
                if (SetProperty(ref _allowScrollbarAutoHide, value))
                {
                    SaveSettings();
                    OnPropertyChanged(nameof(AllowScrollbarAutoHide));
                }
            }
        }

        // NPC Speaker Visual Preferences (Issue #16, #36, #179)
        // Now delegates to SpeakerPreferencesService for storage in separate file
        public Dictionary<string, SpeakerPreferences> NpcSpeakerPreferences
        {
            get => SpeakerPreferencesService.Instance.Preferences;
        }

        public void SetSpeakerPreference(string speakerTag, string? color, SpeakerVisualHelper.SpeakerShape? shape)
        {
            SpeakerPreferencesService.Instance.SetPreference(speakerTag, color, shape);
            OnPropertyChanged(nameof(NpcSpeakerPreferences));
        }

        public (string? color, SpeakerVisualHelper.SpeakerShape? shape) GetSpeakerPreference(string speakerTag)
        {
            // If NPC tag coloring disabled, return null (use theme defaults only)
            if (!_enableNpcTagColoring)
                return (null, null);

            return SpeakerPreferencesService.Instance.GetPreference(speakerTag);
        }

        public bool EnableNpcTagColoring
        {
            get => _enableNpcTagColoring;
            set
            {
                if (SetProperty(ref _enableNpcTagColoring, value))
                {
                    SaveSettings();
                    OnPropertyChanged(nameof(EnableNpcTagColoring));
                }
            }
        }

        public bool ShowDeleteConfirmation
        {
            get => _showDeleteConfirmation;
            set
            {
                if (SetProperty(ref _showDeleteConfirmation, value))
                {
                    SaveSettings();
                }
            }
        }

        // Script Editor Settings Properties
        public string ExternalEditorPath
        {
            get => _externalEditorPath;
            set { if (SetProperty(ref _externalEditorPath, value ?? "")) SaveSettings(); }
        }

        public List<string> ScriptSearchPaths
        {
            get => _scriptSearchPaths;
            set
            {
                _scriptSearchPaths = value ?? new List<string>();
                OnPropertyChanged(nameof(ScriptSearchPaths));
                SaveSettings();
            }
        }

        public bool WarnMissingScriptInDialogDirectory
        {
            get => _warnMissingScriptInDialogDirectory;
            set { if (SetProperty(ref _warnMissingScriptInDialogDirectory, value)) SaveSettings(); }
        }

        // Radoub Tool Integration Properties (#416)
        public string ManifestPath
        {
            get => _manifestPath;
            set { if (SetProperty(ref _manifestPath, value ?? "")) SaveSettings(); }
        }

        // Parameter Cache Settings Properties
        public bool EnableParameterCache
        {
            get => _enableParameterCache;
            set
            {
                if (SetProperty(ref _enableParameterCache, value))
                {
                    ParameterCacheService.Instance.EnableCaching = value;
                    SaveSettings();
                    UnifiedLogger.LogApplication(LogLevel.INFO, $"Parameter cache {(value ? "enabled" : "disabled")}");
                }
            }
        }

        public int MaxCachedValuesPerParameter
        {
            get => _maxCachedValuesPerParameter;
            set
            {
                // Clamp between 5-50 values
                if (SetProperty(ref _maxCachedValuesPerParameter, Math.Max(5, Math.Min(50, value))))
                {
                    ParameterCacheService.Instance.MaxValuesPerParameter = value;
                    SaveSettings();
                    UnifiedLogger.LogApplication(LogLevel.INFO, $"Max cached values per parameter set to {value}");
                }
            }
        }

        public int MaxCachedScripts
        {
            get => _maxCachedScripts;
            set
            {
                // Clamp between 100-10000 scripts
                if (SetProperty(ref _maxCachedScripts, Math.Max(100, Math.Min(10000, value))))
                {
                    ParameterCacheService.Instance.MaxScriptsInCache = value;
                    SaveSettings();
                    UnifiedLogger.LogApplication(LogLevel.INFO, $"Max cached scripts set to {value}");
                }
            }
        }

        // Sound Browser Settings Properties (#220)
        public bool SoundBrowserIncludeGameResources
        {
            get => _soundBrowserIncludeGameResources;
            set { if (SetProperty(ref _soundBrowserIncludeGameResources, value)) SaveSettings(); }
        }

        public bool SoundBrowserIncludeHakFiles
        {
            get => _soundBrowserIncludeHakFiles;
            set { if (SetProperty(ref _soundBrowserIncludeHakFiles, value)) SaveSettings(); }
        }

        public bool SoundBrowserIncludeBifFiles
        {
            get => _soundBrowserIncludeBifFiles;
            set { if (SetProperty(ref _soundBrowserIncludeBifFiles, value)) SaveSettings(); }
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
                        _recentFiles = ExpandPaths(settings.RecentFiles?.ToList() ?? new List<string>());
                        _maxRecentFiles = Math.Max(1, Math.Min(20, settings.MaxRecentFiles));

                        // Load window settings
                        _windowLeft = settings.WindowLeft;
                        _windowTop = settings.WindowTop;
                        _windowWidth = Math.Max(400, settings.WindowWidth);
                        _windowHeight = Math.Max(300, settings.WindowHeight);
                        _windowMaximized = settings.WindowMaximized;

                        UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Loaded window position from settings: Left={_windowLeft}, Top={_windowTop}, Width={_windowWidth}, Height={_windowHeight}");

                        // Load panel layout settings
                        _leftPanelWidth = Math.Max(350, settings.LeftPanelWidth);
                        _topLeftPanelHeight = Math.Max(150, settings.TopLeftPanelHeight);

                        // Load UI settings
                        _fontSize = Math.Max(8, Math.Min(24, settings.FontSize));
                        _fontFamily = settings.FontFamily ?? "";

                        // Migrate from old IsDarkTheme to new CurrentThemeId
                        if (!string.IsNullOrEmpty(settings.CurrentThemeId))
                        {
                            _currentThemeId = settings.CurrentThemeId;
                            _isDarkTheme = settings.IsDarkTheme; // Keep for compatibility
                        }
                        else
                        {
                            // Old settings file - migrate
                            _isDarkTheme = settings.IsDarkTheme;
                            _currentThemeId = _isDarkTheme ? "org.parley.theme.dark" : "org.parley.theme.light";
                        }

                        _useNewLayout = settings.UseNewLayout;
                        _flowchartLayout = settings.FlowchartLayout ?? "Floating"; // #329: Flowchart layout
                        // Flowchart window settings (#377)
                        _flowchartWindowLeft = settings.FlowchartWindowLeft;
                        _flowchartWindowTop = settings.FlowchartWindowTop;
                        _flowchartWindowWidth = Math.Max(200, settings.FlowchartWindowWidth);
                        _flowchartWindowHeight = Math.Max(150, settings.FlowchartWindowHeight);
                        _flowchartWindowOpen = settings.FlowchartWindowOpen;
                        _flowchartPanelWidth = Math.Max(200, settings.FlowchartPanelWidth);
                        _flowchartVisible = settings.FlowchartVisible;
                        _allowScrollbarAutoHide = settings.AllowScrollbarAutoHide; // Issue #63

                        // Issue #179: Migrate speaker preferences to separate file
                        // Store temporarily for migration, then clear from main settings
                        _legacyNpcSpeakerPreferences = settings.NpcSpeakerPreferences;
                        if (_legacyNpcSpeakerPreferences != null && _legacyNpcSpeakerPreferences.Count > 0)
                        {
                            SpeakerPreferencesService.Instance.MigrateFromSettingsData(_legacyNpcSpeakerPreferences);
                            _legacyNpcSpeakerPreferences = null; // Clear after migration
                        }

                        _enableNpcTagColoring = settings.EnableNpcTagColoring; // Issue #16, #36
                        _showDeleteConfirmation = settings.ShowDeleteConfirmation; // Issue #14

                        // Load game settings (expand ~ to user home directory)
                        _neverwinterNightsPath = ExpandPath(settings.NeverwinterNightsPath ?? "");
                        _baseGameInstallPath = ExpandPath(settings.BaseGameInstallPath ?? ""); // Phase 2
                        _currentModulePath = ExpandPath(settings.CurrentModulePath ?? "");
                        _modulePaths = ExpandPaths(settings.ModulePaths?.ToList() ?? new List<string>());
                        _tlkLanguage = settings.TlkLanguage ?? ""; // TLK language preference
                        _tlkUseFemale = settings.TlkUseFemale; // TLK gender preference

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

                        // Load debug window settings
                        _debugLogFilterLevel = settings.DebugLogFilterLevel;
                        _debugWindowVisible = settings.DebugWindowVisible;

                        // Load auto-save settings - Phase 1 Step 6 + Issue #62
                        _autoSaveEnabled = settings.AutoSaveEnabled;
                        _autoSaveDelayMs = Math.Max(1000, Math.Min(10000, settings.AutoSaveDelayMs));
                        _autoSaveIntervalMinutes = Math.Max(0, Math.Min(60, settings.AutoSaveIntervalMinutes));

                        // Load parameter cache settings
                        _enableParameterCache = settings.EnableParameterCache;
                        _maxCachedValuesPerParameter = Math.Max(5, Math.Min(50, settings.MaxCachedValuesPerParameter));
                        _maxCachedScripts = Math.Max(100, Math.Min(10000, settings.MaxCachedScripts));

                        // Apply parameter cache settings
                        ParameterCacheService.Instance.EnableCaching = _enableParameterCache;
                        ParameterCacheService.Instance.MaxValuesPerParameter = _maxCachedValuesPerParameter;
                        ParameterCacheService.Instance.MaxScriptsInCache = _maxCachedScripts;

                        // Load Sound Browser settings (#220)
                        _soundBrowserIncludeGameResources = settings.SoundBrowserIncludeGameResources;
                        _soundBrowserIncludeHakFiles = settings.SoundBrowserIncludeHakFiles;
                        _soundBrowserIncludeBifFiles = settings.SoundBrowserIncludeBifFiles;

                        // Load Radoub tool integration settings (#416)
                        _manifestPath = ExpandPath(settings.ManifestPath ?? "");

                        UnifiedLogger.LogApplication(LogLevel.INFO, $"Loaded settings: {_recentFiles.Count} recent files, max={_maxRecentFiles}, theme={(_isDarkTheme ? "dark" : "light")}, logLevel={_logLevel}, retention={_logRetentionSessions} sessions, autoSave={_autoSaveEnabled}, delay={_autoSaveDelayMs}ms, paramCache={_enableParameterCache}");
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
                    RecentFiles = ContractPaths(_recentFiles), // Use ~ for home directory
                    MaxRecentFiles = MaxRecentFiles,
                    WindowLeft = WindowLeft,
                    WindowTop = WindowTop,
                    WindowWidth = WindowWidth,
                    WindowHeight = WindowHeight,
                    WindowMaximized = WindowMaximized,
                    LeftPanelWidth = LeftPanelWidth,
                    TopLeftPanelHeight = TopLeftPanelHeight,
                    FontSize = FontSize,
                    FontFamily = FontFamily,
                    IsDarkTheme = IsDarkTheme, // Keep for backwards compatibility
                    CurrentThemeId = CurrentThemeId,
                    UseNewLayout = UseNewLayout,
                    FlowchartLayout = FlowchartLayout, // #329: Flowchart layout
                    // Flowchart window settings (#377)
                    FlowchartWindowLeft = FlowchartWindowLeft,
                    FlowchartWindowTop = FlowchartWindowTop,
                    FlowchartWindowWidth = FlowchartWindowWidth,
                    FlowchartWindowHeight = FlowchartWindowHeight,
                    FlowchartWindowOpen = FlowchartWindowOpen,
                    FlowchartPanelWidth = FlowchartPanelWidth,
                    FlowchartVisible = FlowchartVisible,
                    AllowScrollbarAutoHide = AllowScrollbarAutoHide, // Issue #63
                    // Issue #179: NpcSpeakerPreferences moved to SpeakerPreferences.json
                    // Keep NpcSpeakerPreferences = null to avoid saving back to main settings
                    EnableNpcTagColoring = EnableNpcTagColoring, // Issue #16, #36
                    ShowDeleteConfirmation = ShowDeleteConfirmation, // Issue #14
                    NeverwinterNightsPath = ContractPath(NeverwinterNightsPath), // Use ~ for home directory
                    BaseGameInstallPath = ContractPath(BaseGameInstallPath), // Use ~ for home directory
                    CurrentModulePath = ContractPath(CurrentModulePath), // Use ~ for home directory
                    ModulePaths = ContractPaths(_modulePaths), // Use ~ for home directory
                    TlkLanguage = TlkLanguage, // TLK language preference
                    TlkUseFemale = TlkUseFemale, // TLK gender preference
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
                    // Sound Browser settings (#220)
                    SoundBrowserIncludeGameResources = SoundBrowserIncludeGameResources,
                    SoundBrowserIncludeHakFiles = SoundBrowserIncludeHakFiles,
                    SoundBrowserIncludeBifFiles = SoundBrowserIncludeBifFiles,
                    // Radoub tool integration (#416)
                    ManifestPath = ContractPath(ManifestPath)
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
        /// This makes settings files portable and privacy-safe for sharing
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

        /// <summary>
        /// Contracts a list of paths for storage
        /// </summary>
        private static List<string> ContractPaths(List<string> paths)
        {
            return paths.Select(ContractPath).ToList();
        }

        /// <summary>
        /// Expands a list of paths from storage
        /// </summary>
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
            public double WindowWidth { get; set; } = 1200;
            public double WindowHeight { get; set; } = 800;
            public bool WindowMaximized { get; set; } = false;

            // Panel layout settings
            public double LeftPanelWidth { get; set; } = 800;
            public double TopLeftPanelHeight { get; set; } = 400;

            // UI settings
            public double FontSize { get; set; } = 14;
            public string FontFamily { get; set; } = "";
            public bool IsDarkTheme { get; set; } = false; // DEPRECATED: For backwards compatibility
            public string? CurrentThemeId { get; set; } = "org.parley.theme.light";
            public bool UseNewLayout { get; set; } = false;
            public string FlowchartLayout { get; set; } = "Floating"; // #329: Flowchart layout
            // Flowchart window settings (#377)
            public double FlowchartWindowLeft { get; set; } = 100;
            public double FlowchartWindowTop { get; set; } = 100;
            public double FlowchartWindowWidth { get; set; } = 800;
            public double FlowchartWindowHeight { get; set; } = 600;
            public bool FlowchartWindowOpen { get; set; } = false;
            public double FlowchartPanelWidth { get; set; } = 400;
            public bool FlowchartVisible { get; set; } = false;
            public bool AllowScrollbarAutoHide { get; set; } = false; // Issue #63: Default always visible
            public Dictionary<string, SpeakerPreferences>? NpcSpeakerPreferences { get; set; } // Issue #16, #36
            public bool EnableNpcTagColoring { get; set; } = true; // Issue #16, #36: Default ON
            public bool ShowDeleteConfirmation { get; set; } = true; // Issue #14: Default ON

            // Game settings
            public string NeverwinterNightsPath { get; set; } = "";
            public string BaseGameInstallPath { get; set; } = ""; // Phase 2: Base game installation
            public string CurrentModulePath { get; set; } = "";
            public List<string> ModulePaths { get; set; } = new List<string>();
            public string TlkLanguage { get; set; } = ""; // TLK language: "", "en", "de", "fr", "es", "it", "pl"
            public bool TlkUseFemale { get; set; } = false; // Use dialogf.tlk (female) instead of dialog.tlk

            // Logging settings
            public int LogRetentionSessions { get; set; } = 3; // Keep 3 most recent sessions
            public LogLevel LogLevel { get; set; } = LogLevel.INFO;
            public LogLevel DebugLogFilterLevel { get; set; } = LogLevel.INFO; // Debug window filter
            public bool DebugWindowVisible { get; set; } = false; // Debug window visibility

            // Auto-save settings - Phase 1 Step 6 + Issue #62
            public bool AutoSaveEnabled { get; set; } = true;
            public int AutoSaveDelayMs { get; set; } = 2000;
            public int AutoSaveIntervalMinutes { get; set; } = 0; // 0 = use fast debounce

            // Parameter cache settings
            public bool EnableParameterCache { get; set; } = true;
            public int MaxCachedValuesPerParameter { get; set; } = 10;
            public int MaxCachedScripts { get; set; } = 1000;

            // Sound Browser settings (#220) - default OFF until user enables
            public bool SoundBrowserIncludeGameResources { get; set; } = false;
            public bool SoundBrowserIncludeHakFiles { get; set; } = false;
            public bool SoundBrowserIncludeBifFiles { get; set; } = false;

            // Radoub tool integration settings (#416)
            public string ManifestPath { get; set; } = "";
        }
    }
}