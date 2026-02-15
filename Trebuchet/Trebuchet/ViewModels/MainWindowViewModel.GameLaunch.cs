using System.IO;
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

    /// <summary>
    /// Check if an unpacked working directory exists for the current module.
    /// </summary>
    private bool HasUnpackedWorkingDirectory()
    {
        var modulePath = RadoubSettings.Instance.CurrentModulePath;
        if (string.IsNullOrEmpty(modulePath))
            return false;

        // If it's a .mod file, check for unpacked directory
        if (modulePath.EndsWith(".mod", System.StringComparison.OrdinalIgnoreCase) && File.Exists(modulePath))
        {
            var moduleName = Path.GetFileNameWithoutExtension(modulePath);
            var moduleDir = Path.GetDirectoryName(modulePath);
            if (string.IsNullOrEmpty(moduleDir))
                return false;

            var workingDir = Path.Combine(moduleDir, moduleName);
            return Directory.Exists(workingDir) && File.Exists(Path.Combine(workingDir, "module.ifo"));
        }

        // If it's already a directory path, check if module.ifo exists
        if (Directory.Exists(modulePath))
        {
            return File.Exists(Path.Combine(modulePath, "module.ifo"));
        }

        return false;
    }

    /// <summary>
    /// Get the working directory path for the current module.
    /// </summary>
    private string? GetWorkingDirectoryPath()
    {
        var modulePath = RadoubSettings.Instance.CurrentModulePath;
        if (string.IsNullOrEmpty(modulePath))
            return null;

        if (modulePath.EndsWith(".mod", System.StringComparison.OrdinalIgnoreCase) && File.Exists(modulePath))
        {
            var moduleName = Path.GetFileNameWithoutExtension(modulePath);
            var moduleDir = Path.GetDirectoryName(modulePath);
            if (string.IsNullOrEmpty(moduleDir))
                return null;

            var workingDir = Path.Combine(moduleDir, moduleName);
            if (Directory.Exists(workingDir))
                return workingDir;
        }
        else if (Directory.Exists(modulePath))
        {
            return modulePath;
        }

        return null;
    }

    /// <summary>
    /// Get the .mod file path for the current module.
    /// </summary>
    private string? GetModFilePath()
    {
        var modulePath = RadoubSettings.Instance.CurrentModulePath;
        if (string.IsNullOrEmpty(modulePath))
            return null;

        if (modulePath.EndsWith(".mod", System.StringComparison.OrdinalIgnoreCase))
            return modulePath;

        // If it's a directory, look for .mod file in parent
        if (Directory.Exists(modulePath))
        {
            var dirName = Path.GetFileName(modulePath);
            var parentDir = Path.GetDirectoryName(modulePath);
            if (!string.IsNullOrEmpty(parentDir))
            {
                var modPath = Path.Combine(parentDir, dirName + ".mod");
                if (File.Exists(modPath))
                    return modPath;
            }
        }

        return null;
    }
}
