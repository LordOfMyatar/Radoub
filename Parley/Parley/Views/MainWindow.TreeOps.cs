using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using DialogEditor.Models;
using DialogEditor.Services;
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
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"🔄 RefreshTreeDisplayPreserveState: Selected node text = '{selectedNodeText?.Substring(0, System.Math.Min(30, selectedNodeText?.Length ?? 0))}...'");

            // Force refresh by re-populating
            _viewModel.PopulateDialogNodes();
            UnifiedLogger.LogApplication(LogLevel.DEBUG, "🔄 RefreshTreeDisplayPreserveState: PopulateDialogNodes completed");

            // Restore expansion state and selection after UI updates
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                RestoreExpansionState(_viewModel.DialogNodes, expandedNodes);

                // Restore selection if we had one
                if (!string.IsNullOrEmpty(selectedNodeText))
                {
                    RestoreSelection(_viewModel.DialogNodes, selectedNodeText);
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
    }
}
