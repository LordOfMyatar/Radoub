using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Radoub.Formats.Gff;
using Radoub.UI.ViewModels;

namespace Radoub.UI.Controls;

/// <summary>
/// Shared variables editor: a DataGrid (Name | Type | Value) with Add/Replace/Delete.
/// Undo-agnostic — the panel raises <see cref="AddRequested"/>, <see cref="ReplaceRequested"/>,
/// and <see cref="DeleteRequested"/> events the host turns into undoable commands. The panel
/// does not mutate <see cref="Variables"/> itself on Add/Delete; the host owns the mutation
/// so it can wrap it in an <c>IUndoableCommand</c> (#2293, Reliquary epic #2289).
/// </summary>
public partial class VariablesPanel : UserControl
{
    public static readonly StyledProperty<ObservableCollection<VariableViewModel>> VariablesProperty =
        AvaloniaProperty.Register<VariablesPanel, ObservableCollection<VariableViewModel>>(
            nameof(Variables), defaultValue: new ObservableCollection<VariableViewModel>());

    public static readonly StyledProperty<VariableViewModel?> SelectedVariableProperty =
        AvaloniaProperty.Register<VariablesPanel, VariableViewModel?>(nameof(SelectedVariable));

    /// <summary>The variables shown in the grid. Host owns the collection and its mutations.</summary>
    public ObservableCollection<VariableViewModel> Variables
    {
        get => GetValue(VariablesProperty);
        set => SetValue(VariablesProperty, value);
    }

    /// <summary>The currently selected variable (two-way bound to the grid).</summary>
    public VariableViewModel? SelectedVariable
    {
        get => GetValue(SelectedVariableProperty);
        set => SetValue(SelectedVariableProperty, value);
    }

    /// <summary>Raised when the user clicks Add. Host should append a variable and (optionally) select it.</summary>
    public event EventHandler<VariableAddRequestedEventArgs>? AddRequested;

    /// <summary>Raised when the user clicks Replace with a selected row. Host re-commits the edited value.</summary>
    public event EventHandler<VariableReplaceRequestedEventArgs>? ReplaceRequested;

    /// <summary>Raised when the user clicks Delete with a selection. Host removes the variable(s).</summary>
    public event EventHandler<VariableDeleteRequestedEventArgs>? DeleteRequested;

    /// <summary>Raised after name re-validation so the host can refresh any save-blocking state.</summary>
    public event EventHandler? ValidationChanged;

    public VariablesPanel()
    {
        InitializeComponent();
    }

    // --- Public API (callable from tests and host code) ---

    /// <summary>Request an Add. Raises <see cref="AddRequested"/>; the host performs the mutation.</summary>
    public void RequestAdd()
    {
        var args = new VariableAddRequestedEventArgs();
        AddRequested?.Invoke(this, args);
    }

    /// <summary>Request a Replace of the selected variable. No-op without a selection.</summary>
    public void RequestReplace()
    {
        if (SelectedVariable is null) return;
        ReplaceRequested?.Invoke(this, new VariableReplaceRequestedEventArgs(SelectedVariable));
    }

    /// <summary>Request deletion of the selected variable(s). No-op without a selection.</summary>
    public void RequestDelete()
    {
        var selected = SelectedVariables().ToList();
        if (selected.Count == 0) return;
        DeleteRequested?.Invoke(this, new VariableDeleteRequestedEventArgs(selected));
    }

    /// <summary>
    /// Re-run name validation across the whole collection: empty, invalid characters/length,
    /// and case-insensitive duplicates all set <see cref="VariableViewModel.HasError"/> +
    /// <see cref="VariableViewModel.ErrorMessage"/>. Updates the validation summary.
    /// </summary>
    public void RevalidateNames()
    {
        var vars = Variables;
        if (vars is null) return;

        var nameCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var vm in vars)
        {
            if (string.IsNullOrWhiteSpace(vm.Name)) continue;
            nameCounts.TryGetValue(vm.Name, out var c);
            nameCounts[vm.Name] = c + 1;
        }

        var errors = new List<string>();
        foreach (var vm in vars)
        {
            if (string.IsNullOrWhiteSpace(vm.Name))
            {
                Flag(vm, "Variable name is required");
            }
            else if (!VariableViewModel.IsValidName(vm.Name))
            {
                Flag(vm, $"Invalid name \"{vm.Name}\" — use ≤{VariableViewModel.MaxNameLength} chars of A-Z, 0-9, _");
            }
            else if (nameCounts.TryGetValue(vm.Name, out var c) && c > 1)
            {
                Flag(vm, $"Duplicate name: \"{vm.Name}\"");
            }
            else
            {
                vm.HasError = false;
                vm.ErrorMessage = string.Empty;
            }
        }

        var emptyCount = vars.Count(v => string.IsNullOrWhiteSpace(v.Name));
        if (emptyCount > 0) errors.Add($"{emptyCount} variable(s) missing name");
        foreach (var dup in vars.Where(v => v.HasError && !string.IsNullOrWhiteSpace(v.Name))
                                .Select(v => v.Name).Distinct(StringComparer.OrdinalIgnoreCase))
            errors.Add($"Issue: \"{dup}\"");

        if (ValidationText is not null)
        {
            ValidationText.Text = string.Join(" | ", errors);
            ValidationText.IsVisible = errors.Count > 0;
        }

        ValidationChanged?.Invoke(this, EventArgs.Empty);

        static void Flag(VariableViewModel vm, string msg)
        {
            vm.HasError = true;
            vm.ErrorMessage = msg;
        }
    }

    /// <summary>True if any variable currently has a validation error.</summary>
    public bool HasValidationErrors => Variables?.Any(v => v.HasError) ?? false;

    private IEnumerable<VariableViewModel> SelectedVariables()
    {
        if (VariablesGrid?.SelectedItems is { Count: > 0 } items)
            return items.Cast<VariableViewModel>();
        return SelectedVariable is not null
            ? new[] { SelectedVariable }
            : Enumerable.Empty<VariableViewModel>();
    }

    // --- Event handlers ---

    private void OnAddClick(object? sender, RoutedEventArgs e) => RequestAdd();

    private void OnReplaceClick(object? sender, RoutedEventArgs e) => RequestReplace();

    private void OnDeleteClick(object? sender, RoutedEventArgs e) => RequestDelete();

    private void OnTypeSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        // Type binding flows through the VM; re-validate (value editor visibility is VM-driven).
        Dispatcher.UIThread.Post(RevalidateNames, DispatcherPriority.Background);
    }
}

/// <summary>Event args for an Add request. The host creates and appends the variable.</summary>
public sealed class VariableAddRequestedEventArgs : EventArgs
{
}

/// <summary>Event args carrying the variable the host should re-commit (Replace).</summary>
public sealed class VariableReplaceRequestedEventArgs : EventArgs
{
    public VariableReplaceRequestedEventArgs(VariableViewModel variable) => Variable = variable;

    public VariableViewModel Variable { get; }
}

/// <summary>Event args carrying the variable(s) the host should remove (Delete).</summary>
public sealed class VariableDeleteRequestedEventArgs : EventArgs
{
    public VariableDeleteRequestedEventArgs(IReadOnlyList<VariableViewModel> variables) => Variables = variables;

    public IReadOnlyList<VariableViewModel> Variables { get; }

    /// <summary>Convenience accessor for the first selected variable.</summary>
    public VariableViewModel Variable => Variables[0];
}
