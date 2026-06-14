using ItemEditor.Services;
using Radoub.Formats.Uti;

namespace ItemEditor.Tests.Services;

/// <summary>
/// Tests for PropertyListMutator — the pure model-mutation + rollback logic extracted
/// from the Relique property handlers (#2258). Each operation applies a model change,
/// runs a refresh callback, and rolls the model back if the refresh throws, mirroring
/// the canonical TryAddProperty pattern (#2166).
/// </summary>
public class PropertyListMutatorTests
{
    private static ItemProperty Prop(ushort name, ushort subtype = 0) =>
        new() { PropertyName = name, Subtype = subtype, ChanceAppear = 100 };

    private static List<ItemProperty> List(params ushort[] names)
    {
        var list = new List<ItemProperty>();
        foreach (var n in names) list.Add(Prop(n));
        return list;
    }

    // --- BatchAdd ---

    [Fact]
    public void BatchAdd_RefreshSucceeds_KeepsAddedItems()
    {
        var list = List(1, 2);
        var toAdd = new[] { Prop(3), Prop(4) };

        var ok = PropertyListMutator.BatchAdd(list, toAdd, () => { });

        Assert.True(ok);
        Assert.Equal(new ushort[] { 1, 2, 3, 4 }, list.Select(p => p.PropertyName));
    }

    [Fact]
    public void BatchAdd_RefreshThrows_RollsBackAllAdded()
    {
        var list = List(1, 2);
        var toAdd = new[] { Prop(3), Prop(4) };

        var ok = PropertyListMutator.BatchAdd(list, toAdd, () => throw new InvalidOperationException("boom"));

        Assert.False(ok);
        Assert.Equal(new ushort[] { 1, 2 }, list.Select(p => p.PropertyName));
    }

    [Fact]
    public void BatchAdd_EmptyAddList_NoRefreshNoChange()
    {
        var list = List(1, 2);
        var refreshCalled = false;

        var ok = PropertyListMutator.BatchAdd(list, Array.Empty<ItemProperty>(), () => refreshCalled = true);

        Assert.False(ok);
        Assert.False(refreshCalled);
        Assert.Equal(new ushort[] { 1, 2 }, list.Select(p => p.PropertyName));
    }

    // --- RemoveAt ---

    [Fact]
    public void RemoveAt_RefreshSucceeds_KeepsRemoval()
    {
        var list = List(1, 2, 3, 4);

        var ok = PropertyListMutator.RemoveAt(list, new[] { 1, 3 }, () => { });

        Assert.True(ok);
        Assert.Equal(new ushort[] { 1, 3 }, list.Select(p => p.PropertyName));
    }

    [Fact]
    public void RemoveAt_RefreshThrows_RestoresRemovedAtOriginalPositions()
    {
        var list = List(1, 2, 3, 4);

        var ok = PropertyListMutator.RemoveAt(list, new[] { 1, 3 }, () => throw new InvalidOperationException("boom"));

        Assert.False(ok);
        Assert.Equal(new ushort[] { 1, 2, 3, 4 }, list.Select(p => p.PropertyName));
    }

    [Fact]
    public void RemoveAt_EmptyIndices_NoRefreshNoChange()
    {
        var list = List(1, 2);
        var refreshCalled = false;

        var ok = PropertyListMutator.RemoveAt(list, Array.Empty<int>(), () => refreshCalled = true);

        Assert.False(ok);
        Assert.False(refreshCalled);
        Assert.Equal(new ushort[] { 1, 2 }, list.Select(p => p.PropertyName));
    }

    // --- ClearAll ---

    [Fact]
    public void ClearAll_RefreshSucceeds_ListEmpty()
    {
        var list = List(1, 2, 3);

        var ok = PropertyListMutator.ClearAll(list, () => { });

        Assert.True(ok);
        Assert.Empty(list);
    }

    [Fact]
    public void ClearAll_RefreshThrows_RestoresSnapshotInOrder()
    {
        var list = List(1, 2, 3);

        var ok = PropertyListMutator.ClearAll(list, () => throw new InvalidOperationException("boom"));

        Assert.False(ok);
        Assert.Equal(new ushort[] { 1, 2, 3 }, list.Select(p => p.PropertyName));
    }

    [Fact]
    public void ClearAll_EmptyList_NoRefreshNoChange()
    {
        var list = new List<ItemProperty>();
        var refreshCalled = false;

        var ok = PropertyListMutator.ClearAll(list, () => refreshCalled = true);

        Assert.False(ok);
        Assert.False(refreshCalled);
        Assert.Empty(list);
    }

    // --- InsertAt (inverse of RemoveAt; used by undo of remove/clear, #2231) ---

    [Fact]
    public void InsertAt_RefreshSucceeds_RestoresAtOriginalPositions()
    {
        var list = List(1, 3); // 2 and 4 were removed from [1,2,3,4]
        var entries = new[] { (Index: 1, Value: Prop(2)), (Index: 3, Value: Prop(4)) };

        var ok = PropertyListMutator.InsertAt(list, entries, () => { });

        Assert.True(ok);
        Assert.Equal(new ushort[] { 1, 2, 3, 4 }, list.Select(p => p.PropertyName));
    }

    [Fact]
    public void InsertAt_UnorderedEntries_StillLandAtCorrectIndices()
    {
        var list = List(2, 4); // 1 and 3 removed from [1,2,3,4]
        var entries = new[] { (Index: 2, Value: Prop(3)), (Index: 0, Value: Prop(1)) };

        var ok = PropertyListMutator.InsertAt(list, entries, () => { });

        Assert.True(ok);
        Assert.Equal(new ushort[] { 1, 2, 3, 4 }, list.Select(p => p.PropertyName));
    }

    [Fact]
    public void InsertAt_RefreshThrows_RemovesReinsertedEntries()
    {
        var list = List(1, 3);
        var entries = new[] { (Index: 1, Value: Prop(2)) };

        var ok = PropertyListMutator.InsertAt(list, entries, () => throw new InvalidOperationException("boom"));

        Assert.False(ok);
        Assert.Equal(new ushort[] { 1, 3 }, list.Select(p => p.PropertyName)); // rolled back
    }

    [Fact]
    public void InsertAt_EmptyEntries_NoRefreshNoChange()
    {
        var list = List(1, 2);
        var refreshCalled = false;

        var ok = PropertyListMutator.InsertAt(
            list, Array.Empty<(int, ItemProperty)>(), () => refreshCalled = true);

        Assert.False(ok);
        Assert.False(refreshCalled);
        Assert.Equal(new ushort[] { 1, 2 }, list.Select(p => p.PropertyName));
    }
}
