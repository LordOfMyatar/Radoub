using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using DialogEditor.Models;
using DialogEditor.Services;
using Radoub.Formats.Logging;
using DialogEditor.ViewModels;

namespace DialogEditor.Views
{
    /// <summary>
    /// MainWindow partial class for tree view operations (expand/collapse, state persistence).
    /// Extracted from MainWindow.axaml.cs for maintainability (#535).
    /// </summary>
    public partial class MainWindow
    {
        private void OnExpandAllClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            try
            {
                var treeView = this.FindControl<TreeView>("DialogTreeView");
                if (treeView != null)
                {
                    ExpandAllTreeViewItems(treeView);
                    _viewModel.StatusMessage = "Expanded all tree nodes";
                }
            }
            catch (System.Exception ex)
            {
                _viewModel.StatusMessage = $"Error expanding tree: {ex.Message}";
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to expand all: {ex.Message}");
            }
        }

        private void OnCollapseAllClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            try
            {
                var treeView = this.FindControl<TreeView>("DialogTreeView");
                if (treeView != null)
                {
                    CollapseAllTreeViewItems(treeView);
                    _viewModel.StatusMessage = "Collapsed all tree nodes";
                }
            }
            catch (System.Exception ex)
            {
                _viewModel.StatusMessage = $"Error collapsing tree: {ex.Message}";
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to collapse all: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle Word Wrap checkbox change - refresh tree to apply new TextWrapping (#903)
        /// </summary>
        private void OnWordWrapChanged(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            // Refresh tree view to apply new word wrap setting
            _viewModel.RefreshTreeViewColors();
            var enabled = SettingsService.Instance.TreeViewWordWrap;
            _viewModel.StatusMessage = enabled ? "Word wrap enabled" : "Word wrap disabled";
            UnifiedLogger.LogUI(LogLevel.INFO, $"TreeView word wrap toggled: {enabled}");
        }

        private void ExpandAllTreeViewItems(TreeView treeView)
        {
            // Avalonia approach: Work directly with ViewModel data
            if (_viewModel.DialogNodes == null || _viewModel.DialogNodes.Count == 0) return;

            foreach (var node in _viewModel.DialogNodes)
            {
                ExpandTreeNode(node);
            }
        }

        private void ExpandTreeNode(TreeViewSafeNode node)
        {
            node.IsExpanded = true;

            // Recursively expand children
            if (node.Children != null)
            {
                foreach (var child in node.Children)
                {
                    ExpandTreeNode(child);
                }
            }
        }

        private void CollapseAllTreeViewItems(TreeView treeView)
        {
            // Avalonia approach: Work directly with ViewModel data
            if (_viewModel.DialogNodes == null || _viewModel.DialogNodes.Count == 0) return;

            foreach (var node in _viewModel.DialogNodes)
            {
                CollapseTreeNode(node);
            }
        }

        private void CollapseTreeNode(TreeViewSafeNode node)
        {
            node.IsExpanded = false;

            // Recursively collapse children
            if (node.Children != null)
            {
                foreach (var child in node.Children)
                {
                    CollapseTreeNode(child);
                }
            }
        }

        private void RefreshTreeDisplay()
        {
            // OLD: This method collapses tree - kept for compatibility
            RefreshTreeDisplayPreserveState();
        }

        private void RefreshTreeDisplayPreserveState()
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG, "🔄 RefreshTreeDisplayPreserveState: Starting tree refresh");

            // Phase 0 Fix: Save expansion state AND selection before refresh
            var expandedNodes = new HashSet<TreeViewSafeNode>();
            SaveExpansionState(_viewModel.DialogNodes, expandedNodes);

            var selectedNodeText = _selectedNode?.OriginalNode?.DisplayText;
            // Issue #594: Capture the selected node reference to detect if user navigates away during refresh
            var selectedNodeAtRefreshStart = _selectedNode;
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"🔄 RefreshTreeDisplayPreserveState: Selected node text = '{selectedNodeText?.Substring(0, System.Math.Min(30, selectedNodeText?.Length ?? 0))}...'");

            // Force refresh by re-populating
            // Issue #882: Pass skipAutoSelect=true to prevent ROOT auto-selection during refresh
            // Selection will be restored by RestoreSelection below
            _viewModel.PopulateDialogNodes(skipAutoSelect: true);
            UnifiedLogger.LogApplication(LogLevel.DEBUG, "🔄 RefreshTreeDisplayPreserveState: PopulateDialogNodes completed");

            // Restore expansion state and selection after UI updates
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                RestoreExpansionState(_viewModel.DialogNodes, expandedNodes);

                // Issue #594: Only restore selection if user hasn't navigated to a different node
                // If user clicked a new node during refresh, _selectedNode will have changed
                if (!string.IsNullOrEmpty(selectedNodeText) && _selectedNode == selectedNodeAtRefreshStart)
                {
                    RestoreSelection(_viewModel.DialogNodes, selectedNodeText);
                }
                else if (_selectedNode != selectedNodeAtRefreshStart)
                {
                    UnifiedLogger.LogApplication(LogLevel.DEBUG,
                        "🔄 RefreshTreeDisplayPreserveState: Skipping selection restore - user navigated to different node");
                }
            }, global::Avalonia.Threading.DispatcherPriority.Loaded);
        }

        private void SaveExpansionState(System.Collections.ObjectModel.ObservableCollection<TreeViewSafeNode> nodes, HashSet<TreeViewSafeNode> expandedNodes)
        {
            foreach (var node in nodes)
            {
                if (node.IsExpanded)
                {
                    expandedNodes.Add(node);
                }
                if (node.Children != null)
                {
                    SaveExpansionState(node.Children, expandedNodes);
                }
            }
        }

        private void RestoreExpansionState(System.Collections.ObjectModel.ObservableCollection<TreeViewSafeNode> nodes, HashSet<TreeViewSafeNode> expandedNodes)
        {
            foreach (var node in nodes)
            {
                // Match by underlying node reference
                if (expandedNodes.Any(n => n.OriginalNode == node.OriginalNode))
                {
                    node.IsExpanded = true;
                }
                if (node.Children != null)
                {
                    RestoreExpansionState(node.Children, expandedNodes);
                }
            }
        }

        private void RestoreSelection(System.Collections.ObjectModel.ObservableCollection<TreeViewSafeNode> nodes, string displayText)
        {
            foreach (var node in nodes)
            {
                if (node.OriginalNode?.DisplayText == displayText)
                {
                    var treeView = this.FindControl<TreeView>("DialogTreeView");
                    if (treeView != null)
                    {
                        treeView.SelectedItem = node;
                        _selectedNode = node;
                    }
                    return;
                }
                if (node.Children != null)
                {
                    RestoreSelection(node.Children, displayText);
                }
            }
        }

        /// <summary>
        /// Expands all ancestor nodes to make target node visible (Issue #7)
        /// Handles "collapse all" scenario by expanding entire path from root to node
        /// </summary>
        private void ExpandToNode(TreeView treeView, TreeViewSafeNode targetNode)
        {
            // Collect all ancestors from target to root
            var ancestors = new List<TreeViewSafeNode>();
            var currentNode = targetNode;

            while (currentNode != null)
            {
                var parent = FindParentNode(treeView, currentNode);
                if (parent != null)
                {
                    ancestors.Add(parent);
                    currentNode = parent;
                }
                else
                {
                    break;
                }
            }

            // Expand from root down to target (reverse order)
            ancestors.Reverse();
            foreach (var ancestor in ancestors)
            {
                ancestor.IsExpanded = true;
            }

            if (ancestors.Count > 0)
            {
                UnifiedLogger.LogApplication(LogLevel.INFO, $"OnAddSmartNodeClick: Expanded {ancestors.Count} ancestor nodes to show new child");
            }
        }

        /// <summary>
        /// Finds the parent node of a given child node in the tree (Issue #7)
        /// </summary>
        private TreeViewSafeNode? FindParentNode(TreeView treeView, TreeViewSafeNode targetNode)
        {
            if (treeView.ItemsSource == null) return null;

            foreach (var item in treeView.ItemsSource)
            {
                if (item is TreeViewSafeNode node)
                {
                    var parent = FindParentNodeRecursive(node, targetNode);
                    if (parent != null) return parent;
                }
            }
            return null;
        }

        private TreeViewSafeNode? FindParentNodeRecursive(TreeViewSafeNode currentNode, TreeViewSafeNode targetNode)
        {
            if (currentNode.Children == null) return null;

            // Check if targetNode is a direct child
            if (currentNode.Children.Contains(targetNode))
            {
                return currentNode;
            }

            // Recurse through children
            foreach (var child in currentNode.Children)
            {
                var found = FindParentNodeRecursive(child, targetNode);
                if (found != null) return found;
            }

            return null;
        }

        /// <summary>
        /// Issue #609: Refreshes validation status for sibling nodes when a condition script changes.
        /// Recalculates which entry siblings are unreachable based on updated ScriptAppears values.
        /// </summary>
        private void RefreshSiblingValidation(TreeViewSafeNode changedNode)
        {
            try
            {
                UnifiedLogger.LogApplication(LogLevel.INFO,
                    $"RefreshSiblingValidation: ENTRY - changedNode='{changedNode?.OriginalNode?.DisplayText ?? "null"}'");

                if (changedNode == null)
                {
                    UnifiedLogger.LogApplication(LogLevel.WARN, "RefreshSiblingValidation: changedNode is null");
                    return;
                }

                // Debug: Log source pointer info to understand node's origin
                var srcPtr = changedNode.SourcePointer;
                UnifiedLogger.LogApplication(LogLevel.INFO,
                    $"RefreshSiblingValidation: changedNode.SourcePointer={srcPtr != null}, " +
                    $"ScriptAppears='{srcPtr?.ScriptAppears ?? "(null)"}', " +
                    $"NodeType={changedNode.OriginalNode?.Type}, IsChild={changedNode.IsChild}");

                var treeView = this.FindControl<TreeView>("DialogTreeView");
                if (treeView == null)
                {
                    UnifiedLogger.LogApplication(LogLevel.WARN, "RefreshSiblingValidation: TreeView not found");
                    return;
                }

                // Find the parent node that contains this node as a child
                var parentNode = FindParentNode(treeView, changedNode);
                if (parentNode?.Children == null)
                {
                    // Parent not found - this can happen if node selection changed or tree was rebuilt
                    // Fall back to full tree refresh which will recalculate all validation
                    UnifiedLogger.LogApplication(LogLevel.WARN,
                        $"RefreshSiblingValidation: Parent not found or has no children - falling back to full refresh");
                    RefreshTreeDisplayPreserveState();
                    return;
                }

                // Debug: Log parent node type to understand tree structure
                var parentTypeName = parentNode.GetType().Name;
                var parentIsRoot = parentNode is TreeViewRootNode;
                var parentIsLink = parentNode is TreeViewSafeLinkNode;
                UnifiedLogger.LogApplication(LogLevel.INFO,
                    $"RefreshSiblingValidation: Found parent '{parentNode.OriginalNode?.DisplayText}' " +
                    $"(Type={parentNode.OriginalNode?.Type}, NodeClass={parentTypeName}, IsRoot={parentIsRoot}, IsLink={parentIsLink}) " +
                    $"with {parentNode.Children.Count} children");

                // Issue #609: Handle ROOT node specially - it uses Starts list, not Pointers
                IList<DialogPtr>? pointersToCheck = null;

                if (parentNode is TreeViewRootNode rootNode)
                {
                    // ROOT node: siblings are in CurrentDialog.Starts
                    pointersToCheck = rootNode.Dialog?.Starts;
                    UnifiedLogger.LogApplication(LogLevel.INFO,
                        $"RefreshSiblingValidation: Parent is ROOT, using Starts list ({pointersToCheck?.Count ?? 0} entries)");
                }
                else
                {
                    // Regular node: siblings are in parent's Pointers list
                    pointersToCheck = parentNode.OriginalNode?.Pointers;
                    UnifiedLogger.LogApplication(LogLevel.INFO,
                        $"RefreshSiblingValidation: Parent DialogNode has {pointersToCheck?.Count ?? 0} pointers");
                }

                if (pointersToCheck == null || pointersToCheck.Count == 0)
                {
                    // Fallback: just refresh the tree to recalculate all validation
                    UnifiedLogger.LogApplication(LogLevel.INFO,
                        "RefreshSiblingValidation: No pointers found - refreshing tree display");
                    RefreshTreeDisplayPreserveState();
                    return;
                }

                // Log pointer conditions for debugging
                for (int i = 0; i < pointersToCheck.Count; i++)
                {
                    var ptr = pointersToCheck[i];
                    UnifiedLogger.LogApplication(LogLevel.DEBUG,
                        $"RefreshSiblingValidation: Pointer[{i}] Type={ptr.Type}, ScriptAppears='{ptr.ScriptAppears ?? "(null)"}', IsLink={ptr.IsLink}");
                }

                // Recalculate unreachable siblings using the updated pointer data
                var unreachableIndices = TreeViewValidation.CalculateUnreachableSiblings(pointersToCheck);

                UnifiedLogger.LogApplication(LogLevel.INFO,
                    $"RefreshSiblingValidation: Calculated {unreachableIndices.Count} unreachable indices: [{string.Join(",", unreachableIndices)}]");

                // Update each child's unreachable status
                int childIndex = 0;
                foreach (var child in parentNode.Children)
                {
                    if (child is TreeViewSafeNode safeChild)
                    {
                        bool isUnreachable = unreachableIndices.Contains(childIndex);
                        bool wasUnreachable = safeChild.IsUnreachableSibling;
                        safeChild.UpdateUnreachableStatus(isUnreachable);

                        if (wasUnreachable != isUnreachable)
                        {
                            UnifiedLogger.LogApplication(LogLevel.INFO,
                                $"RefreshSiblingValidation: Child[{childIndex}] '{safeChild.OriginalNode?.DisplayText}' changed: {wasUnreachable} -> {isUnreachable}");
                        }
                    }
                    childIndex++;
                }

                UnifiedLogger.LogApplication(LogLevel.INFO,
                    $"RefreshSiblingValidation: Updated {parentNode.Children.Count} siblings, {unreachableIndices.Count} marked unreachable");
            }
            catch (System.Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR,
                    $"RefreshSiblingValidation: Error updating sibling validation: {ex.Message}");
            }
        }
    }
}
