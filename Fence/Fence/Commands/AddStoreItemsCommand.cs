using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using MerchantEditor.ViewModels;
using Radoub.UI.Undo;

namespace MerchantEditor.Commands;

/// <summary>
/// Adds one or more items to the store inventory collection as a single undo step. Fence rebuilds
/// the UTM <c>StoreList</c> from this collection at save time (see <c>UpdateStoreFromUI</c>), so the
/// collection is the live source of truth and the command needs no parallel model list. Reversible:
/// Undo removes exactly the appended items. An empty add records nothing (Do returns false).
/// </summary>
public sealed class AddStoreItemsCommand : IUndoableCommand
{
    private readonly ObservableCollection<StoreItemViewModel> _items;
    private readonly List<StoreItemViewModel> _added;

    public AddStoreItemsCommand(ObservableCollection<StoreItemViewModel> items,
        IEnumerable<StoreItemViewModel> toAdd)
    {
        _items = items ?? throw new ArgumentNullException(nameof(items));
        _added = (toAdd ?? throw new ArgumentNullException(nameof(toAdd))).ToList();
    }

    public string Description => _added.Count == 1
        ? (string.IsNullOrEmpty(_added[0].ResRef) ? "add item" : $"add item {_added[0].ResRef}")
        : $"add {_added.Count} items";

    public bool Do()
    {
        if (_added.Count == 0) return false; // nothing to add → don't record
        foreach (var item in _added)
            _items.Add(item);
        return true;
    }

    public void Undo()
    {
        foreach (var item in _added)
            _items.Remove(item);
    }
}
