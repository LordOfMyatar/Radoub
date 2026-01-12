using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using DialogEditor.Models;
using DialogEditor.Services;
using Radoub.Formats.Logging;
using DialogEditor.Utils;
using DialogEditor.ViewModels;

namespace Parley.Views.Helpers
{
    /// <summary>
    /// Manages all TreeView UI interactions for MainWindow.
    /// Extracted from MainWindow to reduce method size and improve maintainability (Epic #457, Sprint 2).
    ///
    /// Handles:
    /// 1. Drag-drop UI event handlers (validation/move logic stays in TreeViewDragDropService)
    /// 2. Selection change handling and property panel updates
    /// 3. Expand/collapse operations (recursive)
    /// 4. Link node navigation (Go to Parent)
    /// 5. Double-tap to toggle expansion
    /// </summary>
    public class TreeViewUIController
    {
        private readonly Window _window;
        private readonly SafeControlFinder _controls;
        private readonly TreeViewDragDropService _dragDropService;
        private readonly Func<MainViewModel> _getViewModel;
        private readonly Func<TreeViewSafeNode?> _getSelectedNode;
        private readonly Action<TreeViewSafeNode?> _setSelectedNode;
        private readonly Action<TreeViewSafeNode> _populatePropertiesPanel;
        private readonly Action _saveCurrentNodeProperties;
        private readonly Action _clearAllFields;
        private readonly Func<bool> _getIsSettingSelectionProgrammatically;
        private readonly Action<DialogNode?> _syncSelectionToFlowcharts;
        private readonly Action _updatePluginSelectionSync;

        // Track current drop indicator target for cleanup
        private TreeViewItem? _currentDropIndicatorItem;
        private DropPosition _currentDropPosition;

        public TreeViewUIController(
            Window window,
            SafeControlFinder controls,
            TreeViewDragDropService dragDropService,
            Func<MainViewModel> getViewModel,
            Func<TreeViewSafeNode?> getSelectedNode,
            Action<TreeViewSafeNode?> setSelectedNode,
            Action<TreeViewSafeNode> populatePropertiesPanel,
            Action saveCurrentNodeProperties,
            Action clearAllFields,
            Func<bool> getIsSettingSelectionProgrammatically,
            Action<DialogNode?> syncSelectionToFlowcharts,
            Action updatePluginSelectionSync)
        {
            _window = window;
            _controls = controls;
            _dragDropService = dragDropService;
            _getViewModel = getViewModel;
            _getSelectedNode = getSelectedNode;
            _setSelectedNode = setSelectedNode;
            _populatePropertiesPanel = populatePropertiesPanel;
            _saveCurrentNodeProperties = saveCurrentNodeProperties;
            _clearAllFields = clearAllFields;
            _getIsSettingSelectionProgrammatically = getIsSettingSelectionProgrammatically;
            _syncSelectionToFlowcharts = syncSelectionToFlowcharts;
            _updatePluginSelectionSync = updatePluginSelectionSync;
        }

        private MainViewModel ViewModel => _getViewModel();

        #region Setup

        /// <summary>
        /// Sets up all TreeView event handlers for drag-drop operations.
        /// Call this from MainWindow constructor after TreeView is available.
        /// </summary>
        public void SetupTreeViewDragDrop()
        {
            var treeView = _window.FindControl<TreeView>("DialogTreeView");
            if (treeView == null)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN, "SetupTreeViewDragDrop: DialogTreeView not found");
                return;
            }

            // Wire up DragOver handler for visual feedback and validation
            DragDrop.SetAllowDrop(treeView, true);
            treeView.AddHandler(DragDrop.DragOverEvent, OnTreeViewDragOver);
            treeView.AddHandler(DragDrop.DropEvent, OnTreeViewDrop);
            treeView.AddHandler(DragDrop.DragLeaveEvent, OnTreeViewDragLeave);

            // Wire up pointer events for drag initiation on TreeViewItems
            treeView.AddHandler(InputElement.PointerPressedEvent, OnTreeViewItemPointerPressed, RoutingStrategies.Tunnel);
            treeView.AddHandler(InputElement.PointerMovedEvent, OnTreeViewItemPointerMoved, RoutingStrategies.Tunnel);
            treeView.AddHandler(InputElement.PointerReleasedEvent, OnTreeViewItemPointerReleased, RoutingStrategies.Tunnel);

            UnifiedLogger.LogApplication(LogLevel.INFO, "SetupTreeViewDragDrop: Drag-drop handlers registered");
        }

        #endregion

        #region Drag-Drop UI Handlers

        /// <summary>
        /// Handles pointer pressed on TreeView items to initiate potential drag.
        /// </summary>
        public void OnTreeViewItemPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            // Find the TreeViewItem and its DataContext (TreeViewSafeNode)
            var source = e.Source as Control;
            var treeViewItem = source?.FindAncestorOfType<TreeViewItem>();
            if (treeViewItem?.DataContext is TreeViewSafeNode node)
            {
                _dragDropService.OnPointerPressed(node, e);
            }
        }

        /// <summary>
        /// Handles pointer moved to detect drag threshold and start drag operation.
        /// </summary>
        public async void OnTreeViewItemPointerMoved(object? sender, PointerEventArgs e)
        {
            if (!_dragDropService.IsDragging && _dragDropService.DraggedNode != null)
            {
                var treeView = sender as TreeView;
                if (_dragDropService.OnPointerMoved(e, treeView))
                {
                    // Threshold exceeded, start the drag operation
                    var draggedNode = _dragDropService.DraggedNode;
                    if (draggedNode != null)
                    {
#pragma warning disable CS0618 // Type or member is obsolete - DataObject and DoDragDrop
                        var data = new DataObject();
                        data.Set("TreeViewSafeNode", draggedNode);

                        // Start the drag-drop operation
                        var result = await DragDrop.DoDragDrop(e, data, DragDropEffects.Move);
#pragma warning restore CS0618

                        // Always reset after DoDragDrop completes (regardless of result)
                        // OnTreeViewDrop also calls Reset(), but this ensures cleanup
                        // even if an exception occurs or drop is outside tree bounds
                        _dragDropService.Reset();
                    }
                }
            }
        }

        /// <summary>
        /// Handles pointer released to complete or cancel drag operation.
        /// </summary>
        public void OnTreeViewItemPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            // DragDrop.DoDragDrop handles the actual drop - this just resets if no drag started
            if (!_dragDropService.IsDragging)
            {
                _dragDropService.Reset();
            }
        }

        /// <summary>
        /// Handles DragOver event for the TreeView to show drop indicators.
        /// </summary>
        public void OnTreeViewDragOver(object? sender, DragEventArgs e)
        {
            e.DragEffects = DragDropEffects.None;

            // Get the dragged node from the data
#pragma warning disable CS0618 // Type or member is obsolete - Data property
            if (!e.Data.Contains("TreeViewSafeNode"))
                return;

            var draggedNode = e.Data.Get("TreeViewSafeNode") as TreeViewSafeNode;
#pragma warning restore CS0618
            if (draggedNode == null)
                return;

            // Find the target TreeViewItem under the pointer
            var treeView = sender as TreeView;
            var targetItem = FindTreeViewItemAtPosition(treeView, e);

            if (targetItem?.DataContext is TreeViewSafeNode targetNode)
            {
                // Calculate drop position based on pointer location within item
                // GetPosition(targetItem) returns position relative to targetItem, so Y starts at 0
                // We need bounds relative to self (starting at 0,0) for correct zone calculation
                var pointerPos = e.GetPosition(targetItem);
                var itemBounds = new Rect(0, 0, targetItem.Bounds.Width, targetItem.Bounds.Height);
                var dropPosition = _dragDropService.CalculateDropPosition(pointerPos, itemBounds);

                // Validate the drop
                var validation = _dragDropService.ValidateDrop(draggedNode, targetNode, dropPosition);

                if (validation.IsValid)
                {
                    e.DragEffects = DragDropEffects.Move;

                    // Update visual feedback
                    _dragDropService.NotifyDropPositionChanged(validation);
                    UpdateDropIndicator(targetItem, dropPosition);
                }
                else
                {
                    ClearDropIndicator();
                }
            }
            else
            {
                ClearDropIndicator();
            }

            e.Handled = true;
        }

        /// <summary>
        /// Handles Drop event to complete the drag-drop operation.
        /// Returns drop result for MainWindow to execute the actual move.
        /// </summary>
        public (TreeViewSafeNode? DraggedNode, DialogNode? NewParent, DropPosition Position, int InsertIndex)? OnTreeViewDrop(object? sender, DragEventArgs e)
        {
            ClearDropIndicator();

#pragma warning disable CS0618 // Type or member is obsolete - Data property
            if (!e.Data.Contains("TreeViewSafeNode"))
                return null;

            var draggedNode = e.Data.Get("TreeViewSafeNode") as TreeViewSafeNode;
#pragma warning restore CS0618
            if (draggedNode == null)
                return null;

            // Find the target TreeViewItem
            var treeView = sender as TreeView;
            var targetItem = FindTreeViewItemAtPosition(treeView, e);

            if (targetItem?.DataContext is TreeViewSafeNode targetNode)
            {
                // Use 0,0-based bounds - GetPosition returns relative coordinates
                var itemBounds = new Rect(0, 0, targetItem.Bounds.Width, targetItem.Bounds.Height);
                var dropPosition = _dragDropService.CalculateDropPosition(e.GetPosition(targetItem), itemBounds);

                var validation = _dragDropService.ValidateDrop(draggedNode, targetNode, dropPosition);
                if (validation.IsValid)
                {
                    // Get the new parent - prefer TreeViewSafeNode.OriginalNode, fall back to NewParentDialogNode
                    var newParentDialogNode = validation.NewParent?.OriginalNode ?? validation.NewParentDialogNode;

                    _dragDropService.Reset();
                    e.DragEffects = DragDropEffects.Move;
                    e.Handled = true;

                    return (draggedNode, newParentDialogNode, dropPosition, validation.InsertIndex);
                }
                else
                {
                    UnifiedLogger.LogApplication(LogLevel.DEBUG,
                        $"OnTreeViewDrop: Invalid drop - {validation.ErrorMessage}");
                    e.DragEffects = DragDropEffects.None;
                }
            }

            _dragDropService.Reset();
            e.Handled = true;
            return null;
        }

        /// <summary>
        /// Handles DragLeave to clear visual indicators.
        /// Note: Don't reset here - user might drag back into tree.
        /// Reset happens after DoDragDrop completes.
        /// </summary>
        public void OnTreeViewDragLeave(object? sender, DragEventArgs e)
        {
            ClearDropIndicator();
            _dragDropService.NotifyDropPositionChanged(null);
        }

        /// <summary>
        /// Finds the TreeViewItem at the given drag position.
        /// </summary>
        private TreeViewItem? FindTreeViewItemAtPosition(TreeView? treeView, DragEventArgs e)
        {
            if (treeView == null)
                return null;

            // Walk up from the source to find a TreeViewItem
            var source = e.Source as Control;
            return source?.FindAncestorOfType<TreeViewItem>();
        }

        /// <summary>
        /// Updates the visual drop indicator on a TreeViewItem.
        /// </summary>
        private void UpdateDropIndicator(TreeViewItem item, DropPosition position)
        {
            // Clear previous indicator if different item
            if (_currentDropIndicatorItem != item)
            {
                ClearDropIndicator();
            }

            _currentDropIndicatorItem = item;
            _currentDropPosition = position;

            // Apply visual indicator using CSS classes
            item.Classes.Remove("drop-before");
            item.Classes.Remove("drop-after");
            item.Classes.Remove("drop-into");

            switch (position)
            {
                case DropPosition.Before:
                    item.Classes.Add("drop-before");
                    break;
                case DropPosition.After:
                    item.Classes.Add("drop-after");
                    break;
                case DropPosition.Into:
                    item.Classes.Add("drop-into");
                    break;
            }
        }

        /// <summary>
        /// Clears the current drop indicator.
        /// </summary>
        public void ClearDropIndicator()
        {
            if (_currentDropIndicatorItem != null)
            {
                _currentDropIndicatorItem.Classes.Remove("drop-before");
                _currentDropIndicatorItem.Classes.Remove("drop-after");
                _currentDropIndicatorItem.Classes.Remove("drop-into");
                _currentDropIndicatorItem = null;
            }
        }

        #endregion

        #region Selection Handling

        /// <summary>
        /// Handles selection changes in the TreeView.
        /// </summary>
        public void OnDialogTreeViewSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            // Skip if this is a programmatic selection (prevents feedback loops from flowchart sync)
            if (_getIsSettingSelectionProgrammatically())
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG, "OnDialogTreeViewSelectionChanged: Skipping - programmatic selection");
                return;
            }

            var treeView = sender as TreeView;
            var newSelectedNode = treeView?.SelectedItem as TreeViewSafeNode;
            var currentSelectedNode = _getSelectedNode();

            // Skip if selection hasn't actually changed (prevents duplicate processing)
            if (newSelectedNode == currentSelectedNode)
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"OnDialogTreeViewSelectionChanged: Skipping - same node already selected: {newSelectedNode?.DisplayText ?? "null"}");
                return;
            }

            // CRITICAL FIX: Save the PREVIOUS node's properties before switching
            if (currentSelectedNode != null && !(currentSelectedNode is TreeViewRootNode))
            {
                _saveCurrentNodeProperties();
                UnifiedLogger.LogApplication(LogLevel.DEBUG, "Saved previous node properties before tree selection change");
            }

            _setSelectedNode(newSelectedNode);

            // Update ViewModel's selected tree node for Restore button enabling and bindings
            // Always update - the flags prevent View→ViewModel→View feedback loops elsewhere
            ViewModel.SelectedTreeNode = newSelectedNode;

            // Update DialogContextService.SelectedNodeId for plugin sync (Epic 40 Phase 3 / #234)
            _updatePluginSelectionSync();

            // Sync selection to flowchart (Epic #325 Sprint 3 - bidirectional sync)
            // Issue #457: Use FlowchartManager for sync
            _syncSelectionToFlowcharts(newSelectedNode?.OriginalNode);

            // Show/hide panels based on node type
            var conversationSettingsPanel = _window.FindControl<StackPanel>("ConversationSettingsPanel");
            var nodePropertiesPanel = _window.FindControl<StackPanel>("NodePropertiesPanel");

            if (newSelectedNode is TreeViewRootNode)
            {
                // ROOT node: Show conversation settings, hide node properties
                if (conversationSettingsPanel != null) conversationSettingsPanel.IsVisible = true;
                if (nodePropertiesPanel != null) nodePropertiesPanel.IsVisible = false;
            }
            else
            {
                // Regular node: Hide conversation settings, show node properties
                if (conversationSettingsPanel != null) conversationSettingsPanel.IsVisible = false;
                if (nodePropertiesPanel != null) nodePropertiesPanel.IsVisible = true;
            }

            if (newSelectedNode != null)
            {
                _populatePropertiesPanel(newSelectedNode);
            }
            else
            {
                _clearAllFields();
            }
        }

        /// <summary>
        /// Handles double-tap on TreeView items to toggle expansion.
        /// </summary>
        public void OnTreeViewItemDoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
        {
            // Toggle expansion of the selected node when double-tapped
            var selectedNode = _getSelectedNode();
            if (selectedNode != null)
            {
                try
                {
                    selectedNode.IsExpanded = !selectedNode.IsExpanded;
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Double-tap toggled node expansion: {selectedNode.IsExpanded}");
                }
                catch (Exception ex)
                {
                    UnifiedLogger.LogApplication(LogLevel.ERROR, $"Double-tap expand failed: {ex.Message}");
                    ViewModel.StatusMessage = "Error expanding node - see logs";
                }
            }
        }

        #endregion

        #region Expand/Collapse Operations

        /// <summary>
        /// Expands the selected node and all its subnodes recursively.
        /// </summary>
        public void OnExpandSubnodesClick(object? sender, RoutedEventArgs e)
        {
            var selectedNode = GetSelectedTreeNode();
            if (selectedNode == null)
            {
                ViewModel.StatusMessage = "Please select a node to expand";
                return;
            }

            ExpandNodeRecursive(selectedNode);
            ViewModel.StatusMessage = $"Expanded node and all subnodes: {selectedNode.DisplayText}";
        }

        /// <summary>
        /// Collapses the selected node and all its subnodes recursively.
        /// </summary>
        public void OnCollapseSubnodesClick(object? sender, RoutedEventArgs e)
        {
            var selectedNode = GetSelectedTreeNode();
            if (selectedNode == null)
            {
                ViewModel.StatusMessage = "Please select a node to collapse";
                return;
            }

            CollapseNodeRecursive(selectedNode);
            ViewModel.StatusMessage = $"Collapsed node and all subnodes: {selectedNode.DisplayText}";
        }

        /// <summary>
        /// Issue #149: Navigate from a link node to its parent (original) node.
        /// Only works when a link node (gray) is selected.
        /// </summary>
        public void OnGoToParentNodeClick(object? sender, RoutedEventArgs e)
        {
            var selectedNode = GetSelectedTreeNode();
            if (selectedNode == null)
            {
                ViewModel.StatusMessage = "Please select a link node";
                return;
            }

            // Only works on link nodes
            if (!selectedNode.IsChild)
            {
                ViewModel.StatusMessage = "Go to Parent only works on link nodes (gray nodes)";
                return;
            }

            // Get the underlying DialogNode that this link points to
            var targetDialogNode = selectedNode.OriginalNode;
            if (targetDialogNode == null)
            {
                ViewModel.StatusMessage = "Could not find linked node";
                return;
            }

            // Find the non-link occurrence of this node in the tree
            var parentNode = ViewModel.FindTreeNodeForDialogNode(targetDialogNode);
            if (parentNode == null || parentNode.IsChild)
            {
                ViewModel.StatusMessage = "Could not find parent node in tree";
                return;
            }

            // Navigate to and select the parent node
            ViewModel.SelectedTreeNode = parentNode;
            UnifiedLogger.LogApplication(LogLevel.INFO, $"Navigated from link to parent: {parentNode.DisplayText}");
            ViewModel.StatusMessage = $"Jumped to parent node: {parentNode.DisplayText}";
        }

        private TreeViewSafeNode? GetSelectedTreeNode()
        {
            var treeView = _window.FindControl<TreeView>("DialogTreeView");
            return treeView?.SelectedItem as TreeViewSafeNode;
        }

        private void ExpandNodeRecursive(TreeViewSafeNode node, HashSet<TreeViewSafeNode>? visited = null)
        {
            try
            {
                // Prevent infinite loops from circular references
                visited ??= new HashSet<TreeViewSafeNode>();

                if (!visited.Add(node))
                {
                    // Already visited - circular reference detected
                    UnifiedLogger.LogApplication(LogLevel.WARN, $"Circular reference detected in expand: {node.DisplayText}");
                    return;
                }

                node.IsExpanded = true;

                // Copy children list to avoid collection modification issues
                var children = node.Children?.ToList() ?? new List<TreeViewSafeNode>();
                foreach (var child in children)
                {
                    if (child != null)
                    {
                        ExpandNodeRecursive(child, visited);
                    }
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error expanding node '{node?.DisplayText}': {ex.Message}");
                ViewModel.StatusMessage = $"Error expanding node: {ex.Message}";
            }
        }

        private void CollapseNodeRecursive(TreeViewSafeNode node, HashSet<TreeViewSafeNode>? visited = null)
        {
            try
            {
                // Prevent infinite loops from circular references
                visited ??= new HashSet<TreeViewSafeNode>();

                if (!visited.Add(node))
                {
                    // Already visited - circular reference detected
                    UnifiedLogger.LogApplication(LogLevel.WARN, $"Circular reference detected in collapse: {node.DisplayText}");
                    return;
                }

                node.IsExpanded = false;

                // Copy children list to avoid collection modification issues
                var children = node.Children?.ToList() ?? new List<TreeViewSafeNode>();
                foreach (var child in children)
                {
                    if (child != null)
                    {
                        CollapseNodeRecursive(child, visited);
                    }
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error collapsing node '{node?.DisplayText}': {ex.Message}");
                ViewModel.StatusMessage = $"Error collapsing node: {ex.Message}";
            }
        }

        #endregion
    }
}
