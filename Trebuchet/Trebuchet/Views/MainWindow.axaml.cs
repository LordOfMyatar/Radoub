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

        // Keyboard navigation (Ctrl+1/2/3 tab switching)
        KeyDown += OnKeyDown;

        // Tab change updates title and status bar context
        var workspaceTabs = this.FindControl<TabControl>("WorkspaceTabs");
        if (workspaceTabs != null)
        {
            workspaceTabs.SelectionChanged += OnWorkspaceTabChanged;
        }

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

        UpdateWindowTitle();

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
        {
            UpdateModuleNameColor(vm);
            UpdateWindowTitle();
        }

        if (e.PropertyName == nameof(MainWindowViewModel.HasModule))
            UpdateWindowTitle();
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

    #region Window Title & Status Context

    private void OnWorkspaceTabChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not TabControl tabControl) return;
        UpdateWindowTitle(tabControl);
    }

    private void UpdateWindowTitle(TabControl? tabControl = null)
    {
        tabControl ??= this.FindControl<TabControl>("WorkspaceTabs");

        var moduleName = _viewModel?.CurrentModuleName ?? "(No module)";
        var baseTitle = "Trebuchet";

        if (tabControl?.SelectedItem is TabItem selectedTab && _viewModel?.HasModule == true)
        {
            var tabName = selectedTab.Header?.ToString() ?? "";
            Title = $"{baseTitle} - {moduleName} [{tabName}]";
        }
        else if (_viewModel?.HasModule == true)
        {
            Title = $"{baseTitle} - {moduleName}";
        }
        else
        {
            Title = $"{baseTitle} - Radoub Toolset (Alpha)";
        }
    }

    #endregion

    #region Keyboard Navigation

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        // Ctrl+1/2/3 switches workspace tabs
        if (e.KeyModifiers == KeyModifiers.Control)
        {
            var tabControl = this.FindControl<TabControl>("WorkspaceTabs");
            if (tabControl == null || !tabControl.IsVisible) return;

            int? tabIndex = e.Key switch
            {
                Key.D1 => 0, // Module
                Key.D2 => 1, // Factions
                Key.D3 => 2, // Launch & Test
                _ => null
            };

            if (tabIndex.HasValue && tabIndex.Value < tabControl.ItemCount)
            {
                tabControl.SelectedIndex = tabIndex.Value;
                e.Handled = true;
            }
        }
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
