using Radoub.Formats.Dlg;
using Radoub.Formats.Gff;

namespace Radoub.TestUtilities.Builders;

/// <summary>
/// Fluent builder for constructing DlgFile instances for testing.
/// </summary>
public class DialogBuilder
{
    private readonly DlgFile _dialog = new();
    private DlgEntry? _currentEntry;
    private DlgReply? _currentReply;

    /// <summary>
    /// Set global dialog properties.
    /// </summary>
    public DialogBuilder WithDelays(uint entryDelay = 0, uint replyDelay = 0)
    {
        _dialog.DelayEntry = entryDelay;
        _dialog.DelayReply = replyDelay;
        return this;
    }

    /// <summary>
    /// Set end conversation scripts.
    /// </summary>
    public DialogBuilder WithEndScripts(string? normal = null, string? abort = null)
    {
        if (normal != null) _dialog.EndConversation = normal;
        if (abort != null) _dialog.EndConverAbort = abort;
        return this;
    }

    /// <summary>
    /// Set prevent zoom in flag.
    /// </summary>
    public DialogBuilder WithPreventZoomIn(bool prevent = true)
    {
        _dialog.PreventZoomIn = prevent;
        return this;
    }

    /// <summary>
    /// Add an NPC entry at the root level (starting point).
    /// </summary>
    /// <param name="text">Dialog text (English)</param>
    /// <param name="speaker">Speaker tag (optional)</param>
    /// <param name="script">Action script (optional)</param>
    public DialogBuilder WithEntry(string text, string speaker = "", string script = "")
    {
        var entry = CreateEntry(text, speaker, script);
        _dialog.Entries.Add(entry);

        // Add to starting list
        _dialog.StartingList.Add(new DlgLink
        {
            Index = (uint)(_dialog.Entries.Count - 1),
            IsChild = false
        });

        _currentEntry = entry;
        _currentReply = null;
        return this;
    }

    /// <summary>
    /// Add a conditional starting entry.
    /// </summary>
    public DialogBuilder WithConditionalEntry(string text, string condition, string speaker = "")
    {
        var entry = CreateEntry(text, speaker, "");
        _dialog.Entries.Add(entry);

        _dialog.StartingList.Add(new DlgLink
        {
            Index = (uint)(_dialog.Entries.Count - 1),
            Active = condition,
            IsChild = false
        });

        _currentEntry = entry;
        _currentReply = null;
        return this;
    }

    /// <summary>
    /// Add a PC reply to the current entry.
    /// </summary>
    public DialogBuilder WithReply(string text, string script = "")
    {
        if (_currentEntry == null)
            throw new InvalidOperationException("Must add an entry before adding replies.");

        var reply = CreateReply(text, script);
        _dialog.Replies.Add(reply);

        _currentEntry.RepliesList.Add(new DlgLink
        {
            Index = (uint)(_dialog.Replies.Count - 1),
            IsChild = false
        });

        _currentReply = reply;
        return this;
    }

    /// <summary>
    /// Add a conditional PC reply to the current entry.
    /// </summary>
    public DialogBuilder WithConditionalReply(string text, string condition)
    {
        if (_currentEntry == null)
            throw new InvalidOperationException("Must add an entry before adding replies.");

        var reply = CreateReply(text, "");
        _dialog.Replies.Add(reply);

        _currentEntry.RepliesList.Add(new DlgLink
        {
            Index = (uint)(_dialog.Replies.Count - 1),
            Active = condition,
            IsChild = false
        });

        _currentReply = reply;
        return this;
    }

    /// <summary>
    /// Add a follow-up NPC entry to the current reply.
    /// </summary>
    public DialogBuilder ThenEntry(string text, string speaker = "", string script = "")
    {
        if (_currentReply == null)
            throw new InvalidOperationException("Must add a reply before adding follow-up entries.");

        var entry = CreateEntry(text, speaker, script);
        _dialog.Entries.Add(entry);

        _currentReply.EntriesList.Add(new DlgLink
        {
            Index = (uint)(_dialog.Entries.Count - 1),
            IsChild = false
        });

        _currentEntry = entry;
        _currentReply = null;
        return this;
    }

    /// <summary>
    /// Link to an existing entry by index.
    /// </summary>
    public DialogBuilder LinkToEntry(uint entryIndex, string linkComment = "")
    {
        if (_currentReply == null)
            throw new InvalidOperationException("Must have a current reply to link from.");

        _currentReply.EntriesList.Add(new DlgLink
        {
            Index = entryIndex,
            IsChild = true,
            LinkComment = linkComment
        });

        return this;
    }

    /// <summary>
    /// Link to an existing reply by index.
    /// </summary>
    public DialogBuilder LinkToReply(uint replyIndex)
    {
        if (_currentEntry == null)
            throw new InvalidOperationException("Must have a current entry to link from.");

        _currentEntry.RepliesList.Add(new DlgLink
        {
            Index = replyIndex,
            IsChild = true
        });

        return this;
    }

    /// <summary>
    /// Navigate back to a specific entry by index for adding more content.
    /// </summary>
    public DialogBuilder AtEntry(int index)
    {
        if (index < 0 || index >= _dialog.Entries.Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        _currentEntry = _dialog.Entries[index];
        _currentReply = null;
        return this;
    }

    /// <summary>
    /// Navigate back to a specific reply by index for adding more content.
    /// </summary>
    public DialogBuilder AtReply(int index)
    {
        if (index < 0 || index >= _dialog.Replies.Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        _currentReply = _dialog.Replies[index];
        return this;
    }

    /// <summary>
    /// Build the final DlgFile.
    /// </summary>
    public DlgFile Build()
    {
        // Update word count
        _dialog.NumWords = (uint)CountWords();
        return _dialog;
    }

    #region Helpers

    private static DlgEntry CreateEntry(string text, string speaker, string script)
    {
        var entry = new DlgEntry
        {
            Speaker = speaker,
            Script = script
        };
        entry.Text.LocalizedStrings[0] = text; // English
        return entry;
    }

    private static DlgReply CreateReply(string text, string script)
    {
        var reply = new DlgReply
        {
            Script = script
        };
        reply.Text.LocalizedStrings[0] = text; // English
        return reply;
    }

    private int CountWords()
    {
        int count = 0;
        foreach (var entry in _dialog.Entries)
        {
            if (entry.Text.LocalizedStrings.TryGetValue(0, out var text))
                count += CountWordsInString(text);
        }
        foreach (var reply in _dialog.Replies)
        {
            if (reply.Text.LocalizedStrings.TryGetValue(0, out var text))
                count += CountWordsInString(text);
        }
        return count;
    }

    private static int CountWordsInString(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return 0;
        return s.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;
    }

    #endregion
}

/// <summary>
/// Extension methods for common dialog patterns.
/// </summary>
public static class DialogBuilderExtensions
{
    /// <summary>
    /// Create a simple linear conversation (entry -> reply -> entry -> reply...).
    /// </summary>
    public static DlgFile CreateLinearDialog(params string[] alternatingTexts)
    {
        var builder = new DialogBuilder();
        bool isEntry = true;

        foreach (var text in alternatingTexts)
        {
            if (isEntry)
                builder.WithEntry(text);
            else
                builder.WithReply(text);

            isEntry = !isEntry;
        }

        return builder.Build();
    }

    /// <summary>
    /// Create a dialog with a single entry and multiple reply choices.
    /// </summary>
    public static DlgFile CreateChoiceDialog(string entryText, params string[] replyChoices)
    {
        var builder = new DialogBuilder().WithEntry(entryText);

        foreach (var reply in replyChoices)
        {
            builder.WithReply(reply);
        }

        return builder.Build();
    }
}
