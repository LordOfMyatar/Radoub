using System;
using System.Collections.Generic;
using System.Linq;
using ItemEditor.Services;
using Radoub.Formats.Uti;

namespace ItemEditor.Commands;

/// <summary>
/// Adds several item properties as a single undo step (the "Add checked" path, #2234). Wraps
/// <see cref="PropertyListMutator.BatchAdd"/> on Do and <see cref="PropertyListMutator.RemoveAt"/>
/// on Undo so the rollback-on-refresh-failure seam (#2258) guards both directions. Part of the
/// cross-tool undo epic (#2231).
/// </summary>
public sealed class BatchAddPropertiesCommand : Radoub.UI.Undo.IUndoableCommand
{
    private readonly List<ItemProperty> _properties;
    private readonly IReadOnlyList<ItemProperty> _toAdd;
    private readonly Action _refresh;
    private int _firstIndex = -1;

    public BatchAddPropertiesCommand(List<ItemProperty> properties, IReadOnlyList<ItemProperty> toAdd, Action refresh, string description)
    {
        _properties = properties ?? throw new ArgumentNullException(nameof(properties));
        _toAdd = toAdd ?? throw new ArgumentNullException(nameof(toAdd));
        _refresh = refresh ?? throw new ArgumentNullException(nameof(refresh));
        Description = description ?? $"add {toAdd.Count} properties";
    }

    public string Description { get; }

    public bool Do()
    {
        // BatchAdd appends the range to the end; capture the appended range for Undo. Safe because
        // history is linear (any new Execute clears redo) so the range can't be shifted before Undo.
        int firstIfApplied = _properties.Count;
        if (!PropertyListMutator.BatchAdd(_properties, _toAdd, _refresh))
            return false;
        _firstIndex = firstIfApplied;
        return true;
    }

    public void Undo()
    {
        if (_firstIndex < 0) return;
        var indices = Enumerable.Range(_firstIndex, _toAdd.Count).ToList();
        PropertyListMutator.RemoveAt(_properties, indices, _refresh);
    }
}
