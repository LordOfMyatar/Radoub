using Radoub.Formats.Dlg;
using Xunit;

namespace Radoub.TestUtilities.Assertions;

/// <summary>
/// Assertion helpers for comparing dialog structures.
/// </summary>
public static class DialogAssertions
{
    /// <summary>
    /// Assert that two dialogs are structurally equal.
    /// </summary>
    public static void AssertStructurallyEqual(DlgFile expected, DlgFile actual)
    {
        Assert.Equal(expected.Entries.Count, actual.Entries.Count);
        Assert.Equal(expected.Replies.Count, actual.Replies.Count);
        Assert.Equal(expected.StartingList.Count, actual.StartingList.Count);

        for (int i = 0; i < expected.Entries.Count; i++)
        {
            AssertEntryEqual(expected.Entries[i], actual.Entries[i], $"Entry[{i}]");
        }

        for (int i = 0; i < expected.Replies.Count; i++)
        {
            AssertReplyEqual(expected.Replies[i], actual.Replies[i], $"Reply[{i}]");
        }

        for (int i = 0; i < expected.StartingList.Count; i++)
        {
            AssertLinkEqual(expected.StartingList[i], actual.StartingList[i], $"Start[{i}]");
        }
    }

    /// <summary>
    /// Assert that two dialog entries are equal.
    /// </summary>
    public static void AssertEntryEqual(DlgEntry expected, DlgEntry actual, string context = "Entry")
    {
        Assert.Equal(expected.Speaker, actual.Speaker);
        Assert.Equal(expected.Script, actual.Script);
        Assert.Equal(expected.Comment, actual.Comment);
        AssertLocStringEqual(expected.Text, actual.Text, $"{context}.Text");
        Assert.Equal(expected.RepliesList.Count, actual.RepliesList.Count);
    }

    /// <summary>
    /// Assert that two dialog replies are equal.
    /// </summary>
    public static void AssertReplyEqual(DlgReply expected, DlgReply actual, string context = "Reply")
    {
        Assert.Equal(expected.Script, actual.Script);
        Assert.Equal(expected.Comment, actual.Comment);
        AssertLocStringEqual(expected.Text, actual.Text, $"{context}.Text");
        Assert.Equal(expected.EntriesList.Count, actual.EntriesList.Count);
    }

    /// <summary>
    /// Assert that two dialog links are equal.
    /// </summary>
    public static void AssertLinkEqual(DlgLink expected, DlgLink actual, string context = "Link")
    {
        Assert.Equal(expected.Index, actual.Index);
        Assert.Equal(expected.Active, actual.Active);
        Assert.Equal(expected.IsChild, actual.IsChild);
    }

    /// <summary>
    /// Assert that two localized strings have the same content.
    /// </summary>
    public static void AssertLocStringEqual(
        Radoub.Formats.Gff.CExoLocString expected,
        Radoub.Formats.Gff.CExoLocString actual,
        string context = "LocString")
    {
        Assert.Equal(expected.StrRef, actual.StrRef);
        Assert.Equal(expected.LocalizedStrings.Count, actual.LocalizedStrings.Count);

        foreach (var kvp in expected.LocalizedStrings)
        {
            Assert.True(actual.LocalizedStrings.ContainsKey(kvp.Key),
                $"{context}: Missing language ID {kvp.Key}");
            Assert.Equal(kvp.Value, actual.LocalizedStrings[kvp.Key]);
        }
    }

    /// <summary>
    /// Assert that a dialog has the expected number of nodes.
    /// </summary>
    public static void AssertNodeCounts(DlgFile dialog, int entries, int replies, int starts)
    {
        Assert.Equal(entries, dialog.Entries.Count);
        Assert.Equal(replies, dialog.Replies.Count);
        Assert.Equal(starts, dialog.StartingList.Count);
    }

    /// <summary>
    /// Assert that an entry has specific text (English).
    /// </summary>
    public static void AssertEntryText(DlgFile dialog, int entryIndex, string expectedText)
    {
        Assert.True(entryIndex < dialog.Entries.Count,
            $"Entry index {entryIndex} out of range (count: {dialog.Entries.Count})");

        var entry = dialog.Entries[entryIndex];
        var actualText = entry.Text.GetString(0);
        Assert.Equal(expectedText, actualText);
    }

    /// <summary>
    /// Assert that a reply has specific text (English).
    /// </summary>
    public static void AssertReplyText(DlgFile dialog, int replyIndex, string expectedText)
    {
        Assert.True(replyIndex < dialog.Replies.Count,
            $"Reply index {replyIndex} out of range (count: {dialog.Replies.Count})");

        var reply = dialog.Replies[replyIndex];
        var actualText = reply.Text.GetString(0);
        Assert.Equal(expectedText, actualText);
    }
}
