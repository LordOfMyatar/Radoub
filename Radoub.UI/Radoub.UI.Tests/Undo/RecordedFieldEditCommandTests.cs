using System;
using Radoub.UI.Undo;
using Xunit;

namespace Radoub.UI.Tests.Undo;

/// <summary>
/// Tests for <see cref="RecordedFieldEditCommand{T}"/> — the "record an edit that already happened"
/// primitive used for document/whole-field undo of TwoWay-bound fields (#2231). Unlike
/// <see cref="SetFieldCommand{T}"/> (which captures the old value at Do-time), this command is
/// given an explicit old and new value, because the binding has already mutated the model by the
/// time the host records the edit (on focus-loss).
/// </summary>
public class RecordedFieldEditCommandTests
{
    [Fact]
    public void Do_SetsNewValue()
    {
        string field = "Apple";
        var cmd = new RecordedFieldEditCommand<string>(
            oldValue: "Apple", newValue: "Banana", v => field = v, "edit name");

        Assert.True(cmd.Do());
        Assert.Equal("Banana", field);
    }

    [Fact]
    public void Do_WhenValueAlreadyApplied_IsIdempotentAndRecords()
    {
        // Simulates the real flow: the binding already wrote "Banana" before the command is built.
        string field = "Banana";
        var cmd = new RecordedFieldEditCommand<string>("Apple", "Banana", v => field = v, "edit name");

        Assert.True(cmd.Do());     // setting Banana again is a harmless no-op
        Assert.Equal("Banana", field);
    }

    [Fact]
    public void Undo_RestoresOldValue_WholeField()
    {
        string field = "Banana";
        var cmd = new RecordedFieldEditCommand<string>("Apple", "Banana", v => field = v, "edit name");
        cmd.Do();

        cmd.Undo();

        Assert.Equal("Apple", field); // whole previous value, not char-by-char
    }

    [Fact]
    public void DoUndoRedo_RoundTrips()
    {
        string field = "Apple";
        var cmd = new RecordedFieldEditCommand<string>("Apple", "Banana", v => field = v, "edit name");

        cmd.Do();   // Banana
        cmd.Undo(); // Apple
        Assert.Equal("Apple", field);
        Assert.True(cmd.Do()); // redo → Banana
        Assert.Equal("Banana", field);
    }

    [Fact]
    public void Undo_RestoresEmptyOldValue_NotPreBindingBlank()
    {
        // Old value was a real empty string the user committed; undo restores exactly that,
        // and never reverts past it (the Apple→blank class of bug, #2231).
        string field = "typed";
        var cmd = new RecordedFieldEditCommand<string>("", "typed", v => field = v, "edit");
        cmd.Do();

        cmd.Undo();

        Assert.Equal("", field);
    }

    [Fact]
    public void Description_IsExposed()
    {
        var cmd = new RecordedFieldEditCommand<string>("a", "b", _ => { }, "edit tag");
        Assert.Equal("edit tag", cmd.Description);
    }

    [Fact]
    public void NullSetter_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new RecordedFieldEditCommand<string>("a", "b", null!, "x"));
    }

    [Fact]
    public void WorksWithValueTypes()
    {
        int field = 5;
        var cmd = new RecordedFieldEditCommand<int>(5, 10, v => field = v, "edit count");
        cmd.Do();
        Assert.Equal(10, field);
        cmd.Undo();
        Assert.Equal(5, field);
    }
}
