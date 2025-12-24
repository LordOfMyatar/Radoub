using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using DialogEditor.Models;
using DialogEditor.Services;
using DialogEditor.Utils;
using DialogEditor.ViewModels;
using DialogEditor.Views;

namespace Parley.Views.Helpers
{
    /// <summary>
    /// Manages all flowchart-related functionality for MainWindow.
    /// Extracted from MainWindow to reduce method size and improve maintainability (Epic #457, Sprint 1).
    ///
    /// Handles:
    /// 1. Flowchart layout modes (Floating, Side-by-Side, Tabbed)
    /// 2. PNG/SVG export functionality
    /// 3. Flowchart ↔ TreeView synchronization (node clicks, selection sync)
    /// 4. FlowView collapse/expand event handling
    /// 5. Flowchart panel updates on dialog changes
    /// </summary>
    public class FlowchartManager
    {
        private readonly Window _window;
        private readonly SafeControlFinder _controls;
        private readonly WindowLifecycleManager _windows;
        private readonly Func<MainViewModel> _getViewModel;
        private readonly Func<TreeViewSafeNode?> _getSelectedNode;
        private readonly Action<TreeViewSafeNode> _setSelectedNode;
        private readonly Action<TreeViewSafeNode> _populatePropertiesPanel;
        private readonly Action _saveCurrentNodeProperties;
        private readonly Func<bool> _getIsSettingSelectionProgrammatically;
        private readonly Action<bool> _setIsSettingSelectionProgrammatically;

        // Track whether embedded/tabbed panels have been wired up
        private bool _embeddedFlowchartWired = false;
        private bool _tabbedFlowchartWired = false;

        public FlowchartManager(
            Window window,
            SafeControlFinder controls,
            WindowLifecycleManager windows,
            Func<MainViewModel> getViewModel,
            Func<TreeViewSafeNode?> getSelectedNode,
            Action<TreeViewSafeNode> setSelectedNode,
            Action<TreeViewSafeNode> populatePropertiesPanel,
            Action saveCurrentNodeProperties,
            Func<bool> getIsSettingSelectionProgrammatically,
            Action<bool> setIsSettingSelectionProgrammatically)
        {
            _window = window;
            _controls = controls;
            _windows = windows;
            _getViewModel = getViewModel;
            _getSelectedNode = getSelectedNode;
            _setSelectedNode = setSelectedNode;
            _populatePropertiesPanel = populatePropertiesPanel;
            _saveCurrentNodeProperties = saveCurrentNodeProperties;
            _getIsSettingSelectionProgrammatically = getIsSettingSelectionProgrammatically;
            _setIsSettingSelectionProgrammatically = setIsSettingSelectionProgrammatically;
        }

        private MainViewModel ViewModel => _getViewModel();

        #region Layout Modes

        /// <summary>
        /// Opens the floating flowchart window (F5 shortcut).
        /// </summary>
        public void OpenFloatingFlowchart()
        {
            try
            {
                // Use WindowLifecycleManager for flowchart window
                var flowchart = _windows.ShowOrActivate(
                    WindowKeys.Flowchart,
                    () =>
                    {
                        var w = new FlowchartWindow();
                        w.NodeClicked += OnFlowchartNodeClicked;
                        return w;
                    });

                // Update with current dialog
                flowchart.UpdateDialog(ViewModel.CurrentDialog, ViewModel.CurrentFileName);

                // Mark flowchart as open (#377)
                SettingsService.Instance.FlowchartWindowOpen = true;
                SettingsService.Instance.FlowchartVisible = true;

                ViewModel.StatusMessage = "Flowchart view opened";
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error opening flowchart: {ex.Message}");
                ViewModel.StatusMessage = "Error opening flowchart view";
            }
        }

        /// <summary>
        /// Applies the selected flowchart layout mode.
        /// </summary>
        public void ApplyLayout(string layoutValue)
        {
            SettingsService.Instance.FlowchartLayout = layoutValue;
            UpdateLayoutMenuChecks();
            ApplyLayoutInternal();
            ViewModel.StatusMessage = $"Flowchart layout set to {layoutValue}";
        }

        /// <summary>
        /// Updates the flowchart layout menu check marks.
        /// </summary>
        public void UpdateLayoutMenuChecks()
        {
            var currentLayout = SettingsService.Instance.FlowchartLayout;

            var floatingItem = _window.FindControl<MenuItem>("FlowchartLayoutFloating");
            var sideBySideItem = _window.FindControl<MenuItem>("FlowchartLayoutSideBySide");
            var tabbedItem = _window.FindControl<MenuItem>("FlowchartLayoutTabbed");

            if (floatingItem != null)
                floatingItem.Icon = currentLayout == "Floating" ? new TextBlock { Text = "✓" } : null;
            if (sideBySideItem != null)
                sideBySideItem.Icon = currentLayout == "SideBySide" ? new TextBlock { Text = "✓" } : null;
            if (tabbedItem != null)
                tabbedItem.Icon = currentLayout == "Tabbed" ? new TextBlock { Text = "✓" } : null;
        }

        /// <summary>
        /// Applies the current layout setting.
        /// </summary>
        private void ApplyLayoutInternal()
        {
            var layout = SettingsService.Instance.FlowchartLayout;

            // Close existing floating window if switching to embedded mode
            if (layout != "Floating")
            {
                _windows.Close(WindowKeys.Flowchart);
            }

            // Apply layout based on setting
            switch (layout)
            {
                case "SideBySide":
                    ShowSideBySideFlowchart();
                    break;
                case "Tabbed":
                    ShowTabbedFlowchart();
                    break;
                default: // "Floating"
                    HideEmbeddedFlowchart();
                    break;
            }
        }

        /// <summary>
        /// Shows the side-by-side (embedded) flowchart panel.
        /// </summary>
        public void ShowSideBySideFlowchart()
        {
            // Hide tabbed panel if it was showing
            HideTabbedFlowchart();

            // Use WithControls for coordinated multi-control updates
            var success = _controls.WithControls<Grid, GridSplitter, Border, FlowchartPanel>(
                "MainContentGrid", "FlowchartSplitter", "EmbeddedFlowchartBorder", "EmbeddedFlowchartPanel",
                (grid, splitter, border, panel) =>
                {
                    if (grid.ColumnDefinitions.Count < 5) return;

                    // Show columns (indices 3 and 4 are the splitter and panel columns)
                    // Use saved width or default (#377)
                    var savedWidth = SettingsService.Instance.FlowchartPanelWidth;
                    grid.ColumnDefinitions[3].Width = new GridLength(5);
                    grid.ColumnDefinitions[4].Width = new GridLength(savedWidth, GridUnitType.Pixel);
                    grid.ColumnDefinitions[4].MinWidth = 200;

                    // Show controls
                    splitter.IsVisible = true;
                    border.IsVisible = true;

                    // Wire up node click handler if not already done
                    if (!_embeddedFlowchartWired)
                    {
                        panel.NodeClicked += OnEmbeddedFlowchartNodeClicked;
                        _embeddedFlowchartWired = true;
                    }

                    // Watch for column width changes to save (#377)
                    grid.ColumnDefinitions[4].PropertyChanged += OnFlowchartColumnWidthChanged;

                    // Update with current dialog
                    panel.UpdateDialog(ViewModel.CurrentDialog, ViewModel.CurrentFileName);
                });

            if (success)
            {
                // Mark flowchart as visible (#377)
                SettingsService.Instance.FlowchartVisible = true;
                UnifiedLogger.LogUI(LogLevel.INFO, "Side-by-side flowchart panel shown");
            }
            else
            {
                UnifiedLogger.LogUI(LogLevel.WARN, "Failed to show Side-by-Side flowchart: one or more controls not found");
            }
        }

        /// <summary>
        /// Shows the tabbed flowchart panel.
        /// </summary>
        public void ShowTabbedFlowchart()
        {
            // Hide side-by-side panel if it was showing
            HideSideBySideFlowchart();

            // Use WithControls for coordinated multi-control updates
            var success = _controls.WithControls<TabItem, FlowchartPanel>(
                "FlowchartTab", "TabbedFlowchartPanel",
                (tab, panel) =>
                {
                    tab.IsVisible = true;

                    // Wire up node click handler if not already done
                    if (!_tabbedFlowchartWired)
                    {
                        panel.NodeClicked += OnTabbedFlowchartNodeClicked;
                        _tabbedFlowchartWired = true;
                    }

                    // Update with current dialog
                    panel.UpdateDialog(ViewModel.CurrentDialog, ViewModel.CurrentFileName);
                });

            if (success)
            {
                // Mark flowchart as visible (#377)
                SettingsService.Instance.FlowchartVisible = true;
                UnifiedLogger.LogUI(LogLevel.INFO, "Tabbed flowchart panel shown");
            }
            else
            {
                UnifiedLogger.LogUI(LogLevel.WARN, "Failed to show Tabbed flowchart: tab or panel not found");
            }
        }

        /// <summary>
        /// Hides the side-by-side flowchart panel.
        /// </summary>
        public void HideSideBySideFlowchart()
        {
            // Use WithControls for coordinated multi-control updates
            _controls.WithControls<Grid, GridSplitter, Border>(
                "MainContentGrid", "FlowchartSplitter", "EmbeddedFlowchartBorder",
                (grid, splitter, border) =>
                {
                    if (grid.ColumnDefinitions.Count < 5) return;

                    // Hide columns (indices 3 and 4 are the splitter and panel columns)
                    grid.ColumnDefinitions[3].Width = new GridLength(0);
                    grid.ColumnDefinitions[4].Width = new GridLength(0);
                    grid.ColumnDefinitions[4].MinWidth = 0;

                    // Hide controls
                    splitter.IsVisible = false;
                    border.IsVisible = false;
                });
        }

        /// <summary>
        /// Hides the tabbed flowchart panel.
        /// </summary>
        public void HideTabbedFlowchart()
        {
            if (_controls.SetVisible("FlowchartTab", false))
            {
                UnifiedLogger.LogUI(LogLevel.INFO, "Tabbed flowchart panel hidden");
            }
        }

        /// <summary>
        /// Hides all embedded flowchart panels (side-by-side and tabbed).
        /// </summary>
        public void HideEmbeddedFlowchart()
        {
            HideSideBySideFlowchart();
            HideTabbedFlowchart();
            // Mark flowchart as not visible (#377)
            SettingsService.Instance.FlowchartVisible = false;
            UnifiedLogger.LogUI(LogLevel.INFO, "All embedded flowchart panels hidden");
        }

        /// <summary>
        /// Restores flowchart visibility on startup based on saved settings (#377).
        /// </summary>
        public void RestoreOnStartup()
        {
            var layout = SettingsService.Instance.FlowchartLayout;
            UnifiedLogger.LogUI(LogLevel.INFO, $"Restoring flowchart on startup: layout={layout}");

            switch (layout)
            {
                case "Floating":
                    OpenFloatingFlowchart();
                    break;
                case "SideBySide":
                    ShowSideBySideFlowchart();
                    break;
                case "Tabbed":
                    ShowTabbedFlowchart();
                    break;
            }
        }

        private void OnFlowchartColumnWidthChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property.Name == "Width" && sender is ColumnDefinition colDef && colDef.Width.IsAbsolute)
            {
                SettingsService.Instance.FlowchartPanelWidth = colDef.Width.Value;
            }
        }

        #endregion

        #region Panel Updates

        /// <summary>
        /// Updates all flowchart panels (floating, embedded, tabbed) with current dialog.
        /// Called when dialog structure changes to keep FlowView in sync with TreeView.
        /// </summary>
        public void UpdateAllPanels()
        {
            if (ViewModel.CurrentDialog == null)
                return;

            // Update floating flowchart window if open
            _windows.WithWindow<FlowchartWindow>(WindowKeys.Flowchart, w =>
            {
                w.UpdateDialog(ViewModel.CurrentDialog, ViewModel.CurrentFileName);
            });

            // Update embedded panel (side-by-side layout)
            var embeddedPanel = _window.FindControl<FlowchartPanel>("EmbeddedFlowchartPanel");
            if (embeddedPanel != null)
            {
                embeddedPanel.UpdateDialog(ViewModel.CurrentDialog, ViewModel.CurrentFileName);
            }

            // Update tabbed panel
            var tabbedPanel = _window.FindControl<FlowchartPanel>("TabbedFlowchartPanel");
            if (tabbedPanel != null)
            {
                tabbedPanel.UpdateDialog(ViewModel.CurrentDialog, ViewModel.CurrentFileName);
            }
        }

        /// <summary>
        /// Updates all flowchart views after a dialog is loaded.
        /// Handles floating window, side-by-side, and tabbed layouts (#394).
        /// </summary>
        public void UpdateAfterLoad()
        {
            var layout = SettingsService.Instance.FlowchartLayout;
            var embeddedBorder = _window.FindControl<Border>("EmbeddedFlowchartBorder");
            var embeddedPanel = _window.FindControl<FlowchartPanel>("EmbeddedFlowchartPanel");
            var flowchartTab = _window.FindControl<TabItem>("FlowchartTab");
            var tabbedPanel = _window.FindControl<FlowchartPanel>("TabbedFlowchartPanel");

            // Update floating window if open (#394)
            _windows.WithWindow<FlowchartWindow>(WindowKeys.Flowchart, w =>
            {
                w.UpdateDialog(ViewModel.CurrentDialog, ViewModel.CurrentFileName);
                UnifiedLogger.LogUI(LogLevel.DEBUG, "Floating flowchart updated after dialog load");
            });

            if (layout == "SideBySide" && embeddedBorder?.IsVisible == true && embeddedPanel != null)
            {
                embeddedPanel.UpdateDialog(ViewModel.CurrentDialog, ViewModel.CurrentFileName);
            }
            else if (layout == "Tabbed" && flowchartTab?.IsVisible == true && tabbedPanel != null)
            {
                tabbedPanel.UpdateDialog(ViewModel.CurrentDialog, ViewModel.CurrentFileName);
            }
        }

        /// <summary>
        /// Clears all flowchart views when a dialog file is closed (#378).
        /// </summary>
        public void ClearAll()
        {
            // Clear floating window if open
            _windows.WithWindow<FlowchartWindow>(WindowKeys.Flowchart, w =>
            {
                w.Clear();
                UnifiedLogger.LogUI(LogLevel.DEBUG, "Floating flowchart cleared");
            });

            // Clear embedded panel (side-by-side layout)
            var embeddedPanel = _window.FindControl<FlowchartPanel>("EmbeddedFlowchartPanel");
            if (embeddedPanel != null)
            {
                embeddedPanel.Clear();
            }

            // Clear tabbed panel
            var tabbedPanel = _window.FindControl<FlowchartPanel>("TabbedFlowchartPanel");
            if (tabbedPanel != null)
            {
                tabbedPanel.Clear();
            }

            UnifiedLogger.LogUI(LogLevel.DEBUG, "All flowchart views cleared");
        }

        /// <summary>
        /// Syncs selection to all flowchart panels when TreeView selection changes.
        /// </summary>
        public void SyncSelectionToFlowcharts(DialogNode? originalNode)
        {
            // Sync to floating window
            _windows.WithWindow<FlowchartWindow>(WindowKeys.Flowchart, w => w.SelectNode(originalNode));

            // Sync to embedded panel
            var embeddedBorder = _window.FindControl<Border>("EmbeddedFlowchartBorder");
            var embeddedPanel = _window.FindControl<FlowchartPanel>("EmbeddedFlowchartPanel");
            if (embeddedBorder?.IsVisible == true && embeddedPanel != null)
            {
                embeddedPanel.SelectNode(originalNode);
            }

            // Sync to tabbed panel
            var flowchartTab = _window.FindControl<TabItem>("FlowchartTab");
            var tabbedPanel = _window.FindControl<FlowchartPanel>("TabbedFlowchartPanel");
            if (tabbedPanel != null && flowchartTab?.IsVisible == true)
            {
                tabbedPanel.SelectNode(originalNode);
            }
        }

        #endregion

        #region Export

        /// <summary>
        /// Exports the flowchart to PNG format.
        /// </summary>
        public async Task ExportToPngAsync()
        {
            await ExportAsync("png");
        }

        /// <summary>
        /// Exports the flowchart to SVG format.
        /// </summary>
        public async Task ExportToSvgAsync()
        {
            await ExportAsync("svg");
        }

        private async Task ExportAsync(string format)
        {
            try
            {
                // Get the active flowchart panel
                FlowchartPanel? activePanel = GetActivePanel();
                if (activePanel == null)
                {
                    ViewModel.StatusMessage = "No flowchart to export. Open a dialog first.";
                    return;
                }

                // Set up file picker
                var storageProvider = _window.StorageProvider;
                var extension = format.ToLower();
                var filterName = extension.ToUpper();

                var options = new FilePickerSaveOptions
                {
                    Title = $"Export Flowchart as {filterName}",
                    SuggestedFileName = string.IsNullOrEmpty(ViewModel.CurrentFileName)
                        ? $"flowchart.{extension}"
                        : System.IO.Path.GetFileNameWithoutExtension(ViewModel.CurrentFileName) + $"_flowchart.{extension}",
                    FileTypeChoices = new[]
                    {
                        new FilePickerFileType(filterName) { Patterns = new[] { $"*.{extension}" } }
                    }
                };

                var file = await storageProvider.SaveFilePickerAsync(options);
                if (file == null) return;

                var filePath = file.Path.LocalPath;

                bool success;
                if (format == "png")
                {
                    success = await activePanel.ExportToPngAsync(filePath);
                }
                else
                {
                    success = await activePanel.ExportToSvgAsync(filePath);
                }

                if (success)
                {
                    ViewModel.StatusMessage = $"Flowchart exported to {System.IO.Path.GetFileName(filePath)}";
                }
                else
                {
                    ViewModel.StatusMessage = "Export failed. Check logs for details.";
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Export flowchart failed: {ex.Message}");
                ViewModel.StatusMessage = $"Export failed: {ex.Message}";
            }
        }

        /// <summary>
        /// Gets the currently active flowchart panel for export.
        /// </summary>
        public FlowchartPanel? GetActivePanel()
        {
            var layout = SettingsService.Instance.FlowchartLayout;
            var embeddedBorder = _window.FindControl<Border>("EmbeddedFlowchartBorder");
            var embeddedPanel = _window.FindControl<FlowchartPanel>("EmbeddedFlowchartPanel");
            var flowchartTab = _window.FindControl<TabItem>("FlowchartTab");
            var tabbedPanel = _window.FindControl<FlowchartPanel>("TabbedFlowchartPanel");

            switch (layout)
            {
                case "SideBySide":
                    if (embeddedBorder?.IsVisible == true && embeddedPanel?.ViewModel?.HasContent == true)
                        return embeddedPanel;
                    break;
                case "Tabbed":
                    if (flowchartTab?.IsVisible == true && tabbedPanel?.ViewModel?.HasContent == true)
                        return tabbedPanel;
                    break;
            }

            // For Floating mode or if embedded panels don't have content,
            // check if we can use the floating window
            if (_windows.IsOpen(WindowKeys.Flowchart))
            {
                // FlowchartWindow doesn't expose FlowchartPanel directly, so we need to update it
                // For now, just return the embedded panel if it has content
                if (embeddedBorder?.IsVisible == true && embeddedPanel?.ViewModel?.HasContent == true)
                    return embeddedPanel;
                if (flowchartTab?.IsVisible == true && tabbedPanel?.ViewModel?.HasContent == true)
                    return tabbedPanel;
            }

            // If no embedded panel is visible but we have a dialog loaded, use the side-by-side panel
            // (it's always in the XAML, just hidden)
            if (ViewModel.CurrentDialog != null && embeddedPanel != null)
            {
                // Temporarily update the embedded panel for export
                embeddedPanel.UpdateDialog(ViewModel.CurrentDialog, ViewModel.CurrentFileName);
                return embeddedPanel;
            }

            return null;
        }

        #endregion

        #region Node Click Handling & Tree Sync

        private void OnEmbeddedFlowchartNodeClicked(object? sender, FlowchartNode? flowchartNode)
        {
            // Reuse the same logic as the floating window
            OnFlowchartNodeClicked(sender, flowchartNode);
        }

        private void OnTabbedFlowchartNodeClicked(object? sender, FlowchartNode? flowchartNode)
        {
            // Reuse the same logic as the floating window
            OnFlowchartNodeClicked(sender, flowchartNode);
        }

        /// <summary>
        /// Handles node clicks from any flowchart panel (floating, embedded, tabbed).
        /// Selects the corresponding node in the TreeView.
        /// For link nodes, finds the specific link instance rather than the target node.
        /// </summary>
        private void OnFlowchartNodeClicked(object? sender, FlowchartNode? flowchartNode)
        {
            UnifiedLogger.LogUI(LogLevel.DEBUG, $"OnFlowchartNodeClicked: flowchartNode={flowchartNode?.Id}, OriginalNode={flowchartNode?.OriginalNode?.DisplayText ?? "null"}, IsLink={flowchartNode?.IsLink}");

            // ROOT node has no OriginalNode - just select the ROOT in tree
            if (flowchartNode?.NodeType == FlowchartNodeType.Root)
            {
                var rootNode = ViewModel.DialogNodes?.FirstOrDefault() as TreeViewRootNode;
                if (rootNode != null)
                {
                    _setSelectedNode(rootNode);
                    ViewModel.SelectedTreeNode = rootNode;
                    _populatePropertiesPanel(rootNode);
                    UnifiedLogger.LogUI(LogLevel.INFO, "Flowchart -> TreeView: Selected ROOT");
                    ViewModel.StatusMessage = "Selected: ROOT";
                }
                return;
            }

            if (flowchartNode?.OriginalNode == null || ViewModel.DialogNodes == null)
            {
                UnifiedLogger.LogUI(LogLevel.WARN, $"OnFlowchartNodeClicked: Early return - OriginalNode null or DialogNodes null");
                return;
            }

            try
            {
                var treeNavManager = new TreeNavigationManager();
                TreeViewSafeNode? treeNode = null;

                if (flowchartNode.IsLink)
                {
                    // For link nodes, we need to find the specific link instance in the tree
                    // The link has a pointer (OriginalPointer) that identifies which specific
                    // link child to select, not just any occurrence of the target node
                    treeNode = FindLinkNodeInTree(ViewModel.DialogNodes, flowchartNode);
                    UnifiedLogger.LogUI(LogLevel.DEBUG, $"FindLinkNodeInTree returned: {treeNode?.DisplayText ?? "null"}");
                }
                else
                {
                    // Regular node - find by DialogNode reference
                    treeNode = treeNavManager.FindTreeNodeForDialogNode(ViewModel.DialogNodes, flowchartNode.OriginalNode);
                    UnifiedLogger.LogUI(LogLevel.DEBUG, $"FindTreeNodeForDialogNode returned: {treeNode?.DisplayText ?? "null"}, IsChild={treeNode?.IsChild}");
                }

                if (treeNode != null)
                {
                    // Save previous node properties before switching
                    var currentSelectedNode = _getSelectedNode();
                    if (currentSelectedNode != null && !(currentSelectedNode is TreeViewRootNode))
                    {
                        _saveCurrentNodeProperties();
                    }

                    // Expand ancestors to make the node visible
                    treeNavManager.ExpandAncestors(ViewModel.DialogNodes, treeNode);

                    // Update selection state with flag to prevent feedback loops
                    // Setting SelectedTreeNode triggers PropertyChanged -> View handler -> TreeView.SelectedItem
                    // which would trigger OnDialogTreeViewSelectionChanged causing double population
                    _setIsSettingSelectionProgrammatically(true);
                    try
                    {
                        _setSelectedNode(treeNode);
                        ViewModel.SelectedTreeNode = treeNode;

                        // Also update the TreeView's selected item directly
                        var treeView = _window.FindControl<TreeView>("DialogTreeView");
                        if (treeView != null)
                        {
                            treeView.SelectedItem = treeNode;
                        }
                    }
                    finally
                    {
                        _setIsSettingSelectionProgrammatically(false);
                    }

                    // Directly populate properties panel (needed for tabbed mode where TreeView isn't visible)
                    var conversationSettingsPanel = _window.FindControl<StackPanel>("ConversationSettingsPanel");
                    var nodePropertiesPanel = _window.FindControl<StackPanel>("NodePropertiesPanel");

                    if (treeNode is TreeViewRootNode)
                    {
                        if (conversationSettingsPanel != null) conversationSettingsPanel.IsVisible = true;
                        if (nodePropertiesPanel != null) nodePropertiesPanel.IsVisible = false;
                    }
                    else
                    {
                        if (conversationSettingsPanel != null) conversationSettingsPanel.IsVisible = false;
                        if (nodePropertiesPanel != null) nodePropertiesPanel.IsVisible = true;
                    }

                    _populatePropertiesPanel(treeNode);

                    // Bring main window to front briefly to show selection, then return focus to flowchart
                    _window.Activate();

                    UnifiedLogger.LogUI(LogLevel.INFO, $"Flowchart -> TreeView: Selected '{treeNode.DisplayText}' (IsLink: {flowchartNode.IsLink})");
                    ViewModel.StatusMessage = $"Selected: {treeNode.DisplayText}";
                }
                else
                {
                    UnifiedLogger.LogUI(LogLevel.DEBUG, $"Could not find tree node for flowchart node {flowchartNode.Id}");
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error syncing flowchart selection: {ex.Message}");
            }
        }

        /// <summary>
        /// Finds a link node in the tree that matches the flowchart link node.
        /// Link nodes in the tree have IsChild=true and point to the same OriginalNode.
        /// </summary>
        private TreeViewSafeNode? FindLinkNodeInTree(ObservableCollection<TreeViewSafeNode> nodes, FlowchartNode flowchartNode)
        {
            return FindLinkNodeRecursive(nodes, flowchartNode, new System.Collections.Generic.HashSet<TreeViewSafeNode>());
        }

        private TreeViewSafeNode? FindLinkNodeRecursive(ObservableCollection<TreeViewSafeNode> nodes, FlowchartNode flowchartNode, System.Collections.Generic.HashSet<TreeViewSafeNode> visited)
        {
            foreach (var node in nodes)
            {
                if (!visited.Add(node)) continue;

                // For a link, we're looking for:
                // 1. A TreeViewSafeNode with IsChild=true (it's a link/child appearance)
                // 2. Whose OriginalNode matches the flowchart node's OriginalNode (target)
                if (node.IsChild && node.OriginalNode == flowchartNode.OriginalNode)
                {
                    return node;
                }

                // Check children
                if (node.HasChildren)
                {
                    node.PopulateChildren();
                    if (node.Children != null)
                    {
                        var found = FindLinkNodeRecursive(node.Children, flowchartNode, visited);
                        if (found != null)
                            return found;
                    }
                }

                visited.Remove(node);
            }

            return null;
        }

        #endregion

        #region FlowView Collapse/Expand Event Handling

        /// <summary>
        /// Handles collapse/expand events from FlowView to sync TreeView state (#451).
        /// Called from MainWindow.OnDialogChanged when Context == "FlowView".
        /// </summary>
        public void HandleFlowViewCollapseEvent(DialogChangeEventArgs e)
        {
            if (ViewModel.DialogNodes == null || ViewModel.DialogNodes.Count == 0)
                return;

            try
            {
                // Suppress TreeView events to prevent loops
                TreeViewSafeNode.SuppressCollapseEvents = true;

                switch (e.ChangeType)
                {
                    case DialogChangeType.NodeCollapsed:
                        if (e.AffectedNode != null)
                        {
                            CollapseTreeViewNode(e.AffectedNode);
                        }
                        break;

                    case DialogChangeType.NodeExpanded:
                        if (e.AffectedNode != null)
                        {
                            ExpandTreeViewNode(e.AffectedNode);
                        }
                        break;

                    case DialogChangeType.AllCollapsed:
                        CollapseAllTreeViewNodes();
                        break;

                    case DialogChangeType.AllExpanded:
                        ExpandAllTreeViewNodes();
                        break;
                }
            }
            finally
            {
                TreeViewSafeNode.SuppressCollapseEvents = false;
            }
        }

        /// <summary>
        /// Collapses a TreeView node matching the given DialogNode.
        /// </summary>
        private void CollapseTreeViewNode(DialogNode dialogNode)
        {
            var treeViewNode = FindTreeViewNodeForDialogNode(dialogNode);
            if (treeViewNode != null)
            {
                treeViewNode.IsExpanded = false;
                UnifiedLogger.LogUI(LogLevel.DEBUG, $"TreeView node collapsed (FlowView sync): {dialogNode.DisplayText.Substring(0, Math.Min(20, dialogNode.DisplayText.Length))}");
            }
        }

        /// <summary>
        /// Expands a TreeView node matching the given DialogNode.
        /// </summary>
        private void ExpandTreeViewNode(DialogNode dialogNode)
        {
            var treeViewNode = FindTreeViewNodeForDialogNode(dialogNode);
            if (treeViewNode != null)
            {
                treeViewNode.IsExpanded = true;
                UnifiedLogger.LogUI(LogLevel.DEBUG, $"TreeView node expanded (FlowView sync): {dialogNode.DisplayText.Substring(0, Math.Min(20, dialogNode.DisplayText.Length))}");
            }
        }

        /// <summary>
        /// Collapses all TreeView nodes (#451).
        /// </summary>
        private void CollapseAllTreeViewNodes()
        {
            if (ViewModel.DialogNodes == null)
                return;

            foreach (var rootNode in ViewModel.DialogNodes)
            {
                CollapseTreeViewNodeRecursive(rootNode);
            }
            UnifiedLogger.LogUI(LogLevel.DEBUG, "TreeView: All nodes collapsed (FlowView sync)");
        }

        /// <summary>
        /// Expands all TreeView nodes (#451).
        /// </summary>
        private void ExpandAllTreeViewNodes()
        {
            if (ViewModel.DialogNodes == null)
                return;

            foreach (var rootNode in ViewModel.DialogNodes)
            {
                ExpandTreeViewNodeRecursive(rootNode);
            }
            UnifiedLogger.LogUI(LogLevel.DEBUG, "TreeView: All nodes expanded (FlowView sync)");
        }

        private void CollapseTreeViewNodeRecursive(TreeViewSafeNode node)
        {
            node.IsExpanded = false;
            if (node.Children != null)
            {
                foreach (var child in node.Children)
                {
                    CollapseTreeViewNodeRecursive(child);
                }
            }
        }

        private void ExpandTreeViewNodeRecursive(TreeViewSafeNode node)
        {
            node.IsExpanded = true;
            if (node.Children != null)
            {
                foreach (var child in node.Children)
                {
                    ExpandTreeViewNodeRecursive(child);
                }
            }
        }

        /// <summary>
        /// Finds a TreeViewSafeNode in the tree that corresponds to a DialogNode.
        /// </summary>
        private TreeViewSafeNode? FindTreeViewNodeForDialogNode(DialogNode dialogNode)
        {
            if (ViewModel.DialogNodes == null)
                return null;

            foreach (var rootNode in ViewModel.DialogNodes)
            {
                var found = FindTreeViewNodeRecursive(rootNode, dialogNode);
                if (found != null)
                    return found;
            }
            return null;
        }

        private TreeViewSafeNode? FindTreeViewNodeRecursive(TreeViewSafeNode treeNode, DialogNode target)
        {
            if (treeNode.OriginalNode == target)
                return treeNode;

            if (treeNode.Children != null)
            {
                foreach (var child in treeNode.Children)
                {
                    var found = FindTreeViewNodeRecursive(child, target);
                    if (found != null)
                        return found;
                }
            }
            return null;
        }

        #endregion
    }
}
