using System;
using System.Diagnostics;
using System.IO;
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
            // Theme management moved to Trebuchet — launch settings
            LaunchTrebuchetSettings();
        }

        private static void LaunchTrebuchetSettings()
        {
            try
            {
                var trebuchetPath = Radoub.Formats.Settings.RadoubSettings.Instance.TrebuchetPath;
                if (!string.IsNullOrEmpty(trebuchetPath) && File.Exists(trebuchetPath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = trebuchetPath,
                        Arguments = "--settings",
                        UseShellExecute = false
                    });
                }
                else
                {
                    UnifiedLogger.LogApplication(LogLevel.WARN, "Trebuchet not found — cannot open settings");
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN, $"Could not launch Trebuchet: {ex.Message}");
            }
        }

        private void OnEditSettingsFileClick(object? sender, RoutedEventArgs e)
        {
            var settingsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Radoub", "RadoubSettings.json");

            if (!File.Exists(settingsPath))
            {
                _viewModel.StatusMessage = "Settings file not found";
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo(settingsPath) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN, $"Failed to open settings file: {ex.Message}");
                _viewModel.StatusMessage = "Could not open settings file";
            }
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

        #region Search Handlers (#1842, #1843)

        private void OnFindClick(object? sender, RoutedEventArgs e)
        {
            var searchBar = this.FindControl<Radoub.UI.Controls.SearchBar>("DialogSearchBar");
            searchBar?.Show(_viewModel.CurrentFileName);
        }

        private void OnFindReplaceClick(object? sender, RoutedEventArgs e)
        {
            var searchBar = this.FindControl<Radoub.UI.Controls.SearchBar>("DialogSearchBar");
            searchBar?.ShowReplace(_viewModel.CurrentFileName);
        }

        private void OnSearchModuleClick(object? sender, RoutedEventArgs e)
        {
            var moduleDir = Services.ModuleDirectoryResolver.Resolve(
                Radoub.Formats.Settings.RadoubSettings.Instance.CurrentModulePath,
                _viewModel.CurrentFileName);

            if (string.IsNullOrEmpty(moduleDir))
            {
                _viewModel.StatusMessage = "Open a dialog file or use --mod to set a module first.";
                return;
            }

            var searchWindow = new ModuleSearchWindow();
            searchWindow.Initialize(moduleDir, _viewModel.CurrentFileName);
            searchWindow.Show(this);
        }

        private async void OnSearchFileModified(object? sender, EventArgs e)
        {
            // Reload the file after a replace operation modified it on disk
            if (!string.IsNullOrEmpty(_viewModel.CurrentFileName))
            {
                await _viewModel.LoadDialogAsync(_viewModel.CurrentFileName);
                _viewModel.StatusMessage = "File reloaded after replace.";
            }
        }

        private void OnSearchNavigateToMatch(object? sender, Radoub.Formats.Search.SearchMatch? match)
        {
            if (match?.Location is not Radoub.Formats.Search.DlgMatchLocation dlgLoc)
                return;

            // Navigate to the matching node in the tree
            NavigateToDialogNode(dlgLoc, match);
        }

        private void NavigateToDialogNode(
            Radoub.Formats.Search.DlgMatchLocation location,
            Radoub.Formats.Search.SearchMatch? match = null)
        {
            if (_viewModel.CurrentDialog == null || location.NodeIndex == null)
                return;

            var targetIndex = location.NodeIndex.Value;

            // Get the target DialogNode from the Dialog's Entries/Replies list by index
            DialogEditor.Models.DialogNode? targetDialogNode = null;
            if (location.NodeType == Radoub.Formats.Search.DlgNodeType.Entry &&
                targetIndex < _viewModel.CurrentDialog.Entries.Count)
            {
                targetDialogNode = _viewModel.CurrentDialog.Entries[targetIndex];
            }
            else if (location.NodeType == Radoub.Formats.Search.DlgNodeType.Reply &&
                     targetIndex < _viewModel.CurrentDialog.Replies.Count)
            {
                targetDialogNode = _viewModel.CurrentDialog.Replies[targetIndex];
            }

            if (targetDialogNode == null) return;

            var treeView = this.FindControl<Avalonia.Controls.TreeView>("DialogTreeView");
            if (treeView == null) return;

            // Find the first TreeViewSafeNode that wraps this DialogNode (by reference)
            var treeNode = FindTreeNodeByReference(_viewModel.DialogNodes, targetDialogNode);
            if (treeNode != null)
            {
                ExpandToNode(treeNode);
                treeView.SelectedItem = treeNode;

                // Show which field matched and the matched text in the status bar
                if (match != null)
                {
                    var fieldName = match.Field.Name;
                    var matchedText = match.MatchedText;
                    var preview = match.FullFieldValue.Length > 60
                        ? match.FullFieldValue[..60] + "..."
                        : match.FullFieldValue;
                    _viewModel.StatusMessage = $"Found \"{matchedText}\" in {fieldName} \u2014 {location.DisplayPath}: {preview}";
                }
                else
                {
                    _viewModel.StatusMessage = $"Found: {location.DisplayPath}";
                }
            }
        }

        /// <summary>
        /// Find a TreeViewSafeNode by its underlying DialogNode reference.
        /// Forces lazy-load of children at each level to traverse collapsed nodes.
        /// </summary>
        private Models.TreeViewSafeNode? FindTreeNodeByReference(
            System.Collections.ObjectModel.ObservableCollection<Models.TreeViewSafeNode> nodes,
            Models.DialogNode target)
        {
            foreach (var node in nodes)
            {
                if (ReferenceEquals(node.OriginalNode, target))
                    return node;

                // Force lazy-load so we can search collapsed subtrees
                node.PopulateChildren();

                if (node.Children == null || node.Children.Count == 0) continue;
                var found = FindTreeNodeByReference(node.Children, target);
                if (found != null)
                    return found;
            }
            return null;
        }

        /// <summary>
        /// Expand all ancestors of the target node so it becomes visible in the tree.
        /// </summary>
        private void ExpandToNode(Models.TreeViewSafeNode targetNode)
        {
            var path = new System.Collections.Generic.List<Models.TreeViewSafeNode>();
            FindNodePath(_viewModel.DialogNodes, targetNode, path);

            foreach (var ancestor in path)
                ancestor.IsExpanded = true;
        }

        private bool FindNodePath(
            System.Collections.ObjectModel.ObservableCollection<Models.TreeViewSafeNode> nodes,
            Models.TreeViewSafeNode target,
            System.Collections.Generic.List<Models.TreeViewSafeNode> path)
        {
            foreach (var node in nodes)
            {
                if (ReferenceEquals(node, target))
                    return true;

                // Force lazy-load for path finding too
                node.PopulateChildren();

                if (node.Children == null || node.Children.Count == 0) continue;
                path.Add(node);
                if (FindNodePath(node.Children, target, path))
                    return true;
                path.RemoveAt(path.Count - 1);
            }
            return false;
        }

        #endregion
    }
}

