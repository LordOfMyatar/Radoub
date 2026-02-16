using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Radoub.Formats.Common;
using Radoub.Formats.Logging;

namespace Radoub.Formats.Settings;

/// <summary>
/// Shared settings for all Radoub tools.
/// Stores game paths and TLK configuration in ~/Radoub/RadoubSettings.json
/// Individual tools have their own settings files for tool-specific preferences.
/// </summary>
public class RadoubSettings : INotifyPropertyChanged
{
    private static RadoubSettings? _instance;
    private static readonly object _lock = new();

    /// <summary>
    /// Singleton instance of shared settings.
    /// </summary>
    public static RadoubSettings Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new RadoubSettings();
                }
            }
            return _instance;
        }
    }

    private static string? _settingsDirectory;
    private static string SettingsDirectory
    {
        get
        {
            if (_settingsDirectory == null)
            {
                // Check for test override first (allows UI tests to use isolated settings)
                var testDir = Environment.GetEnvironmentVariable("RADOUB_SETTINGS_DIR");
                if (!string.IsNullOrEmpty(testDir))
                {
                    _settingsDirectory = testDir;
                }
                else
                {
                    var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    _settingsDirectory = Path.Combine(userProfile, "Radoub");
                }
            }
            return _settingsDirectory;
        }
    }

    /// <summary>
    /// Reset the singleton instance for testing. Clears instance and settings directory cache.
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
    /// Configure an isolated settings directory for testing.
    /// Must be called before first Instance access.
    /// </summary>
    public static void ConfigureForTesting(string testDirectory)
    {
        lock (_lock)
        {
            _settingsDirectory = testDirectory;
        }
    }

    private static string SettingsFilePath => Path.Combine(SettingsDirectory, "RadoubSettings.json");

    // Game installation paths
    private string _baseGameInstallPath = "";
    private string _neverwinterNightsPath = "";
    private string _currentModulePath = "";

    // Runtime-only: Current module's DefaultBic (not persisted)
    // Used by MainWindow to disable Load Module when DefaultBic is set
    private string _currentModuleDefaultBic = "";

    // Custom content paths
    private string _customTlkPath = "";  // Path to custom TLK file (module-specific)
    private List<string> _hakSearchPaths = new();  // Additional HAK search paths

    // TLK settings
    private string _tlkLanguage = "";  // Empty = auto-detect from OS or default to English
    private bool _tlkUseFemale = false;
    private Language _defaultLanguage = Language.English;  // Default display language

    // Theme settings (shared across all tools)
    private string _sharedThemeId = "";  // Empty = no shared theme (tools use their own)
    private bool _useSharedTheme = true;  // If true, tools prefer shared theme over tool-specific

    // Logging settings (shared across all tools)
    private LogLevel _sharedLogLevel = LogLevel.INFO;
    private int _sharedLogRetentionSessions = 3;
    private bool _useSharedLogging = true;  // If true, tools use shared logging settings

    // Tool paths - auto-populated when tools run, used for cross-tool integration
    private string _parleyPath = "";
    private string _manifestPath = "";
    private string _quartermasterPath = "";
    private string _fencePath = "";
    private string _trebuchetPath = "";

    public event PropertyChangedEventHandler? PropertyChanged;

    private RadoubSettings()
    {
        LoadSettings();

        // Auto-detect paths on first run
        if (string.IsNullOrEmpty(_baseGameInstallPath))
        {
            AutoDetectPaths();
        }
    }

    /// <summary>
    /// Reload settings from disk. Used when another process (e.g., Trebuchet)
    /// may have updated RadoubSettings.json since this instance was created. (#1384)
    /// </summary>
    public void ReloadSettings()
    {
        LoadSettings();
        UnifiedLogger.Log(LogLevel.DEBUG,
            $"Reloaded settings - Module: {(string.IsNullOrEmpty(_currentModulePath) ? "(none)" : UnifiedLogger.SanitizePath(_currentModulePath))}, " +
            $"TLK: {(string.IsNullOrEmpty(_customTlkPath) ? "(none)" : UnifiedLogger.SanitizePath(_customTlkPath))}",
            "RadoubSettings", "Settings");
    }

    /// <summary>
    /// Base game installation path (Steam/GOG - contains data\ folder with BIF/KEY files).
    /// </summary>
    public string BaseGameInstallPath
    {
        get => _baseGameInstallPath;
        set { if (SetProperty(ref _baseGameInstallPath, value ?? "")) SaveSettings(); }
    }

    /// <summary>
    /// User documents path (contains modules, override, portraits, etc.).
    /// Typically: ~/Documents/Neverwinter Nights (Windows) or ~/.local/share/Neverwinter Nights (Linux)
    /// </summary>
    public string NeverwinterNightsPath
    {
        get => _neverwinterNightsPath;
        set { if (SetProperty(ref _neverwinterNightsPath, value ?? "")) SaveSettings(); }
    }

    /// <summary>
    /// Currently active module path.
    /// </summary>
    public string CurrentModulePath
    {
        get => _currentModulePath;
        set { if (SetProperty(ref _currentModulePath, value ?? "")) SaveSettings(); }
    }

    /// <summary>
    /// The DefaultBic value from the current module's IFO (runtime only, not persisted).
    /// When set, indicates the module uses a pre-generated character for testing.
    /// Used by MainWindow to disable "Load Module" button (only "Test Module" works with DefaultBic).
    /// </summary>
    public string CurrentModuleDefaultBic
    {
        get => _currentModuleDefaultBic;
        set => SetProperty(ref _currentModuleDefaultBic, value ?? "");
    }

    /// <summary>
    /// Path to custom TLK file for the current module.
    /// Module's CustomTlk reference (from module.ifo) is a name without extension;
    /// this is the full resolved path to the actual .tlk file.
    /// Empty = no custom TLK loaded.
    /// </summary>
    public string CustomTlkPath
    {
        get => _customTlkPath;
        set { if (SetProperty(ref _customTlkPath, value ?? "")) SaveSettings(); }
    }

    /// <summary>
    /// Additional search paths for HAK files, beyond the default NeverwinterNightsPath/hak/.
    /// Useful for custom content packs (CEP, PRC, etc.) stored in non-standard locations.
    /// Paths are searched in order: default hak folder first, then these paths.
    /// </summary>
    public IReadOnlyList<string> HakSearchPaths => _hakSearchPaths.AsReadOnly();

    /// <summary>
    /// Add a HAK search path if not already present.
    /// </summary>
    public void AddHakSearchPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        var normalized = Path.GetFullPath(path);
        if (!_hakSearchPaths.Contains(normalized, StringComparer.OrdinalIgnoreCase))
        {
            _hakSearchPaths.Add(normalized);
            OnPropertyChanged(nameof(HakSearchPaths));
            SaveSettings();
        }
    }

    /// <summary>
    /// Remove a HAK search path.
    /// </summary>
    public bool RemoveHakSearchPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;

        var normalized = Path.GetFullPath(path);
        var index = _hakSearchPaths.FindIndex(p => string.Equals(p, normalized, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
        {
            _hakSearchPaths.RemoveAt(index);
            OnPropertyChanged(nameof(HakSearchPaths));
            SaveSettings();
            return true;
        }
        return false;
    }

    /// <summary>
    /// Clear all additional HAK search paths.
    /// </summary>
    public void ClearHakSearchPaths()
    {
        if (_hakSearchPaths.Count > 0)
        {
            _hakSearchPaths.Clear();
            OnPropertyChanged(nameof(HakSearchPaths));
            SaveSettings();
        }
    }

    /// <summary>
    /// Set all HAK search paths at once.
    /// </summary>
    public void SetHakSearchPaths(IEnumerable<string> paths)
    {
        _hakSearchPaths.Clear();
        foreach (var path in paths)
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                var normalized = Path.GetFullPath(path);
                if (!_hakSearchPaths.Contains(normalized, StringComparer.OrdinalIgnoreCase))
                {
                    _hakSearchPaths.Add(normalized);
                }
            }
        }
        OnPropertyChanged(nameof(HakSearchPaths));
        SaveSettings();
    }

    /// <summary>
    /// Get all HAK search paths including the default hak folder.
    /// Returns paths in search order: default hak folder first, then additional paths.
    /// </summary>
    public IEnumerable<string> GetAllHakSearchPaths()
    {
        // Default hak folder from NeverwinterNightsPath
        if (!string.IsNullOrEmpty(_neverwinterNightsPath))
        {
            var defaultHakPath = Path.Combine(_neverwinterNightsPath, "hak");
            if (Directory.Exists(defaultHakPath))
            {
                yield return defaultHakPath;
            }
        }

        // Additional search paths
        foreach (var path in _hakSearchPaths)
        {
            if (Directory.Exists(path))
            {
                yield return path;
            }
        }
    }

    /// <summary>
    /// TLK language preference. Empty = auto-detect.
    /// Valid values: "en", "fr", "de", "it", "es", "pl"
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

    /// <summary>
    /// Default display language for localized strings.
    /// This is used when viewing/editing strings and as fallback for TLK resolution.
    /// Default: English. Can be changed to match user's OS locale or preference.
    /// </summary>
    public Language DefaultLanguage
    {
        get => _defaultLanguage;
        set { if (SetProperty(ref _defaultLanguage, value)) SaveSettings(); }
    }

    /// <summary>
    /// Get the preferred language as enum, or DefaultLanguage if auto-detect.
    /// </summary>
    public Language? PreferredLanguage
    {
        get => string.IsNullOrEmpty(_tlkLanguage) ? null : LanguageHelper.FromLanguageCode(_tlkLanguage);
    }

    /// <summary>
    /// Get the effective language for display (PreferredLanguage or DefaultLanguage).
    /// </summary>
    public Language EffectiveLanguage => PreferredLanguage ?? _defaultLanguage;

    /// <summary>
    /// Get the preferred gender for TLK strings.
    /// </summary>
    public Gender PreferredGender => _tlkUseFemale ? Gender.Female : Gender.Male;

    /// <summary>
    /// Shared theme ID applied to all tools.
    /// Empty = no shared theme (tools use their own settings).
    /// </summary>
    public string SharedThemeId
    {
        get => _sharedThemeId;
        set { if (SetProperty(ref _sharedThemeId, value ?? "")) SaveSettings(); }
    }

    /// <summary>
    /// If true, tools prefer the shared theme over their tool-specific setting.
    /// Tools can still override by setting this to false in their own settings.
    /// </summary>
    public bool UseSharedTheme
    {
        get => _useSharedTheme;
        set { if (SetProperty(ref _useSharedTheme, value)) SaveSettings(); }
    }

    /// <summary>
    /// Check if a shared theme is configured.
    /// </summary>
    public bool HasSharedTheme => _useSharedTheme && !string.IsNullOrEmpty(_sharedThemeId);

    /// <summary>
    /// Shared log level for all tools.
    /// </summary>
    public LogLevel SharedLogLevel
    {
        get => _sharedLogLevel;
        set { if (SetProperty(ref _sharedLogLevel, value)) SaveSettings(); }
    }

    /// <summary>
    /// Shared log retention sessions (1-10).
    /// </summary>
    public int SharedLogRetentionSessions
    {
        get => _sharedLogRetentionSessions;
        set
        {
            var clamped = Math.Max(1, Math.Min(10, value));
            if (SetProperty(ref _sharedLogRetentionSessions, clamped)) SaveSettings();
        }
    }

    /// <summary>
    /// If true, tools use shared logging settings instead of their own.
    /// </summary>
    public bool UseSharedLogging
    {
        get => _useSharedLogging;
        set { if (SetProperty(ref _useSharedLogging, value)) SaveSettings(); }
    }

    /// <summary>
    /// Check if shared logging is enabled.
    /// </summary>
    public bool HasSharedLogging => _useSharedLogging;

    /// <summary>
    /// Get a LoggingSettings instance from shared settings.
    /// Tools can use this to initialize their logging.
    /// </summary>
    public LoggingSettings GetSharedLoggingSettings()
    {
        return new LoggingSettings
        {
            LogLevel = _sharedLogLevel,
            LogRetentionSessions = _sharedLogRetentionSessions
        };
    }

    /// <summary>
    /// Get the path to Radoub-level themes folder.
    /// Location: ~/Radoub/Themes/
    /// </summary>
    public string GetSharedThemesPath()
    {
        var path = Path.Combine(SettingsDirectory, "Themes");
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
        return path;
    }

    // Tool path properties - auto-populated by tools for cross-tool discovery

    /// <summary>
    /// Path to Parley.exe. Auto-set when Parley runs.
    /// </summary>
    public string ParleyPath
    {
        get => _parleyPath;
        set { if (SetProperty(ref _parleyPath, value ?? "")) SaveSettings(); }
    }

    /// <summary>
    /// Path to Manifest.exe. Auto-set when Manifest runs.
    /// </summary>
    public string ManifestPath
    {
        get => _manifestPath;
        set { if (SetProperty(ref _manifestPath, value ?? "")) SaveSettings(); }
    }

    /// <summary>
    /// Path to Quartermaster.exe. Auto-set when Quartermaster runs.
    /// </summary>
    public string QuartermasterPath
    {
        get => _quartermasterPath;
        set { if (SetProperty(ref _quartermasterPath, value ?? "")) SaveSettings(); }
    }

    /// <summary>
    /// Path to Fence.exe. Auto-set when Fence runs.
    /// </summary>
    public string FencePath
    {
        get => _fencePath;
        set { if (SetProperty(ref _fencePath, value ?? "")) SaveSettings(); }
    }

    /// <summary>
    /// Path to Trebuchet.exe. Auto-set when Trebuchet runs.
    /// </summary>
    public string TrebuchetPath
    {
        get => _trebuchetPath;
        set { if (SetProperty(ref _trebuchetPath, value ?? "")) SaveSettings(); }
    }

    /// <summary>
    /// Check if game paths are configured.
    /// </summary>
    public bool HasGamePaths => !string.IsNullOrEmpty(_baseGameInstallPath) || !string.IsNullOrEmpty(_neverwinterNightsPath);

    /// <summary>
    /// Check if CurrentModulePath points to a valid module (not just the modules parent directory).
    /// A valid module is either a .mod file or a directory containing module.ifo.
    /// Returns false if the path is the modules parent folder (e.g., ~/Documents/Neverwinter Nights/modules/).
    /// </summary>
    public bool HasValidModulePath()
    {
        return IsValidModulePath(_currentModulePath);
    }

    /// <summary>
    /// Check if a path points to a valid module.
    /// A valid module is either a .mod file or a directory containing module.ifo.
    /// Returns false for empty paths, the modules parent folder, or nonexistent paths.
    /// </summary>
    public static bool IsValidModulePath(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        // .mod file
        if (File.Exists(path) && path.EndsWith(".mod", StringComparison.OrdinalIgnoreCase))
            return true;

        // Directory with module.ifo (unpacked module) - case-insensitive for Linux (#1384)
        if (Directory.Exists(path) && PathHelper.FileExistsInDirectory(path, "module.ifo"))
            return true;

        return false;
    }

    /// <summary>
    /// Get the path to the game data folder (contains BIF files).
    /// Returns null if not configured.
    /// </summary>
    public string? GetGameDataPath()
    {
        if (!string.IsNullOrEmpty(_baseGameInstallPath))
        {
            var dataPath = Path.Combine(_baseGameInstallPath, "data");
            if (Directory.Exists(dataPath))
                return dataPath;

            // Maybe the path IS the data folder - case-insensitive for Linux (#1384)
            if (PathHelper.FileExistsInDirectory(_baseGameInstallPath, "nwn_base.key"))
                return _baseGameInstallPath;
        }

        return null;
    }

    /// <summary>
    /// Find available TLK languages in the game installation.
    /// </summary>
    public IEnumerable<Language> GetAvailableTlkLanguages()
    {
        if (string.IsNullOrEmpty(_baseGameInstallPath))
            yield break;

        var langPath = Path.Combine(_baseGameInstallPath, "lang");
        if (!Directory.Exists(langPath))
            yield break;

        foreach (var dir in Directory.GetDirectories(langPath))
        {
            var langCode = Path.GetFileName(dir);
            var language = LanguageHelper.FromLanguageCode(langCode);
            if (language.HasValue)
            {
                // Case-insensitive for Linux (#1384)
                var dataDir = Path.Combine(dir, "data");
                if (PathHelper.FileExistsInDirectory(dataDir, "dialog.tlk"))
                    yield return language.Value;
            }
        }
    }

    /// <summary>
    /// Get the TLK file path for a specific language.
    /// </summary>
    public string? GetTlkPath(Language language, Gender gender = Gender.Male)
    {
        if (string.IsNullOrEmpty(_baseGameInstallPath))
            return null;

        var langCode = LanguageHelper.GetLanguageCode(language);
        var tlkFilename = gender == Gender.Female ? "dialogf.tlk" : "dialog.tlk";

        // NWN:EE structure: lang/XX/data/dialog.tlk - case-insensitive for Linux (#1384)
        var eeDataDir = Path.Combine(_baseGameInstallPath, "lang", langCode, "data");
        var eePath = PathHelper.FindFileInDirectory(eeDataDir, tlkFilename);
        if (eePath != null)
            return eePath;

        // Fall back to non-gendered if female not found
        if (gender == Gender.Female)
        {
            var fallbackPath = PathHelper.FindFileInDirectory(eeDataDir, "dialog.tlk");
            if (fallbackPath != null)
                return fallbackPath;
        }

        // Classic NWN structure: data/dialog.tlk (single language)
        var classicDataDir = Path.Combine(_baseGameInstallPath, "data");
        var classicPath = PathHelper.FindFileInDirectory(classicDataDir, tlkFilename);
        if (classicPath != null)
            return classicPath;

        if (gender == Gender.Female)
        {
            var classicFallback = PathHelper.FindFileInDirectory(classicDataDir, "dialog.tlk");
            if (classicFallback != null)
                return classicFallback;
        }

        return null;
    }

    private void AutoDetectPaths()
    {
        // Try to detect base game installation
        var basePath = ResourcePathDetector.AutoDetectBaseGamePath();
        if (!string.IsNullOrEmpty(basePath))
        {
            _baseGameInstallPath = basePath;
        }

        // Try to detect user documents path
        var docsPath = ResourcePathDetector.AutoDetectGamePath();
        if (!string.IsNullOrEmpty(docsPath))
        {
            _neverwinterNightsPath = docsPath;

            // Try to find modules folder
            var modulePath = ResourcePathDetector.AutoDetectModulePath(docsPath);
            if (!string.IsNullOrEmpty(modulePath))
            {
                _currentModulePath = modulePath;
            }
        }

        if (HasGamePaths)
        {
            SaveSettings();
        }
    }

    private void LoadSettings()
    {
        try
        {
            if (!Directory.Exists(SettingsDirectory))
            {
                Directory.CreateDirectory(SettingsDirectory);
            }

            if (File.Exists(SettingsFilePath))
            {
                var json = File.ReadAllText(SettingsFilePath);
                var data = JsonSerializer.Deserialize<SettingsData>(json);

                if (data != null)
                {
                    _baseGameInstallPath = PathHelper.ExpandPath(data.BaseGameInstallPath ?? "");
                    _neverwinterNightsPath = PathHelper.ExpandPath(data.NeverwinterNightsPath ?? "");
                    _currentModulePath = PathHelper.ExpandPath(data.CurrentModulePath ?? "");
                    _tlkLanguage = data.TlkLanguage ?? "";
                    _tlkUseFemale = data.TlkUseFemale;
                    _defaultLanguage = data.DefaultLanguage;

                    // Custom content paths
                    _customTlkPath = PathHelper.ExpandPath(data.CustomTlkPath ?? "");
                    _hakSearchPaths = (data.HakSearchPaths ?? new List<string>())
                        .Select(PathHelper.ExpandPath)
                        .Where(p => !string.IsNullOrEmpty(p))
                        .ToList();

                    // Theme settings
                    _sharedThemeId = data.SharedThemeId ?? "";
                    _useSharedTheme = data.UseSharedTheme;

                    // Logging settings
                    _sharedLogLevel = data.SharedLogLevel;
                    _sharedLogRetentionSessions = Math.Max(1, Math.Min(10, data.SharedLogRetentionSessions));
                    _useSharedLogging = data.UseSharedLogging;

                    // Tool paths
                    _parleyPath = PathHelper.ExpandPath(data.ParleyPath ?? "");
                    _manifestPath = PathHelper.ExpandPath(data.ManifestPath ?? "");
                    _quartermasterPath = PathHelper.ExpandPath(data.QuartermasterPath ?? "");
                    _fencePath = PathHelper.ExpandPath(data.FencePath ?? "");
                    _trebuchetPath = PathHelper.ExpandPath(data.TrebuchetPath ?? "");
                }
            }
        }
        catch (Exception ex)
        {
            // Use defaults on error, but log so failures aren't invisible (#1384)
            UnifiedLogger.Log(LogLevel.WARN,
                $"Failed to load RadoubSettings: {ex.Message}", "RadoubSettings", "Settings");
        }
    }

    private void SaveSettings()
    {
        try
        {
            if (!Directory.Exists(SettingsDirectory))
            {
                Directory.CreateDirectory(SettingsDirectory);
            }

            var data = new SettingsData
            {
                BaseGameInstallPath = PathHelper.ContractPath(_baseGameInstallPath),
                NeverwinterNightsPath = PathHelper.ContractPath(_neverwinterNightsPath),
                CurrentModulePath = PathHelper.ContractPath(_currentModulePath),
                TlkLanguage = _tlkLanguage,
                TlkUseFemale = _tlkUseFemale,
                DefaultLanguage = _defaultLanguage,

                // Custom content paths
                CustomTlkPath = PathHelper.ContractPath(_customTlkPath),
                HakSearchPaths = _hakSearchPaths.Select(PathHelper.ContractPath).ToList(),

                // Theme settings
                SharedThemeId = _sharedThemeId,
                UseSharedTheme = _useSharedTheme,

                // Logging settings
                SharedLogLevel = _sharedLogLevel,
                SharedLogRetentionSessions = _sharedLogRetentionSessions,
                UseSharedLogging = _useSharedLogging,

                // Tool paths
                ParleyPath = PathHelper.ContractPath(_parleyPath),
                ManifestPath = PathHelper.ContractPath(_manifestPath),
                QuartermasterPath = PathHelper.ContractPath(_quartermasterPath),
                FencePath = PathHelper.ContractPath(_fencePath),
                TrebuchetPath = PathHelper.ContractPath(_trebuchetPath)
            };

            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFilePath, json);
        }
        catch (Exception ex)
        {
            // Log save errors so failures aren't invisible (#1384)
            UnifiedLogger.Log(LogLevel.WARN,
                $"Failed to save RadoubSettings: {ex.Message}", "RadoubSettings", "Settings");
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
        public string? BaseGameInstallPath { get; set; }
        public string? NeverwinterNightsPath { get; set; }
        public string? CurrentModulePath { get; set; }
        public string? TlkLanguage { get; set; }
        public bool TlkUseFemale { get; set; }
        public Language DefaultLanguage { get; set; } = Language.English;

        // Custom content paths
        public string? CustomTlkPath { get; set; }
        public List<string>? HakSearchPaths { get; set; }

        // Theme settings (shared across all tools)
        public string? SharedThemeId { get; set; }
        public bool UseSharedTheme { get; set; } = true;

        // Logging settings (shared across all tools)
        public LogLevel SharedLogLevel { get; set; } = LogLevel.INFO;
        public int SharedLogRetentionSessions { get; set; } = 3;
        public bool UseSharedLogging { get; set; } = true;

        // Tool paths for cross-tool discovery
        public string? ParleyPath { get; set; }
        public string? ManifestPath { get; set; }
        public string? QuartermasterPath { get; set; }
        public string? FencePath { get; set; }
        public string? TrebuchetPath { get; set; }
    }
}
