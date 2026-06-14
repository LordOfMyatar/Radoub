using Manifest.Services;
using Radoub.Formats.Jrl;
using Radoub.UI.Undo;
using Xunit;

namespace Manifest.Tests;

/// <summary>
/// Tests for the structural undo commands (#2253 / #2231 Sprint 3): add/delete of journal
/// categories and entries routed through <see cref="IUndoableCommand"/>. These are the
/// highest data-loss-risk operations, so they get pure unit coverage independent of FlaUI.
/// Do/Undo/Redo round-trips and index preservation mirror Reliquary's command tests.
/// </summary>
public class JournalStructuralCommandTests
{
    private static JournalCategory Cat(string tag)
    {
        var c = new JournalCategory { Tag = tag };
        return c;
    }

    private static JournalEntry Entry(uint id)
    {
        var e = new JournalEntry { ID = id };
        e.Text.SetString(0, $"entry {id}");
        return e;
    }

    // --- Add category ---

    [Fact]
    public void AddCategory_DoAppends_UndoRemoves_RedoReAppends()
    {
        var jrl = new JrlFile();
        var added = Cat("new_quest");
        var cmd = new AddCategoryCommand(jrl, added);

        Assert.True(cmd.Do());
        Assert.Single(jrl.Categories);
        Assert.Same(added, jrl.Categories[0]);

        cmd.Undo();
        Assert.Empty(jrl.Categories);

        Assert.True(cmd.Do()); // redo
        Assert.Single(jrl.Categories);
        Assert.Same(added, jrl.Categories[0]);
    }

    // --- Delete category (index preserved) ---

    [Fact]
    public void DeleteCategory_UndoRestoresAtOriginalIndex()
    {
        var jrl = new JrlFile();
        var a = Cat("a"); var b = Cat("b"); var c = Cat("c");
        jrl.Categories.Add(a); jrl.Categories.Add(b); jrl.Categories.Add(c);

        var cmd = new DeleteCategoryCommand(jrl, b); // middle item

        Assert.True(cmd.Do());
        Assert.Equal(new[] { a, c }, jrl.Categories);

        cmd.Undo();
        Assert.Equal(new[] { a, b, c }, jrl.Categories); // b back at index 1
    }

    [Fact]
    public void DeleteCategory_NotPresent_DoReturnsFalse()
    {
        var jrl = new JrlFile();
        jrl.Categories.Add(Cat("a"));
        var orphan = Cat("orphan");

        var cmd = new DeleteCategoryCommand(jrl, orphan);

        Assert.False(cmd.Do()); // refuse-to-push: nothing to delete
        Assert.Single(jrl.Categories);
    }

    // --- Add entry ---

    [Fact]
    public void AddEntry_DoAppends_UndoRemoves_RedoReAppends()
    {
        var cat = Cat("quest");
        var entry = Entry(100);
        var cmd = new AddEntryCommand(cat, entry);

        Assert.True(cmd.Do());
        Assert.Single(cat.Entries);

        cmd.Undo();
        Assert.Empty(cat.Entries);

        Assert.True(cmd.Do()); // redo
        Assert.Single(cat.Entries);
        Assert.Same(entry, cat.Entries[0]);
    }

    // --- Delete entry (index preserved) ---

    [Fact]
    public void DeleteEntry_UndoRestoresAtOriginalIndex()
    {
        var cat = Cat("quest");
        var e1 = Entry(100); var e2 = Entry(200); var e3 = Entry(300);
        cat.Entries.Add(e1); cat.Entries.Add(e2); cat.Entries.Add(e3);

        var cmd = new DeleteEntryCommand(cat, e2);

        Assert.True(cmd.Do());
        Assert.Equal(new[] { e1, e3 }, cat.Entries);

        cmd.Undo();
        Assert.Equal(new[] { e1, e2, e3 }, cat.Entries);
    }

    // --- Through the manager (refuse-to-push contract) ---

    [Fact]
    public void Manager_DeleteAbsentCategory_NotPushed()
    {
        var jrl = new JrlFile();
        jrl.Categories.Add(Cat("a"));
        var mgr = new UndoRedoManager();

        mgr.Execute(new DeleteCategoryCommand(jrl, Cat("absent")));

        Assert.False(mgr.CanUndo); // self-rolled-back command not recorded
    }

    [Fact]
    public void Manager_AddThenUndoRedo_RoundTrips()
    {
        var jrl = new JrlFile();
        var mgr = new UndoRedoManager();
        var cat = Cat("q");

        mgr.Execute(new AddCategoryCommand(jrl, cat));
        Assert.Single(jrl.Categories);
        Assert.True(mgr.CanUndo);

        mgr.Undo();
        Assert.Empty(jrl.Categories);
        Assert.True(mgr.CanRedo);

        mgr.Redo();
        Assert.Single(jrl.Categories);
    }
}
