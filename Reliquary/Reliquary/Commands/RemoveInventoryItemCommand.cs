using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Radoub.Formats.Utp;
using Radoub.UI.Undo;
using Radoub.UI.ViewModels;

namespace PlaceableEditor.Commands;

/// <summary>
/// Removes an item from a placeable's inventory, capturing the removed <see cref="PlaceableItem"/>
/// and its index so Undo reinserts it in place. The model list is index-aligned with the UI
/// <see cref="ItemViewModel"/> collection (mirrors <see cref="RemoveVariableCommand"/>).
/// </summary>
public sealed class RemoveInventoryItemCommand : IUndoableCommand
{
    private readonly List<PlaceableItem> _model;
    private readonly ObservableCollection<ItemViewModel> _ui;
    private readonly ItemViewModel _vm;

    private int _index = -1;
    private PlaceableItem? _modelEntry;

    public RemoveInventoryItemCommand(List<PlaceableItem> model, ObservableCollection<ItemViewModel> ui, ItemViewModel vm)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _ui = ui ?? throw new ArgumentNullException(nameof(ui));
        _vm = vm ?? throw new ArgumentNullException(nameof(vm));
    }

    public string Description =>
        string.IsNullOrEmpty(_vm.ResRef) ? "remove item" : $"remove item {_vm.ResRef}";

    public bool Do()
    {
        _index = _ui.IndexOf(_vm);
        if (_index < 0) return false; // not present; nothing removed → don't record

        if (_index < _model.Count)
        {
            _modelEntry = _model[_index];
            _model.RemoveAt(_index);
        }
        _ui.RemoveAt(_index);
        return true;
    }

    public void Undo()
    {
        if (_index < 0) return;
        _ui.Insert(_index, _vm);
        if (_modelEntry != null)
            _model.Insert(Math.Min(_index, _model.Count), _modelEntry);
    }
}
