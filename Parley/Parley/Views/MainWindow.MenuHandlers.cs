using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using DialogEditor.Models;
using DialogEditor.Services;
using DialogEditor.Utils;
using Radoub.Formats.Logging;
using Radoub.UI.Utils;
using Radoub.UI.Views;

namespace DialogEditor.Views
{
    /// <summary>
    /// MainWindow partial class for menu event handlers (View, Scrap, Help, Settings).
    /// Extracted from MainWindow.axaml.cs (#1224).
    /// </summary>
    public partial class MainWindow
    {
        #region View Menu Handlers

        private void OnClearDebugClick(object? sender, RoutedEventArgs e)
        {
            ClearDebugOutput();
        }

        private void OnLogLevelFilterChanged(object? sender, SelectionChangedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            if (comboBox == null)
                return;

            var selectedIndex = comboBox.SelectedIndex;
            var filterLevel = selectedIndex switch
            {
                0 => LogLevel.ERROR,
                1 => LogLevel.WARN,
                2 => LogLevel.INFO,
                3 => LogLevel.DEBUG,
                4 => LogLevel.TRACE,
                _ => LogLevel.INFO
            };

            DebugLogger.SetLogLevelFilter(filterLevel);

            // Save the filter level to settings
            SettingsService.Instance.DebugLogFilterLevel = filterLevel;
        }

        private void OnOpenLogFolderClick(object? sender, RoutedEventArgs e)
        {
            _services.DebugLogging.OpenLogFolder();
        }

        private async void OnExportLogsClick(object? sender, RoutedEventArgs e)
        {
            await _services.DebugLogging.ExportLogsAsync(this);
        }

        #endregion

        #region Scrap Tab Handlers

        private void OnRestoreScrapClick(object? sender, RoutedEventArgs e)
        {
            var treeView = this.FindControl<TreeView>("DialogTreeView");
            var selectedNode = treeView?.SelectedItem as TreeViewSafeNode;
            _services.DebugLogging.RestoreFromScrap(selectedNode);
        }

        private async void OnClearScrapClick(object? sender, RoutedEventArgs e)
        {
            await _services.DebugLogging.ClearScrapAsync(this);
        }

        private void OnSwapRolesClick(object? sender, RoutedEventArgs e)
        {
            _services.DebugLogging.SwapScrapRoles();
        }

        #endregion

        #region Help Menu Handlers

        private void OnAboutClick(object? sender, RoutedEventArgs e)
        {
            var aboutWindow = AboutWindow.Create(new AboutWindowConfig
            {
                ToolName = "Parley",
                Subtitle = "Dialog Editor for Neverwinter Nights",
                Version = VersionHelper.GetVersion(),
                IconBitmap = new Avalonia.Media.Imaging.Bitmap(
                    Avalonia.Platform.AssetLoader.Open(
                        new System.Uri("avares://Parley/Assets/parley.ico")))
            });
            aboutWindow.Show(this);
        }

        private void OnDocumentationClick(object? sender, RoutedEventArgs e)
        {
            try
            {
                var url = "https://github.com/LordOfMyatar/Radoub/wiki";
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error opening documentation: {ex.Message}");
                _viewModel.StatusMessage = "Could not open documentation URL";
            }
        }

        private void OnReportIssueClick(object? sender, RoutedEventArgs e)
        {
            try
            {
                var url = "https://github.com/LordOfMyatar/Radoub/issues/new";
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error opening issue page: {ex.Message}");
                _viewModel.StatusMessage = "Could not open issue page URL";
            }
        }

        #endregion

        #region Settings Handlers

        // Issue #343: Common callback for Settings window close
        private void OnSettingsWindowClosed(SettingsWindow _)
        {
            ApplySavedTheme();
            _viewModel.StatusMessage = "Settings updated";
        }

        private void OnPreferencesClick(object? sender, RoutedEventArgs e)
        {
            try
            {
                // Issue #343: Use WindowLifecycleManager for Settings window
                _windows.ShowOrActivate(
                    WindowKeys.Settings,
                    () => new SettingsWindow(),
                    OnSettingsWindowClosed);
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error opening preferences: {ex.Message}");
                _viewModel.StatusMessage = $"Error opening preferences: {ex.Message}";
            }
        }

        private void OnGameDirectoriesClick(object? sender, RoutedEventArgs e)
        {
            try
            {
                // Issue #343: Use WindowLifecycleManager - if open, just activate
                if (_windows.IsOpen(WindowKeys.Settings))
                {
                    _windows.WithWindow<SettingsWindow>(WindowKeys.Settings, w => w.Activate());
                    return;
                }

                // Open preferences with Resource Paths tab selected (tab 0)
                _windows.ShowOrActivate(
                    WindowKeys.Settings,
                    () => new SettingsWindow(initialTab: 0),
                    OnSettingsWindowClosed);
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error opening game directories: {ex.Message}");
                _viewModel.StatusMessage = $"Error opening settings: {ex.Message}";
            }
        }

        private void OnLogSettingsClick(object? sender, RoutedEventArgs e)
        {
            try
            {
                // Issue #343: Use WindowLifecycleManager - if open, just activate
                if (_windows.IsOpen(WindowKeys.Settings))
                {
                    _windows.WithWindow<SettingsWindow>(WindowKeys.Settings, w => w.Activate());
                    return;
                }

                // Open preferences with Logging tab selected (tab 2)
                _windows.ShowOrActivate(
                    WindowKeys.Settings,
                    () => new SettingsWindow(initialTab: 2),
                    OnSettingsWindowClosed);
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error opening logging settings: {ex.Message}");
                _viewModel.StatusMessage = $"Error opening settings: {ex.Message}";
            }
        }

        private void OnRefreshScriptCacheClick(object? sender, RoutedEventArgs e)
        {
            ScriptService.Instance.ClearCache();
            _viewModel.StatusMessage = "Script cache refreshed";
        }

        #endregion
    }
}
