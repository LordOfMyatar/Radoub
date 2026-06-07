using System.Collections.Generic;
using System.Linq;
using DialogEditor.Models;

namespace DialogEditor.Services
{
    /// <summary>
    /// Result of resolving a sibling-reorder between two nodes that share a parent (#2109).
    /// </summary>
    public readonly struct SiblingReorderResult
    {
        public bool Found { get; init; }
        /// <summary>The shared parent node, or null when both siblings are root start points.</summary>
        public DialogNode? Parent { get; init; }
        public int FromIndex { get; init; }
        public int ToIndex { get; init; }

        public static SiblingReorderResult NotFound => new() { Found = false, FromIndex = -1, ToIndex = -1 };
    }

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
    /// Both TreeView and FlowView delegate their drag-drop logic here so the two
    /// views can never disagree about which drops are legal or how reorders resolve.
    /// The validator owns reparent validation (Into / Root), circular-reference
    /// detection, NPC/PC placement rules, parent resolution, sibling detection, and
    /// sibling-reorder index math (#2060, #2109). Views are thin adapters that map
    /// their own node wrappers onto the shared DialogNode-level API.
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
        /// Resolves the parent DialogNode of <paramref name="node"/>, or null when the
        /// node is a root-level start point. Shared by both views (#2109) — replaces
        /// TreeView's GetParentNodeForDialogNode and FlowView's inline parent search.
        /// </summary>
        public static DialogNode? ResolveParent(DialogNode node, Dialog dialog)
        {
            // Root-level start points have no parent node.
            if (dialog.Starts.Any(s => s.Node == node))
                return null;

            foreach (var entry in dialog.Entries)
            {
                if (entry.Pointers.Any(p => p.Node == node))
                    return entry;
            }
            foreach (var reply in dialog.Replies)
            {
                if (reply.Pointers.Any(p => p.Node == node))
                    return reply;
            }

            return null;
        }

        /// <summary>
        /// True if <paramref name="a"/> and <paramref name="b"/> share a parent — both
        /// root start points, or both pointed to by the same node. Shared by both views
        /// (#2109) — replaces FlowView's AreSiblings.
        /// </summary>
        public static bool AreSiblings(DialogNode a, DialogNode b, Dialog dialog)
        {
            if (a == b) return false;

            bool aIsRoot = dialog.Starts.Any(s => s.Node == a);
            bool bIsRoot = dialog.Starts.Any(s => s.Node == b);
            if (aIsRoot || bIsRoot)
                return aIsRoot && bIsRoot;

            foreach (var parent in dialog.Entries.Concat(dialog.Replies))
            {
                bool hasA = parent.Pointers.Any(p => p.Node == a);
                bool hasB = parent.Pointers.Any(p => p.Node == b);
                if (hasA && hasB)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Resolves the shared parent and the from/to indices for reordering
        /// <paramref name="dragged"/> relative to <paramref name="target"/>. Returns
        /// <see cref="SiblingReorderResult.NotFound"/> when the two are not siblings.
        /// Shared by both views (#2109) — replaces the index math duplicated in
        /// TreeView's GetSiblingIndexForDialogNode and FlowView's ExecuteSiblingReorder.
        /// </summary>
        public static SiblingReorderResult ResolveSiblingReorder(DialogNode dragged, DialogNode target, Dialog dialog)
        {
            // Root level: both must be in Starts.
            int draggedStart = dialog.Starts.FindIndex(s => s.Node == dragged);
            int targetStart = dialog.Starts.FindIndex(s => s.Node == target);
            if (draggedStart >= 0 && targetStart >= 0)
            {
                return new SiblingReorderResult
                {
                    Found = true,
                    Parent = null,
                    FromIndex = draggedStart,
                    ToIndex = targetStart
                };
            }
            if (draggedStart >= 0 || targetStart >= 0)
                return SiblingReorderResult.NotFound;

            // Child level: find a parent that points to both.
            foreach (var parent in dialog.Entries.Concat(dialog.Replies))
            {
                int from = parent.Pointers.FindIndex(p => p.Node == dragged);
                int to = parent.Pointers.FindIndex(p => p.Node == target);
                if (from >= 0 && to >= 0)
                {
                    return new SiblingReorderResult
                    {
                        Found = true,
                        Parent = parent,
                        FromIndex = from,
                        ToIndex = to
                    };
                }
            }

            return SiblingReorderResult.NotFound;
        }

        /// <summary>
        /// Index at which a node should be inserted relative to <paramref name="target"/>
        /// among its siblings. <paramref name="parent"/> null means root level (Starts list).
        /// Shared by both views (#2109) — replaces TreeView's GetSiblingIndex math.
        /// </summary>
        public static int GetSiblingInsertIndex(DialogNode target, DialogNode? parent, Dialog dialog, bool insertAfter)
        {
            int index = parent == null
                ? dialog.Starts.FindIndex(s => s.Node == target)
                : parent.Pointers.FindIndex(p => p.Node == target);

            return insertAfter ? index + 1 : System.Math.Max(0, index);
        }

        /// <summary>
        /// True if <paramref name="possibleDescendant"/> is reachable from
        /// <paramref name="source"/> via its pointer graph. Public so both views can
        /// run the same circular-reference check (#2109).
        /// </summary>
        public static bool IsDescendant(DialogNode source, DialogNode possibleDescendant)
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
