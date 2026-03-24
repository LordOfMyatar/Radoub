using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using DialogEditor.Models;
using DialogEditor.Services;
using DialogEditor.ViewModels;
using DialogEditor.Views;
using Radoub.Formats.Logging;

namespace Parley.Views.Helpers
{
    /// <summary>
    /// Node click handling, tree synchronization, and FlowView collapse/expand event handling.
    /// </summary>
    public partial class FlowchartManager
    {
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
        /// Handles context menu actions from any flowchart panel (#461).
        /// Routes actions to the MainWindow via the callback.
        /// </summary>
        private void OnFlowchartContextMenuAction(object? sender, FlowchartContextMenuEventArgs e)
        {
            UnifiedLogger.LogUI(LogLevel.DEBUG, $"Flowchart context menu action: {e.Action} on node {e.Node.Id}");

            // First, select the node in TreeView to ensure operations target the correct node
            OnFlowchartNodeClicked(sender, e.Node);

            // Then route the action to MainWindow
            _onContextMenuAction?.Invoke(e);
        }

        /// <summary>
        /// Handles sibling reorder requests from flowchart drag-drop (#240).
        /// Routes to MainWindow for undo state + execution.
        /// </summary>
        private void OnFlowchartSiblingReorder(DialogNode node, DialogNode? parent, int fromIndex, int toIndex)
        {
            _onSiblingReorder?.Invoke(node, parent, fromIndex, toIndex);
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
            return FindLinkNodeRecursive(nodes, flowchartNode, new HashSet<TreeViewSafeNode>());
        }

        private TreeViewSafeNode? FindLinkNodeRecursive(ObservableCollection<TreeViewSafeNode> nodes, FlowchartNode flowchartNode, HashSet<TreeViewSafeNode> visited)
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
