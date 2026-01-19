using Radoub.Formats.Common;
using Radoub.Formats.Gff;
using Radoub.Formats.Logging;
using Radoub.Formats.Tlk;

namespace Radoub.UI.Services;

/// <summary>
/// Implementation of ITlkService for Talk Table string resolution.
/// Supports primary game TLK, custom module TLK, and multilingual content.
/// Issue #971 - Part of Epic #959 (UI Uniformity).
/// </summary>
public class TlkService : ITlkService
{
    private TlkFile? _primaryTlk;
    private TlkFile? _customTlk;
    private readonly object _lock = new();
    private bool _disposed;

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

    #region IDisposable

    public void Dispose()
    {
        if (_disposed)
            return;

        _primaryTlk = null;
        _customTlk = null;
        _availableLanguages.Clear();
        _disposed = true;

        GC.SuppressFinalize(this);
    }

    #endregion
}
