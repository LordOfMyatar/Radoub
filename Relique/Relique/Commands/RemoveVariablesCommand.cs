using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Radoub.UI.Undo;
using Radoub.UI.ViewModels;

namespace ItemEditor.Commands;

/// <summary>
/// Removes one or more local variables from the UI <see cref="VariableViewModel"/> collection,
/// capturing each removed item and its index so Undo reinserts them in place. Relique derives the
/// model VarTable from this collection at save time, so the command operates on the collection
/// only. Part of the cross-tool undo epic (#2231).
/// </summary>
public sealed class RemoveVariablesCommand : IUndoableCommand
{
    private readonly ObservableCollection<VariableViewModel> _variables;
    private readonly IReadOnlyList<VariableViewModel> _toRemove;
    private List<(int Index, VariableViewModel Value)> _removed = new();

    public RemoveVariablesCommand(ObservableCollection<VariableViewModel> variables, IReadOnlyList<VariableViewModel> toRemove)
    {
        _variables = variables ?? throw new ArgumentNullException(nameof(variables));
        _toRemove = toRemove ?? throw new ArgumentNullException(nameof(toRemove));
    }

    public string Description => _toRemove.Count == 1 ? "remove variable" : $"remove {_toRemove.Count} variables";

    public bool Do()
    {
        // Capture (index, value) before removal so Undo restores order. Indices are valid until
        // Undo runs because history is linear (any new Execute clears redo).
        _removed = _toRemove
            .Select(v => (Index: _variables.IndexOf(v), Value: v))
            .Where(e => e.Index >= 0)
            .OrderByDescending(e => e.Index)
            .ToList();

        if (_removed.Count == 0) return false; // nothing present → don't record

        foreach (var (index, _) in _removed)
            _variables.RemoveAt(index);
        return true;
    }

    public void Undo()
    {
        // Re-insert ascending so earlier indices fill before later ones shift into place.
        foreach (var (index, value) in _removed.OrderBy(e => e.Index))
            _variables.Insert(Math.Min(index, _variables.Count), value);
    }
}
