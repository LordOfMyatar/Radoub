namespace Radoub.Dictionary.Models;

/// <summary>
/// Represents a single word or term in a dictionary.
/// </summary>
public class DictionaryEntry
{
    /// <summary>
    /// The word or term.
    /// </summary>
    public required string Word { get; set; }

    /// <summary>
    /// Optional category for the term (e.g., "spell", "creature", "item", "location").
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Optional source where the term was extracted from (e.g., "spells.2da", "module dialogs").
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    /// Whether this entry was added by the user (vs. auto-extracted or bundled).
    /// </summary>
    public bool IsUserAdded { get; set; }
}
