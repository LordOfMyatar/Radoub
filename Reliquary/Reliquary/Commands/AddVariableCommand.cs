using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Radoub.Formats.Gff;
using Radoub.UI.Undo;
using Radoub.UI.ViewModels;

namespace PlaceableEditor.Commands;

/// <summary>
/// Adds a local variable to both the model <see cref="Variable"/> list and the UI
/// <see cref="VariableViewModel"/> collection, keeping the two index-aligned. Reversible:
/// Undo removes the same entry from both. The VariablesPanel is undo-agnostic (#2293) — the
/// host turns its AddRequested event into this command.
/// </summary>
public sealed class AddVariableCommand : IUndoableCommand
{
    private readonly List<Variable> _model;
    private readonly ObservableCollection<VariableViewModel> _ui;
    private readonly VariableViewModel _vm;
    private Variable? _modelEntry;

    public AddVariableCommand(List<Variable> model, ObservableCollection<VariableViewModel> ui, VariableViewModel vm)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _ui = ui ?? throw new ArgumentNullException(nameof(ui));
        _vm = vm ?? throw new ArgumentNullException(nameof(vm));
    }

    public string Description => string.IsNullOrEmpty(_vm.Name) ? "add variable" : $"add variable {_vm.Name}";

    public bool Do()
    {
        _modelEntry = _vm.ToVariable();
        _model.Add(_modelEntry);
        _ui.Add(_vm);
        return true;
    }

    public void Undo()
    {
        _ui.Remove(_vm);
        if (_modelEntry != null) _model.Remove(_modelEntry);
    }
}
