using System.Collections.ObjectModel;
using System.Linq;
using MerchantEditor.Commands;
using MerchantEditor.ViewModels;
using Radoub.Formats.Gff;
using Radoub.UI.Undo;
using Radoub.UI.ViewModels;
using Xunit;

namespace MerchantEditor.Tests;

/// <summary>
/// Undo/redo coverage for Fence mutation commands (#2255 / epic #2231). Fence is a blueprint
/// editor: the UTM model is rebuilt from the UI at save time, so the inventory and variable
/// commands operate on the single UI <see cref="ObservableCollection{T}"/> that is the live
/// source of truth — there is no parallel model list to keep index-aligned during editing.
/// </summary>
public class FenceUndoCommandTests
{
    private static StoreItemViewModel Item(string resRef, int panelId = 0)
        => new() { ResRef = resRef, DisplayName = resRef, PanelId = panelId };

    // --- AddStoreItems (batch add as one undo step) ---

    [Fact]
    public void AddStoreItems_AddsAllToCollection()
    {
        var ui = new ObservableCollection<StoreItemViewModel>();
        var mgr = new UndoRedoManager();

        mgr.Execute(new AddStoreItemsCommand(ui, new[] { Item("sw_a"), Item("sw_b") }));

        Assert.Equal(2, ui.Count);
        Assert.Equal("sw_a", ui[0].ResRef);
        Assert.Equal("sw_b", ui[1].ResRef);
    }

    [Fact]
    public void AddStoreItems_UndoRemovesAll()
    {
        var ui = new ObservableCollection<StoreItemViewModel>();
        var mgr = new UndoRedoManager();

        mgr.Execute(new AddStoreItemsCommand(ui, new[] { Item("sw_a"), Item("sw_b") }));
        mgr.Undo();

        Assert.Empty(ui);
    }

    [Fact]
    public void AddStoreItems_RedoReappliesAll()
    {
        var ui = new ObservableCollection<StoreItemViewModel>();
        var mgr = new UndoRedoManager();

        mgr.Execute(new AddStoreItemsCommand(ui, new[] { Item("sw_a"), Item("sw_b") }));
        mgr.Undo();
        mgr.Redo();

        Assert.Equal(2, ui.Count);
        Assert.Equal("sw_b", ui[1].ResRef);
    }

    [Fact]
    public void AddStoreItems_Empty_DoesNotRecord()
    {
        var ui = new ObservableCollection<StoreItemViewModel>();
        var mgr = new UndoRedoManager();

        mgr.Execute(new AddStoreItemsCommand(ui, System.Array.Empty<StoreItemViewModel>()));

        Assert.False(mgr.CanUndo);
    }

    // --- RemoveStoreItems (batch remove as one undo step, restoring positions) ---

    [Fact]
    public void RemoveStoreItems_RemovesSelected()
    {
        var a = Item("a"); var b = Item("b"); var c = Item("c");
        var ui = new ObservableCollection<StoreItemViewModel> { a, b, c };
        var mgr = new UndoRedoManager();

        mgr.Execute(new RemoveStoreItemsCommand(ui, new[] { b }));

        Assert.Equal(2, ui.Count);
        Assert.Equal("a", ui[0].ResRef);
        Assert.Equal("c", ui[1].ResRef);
    }

    [Fact]
    public void RemoveStoreItems_UndoReinsertsAtOriginalIndex()
    {
        var a = Item("a"); var b = Item("b"); var c = Item("c");
        var ui = new ObservableCollection<StoreItemViewModel> { a, b, c };
        var mgr = new UndoRedoManager();

        mgr.Execute(new RemoveStoreItemsCommand(ui, new[] { b })); // remove middle
        mgr.Undo();

        Assert.Equal(3, ui.Count);
        Assert.Equal("b", ui[1].ResRef); // back in the middle
    }

    [Fact]
    public void RemoveStoreItems_MultipleNonContiguous_UndoRestoresAllPositions()
    {
        var a = Item("a"); var b = Item("b"); var c = Item("c"); var d = Item("d");
        var ui = new ObservableCollection<StoreItemViewModel> { a, b, c, d };
        var mgr = new UndoRedoManager();

        mgr.Execute(new RemoveStoreItemsCommand(ui, new[] { a, c })); // remove indices 0 and 2
        Assert.Equal(2, ui.Count);

        mgr.Undo();
        Assert.Equal(4, ui.Count);
        Assert.Equal("a", ui[0].ResRef);
        Assert.Equal("b", ui[1].ResRef);
        Assert.Equal("c", ui[2].ResRef);
        Assert.Equal("d", ui[3].ResRef);
    }

    [Fact]
    public void RemoveStoreItems_RedoRemovesAgain()
    {
        var a = Item("a");
        var ui = new ObservableCollection<StoreItemViewModel> { a };
        var mgr = new UndoRedoManager();

        mgr.Execute(new RemoveStoreItemsCommand(ui, new[] { a }));
        mgr.Undo();
        mgr.Redo();

        Assert.Empty(ui);
    }

    [Fact]
    public void RemoveStoreItems_None_DoesNotRecord()
    {
        var ui = new ObservableCollection<StoreItemViewModel> { Item("a") };
        var mgr = new UndoRedoManager();

        mgr.Execute(new RemoveStoreItemsCommand(ui, System.Array.Empty<StoreItemViewModel>()));

        Assert.False(mgr.CanUndo);
    }

    // --- AddVariable ---

    [Fact]
    public void AddVariable_AddsToCollection()
    {
        var ui = new ObservableCollection<VariableViewModel>();
        var mgr = new UndoRedoManager();

        mgr.Execute(new AddVariableCommand(ui,
            new VariableViewModel { Name = "X", Type = VariableType.Int, ValueText = "5" }));

        Assert.Single(ui);
        Assert.Equal("X", ui[0].Name);
    }

    [Fact]
    public void AddVariable_UndoRemoves()
    {
        var ui = new ObservableCollection<VariableViewModel>();
        var mgr = new UndoRedoManager();

        mgr.Execute(new AddVariableCommand(ui,
            new VariableViewModel { Name = "X", Type = VariableType.Int, ValueText = "5" }));
        mgr.Undo();

        Assert.Empty(ui);
    }

    [Fact]
    public void AddVariable_RedoReapplies()
    {
        var ui = new ObservableCollection<VariableViewModel>();
        var mgr = new UndoRedoManager();

        mgr.Execute(new AddVariableCommand(ui,
            new VariableViewModel { Name = "X", Type = VariableType.Int, ValueText = "5" }));
        mgr.Undo();
        mgr.Redo();

        Assert.Single(ui);
        Assert.Equal("X", ui[0].Name);
    }

    // --- RemoveVariable ---

    [Fact]
    public void RemoveVariables_RemovesSelected()
    {
        var vmA = new VariableViewModel { Name = "A", Type = VariableType.Int, ValueText = "1" };
        var vmB = new VariableViewModel { Name = "B", Type = VariableType.Int, ValueText = "2" };
        var ui = new ObservableCollection<VariableViewModel> { vmA, vmB };
        var mgr = new UndoRedoManager();

        mgr.Execute(new RemoveVariablesCommand(ui, new[] { vmB }));

        Assert.Single(ui);
        Assert.Equal("A", ui[0].Name);
    }

    [Fact]
    public void RemoveVariables_UndoReinsertsAtOriginalIndex()
    {
        var vmA = new VariableViewModel { Name = "A", Type = VariableType.Int, ValueText = "1" };
        var vmB = new VariableViewModel { Name = "B", Type = VariableType.Int, ValueText = "2" };
        var vmC = new VariableViewModel { Name = "C", Type = VariableType.Int, ValueText = "3" };
        var ui = new ObservableCollection<VariableViewModel> { vmA, vmB, vmC };
        var mgr = new UndoRedoManager();

        mgr.Execute(new RemoveVariablesCommand(ui, new[] { vmB }));
        mgr.Undo();

        Assert.Equal(3, ui.Count);
        Assert.Equal("B", ui[1].Name);
    }

    [Fact]
    public void RemoveVariables_RedoRemovesAgain()
    {
        var vmA = new VariableViewModel { Name = "A", Type = VariableType.Int, ValueText = "1" };
        var ui = new ObservableCollection<VariableViewModel> { vmA };
        var mgr = new UndoRedoManager();

        mgr.Execute(new RemoveVariablesCommand(ui, new[] { vmA }));
        mgr.Undo();
        mgr.Redo();

        Assert.Empty(ui);
    }

    // --- SetInfinite (toggle Infinite on a set of items, restoring each item's prior value) ---

    [Fact]
    public void SetInfinite_SetsAllTargetsToValue()
    {
        var a = Item("a"); var b = Item("b"); b.Infinite = true;
        var mgr = new UndoRedoManager();

        mgr.Execute(new SetInfiniteCommand(new[] { a, b }, true));

        Assert.True(a.Infinite);
        Assert.True(b.Infinite);
    }

    [Fact]
    public void SetInfinite_UndoRestoresEachPriorValue()
    {
        var a = Item("a"); a.Infinite = false;
        var b = Item("b"); b.Infinite = true;
        var mgr = new UndoRedoManager();

        mgr.Execute(new SetInfiniteCommand(new[] { a, b }, true));
        mgr.Undo();

        Assert.False(a.Infinite); // restored to its own prior value
        Assert.True(b.Infinite);  // unchanged, stays true
    }

    [Fact]
    public void SetInfinite_NoActualChange_DoesNotRecord()
    {
        var a = Item("a"); a.Infinite = true;
        var mgr = new UndoRedoManager();

        mgr.Execute(new SetInfiniteCommand(new[] { a }, true)); // already true

        Assert.False(mgr.CanUndo);
    }

    [Fact]
    public void SetInfinite_RedoReapplies()
    {
        var a = Item("a"); a.Infinite = false;
        var mgr = new UndoRedoManager();

        mgr.Execute(new SetInfiniteCommand(new[] { a }, true));
        mgr.Undo();
        mgr.Redo();

        Assert.True(a.Infinite);
    }

    // --- SetResRef (single store item ResRef edit) ---

    [Fact]
    public void SetResRef_AppliesNewValue()
    {
        var a = Item("old");
        var mgr = new UndoRedoManager();

        mgr.Execute(new SetResRefCommand(a, "old", "new"));

        Assert.Equal("new", a.ResRef);
    }

    [Fact]
    public void SetResRef_UndoRestoresOld()
    {
        var a = Item("old");
        a.ResRef = "new"; // simulate the grid binding already applied the edit
        var mgr = new UndoRedoManager();

        mgr.Execute(new SetResRefCommand(a, "old", "new"));
        mgr.Undo();

        Assert.Equal("old", a.ResRef);
    }

    [Fact]
    public void SetResRef_RedoReapplies()
    {
        var a = Item("old");
        var mgr = new UndoRedoManager();

        mgr.Execute(new SetResRefCommand(a, "old", "new"));
        mgr.Undo();
        mgr.Redo();

        Assert.Equal("new", a.ResRef);
    }

    [Fact]
    public void SetResRef_NoChange_DoesNotRecord()
    {
        var a = Item("same");
        var mgr = new UndoRedoManager();

        mgr.Execute(new SetResRefCommand(a, "same", "same"));

        Assert.False(mgr.CanUndo);
    }

    // --- BuyRestrictionsSnapshot (whole-state capture/restore) ---

    private static ObservableCollection<SelectableBaseItemTypeViewModel> Types(params int[] indices)
    {
        var c = new ObservableCollection<SelectableBaseItemTypeViewModel>();
        foreach (var i in indices)
            c.Add(new SelectableBaseItemTypeViewModel(i, $"type{i}"));
        return c;
    }

    [Fact]
    public void BuyRestrictions_CaptureRecordsModeAndSelection()
    {
        var types = Types(1, 2, 3);
        types[0].IsSelected = true;
        types[2].IsSelected = true;

        var snap = BuyRestrictionsSnapshot.Capture(BuyMode.WillOnlyBuy, types);

        Assert.Equal(BuyMode.WillOnlyBuy, snap.Mode);
        Assert.Equal(new[] { 1, 3 }, snap.SelectedIndices.OrderBy(x => x));
    }

    [Fact]
    public void BuyRestrictions_ApplyRestoresSelection()
    {
        var types = Types(1, 2, 3);
        var snap = new BuyRestrictionsSnapshot(BuyMode.WillNotBuy, new[] { 2 });

        snap.ApplyTo(types);

        Assert.False(types[0].IsSelected);
        Assert.True(types[1].IsSelected);
        Assert.False(types[2].IsSelected);
    }

    [Fact]
    public void BuyRestrictions_UndoRestoresPriorState_ViaRelayCommand()
    {
        var types = Types(1, 2, 3);
        types[0].IsSelected = true; // initial: OnlyBuy {1}
        var mode = BuyMode.WillOnlyBuy;

        var before = BuyRestrictionsSnapshot.Capture(mode, types);

        // Simulate user switching to "Buy All" (clears selection).
        foreach (var t in types) t.IsSelected = false;
        mode = BuyMode.All;
        var after = BuyRestrictionsSnapshot.Capture(mode, types);

        var mgr = new UndoRedoManager();
        mgr.Execute(new RelayUndoableCommand(
            () => { after.ApplyTo(types); },
            () => { before.ApplyTo(types); },
            "change buy restrictions"));

        Assert.False(types[0].IsSelected); // after state

        mgr.Undo();
        Assert.True(types[0].IsSelected);  // before state restored
        Assert.False(types[1].IsSelected);

        mgr.Redo();
        Assert.False(types[0].IsSelected); // after re-applied
    }

    [Fact]
    public void BuyRestrictions_Equality_IgnoresOrder()
    {
        var a = new BuyRestrictionsSnapshot(BuyMode.WillNotBuy, new[] { 3, 1, 2 });
        var b = new BuyRestrictionsSnapshot(BuyMode.WillNotBuy, new[] { 1, 2, 3 });
        Assert.Equal(a, b);
    }
}
