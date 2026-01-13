using Radoub.Formats.Jrl;

namespace Radoub.TestUtilities.Builders;

/// <summary>
/// Fluent builder for constructing JrlFile (journal) instances for testing.
/// </summary>
public class JournalBuilder
{
    private readonly JrlFile _journal = new();
    private JournalCategory? _currentCategory;

    /// <summary>
    /// Add a quest category.
    /// </summary>
    public JournalBuilder WithQuest(string tag, string name, uint priority = 0, uint xp = 0)
    {
        var category = new JournalCategory
        {
            Tag = tag,
            Priority = priority,
            XP = xp
        };
        category.Name.LocalizedStrings[0] = name;

        _journal.Categories.Add(category);
        _currentCategory = category;
        return this;
    }

    /// <summary>
    /// Add a journal entry to the current quest.
    /// </summary>
    public JournalBuilder WithEntry(uint id, string text, bool isEnd = false)
    {
        if (_currentCategory == null)
            throw new InvalidOperationException("Must add a quest before adding entries.");

        var entry = new JournalEntry
        {
            ID = id,
            End = isEnd
        };
        entry.Text.LocalizedStrings[0] = text;

        _currentCategory.Entries.Add(entry);
        return this;
    }

    /// <summary>
    /// Navigate to a specific quest by tag.
    /// </summary>
    public JournalBuilder AtQuest(string tag)
    {
        _currentCategory = _journal.FindCategory(tag)
            ?? throw new ArgumentException($"Quest with tag '{tag}' not found.");
        return this;
    }

    /// <summary>
    /// Build the final JrlFile.
    /// </summary>
    public JrlFile Build()
    {
        return _journal;
    }
}

/// <summary>
/// Extension methods for common journal patterns.
/// </summary>
public static class JournalBuilderExtensions
{
    /// <summary>
    /// Create a simple quest with start and end entries.
    /// </summary>
    public static JrlFile CreateSimpleQuest(string tag, string questName, string startText, string endText)
    {
        return new JournalBuilder()
            .WithQuest(tag, questName)
                .WithEntry(1, startText)
                .WithEntry(100, endText, isEnd: true)
            .Build();
    }

    /// <summary>
    /// Create a multi-stage quest.
    /// </summary>
    public static JrlFile CreateMultiStageQuest(string tag, string questName, params string[] stageTexts)
    {
        var builder = new JournalBuilder()
            .WithQuest(tag, questName);

        for (int i = 0; i < stageTexts.Length; i++)
        {
            bool isEnd = (i == stageTexts.Length - 1);
            builder.WithEntry((uint)(i + 1), stageTexts[i], isEnd);
        }

        return builder.Build();
    }
}
