using System.Text.Json.Serialization;

namespace Radoub.Dictionary.Models;

/// <summary>
/// Represents a dictionary file containing words and terms.
/// </summary>
public class CustomDictionary
{
    /// <summary>
    /// Dictionary format version.
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0";

    /// <summary>
    /// Description of the dictionary source (e.g., "NWN Official Campaign", "D&D SRD 5e").
    /// </summary>
    [JsonPropertyName("source")]
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// Optional description or notes about the dictionary.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Simple word list for basic dictionaries.
    /// </summary>
    [JsonPropertyName("words")]
    public List<string> Words { get; set; } = [];

    /// <summary>
    /// Detailed entries with metadata (optional, for rich dictionaries).
    /// </summary>
    [JsonPropertyName("entries")]
    public List<DictionaryEntry>? Entries { get; set; }

    /// <summary>
    /// Words to permanently ignore (not flagged as spelling errors).
    /// </summary>
    [JsonPropertyName("ignoredWords")]
    public List<string> IgnoredWords { get; set; } = [];

    /// <summary>
    /// Gets all words in this dictionary (from both Words and Entries).
    /// </summary>
    [JsonIgnore]
    public IEnumerable<string> AllWords
    {
        get
        {
            foreach (var word in Words)
            {
                yield return word;
            }

            if (Entries != null)
            {
                foreach (var entry in Entries)
                {
                    yield return entry.Word;
                }
            }
        }
    }
}
