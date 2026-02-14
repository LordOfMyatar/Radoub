using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using RadoubLauncher.ViewModels;

namespace RadoubLauncher.Controls;

public partial class ModuleEditorPanel : UserControl
{
    private ModuleEditorViewModel? _viewModel;

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

        // Subscribe to variable added event to auto-focus the name field
        viewModel.VariableAdded += OnVariableAdded;
    }

    /// <summary>
    /// Load the current module's IFO data into the panel.
    /// </summary>
    public async Task LoadModuleAsync()
    {
        if (_viewModel == null) return;
        await _viewModel.LoadCurrentModuleAsync();
    }

    private void OnVariableAdded(object? sender, System.EventArgs e)
    {
        // Delay to let the DataGrid update its rows first
        Dispatcher.UIThread.Post(() =>
        {
            VariablesDataGrid.BeginEdit();
        }, DispatcherPriority.Background);
    }
}
