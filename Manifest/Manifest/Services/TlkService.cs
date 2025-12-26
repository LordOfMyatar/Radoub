using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Radoub.Formats.Common;
using Radoub.Formats.Gff;
using Radoub.Formats.Settings;
using Radoub.Formats.Tlk;

namespace Manifest.Services;

/// <summary>
/// Service for loading and resolving TLK (Talk Table) strings.
/// Supports multiple languages and provides string resolution for StrRefs.
/// </summary>
public class TlkService : IDisposable
{
    private static TlkService? _instance;
    private static readonly object _lock = new();

    private readonly Dictionary<Language, TlkFile> _tlkCache = new();
    private TlkFile? _customTlk;
    private bool _disposed;

    /// <summary>
    /// Singleton instance of TlkService.
    /// </summary>
    public static TlkService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new TlkService();
                }
            }
            return _instance;
        }
    }

    private TlkService()
    {
        // Subscribe to settings changes to invalidate cache
        RadoubSettings.Instance.PropertyChanged += OnSettingsChanged;
    }

    private void OnSettingsChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(RadoubSettings.BaseGameInstallPath) or
            nameof(RadoubSettings.TlkLanguage) or
            nameof(RadoubSettings.TlkUseFemale))
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
            _tlkCache.Clear();
            _customTlk = null;
            UnifiedLogger.LogApplication(LogLevel.DEBUG, "TLK cache invalidated");
        }
    }

    /// <summary>
    /// Get available languages that have TLK files installed.
    /// </summary>
    public IReadOnlyList<Language> GetAvailableLanguages()
    {
        return RadoubSettings.Instance.GetAvailableTlkLanguages().ToList();
    }

    /// <summary>
    /// Check if TLK resources are available.
    /// </summary>
    public bool IsAvailable => RadoubSettings.Instance.HasGamePaths;

    /// <summary>
    /// Resolve a StrRef to its text in the preferred language.
    /// Returns null if StrRef is invalid or not found.
    /// </summary>
    public string? ResolveStrRef(uint strRef)
    {
        return ResolveStrRef(strRef, RadoubSettings.Instance.EffectiveLanguage);
    }

    /// <summary>
    /// Resolve a StrRef to its text in a specific language.
    /// Returns null if StrRef is invalid or not found.
    /// </summary>
    public string? ResolveStrRef(uint strRef, Language language)
    {
        if (!LanguageHelper.IsValidStrRef(strRef))
            return null;

        // Check for custom TLK
        if (LanguageHelper.IsCustomTlkStrRef(strRef))
        {
            var customTlk = GetCustomTlk();
            if (customTlk != null)
            {
                var index = LanguageHelper.GetTlkIndex(strRef);
                var text = customTlk.GetString(index);
                if (text != null)
                    return text;
            }
            // Fall back to base TLK if custom not found
            strRef = LanguageHelper.GetTlkIndex(strRef);
        }

        // Get TLK for requested language
        var tlk = GetTlk(language);
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

            tlk = GetTlk(fallback);
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
    /// Resolve a CExoLocString to display text.
    /// Checks embedded strings first, then falls back to StrRef.
    /// 2025-12-25: Updated to use consolidated CExoLocString (Sprint #548)
    /// </summary>
    public string ResolveLocString(CExoLocString locString, Language? preferredLanguage = null)
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

        // 3. Try StrRef
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
        // Check if TLK is resolvable
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

    private TlkFile? GetTlk(Language language)
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
                UnifiedLogger.LogApplication(LogLevel.INFO, $"Loaded TLK: {LanguageHelper.GetDisplayName(language)} ({tlk.Count} entries)");
                return tlk;
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to load TLK for {language}: {ex.Message}");
                return null;
            }
        }
    }

    private TlkFile? GetCustomTlk()
    {
        // Custom TLK loading would go here
        // For now, return null - custom TLK support can be added when needed
        return _customTlk;
    }

    /// <summary>
    /// Get the TLK file path for the effective language.
    /// Returns null if no TLK is available.
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

        // Shorten path for display
        var displayPath = UnifiedLogger.SanitizePath(path);
        var langName = LanguageHelper.GetDisplayName(RadoubSettings.Instance.EffectiveLanguage);

        return $"TLK: {langName} - {displayPath}";
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        RadoubSettings.Instance.PropertyChanged -= OnSettingsChanged;
        _tlkCache.Clear();
        _customTlk = null;
        _disposed = true;
    }
}

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
