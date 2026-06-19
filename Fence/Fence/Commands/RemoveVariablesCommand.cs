using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Radoub.UI.Undo;
using Radoub.UI.ViewModels;

namespace MerchantEditor.Commands;

/// <summary>
/// Removes one or more selected local variables from the UI collection as a single undo step,
/// capturing each entry's original index so Undo reinserts them in place. The shared
/// <see cref="Radoub.UI.Controls.VariablesPanel"/> is undo-agnostic (#2293); the host turns its
/// DeleteRequested event into this command. Removing nothing records nothing (Do returns false).
/// </summary>
public sealed class RemoveVariablesCommand : IUndoableCommand
{
    private readonly ObservableCollection<VariableViewModel> _ui;
    private readonly List<VariableViewModel> _targets;
    private List<(int Index, VariableViewModel Item)> _removed = new();

    public RemoveVariablesCommand(ObservableCollection<VariableViewModel> ui,
        IEnumerable<VariableViewModel> toRemove)
    {
        _ui = ui ?? throw new ArgumentNullException(nameof(ui));
        _targets = (toRemove ?? throw new ArgumentNullException(nameof(toRemove))).ToList();
    }

    public string Description => _targets.Count == 1
        ? (string.IsNullOrEmpty(_targets[0].Name) ? "remove variable" : $"remove variable {_targets[0].Name}")
        : $"remove {_targets.Count} variables";

    public bool Do()
    {
        _removed = _targets
            .Select(t => (Index: _ui.IndexOf(t), Item: t))
            .Where(e => e.Index >= 0)
            .OrderByDescending(e => e.Index)
            .ToList();

        if (_removed.Count == 0) return false; // nothing present → don't record

        foreach (var (index, _) in _removed)
            _ui.RemoveAt(index);
        return true;
    }

    public void Undo()
    {
        foreach (var (index, item) in _removed.OrderBy(e => e.Index))
            _ui.Insert(Math.Min(index, _ui.Count), item);
    }
}
