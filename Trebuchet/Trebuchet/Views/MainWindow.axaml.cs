using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Radoub.UI.Services;
using RadoubLauncher.ViewModels;
using RadoubLauncher.Services;
using System.ComponentModel;

namespace RadoubLauncher.Views;

public partial class MainWindow : Window
{
    private MainWindowViewModel? _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainWindowViewModel();
        _viewModel.SetParentWindow(this);
        DataContext = _viewModel;

        // Initialize the embedded module editor panel
        var moduleEditorPanel = this.FindControl<Controls.ModuleEditorPanel>("ModuleEditorPanel");
        if (moduleEditorPanel != null)
        {
            var moduleEditorVm = new ModuleEditorViewModel();
            moduleEditorPanel.Initialize(moduleEditorVm, this);
            _viewModel.SetModuleEditorViewModel(moduleEditorVm);
        }

        // Initialize the embedded faction editor panel
        var factionEditorPanel = this.FindControl<Controls.FactionEditorPanel>("FactionEditorPanel");
        if (factionEditorPanel != null)
        {
            var factionEditorVm = new FactionEditorViewModel();
            factionEditorPanel.Initialize(factionEditorVm, this);
            _viewModel.SetFactionEditorViewModel(factionEditorVm);
        }

        // Initialize the launch & test panel
        var launchTestPanel = this.FindControl<Controls.LaunchTestPanel>("LaunchTestPanel");
        launchTestPanel?.Initialize(_viewModel);

        // Restore window position from settings
        RestoreWindowState();

        // Save window state on close
        Closing += OnWindowClosing;

        // Update module name color when module changes (#1003)
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        Opened += OnWindowOpened;
    }

    private async void OnWindowOpened(object? sender, System.EventArgs e)
    {
        Opened -= OnWindowOpened;

        if (_viewModel != null)
            UpdateModuleNameColor(_viewModel);

        // Auto-load module if one is already selected
        var moduleEditorPanel = this.FindControl<Controls.ModuleEditorPanel>("ModuleEditorPanel");
        if (moduleEditorPanel != null && _viewModel?.HasModule == true)
        {
            await moduleEditorPanel.LoadModuleAsync();
        }

        // Auto-load factions if a module is already selected
        var factionEditorPanel = this.FindControl<Controls.FactionEditorPanel>("FactionEditorPanel");
        if (factionEditorPanel != null && _viewModel?.HasModule == true)
        {
            await factionEditorPanel.LoadFacFileAsync();
        }
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
        (_viewModel)?.Cleanup();
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

    #region Module Name Color (#1003)

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.CurrentModuleName) && sender is MainWindowViewModel vm)
            UpdateModuleNameColor(vm);
    }

    private void UpdateModuleNameColor(MainWindowViewModel vm)
    {
        var moduleNameText = this.FindControl<TextBlock>("ModuleNameText");
        if (moduleNameText == null) return;

        var hasModule = !string.IsNullOrEmpty(Radoub.Formats.Settings.RadoubSettings.Instance.CurrentModulePath);
        moduleNameText.Foreground = hasModule
            ? BrushManager.GetInfoBrush(this)
            : BrushManager.GetWarningBrush(this);
    }

    #endregion

    #region Title Bar Handlers

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void OnTitleBarDoubleTapped(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    #endregion
}
