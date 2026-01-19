using Avalonia.Controls;
using Avalonia.Interactivity;
using MerchantEditor.ViewModels;
using Radoub.Formats.Gff;
using Radoub.Formats.Logging;
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

        // Rebind grid after population complete
        VariablesGrid.ItemsSource = Variables;
        UnifiedLogger.LogApplication(LogLevel.INFO, $"Loaded {Variables.Count} local variables");
    }

    private void OnVariablePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // Mark dirty when any variable property changes
        _isDirty = true;
        UpdateTitle();
    }

    /// <summary>
    /// Update the current store's VarTable from the Variables collection.
    /// </summary>
    private void UpdateVarTable()
    {
        if (_currentStore == null) return;

        _currentStore.VarTable.Clear();
        foreach (var vm in Variables)
        {
            _currentStore.VarTable.Add(vm.ToVariable());
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

        // Generate a unique variable name
        var baseName = "NewVar";
        var counter = 1;
        var name = baseName;

        while (Variables.Any(v => v.Name == name))
        {
            name = $"{baseName}{counter}";
            counter++;
        }

        var newVar = new VariableViewModel
        {
            Name = name,
            Type = VariableType.Int,
            IntValue = 0
        };

        newVar.PropertyChanged += OnVariablePropertyChanged;
        Variables.Add(newVar);
        VariablesGrid.SelectedItem = newVar;

        _isDirty = true;
        UpdateTitle();

        UnifiedLogger.LogApplication(LogLevel.INFO, $"Added new variable: {name}");
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

        _isDirty = true;
        UpdateTitle();

        UnifiedLogger.LogApplication(LogLevel.INFO, $"Removed {selectedItems.Count} variable(s)");
    }

    private void OnVariableTypeChanged(object? sender, SelectionChangedEventArgs e)
    {
        // Type change handled by ViewModel property change notification
        _isDirty = true;
        UpdateTitle();
    }

    #endregion
}
