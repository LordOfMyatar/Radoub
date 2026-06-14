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

    // --- Refuse-to-push: a command whose Do() self-rolls-back (returns false) must not be
    // recorded, otherwise its Undo() would later revert a change that never happened (#2231). ---

    /// <summary>Test command with a controllable Do() result and call counters.</summary>
    private sealed class FakeCommand : IUndoableCommand
    {
        private readonly bool _doResult;
        public FakeCommand(bool doResult, string description = "fake") { _doResult = doResult; Description = description; }
        public int DoCount { get; private set; }
        public int UndoCount { get; private set; }
        public string Description { get; }
        public bool Do() { DoCount++; return _doResult; }
        public void Undo() => UndoCount++;
    }

    [Fact]
    public void Execute_WhenDoReturnsFalse_DoesNotPush()
    {
        var mgr = new UndoRedoManager();
        var cmd = new FakeCommand(doResult: false);

        mgr.Execute(cmd);

        Assert.Equal(1, cmd.DoCount); // Do() still ran
        Assert.False(mgr.CanUndo);    // but nothing was recorded
    }

    [Fact]
    public void Execute_WhenDoReturnsFalse_DoesNotClearRedo()
    {
        var mgr = new UndoRedoManager();
        mgr.Execute(new FakeCommand(doResult: true, "a"));
        mgr.Undo();
        Assert.True(mgr.CanRedo);

        mgr.Execute(new FakeCommand(doResult: false, "b"));

        Assert.True(mgr.CanRedo); // a failed command must not invalidate the redo branch
    }

    [Fact]
    public void Execute_WhenDoReturnsFalse_FiresNoStateChanged()
    {
        var mgr = new UndoRedoManager();
        int fired = 0;
        mgr.StateChanged += (_, _) => fired++;

        mgr.Execute(new FakeCommand(doResult: false));

        Assert.Equal(0, fired);
    }

    [Fact]
    public void Execute_WhenDoReturnsTrue_PushesAndClearsRedo()
    {
        var mgr = new UndoRedoManager();
        mgr.Execute(new FakeCommand(doResult: true, "a"));
        mgr.Undo();
        Assert.True(mgr.CanRedo);

        mgr.Execute(new FakeCommand(doResult: true, "b"));

        Assert.True(mgr.CanUndo);
        Assert.False(mgr.CanRedo);
    }

    [Fact]
    public void Redo_WhenDoReturnsFalse_DoesNotPushToUndo()
    {
        var mgr = new UndoRedoManager();
        // Execute a command that succeeds, then undo it so it's on the redo stack.
        var cmd = new RefusingOnRedoCommand();
        mgr.Execute(cmd);   // Do() #1 returns true → pushed to undo
        mgr.Undo();         // moved to redo

        mgr.Redo();         // Do() #2 returns false → must NOT re-push to undo

        Assert.False(mgr.CanUndo);
    }

    [Fact]
    public void Redo_WhenDoReturnsTrue_MovesToUndo()
    {
        var mgr = new UndoRedoManager();
        var cmd = new FakeCommand(doResult: true);
        mgr.Execute(cmd);
        mgr.Undo();
        Assert.True(mgr.CanRedo);

        mgr.Redo();

        Assert.True(mgr.CanUndo);
        Assert.False(mgr.CanRedo);
    }

    /// <summary>Returns true on first Do() (Execute) and false on the second (Redo).</summary>
    private sealed class RefusingOnRedoCommand : IUndoableCommand
    {
        private int _doCalls;
        public string Description => "refusing-on-redo";
        public bool Do() => ++_doCalls == 1;
        public void Undo() { }
    }
}
