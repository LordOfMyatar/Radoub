using Avalonia.Controls;
using Avalonia.Interactivity;
using MerchantEditor.Services;
using Radoub.Formats.Logging;
using Radoub.UI.Views;

namespace MerchantEditor.Views;

/// <summary>
/// MainWindow partial: Script operations (browse, clear for OnOpenStore/OnStoreClosed)
/// </summary>
public partial class MainWindow
{
    #region Script Browse/Clear Handlers

    private async void OnBrowseOpenScript(object? sender, RoutedEventArgs e)
    {
        var script = await BrowseForScript();
        if (script != null)
        {
            OnOpenStoreBox.Text = script;
            _isDirty = true;
            UpdateTitle();
        }
    }

    private void OnClearOpenScript(object? sender, RoutedEventArgs e)
    {
        OnOpenStoreBox.Text = "";
        _isDirty = true;
        UpdateTitle();
    }

    private async void OnBrowseClosedScript(object? sender, RoutedEventArgs e)
    {
        var script = await BrowseForScript();
        if (script != null)
        {
            OnStoreClosedBox.Text = script;
            _isDirty = true;
            UpdateTitle();
        }
    }

    private void OnClearClosedScript(object? sender, RoutedEventArgs e)
    {
        OnStoreClosedBox.Text = "";
        _isDirty = true;
        UpdateTitle();
    }

    private async System.Threading.Tasks.Task<string?> BrowseForScript()
    {
        try
        {
            var context = new FenceScriptBrowserContext(_currentFilePath, _gameDataService);
            var browser = new ScriptBrowserWindow(context);
            var result = await browser.ShowDialog<string?>(this);

            if (!string.IsNullOrEmpty(result))
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Selected script: {result}");
                return result;
            }
        }
        catch (System.Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Script browser error: {ex.Message}");
            ShowError($"Failed to open script browser: {ex.Message}");
        }

        return null;
    }

    #endregion
}
