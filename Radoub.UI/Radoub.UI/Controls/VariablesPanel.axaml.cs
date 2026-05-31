using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Radoub.UI.ViewModels;

namespace Radoub.UI.Controls;

/// <summary>
/// Shared variables editor: a DataGrid (Name | Type | Value) with Add/Replace/Delete.
/// Undo-agnostic — the panel raises <see cref="AddRequested"/>, <see cref="ReplaceRequested"/>,
/// and <see cref="DeleteRequested"/> events the host turns into undoable commands. The panel
/// does not mutate <see cref="Variables"/> itself on Add/Delete; the host owns the mutation
/// so it can wrap it in an <c>IUndoableCommand</c> (#2293, Reliquary epic #2289).
/// </summary>
/// <remarks>
/// The panel owns validation: it subscribes to its <see cref="Variables"/> collection and each
/// item's <see cref="INotifyPropertyChanged"/>, revalidating on every edit (name format, value
/// parse, duplicate names). Hosts no longer re-implement this — that gap previously left
/// Trebuchet with stale validation. A user edit raises <see cref="VariablesChanged"/> (the host's
/// dirty signal); assigning the collection during populate does NOT, so screen switches don't
/// falsely mark the document dirty.
/// </remarks>
public partial class VariablesPanel : UserControl
{
    public static readonly StyledProperty<ObservableCollection<VariableViewModel>> VariablesProperty =
        AvaloniaProperty.Register<VariablesPanel, ObservableCollection<VariableViewModel>>(
            nameof(Variables), defaultValue: new ObservableCollection<VariableViewModel>());

    public static readonly StyledProperty<VariableViewModel?> SelectedVariableProperty =
        AvaloniaProperty.Register<VariablesPanel, VariableViewModel?>(nameof(SelectedVariable));

    private ObservableCollection<VariableViewModel>? _subscribedCollection;

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

    /// <summary>
    /// Raised when the USER changes a variable (edits a field, or add/delete completes).
    /// This is the host's dirty signal. NOT raised when the host assigns <see cref="Variables"/>
    /// during populate, so loading a file / switching screens does not mark the document dirty.
    /// </summary>
    public event EventHandler? VariablesChanged;

    /// <summary>Raised after re-validation so a host can refresh save-blocking state if it wants.</summary>
    public event EventHandler? ValidationChanged;

    public VariablesPanel()
    {
        InitializeComponent();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == VariablesProperty)
            HookCollection(change.GetNewValue<ObservableCollection<VariableViewModel>>());
    }

    /// <summary>(Re)subscribe to the active collection + its items. Assignment is a populate, not a user edit.</summary>
    private void HookCollection(ObservableCollection<VariableViewModel>? collection)
    {
        if (ReferenceEquals(_subscribedCollection, collection)) return;

        if (_subscribedCollection != null)
        {
            _subscribedCollection.CollectionChanged -= OnVariablesCollectionChanged;
            foreach (var vm in _subscribedCollection)
                vm.PropertyChanged -= OnItemPropertyChanged;
        }

        _subscribedCollection = collection;

        if (_subscribedCollection != null)
        {
            _subscribedCollection.CollectionChanged += OnVariablesCollectionChanged;
            foreach (var vm in _subscribedCollection)
                vm.PropertyChanged += OnItemPropertyChanged;
        }

        // Validate the freshly-assigned set, but do NOT raise VariablesChanged (this is a populate).
        RevalidateNames();
    }

    private void OnVariablesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
            foreach (VariableViewModel vm in e.OldItems)
                vm.PropertyChanged -= OnItemPropertyChanged;
        if (e.NewItems != null)
            foreach (VariableViewModel vm in e.NewItems)
                vm.PropertyChanged += OnItemPropertyChanged;

        RevalidateNames();
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // HasError/ErrorMessage are set BY validation — ignore them to avoid re-entrancy.
        if (e.PropertyName == nameof(VariableViewModel.HasError) ||
            e.PropertyName == nameof(VariableViewModel.ErrorMessage))
            return;

        RevalidateNames();
        VariablesChanged?.Invoke(this, EventArgs.Empty); // user edited a field
    }

    // --- Public API (callable from tests and host code) ---

    /// <summary>Request an Add. Raises <see cref="AddRequested"/>; the host performs the mutation.</summary>
    public void RequestAdd()
    {
        AddRequested?.Invoke(this, new VariableAddRequestedEventArgs());
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
    /// Focus the Name cell of the currently selected row and begin editing it. Called after Add
    /// so the user lands directly in the new variable's name field (rather than outside it,
    /// which would surface the "name required" error before they type).
    /// </summary>
    public void FocusSelectedName()
    {
        if (VariablesGrid is null || SelectedVariable is null) return;

        Dispatcher.UIThread.Post(() =>
        {
            VariablesGrid.ScrollIntoView(SelectedVariable, VariablesGrid.Columns.Count > 0 ? VariablesGrid.Columns[0] : null);
            VariablesGrid.SelectedItem = SelectedVariable;
            VariablesGrid.CurrentColumn = VariablesGrid.Columns.Count > 0 ? VariablesGrid.Columns[0] : null;
            VariablesGrid.BeginEdit();

            // Drill into the row's first TextBox and focus it.
            Dispatcher.UIThread.Post(() =>
            {
                var tb = FindNameTextBox(SelectedVariable);
                if (tb != null) { tb.Focus(); tb.SelectAll(); }
            }, DispatcherPriority.Background);
        }, DispatcherPriority.Background);
    }

    private TextBox? FindNameTextBox(VariableViewModel target)
    {
        foreach (var d in VariablesGrid.GetVisualDescendants())
        {
            if (d is DataGridRow row && ReferenceEquals(row.DataContext, target))
            {
                foreach (var child in row.GetVisualDescendants())
                    if (child is TextBox tb) return tb;
            }
        }
        return null;
    }

    /// <summary>
    /// Re-validate the whole collection: per-variable name format + value parse, plus
    /// case-insensitive duplicate-name detection. Sets <see cref="VariableViewModel.HasError"/>/
    /// <see cref="VariableViewModel.ErrorMessage"/> and updates the validation summary.
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
            string? error;
            if (string.IsNullOrWhiteSpace(vm.Name))
                error = "Variable name is required";
            else if (!VariableViewModel.IsValidName(vm.Name))
                error = $"Invalid name \"{vm.Name}\" — use ≤{VariableViewModel.MaxNameLength} chars of A-Z, 0-9, _";
            else if (nameCounts.TryGetValue(vm.Name, out var c) && c > 1)
                error = $"Duplicate name: \"{vm.Name}\"";
            else
                error = vm.ValidateValue(); // value parse (null if value OK)

            vm.HasError = error != null;
            vm.ErrorMessage = error ?? string.Empty;
        }

        var emptyCount = vars.Count(v => string.IsNullOrWhiteSpace(v.Name));
        if (emptyCount > 0) errors.Add($"{emptyCount} variable(s) missing name");
        foreach (var msg in vars.Where(v => v.HasError && !string.IsNullOrWhiteSpace(v.Name))
                                .Select(v => v.ErrorMessage).Distinct())
            errors.Add(msg);

        if (ValidationText is not null)
        {
            ValidationText.Text = string.Join(" | ", errors);
            ValidationText.IsVisible = errors.Count > 0;
        }

        ValidationChanged?.Invoke(this, EventArgs.Empty);
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
