using System;
using System.Collections.Generic;
using ItemEditor.Services;
using Radoub.Formats.Uti;

namespace ItemEditor.Commands;

/// <summary>
/// Adds a single item property, wrapping <see cref="PropertyListMutator"/> so the tested
/// rollback-on-refresh-failure seam (#2258) stays in the mutation path. Reversible: Undo removes
/// the same entry. Part of the cross-tool undo epic (#2231).
///
/// <para><see cref="Do"/> returns the mutator's result: <c>true</c> when the add applied and the
/// refresh succeeded, <c>false</c> when the refresh threw and the model self-rolled-back. The
/// host's <see cref="Radoub.UI.Undo.UndoRedoManager"/> skips recording a command whose Do()
/// returned false, so a rolled-back add never leaves a stale entry on the undo stack.</para>
/// </summary>
public sealed class AddPropertyCommand : Radoub.UI.Undo.IUndoableCommand
{
    private readonly List<ItemProperty> _properties;
    private readonly ItemProperty _property;
    private readonly Action _refresh;
    private int _addedIndex = -1;

    public AddPropertyCommand(List<ItemProperty> properties, ItemProperty property, Action refresh, string description)
    {
        _properties = properties ?? throw new ArgumentNullException(nameof(properties));
        _property = property ?? throw new ArgumentNullException(nameof(property));
        _refresh = refresh ?? throw new ArgumentNullException(nameof(refresh));
        Description = description ?? "add property";
    }

    public string Description { get; }

    /// <summary>True after a successful <see cref="Do"/> (the add applied and the refresh succeeded).</summary>
    public bool WasApplied => _addedIndex >= 0;

    public bool Do()
    {
        // BatchAdd appends to the end; capture the resulting index for Undo. Safe because history
        // is linear (any new Execute clears redo), so no intervening edit can shift this entry
        // before Undo runs.
        int indexIfApplied = _properties.Count;
        if (!PropertyListMutator.BatchAdd(_properties, new[] { _property }, _refresh))
            return false;
        _addedIndex = indexIfApplied;
        return true;
    }

    public void Undo()
    {
        if (_addedIndex < 0) return;
        // Remove via the mutator so the refresh runs and the rollback seam guards undo too.
        PropertyListMutator.RemoveAt(_properties, new[] { _addedIndex }, _refresh);
    }
}
