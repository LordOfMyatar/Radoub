using Avalonia.Controls;
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
    }

    private async void OnWindowOpened(object? sender, System.EventArgs e)
    {
        Opened -= OnWindowOpened;
        _viewModel.SetParentWindow(this);
        await _viewModel.LoadCurrentModuleAsync();
    }
}
