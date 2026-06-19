using System;
using System.Collections.ObjectModel;
using Radoub.UI.Undo;
using Radoub.UI.ViewModels;

namespace MerchantEditor.Commands;

/// <summary>
/// Adds a local variable to the UI <see cref="VariableViewModel"/> collection as one undo step.
/// Fence rebuilds the UTM <c>VarTable</c> from this collection at save time (see
/// <c>UpdateVarTable</c>), so the collection is the live source of truth — no parallel model list.
/// The shared <see cref="Radoub.UI.Controls.VariablesPanel"/> is undo-agnostic (#2293); the host
/// turns its AddRequested event into this command. Reversible: Undo removes the same entry.
/// </summary>
public sealed class AddVariableCommand : IUndoableCommand
{
    private readonly ObservableCollection<VariableViewModel> _ui;
    private readonly VariableViewModel _vm;

    public AddVariableCommand(ObservableCollection<VariableViewModel> ui, VariableViewModel vm)
    {
        _ui = ui ?? throw new ArgumentNullException(nameof(ui));
        _vm = vm ?? throw new ArgumentNullException(nameof(vm));
    }

    public string Description => string.IsNullOrEmpty(_vm.Name) ? "add variable" : $"add variable {_vm.Name}";

    public bool Do()
    {
        _ui.Add(_vm);
        return true;
    }

    public void Undo() => _ui.Remove(_vm);
}
