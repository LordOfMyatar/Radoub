using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Radoub.Formats.Logging;
using Radoub.Formats.Settings;
using Radoub.UI.Utils;
using Radoub.UI.Views;
using RadoubLauncher.Services;
using RadoubLauncher.Views;

namespace RadoubLauncher.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly ToolLauncherService _toolLauncher;
    private readonly GameLauncherService _gameLauncher;
    private Window? _parentWindow;

    [ObservableProperty]
    private ObservableCollection<ToolInfo> _tools;

    [ObservableProperty]
    private ObservableCollection<string> _recentModules;

    [ObservableProperty]
    private string _currentModuleName = "(No module selected)";

    [ObservableProperty]
    private bool _isModuleValid = true;

    private static readonly IBrush WarningBrush = new SolidColorBrush(Color.Parse("#FFCC00"));
    private static readonly IBrush NormalBrush = new SolidColorBrush(Colors.White);

    [ObservableProperty]
    private string _gameStatusText = "Game: Not configured";

    [ObservableProperty]
    private string _tlkStatusText = "TLK: Default";

    [ObservableProperty]
    private string _versionText = "v1.0.0";

    [ObservableProperty]
    private bool _updateAvailable;

    [ObservableProperty]
    private string _updateStatusText = "";

    [ObservableProperty]
    private bool _isCheckingForUpdates;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanTestModule))]
    [NotifyPropertyChangedFor(nameof(CanLoadModule))]
    private bool _isGameAvailable;

    public bool HasRecentModules => RecentModules.Count > 0;

    public bool CanEditModule => IsModuleValid && !string.IsNullOrEmpty(RadoubSettings.Instance.CurrentModulePath);
    public bool CanTestModule => IsGameAvailable && !string.IsNullOrEmpty(RadoubSettings.Instance.CurrentModulePath);
    public bool CanLoadModule => CanTestModule;

    public MainWindowViewModel()
    {
        _toolLauncher = ToolLauncherService.Instance;
        _gameLauncher = GameLauncherService.Instance;
        _tools = new ObservableCollection<ToolInfo>(_toolLauncher.Tools);
        _recentModules = new ObservableCollection<string>(SettingsService.Instance.RecentModules);

        LoadVersionInfo();
        UpdateStatusFromSettings();
        UpdateGameAvailability();
    }

    public void SetParentWindow(Window window)
    {
        _parentWindow = window;

        // Subscribe to settings changes
        RadoubSettings.Instance.PropertyChanged += OnSharedSettingsChanged;
        SettingsService.Instance.PropertyChanged += OnLocalSettingsChanged;

        // Check for updates on startup (fire and forget)
        _ = CheckForUpdatesAsync();
    }

    private void LoadVersionInfo()
    {
        try
        {
            VersionText = $"v{VersionHelper.GetVersion()}";
        }
        catch
        {
            VersionText = "v0.1.0";
        }
    }

    private void UpdateStatusFromSettings()
    {
        var shared = RadoubSettings.Instance;

        // Module status with validation
        if (!string.IsNullOrEmpty(shared.CurrentModulePath))
        {
            var modulePath = shared.CurrentModulePath;
            var moduleName = Path.GetFileNameWithoutExtension(modulePath);

            // Check if it's a .mod file that exists
            if (modulePath.EndsWith(".mod", StringComparison.OrdinalIgnoreCase) && File.Exists(modulePath))
            {
                CurrentModuleName = moduleName;
                IsModuleValid = true;
            }
            // Legacy: check if it's a working directory with module.ifo
            else if (Directory.Exists(modulePath) && File.Exists(Path.Combine(modulePath, "module.ifo")))
            {
                CurrentModuleName = Path.GetFileName(modulePath);
                IsModuleValid = true;
            }
            else
            {
                CurrentModuleName = $"{Path.GetFileName(modulePath)} (Invalid)";
                IsModuleValid = false;
                UnifiedLogger.LogApplication(LogLevel.WARN, $"Module validation failed: file not found or invalid format");
            }
        }
        else
        {
            CurrentModuleName = "(No module selected)";
            IsModuleValid = true;
        }

        // Game status
        if (!string.IsNullOrEmpty(shared.BaseGameInstallPath))
        {
            var gamePath = shared.BaseGameInstallPath;
            // Detect game type from path
            if (gamePath.Contains("Steam", StringComparison.OrdinalIgnoreCase))
            {
                GameStatusText = "Game: NWN:EE (Steam)";
            }
            else if (gamePath.Contains("GOG", StringComparison.OrdinalIgnoreCase))
            {
                GameStatusText = "Game: NWN:EE (GOG)";
            }
            else
            {
                GameStatusText = "Game: Configured";
            }
        }
        else
        {
            GameStatusText = "Game: Not configured";
        }

        // TLK status
        var lang = shared.EffectiveLanguage;
        var gender = shared.TlkUseFemale ? " (F)" : "";
        TlkStatusText = $"TLK: {lang}{gender}";
    }

    private void OnSharedSettingsChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        UpdateStatusFromSettings();
        UpdateGameAvailability();

        // Update module-dependent properties when module changes
        if (e.PropertyName == nameof(RadoubSettings.CurrentModulePath))
        {
            OnPropertyChanged(nameof(CanEditModule));
            OnPropertyChanged(nameof(CanTestModule));
            OnPropertyChanged(nameof(CanLoadModule));
        }
    }

    private void UpdateGameAvailability()
    {
        _gameLauncher.RefreshDiscovery();
        IsGameAvailable = _gameLauncher.IsGameAvailable;
    }

    private void OnLocalSettingsChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsService.RecentModules))
        {
            RecentModules = new ObservableCollection<string>(SettingsService.Instance.RecentModules);
            OnPropertyChanged(nameof(HasRecentModules));
        }
    }

    [RelayCommand]
    private void LaunchTool(ToolInfo tool)
    {
        if (tool == null) return;

        UnifiedLogger.LogApplication(LogLevel.INFO, $"Launching tool: {tool.Name}");

        // Pass current module path if set
        var modulePath = RadoubSettings.Instance.CurrentModulePath;
        string? args = null;

        if (!string.IsNullOrEmpty(modulePath))
        {
            // Some tools may support --module argument
            // For now, just launch without arguments - tools read RadoubSettings
        }

        _toolLauncher.LaunchTool(tool, args);
    }

    [RelayCommand]
    private void LaunchToolWithFile(ToolFileLaunchInfo launchInfo)
    {
        if (launchInfo?.Tool == null) return;

        UnifiedLogger.LogApplication(LogLevel.INFO,
            $"Launching {launchInfo.Tool.Name} with file: {Path.GetFileName(launchInfo.FilePath)}");

        _toolLauncher.LaunchToolWithFile(launchInfo.Tool, launchInfo.FilePath);
    }

    /// <summary>
    /// Get recent files for a tool (for UI binding).
    /// </summary>
    public List<RecentFileInfo> GetToolRecentFiles(string toolName)
    {
        return ToolRecentFilesService.Instance.GetRecentFiles(toolName);
    }

    /// <summary>
    /// Check if a tool has recent files.
    /// </summary>
    public bool ToolHasRecentFiles(string toolName)
    {
        return ToolRecentFilesService.Instance.HasRecentFiles(toolName);
    }

    [RelayCommand]
    private async Task OpenModule()
    {
        if (_parentWindow == null) return;

        UnifiedLogger.LogApplication(LogLevel.INFO, "Open module dialog requested");

        // Use the custom module browser
        var nwnPath = RadoubSettings.Instance.NeverwinterNightsPath;
        var browser = new ModuleBrowserWindow(nwnPath);
        var result = await browser.ShowDialog<string?>(_parentWindow);

        if (!string.IsNullOrEmpty(result))
        {
            RadoubSettings.Instance.CurrentModulePath = result;
            SettingsService.Instance.AddRecentModule(result);
            UnifiedLogger.LogApplication(LogLevel.INFO, $"Opened module: {UnifiedLogger.SanitizePath(result)}");
        }
    }

    [RelayCommand]
    private void OpenRecentModule(string modulePath)
    {
        if (string.IsNullOrEmpty(modulePath)) return;

        if (!File.Exists(modulePath) && !Directory.Exists(modulePath))
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, $"Module not found: {UnifiedLogger.SanitizePath(modulePath)}");
            SettingsService.Instance.RemoveRecentModule(modulePath);
            return;
        }

        RadoubSettings.Instance.CurrentModulePath = modulePath;
        SettingsService.Instance.AddRecentModule(modulePath);
        UnifiedLogger.LogApplication(LogLevel.INFO, $"Opened recent module: {UnifiedLogger.SanitizePath(modulePath)}");
    }

    [RelayCommand]
    private void OpenSettings()
    {
        if (_parentWindow == null) return;

        UnifiedLogger.LogApplication(LogLevel.INFO, "Opening settings window");
        var settingsWindow = new SettingsWindow();
        settingsWindow.Show(_parentWindow);  // Non-modal settings window
    }

    [RelayCommand]
    private void OpenModuleEditor()
    {
        if (_parentWindow == null) return;

        UnifiedLogger.LogApplication(LogLevel.INFO, "Opening module editor");
        var editorWindow = new ModuleEditorWindow();
        editorWindow.Show(_parentWindow);  // Non-modal editor window
    }

    [RelayCommand]
    private void OpenAbout()
    {
        if (_parentWindow == null) return;

        UnifiedLogger.LogApplication(LogLevel.INFO, "Opening about window");

        var aboutWindow = Radoub.UI.Views.AboutWindow.Create(new AboutWindowConfig
        {
            ToolName = "Trebuchet",
            Subtitle = "Radoub Launcher for Neverwinter Nights",
            Version = VersionHelper.GetVersion(),
            AdditionalInfo = "Radoub Toolset includes:\nParley - Dialog Editor\nManifest - Journal Editor\nQuartermaster - Creature/Item Editor\nFence - Merchant Editor"
        });
        aboutWindow.Show(_parentWindow);  // Non-modal about window
    }

    [RelayCommand]
    private void RefreshTools()
    {
        _toolLauncher.RefreshTools();
        Tools = new ObservableCollection<ToolInfo>(_toolLauncher.Tools);
    }

    [RelayCommand]
    private async Task CheckForUpdatesAsync()
    {
        IsCheckingForUpdates = true;
        UpdateStatusText = "Checking for updates...";

        try
        {
            var hasUpdate = await UpdateService.Instance.CheckForUpdatesAsync();
            UpdateAvailable = hasUpdate;

            if (hasUpdate)
            {
                UpdateStatusText = $"Update available: v{UpdateService.Instance.LatestVersion}";
            }
            else
            {
                UpdateStatusText = "Up to date";
            }
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, $"Update check failed: {ex.Message}");
            UpdateStatusText = "Update check failed";
        }
        finally
        {
            IsCheckingForUpdates = false;
        }
    }

    [RelayCommand]
    private void OpenReleasePage()
    {
        UpdateService.Instance.OpenReleasePage();
    }

    [RelayCommand]
    private void LaunchGame()
    {
        UnifiedLogger.LogApplication(LogLevel.INFO, "Launching NWN:EE");
        _gameLauncher.LaunchGame();
    }

    [RelayCommand]
    private void LaunchTestModule()
    {
        var moduleName = GameLauncherService.GetModuleNameFromPath(RadoubSettings.Instance.CurrentModulePath);
        if (string.IsNullOrEmpty(moduleName))
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, "Cannot launch test module: no module selected");
            return;
        }

        UnifiedLogger.LogApplication(LogLevel.INFO, $"Launching NWN:EE with +TestNewModule \"{moduleName}\"");
        _gameLauncher.LaunchWithModule(moduleName, testMode: true);
    }

    [RelayCommand]
    private void LaunchLoadModule()
    {
        var moduleName = GameLauncherService.GetModuleNameFromPath(RadoubSettings.Instance.CurrentModulePath);
        if (string.IsNullOrEmpty(moduleName))
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, "Cannot launch load module: no module selected");
            return;
        }

        UnifiedLogger.LogApplication(LogLevel.INFO, $"Launching NWN:EE with +LoadNewModule \"{moduleName}\"");
        _gameLauncher.LaunchWithModule(moduleName, testMode: false);
    }
}
