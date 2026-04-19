using System.Collections.Generic;
using DialogEditor.Models;

namespace DialogEditor.Services
{
    /// <summary>
    /// Classification of a drag-drop target.
    /// </summary>
    public enum DropTargetKind
    {
        /// <summary>Dropped onto another node — source becomes a child of target.</summary>
        Node,

        /// <summary>Dropped onto the empty background — source becomes a new root/start point.</summary>
        Root,

        /// <summary>Invalid target (e.g., dropped on self).</summary>
        Invalid
    }

    /// <summary>
    /// Result of validating a drag-drop reparent operation.
    /// </summary>
    public class DropValidationResult
    {
        public bool IsValid { get; init; }
        public DropTargetKind TargetKind { get; init; }
        public string? RejectReason { get; init; }
    }

    /// <summary>
    /// Shared drag-drop validator for "drop as child of target" (Into) and
    /// "drop to root" (target == null) scenarios.
    ///
    /// Both TreeView and FlowView delegate their parent-assignment validation
    /// here so the two views can never disagree about which drops are legal (#2060).
    /// Sibling-reorder (Before / After) remains in each view's own path pending
    /// full unification (#2109).
    /// </summary>
    public static class DialogDragDropValidator
    {
        /// <summary>
        /// Validates a reparent operation.
        /// </summary>
        /// <param name="source">The node being dragged.</param>
        /// <param name="target">The node being dropped onto, or null for drop-to-root.</param>
        /// <param name="dialog">The dialog that owns both nodes.</param>
        public static DropValidationResult ValidateReparent(DialogNode source, DialogNode? target, Dialog dialog)
        {
            // Drop onto empty background → root placement
            if (target == null)
            {
                if (source.Type != DialogNodeType.Entry)
                {
                    return new DropValidationResult
                    {
                        IsValid = false,
                        TargetKind = DropTargetKind.Root,
                        RejectReason = "Only NPC Entry nodes can be at root level"
                    };
                }
                return new DropValidationResult
                {
                    IsValid = true,
                    TargetKind = DropTargetKind.Root
                };
            }

            // Drop on self
            if (source == target)
            {
                return new DropValidationResult
                {
                    IsValid = false,
                    TargetKind = DropTargetKind.Invalid,
                    RejectReason = "Cannot drop node on itself"
                };
            }

            // Circular reference: target must not be reachable from source
            if (IsDescendant(source, target))
            {
                return new DropValidationResult
                {
                    IsValid = false,
                    TargetKind = DropTargetKind.Node,
                    RejectReason = "Cannot drop node on its own descendant"
                };
            }

            // NPC/PC alternation — Entry parent may only have Reply children and vice versa
            if (target.Type == DialogNodeType.Entry && source.Type != DialogNodeType.Reply)
            {
                return new DropValidationResult
                {
                    IsValid = false,
                    TargetKind = DropTargetKind.Node,
                    RejectReason = "NPC Entry nodes can only have PC Reply children"
                };
            }
            if (target.Type == DialogNodeType.Reply && source.Type != DialogNodeType.Entry)
            {
                return new DropValidationResult
                {
                    IsValid = false,
                    TargetKind = DropTargetKind.Node,
                    RejectReason = "PC Reply nodes can only have NPC Entry children"
                };
            }

            return new DropValidationResult
            {
                IsValid = true,
                TargetKind = DropTargetKind.Node
            };
        }

        /// <summary>
        /// True if <paramref name="possibleDescendant"/> is reachable from
        /// <paramref name="source"/> via its pointer graph.
        /// </summary>
        private static bool IsDescendant(DialogNode source, DialogNode possibleDescendant)
        {
            var visited = new HashSet<DialogNode>();
            return IsReachable(source, possibleDescendant, visited);
        }

        private static bool IsReachable(DialogNode current, DialogNode target, HashSet<DialogNode> visited)
        {
            if (current == target) return true;
            if (!visited.Add(current)) return false;

            foreach (var ptr in current.Pointers)
            {
                if (ptr.Node != null && IsReachable(ptr.Node, target, visited))
                    return true;
            }
            return false;
        }
    }
}
