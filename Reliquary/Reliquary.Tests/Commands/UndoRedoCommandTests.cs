using System.Collections.ObjectModel;
using System.Collections.Generic;
using Radoub.Formats.Gff;
using Radoub.Formats.Utp;
using Radoub.UI.Undo;
using Radoub.UI.ViewModels;
using PlaceableEditor.Commands;
using PlaceableEditor.ViewModels;

namespace PlaceableEditor.Tests.Commands;

/// <summary>
/// Undo/redo coverage for the placeable mutation commands (design §6.5): a field set via
/// SetFieldCommand, and variable add/remove that must keep the model List and the UI
/// ObservableCollection in lock-step across undo and redo.
/// </summary>
public class UndoRedoCommandTests
{
    // --- SetField on a placeable property ---

    [Fact]
    public void SetField_OnHp_UndoRestoresOriginal()
    {
        var vm = new PlaceableViewModel(new UtpFile { HP = 10 });
        var mgr = new UndoRedoManager();

        mgr.Execute(new SetFieldCommand<short>(() => vm.HP, v => vm.HP = v, 50, "change HP"));
        Assert.Equal((short)50, vm.HP);

        mgr.Undo();
        Assert.Equal((short)10, vm.HP);

        mgr.Redo();
        Assert.Equal((short)50, vm.HP);
    }

    // --- AddVariable ---

    [Fact]
    public void AddVariable_AddsToModelAndUi()
    {
        var model = new List<Variable>();
        var ui = new ObservableCollection<VariableViewModel>();
        var mgr = new UndoRedoManager();
        var vm = new VariableViewModel { Name = "X", Type = VariableType.Int, ValueText = "5" };

        mgr.Execute(new AddVariableCommand(model, ui, vm));

        Assert.Single(model);
        Assert.Single(ui);
        Assert.Equal("X", model[0].Name);
    }

    [Fact]
    public void AddVariable_UndoRemovesFromBoth()
    {
        var model = new List<Variable>();
        var ui = new ObservableCollection<VariableViewModel>();
        var mgr = new UndoRedoManager();

        mgr.Execute(new AddVariableCommand(model, ui,
            new VariableViewModel { Name = "X", Type = VariableType.Int, ValueText = "5" }));
        mgr.Undo();

        Assert.Empty(model);
        Assert.Empty(ui);
    }

    [Fact]
    public void AddVariable_RedoReappliesToBoth()
    {
        var model = new List<Variable>();
        var ui = new ObservableCollection<VariableViewModel>();
        var mgr = new UndoRedoManager();

        mgr.Execute(new AddVariableCommand(model, ui,
            new VariableViewModel { Name = "X", Type = VariableType.Int, ValueText = "5" }));
        mgr.Undo();
        mgr.Redo();

        Assert.Single(model);
        Assert.Single(ui);
        Assert.Equal("X", ui[0].Name);
    }

    // --- RemoveVariable ---

    [Fact]
    public void RemoveVariable_RemovesFromBoth()
    {
        var vmA = new VariableViewModel { Name = "A", Type = VariableType.Int, ValueText = "1" };
        var vmB = new VariableViewModel { Name = "B", Type = VariableType.Int, ValueText = "2" };
        var model = new List<Variable> { vmA.ToVariable(), vmB.ToVariable() };
        var ui = new ObservableCollection<VariableViewModel> { vmA, vmB };
        var mgr = new UndoRedoManager();

        mgr.Execute(new RemoveVariableCommand(model, ui, vmB));

        Assert.Single(ui);
        Assert.Single(model);
        Assert.Equal("A", ui[0].Name);
    }

    [Fact]
    public void RemoveVariable_UndoReinsertsAtOriginalIndex()
    {
        var vmA = new VariableViewModel { Name = "A", Type = VariableType.Int, ValueText = "1" };
        var vmB = new VariableViewModel { Name = "B", Type = VariableType.Int, ValueText = "2" };
        var vmC = new VariableViewModel { Name = "C", Type = VariableType.Int, ValueText = "3" };
        var model = new List<Variable> { vmA.ToVariable(), vmB.ToVariable(), vmC.ToVariable() };
        var ui = new ObservableCollection<VariableViewModel> { vmA, vmB, vmC };
        var mgr = new UndoRedoManager();

        mgr.Execute(new RemoveVariableCommand(model, ui, vmB)); // remove middle
        mgr.Undo();

        Assert.Equal(3, ui.Count);
        Assert.Equal("B", ui[1].Name);     // back in the middle
        Assert.Equal("B", model[1].Name);
    }

    [Fact]
    public void RemoveVariable_RedoRemovesAgain()
    {
        var vmA = new VariableViewModel { Name = "A", Type = VariableType.Int, ValueText = "1" };
        var model = new List<Variable> { vmA.ToVariable() };
        var ui = new ObservableCollection<VariableViewModel> { vmA };
        var mgr = new UndoRedoManager();

        mgr.Execute(new RemoveVariableCommand(model, ui, vmA));
        mgr.Undo();
        mgr.Redo();

        Assert.Empty(ui);
        Assert.Empty(model);
    }

    // --- AddInventoryItem (keeps PlaceableItem model list + ItemViewModel UI in lock-step) ---

    private static ItemViewModel BackpackItem(string resRef)
        => new ItemViewModel { Name = resRef, ResRef = resRef };

    [Fact]
    public void AddInventoryItem_AddsToModelAndUi()
    {
        var model = new List<PlaceableItem>();
        var ui = new ObservableCollection<ItemViewModel>();
        var mgr = new UndoRedoManager();

        mgr.Execute(new AddInventoryItemCommand(model, ui, BackpackItem("nw_it_gold001")));

        Assert.Single(model);
        Assert.Single(ui);
        Assert.Equal("nw_it_gold001", model[0].InventoryRes);
    }

    [Fact]
    public void AddInventoryItem_UndoRemovesFromBoth()
    {
        var model = new List<PlaceableItem>();
        var ui = new ObservableCollection<ItemViewModel>();
        var mgr = new UndoRedoManager();

        mgr.Execute(new AddInventoryItemCommand(model, ui, BackpackItem("nw_it_gold001")));
        mgr.Undo();

        Assert.Empty(model);
        Assert.Empty(ui);
    }

    [Fact]
    public void AddInventoryItem_RedoReappliesToBoth()
    {
        var model = new List<PlaceableItem>();
        var ui = new ObservableCollection<ItemViewModel>();
        var mgr = new UndoRedoManager();

        mgr.Execute(new AddInventoryItemCommand(model, ui, BackpackItem("nw_it_gold001")));
        mgr.Undo();
        mgr.Redo();

        Assert.Single(model);
        Assert.Single(ui);
        Assert.Equal("nw_it_gold001", ui[0].ResRef);
    }

    // --- RemoveInventoryItem ---

    [Fact]
    public void RemoveInventoryItem_RemovesFromBoth()
    {
        var a = BackpackItem("item_a");
        var b = BackpackItem("item_b");
        var model = new List<PlaceableItem>
        {
            new() { InventoryRes = "item_a" },
            new() { InventoryRes = "item_b" }
        };
        var ui = new ObservableCollection<ItemViewModel> { a, b };
        var mgr = new UndoRedoManager();

        mgr.Execute(new RemoveInventoryItemCommand(model, ui, b));

        Assert.Single(ui);
        Assert.Single(model);
        Assert.Equal("item_a", ui[0].ResRef);
        Assert.Equal("item_a", model[0].InventoryRes);
    }

    [Fact]
    public void RemoveInventoryItem_UndoReinsertsAtOriginalIndex()
    {
        var a = BackpackItem("item_a");
        var b = BackpackItem("item_b");
        var c = BackpackItem("item_c");
        var model = new List<PlaceableItem>
        {
            new() { InventoryRes = "item_a" },
            new() { InventoryRes = "item_b" },
            new() { InventoryRes = "item_c" }
        };
        var ui = new ObservableCollection<ItemViewModel> { a, b, c };
        var mgr = new UndoRedoManager();

        mgr.Execute(new RemoveInventoryItemCommand(model, ui, b)); // remove middle
        mgr.Undo();

        Assert.Equal(3, ui.Count);
        Assert.Equal("item_b", ui[1].ResRef);
        Assert.Equal("item_b", model[1].InventoryRes);
    }

    [Fact]
    public void RemoveInventoryItem_RedoRemovesAgain()
    {
        var a = BackpackItem("item_a");
        var model = new List<PlaceableItem> { new() { InventoryRes = "item_a" } };
        var ui = new ObservableCollection<ItemViewModel> { a };
        var mgr = new UndoRedoManager();

        mgr.Execute(new RemoveInventoryItemCommand(model, ui, a));
        mgr.Undo();
        mgr.Redo();

        Assert.Empty(ui);
        Assert.Empty(model);
    }
}
