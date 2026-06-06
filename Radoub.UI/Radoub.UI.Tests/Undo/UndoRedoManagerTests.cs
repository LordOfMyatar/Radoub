using System;
using Radoub.UI.Undo;
using Xunit;

namespace Radoub.UI.Tests.Undo;

/// <summary>
/// Tests for the shared undo/redo stack (Radoub.UI.Undo). First consumer is Reliquary
/// (#2295); the manager is the foundation for the cross-tool undo epic (#2231).
/// </summary>
public class UndoRedoManagerTests
{
    [Fact]
    public void Execute_RunsCommandDo()
    {
        var mgr = new UndoRedoManager();
        int value = 0;
        var cmd = new RelayUndoableCommand(() => value = 5, () => value = 0, "set 5");

        mgr.Execute(cmd);

        Assert.Equal(5, value);
    }

    [Fact]
    public void Execute_EnablesUndo()
    {
        var mgr = new UndoRedoManager();
        Assert.False(mgr.CanUndo);

        mgr.Execute(new RelayUndoableCommand(() => { }, () => { }, "noop"));

        Assert.True(mgr.CanUndo);
        Assert.False(mgr.CanRedo);
    }

    [Fact]
    public void Undo_RevertsLastCommand()
    {
        var mgr = new UndoRedoManager();
        int value = 0;
        mgr.Execute(new RelayUndoableCommand(() => value = 5, () => value = 0, "set 5"));

        mgr.Undo();

        Assert.Equal(0, value);
        Assert.False(mgr.CanUndo);
        Assert.True(mgr.CanRedo);
    }

    [Fact]
    public void Redo_ReappliesUndoneCommand()
    {
        var mgr = new UndoRedoManager();
        int value = 0;
        mgr.Execute(new RelayUndoableCommand(() => value = 5, () => value = 0, "set 5"));
        mgr.Undo();

        mgr.Redo();

        Assert.Equal(5, value);
        Assert.True(mgr.CanUndo);
        Assert.False(mgr.CanRedo);
    }

    [Fact]
    public void Execute_ClearsRedoStack()
    {
        var mgr = new UndoRedoManager();
        int value = 0;
        mgr.Execute(new RelayUndoableCommand(() => value = 1, () => value = 0, "a"));
        mgr.Undo();
        Assert.True(mgr.CanRedo);

        mgr.Execute(new RelayUndoableCommand(() => value = 2, () => value = 1, "b"));

        Assert.False(mgr.CanRedo); // new branch invalidates redo
    }

    [Fact]
    public void UndoRedo_MultiStep_PreservesOrder()
    {
        var mgr = new UndoRedoManager();
        var log = new System.Collections.Generic.List<string>();
        mgr.Execute(new RelayUndoableCommand(() => log.Add("do-a"), () => log.Add("undo-a"), "a"));
        mgr.Execute(new RelayUndoableCommand(() => log.Add("do-b"), () => log.Add("undo-b"), "b"));

        mgr.Undo(); // undo b
        mgr.Undo(); // undo a

        Assert.Equal(new[] { "do-a", "do-b", "undo-b", "undo-a" }, log);
    }

    [Fact]
    public void Clear_EmptiesBothStacks()
    {
        var mgr = new UndoRedoManager();
        mgr.Execute(new RelayUndoableCommand(() => { }, () => { }, "a"));
        mgr.Undo();

        mgr.Clear();

        Assert.False(mgr.CanUndo);
        Assert.False(mgr.CanRedo);
    }

    [Fact]
    public void Descriptions_ReflectStackTops()
    {
        var mgr = new UndoRedoManager();
        mgr.Execute(new RelayUndoableCommand(() => { }, () => { }, "change HP"));

        Assert.Equal("change HP", mgr.UndoDescription);
        Assert.Null(mgr.RedoDescription);

        mgr.Undo();

        Assert.Null(mgr.UndoDescription);
        Assert.Equal("change HP", mgr.RedoDescription);
    }

    [Fact]
    public void StateChanged_FiresOnExecuteUndoRedoClear()
    {
        var mgr = new UndoRedoManager();
        int fired = 0;
        mgr.StateChanged += (_, _) => fired++;

        mgr.Execute(new RelayUndoableCommand(() => { }, () => { }, "a")); // 1
        mgr.Undo();   // 2
        mgr.Redo();   // 3
        mgr.Clear();  // 4

        Assert.Equal(4, fired);
    }

    [Fact]
    public void Undo_WhenEmpty_IsNoOp()
    {
        var mgr = new UndoRedoManager();
        mgr.Undo(); // must not throw
        Assert.False(mgr.CanUndo);
    }

    [Fact]
    public void Redo_WhenEmpty_IsNoOp()
    {
        var mgr = new UndoRedoManager();
        mgr.Redo(); // must not throw
        Assert.False(mgr.CanRedo);
    }

    [Fact]
    public void RelayCommand_NullActions_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => new RelayUndoableCommand(null!, () => { }, "x"));
        Assert.Throws<ArgumentNullException>(() => new RelayUndoableCommand(() => { }, null!, "x"));
    }
}
