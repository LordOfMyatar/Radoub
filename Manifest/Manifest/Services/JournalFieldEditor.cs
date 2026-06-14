using Radoub.Formats.Jrl;

namespace Manifest.Services;

/// <summary>
/// Pure commit kernel for journal text fields (#2461). Pushes a pending text-box value
/// into the journal model and returns whether the value actually changed, so the caller
/// can mark the document dirty.
///
/// Extracted from MainWindow.PropertyPanel so the same logic runs on both the LostFocus
/// handlers AND a force-commit-before-save path. The #2461 data-loss bug was that text
/// fields committed to the model only on LostFocus: a Save (Ctrl+S / menu) issued while a
/// text box still had focus wrote the pre-edit model value, so the visible edit silently
/// reverted. Routing save through a force-commit that calls these helpers guarantees the
/// visible value is persisted.
///
/// Text writes target language slot 0 (default), matching the existing handler behavior.
/// Keeping it pure (no UI types) makes it unit-testable without FlaUI.
/// </summary>
internal static class JournalFieldEditor
{
    /// <summary>Apply entry text to the model. Returns true if the value changed.</summary>
    public static bool ApplyEntryText(JournalEntry entry, string? newText)
    {
        var value = newText ?? "";
        if (entry.Text.GetDefault() == value) return false;
        entry.Text.SetString(0, value);
        return true;
    }

    /// <summary>Apply category name to the model. Returns true if the value changed.</summary>
    public static bool ApplyCategoryName(JournalCategory category, string? newName)
    {
        var value = newName ?? "";
        if (category.Name.GetDefault() == value) return false;
        category.Name.SetString(0, value);
        return true;
    }

    /// <summary>Apply category tag to the model. Returns true if the value changed.</summary>
    public static bool ApplyCategoryTag(JournalCategory category, string? newTag)
    {
        var value = newTag ?? "";
        if (category.Tag == value) return false;
        category.Tag = value;
        return true;
    }

    /// <summary>Apply category comment to the model. Returns true if the value changed.</summary>
    public static bool ApplyCategoryComment(JournalCategory category, string? newComment)
    {
        var value = newComment ?? "";
        if (category.Comment == value) return false;
        category.Comment = value;
        return true;
    }
}
