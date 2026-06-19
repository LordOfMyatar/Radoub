using MerchantEditor.Commands;
using MerchantEditor.ViewModels;
using Radoub.Formats.Logging;
using Radoub.UI.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using VariableViewModel = Radoub.UI.ViewModels.VariableViewModel;

namespace MerchantEditor.Views;

/// <summary>
/// MainWindow partial: Local variable operations, hosted by the shared
/// Radoub.UI.Controls.VariablesPanel (#2293). The panel raises Add/Delete events;
/// this host owns the collection mutation and dirty marking.
/// </summary>
public partial class MainWindow
{
    public ObservableCollection<VariableViewModel> Variables { get; } = new();

    private bool _variablesPanelWired;

    #region Variable Loading

    private void WireUpVariables()
    {
        if (_variablesPanelWired) return;
        _variablesPanelWired = true;

        VariablesPanelControl.Variables = Variables;
        VariablesPanelControl.AddRequested += OnVariableAddRequested;
        VariablesPanelControl.DeleteRequested += OnVariableDeleteRequested;
        // Panel self-validates and raises VariablesChanged only on real user edits.
        VariablesPanelControl.VariablesChanged += (_, _) => _documentState.MarkDirty();
    }

    /// <summary>
    /// Populate the Variables collection from the current store's VarTable.
    /// </summary>
    private void PopulateVariables()
    {
        WireUpVariables();

        Variables.Clear();
        VariablesPanelControl.CanAdd = _currentStore != null; // gate Add until a store is loaded

        if (_currentStore == null) return;

        foreach (var variable in _currentStore.VarTable)
            Variables.Add(VariableViewModel.FromVariable(variable));

        VariablesPanelControl.RevalidateNames();
        UnifiedLogger.LogApplication(LogLevel.INFO, $"Loaded {Variables.Count} local variables");
    }

    /// <summary>
    /// Update the current store's VarTable from the Variables collection.
    /// Empty-name variables are stripped on save.
    /// </summary>
    private void UpdateVarTable()
    {
        if (_currentStore == null) return;

        _currentStore.VarTable.Clear();
        foreach (var vm in Variables)
        {
            if (!string.IsNullOrWhiteSpace(vm.Name))
                _currentStore.VarTable.Add(vm.ToVariable());
        }
    }

    #endregion

    #region Variable Validation

    /// <summary>
    /// Validate variables before save. Returns error message or null if valid.
    /// Duplicate names block save.
    /// </summary>
    public string? ValidateVariablesForSave()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var vm in Variables)
        {
            if (string.IsNullOrWhiteSpace(vm.Name)) continue;
            if (!names.Add(vm.Name))
                return $"Cannot save: duplicate variable name \"{vm.Name}\". Each variable must have a unique name.";
        }
        return null;
    }

    #endregion

    #region Variable Operations

    private void OnVariableAddRequested(object? sender, VariableAddRequestedEventArgs e)
    {
        if (_currentStore == null)
        {
            ShowError("No store is currently open.");
            return;
        }

        // Seed a unique default name so the user lands on a valid, ready-to-overtype field
        var newVar = new VariableViewModel
        {
            Name = VariableViewModel.NextDefaultName(Variables.Select(v => v.Name)),
            Type = Radoub.Formats.Gff.VariableType.Int
        };
        // Route through undo so the add is reversible as one step (#2255).
        _undo.Execute(new AddVariableCommand(Variables, newVar)); // panel auto-validates via CollectionChanged
        VariablesPanelControl.SelectedVariable = newVar;
        VariablesPanelControl.FocusSelectedName(); // land in the name field

        UnifiedLogger.LogApplication(LogLevel.INFO, "Added new variable (awaiting name)");
    }

    private void OnVariableDeleteRequested(object? sender, VariableDeleteRequestedEventArgs e)
    {
        if (e.Variables.Count == 0) return;

        // Route through undo so the removal is reversible as one step (#2255).
        _undo.Execute(new RemoveVariablesCommand(Variables, e.Variables));

        UnifiedLogger.LogApplication(LogLevel.INFO, $"Removed {e.Variables.Count} variable(s)");
    }

    #endregion
}
