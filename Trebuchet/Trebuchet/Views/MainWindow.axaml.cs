using System;
using System.ComponentModel;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Radoub.Formats.Settings;
using Radoub.UI.Services;
using RadoubLauncher.Services;
using RadoubLauncher.ViewModels;

namespace RadoubLauncher.Views;

public partial class MainWindow : Window
{
    private MainWindowViewModel? _viewModel;
    private PaletteCacheWarmupService? _paletteCacheWarmup;
    private CancellationTokenSource? _appShutdownCts;

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

            // Wire palette cache pre-warming (#1633)
            _appShutdownCts = new CancellationTokenSource();
            var cacheService = new SharedPaletteCacheService();
            var hakScanner = new HakPaletteScannerService();
            _paletteCacheWarmup = new PaletteCacheWarmupService(cacheService, hakScanner);

            moduleEditorVm.GameDataServiceInitialized += (_, _) =>
            {
                if (moduleEditorVm.GameDataService?.IsConfigured == true)
                {
                    _ = _paletteCacheWarmup.WarmBifCacheAsync(
                        moduleEditorVm.GameDataService, _appShutdownCts.Token);
                }
            };

            moduleEditorVm.ModuleLoaded += (_, _) =>
            {
                if (moduleEditorVm.GameDataService?.IsConfigured == true &&
                    moduleEditorVm.ModuleDirectory != null)
                {
                    var hakSearchPaths = RadoubSettings.Instance.GetAllHakSearchPaths();
                    _ = _paletteCacheWarmup.WarmModuleHakCacheAsync(
                        moduleEditorVm.GameDataService,
                        moduleEditorVm.ModuleDirectory,
                        hakSearchPaths,
                        _appShutdownCts.Token);
                }
            };
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

        // Initialize the Marlinspike search & replace panel
        var marlinspikePanel = this.FindControl<Controls.MarlinspikePanel>("MarlinspikePanel");
        if (marlinspikePanel != null)
        {
            var marlinspikeVm = new MarlinspikePanelViewModel();
            marlinspikePanel.Initialize(marlinspikeVm, _viewModel, this);
        }

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

        // --settings flag: auto-open settings window
        if (Program.OpenSettingsOnStartup)
        {
            _viewModel?.OpenSettingsCommand.Execute(null);
        }
    }

    private void RestoreWindowState()
    {
        Radoub.UI.Services.WindowPositionHelper.Restore(this, SettingsService.Instance, validateBounds: true);
    }

    private void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        SaveWindowState();

        // Cancel palette cache warm-up operations (#1633)
        _paletteCacheWarmup?.CancelAll();
        _appShutdownCts?.Cancel();
        _paletteCacheWarmup?.Dispose();
        _appShutdownCts?.Dispose();

        (_viewModel)?.Cleanup();
    }

    private void SaveWindowState()
    {
        Radoub.UI.Services.WindowPositionHelper.Save(this, SettingsService.Instance);
    }

    #region Module Name Color (#1003)

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.CurrentModuleName) && sender is MainWindowViewModel vm)
        {
            UpdateModuleNameColor(vm);
            UpdateWindowTitle();

            // Clear Marlinspike results when module changes
            var marlinspikePanel = this.FindControl<Controls.MarlinspikePanel>("MarlinspikePanel");
            marlinspikePanel?.OnModuleChanged();
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
        var tabControl = this.FindControl<TabControl>("WorkspaceTabs");
        if (tabControl == null || !tabControl.IsVisible) return;

        // Ctrl+Shift+F: Switch to Marlinspike tab and focus search box
        if (e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift) && e.Key == Key.F)
        {
            tabControl.SelectedIndex = 3; // Marlinspike
            var panel = this.FindControl<Controls.MarlinspikePanel>("MarlinspikePanel");
            panel?.FocusSearchBox();
            e.Handled = true;
            return;
        }

        // Ctrl+1/2/3/4 switches workspace tabs
        if (e.KeyModifiers == KeyModifiers.Control)
        {
            int? tabIndex = e.Key switch
            {
                Key.D1 => 0, // Module
                Key.D2 => 1, // Factions
                Key.D3 => 2, // Build & Test
                Key.D4 => 3, // Marlinspike
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
