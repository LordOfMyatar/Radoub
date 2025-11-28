using System;
using System.Collections.Generic;
using DialogEditor.Models;
using DialogEditor.Services;
using DialogEditor.Utils;

namespace DialogEditor.Services
{
    /// <summary>
    /// Service responsible for deep cloning dialog nodes.
    /// Extracted from MainViewModel to improve separation of concerns.
    ///
    /// Handles:
    /// - Deep cloning of DialogNode objects with all properties
    /// - Recursive cloning of child nodes
    /// - Circular reference detection and handling
    /// - LocString cloning
    /// - LinkRegistry integration for cloned pointers
    /// </summary>
    public class NodeCloningService
    {
        /// <summary>
        /// Creates a deep clone of a dialog node and all its children.
        /// Registers all cloned nodes and pointers with the dialog's LinkRegistry.
        /// </summary>
        /// <param name="original">The node to clone</param>
        /// <param name="dialog">The dialog to add cloned nodes to</param>
        /// <returns>The cloned node</returns>
        public DialogNode CloneNode(DialogNode original, Dialog dialog)
        {
            return CloneNodeWithDepth(original, dialog, 0, new HashSet<DialogNode>());
        }

        /// <summary>
        /// Recursively clones a node with depth tracking to prevent infinite recursion.
        /// </summary>
        private DialogNode CloneNodeWithDepth(DialogNode original, Dialog dialog, int depth, HashSet<DialogNode> visited)
        {
            // Prevent infinite recursion with depth limit
            const int MAX_DEPTH = 100;
            if (depth > MAX_DEPTH)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Maximum clone depth ({MAX_DEPTH}) exceeded - possible circular reference");
                throw new InvalidOperationException($"Maximum clone depth ({MAX_DEPTH}) exceeded during node cloning");
            }

            // Prevent circular reference cloning
            if (!visited.Add(original))
            {
                UnifiedLogger.LogApplication(LogLevel.WARN, $"Circular reference detected during clone at depth {depth} - creating link instead");
                // Return a simple node without children to break the cycle
                var circularClone = CloningHelper.CreateShallowNodeClone(original);
                circularClone.Comment = (original.Comment ?? string.Empty) + " [CIRCULAR REF]";
                return circularClone;
            }

            // Deep copy of node using shared helper
            var clone = CloningHelper.CreateShallowNodeClone(original);

            // Recursively clone all child pointers
            foreach (var ptr in original.Pointers)
            {
                // CRITICAL FIX: Null safety - skip if ptr.Node is null
                if (ptr.Node == null)
                {
                    UnifiedLogger.LogApplication(LogLevel.WARN, $"Skipping null pointer during clone of '{original.DisplayText}'");
                    continue;
                }

                var clonedChild = CloneNodeWithDepth(ptr.Node, dialog, depth + 1, visited);

                // Add cloned child to dialog lists using AddNodeInternal to update LinkRegistry
                dialog.AddNodeInternal(clonedChild, clonedChild.Type);

                // Get the correct index after adding (LinkRegistry will track this)
                var nodeIndex = (uint)dialog.GetNodeIndex(clonedChild, clonedChild.Type);

                // Create pointer to cloned child
                var clonedPtr = new DialogPtr
                {
                    Node = clonedChild,
                    Type = clonedChild.Type,
                    Index = nodeIndex,
                    IsLink = ptr.IsLink,
                    ScriptAppears = ptr.ScriptAppears,
                    ConditionParams = new Dictionary<string, string>(ptr.ConditionParams ?? new Dictionary<string, string>()),
                    Comment = ptr.Comment,
                    LinkComment = ptr.LinkComment,
                    Parent = dialog
                };

                clone.Pointers.Add(clonedPtr);

                // Register the new pointer with LinkRegistry
                dialog.LinkRegistry.RegisterLink(clonedPtr);
            }

            return clone;
        }

    }
}
