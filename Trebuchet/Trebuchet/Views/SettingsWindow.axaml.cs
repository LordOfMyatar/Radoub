using Avalonia.Controls;
using RadoubLauncher.ViewModels;

namespace RadoubLauncher.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow() : this(SettingsSetupMode.Normal) { }

    public SettingsWindow(SettingsSetupMode setupMode)
    {
        InitializeComponent();
        DataContext = new SettingsWindowViewModel(this, setupMode);
    }
}
