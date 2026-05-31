using System.Threading.Tasks;
using Avalonia.Controls;
using Radoub.UI.Controls;
using RadoubLauncher.ViewModels;

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
        if (!_variablesWired)
        {
            _variablesWired = true;
            VariablesPanelControl.AddRequested += OnVariableAddRequested;
            VariablesPanelControl.DeleteRequested += OnVariableDeleteRequested;
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
        VariablesPanelControl.RevalidateNames();
    }

    private void OnVariableDeleteRequested(object? sender, VariableDeleteRequestedEventArgs e)
    {
        _viewModel?.RemoveVariables(e.Variables);
        VariablesPanelControl.RevalidateNames();
    }
}
