using Manifest.Services;
using Radoub.Formats.Jrl;
using Xunit;

namespace Manifest.Tests;

/// <summary>
/// Tests for <see cref="JournalFieldEditor"/> — the pure commit kernel that pushes a
/// pending text-box value into the journal model and reports whether anything changed
/// (so the caller can mark the document dirty). Extracted so the focus-commit fix
/// (#2461) is unit-testable without FlaUI: the same kernel runs on LostFocus AND on
/// the force-commit-before-save path, guaranteeing a save-while-focused persists the
/// visible value instead of reverting.
/// </summary>
public class JournalFieldEditorTests
{
    // --- Entry text (language slot 0 / default) ---

    [Fact]
    public void ApplyEntryText_DifferentValue_UpdatesModelAndReportsChanged()
    {
        var entry = new JournalEntry();
        entry.Text.SetString(0, "old quest text");

        var changed = JournalFieldEditor.ApplyEntryText(entry, "new quest text");

        Assert.True(changed);
        Assert.Equal("new quest text", entry.Text.GetDefault());
    }

    [Fact]
    public void ApplyEntryText_SameValue_NoChangeReported()
    {
        var entry = new JournalEntry();
        entry.Text.SetString(0, "same");

        var changed = JournalFieldEditor.ApplyEntryText(entry, "same");

        Assert.False(changed);
        Assert.Equal("same", entry.Text.GetDefault());
    }

    [Fact]
    public void ApplyEntryText_NullTreatedAsEmpty()
    {
        var entry = new JournalEntry();
        entry.Text.SetString(0, "had text");

        var changed = JournalFieldEditor.ApplyEntryText(entry, null);

        Assert.True(changed);
        Assert.Equal("", entry.Text.GetDefault());
    }

    // --- Category name (language slot 0 / default) ---

    [Fact]
    public void ApplyCategoryName_DifferentValue_UpdatesModelAndReportsChanged()
    {
        var cat = new JournalCategory();
        cat.Name.SetString(0, "Old Name");

        var changed = JournalFieldEditor.ApplyCategoryName(cat, "New Name");

        Assert.True(changed);
        Assert.Equal("New Name", cat.Name.GetDefault());
    }

    [Fact]
    public void ApplyCategoryName_SameValue_NoChangeReported()
    {
        var cat = new JournalCategory();
        cat.Name.SetString(0, "Quest");

        Assert.False(JournalFieldEditor.ApplyCategoryName(cat, "Quest"));
    }

    // --- Category tag (plain string) ---

    [Fact]
    public void ApplyCategoryTag_DifferentValue_UpdatesModelAndReportsChanged()
    {
        var cat = new JournalCategory { Tag = "old_tag" };

        var changed = JournalFieldEditor.ApplyCategoryTag(cat, "new_tag");

        Assert.True(changed);
        Assert.Equal("new_tag", cat.Tag);
    }

    [Fact]
    public void ApplyCategoryTag_SameValue_NoChangeReported()
    {
        var cat = new JournalCategory { Tag = "tag" };

        Assert.False(JournalFieldEditor.ApplyCategoryTag(cat, "tag"));
    }

    // --- Category comment (plain string) ---

    [Fact]
    public void ApplyCategoryComment_DifferentValue_UpdatesModelAndReportsChanged()
    {
        var cat = new JournalCategory { Comment = "old" };

        var changed = JournalFieldEditor.ApplyCategoryComment(cat, "new");

        Assert.True(changed);
        Assert.Equal("new", cat.Comment);
    }

    [Fact]
    public void ApplyCategoryComment_NullTreatedAsEmpty()
    {
        var cat = new JournalCategory { Comment = "old" };

        var changed = JournalFieldEditor.ApplyCategoryComment(cat, null);

        Assert.True(changed);
        Assert.Equal("", cat.Comment);
    }
}
