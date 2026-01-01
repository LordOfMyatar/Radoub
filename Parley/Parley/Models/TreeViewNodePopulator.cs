using System.Collections.Generic;
using System.Collections.ObjectModel;
using Radoub.Formats.Logging;

namespace DialogEditor.Models
{
    /// <summary>
    /// Static service for populating TreeView node children.
    /// Handles lazy loading logic with circular reference protection.
    /// Extracted from TreeViewSafeNode.cs for maintainability (#708).
    /// </summary>
    public static class TreeViewNodePopulator
    {
        /// <summary>
        /// Maximum tree depth to prevent infinite recursion.
        /// Issue #32: Set to 250 to support dialogs up to depth 100+
        /// (Each Entry→Reply pair counts as 2 depth levels in TreeView)
        /// </summary>
        public const int MaxDepth = 250;

        /// <summary>
        /// Populates children for a TreeViewSafeNode.
        /// Matches copy tree function logic exactly for consistency.
        /// </summary>
        /// <param name="children">The children collection to populate</param>
        /// <param name="originalNode">The dialog node whose children to populate</param>
        /// <param name="ancestorNodes">Set of ancestor nodes for circular reference detection</param>
        /// <param name="depth">Current depth in the tree</param>
        public static void PopulateChildren(
            ObservableCollection<TreeViewSafeNode> children,
            DialogNode originalNode,
            HashSet<DialogNode> ancestorNodes,
            int depth)
        {
            if (children.Count > 0)
                return; // Already populated

            // Null safety check for originalNode (#375 stability)
            if (originalNode == null)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN, "🌳 TreeView: originalNode is null in PopulateChildren");
                return;
            }

            // Add basic depth protection to prevent infinite recursion
            if (depth >= MaxDepth)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN, $"🌳 TreeView: Hit max depth {depth} for node '{originalNode.DisplayText}' - dialog may be extremely deep");
                return;
            }

            // Null safety check for Pointers (#375 stability)
            if (originalNode.Pointers == null)
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"🌳 TreeView: Node '{originalNode.DisplayText}' has null Pointers");
                return;
            }

            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"🌳 TreeView: Populating children for '{originalNode.DisplayText}' at depth {depth}, has {originalNode.Pointers.Count} pointers");

            // Track nodes already added to this parent to prevent duplicates
            var addedNodes = new HashSet<DialogNode>();

            // Issue #484: Pre-calculate unreachable sibling warnings for NPC entries
            var unreachableSiblingIndices = TreeViewValidation.CalculateUnreachableSiblings(originalNode.Pointers);

            int pointerIndex = 0;
            foreach (var pointer in originalNode.Pointers)
            {
                if (pointer.Node != null)
                {
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"🌳 TreeView: Processing pointer to '{pointer.Node.DisplayText}' (Type: {pointer.Type}, Index: {pointer.Index}, IsLink: {pointer.IsLink})");

                    // Skip if we already added this node to this parent
                    if (addedNodes.Contains(pointer.Node))
                    {
                        UnifiedLogger.LogApplication(LogLevel.DEBUG, $"🌳 TreeView: Skipping duplicate node '{pointer.Node.DisplayText}'");
                        continue;
                    }

                    // Track that we're adding this node
                    addedNodes.Add(pointer.Node);

                    // Links are ALWAYS shown as link nodes (gray, IsChild marked) - don't expand them
                    if (pointer.IsLink)
                    {
                        UnifiedLogger.LogApplication(LogLevel.DEBUG, $"🌳 TreeView: Creating link node for '{pointer.Node.DisplayText}' (IsLink=true)");
                        var linkNode = new TreeViewSafeLinkNode(pointer.Node, depth + 1, "Link", pointer, originalNode);
                        children.Add(linkNode);
                    }
                    // Check if node is in our ancestor chain (circular reference within this path)
                    else if (ancestorNodes.Contains(pointer.Node))
                    {
                        UnifiedLogger.LogApplication(LogLevel.DEBUG, $"🌳 TreeView: Creating circular link for '{pointer.Node.DisplayText}' (ancestor chain protection)");
                        var linkNode = new TreeViewSafeLinkNode(pointer.Node, depth + 1, "Circular", null, originalNode);
                        children.Add(linkNode);
                    }
                    else
                    {
                        // This is a real child node - expand it with updated ancestor chain
                        UnifiedLogger.LogApplication(LogLevel.DEBUG, $"🌳 TreeView: Creating full child node for '{pointer.Node.DisplayText}'");

                        // Pass down ancestor chain for circular detection AND the source pointer for properties display
                        var newAncestors = new HashSet<DialogNode>(ancestorNodes) { originalNode };

                        // Issue #484: Check if this entry is unreachable
                        bool isUnreachable = unreachableSiblingIndices.Contains(pointerIndex);

                        var childNode = new TreeViewSafeNode(pointer.Node, newAncestors, depth + 1, pointer, isUnreachable);
                        children.Add(childNode);
                    }
                }
                else
                {
                    UnifiedLogger.LogApplication(LogLevel.WARN, $"🌳 TreeView: Pointer to Index {pointer.Index} has null Node!");
                }
                pointerIndex++;
            }

            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"🌳 TreeView: Finished populating '{originalNode.DisplayText}', added {children.Count} children");
        }
    }
}
