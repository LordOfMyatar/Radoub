using System.ComponentModel;
using System.Runtime.CompilerServices;
using Radoub.Formats.Common;
using Radoub.Formats.Logging;

namespace Radoub.Formats.Settings;

/// <summary>
/// Shared settings for all Radoub tools.
/// Stores game paths and TLK configuration in ~/Radoub/RadoubSettings.json
/// Individual tools have their own settings files for tool-specific preferences.
/// </summary>
public partial class RadoubSettings : INotifyPropertyChanged
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
                // Support environment variable override for CI and test isolation
                var overrideDir = Environment.GetEnvironmentVariable("RADOUB_SETTINGS_DIR");
                if (!string.IsNullOrEmpty(overrideDir))
                {
                    _settingsDirectory = overrideDir;
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

    // Backup settings (shared across all tools)
    private int _backupRetentionDays = 30;

    // Garbage label filters (shared across all tools)
    private List<string> _garbageFilters = GetDefaultGarbageFilters();

    /// <summary>
    /// Default garbage label filters matching legacy IsGarbageLabel behavior.
    /// Bare strings = case-insensitive substring match.
    /// "=" prefix = case-insensitive exact match.
    /// </summary>
    private static List<string> GetDefaultGarbageFilters() => new()
    {
        "deleted",   // substring: Deleted_Nunchaku, BIORESERVED_DELETED, etc.
        "padding",   // substring: Padding, PAdding, etc.
        "reserved",  // substring: bio_reserved, CEP Reserved, etc.
        "xp2spec",   // substring: xp2spec1, Xp2spec99, etc.
        "=User",     // exact: CEP placeholder rows 214-509
        "=****",     // exact: 2DA null placeholder
        "=blank",    // exact: matches legacy ItemFilterPanel check
        "=invalid",  // exact: ItemFilterPanel extra check
    };

    // Tool paths - auto-populated when tools run, used for cross-tool integration
    private string _parleyPath = "";
    private string _manifestPath = "";
    private string _quartermasterPath = "";
    private string _fencePath = "";
    private string _trebuchetPath = "";
    private string _reliquePath = "";

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
    /// Backup retention in days (1-90). Backups older than this are deleted on startup.
    /// </summary>
    public int BackupRetentionDays
    {
        get => _backupRetentionDays;
        set
        {
            var clamped = Math.Max(1, Math.Min(90, value));
            if (SetProperty(ref _backupRetentionDays, clamped)) SaveSettings();
        }
    }

    /// <summary>
    /// User-configurable garbage label filters for 2DA entries.
    /// Bare strings = case-insensitive substring match (e.g., "deleted" matches any label containing "deleted").
    /// "=" prefix = case-insensitive exact match (e.g., "=User" matches only "User").
    /// </summary>
    public IReadOnlyList<string> GarbageFilters => _garbageFilters.AsReadOnly();

    /// <summary>
    /// Replace all garbage filters.
    /// </summary>
    public void SetGarbageFilters(IEnumerable<string> filters)
    {
        _garbageFilters = filters.Where(f => !string.IsNullOrWhiteSpace(f)).ToList();
        OnPropertyChanged(nameof(GarbageFilters));
        SaveSettings();
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
    /// Path to Relique (ItemEditor) executable. Auto-set when Relique runs.
    /// </summary>
    public string ReliquePath
    {
        get => _reliquePath;
        set { if (SetProperty(ref _reliquePath, value ?? "")) SaveSettings(); }
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
    /// Infer and set the current module path from a file being opened.
    /// If the file's directory is a valid module (contains module.ifo),
    /// sets CurrentModulePath. Only updates if not already set or if
    /// the new path is different from the current one.
    /// Returns true if the module path was updated.
    /// </summary>
    public bool TryInferModuleFromFile(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (string.IsNullOrEmpty(directory))
            return false;

        if (!IsValidModulePath(directory))
            return false;

        // Already pointing to this module
        if (string.Equals(_currentModulePath, directory, StringComparison.OrdinalIgnoreCase))
            return false;

        CurrentModulePath = directory;
        UnifiedLogger.LogApplication(LogLevel.INFO,
            $"Module path inferred from opened file: {UnifiedLogger.SanitizePath(directory)}");
        return true;
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

}
