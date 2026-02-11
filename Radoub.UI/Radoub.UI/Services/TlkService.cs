using Radoub.Formats.Common;
using Radoub.Formats.Gff;
using Radoub.Formats.Logging;
using Radoub.Formats.Settings;
using Radoub.Formats.Tlk;

namespace Radoub.UI.Services;

/// <summary>
/// Information about a LocString's source and available translations.
/// </summary>
public class LocStringInfo
{
    public bool HasEmbeddedStrings { get; init; }
    public bool HasStrRef { get; init; }
    public uint StrRef { get; init; }
    public bool IsCustomTlkRef { get; init; }
    public IReadOnlyList<Language> EmbeddedLanguages { get; init; } = Array.Empty<Language>();
    public string SourceDescription { get; init; } = "";
}

/// <summary>
/// Implementation of ITlkService for Talk Table string resolution.
/// Supports primary game TLK, custom module TLK, and multilingual content.
/// Can optionally integrate with RadoubSettings for automatic TLK management.
/// Issue #971 - Part of Epic #959 (UI Uniformity).
/// Issue #1286 - Consolidated from Manifest's separate implementation.
/// </summary>
public class TlkService : ITlkService
{
    private TlkFile? _primaryTlk;
    private TlkFile? _customTlk;
    private readonly Dictionary<Language, TlkFile> _tlkCache = new();
    private readonly object _lock = new();
    private bool _disposed;
    private bool _settingsSubscribed;

    private Language _currentLanguage = Language.English;
    private Gender _currentGender = Gender.Male;
    private readonly List<Language> _availableLanguages = new();

    #region ITlkService Implementation - Loading

    public bool LoadPrimaryTlk(string path)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            UnifiedLogger.Log(LogLevel.WARN, $"Primary TLK not found: {path}", "TlkService", "[TLK]");
            return false;
        }

        lock (_lock)
        {
            try
            {
                _primaryTlk = TlkReader.Read(path);
                UnifiedLogger.Log(LogLevel.INFO, $"Loaded primary TLK: {_primaryTlk.Count} entries", "TlkService", "[TLK]");
                return true;
            }
            catch (Exception ex)
            {
                UnifiedLogger.Log(LogLevel.ERROR, $"Failed to load primary TLK: {ex.Message}", "TlkService", "[TLK]");
                _primaryTlk = null;
                return false;
            }
        }
    }

    public bool LoadCustomTlk(string path)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            UnifiedLogger.Log(LogLevel.DEBUG, $"Custom TLK not found: {path}", "TlkService", "[TLK]");
            return false;
        }

        lock (_lock)
        {
            try
            {
                _customTlk = TlkReader.Read(path);
                UnifiedLogger.Log(LogLevel.INFO, $"Loaded custom TLK: {_customTlk.Count} entries", "TlkService", "[TLK]");
                return true;
            }
            catch (Exception ex)
            {
                UnifiedLogger.Log(LogLevel.ERROR, $"Failed to load custom TLK: {ex.Message}", "TlkService", "[TLK]");
                _customTlk = null;
                return false;
            }
        }
    }

    public void ClearTlk()
    {
        lock (_lock)
        {
            _primaryTlk = null;
            _customTlk = null;
            UnifiedLogger.Log(LogLevel.DEBUG, "TLK cache cleared", "TlkService", "[TLK]");
        }
    }

    #endregion

    #region ITlkService Implementation - String Resolution

    public string? GetString(uint strRef)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!LanguageHelper.IsValidStrRef(strRef))
            return null;

        // Custom TLK for high StrRefs
        if (LanguageHelper.IsCustomTlkStrRef(strRef))
        {
            var index = LanguageHelper.GetTlkIndex(strRef);
            return _customTlk?.GetString(index);
        }

        // Primary TLK for standard StrRefs
        return _primaryTlk?.GetString(strRef);
    }

    public string? GetString(uint strRef, TlkSource source)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!LanguageHelper.IsValidStrRef(strRef))
            return null;

        var index = LanguageHelper.GetTlkIndex(strRef);

        return source switch
        {
            TlkSource.Primary => _primaryTlk?.GetString(index),
            TlkSource.Custom => _customTlk?.GetString(index),
            TlkSource.Any => _customTlk?.GetString(index) ?? _primaryTlk?.GetString(index),
            _ => null
        };
    }

    public string ResolveLocString(CExoLocString locString, Language? preferredLanguage = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // When settings integration is active, use settings-driven resolution
        // which auto-loads TLK files from disk as needed
        if (_settingsSubscribed)
            return ResolveLocStringFromSettings(locString, preferredLanguage);

        var language = preferredLanguage ?? _currentLanguage;
        var gender = _currentGender;

        // 1. Try exact match (language + gender)
        var combinedId = LanguageHelper.ToCombinedId(language, gender);
        if (locString.LocalizedStrings.TryGetValue(combinedId, out var text) && !string.IsNullOrEmpty(text))
            return text;

        // 2. Try language with default gender (Male)
        var defaultCombinedId = LanguageHelper.ToCombinedId(language, Gender.Male);
        if (locString.LocalizedStrings.TryGetValue(defaultCombinedId, out text) && !string.IsNullOrEmpty(text))
            return text;

        // 3. Try StrRef
        if (LanguageHelper.IsValidStrRef(locString.StrRef))
        {
            var resolved = GetString(locString.StrRef);
            if (!string.IsNullOrEmpty(resolved))
                return resolved;
        }

        // 4. Try fallback languages in embedded strings
        foreach (var fallback in LanguageHelper.FallbackOrder)
        {
            if (fallback == language)
                continue;

            var fallbackId = LanguageHelper.ToCombinedId(fallback, Gender.Male);
            if (locString.LocalizedStrings.TryGetValue(fallbackId, out text) && !string.IsNullOrEmpty(text))
                return text;
        }

        // 5. Return any embedded string
        if (locString.LocalizedStrings.Count > 0)
            return locString.LocalizedStrings.Values.First();

        return string.Empty;
    }

    #endregion

    #region ITlkService Implementation - Status

    public bool IsPrimaryLoaded => _primaryTlk != null;

    public bool IsCustomLoaded => _customTlk != null;

    public int PrimaryEntryCount => _primaryTlk?.Count ?? 0;

    public int CustomEntryCount => _customTlk?.Count ?? 0;

    #endregion

    #region ITlkService Implementation - Language Support

    public Language CurrentLanguage
    {
        get => _currentLanguage;
        set => _currentLanguage = value;
    }

    public Gender CurrentGender
    {
        get => _currentGender;
        set => _currentGender = value;
    }

    public IReadOnlyList<Language> AvailableLanguages => _availableLanguages.AsReadOnly();

    public IReadOnlyList<Language> DetectAvailableLanguages(string gameInstallPath)
    {
        _availableLanguages.Clear();

        if (string.IsNullOrEmpty(gameInstallPath) || !Directory.Exists(gameInstallPath))
            return _availableLanguages;

        // NWN:EE language folder structure: data/lang/{language_code}/data/dialog.tlk
        var langPath = Path.Combine(gameInstallPath, "lang");
        if (!Directory.Exists(langPath))
        {
            // Try classic NWN structure (dialog.tlk in data folder directly)
            var classicTlk = Path.Combine(gameInstallPath, "data", "dialog.tlk");
            if (File.Exists(classicTlk))
            {
                _availableLanguages.Add(Language.English);
            }
            return _availableLanguages;
        }

        // Scan language folders
        foreach (var langDir in Directory.EnumerateDirectories(langPath))
        {
            var langCode = Path.GetFileName(langDir);
            var language = LanguageHelper.FromLanguageCode(langCode);

            if (language.HasValue)
            {
                // Check if dialog.tlk exists for this language
                var tlkPath = Path.Combine(langDir, "data", "dialog.tlk");
                if (File.Exists(tlkPath))
                {
                    _availableLanguages.Add(language.Value);
                }
            }
        }

        // Sort by display order (English first, then alphabetically)
        _availableLanguages.Sort((a, b) =>
        {
            if (a == Language.English) return -1;
            if (b == Language.English) return 1;
            return LanguageHelper.GetDisplayName(a).CompareTo(LanguageHelper.GetDisplayName(b));
        });

        UnifiedLogger.Log(LogLevel.INFO,
            $"Detected {_availableLanguages.Count} languages: {string.Join(", ", _availableLanguages.Select(LanguageHelper.GetDisplayName))}",
            "TlkService", "[TLK]");

        return _availableLanguages;
    }

    public string? GetTlkPath(string gameInstallPath, Language language, Gender gender = Gender.Male)
    {
        if (string.IsNullOrEmpty(gameInstallPath))
            return null;

        var langCode = LanguageHelper.GetLanguageCode(language);

        // NWN:EE structure: lang/{code}/data/dialog.tlk or dialogf.tlk
        var tlkName = gender == Gender.Female ? "dialogf.tlk" : "dialog.tlk";
        var eePath = Path.Combine(gameInstallPath, "lang", langCode, "data", tlkName);
        if (File.Exists(eePath))
            return eePath;

        // Try without gender variant
        if (gender == Gender.Female)
        {
            eePath = Path.Combine(gameInstallPath, "lang", langCode, "data", "dialog.tlk");
            if (File.Exists(eePath))
                return eePath;
        }

        // Classic NWN structure: data/dialog.tlk (English only typically)
        if (language == Language.English)
        {
            var classicPath = Path.Combine(gameInstallPath, "data", "dialog.tlk");
            if (File.Exists(classicPath))
                return classicPath;
        }

        return null;
    }

    #endregion

    #region Settings Integration

    /// <summary>
    /// Enable automatic TLK management from RadoubSettings.
    /// Subscribes to settings changes and invalidates cache when TLK-related settings change.
    /// </summary>
    public void EnableSettingsIntegration()
    {
        if (_settingsSubscribed)
            return;

        RadoubSettings.Instance.PropertyChanged += OnSettingsChanged;
        _settingsSubscribed = true;
    }

    private void OnSettingsChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(RadoubSettings.BaseGameInstallPath) or
            nameof(RadoubSettings.TlkLanguage) or
            nameof(RadoubSettings.TlkUseFemale) or
            nameof(RadoubSettings.CustomTlkPath))
        {
            InvalidateCache();
        }
    }

    /// <summary>
    /// Invalidate all cached TLK files.
    /// </summary>
    public void InvalidateCache()
    {
        lock (_lock)
        {
            _primaryTlk = null;
            _customTlk = null;
            _tlkCache.Clear();
            UnifiedLogger.Log(LogLevel.DEBUG, "TLK cache invalidated", "TlkService", "[TLK]");
        }
    }

    /// <summary>
    /// Check if TLK resources are available (game paths configured).
    /// </summary>
    public bool IsAvailable => RadoubSettings.Instance.HasGamePaths;

    /// <summary>
    /// Get available languages from RadoubSettings.
    /// </summary>
    public IReadOnlyList<Language> GetAvailableLanguages()
    {
        return RadoubSettings.Instance.GetAvailableTlkLanguages().ToList();
    }

    /// <summary>
    /// Resolve a StrRef using settings-driven language and TLK auto-loading.
    /// Falls back through available languages if primary language doesn't have the string.
    /// </summary>
    public string? ResolveStrRef(uint strRef)
    {
        return ResolveStrRef(strRef, RadoubSettings.Instance.EffectiveLanguage);
    }

    /// <summary>
    /// Resolve a StrRef in a specific language with settings-driven TLK auto-loading.
    /// </summary>
    public string? ResolveStrRef(uint strRef, Language language)
    {
        if (!LanguageHelper.IsValidStrRef(strRef))
            return null;

        // Check for custom TLK
        if (LanguageHelper.IsCustomTlkStrRef(strRef))
        {
            var customTlk = GetCachedCustomTlk();
            if (customTlk != null)
            {
                var index = LanguageHelper.GetTlkIndex(strRef);
                var text = customTlk.GetString(index);
                if (text != null)
                    return text;
            }
            strRef = LanguageHelper.GetTlkIndex(strRef);
        }

        // Get TLK for requested language
        var tlk = GetCachedTlk(language);
        if (tlk != null)
        {
            var text = tlk.GetString(strRef);
            if (text != null)
                return text;
        }

        // Try fallback languages
        foreach (var fallback in LanguageHelper.FallbackOrder)
        {
            if (fallback == language)
                continue;

            tlk = GetCachedTlk(fallback);
            if (tlk != null)
            {
                var text = tlk.GetString(strRef);
                if (text != null)
                    return text;
            }
        }

        return null;
    }

    /// <summary>
    /// Resolve a CExoLocString using settings-driven language/gender preferences.
    /// </summary>
    public string ResolveLocStringFromSettings(CExoLocString locString, Language? preferredLanguage = null)
    {
        var language = preferredLanguage ?? RadoubSettings.Instance.EffectiveLanguage;
        var gender = RadoubSettings.Instance.PreferredGender;
        var combinedId = LanguageHelper.ToCombinedId(language, gender);

        // 1. Try exact match (language + gender)
        if (locString.LocalizedStrings.TryGetValue(combinedId, out var text) && !string.IsNullOrEmpty(text))
            return text;

        // 2. Try language without gender preference
        var defaultCombinedId = LanguageHelper.ToCombinedId(language, Gender.Male);
        if (locString.LocalizedStrings.TryGetValue(defaultCombinedId, out text) && !string.IsNullOrEmpty(text))
            return text;

        // 3. Try StrRef (using settings-driven loading)
        if (LanguageHelper.IsValidStrRef(locString.StrRef))
        {
            var resolved = ResolveStrRef(locString.StrRef, language);
            if (!string.IsNullOrEmpty(resolved))
                return resolved;
        }

        // 4. Try fallback languages in embedded strings
        foreach (var fallback in LanguageHelper.FallbackOrder)
        {
            var fallbackId = LanguageHelper.ToCombinedId(fallback, Gender.Male);
            if (locString.LocalizedStrings.TryGetValue(fallbackId, out text) && !string.IsNullOrEmpty(text))
                return text;
        }

        // 5. Return any embedded string
        if (locString.LocalizedStrings.Count > 0)
            return locString.LocalizedStrings.Values.First();

        return string.Empty;
    }

    /// <summary>
    /// Get information about a LocString's source.
    /// </summary>
    public LocStringInfo GetLocStringInfo(CExoLocString locString)
    {
        var hasEmbedded = locString.LocalizedStrings.Count > 0;
        var hasStrRef = LanguageHelper.IsValidStrRef(locString.StrRef);
        var isCustomStrRef = hasStrRef && LanguageHelper.IsCustomTlkStrRef(locString.StrRef);

        var embeddedLanguages = locString.LocalizedStrings.Keys
            .Select(id => LanguageHelper.GetLanguage(id))
            .Distinct()
            .ToList();

        return new LocStringInfo
        {
            HasEmbeddedStrings = hasEmbedded,
            HasStrRef = hasStrRef,
            StrRef = locString.StrRef,
            IsCustomTlkRef = isCustomStrRef,
            EmbeddedLanguages = embeddedLanguages,
            SourceDescription = GetSourceDescription(hasEmbedded, hasStrRef, isCustomStrRef, locString.StrRef)
        };
    }

    private string GetSourceDescription(bool hasEmbedded, bool hasStrRef, bool isCustom, uint strRef)
    {
        string tlkDesc = "";
        if (hasStrRef)
        {
            var resolved = ResolveStrRef(strRef);
            if (resolved != null)
            {
                tlkDesc = isCustom ? $"Custom TLK:{strRef - LanguageHelper.CustomTlkThreshold}" : $"TLK:{strRef}";
            }
            else if (!IsAvailable)
            {
                tlkDesc = isCustom ? $"Custom TLK:{strRef - LanguageHelper.CustomTlkThreshold} (no game path)" : $"TLK:{strRef} (no game path)";
            }
            else
            {
                tlkDesc = isCustom ? $"Custom TLK:{strRef - LanguageHelper.CustomTlkThreshold} (not found)" : $"TLK:{strRef} (not found)";
            }
        }

        if (hasEmbedded && hasStrRef)
            return $"Embedded + {tlkDesc}";
        if (hasEmbedded)
            return "Embedded";
        if (hasStrRef)
            return tlkDesc;
        return "Empty";
    }

    /// <summary>
    /// Get all available translations for a LocString.
    /// </summary>
    public Dictionary<Language, string> GetAllTranslations(CExoLocString locString)
    {
        var translations = new Dictionary<Language, string>();

        // Get embedded strings
        foreach (var (combinedId, text) in locString.LocalizedStrings)
        {
            var language = LanguageHelper.GetLanguage(combinedId);
            if (!translations.ContainsKey(language) && !string.IsNullOrEmpty(text))
            {
                translations[language] = text;
            }
        }

        // Get TLK strings if StrRef is valid
        if (LanguageHelper.IsValidStrRef(locString.StrRef))
        {
            foreach (var language in GetAvailableLanguages())
            {
                if (!translations.ContainsKey(language))
                {
                    var text = ResolveStrRef(locString.StrRef, language);
                    if (!string.IsNullOrEmpty(text))
                    {
                        translations[language] = text;
                    }
                }
            }
        }

        return translations;
    }

    /// <summary>
    /// Get the TLK file path for the effective language from settings.
    /// </summary>
    public string? GetCurrentTlkPath()
    {
        return RadoubSettings.Instance.GetTlkPath(
            RadoubSettings.Instance.EffectiveLanguage,
            RadoubSettings.Instance.PreferredGender);
    }

    /// <summary>
    /// Get a summary of TLK status for display.
    /// </summary>
    public string GetTlkStatusSummary()
    {
        if (!IsAvailable)
            return "TLK: No game path configured";

        var path = GetCurrentTlkPath();
        if (string.IsNullOrEmpty(path))
            return $"TLK: Not found for {LanguageHelper.GetDisplayName(RadoubSettings.Instance.EffectiveLanguage)}";

        var displayPath = UnifiedLogger.SanitizePath(path);
        var langName = LanguageHelper.GetDisplayName(RadoubSettings.Instance.EffectiveLanguage);

        return $"TLK: {langName} - {displayPath}";
    }

    /// <summary>
    /// Load a TLK from cache or disk for the given language (settings-driven).
    /// </summary>
    private TlkFile? GetCachedTlk(Language language)
    {
        lock (_lock)
        {
            if (_tlkCache.TryGetValue(language, out var cached))
                return cached;

            var gender = RadoubSettings.Instance.PreferredGender;
            var tlkPath = RadoubSettings.Instance.GetTlkPath(language, gender);

            if (string.IsNullOrEmpty(tlkPath) || !File.Exists(tlkPath))
                return null;

            try
            {
                var tlk = TlkReader.Read(tlkPath);
                _tlkCache[language] = tlk;
                UnifiedLogger.Log(LogLevel.INFO,
                    $"Loaded TLK: {LanguageHelper.GetDisplayName(language)} ({tlk.Count} entries)",
                    "TlkService", "[TLK]");
                return tlk;
            }
            catch (Exception ex)
            {
                UnifiedLogger.Log(LogLevel.ERROR,
                    $"Failed to load TLK for {language}: {ex.Message}",
                    "TlkService", "[TLK]");
                return null;
            }
        }
    }

    /// <summary>
    /// Load custom TLK from cache or disk (settings-driven).
    /// </summary>
    private TlkFile? GetCachedCustomTlk()
    {
        lock (_lock)
        {
            if (_customTlk != null)
                return _customTlk;

            var customTlkPath = RadoubSettings.Instance.CustomTlkPath;
            if (string.IsNullOrEmpty(customTlkPath) || !File.Exists(customTlkPath))
                return null;

            try
            {
                _customTlk = TlkReader.Read(customTlkPath);
                UnifiedLogger.Log(LogLevel.INFO,
                    $"Loaded custom TLK: {_customTlk.Count} entries",
                    "TlkService", "[TLK]");
                return _customTlk;
            }
            catch (Exception ex)
            {
                UnifiedLogger.Log(LogLevel.ERROR,
                    $"Failed to load custom TLK: {ex.Message}",
                    "TlkService", "[TLK]");
                return null;
            }
        }
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (_disposed)
            return;

        if (_settingsSubscribed)
        {
            RadoubSettings.Instance.PropertyChanged -= OnSettingsChanged;
        }

        _primaryTlk = null;
        _customTlk = null;
        _tlkCache.Clear();
        _availableLanguages.Clear();
        _disposed = true;

        GC.SuppressFinalize(this);
    }

    #endregion
}
