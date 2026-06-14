using System;
using System.Collections.ObjectModel;
using Radoub.UI.Undo;
using Radoub.UI.ViewModels;

namespace ItemEditor.Commands;

/// <summary>
/// Adds a local variable to the UI <see cref="VariableViewModel"/> collection. Relique derives the
/// model VarTable from this collection at save time (see <c>UpdateVarTable</c>), so the command
/// operates on the collection only — there is no separate model list to keep aligned (unlike
/// Reliquary, whose model + UI are index-aligned). Part of the cross-tool undo epic (#2231).
/// </summary>
public sealed class AddVariableCommand : IUndoableCommand
{
    private readonly ObservableCollection<VariableViewModel> _variables;
    private readonly VariableViewModel _vm;

    public AddVariableCommand(ObservableCollection<VariableViewModel> variables, VariableViewModel vm)
    {
        _variables = variables ?? throw new ArgumentNullException(nameof(variables));
        _vm = vm ?? throw new ArgumentNullException(nameof(vm));
    }

    public string Description => string.IsNullOrEmpty(_vm.Name) ? "add variable" : $"add variable {_vm.Name}";

    public bool Do()
    {
        _variables.Add(_vm);
        return true;
    }

    public void Undo() => _variables.Remove(_vm);
}
