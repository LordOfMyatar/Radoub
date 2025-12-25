using System.Text.Json;

namespace Radoub.Dictionary;

/// <summary>
/// Manages dictionary settings (language selection, enabled dictionaries).
/// Settings are shared across all Radoub tools.
/// </summary>
public class DictionarySettingsService
{
    private static DictionarySettingsService? _instance;
    public static DictionarySettingsService Instance => _instance ??= new DictionarySettingsService();

    private readonly string _settingsPath;
    private DictionarySettingsData _settings = new();

    /// <summary>
    /// Event raised when the primary language changes.
    /// </summary>
    public event EventHandler<string>? PrimaryLanguageChanged;

    /// <summary>
    /// Event raised when a custom dictionary is enabled or disabled.
    /// </summary>
    public event EventHandler<DictionaryToggleEventArgs>? CustomDictionaryToggled;

    private DictionarySettingsService()
    {
        var userDictPath = DictionaryDiscovery.GetDefaultUserDictionaryPath();
        Directory.CreateDirectory(userDictPath);
        _settingsPath = Path.Combine(userDictPath, "settings.json");

        LoadSettings();
    }

    /// <summary>
    /// For testing: allows creating instance with custom path.
    /// </summary>
    internal DictionarySettingsService(string settingsPath)
    {
        _settingsPath = settingsPath;
        LoadSettings();
    }

    private void LoadSettings()
    {
        if (File.Exists(_settingsPath))
        {
            try
            {
                var json = File.ReadAllText(_settingsPath);
                _settings = JsonSerializer.Deserialize<DictionarySettingsData>(json) ?? new();
            }
            catch
            {
                _settings = new();
            }
        }
    }

    private void SaveSettings()
    {
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(_settings, options);
            File.WriteAllText(_settingsPath, json);
        }
        catch
        {
            // Silently fail - settings will be lost but app continues
        }
    }

    /// <summary>
    /// Gets or sets the primary language code (e.g., "en_US", "es_ES").
    /// </summary>
    public string PrimaryLanguage
    {
        get => _settings.PrimaryLanguage;
        set
        {
            if (_settings.PrimaryLanguage != value)
            {
                _settings.PrimaryLanguage = value;
                SaveSettings();
                PrimaryLanguageChanged?.Invoke(this, value);
            }
        }
    }

    /// <summary>
    /// Checks if a custom dictionary is enabled.
    /// </summary>
    public bool IsCustomDictionaryEnabled(string dictionaryId)
    {
        // If not in disabled list, it's enabled by default
        return !_settings.DisabledCustomDictionaries.Contains(dictionaryId);
    }

    /// <summary>
    /// Sets whether a custom dictionary is enabled.
    /// </summary>
    public void SetCustomDictionaryEnabled(string dictionaryId, bool enabled)
    {
        bool changed = false;

        if (enabled)
        {
            changed = _settings.DisabledCustomDictionaries.Remove(dictionaryId);
        }
        else
        {
            if (!_settings.DisabledCustomDictionaries.Contains(dictionaryId))
            {
                _settings.DisabledCustomDictionaries.Add(dictionaryId);
                changed = true;
            }
        }

        if (changed)
        {
            SaveSettings();
            CustomDictionaryToggled?.Invoke(this, new DictionaryToggleEventArgs(dictionaryId, enabled));
        }
    }

    /// <summary>
    /// Gets list of disabled custom dictionary IDs.
    /// </summary>
    public List<string> DisabledCustomDictionaries => new(_settings.DisabledCustomDictionaries);

    /// <summary>
    /// Gets list of all enabled custom dictionary IDs from the given available list.
    /// </summary>
    public List<string> GetEnabledCustomDictionaries(IEnumerable<string> availableDictionaryIds)
    {
        return availableDictionaryIds
            .Where(id => !_settings.DisabledCustomDictionaries.Contains(id))
            .ToList();
    }
}

/// <summary>
/// Event args for custom dictionary toggle events.
/// </summary>
public class DictionaryToggleEventArgs : EventArgs
{
    public string DictionaryId { get; }
    public bool IsEnabled { get; }

    public DictionaryToggleEventArgs(string dictionaryId, bool isEnabled)
    {
        DictionaryId = dictionaryId;
        IsEnabled = isEnabled;
    }
}

/// <summary>
/// Dictionary settings data structure.
/// </summary>
public class DictionarySettingsData
{
    /// <summary>
    /// Primary Hunspell language code (e.g., "en_US").
    /// </summary>
    public string PrimaryLanguage { get; set; } = "en_US";

    /// <summary>
    /// List of disabled custom dictionary IDs.
    /// Dictionaries not in this list are enabled by default.
    /// </summary>
    public List<string> DisabledCustomDictionaries { get; set; } = new();
}
