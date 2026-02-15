using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Radoub.Formats.Logging;
using Radoub.Formats.Settings;
using Radoub.UI.Views;
using RadoubLauncher.Services;

namespace RadoubLauncher.ViewModels;

// Module open/close/recent and embedded editor reload
public partial class MainWindowViewModel
{
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
    private void ClearRecentModules()
    {
        SettingsService.Instance.ClearRecentModules();
        UnifiedLogger.LogApplication(LogLevel.INFO, "Cleared recent modules list");
    }

    /// <summary>
    /// Reload module editor first (may auto-unpack .mod), then dependents that
    /// need the unpacked directory to exist. (#1384)
    /// </summary>
    private async Task ReloadModuleEditorThenDependentsAsync()
    {
        // Module editor must finish first - it auto-unpacks .mod files
        if (_moduleEditorViewModel != null)
        {
            var currentPath = RadoubSettings.Instance.CurrentModulePath;
            if (!string.IsNullOrEmpty(currentPath))
            {
                await _moduleEditorViewModel.LoadModuleAsync(currentPath);
            }
        }

        // Read DefaultBic after module is loaded
        _ = ReadModuleDefaultBicAsync(_cts.Token);

        // Now faction editor can find the unpacked directory
        if (_factionEditorViewModel != null)
        {
            await _factionEditorViewModel.LoadFacFileAsync();
        }
    }
}
