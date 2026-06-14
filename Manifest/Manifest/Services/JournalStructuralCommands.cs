using System.Collections.Generic;
using Radoub.Formats.Jrl;
using Radoub.UI.Undo;

namespace Manifest.Services;

/// <summary>
/// Undoable structural operations for the journal (#2253 / #2231 Sprint 3): add and delete of
/// categories and entries. These mutate the model lists directly and capture enough state to
/// restore the prior arrangement (delete records the original index). The host passes a UI
/// refresh callback separately (it rebuilds the tree after Do/Undo); these commands stay pure so
/// they are unit-testable without FlaUI.
///
/// Delete commands return false from <see cref="IUndoableCommand.Do"/> when the target is not
/// present, honoring the manager's refuse-to-push contract so a no-op delete never lands on the
/// undo stack.
/// </summary>
public sealed class AddCategoryCommand : IUndoableCommand
{
    private readonly List<JournalCategory> _categories;
    private readonly JournalCategory _category;

    public AddCategoryCommand(JrlFile jrl, JournalCategory category)
    {
        _categories = jrl.Categories;
        _category = category;
    }

    public string Description => "add category";

    public bool Do()
    {
        _categories.Add(_category);
        return true;
    }

    public void Undo() => _categories.Remove(_category);
}

public sealed class DeleteCategoryCommand : IUndoableCommand
{
    private readonly List<JournalCategory> _categories;
    private readonly JournalCategory _category;
    private int _index = -1;

    public DeleteCategoryCommand(JrlFile jrl, JournalCategory category)
    {
        _categories = jrl.Categories;
        _category = category;
    }

    public string Description => "delete category";

    public bool Do()
    {
        _index = _categories.IndexOf(_category);
        if (_index < 0) return false; // not present → nothing to delete, don't record
        _categories.RemoveAt(_index);
        return true;
    }

    public void Undo()
    {
        if (_index < 0) return;
        _categories.Insert(_index, _category);
    }
}

public sealed class AddEntryCommand : IUndoableCommand
{
    private readonly List<JournalEntry> _entries;
    private readonly JournalEntry _entry;

    public AddEntryCommand(JournalCategory category, JournalEntry entry)
    {
        _entries = category.Entries;
        _entry = entry;
    }

    public string Description => "add entry";

    public bool Do()
    {
        _entries.Add(_entry);
        return true;
    }

    public void Undo() => _entries.Remove(_entry);
}

public sealed class DeleteEntryCommand : IUndoableCommand
{
    private readonly List<JournalEntry> _entries;
    private readonly JournalEntry _entry;
    private int _index = -1;

    public DeleteEntryCommand(JournalCategory category, JournalEntry entry)
    {
        _entries = category.Entries;
        _entry = entry;
    }

    public string Description => "delete entry";

    public bool Do()
    {
        _index = _entries.IndexOf(_entry);
        if (_index < 0) return false;
        _entries.RemoveAt(_index);
        return true;
    }

    public void Undo()
    {
        if (_index < 0) return;
        _entries.Insert(_index, _entry);
    }
}
