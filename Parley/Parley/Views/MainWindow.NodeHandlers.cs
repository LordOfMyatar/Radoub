using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using DialogEditor.Models;
using DialogEditor.Services;
using DialogEditor.ViewModels;
using Radoub.Formats.Logging;

namespace DialogEditor.Views
{
    /// <summary>
    /// MainWindow partial class for node CRUD operation handlers.
    /// Extracted from MainWindow.axaml.cs (#1222).
    /// </summary>
    public partial class MainWindow
    {
        private void OnAddContextAwareReply(object? sender, RoutedEventArgs e)
        {
            // Phase 1 Bug Fix: Format-correct node creation
            // ROOT → Entry (NPC speech)
            // Entry → PC Reply (player response)
            // PC Reply → Entry (NPC response)
            // Reply structs have NO Speaker field - all replies are PC

            var selectedNode = GetSelectedTreeNode();

            if (selectedNode == null || selectedNode is TreeViewRootNode)
            {
                // No selection or ROOT → Create Entry
                OnAddEntryClick(sender, e);
            }
            else
            {
                var parentNode = selectedNode.OriginalNode;

                if (parentNode.Type == DialogNodeType.Entry)
                {
                    // Entry → PC Reply
                    OnAddPCReplyClick(sender, e);
                }
                else // Reply node (always PC - Reply structs don't have Speaker)
                {
                    // PC Reply → Entry (NPC response)
                    OnAddEntryClick(sender, e);
                }
            }
        }

        // Node creation handlers - Phase 1 Step 3/4
        /// <summary>
        /// Smart Add Node - Context-aware node creation with auto-focus (Phase 2)
        /// </summary>
        private async void OnAddSmartNodeClick(object? sender, RoutedEventArgs e)
        {
            var selectedNode = GetSelectedTreeNode();
            await _services.NodeCreation.CreateSmartNodeAsync(selectedNode);
        }

        /// <summary>
        /// Issue #150: Add sibling node - creates node at same level as current selection.
        /// Uses parent of selected node as target for new node creation.
        /// </summary>
        private async void OnAddSiblingNodeClick(object? sender, RoutedEventArgs e)
        {
            var selectedNode = GetSelectedTreeNode();
            if (selectedNode == null)
            {
                _viewModel.StatusMessage = "Please select a node first";
                return;
            }

            // Cannot add sibling to ROOT
            if (selectedNode is TreeViewRootNode)
            {
                _viewModel.StatusMessage = "Cannot add sibling to ROOT - use Add Node instead";
                return;
            }

            // Cannot add sibling to link nodes
            if (selectedNode.IsChild)
            {
                _viewModel.StatusMessage = "Cannot add sibling to link nodes - select the parent node first";
                return;
            }

            // Find the parent node in the tree
            var treeView = this.FindControl<TreeView>("DialogTreeView");
            if (treeView == null) return;

            var parentNode = _services.NodeCreation.FindParentNode(treeView, selectedNode);

            // Add node as child of parent (sibling of selected)
            // This creates a new node at the same level as the selected node
            await _services.NodeCreation.CreateSmartNodeAsync(parentNode);
            UnifiedLogger.LogApplication(LogLevel.INFO, $"Added sibling node to: {selectedNode.DisplayText}");
        }

        private void OnAddEntryClick(object? sender, RoutedEventArgs e)
        {
            // Phase 1 Bug Fix: Entry nodes can be root-level OR child of Reply nodes
            var selectedNode = GetSelectedTreeNode();
            _viewModel.AddEntryNode(selectedNode);

            // Trigger auto-save after node creation
            TriggerDebouncedAutoSave();
        }

        // Phase 1 Bug Fix: Removed OnAddNPCReplyClick - use OnAddEntryClick for NPC responses after PC

        private void OnAddPCReplyClick(object? sender, RoutedEventArgs e)
        {
            var selectedNode = GetSelectedTreeNode();
            if (selectedNode == null)
            {
                _viewModel.StatusMessage = "Please select a parent node first";
                return;
            }

            // Check if ROOT selected
            if (selectedNode is TreeViewRootNode)
            {
                _viewModel.StatusMessage = "Cannot add PC Reply to ROOT. Select ROOT to add Entry instead.";
                return;
            }

            _viewModel.AddPCReplyNode(selectedNode);

            // Trigger auto-save after node creation
            TriggerDebouncedAutoSave();
        }

        private async void OnDeleteNodeClick(object? sender, RoutedEventArgs e)
        {
            var selectedNode = GetSelectedTreeNode();
            if (selectedNode == null)
            {
                _viewModel.StatusMessage = "Please select a node to delete";
                return;
            }

            // Issue #17: Block ROOT deletion silently with status message only (no dialog)
            if (selectedNode is TreeViewRootNode)
            {
                _viewModel.StatusMessage = "Cannot delete ROOT node";
                return;
            }

            // Check if delete confirmation is enabled (Issue #14)
            bool confirmed = true;
            if (_services.Settings.ShowDeleteConfirmation)
            {
                // Confirm deletion with "Don't show this again" option
                confirmed = await _services.Dialog.ShowConfirmDialogAsync(
                    "Delete Node",
                    $"Are you sure you want to delete this node and all its children?\n\n\"{selectedNode.DisplayText}\"",
                    showDontAskAgain: true
                );
            }

            if (confirmed)
            {
                _viewModel.DeleteNode(selectedNode);
            }
        }

        // Phase 2a: Node Reordering
        public void OnMoveNodeUpClick(object? sender, RoutedEventArgs e)
        {
            UnifiedLogger.LogApplication(LogLevel.INFO, "🔼 OnMoveNodeUpClick called");
            var selectedNode = GetSelectedTreeNode();
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Selected node: {selectedNode?.DisplayText ?? "null"}");

            if (selectedNode == null || selectedNode is TreeViewRootNode)
            {
                _viewModel.StatusMessage = "Select a node to move";
                UnifiedLogger.LogApplication(LogLevel.WARN, "No valid node selected for move up");
                return;
            }

            UnifiedLogger.LogApplication(LogLevel.INFO, $"Calling MoveNodeUp for: {selectedNode.DisplayText}");
            _viewModel.MoveNodeUp(selectedNode);
        }

        public void OnMoveNodeDownClick(object? sender, RoutedEventArgs e)
        {
            UnifiedLogger.LogApplication(LogLevel.INFO, "🔽 OnMoveNodeDownClick called");
            var selectedNode = GetSelectedTreeNode();
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Selected node: {selectedNode?.DisplayText ?? "null"}");

            if (selectedNode == null || selectedNode is TreeViewRootNode)
            {
                _viewModel.StatusMessage = "Select a node to move";
                UnifiedLogger.LogApplication(LogLevel.WARN, "No valid node selected for move down");
                return;
            }

            UnifiedLogger.LogApplication(LogLevel.INFO, $"Calling MoveNodeDown for: {selectedNode.DisplayText}");
            _viewModel.MoveNodeDown(selectedNode);
        }

        #region Selection and Drag-Drop

        private TreeViewSafeNode? GetSelectedTreeNode()
        {
            var treeView = this.FindControl<TreeView>("DialogTreeView");
            return treeView?.SelectedItem as TreeViewSafeNode;
        }

        /// <summary>
        /// Handles completion of a TreeView drag-drop operation (#450).
        /// </summary>
        private void OnDragDropCompleted(TreeViewSafeNode draggedNode, DialogNode? newParent, DropPosition position, int insertIndex)
        {
            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"OnDragDropCompleted: Moving '{draggedNode.DisplayText}' to {(newParent?.DisplayText ?? "ROOT")}, position={position}, index={insertIndex}");

            // Perform the move operation
            _viewModel.MoveNodeToPosition(draggedNode, newParent, insertIndex);
        }

        // Issue #463: Delegated to TreeViewUIController
        private void OnDialogTreeViewSelectionChanged(object? sender, SelectionChangedEventArgs e)
            => _controllers.TreeView.OnDialogTreeViewSelectionChanged(sender, e);

        // Issue #463: Delegated to TreeViewUIController
        private void OnTreeViewItemDoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
            => _controllers.TreeView.OnTreeViewItemDoubleTapped(sender, e);

        // Issue #463: Expand/Collapse/Navigate delegated to TreeViewUIController
        private void OnExpandSubnodesClick(object? sender, RoutedEventArgs e)
            => _controllers.TreeView.OnExpandSubnodesClick(sender, e);

        private void OnCollapseSubnodesClick(object? sender, RoutedEventArgs e)
            => _controllers.TreeView.OnCollapseSubnodesClick(sender, e);

        private void OnGoToParentNodeClick(object? sender, RoutedEventArgs e)
            => _controllers.TreeView.OnGoToParentNodeClick(sender, e);

        #endregion

        #region ViewModel Property Changed

        private async void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Load journal when file is loaded (CurrentFileName is set AFTER CurrentDialog)
            if (e.PropertyName == nameof(MainViewModel.CurrentFileName))
            {
                if (!string.IsNullOrEmpty(_viewModel.CurrentFileName))
                {
                    UnifiedLogger.LogApplication(LogLevel.INFO, $"CurrentFileName changed to: {UnifiedLogger.SanitizePath(_viewModel.CurrentFileName)} - loading journal");
                    await _controllers.Quest.LoadJournalForCurrentModuleAsync();
                }
            }

            // Watch for node selection requests - ViewModel finds the node, View sets TreeView.SelectedItem
            // This is needed because TreeView binding doesn't work well with lazily-populated children
            if (e.PropertyName == nameof(MainViewModel.SelectedTreeNode))
            {
                var selectedNode = _viewModel.SelectedTreeNode;
                // Only handle non-ROOT programmatic selection
                // Skip if selection came from TreeView (flag set by respective handler)
                if (selectedNode != null && !(selectedNode is TreeViewRootNode) &&
                    !_uiState.IsSettingSelectionProgrammatically)
                {
                    UnifiedLogger.LogApplication(LogLevel.DEBUG,
                        $"View: SelectedTreeNode changed to '{selectedNode.DisplayText}', scheduling selection");

                    // Defer selection to allow visual tree to render expanded children
                    // Use Background priority (lower than Loaded) to run AFTER layout has completed
                    global::Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
                    {
                        var treeView = this.FindControl<TreeView>("DialogTreeView");
                        if (treeView != null)
                        {
                            // Small delay to ensure TreeView has rendered expanded children
                            await Task.Delay(50);

                            // Expand ALL ancestors to ensure node is visible in visual tree
                            _services.NodeCreation.ExpandToNode(treeView, selectedNode);
                            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                                $"View: Expanded ancestors for '{selectedNode.DisplayText}'");

                            // Set flag to prevent feedback loop when setting SelectedItem
                            _uiState.IsSettingSelectionProgrammatically = true;
                            try
                            {
                                // Force set TreeView selection (binding alone doesn't work for lazy-loaded children)
                                treeView.SelectedItem = selectedNode;
                                UnifiedLogger.LogApplication(LogLevel.DEBUG,
                                    $"View: Set TreeView.SelectedItem to '{selectedNode.DisplayText}'");
                            }
                            finally
                            {
                                _uiState.IsSettingSelectionProgrammatically = false;
                            }
                        }
                    }, global::Avalonia.Threading.DispatcherPriority.Background);
                }
            }
        }

        #endregion
    }
}
