using Radoub.Formats.Common;
using Radoub.Formats.Gff;

namespace Radoub.UI.Services;

/// <summary>
/// Interface for Talk Table (TLK) string resolution across Radoub tools.
/// Supports primary game TLK, custom/module TLK, and multilingual content.
/// Issue #971 - Part of Epic #959 (UI Uniformity).
/// </summary>
public interface ITlkService : IDisposable
{
    #region TLK Loading

    /// <summary>
    /// Loads the primary TLK file (dialog.tlk or language-specific).
    /// </summary>
    /// <param name="path">Path to the TLK file</param>
    /// <returns>True if loaded successfully</returns>
    bool LoadPrimaryTlk(string path);

    /// <summary>
    /// Loads a custom TLK file (module or server-specific strings).
    /// Custom TLK is used for StrRefs >= 0x01000000.
    /// </summary>
    /// <param name="path">Path to the custom TLK file</param>
    /// <returns>True if loaded successfully</returns>
    bool LoadCustomTlk(string path);

    /// <summary>
    /// Clears all loaded TLK files and cache.
    /// </summary>
    void ClearTlk();

    #endregion

    #region String Resolution

    /// <summary>
    /// Resolves a StrRef to its string value.
    /// Automatically routes to custom TLK for StrRefs >= 0x01000000.
    /// </summary>
    /// <param name="strRef">String reference number</param>
    /// <returns>Resolved string, or null if not found</returns>
    string? GetString(uint strRef);

    /// <summary>
    /// Resolves a StrRef from a specific source.
    /// </summary>
    /// <param name="strRef">String reference number</param>
    /// <param name="source">Which TLK to use</param>
    /// <returns>Resolved string, or null if not found</returns>
    string? GetString(uint strRef, TlkSource source);

    /// <summary>
    /// Resolves a CExoLocString to display text.
    /// Priority: embedded strings (preferred language) > StrRef > any embedded > empty.
    /// </summary>
    /// <param name="locString">Localized string to resolve</param>
    /// <param name="preferredLanguage">Preferred language (null = use service default)</param>
    /// <returns>Resolved display text</returns>
    string ResolveLocString(CExoLocString locString, Language? preferredLanguage = null);

    #endregion

    #region Status

    /// <summary>
    /// Whether a primary TLK file is loaded.
    /// </summary>
    bool IsPrimaryLoaded { get; }

    /// <summary>
    /// Whether a custom TLK file is loaded.
    /// </summary>
    bool IsCustomLoaded { get; }

    /// <summary>
    /// Number of entries in the primary TLK.
    /// </summary>
    int PrimaryEntryCount { get; }

    /// <summary>
    /// Number of entries in the custom TLK.
    /// </summary>
    int CustomEntryCount { get; }

    #endregion

    #region Language Support

    /// <summary>
    /// Current language for string resolution.
    /// </summary>
    Language CurrentLanguage { get; set; }

    /// <summary>
    /// Current gender preference for gendered strings.
    /// </summary>
    Gender CurrentGender { get; set; }

    /// <summary>
    /// List of available languages (detected from game installation).
    /// </summary>
    IReadOnlyList<Language> AvailableLanguages { get; }

    /// <summary>
    /// Detect and return available languages in the game installation.
    /// </summary>
    /// <param name="gameInstallPath">Path to game installation</param>
    /// <returns>List of available languages</returns>
    IReadOnlyList<Language> DetectAvailableLanguages(string gameInstallPath);

    /// <summary>
    /// Gets the TLK file path for a specific language.
    /// </summary>
    /// <param name="gameInstallPath">Path to game installation</param>
    /// <param name="language">Target language</param>
    /// <param name="gender">Gender variant (default Male)</param>
    /// <returns>TLK file path, or null if not found</returns>
    string? GetTlkPath(string gameInstallPath, Language language, Gender gender = Gender.Male);

    #endregion
}

/// <summary>
/// Source for TLK string resolution.
/// </summary>
public enum TlkSource
{
    /// <summary>
    /// Primary TLK (dialog.tlk) - base game strings.
    /// </summary>
    Primary,

    /// <summary>
    /// Custom TLK - module or server-specific strings.
    /// </summary>
    Custom,

    /// <summary>
    /// Try custom first, fall back to primary.
    /// This is the standard resolution order for StrRefs < 0x01000000.
    /// </summary>
    Any
}
