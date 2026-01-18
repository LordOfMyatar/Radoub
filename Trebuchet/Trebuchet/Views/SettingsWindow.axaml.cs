using Avalonia.Controls;
using RadoubLauncher.ViewModels;

namespace RadoubLauncher.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        DataContext = new SettingsWindowViewModel(this);
    }
}
