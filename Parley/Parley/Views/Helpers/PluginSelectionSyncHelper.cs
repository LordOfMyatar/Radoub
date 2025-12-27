using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Threading;
using DialogEditor.Models;
using DialogEditor.Services;
using Radoub.Formats.Logging;
using DialogEditor.ViewModels;

namespace Parley.Views.Helpers
{
    /// <summary>
    /// Helper class for bidirectional node selection sync between Parley and plugins (Epic 40 Phase 3 / #234).
    /// Handles:
    /// - Updating DialogContextService.SelectedNodeId when tree selection changes
    /// - Selecting nodes in the tree when plugins request via gRPC SelectNode
    /// </summary>
    public class PluginSelectionSyncHelper : IDisposable
    {
        private readonly MainViewModel _viewModel;
        private readonly Func<string, Control?> _findControl;
        private readonly Func<TreeViewSafeNode?> _getSelectedNode;
        private readonly Action<TreeViewSafeNode> _setSelectedTreeItem;
        private bool _isSettingSelectionProgrammatically;

        public PluginSelectionSyncHelper(
            MainViewModel viewModel,
            Func<string, Control?> findControl,
            Func<TreeViewSafeNode?> getSelectedNode,
            Action<TreeViewSafeNode> setSelectedTreeItem)
        {
            _viewModel = viewModel;
            _findControl = findControl;
            _getSelectedNode = getSelectedNode;
            _setSelectedTreeItem = setSelectedTreeItem;

            // Subscribe to plugin node selection requests
            DialogContextService.Instance.NodeSelectionRequested += OnPluginNodeSelectionRequested;
        }

        /// <summary>
        /// Update DialogContextService.SelectedNodeId based on current tree selection.
        /// Call this from OnDialogTreeViewSelectionChanged.
        /// </summary>
        public void UpdateDialogContextSelectedNode()
        {
            var selectedNode = _getSelectedNode();

            if (selectedNode == null)
            {
                DialogContextService.Instance.SelectedNodeId = null;
                return;
            }

            if (selectedNode is TreeViewRootNode)
            {
                DialogContextService.Instance.SelectedNodeId = "root";
                return;
            }

            var dialog = _viewModel.CurrentDialog;
            if (dialog == null)
            {
                DialogContextService.Instance.SelectedNodeId = null;
                return;
            }

            // Check if this is a link node - needs special handling for link ID (#234)
            if (selectedNode is TreeViewSafeLinkNode linkNode)
            {
                var nodeId = GetLinkNodeId(linkNode, dialog);
                DialogContextService.Instance.SelectedNodeId = nodeId;
                UnifiedLogger.LogPlugin(LogLevel.DEBUG, $"Updated SelectedNodeId (link): {nodeId ?? "(null)"}");
                return;
            }

            // Get the node ID in the format used by the flowchart: "entry_N" or "reply_N"
            var dialogNode = selectedNode.OriginalNode;
            if (dialogNode == null)
            {
                DialogContextService.Instance.SelectedNodeId = null;
                return;
            }

            // Find the index of this node in the dialog
            string? nodeId2 = null;
            if (dialogNode.Type == DialogNodeType.Entry)
            {
                int index = dialog.Entries.IndexOf(dialogNode);
                if (index >= 0)
                    nodeId2 = $"entry_{index}";
            }
            else if (dialogNode.Type == DialogNodeType.Reply)
            {
                int index = dialog.Replies.IndexOf(dialogNode);
                if (index >= 0)
                    nodeId2 = $"reply_{index}";
            }

            DialogContextService.Instance.SelectedNodeId = nodeId2;
            UnifiedLogger.LogPlugin(LogLevel.DEBUG, $"Updated SelectedNodeId: {nodeId2 ?? "(null)"}");
        }

        /// <summary>
        /// Get the flowchart link ID for a tree view link node (#234).
        /// Format: "link_{parentIndex}_{targetIndex}"
        /// </summary>
        private string? GetLinkNodeId(TreeViewSafeLinkNode linkNode, Dialog dialog)
        {
            var targetNode = linkNode.OriginalNode;
            var parentNode = linkNode.ParentNode;

            if (targetNode == null || parentNode == null)
                return null;

            // Get parent index based on parent type
            int parentIndex = -1;
            if (parentNode.Type == DialogNodeType.Entry)
            {
                parentIndex = dialog.Entries.IndexOf(parentNode);
            }
            else if (parentNode.Type == DialogNodeType.Reply)
            {
                parentIndex = dialog.Replies.IndexOf(parentNode);
            }

            // Get target index based on target type
            int targetIndex = -1;
            if (targetNode.Type == DialogNodeType.Entry)
            {
                targetIndex = dialog.Entries.IndexOf(targetNode);
            }
            else if (targetNode.Type == DialogNodeType.Reply)
            {
                targetIndex = dialog.Replies.IndexOf(targetNode);
            }

            if (parentIndex < 0 || targetIndex < 0)
                return null;

            return $"link_{parentIndex}_{targetIndex}";
        }

        /// <summary>
        /// Handle plugin request to select a node.
        /// Called when a plugin (like the flowchart) requests node selection via gRPC.
        /// </summary>
        private void OnPluginNodeSelectionRequested(object? sender, NodeSelectionRequestedEventArgs e)
        {
            Dispatcher.UIThread.Post(() =>
            {
                SelectNodeById(e.NodeId);
            });
        }

        /// <summary>
        /// Select a node in the tree view by its ID.
        /// </summary>
        /// <param name="nodeId">Node ID in format "entry_N", "reply_N", or "root"</param>
        public void SelectNodeById(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId))
                return;

            var dialog = _viewModel.CurrentDialog;
            if (dialog == null)
                return;

            var treeView = _findControl("DialogTreeView") as TreeView;
            if (treeView == null)
                return;

            UnifiedLogger.LogPlugin(LogLevel.DEBUG, $"SelectNodeById: {nodeId}");

            // Find the DialogNode from the ID
            DialogNode? targetDialogNode = null;

            if (nodeId == "root")
            {
                // Select the root node
                var rootNode = _viewModel.DialogNodes.FirstOrDefault() as TreeViewRootNode;
                if (rootNode != null)
                {
                    _isSettingSelectionProgrammatically = true;
                    try
                    {
                        _setSelectedTreeItem(rootNode);
                        UnifiedLogger.LogPlugin(LogLevel.DEBUG, $"Selected root node");
                    }
                    finally
                    {
                        _isSettingSelectionProgrammatically = false;
                    }
                }
                return;
            }

            if (nodeId.StartsWith("entry_"))
            {
                if (int.TryParse(nodeId.Substring(6), out int index) && index >= 0 && index < dialog.Entries.Count)
                {
                    targetDialogNode = dialog.Entries[index];
                }
            }
            else if (nodeId.StartsWith("reply_"))
            {
                if (int.TryParse(nodeId.Substring(6), out int index) && index >= 0 && index < dialog.Replies.Count)
                {
                    targetDialogNode = dialog.Replies[index];
                }
            }
            else if (nodeId.StartsWith("link_"))
            {
                // Link node format: "link_{parentIndex}_{targetIndex}"
                // Find the link node in the tree by finding the parent, then finding the link child
                var linkNode = FindLinkNodeById(nodeId, dialog);
                if (linkNode != null)
                {
                    _isSettingSelectionProgrammatically = true;
                    try
                    {
                        _setSelectedTreeItem(linkNode);
                        UnifiedLogger.LogPlugin(LogLevel.DEBUG, $"Selected link node: {linkNode.DisplayText}");
                    }
                    finally
                    {
                        _isSettingSelectionProgrammatically = false;
                    }
                }
                else
                {
                    UnifiedLogger.LogPlugin(LogLevel.WARN, $"SelectNodeById: Could not find link node {nodeId}");
                }
                return;
            }

            if (targetDialogNode == null)
            {
                UnifiedLogger.LogPlugin(LogLevel.WARN, $"SelectNodeById: Could not find node {nodeId}");
                return;
            }

            // Find and select the TreeViewSafeNode that wraps this DialogNode
            var treeNode = FindTreeViewNodeByDialogNode(_viewModel.DialogNodes, targetDialogNode);
            if (treeNode != null)
            {
                _isSettingSelectionProgrammatically = true;
                try
                {
                    _setSelectedTreeItem(treeNode);
                    UnifiedLogger.LogPlugin(LogLevel.DEBUG, $"Selected node: {treeNode.DisplayText}");
                }
                finally
                {
                    _isSettingSelectionProgrammatically = false;
                }
            }
            else
            {
                UnifiedLogger.LogPlugin(LogLevel.WARN, $"SelectNodeById: Could not find TreeViewSafeNode for {nodeId}");
            }
        }

        /// <summary>
        /// Whether we're currently setting selection programmatically (to avoid feedback loops).
        /// </summary>
        public bool IsSettingSelectionProgrammatically => _isSettingSelectionProgrammatically;

        /// <summary>
        /// Find a TreeViewSafeNode by its underlying DialogNode.
        /// Searches recursively through the tree, expanding nodes as needed.
        /// </summary>
        private TreeViewSafeNode? FindTreeViewNodeByDialogNode(
            IEnumerable<TreeViewSafeNode> nodes,
            DialogNode targetNode,
            int maxDepth = 50)
        {
            if (maxDepth <= 0)
                return null;

            foreach (var node in nodes)
            {
                if (node.OriginalNode == targetNode)
                    return node;

                // If node has children, search them
                if (node.HasChildren && node.Children != null)
                {
                    // Expand node to populate children if needed
                    if (!node.IsExpanded)
                    {
                        node.IsExpanded = true;
                    }
                    node.PopulateChildren();

                    var found = FindTreeViewNodeByDialogNode(node.Children, targetNode, maxDepth - 1);
                    if (found != null)
                        return found;
                }
            }

            return null;
        }

        /// <summary>
        /// Find a link node in the tree by its ID.
        /// Link ID format: "link_{parentIndex}_{targetIndex}"
        /// Entry→Reply: link_{entryIndex}_{replyIndex}
        /// Reply→Entry: link_{replyIndex}_{entryIndex}
        /// </summary>
        private TreeViewSafeLinkNode? FindLinkNodeById(string linkId, Dialog dialog)
        {
            // Parse link ID: "link_{parentIndex}_{targetIndex}"
            var parts = linkId.Split('_');
            if (parts.Length != 3)
                return null;

            if (!int.TryParse(parts[1], out int idx1) ||
                !int.TryParse(parts[2], out int idx2))
                return null;

            // Try both interpretations and verify by checking actual pointers
            DialogNode? parentNode = null;
            DialogNode? targetNode = null;

            UnifiedLogger.LogPlugin(LogLevel.DEBUG, $"FindLinkNodeById: Parsing {linkId} -> idx1={idx1}, idx2={idx2}");

            // Try entry→reply: idx1 is entry index, idx2 is reply index
            if (idx1 < dialog.Entries.Count && idx2 < dialog.Replies.Count)
            {
                var entry = dialog.Entries[idx1];
                var reply = dialog.Replies[idx2];
                // Verify this entry actually has a link pointer to this reply
                var hasLinkPointer = entry.Pointers.Any(p => p.IsLink && p.Index == idx2);
                UnifiedLogger.LogPlugin(LogLevel.DEBUG, $"FindLinkNodeById: Trying Entry[{idx1}]→Reply[{idx2}], hasLinkPointer={hasLinkPointer}");
                if (hasLinkPointer)
                {
                    parentNode = entry;
                    targetNode = reply;
                }
            }

            // If not found, try reply→entry: idx1 is reply index, idx2 is entry index
            if (parentNode == null && idx1 < dialog.Replies.Count && idx2 < dialog.Entries.Count)
            {
                var reply = dialog.Replies[idx1];
                var entry = dialog.Entries[idx2];
                // Verify this reply actually has a link pointer to this entry
                var hasLinkPointer = reply.Pointers.Any(p => p.IsLink && p.Index == idx2);
                UnifiedLogger.LogPlugin(LogLevel.DEBUG, $"FindLinkNodeById: Trying Reply[{idx1}]→Entry[{idx2}], hasLinkPointer={hasLinkPointer}");
                if (hasLinkPointer)
                {
                    parentNode = reply;
                    targetNode = entry;
                }
            }

            if (parentNode == null || targetNode == null)
            {
                UnifiedLogger.LogPlugin(LogLevel.DEBUG, $"FindLinkNodeById: Could not resolve link {linkId}");
                return null;
            }

            // Search the entire tree for a link node matching both parent and target
            // This handles cases where the same DialogNode appears multiple times in the tree
            UnifiedLogger.LogPlugin(LogLevel.DEBUG, $"FindLinkNodeById: Searching tree for link from '{parentNode.DisplayText}' to '{targetNode.DisplayText}'");
            var linkNode = FindLinkNodeInTree(_viewModel.DialogNodes, parentNode, targetNode);
            if (linkNode != null)
            {
                UnifiedLogger.LogPlugin(LogLevel.DEBUG, $"FindLinkNodeById: Found link node '{linkNode.DisplayText}'");
            }
            else
            {
                UnifiedLogger.LogPlugin(LogLevel.DEBUG, $"FindLinkNodeById: Link node not found in tree");
            }
            return linkNode;
        }

        /// <summary>
        /// Search the entire tree for a link node matching both parent and target DialogNodes.
        /// This method handles the case where the same DialogNode appears multiple times in the tree.
        /// </summary>
        private TreeViewSafeLinkNode? FindLinkNodeInTree(
            IEnumerable<TreeViewSafeNode> nodes,
            DialogNode parentNode,
            DialogNode targetNode,
            int maxDepth = 50)
        {
            if (maxDepth <= 0)
                return null;

            foreach (var node in nodes)
            {
                // Check if this node is the parent we're looking for
                if (node.OriginalNode == parentNode)
                {
                    // Expand and populate children to find the link
                    if (!node.IsExpanded)
                        node.IsExpanded = true;
                    node.PopulateChildren();

                    if (node.Children != null)
                    {
                        foreach (var child in node.Children)
                        {
                            if (child is TreeViewSafeLinkNode linkNode && linkNode.OriginalNode == targetNode)
                            {
                                return linkNode;
                            }
                        }
                    }
                    // Don't return null here - there might be another instance of this parent
                    // in the tree that has the link we're looking for
                }

                // Recursively search children
                if (node.HasChildren && node.Children != null)
                {
                    if (!node.IsExpanded)
                        node.IsExpanded = true;
                    node.PopulateChildren();

                    var found = FindLinkNodeInTree(node.Children, parentNode, targetNode, maxDepth - 1);
                    if (found != null)
                        return found;
                }
            }

            return null;
        }

        public void Dispose()
        {
            DialogContextService.Instance.NodeSelectionRequested -= OnPluginNodeSelectionRequested;
        }
    }
}
