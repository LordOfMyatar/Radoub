namespace Radoub.Formats.Jrl;

/// <summary>
/// Represents a JRL (Journal) file used by Aurora Engine games.
/// JRL files are GFF-based and store quest/journal data.
/// </summary>
public class JrlFile
{
    /// <summary>
    /// File type signature - should be "JRL "
    /// </summary>
    public string FileType { get; set; } = "JRL ";

    /// <summary>
    /// File version - typically "V3.2"
    /// </summary>
    public string FileVersion { get; set; } = "V3.2";

    /// <summary>
    /// List of journal categories (quests)
    /// </summary>
    public List<JournalCategory> Categories { get; set; } = new();

    /// <summary>
    /// Find a category by tag (case-insensitive)
    /// </summary>
    public JournalCategory? FindCategory(string tag)
    {
        return Categories.FirstOrDefault(c =>
            c.Tag.Equals(tag, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Get all quest tags
    /// </summary>
    public IEnumerable<string> GetQuestTags()
    {
        return Categories.Select(c => c.Tag);
    }
}

/// <summary>
/// Represents a journal category (quest) containing entries
/// </summary>
public class JournalCategory
{
    /// <summary>
    /// Quest tag identifier (max 32 characters)
    /// </summary>
    public string Tag { get; set; } = string.Empty;

    /// <summary>
    /// Quest name (localized string)
    /// </summary>
    public JrlLocString Name { get; set; } = new();

    /// <summary>
    /// Priority for sorting (lower = higher priority)
    /// </summary>
    public uint Priority { get; set; }

    /// <summary>
    /// XP reward for completing this quest
    /// </summary>
    public uint XP { get; set; }

    /// <summary>
    /// Optional comment (editor use)
    /// </summary>
    public string Comment { get; set; } = string.Empty;

    /// <summary>
    /// Picture resource reference
    /// </summary>
    public string Picture { get; set; } = string.Empty;

    /// <summary>
    /// List of journal entries for this quest
    /// </summary>
    public List<JournalEntry> Entries { get; set; } = new();

    /// <summary>
    /// Find entry by ID
    /// </summary>
    public JournalEntry? FindEntry(uint id)
    {
        return Entries.FirstOrDefault(e => e.ID == id);
    }
}

/// <summary>
/// Represents a journal entry within a category
/// </summary>
public class JournalEntry
{
    /// <summary>
    /// Entry ID (unique within category)
    /// </summary>
    public uint ID { get; set; }

    /// <summary>
    /// Entry text (localized string)
    /// </summary>
    public JrlLocString Text { get; set; } = new();

    /// <summary>
    /// Whether this entry marks quest completion
    /// </summary>
    public bool End { get; set; }
}

/// <summary>
/// Localized string for JRL files.
/// Simplified version of CExoLocString for JRL-specific use.
/// </summary>
public class JrlLocString
{
    /// <summary>
    /// String reference into TLK file (0xFFFFFFFF = no reference)
    /// </summary>
    public uint StrRef { get; set; } = 0xFFFFFFFF;

    /// <summary>
    /// Localized strings keyed by language ID.
    /// Language ID = (LanguageEnum * 2) + Gender (0=male, 1=female)
    /// </summary>
    public Dictionary<uint, string> Strings { get; set; } = new();

    /// <summary>
    /// Get string for specific language ID (default: English male = 0)
    /// </summary>
    public string GetString(uint languageId = 0)
    {
        return Strings.TryGetValue(languageId, out var text) ? text : string.Empty;
    }

    /// <summary>
    /// Get default string (English male, or first available)
    /// </summary>
    public string GetDefault()
    {
        if (Strings.TryGetValue(0, out var english))
            return english;
        return Strings.Values.FirstOrDefault() ?? string.Empty;
    }

    /// <summary>
    /// Set string for a language ID
    /// </summary>
    public void SetString(uint languageId, string text)
    {
        Strings[languageId] = text;
    }

    /// <summary>
    /// True if no strings and no TLK reference
    /// </summary>
    public bool IsEmpty => Strings.Count == 0 && StrRef == 0xFFFFFFFF;
}
