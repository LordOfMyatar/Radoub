namespace Radoub.Dictionary.Models;

/// <summary>
/// Represents metadata about a discovered dictionary.
/// </summary>
public class DictionaryInfo
{
    /// <summary>
    /// Unique identifier for the dictionary (e.g., "en_US", "nwn", "lotr").
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Display name for the dictionary (e.g., "English (US)", "NWN/D&D Terms", "Lord of the Rings").
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Type of dictionary.
    /// </summary>
    public DictionaryType Type { get; init; }

    /// <summary>
    /// Full path to the dictionary file(s).
    /// For Hunspell: path to .dic file (assumes .aff is alongside).
    /// For JSON: path to .dic or .json file.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Whether this dictionary is bundled with the application.
    /// </summary>
    public bool IsBundled { get; init; }

    /// <summary>
    /// Optional description from the dictionary metadata.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Word count (if known). -1 if not yet loaded.
    /// </summary>
    public int WordCount { get; set; } = -1;

    /// <summary>
    /// Language code for Hunspell dictionaries (e.g., "en_US", "es_ES").
    /// Null for JSON custom dictionaries.
    /// </summary>
    public string? LanguageCode { get; init; }
}

/// <summary>
/// Type of dictionary file.
/// </summary>
public enum DictionaryType
{
    /// <summary>
    /// Hunspell dictionary (.dic + .aff files) for primary language checking.
    /// </summary>
    Hunspell,

    /// <summary>
    /// JSON custom dictionary for domain-specific terminology.
    /// </summary>
    Custom
}
