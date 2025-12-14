using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using DialogEditor.Models;
using DialogEditor.Services;

namespace DialogEditor.Services
{
    /// <summary>
    /// Service responsible for tree view navigation, state management, and traversal.
    /// Extracted from MainViewModel to improve separation of concerns.
    ///
    /// Handles:
    /// 1. Tree expansion state (save/restore)
    /// 2. Tree node search and selection
    /// 3. Tree traversal and structure capture
    /// </summary>
    public class TreeNavigationManager
    {
        /// <summary>
        /// Finds a TreeViewSafeNode for a given DialogNode by recursively searching the tree.
        /// Prefers original (non-link) occurrences over child/link occurrences.
        /// </summary>
        public TreeViewSafeNode? FindTreeNodeForDialogNode(ObservableCollection<TreeViewSafeNode> dialogNodes, DialogNode nodeToFind)
        {
            TreeViewSafeNode? bestMatch = null;

            void FindNodeRecursive(ObservableCollection<TreeViewSafeNode> nodes)
            {
                foreach (var node in nodes)
                {
                    if (node.OriginalNode == nodeToFind)
                    {
                        // Prefer non-child (original) nodes over child (link) nodes
                        if (!node.IsChild)
                        {
                            bestMatch = node;
                            return; // Found the original, no need to search further
                        }
                        else if (bestMatch == null)
                        {
                            // Keep child as fallback if no original found yet
                            bestMatch = node;
                        }
                    }

                    // CRITICAL: Child/link nodes are terminal (bookmarks) - do NOT traverse them (#375)
                    // Only traverse non-link nodes that have children
                    if (!node.IsChild && node.HasChildren)
                    {
                        node.PopulateChildren();
                        if (node.Children != null)
                        {
                            FindNodeRecursive(node.Children);
                            // If we found an original node, stop searching
                            if (bestMatch != null && !bestMatch.IsChild)
                                return;
                        }
                    }
                }
            }

            FindNodeRecursive(dialogNodes);
            return bestMatch;
        }

        /// <summary>
        /// Saves the current tree expansion state (which nodes are expanded).
        /// Returns a set of DialogNodes that were expanded.
        /// </summary>
        public HashSet<DialogNode> SaveTreeExpansionState(ObservableCollection<TreeViewSafeNode> nodes)
        {
            var expandedRefs = new HashSet<DialogNode>();
            SaveTreeExpansionStateRecursive(nodes, expandedRefs);
            return expandedRefs;
        }

        private void SaveTreeExpansionStateRecursive(ObservableCollection<TreeViewSafeNode> nodes, HashSet<DialogNode> expandedRefs)
        {
            foreach (var node in nodes)
            {
                if (node.IsExpanded)
                {
                    expandedRefs.Add(node.OriginalNode);
                }
                if (node.Children != null && node.Children.Count > 0)
                {
                    SaveTreeExpansionStateRecursive(node.Children, expandedRefs);
                }
            }
        }

        /// <summary>
        /// Restores tree expansion state from a set of DialogNodes.
        /// </summary>
        public void RestoreTreeExpansionState(ObservableCollection<TreeViewSafeNode> nodes, HashSet<DialogNode> expandedRefs)
        {
            foreach (var node in nodes)
            {
                if (expandedRefs.Contains(node.OriginalNode))
                {
                    node.IsExpanded = true;
                }
                if (node.Children != null && node.Children.Count > 0)
                {
                    RestoreTreeExpansionState(node.Children, expandedRefs);
                }
            }
        }

        /// <summary>
        /// Captures expanded node paths for advanced tree state restoration.
        /// Uses path-based identification to handle nodes with same text in different locations.
        /// </summary>
        public HashSet<string> CaptureExpandedNodePaths(ObservableCollection<TreeViewSafeNode> nodes)
        {
            var expandedPaths = new HashSet<string>();
            CaptureExpandedNodesRecursive(nodes, "", expandedPaths, null);
            return expandedPaths;
        }

        private void CaptureExpandedNodesRecursive(ObservableCollection<TreeViewSafeNode> nodes, string parentPath, HashSet<string> expandedPaths, HashSet<TreeViewSafeNode>? visited)
        {
            if (nodes == null) return;

            // Circular reference protection
            visited ??= new HashSet<TreeViewSafeNode>();

            foreach (var node in nodes)
            {
                if (node == null) continue;

                // Circular reference check - skip if already visited
                if (!visited.Add(node))
                {
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Circular reference detected in tree state capture: {node.DisplayText}");
                    continue;
                }

                // Create unique path using display text + link status
                var nodePath = string.IsNullOrEmpty(parentPath)
                    ? GetNodeIdentifierInternal(node)
                    : $"{parentPath}|{GetNodeIdentifierInternal(node)}";

                // If expanded, record it
                if (node.IsExpanded)
                {
                    expandedPaths.Add(nodePath);
                }

                // CRITICAL: Child/link nodes are terminal (bookmarks) - do NOT traverse them (#375)
                // Only traverse non-link nodes that have children
                if (!node.IsChild && node.HasChildren)
                {
                    // Populate children before recursing
                    node.PopulateChildren();

                    if (node.Children != null)
                    {
                        CaptureExpandedNodesRecursive(node.Children, nodePath, expandedPaths, visited);
                    }
                }

                // Remove from visited after processing this branch (allows same node in different branches)
                visited.Remove(node);
            }
        }

        /// <summary>
        /// Restores tree expansion state using path-based identification.
        /// </summary>
        public void RestoreExpandedNodePaths(ObservableCollection<TreeViewSafeNode> nodes, HashSet<string> expandedPaths)
        {
            RestoreExpandedNodesRecursive(nodes, "", expandedPaths, null);
        }

        private void RestoreExpandedNodesRecursive(ObservableCollection<TreeViewSafeNode> nodes, string parentPath, HashSet<string> expandedPaths, HashSet<TreeViewSafeNode>? visited)
        {
            if (nodes == null) return;

            // Circular reference protection
            visited ??= new HashSet<TreeViewSafeNode>();

            foreach (var node in nodes)
            {
                if (node == null) continue;

                // Circular reference check - skip if already visited
                if (!visited.Add(node))
                {
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Circular reference detected in tree state restore: {node.DisplayText}");
                    continue;
                }

                // Create unique path using display text + link status
                var nodePath = string.IsNullOrEmpty(parentPath)
                    ? GetNodeIdentifierInternal(node)
                    : $"{parentPath}|{GetNodeIdentifierInternal(node)}";

                // Restore expansion state if path matches
                if (expandedPaths.Contains(nodePath))
                {
                    node.IsExpanded = true;
                }

                // CRITICAL: Child/link nodes are terminal (bookmarks) - do NOT traverse them (#375)
                // Only traverse non-link nodes that have children
                if (!node.IsChild && node.HasChildren)
                {
                    // Force populate children before recursing
                    node.PopulateChildren();

                    if (node.Children != null)
                    {
                        RestoreExpandedNodesRecursive(node.Children, nodePath, expandedPaths, visited);
                    }
                }

                // Remove from visited after processing this branch (allows same node in different branches)
                visited.Remove(node);
            }
        }

        /// <summary>
        /// Creates a unique identifier for a tree node.
        /// Uses display text + type + link status to distinguish between duplicate nodes.
        /// </summary>
        private string GetNodeIdentifierInternal(TreeViewSafeNode node)
        {
            // Use display text + type + link status as identifier
            // This distinguishes between link nodes and duplicate nodes with same text
            if (node is TreeViewRootNode)
                return "ROOT";

            var displayText = node.DisplayText ?? "UNKNOWN";
            var nodeType = node.OriginalNode?.Type.ToString() ?? "UNKNOWN";
            var isLink = node.IsChild ? "LINK" : "NODE";

            return $"{displayText}[{nodeType}:{isLink}]";
        }

        /// <summary>
        /// Gets the full path for a node (for selection restoration).
        /// </summary>
        public string GetNodePath(TreeViewSafeNode node)
        {
            if (node is TreeViewRootNode)
                return "ROOT";

            return GetNodeIdentifierInternal(node);
        }

        /// <summary>
        /// Finds a node by its path (for selection restoration after undo/redo).
        /// </summary>
        public TreeViewSafeNode? FindNodeByPath(ObservableCollection<TreeViewSafeNode> nodes, string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (path == "ROOT")
            {
                return nodes.OfType<TreeViewRootNode>().FirstOrDefault();
            }

            return FindNodeByPathRecursive(nodes, path, null);
        }

        private TreeViewSafeNode? FindNodeByPathRecursive(ObservableCollection<TreeViewSafeNode> nodes, string targetPath, HashSet<TreeViewSafeNode>? visited)
        {
            if (nodes == null) return null;

            visited ??= new HashSet<TreeViewSafeNode>();

            foreach (var node in nodes)
            {
                if (node == null) continue;

                // Circular reference check
                if (!visited.Add(node)) continue;

                var nodeId = GetNodeIdentifierInternal(node);
                if (nodeId == targetPath)
                {
                    return node;
                }

                // CRITICAL: Child/link nodes are terminal (bookmarks) - do NOT traverse them (#375)
                // Only traverse non-link nodes that have children
                if (!node.IsChild && node.HasChildren)
                {
                    // Force populate children before searching
                    node.PopulateChildren();

                    if (node.Children != null)
                    {
                        var found = FindNodeByPathRecursive(node.Children, targetPath, visited);
                        if (found != null)
                        {
                            return found;
                        }
                    }
                }

                visited.Remove(node);
            }

            return null;
        }

        /// <summary>
        /// Issue #252: Expands all ancestor nodes to make a target node visible.
        /// Used after redo to ensure restored nodes with children are visible.
        /// </summary>
        public void ExpandAncestors(ObservableCollection<TreeViewSafeNode> nodes, TreeViewSafeNode targetNode)
        {
            if (nodes == null || targetNode == null) return;

            // Find and expand all ancestors of the target node
            ExpandAncestorsRecursive(nodes, targetNode, null);
        }

        private bool ExpandAncestorsRecursive(ObservableCollection<TreeViewSafeNode> nodes, TreeViewSafeNode targetNode, HashSet<TreeViewSafeNode>? visited)
        {
            if (nodes == null) return false;

            visited ??= new HashSet<TreeViewSafeNode>();

            foreach (var node in nodes)
            {
                if (node == null) continue;

                // Circular reference protection
                if (!visited.Add(node)) continue;

                // Found the target - it's in this branch, return true to expand parent
                if (node == targetNode)
                {
                    return true;
                }

                // CRITICAL: Child/link nodes are terminal (bookmarks) - do NOT traverse them (#375)
                // Only traverse non-link nodes that have children
                if (!node.IsChild && node.HasChildren)
                {
                    // Force populate children before searching
                    node.PopulateChildren();

                    if (node.Children != null && ExpandAncestorsRecursive(node.Children, targetNode, visited))
                    {
                        // Target is in this subtree - expand this node
                        node.IsExpanded = true;
                        UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Expanded ancestor: {node.DisplayText}");
                        return true;
                    }
                }

                visited.Remove(node);
            }

            return false;
        }

        /// <summary>
        /// Captures the entire tree structure as a text representation.
        /// Useful for debugging and logging.
        /// </summary>
        public string CaptureTreeStructure(Dialog dialog)
        {
            if (dialog == null) return "No dialog loaded";

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== Dialog Tree Structure ===");
            sb.AppendLine($"Total Entries: {dialog.Entries.Count}");
            sb.AppendLine($"Total Replies: {dialog.Replies.Count}");
            sb.AppendLine($"Starting Entries: {dialog.Starts.Count}");
            sb.AppendLine();

            // Track visited nodes to prevent infinite loops
            var visitedNodes = new HashSet<DialogNode>();

            // Start from each starting entry
            for (int i = 0; i < dialog.Starts.Count; i++)
            {
                var start = dialog.Starts[i];
                if (start.Node != null)
                {
                    sb.AppendLine($"START [{i}]:");
                    CaptureNodeStructureRecursive(start.Node, sb, 1, "  ", visitedNodes);
                    sb.AppendLine();
                }
            }

            return sb.ToString();
        }

        private void CaptureNodeStructureRecursive(DialogNode node, System.Text.StringBuilder sb, int depth, string prefix, HashSet<DialogNode> visitedNodes)
        {
            if (node == null) return;

            // Prevent infinite loops from circular references
            bool isCircular = visitedNodes.Contains(node);
            if (!isCircular)
            {
                visitedNodes.Add(node);
            }

            // Build node line
            var nodeType = node.Type == DialogNodeType.Entry ? "E" : "R";
            var text = string.IsNullOrEmpty(node.DisplayText) ? "[CONTINUE]" : node.DisplayText;
            var truncatedText = text.Length > 60 ? text.Substring(0, 57) + "..." : text;

            sb.Append(prefix);
            sb.Append($"[{nodeType}] {truncatedText}");

            if (isCircular)
            {
                sb.AppendLine(" [CIRCULAR]");
                return; // Don't recurse into circular references
            }

            sb.AppendLine();

            // Recurse into children
            if (node.Pointers != null && node.Pointers.Count > 0)
            {
                foreach (var ptr in node.Pointers)
                {
                    if (ptr.Node != null)
                    {
                        var linkIndicator = ptr.IsLink ? " (LINK)" : "";
                        CaptureNodeStructureRecursive(ptr.Node, sb, depth + 1, prefix + "  ", visitedNodes);
                    }
                }
            }
        }
    }
}
