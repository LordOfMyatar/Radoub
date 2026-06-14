using System;
using System.Collections.Generic;
using System.Linq;
using ItemEditor.Commands;
using ItemEditor.Services;
using Radoub.Formats.Uti;
using Radoub.UI.Undo;
using Xunit;

namespace ItemEditor.Tests.Commands;

/// <summary>
/// Tests for Relique's property undo/redo commands (Sprint 1 of epic #2231). The commands wrap
/// <see cref="PropertyListMutator"/> so the rollback-on-refresh-failure seam (#2258) is exercised
/// in both directions; a command whose Do() self-rolls-back must report false so the
/// <see cref="UndoRedoManager"/> refuses to record it.
/// </summary>
public class PropertyCommandTests
{
    private static ItemProperty Prop(ushort name, byte subtype = 0) =>
        new ItemProperty { PropertyName = name, Subtype = subtype };

    private static Action NoRefresh => () => { };

    private static Action ThrowingRefresh => () => throw new InvalidOperationException("refresh blew up");

    // --- AddPropertyCommand ---

    [Fact]
    public void AddProperty_Do_AppendsAndReportsSuccess()
    {
        var list = new List<ItemProperty>();
        var cmd = new AddPropertyCommand(list, Prop(6), NoRefresh, "add");

        Assert.True(cmd.Do());
        Assert.Single(list);
        Assert.Equal(6, list[0].PropertyName);
    }

    [Fact]
    public void AddProperty_Undo_RemovesTheAddedEntry()
    {
        var list = new List<ItemProperty> { Prop(1) };
        var cmd = new AddPropertyCommand(list, Prop(6), NoRefresh, "add");
        cmd.Do();

        cmd.Undo();

        Assert.Single(list);
        Assert.Equal(1, list[0].PropertyName); // original preserved, added one gone
    }

    [Fact]
    public void AddProperty_DoUndoRedo_RoundTrips()
    {
        var list = new List<ItemProperty> { Prop(1) };
        var cmd = new AddPropertyCommand(list, Prop(6), NoRefresh, "add");

        cmd.Do();   // [1, 6]
        cmd.Undo(); // [1]
        Assert.True(cmd.Do()); // redo == Do again → [1, 6]

        Assert.Equal(2, list.Count);
        Assert.Equal(6, list[1].PropertyName);
    }

    [Fact]
    public void AddProperty_WhenRefreshThrows_RollsBackAndReportsFalse()
    {
        var list = new List<ItemProperty> { Prop(1) };
        var cmd = new AddPropertyCommand(list, Prop(6), ThrowingRefresh, "add");

        Assert.False(cmd.Do());           // refuse-to-push
        Assert.Single(list);              // model rolled back
        Assert.Equal(1, list[0].PropertyName);
    }

    // --- RemovePropertyCommand ---

    [Fact]
    public void RemoveProperty_Do_RemovesTargets()
    {
        var list = new List<ItemProperty> { Prop(1), Prop(2), Prop(3) };
        var cmd = new RemovePropertyCommand(list, new[] { 1 }, NoRefresh, "remove");

        Assert.True(cmd.Do());
        Assert.Equal(new[] { 1, 3 }, list.Select(p => (int)p.PropertyName));
    }

    [Fact]
    public void RemoveProperty_Undo_RestoresAtOriginalIndex()
    {
        var list = new List<ItemProperty> { Prop(1), Prop(2), Prop(3) };
        var cmd = new RemovePropertyCommand(list, new[] { 1 }, NoRefresh, "remove");
        cmd.Do();   // [1, 3]

        cmd.Undo(); // [1, 2, 3]

        Assert.Equal(new[] { 1, 2, 3 }, list.Select(p => (int)p.PropertyName));
    }

    [Fact]
    public void RemoveProperty_MultiIndex_Undo_RestoresAllInOrder()
    {
        var list = new List<ItemProperty> { Prop(1), Prop(2), Prop(3), Prop(4) };
        var cmd = new RemovePropertyCommand(list, new[] { 0, 2 }, NoRefresh, "remove");
        cmd.Do();   // [2, 4]
        Assert.Equal(new[] { 2, 4 }, list.Select(p => (int)p.PropertyName));

        cmd.Undo(); // [1, 2, 3, 4]

        Assert.Equal(new[] { 1, 2, 3, 4 }, list.Select(p => (int)p.PropertyName));
    }

    [Fact]
    public void RemoveProperty_DoUndoRedo_RoundTrips()
    {
        var list = new List<ItemProperty> { Prop(1), Prop(2), Prop(3) };
        var cmd = new RemovePropertyCommand(list, new[] { 1 }, NoRefresh, "remove");

        cmd.Do();   // [1, 3]
        cmd.Undo(); // [1, 2, 3]
        Assert.True(cmd.Do()); // redo by captured index → [1, 3]

        Assert.Equal(new[] { 1, 3 }, list.Select(p => (int)p.PropertyName));
    }

    [Fact]
    public void RemoveProperty_NoValidTargets_ReportsFalse()
    {
        var list = new List<ItemProperty> { Prop(1) };
        var cmd = new RemovePropertyCommand(list, new[] { 5 }, NoRefresh, "remove");

        Assert.False(cmd.Do()); // nothing to remove → don't record
        Assert.Single(list);
    }

    [Fact]
    public void RemoveProperty_WhenRefreshThrows_RollsBackAndReportsFalse()
    {
        var list = new List<ItemProperty> { Prop(1), Prop(2) };
        var cmd = new RemovePropertyCommand(list, new[] { 0 }, ThrowingRefresh, "remove");

        Assert.False(cmd.Do());
        Assert.Equal(new[] { 1, 2 }, list.Select(p => (int)p.PropertyName)); // restored
    }

    // --- BatchAddPropertiesCommand ---

    [Fact]
    public void BatchAdd_DoUndoRedo_RoundTrips()
    {
        var list = new List<ItemProperty> { Prop(1) };
        var toAdd = new[] { Prop(2), Prop(3) };
        var cmd = new BatchAddPropertiesCommand(list, toAdd, NoRefresh, "add 2");

        cmd.Do();   // [1, 2, 3]
        Assert.Equal(3, list.Count);
        cmd.Undo(); // [1]
        Assert.Equal(new[] { 1 }, list.Select(p => (int)p.PropertyName));
        Assert.True(cmd.Do()); // redo → [1, 2, 3]
        Assert.Equal(new[] { 1, 2, 3 }, list.Select(p => (int)p.PropertyName));
    }

    [Fact]
    public void BatchAdd_WhenRefreshThrows_RollsBackAndReportsFalse()
    {
        var list = new List<ItemProperty> { Prop(1) };
        var cmd = new BatchAddPropertiesCommand(list, new[] { Prop(2), Prop(3) }, ThrowingRefresh, "add 2");

        Assert.False(cmd.Do());
        Assert.Equal(new[] { 1 }, list.Select(p => (int)p.PropertyName));
    }

    // --- ClearPropertiesCommand ---

    [Fact]
    public void Clear_DoUndoRedo_RoundTrips()
    {
        var list = new List<ItemProperty> { Prop(1), Prop(2), Prop(3) };
        var cmd = new ClearPropertiesCommand(list, NoRefresh, "clear");

        cmd.Do();   // []
        Assert.Empty(list);
        cmd.Undo(); // [1, 2, 3]
        Assert.Equal(new[] { 1, 2, 3 }, list.Select(p => (int)p.PropertyName));
        Assert.True(cmd.Do()); // redo → []
        Assert.Empty(list);
    }

    [Fact]
    public void Clear_WhenAlreadyEmpty_ReportsFalse()
    {
        var list = new List<ItemProperty>();
        var cmd = new ClearPropertiesCommand(list, NoRefresh, "clear");

        Assert.False(cmd.Do());
    }

    [Fact]
    public void Clear_WhenRefreshThrows_RollsBackAndReportsFalse()
    {
        var list = new List<ItemProperty> { Prop(1), Prop(2) };
        var cmd = new ClearPropertiesCommand(list, ThrowingRefresh, "clear");

        Assert.False(cmd.Do());
        Assert.Equal(new[] { 1, 2 }, list.Select(p => (int)p.PropertyName)); // restored
    }

    // --- Integration with UndoRedoManager: refuse-to-push end to end ---

    [Fact]
    public void Manager_DoesNotRecord_AddWhoseRefreshThrows()
    {
        var mgr = new UndoRedoManager();
        var list = new List<ItemProperty> { Prop(1) };

        mgr.Execute(new AddPropertyCommand(list, Prop(6), ThrowingRefresh, "add"));

        Assert.False(mgr.CanUndo);        // command not recorded
        Assert.Single(list);              // model unchanged
    }

    [Fact]
    public void Manager_RecordsAndUndoes_SuccessfulAdd()
    {
        var mgr = new UndoRedoManager();
        var list = new List<ItemProperty> { Prop(1) };

        mgr.Execute(new AddPropertyCommand(list, Prop(6), NoRefresh, "add"));
        Assert.True(mgr.CanUndo);
        Assert.Equal(2, list.Count);

        mgr.Undo();
        Assert.Single(list);
        Assert.True(mgr.CanRedo);

        mgr.Redo();
        Assert.Equal(2, list.Count);
    }
}
