using System;
using System.Collections.Generic;
using System.Linq;
using ItemEditor.Services;
using Radoub.Formats.Uti;

namespace ItemEditor.Commands;

/// <summary>
/// Removes one or more item properties, capturing each removed entry and its index so Undo
/// re-inserts them in place. Wraps <see cref="PropertyListMutator"/> on both Do (RemoveAt) and
/// Undo (InsertAt) so the rollback-on-refresh-failure seam (#2258) guards both directions.
/// Part of the cross-tool undo epic (#2231).
///
/// <para>Redo-index validity: indices are captured at Do() time and restored on Undo; redo
/// re-removes by those captured indices. This is only safe because the undo history is linear —
/// any new Execute clears the redo stack, so a captured index can never be invalidated by an
/// intervening edit before redo runs. A future branching-history change would break this.</para>
/// </summary>
public sealed class RemovePropertyCommand : Radoub.UI.Undo.IUndoableCommand
{
    private readonly List<ItemProperty> _properties;
    private readonly IReadOnlyList<int> _indices;
    private readonly Action _refresh;
    private List<(int Index, ItemProperty Value)> _removed = new();

    public RemovePropertyCommand(List<ItemProperty> properties, IReadOnlyList<int> indices, Action refresh, string description)
    {
        _properties = properties ?? throw new ArgumentNullException(nameof(properties));
        _indices = indices ?? throw new ArgumentNullException(nameof(indices));
        _refresh = refresh ?? throw new ArgumentNullException(nameof(refresh));
        Description = description ?? "remove property";
    }

    public string Description { get; }

    public bool Do()
    {
        // Capture (index, value) for the valid targets before removal so Undo can restore them.
        _removed = _indices
            .Where(i => i >= 0 && i < _properties.Count)
            .Distinct()
            .Select(i => (Index: i, Value: _properties[i]))
            .ToList();

        if (_removed.Count == 0) return false; // nothing to remove → don't record

        if (!PropertyListMutator.RemoveAt(_properties, _indices, _refresh))
        {
            // Refresh threw and the mutator rolled back; nothing was removed.
            _removed.Clear();
            return false;
        }
        return true;
    }

    public void Undo()
    {
        if (_removed.Count == 0) return;
        PropertyListMutator.InsertAt(_properties, _removed, _refresh);
    }
}
