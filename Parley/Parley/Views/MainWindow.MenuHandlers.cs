using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using DialogEditor.Models;
using DialogEditor.Services;
using DialogEditor.Utils;
using Parley.Views.Helpers;
using Radoub.Formats.Logging;
using Radoub.UI.Utils;
using Radoub.UI.Views;

namespace DialogEditor.Views
{
    /// <summary>
    /// MainWindow partial class for menu event handlers (View, Flowchart, Scrap, Help, Settings).
    /// Extracted from MainWindow.axaml.cs (#1224, #1225).
    /// </summary>
    public partial class MainWindow
    {
        #region View Menu Handlers

        private void HideDebugConsoleByDefault()
        {
            try
            {
                var debugTab = this.FindControl<TabItem>("DebugTab");
                if (debugTab != null)
                {
                    // Set visibility from settings (default: false)
                    debugTab.IsVisible = _services.Settings.DebugWindowVisible;
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error setting debug console visibility: {ex.Message}");
            }
        }

        // Conversation Simulator handler - Issue #478
        private void OnConversationSimulatorClick(object? sender, RoutedEventArgs e)
        {
            var dialog = _services.DialogContext.CurrentDialog;
            var filePath = _services.DialogContext.CurrentFilePath;

            if (dialog == null || string.IsNullOrEmpty(filePath))
            {
                _viewModel.StatusMessage = "No dialog loaded. Open a dialog file first.";
                return;
            }

            _windows.ShowOrActivate(
                WindowKeys.ConversationSimulator,
                () => new ConversationSimulatorWindow(dialog, filePath));
        }

        private void OnClearDebugClick(object? sender, RoutedEventArgs e)
        {
            ClearDebugOutput();
        }

        private void OnLogLevelFilterChanged(object? sender, SelectionChangedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            if (comboBox == null)
                return;

            // Guard: handler fires during InitializeComponent before _services is assigned
            if (_services == null)
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
            _services.Settings.DebugLogFilterLevel = filterLevel;
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

        private void OnToggleUseRadoubThemeClick(object? sender, RoutedEventArgs e)
        {
            var settings = _services.Settings;
            settings.UseSharedTheme = !settings.UseSharedTheme;
            UpdateUseRadoubThemeMenuState();
            Radoub.UI.Services.ThemeManager.Instance.ApplyEffectiveTheme(settings.CurrentThemeId, settings.UseSharedTheme);
        }

        private void UpdateUseRadoubThemeMenuState()
        {
            var menuItem = this.FindControl<MenuItem>("UseRadoubThemeMenuItem");
            if (menuItem != null)
                menuItem.Icon = _services.Settings.UseSharedTheme ? new TextBlock { Text = "✓" } : null;
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
            _services.Script.ClearCache();
            _viewModel.StatusMessage = "Script cache refreshed";
        }

        #endregion

        #region Flowchart Menu Handlers

        private void OnFlowchartClick(object? sender, RoutedEventArgs e) => _controllers.Flowchart.OpenFloatingFlowchart();

        private void OnFlowchartLayoutClick(object? sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is string layoutValue)
            {
                _controllers.Flowchart.ApplyLayout(layoutValue);
            }
        }

        private async void OnExportFlowchartPngClick(object? sender, RoutedEventArgs e) => await _controllers.Flowchart.ExportToPngAsync();

        private async void OnExportFlowchartSvgClick(object? sender, RoutedEventArgs e) => await _controllers.Flowchart.ExportToSvgAsync();

        private void UpdateEmbeddedFlowchartAfterLoad() => _controllers.Flowchart.UpdateAfterLoad();

        /// <summary>
        /// Handles context menu actions from FlowchartPanel (#461).
        /// Routes actions to the appropriate existing handlers.
        /// </summary>
        private void OnFlowchartContextMenuAction(FlowchartContextMenuEventArgs e)
        {
            UnifiedLogger.LogUI(LogLevel.DEBUG, $"MainWindow handling flowchart context action: {e.Action}");

            switch (e.Action)
            {
                case "AddNode":
                    OnAddSmartNodeClick(null, null!);
                    break;
                case "AddSiblingNode":
                    OnAddSiblingNodeClick(null, null!);
                    break;
                case "DeleteNode":
                    OnDeleteNodeClick(null, null!);
                    break;
                case "CutNode":
                    OnCutNodeClick(null, null!);
                    break;
                case "CopyNode":
                    OnCopyNodeClick(null, null!);
                    break;
                case "PasteNode":
                    OnPasteAsDuplicateClick(null, null!);
                    break;
                case "PasteAsLink":
                    OnPasteAsLinkClick(null, null!);
                    break;
                case "ExpandSubnodes":
                    OnExpandSubnodesClick(null, null!);
                    break;
                case "CollapseSubnodes":
                    OnCollapseSubnodesClick(null, null!);
                    break;
                case "MoveUp":
                    OnMoveNodeUpClick(null, null!);
                    break;
                case "MoveDown":
                    OnMoveNodeDownClick(null, null!);
                    break;
                case "GoToLinkTarget":
                case "GoToParent":
                    OnGoToParentNodeClick(null, null!);
                    break;
                default:
                    UnifiedLogger.LogUI(LogLevel.WARN, $"Unknown flowchart context action: {e.Action}");
                    break;
            }
        }

        #endregion

        #region Script Browser Handlers

        private void OnBrowseConversationScriptClick(object? sender, RoutedEventArgs e)
        {
            if (sender is not Button button) return;
            _controllers.ScriptBrowser.OnBrowseConversationScriptClick(button.Tag?.ToString());
        }

        private void OnBrowseConditionalScriptClick(object? sender, RoutedEventArgs e)
            => _controllers.ScriptBrowser.OnBrowseConditionalScriptClick();

        private void OnBrowseActionScriptClick(object? sender, RoutedEventArgs e)
            => _controllers.ScriptBrowser.OnBrowseActionScriptClick();

        private void OnEditConditionalScriptClick(object? sender, RoutedEventArgs e)
            => _controllers.ScriptBrowser.OnEditConditionalScriptClick();

        private void OnEditActionScriptClick(object? sender, RoutedEventArgs e)
            => _controllers.ScriptBrowser.OnEditActionScriptClick();

        #endregion

        #region Edit Menu Handlers

        private void OnCopyNodeTextClick(object? sender, RoutedEventArgs e)
            => _controllers.EditMenu.OnCopyNodeTextClick(sender, e);

        private void OnCopyNodePropertiesClick(object? sender, RoutedEventArgs e)
            => _controllers.EditMenu.OnCopyNodePropertiesClick(sender, e);

        private void OnCopyTreeStructureClick(object? sender, RoutedEventArgs e)
            => _controllers.EditMenu.OnCopyTreeStructureClick(sender, e);

        private void OnUndoClick(object? sender, RoutedEventArgs e)
            => _controllers.EditMenu.OnUndoClick(sender, e);

        private void OnRedoClick(object? sender, RoutedEventArgs e)
            => _controllers.EditMenu.OnRedoClick(sender, e);

        private void OnCutNodeClick(object? sender, RoutedEventArgs e)
            => _controllers.EditMenu.OnCutNodeClick(sender, e);

        private void OnCopyNodeClick(object? sender, RoutedEventArgs e)
            => _controllers.EditMenu.OnCopyNodeClick(sender, e);

        private void OnPasteAsDuplicateClick(object? sender, RoutedEventArgs e)
            => _controllers.EditMenu.OnPasteAsDuplicateClick(sender, e);

        private void OnPasteAsLinkClick(object? sender, RoutedEventArgs e)
            => _controllers.EditMenu.OnPasteAsLinkClick(sender, e);

        #endregion
    }
}

