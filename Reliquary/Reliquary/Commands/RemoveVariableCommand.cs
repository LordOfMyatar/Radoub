using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Radoub.Formats.Gff;
using Radoub.UI.Undo;
using Radoub.UI.ViewModels;

namespace PlaceableEditor.Commands;

/// <summary>
/// Removes a local variable from both the model list and the UI collection, capturing the
/// removed entries and their index so Undo reinserts them in place. The VariablesPanel is
/// undo-agnostic (#2293) — the host turns its DeleteRequested event into this command.
/// </summary>
public sealed class RemoveVariableCommand : IUndoableCommand
{
    private readonly List<Variable> _model;
    private readonly ObservableCollection<VariableViewModel> _ui;
    private readonly VariableViewModel _vm;

    private int _index = -1;
    private Variable? _modelEntry;

    public RemoveVariableCommand(List<Variable> model, ObservableCollection<VariableViewModel> ui, VariableViewModel vm)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _ui = ui ?? throw new ArgumentNullException(nameof(ui));
        _vm = vm ?? throw new ArgumentNullException(nameof(vm));
    }

    public string Description => string.IsNullOrEmpty(_vm.Name) ? "remove variable" : $"remove variable {_vm.Name}";

    public void Do()
    {
        _index = _ui.IndexOf(_vm);
        if (_index < 0) return; // not present; nothing to do

        // Model is index-aligned with the UI collection.
        if (_index < _model.Count)
        {
            _modelEntry = _model[_index];
            _model.RemoveAt(_index);
        }
        _ui.RemoveAt(_index);
    }

    public void Undo()
    {
        if (_index < 0) return;
        _ui.Insert(_index, _vm);
        if (_modelEntry != null)
            _model.Insert(Math.Min(_index, _model.Count), _modelEntry);
    }
}
