using Avalonia.Controls;
using RadoubLauncher.ViewModels;
using RadoubLauncher.Services;
using System.ComponentModel;
using Avalonia;

namespace RadoubLauncher.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        var viewModel = new MainWindowViewModel();
        viewModel.SetParentWindow(this);
        DataContext = viewModel;

        // Restore window position from settings
        RestoreWindowState();

        // Save window state on close
        Closing += OnWindowClosing;
    }

    private void RestoreWindowState()
    {
        var settings = SettingsService.Instance;

        // Validate saved position is on a visible screen
        var savedLeft = settings.WindowLeft;
        var savedTop = settings.WindowTop;
        var savedWidth = settings.WindowWidth;
        var savedHeight = settings.WindowHeight;

        // Basic bounds check - ensure window is at least partially visible
        if (savedLeft >= 0 && savedTop >= 0 && savedWidth > 100 && savedHeight > 100)
        {
            Position = new PixelPoint((int)savedLeft, (int)savedTop);
            Width = savedWidth;
            Height = savedHeight;
        }

        if (settings.WindowMaximized)
        {
            WindowState = WindowState.Maximized;
        }
    }

    private void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        SaveWindowState();
    }

    private void SaveWindowState()
    {
        var settings = SettingsService.Instance;

        settings.WindowMaximized = WindowState == WindowState.Maximized;

        // Only save position/size when not maximized
        if (WindowState != WindowState.Maximized)
        {
            settings.WindowLeft = Position.X;
            settings.WindowTop = Position.Y;
            settings.WindowWidth = Width;
            settings.WindowHeight = Height;
        }
    }
}
