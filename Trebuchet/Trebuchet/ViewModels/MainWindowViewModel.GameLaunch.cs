using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Radoub.Formats.Logging;
using Radoub.Formats.Settings;
using RadoubLauncher.Services;

namespace RadoubLauncher.ViewModels;

// Game launching commands and module directory helpers
public partial class MainWindowViewModel
{
    [RelayCommand]
    private void LaunchGame()
    {
        UnifiedLogger.LogApplication(LogLevel.INFO, "Launching NWN:EE");
        _gameLauncher.LaunchGame();
    }

    [RelayCommand]
    private async Task LaunchTestModule()
    {
        var moduleName = GameLauncherService.GetModuleNameFromPath(RadoubSettings.Instance.CurrentModulePath);
        if (string.IsNullOrEmpty(moduleName))
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, "Cannot launch test module: no module selected");
            return;
        }

        // Auto-save before testing if enabled
        if (SettingsService.Instance.AlwaysSaveBeforeTesting && CanBuildModule)
        {
            UnifiedLogger.LogApplication(LogLevel.INFO, "Auto-saving before test launch (AlwaysSaveBeforeTesting)");
            await BuildModuleAsync();
        }

        // Block launch if there are unresolved compilation failures
        if (HasFailedScripts)
        {
            BuildStatusText = "Cannot launch: fix failed scripts first";
            UnifiedLogger.LogApplication(LogLevel.WARN, "Test launch blocked — failed scripts present");
            return;
        }

        if (!SettingsService.Instance.AlwaysSaveBeforeTesting)
        {
            // Refresh build status so the Launch & Test tab shows current warnings
            RefreshBuildStatus();
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

        // Refresh build status so the Launch & Test tab shows current warnings
        RefreshBuildStatus();

        UnifiedLogger.LogApplication(LogLevel.INFO, $"Launching NWN:EE with +LoadNewModule \"{moduleName}\"");
        _gameLauncher.LaunchWithModule(moduleName, testMode: false);
    }

    private bool HasUnpackedWorkingDirectory()
        => ModulePathHelper.HasUnpackedWorkingDirectory(RadoubSettings.Instance.CurrentModulePath);

    private string? GetWorkingDirectoryPath()
        => ModulePathHelper.GetWorkingDirectory(RadoubSettings.Instance.CurrentModulePath);

    private string? GetModFilePath()
        => ModulePathHelper.GetModFilePath(RadoubSettings.Instance.CurrentModulePath);
}
