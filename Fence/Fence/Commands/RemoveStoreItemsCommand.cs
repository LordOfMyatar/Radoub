using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using MerchantEditor.ViewModels;
using Radoub.UI.Undo;

namespace MerchantEditor.Commands;

/// <summary>
/// Removes one or more selected items from the store inventory collection as a single undo step,
/// capturing each item's original index so Undo reinserts them in place. Reversible across any
/// selection (contiguous or not). Removing nothing records nothing (Do returns false).
/// </summary>
public sealed class RemoveStoreItemsCommand : IUndoableCommand
{
    private readonly ObservableCollection<StoreItemViewModel> _items;
    private readonly List<StoreItemViewModel> _targets;
    private List<(int Index, StoreItemViewModel Item)> _removed = new();

    public RemoveStoreItemsCommand(ObservableCollection<StoreItemViewModel> items,
        IEnumerable<StoreItemViewModel> toRemove)
    {
        _items = items ?? throw new ArgumentNullException(nameof(items));
        _targets = (toRemove ?? throw new ArgumentNullException(nameof(toRemove))).ToList();
    }

    public string Description => _targets.Count == 1
        ? (string.IsNullOrEmpty(_targets[0].ResRef) ? "remove item" : $"remove item {_targets[0].ResRef}")
        : $"remove {_targets.Count} items";

    public bool Do()
    {
        // Capture (index, item) for present targets, then remove descending so earlier
        // indices stay valid as we delete.
        _removed = _targets
            .Select(t => (Index: _items.IndexOf(t), Item: t))
            .Where(e => e.Index >= 0)
            .OrderByDescending(e => e.Index)
            .ToList();

        if (_removed.Count == 0) return false; // nothing present → don't record

        foreach (var (index, _) in _removed)
            _items.RemoveAt(index);
        return true;
    }

    public void Undo()
    {
        // Reinsert ascending so each item lands at its original index.
        foreach (var (index, item) in _removed.OrderBy(e => e.Index))
            _items.Insert(Math.Min(index, _items.Count), item);
    }
}
