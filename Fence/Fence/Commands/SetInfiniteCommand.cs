using System;
using System.Collections.Generic;
using System.Linq;
using MerchantEditor.ViewModels;
using Radoub.UI.Undo;

namespace MerchantEditor.Commands;

/// <summary>
/// Sets the <see cref="StoreItemViewModel.Infinite"/> flag on a set of store items to a single value
/// as one undo step, capturing each item's prior value so Undo restores them individually. Records
/// nothing if no item actually changes (Do returns false).
/// </summary>
public sealed class SetInfiniteCommand : IUndoableCommand
{
    private readonly List<(StoreItemViewModel Item, bool OldValue)> _changes;
    private readonly bool _newValue;

    public SetInfiniteCommand(IEnumerable<StoreItemViewModel> items, bool newValue)
    {
        if (items == null) throw new ArgumentNullException(nameof(items));
        _newValue = newValue;
        _changes = items
            .Where(i => i.Infinite != newValue)
            .Select(i => (Item: i, OldValue: i.Infinite))
            .ToList();
    }

    public string Description => _changes.Count == 1
        ? (_newValue ? "set infinite" : "clear infinite")
        : (_newValue ? $"set infinite ({_changes.Count})" : $"clear infinite ({_changes.Count})");

    public bool Do()
    {
        if (_changes.Count == 0) return false; // no real change → don't record
        foreach (var (item, _) in _changes)
            item.Infinite = _newValue;
        return true;
    }

    public void Undo()
    {
        foreach (var (item, oldValue) in _changes)
            item.Infinite = oldValue;
    }
}
