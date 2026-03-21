using Avalonia.Controls;
using Avalonia.Interactivity;
using MerchantEditor.ViewModels;
using Radoub.Formats.Gff;
using Radoub.Formats.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace MerchantEditor.Views;

/// <summary>
/// MainWindow partial: Local variable operations (add/edit/remove variables)
/// </summary>
public partial class MainWindow
{
    public ObservableCollection<VariableViewModel> Variables { get; } = new();

    #region Variable Loading

    /// <summary>
    /// Populate the Variables collection from the current store's VarTable.
    /// </summary>
    private void PopulateVariables()
    {
        // Unbind grid first to avoid UI updates during population
        VariablesGrid.ItemsSource = null;

        // Unsubscribe from existing items
        foreach (var vm in Variables)
        {
            vm.PropertyChanged -= OnVariablePropertyChanged;
        }

        Variables.Clear();

        if (_currentStore == null) return;

        foreach (var variable in _currentStore.VarTable)
        {
            var vm = VariableViewModel.FromVariable(variable);
            vm.PropertyChanged += OnVariablePropertyChanged;
            Variables.Add(vm);
        }

        ValidateVariablesRealTime();

        // Rebind grid after population complete
        VariablesGrid.ItemsSource = Variables;
        UnifiedLogger.LogApplication(LogLevel.INFO, $"Loaded {Variables.Count} local variables");
    }

    private void OnVariablePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // Mark dirty when any variable property changes
        _documentState.MarkDirty();

        // Re-validate on name changes
        if (e.PropertyName == nameof(VariableViewModel.Name))
            ValidateVariablesRealTime();
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

    /// <summary>
    /// Real-time validation: mark variables with errors for visual feedback.
    /// Called on every name change.
    /// </summary>
    private void ValidateVariablesRealTime()
    {
        // Count occurrences of each name (case-insensitive)
        var nameCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var vm in Variables)
        {
            if (string.IsNullOrWhiteSpace(vm.Name)) continue;
            nameCounts.TryGetValue(vm.Name, out var count);
            nameCounts[vm.Name] = count + 1;
        }

        foreach (var vm in Variables)
        {
            if (string.IsNullOrWhiteSpace(vm.Name))
            {
                vm.HasError = true;
                vm.ErrorMessage = "Variable name is required";
            }
            else if (nameCounts.TryGetValue(vm.Name, out var count) && count > 1)
            {
                vm.HasError = true;
                vm.ErrorMessage = $"Duplicate name: \"{vm.Name}\"";
            }
            else
            {
                vm.HasError = false;
                vm.ErrorMessage = string.Empty;
            }
        }

        // Update validation summary
        var errors = new List<string>();
        var emptyCount = Variables.Count(v => string.IsNullOrWhiteSpace(v.Name));
        if (emptyCount > 0)
            errors.Add($"{emptyCount} variable(s) missing name");

        var dupNames = Variables
            .Where(v => v.HasError && !string.IsNullOrWhiteSpace(v.Name))
            .Select(v => v.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase);
        foreach (var dup in dupNames)
            errors.Add($"Duplicate: \"{dup}\"");

        if (errors.Count > 0)
        {
            VariableValidationText.Text = string.Join(" | ", errors);
            VariableValidationText.IsVisible = true;
        }
        else
        {
            VariableValidationText.IsVisible = false;
        }
    }

    #endregion

    #region Variable Operations

    private void OnAddVariable(object? sender, RoutedEventArgs e)
    {
        if (_currentStore == null)
        {
            ShowError("No store is currently open.");
            return;
        }

        // Create new variable with empty name - user will fill it in
        var newVar = new VariableViewModel
        {
            Name = string.Empty,
            Type = VariableType.Int,
            IntValue = 0
        };

        newVar.PropertyChanged += OnVariablePropertyChanged;
        Variables.Add(newVar);
        VariablesGrid.SelectedItem = newVar;

        // Begin edit on the Name column so user can type immediately
        // Use Dispatcher to ensure grid has processed the new item
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            VariablesGrid.ScrollIntoView(newVar, VariablesGrid.Columns[0]);
            VariablesGrid.BeginEdit();
        }, Avalonia.Threading.DispatcherPriority.Background);

        _documentState.MarkDirty();
        ValidateVariablesRealTime();

        UnifiedLogger.LogApplication(LogLevel.INFO, "Added new variable (awaiting name)");
    }

    private void OnRemoveVariable(object? sender, RoutedEventArgs e)
    {
        var selectedItems = VariablesGrid.SelectedItems?.Cast<VariableViewModel>().ToList();
        if (selectedItems == null || selectedItems.Count == 0)
            return;

        foreach (var item in selectedItems)
        {
            item.PropertyChanged -= OnVariablePropertyChanged;
            Variables.Remove(item);
        }

        _documentState.MarkDirty();
        ValidateVariablesRealTime();

        UnifiedLogger.LogApplication(LogLevel.INFO, $"Removed {selectedItems.Count} variable(s)");
    }

    private void OnVariableTypeChanged(object? sender, SelectionChangedEventArgs e)
    {
        // Type change handled by ViewModel property change notification
        _documentState.MarkDirty();
    }

    #endregion
}
