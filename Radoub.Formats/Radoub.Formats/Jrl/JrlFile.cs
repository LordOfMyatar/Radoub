using Radoub.Formats.Gff;

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
    public CExoLocString Name { get; set; } = new();

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
    public CExoLocString Text { get; set; } = new();

    /// <summary>
    /// Whether this entry marks quest completion
    /// </summary>
    public bool End { get; set; }
}
