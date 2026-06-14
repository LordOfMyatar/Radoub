using System;
using System.Collections.Generic;
using System.Linq;
using ItemEditor.Services;
using Radoub.Formats.Uti;

namespace ItemEditor.Commands;

/// <summary>
/// Clears all item properties as a single undo step. Captures the full list before clearing so
/// Undo restores it at the original positions. Wraps <see cref="PropertyListMutator.ClearAll"/> on
/// Do and <see cref="PropertyListMutator.InsertAt"/> on Undo so the rollback-on-refresh-failure
/// seam (#2258) guards both directions. Part of the cross-tool undo epic (#2231).
/// </summary>
public sealed class ClearPropertiesCommand : Radoub.UI.Undo.IUndoableCommand
{
    private readonly List<ItemProperty> _properties;
    private readonly Action _refresh;
    private List<(int Index, ItemProperty Value)> _snapshot = new();

    public ClearPropertiesCommand(List<ItemProperty> properties, Action refresh, string description)
    {
        _properties = properties ?? throw new ArgumentNullException(nameof(properties));
        _refresh = refresh ?? throw new ArgumentNullException(nameof(refresh));
        Description = description ?? "clear properties";
    }

    public string Description { get; }

    /// <summary>True after a successful <see cref="Do"/> (the list was non-empty and the refresh succeeded).</summary>
    public bool WasApplied => _snapshot.Count > 0;

    public bool Do()
    {
        // Snapshot (index, value) for every entry before clearing so Undo restores order.
        _snapshot = _properties
            .Select((p, i) => (Index: i, Value: p))
            .ToList();

        if (_snapshot.Count == 0) return false; // already empty → don't record

        if (!PropertyListMutator.ClearAll(_properties, _refresh))
        {
            _snapshot.Clear();
            return false;
        }
        return true;
    }

    public void Undo()
    {
        if (_snapshot.Count == 0) return;
        PropertyListMutator.InsertAt(_properties, _snapshot, _refresh);
    }
}
