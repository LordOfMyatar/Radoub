using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Radoub.Formats.Common;

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

    private static string SettingsDirectory
    {
        get
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(userProfile, "Radoub");
        }
    }

    private static string SettingsFilePath => Path.Combine(SettingsDirectory, "RadoubSettings.json");

    // Game installation paths
    private string _baseGameInstallPath = "";
    private string _neverwinterNightsPath = "";
    private string _currentModulePath = "";

    // TLK settings
    private string _tlkLanguage = "";  // Empty = auto-detect from OS or default to English
    private bool _tlkUseFemale = false;
    private Language _defaultLanguage = Language.English;  // Default display language

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
    /// Check if game paths are configured.
    /// </summary>
    public bool HasGamePaths => !string.IsNullOrEmpty(_baseGameInstallPath) || !string.IsNullOrEmpty(_neverwinterNightsPath);

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

            // Maybe the path IS the data folder
            if (File.Exists(Path.Combine(_baseGameInstallPath, "nwn_base.key")))
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
                var tlkPath = Path.Combine(dir, "data", "dialog.tlk");
                if (File.Exists(tlkPath))
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

        // NWN:EE structure: lang/XX/data/dialog.tlk
        var eePath = Path.Combine(_baseGameInstallPath, "lang", langCode, "data", tlkFilename);
        if (File.Exists(eePath))
            return eePath;

        // Fall back to non-gendered if female not found
        if (gender == Gender.Female)
        {
            var fallbackPath = Path.Combine(_baseGameInstallPath, "lang", langCode, "data", "dialog.tlk");
            if (File.Exists(fallbackPath))
                return fallbackPath;
        }

        // Classic NWN structure: data/dialog.tlk (single language)
        var classicPath = Path.Combine(_baseGameInstallPath, "data", tlkFilename);
        if (File.Exists(classicPath))
            return classicPath;

        if (gender == Gender.Female)
        {
            var classicFallback = Path.Combine(_baseGameInstallPath, "data", "dialog.tlk");
            if (File.Exists(classicFallback))
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
                    _baseGameInstallPath = ExpandPath(data.BaseGameInstallPath ?? "");
                    _neverwinterNightsPath = ExpandPath(data.NeverwinterNightsPath ?? "");
                    _currentModulePath = ExpandPath(data.CurrentModulePath ?? "");
                    _tlkLanguage = data.TlkLanguage ?? "";
                    _tlkUseFemale = data.TlkUseFemale;
                    _defaultLanguage = data.DefaultLanguage;
                }
            }
        }
        catch
        {
            // Use defaults on error
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
                BaseGameInstallPath = ContractPath(_baseGameInstallPath),
                NeverwinterNightsPath = ContractPath(_neverwinterNightsPath),
                CurrentModulePath = ContractPath(_currentModulePath),
                TlkLanguage = _tlkLanguage,
                TlkUseFemale = _tlkUseFemale,
                DefaultLanguage = _defaultLanguage
            };

            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFilePath, json);
        }
        catch
        {
            // Ignore save errors
        }
    }

    /// <summary>
    /// Contract path for storage - replaces user home with ~
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
    /// Expand path from storage - replaces ~ with user home
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
    }
}
