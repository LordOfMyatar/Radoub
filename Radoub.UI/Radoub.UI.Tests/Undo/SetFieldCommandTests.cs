using Radoub.UI.Undo;
using Xunit;

namespace Radoub.UI.Tests.Undo;

/// <summary>
/// Tests for the generic property-set command — the workhorse for field edits in
/// blueprint editors (Reliquary #2295). Captures the old value at Do time and restores
/// it on Undo.
/// </summary>
public class SetFieldCommandTests
{
    private sealed class Box { public int Value { get; set; } public string Text { get; set; } = ""; }

    [Fact]
    public void Do_SetsNewValue()
    {
        var box = new Box { Value = 1 };
        var cmd = new SetFieldCommand<int>(() => box.Value, v => box.Value = v, 42, "set value");

        cmd.Do();

        Assert.Equal(42, box.Value);
    }

    [Fact]
    public void Undo_RestoresCapturedOldValue()
    {
        var box = new Box { Value = 7 };
        var cmd = new SetFieldCommand<int>(() => box.Value, v => box.Value = v, 99, "set value");

        cmd.Do();
        cmd.Undo();

        Assert.Equal(7, box.Value);
    }

    [Fact]
    public void OldValue_CapturedAtDoTime_NotConstructionTime()
    {
        var box = new Box { Value = 1 };
        var cmd = new SetFieldCommand<int>(() => box.Value, v => box.Value = v, 50, "set value");

        box.Value = 30; // changes after construction, before Do
        cmd.Do();        // captures 30 as old
        cmd.Undo();

        Assert.Equal(30, box.Value);
    }

    [Fact]
    public void RoundTrip_ThroughManager_WorksForReferenceTypes()
    {
        var box = new Box { Text = "before" };
        var mgr = new UndoRedoManager();

        mgr.Execute(new SetFieldCommand<string>(() => box.Text, v => box.Text = v, "after", "set text"));
        Assert.Equal("after", box.Text);

        mgr.Undo();
        Assert.Equal("before", box.Text);

        mgr.Redo();
        Assert.Equal("after", box.Text);
    }

    [Fact]
    public void Description_IsExposed()
    {
        var box = new Box();
        var cmd = new SetFieldCommand<int>(() => box.Value, v => box.Value = v, 1, "change HP");
        Assert.Equal("change HP", cmd.Description);
    }
}
