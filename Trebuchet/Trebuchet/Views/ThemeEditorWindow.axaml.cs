using Avalonia.Controls;
using RadoubLauncher.ViewModels;

namespace RadoubLauncher.Views;

public partial class ThemeEditorWindow : Window
{
    public ThemeEditorWindow()
    {
        InitializeComponent();
        DataContext = new ThemeEditorViewModel();
    }

    public ThemeEditorWindow(ThemeEditorViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.CloseRequested += (_, _) => Close();
    }
}
