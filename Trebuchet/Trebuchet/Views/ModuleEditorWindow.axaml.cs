using Avalonia.Controls;
using Avalonia.Threading;
using RadoubLauncher.ViewModels;

namespace RadoubLauncher.Views;

public partial class ModuleEditorWindow : Window
{
    private readonly ModuleEditorViewModel _viewModel;

    public ModuleEditorWindow()
    {
        InitializeComponent();
        _viewModel = new ModuleEditorViewModel();
        DataContext = _viewModel;

        Opened += OnWindowOpened;

        // Subscribe to variable added event to auto-focus the name field
        _viewModel.VariableAdded += OnVariableAdded;
    }

    private async void OnWindowOpened(object? sender, System.EventArgs e)
    {
        Opened -= OnWindowOpened;
        _viewModel.SetParentWindow(this);
        await _viewModel.LoadCurrentModuleAsync();
    }

    private void OnVariableAdded(object? sender, System.EventArgs e)
    {
        // Delay to let the DataGrid update its rows first
        Dispatcher.UIThread.Post(() =>
        {
            // Begin editing the Name column (column index 0)
            VariablesDataGrid.BeginEdit();
        }, DispatcherPriority.Background);
    }
}
