using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Radoub.Formats.Settings;
using Radoub.UI.Controls;
using RadoubLauncher.ViewModels;
using RadoubLauncher.Views;

namespace RadoubLauncher.Controls;

public partial class ModuleEditorPanel : UserControl
{
    private ModuleEditorViewModel? _viewModel;
    private bool _variablesWired;

    public ModuleEditorPanel()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Initialize the panel with its ViewModel and parent window reference.
    /// Called by MainWindow after embedding the panel.
    /// </summary>
    public void Initialize(ModuleEditorViewModel viewModel, Window parentWindow)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        viewModel.SetParentWindow(parentWindow);

        // Route the shared VariablesPanel's Add/Delete events to the ViewModel (#2293).
        // The panel self-validates on edit and raises VariablesChanged for the dirty flag.
        if (!_variablesWired)
        {
            _variablesWired = true;
            VariablesPanelControl.AddRequested += OnVariableAddRequested;
            VariablesPanelControl.DeleteRequested += OnVariableDeleteRequested;
            VariablesPanelControl.VariablesChanged += OnPanelVariablesChanged;
        }
    }

    /// <summary>
    /// Load the current module's IFO data into the panel.
    /// </summary>
    public async Task LoadModuleAsync()
    {
        if (_viewModel == null) return;
        await _viewModel.LoadCurrentModuleAsync();
        VariablesPanelControl.RevalidateNames();
    }

    private void OnVariableAddRequested(object? sender, VariableAddRequestedEventArgs e)
    {
        _viewModel?.AddVariable();
        VariablesPanelControl.FocusSelectedName(); // land in the new variable's name field
    }

    private void OnVariableDeleteRequested(object? sender, VariableDeleteRequestedEventArgs e)
    {
        _viewModel?.RemoveVariables(e.Variables);
    }

    private void OnPanelVariablesChanged(object? sender, System.EventArgs e)
    {
        if (_viewModel != null)
            _viewModel.HasUnsavedChanges = true;
    }

    /// <summary>
    /// Open the HAK conflict checker for the current (in-editor) HAK list (#1162).
    /// Uses the working-copy HAK list so unsaved reorders are reflected.
    /// </summary>
    private async void OnCheckHakConflictsClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel == null) return;

        var hakNames = _viewModel.HakList.ToList();
        var searchPaths = RadoubSettings.Instance.GetAllHakSearchPaths().ToList();

        var window = new HakConflictWindow();
        var owner = GetParentWindow();
        if (owner != null)
            window.Show(owner);
        else
            window.Show();
        await window.RunCheckAsync(hakNames, searchPaths);
    }

    private Window? GetParentWindow() => TopLevel.GetTopLevel(this) as Window;
}
