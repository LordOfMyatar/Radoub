using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Radoub.Formats.Utp;
using Radoub.UI.Undo;
using Radoub.UI.ViewModels;

namespace PlaceableEditor.Commands;

/// <summary>
/// Adds an item to a placeable's inventory: appends a <see cref="PlaceableItem"/> to the model
/// list and the resolved <see cref="ItemViewModel"/> to the UI collection, keeping the two
/// index-aligned. Reversible — Undo removes the same entry from both. UTP placeable contents only
/// store the item ResRef + grid position (the format carries no per-instance stack/charges/plot),
/// so the model entry holds just <see cref="PlaceableItem.InventoryRes"/>.
/// </summary>
public sealed class AddInventoryItemCommand : IUndoableCommand
{
    private readonly List<PlaceableItem> _model;
    private readonly ObservableCollection<ItemViewModel> _ui;
    private readonly ItemViewModel _vm;
    private PlaceableItem? _modelEntry;

    public AddInventoryItemCommand(List<PlaceableItem> model, ObservableCollection<ItemViewModel> ui, ItemViewModel vm)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _ui = ui ?? throw new ArgumentNullException(nameof(ui));
        _vm = vm ?? throw new ArgumentNullException(nameof(vm));
    }

    public string Description =>
        string.IsNullOrEmpty(_vm.ResRef) ? "add item" : $"add item {_vm.ResRef}";

    public void Do()
    {
        _modelEntry = new PlaceableItem { InventoryRes = _vm.ResRef };
        _model.Add(_modelEntry);
        _ui.Add(_vm);
    }

    public void Undo()
    {
        _ui.Remove(_vm);
        if (_modelEntry != null) _model.Remove(_modelEntry);
    }
}
