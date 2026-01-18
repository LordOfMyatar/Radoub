using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Radoub.Formats.Logging;
using Radoub.Formats.Settings;
using RadoubLauncher.Services;
using RadoubLauncher.Views;

namespace RadoubLauncher.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly ToolLauncherService _toolLauncher;
    private Window? _parentWindow;

    [ObservableProperty]
    private ObservableCollection<ToolInfo> _tools;

    [ObservableProperty]
    private ObservableCollection<string> _recentModules;

    [ObservableProperty]
    private string _currentModuleName = "(No module selected)";

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

    public bool HasRecentModules => RecentModules.Count > 0;

    public MainWindowViewModel()
    {
        _toolLauncher = ToolLauncherService.Instance;
        _tools = new ObservableCollection<ToolInfo>(_toolLauncher.Tools);
        _recentModules = new ObservableCollection<string>(SettingsService.Instance.RecentModules);

        LoadVersionInfo();
        UpdateStatusFromSettings();
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
            // Use InformationalVersion for semantic version with suffix (e.g., "0.1.0-alpha")
            var infoVersion = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

            if (!string.IsNullOrEmpty(infoVersion))
            {
                // Strip git hash if present (e.g., "0.1.0-alpha+abc123" -> "0.1.0-alpha")
                var plusIndex = infoVersion.IndexOf('+');
                if (plusIndex > 0)
                    infoVersion = infoVersion[..plusIndex];

                VersionText = $"v{infoVersion}";
            }
            else
            {
                // Fallback to assembly version
                var version = Assembly.GetExecutingAssembly().GetName().Version;
                if (version != null)
                {
                    VersionText = $"v{version.Major}.{version.Minor}.{version.Build}";
                }
            }
        }
        catch
        {
            VersionText = "v0.1.0";
        }
    }

    private void UpdateStatusFromSettings()
    {
        var shared = RadoubSettings.Instance;

        // Module status
        if (!string.IsNullOrEmpty(shared.CurrentModulePath))
        {
            CurrentModuleName = Path.GetFileName(shared.CurrentModulePath);
        }
        else
        {
            CurrentModuleName = "(No module selected)";
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
    private async Task OpenModule()
    {
        if (_parentWindow == null) return;

        UnifiedLogger.LogApplication(LogLevel.INFO, "Open module dialog requested");

        // Modules are extracted to working directories (temp0, temp1, or named folders)
        // We browse for the extracted module folder, not the .mod file
        var options = new FolderPickerOpenOptions
        {
            Title = "Select Module Working Directory (extracted module folder)",
            AllowMultiple = false
        };

        // Start in NWN documents folder if available (where temp0/temp1/named modules live)
        var nwnPath = RadoubSettings.Instance.NeverwinterNightsPath;
        if (!string.IsNullOrEmpty(nwnPath) && Directory.Exists(nwnPath))
        {
            try
            {
                options.SuggestedStartLocation = await _parentWindow.StorageProvider
                    .TryGetFolderFromPathAsync(nwnPath);
            }
            catch
            {
                // Ignore if path can't be resolved - dialog will use default
            }
        }

        var result = await _parentWindow.StorageProvider.OpenFolderPickerAsync(options);

        if (result.Count > 0)
        {
            var selectedPath = result[0].Path.LocalPath;
            RadoubSettings.Instance.CurrentModulePath = selectedPath;
            SettingsService.Instance.AddRecentModule(selectedPath);
            UnifiedLogger.LogApplication(LogLevel.INFO, $"Opened module: {UnifiedLogger.SanitizePath(selectedPath)}");
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
        settingsWindow.ShowDialog(_parentWindow);
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
}
