using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using DialogEditor.Models;

namespace DialogEditor.Services
{
    /// <summary>
    /// Defines where a drop will occur relative to the target node.
    /// </summary>
    public enum DropPosition
    {
        /// <summary>Drop is not valid at current position.</summary>
        None,

        /// <summary>Insert before the target node (as sibling above).</summary>
        Before,

        /// <summary>Insert after the target node (as sibling below).</summary>
        After,

        /// <summary>Insert as first child of the target node.</summary>
        Into
    }

    /// <summary>
    /// Result of a drag-drop operation validation.
    /// </summary>
    public class DragDropValidation
    {
        public bool IsValid { get; set; }
        public DropPosition Position { get; set; }
        public string? ErrorMessage { get; set; }
        public TreeViewSafeNode? TargetNode { get; set; }
        public TreeViewSafeNode? NewParent { get; set; }
        public int InsertIndex { get; set; }
    }

    /// <summary>
    /// Service for handling drag-and-drop operations in the dialog TreeView.
    /// Supports reordering (within same parent) and reparenting (to different parent).
    /// </summary>
    /// <remarks>
    /// Aurora Engine dialog rules enforced:
    /// - NPC Entry nodes can only have PC Reply children
    /// - PC Reply nodes can only have NPC Entry children
    /// - Only NPC Entry nodes can be at root level (in Starts list)
    /// </remarks>
    public class TreeViewDragDropService
    {
        private TreeViewSafeNode? _draggedNode;
        private Point _dragStartPoint;
        private bool _isDragging;
        private const double DragThreshold = 5.0; // Pixels to move before drag starts

        /// <summary>
        /// Event fired when a valid drop operation completes.
        /// </summary>
        public event Action<TreeViewSafeNode, TreeViewSafeNode?, DropPosition, int>? DropCompleted;

        /// <summary>
        /// Event fired when drop position changes (for visual feedback).
        /// </summary>
        public event Action<DragDropValidation?>? DropPositionChanged;

        /// <summary>
        /// The node currently being dragged, if any.
        /// </summary>
        public TreeViewSafeNode? DraggedNode => _draggedNode;

        /// <summary>
        /// Whether a drag operation is in progress.
        /// </summary>
        public bool IsDragging => _isDragging;

        /// <summary>
        /// Called when pointer is pressed on a tree item.
        /// Records the start point for potential drag.
        /// </summary>
        public void OnPointerPressed(TreeViewSafeNode node, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(null).Properties.IsLeftButtonPressed)
            {
                _draggedNode = node;
                _dragStartPoint = e.GetPosition(null);
                _isDragging = false;
            }
        }

        /// <summary>
        /// Called when pointer moves. Initiates drag if threshold exceeded.
        /// </summary>
        public bool OnPointerMoved(PointerEventArgs e, Visual? relativeTo)
        {
            if (_draggedNode == null || _isDragging)
                return _isDragging;

            var currentPoint = e.GetPosition(null);
            var distance = Math.Sqrt(
                Math.Pow(currentPoint.X - _dragStartPoint.X, 2) +
                Math.Pow(currentPoint.Y - _dragStartPoint.Y, 2));

            if (distance > DragThreshold)
            {
                _isDragging = true;
                UnifiedLogger.LogApplication(LogLevel.DEBUG,
                    $"TreeViewDragDrop: Started dragging '{_draggedNode.DisplayText}'");
                return true;
            }

            return false;
        }

        /// <summary>
        /// Called when pointer is released. Completes or cancels drag.
        /// </summary>
        public void OnPointerReleased(PointerReleasedEventArgs e, TreeViewSafeNode? targetNode, DropPosition position)
        {
            if (!_isDragging || _draggedNode == null)
            {
                Reset();
                return;
            }

            if (targetNode != null && position != DropPosition.None)
            {
                var validation = ValidateDrop(_draggedNode, targetNode, position);
                if (validation.IsValid)
                {
                    UnifiedLogger.LogApplication(LogLevel.INFO,
                        $"TreeViewDragDrop: Dropping '{_draggedNode.DisplayText}' {position} '{targetNode.DisplayText}'");

                    DropCompleted?.Invoke(_draggedNode, validation.NewParent, position, validation.InsertIndex);
                }
                else
                {
                    UnifiedLogger.LogApplication(LogLevel.DEBUG,
                        $"TreeViewDragDrop: Invalid drop - {validation.ErrorMessage}");
                }
            }

            Reset();
        }

        /// <summary>
        /// Cancels the current drag operation.
        /// </summary>
        public void Reset()
        {
            if (_isDragging)
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG, "TreeViewDragDrop: Drag cancelled/reset");
            }
            _draggedNode = null;
            _isDragging = false;
            DropPositionChanged?.Invoke(null);
        }

        /// <summary>
        /// Validates whether a drop is allowed at the given position.
        /// </summary>
        public DragDropValidation ValidateDrop(TreeViewSafeNode source, TreeViewSafeNode target, DropPosition position)
        {
            var result = new DragDropValidation
            {
                TargetNode = target,
                Position = position
            };

            // Can't drop on self
            if (source == target)
            {
                result.ErrorMessage = "Cannot drop node on itself";
                return result;
            }

            // Can't drop on descendant (would create circular reference)
            if (IsDescendantOf(target, source))
            {
                result.ErrorMessage = "Cannot drop node on its own descendant";
                return result;
            }

            // Can't drop link nodes (they're references, not actual nodes)
            if (source.IsChild)
            {
                result.ErrorMessage = "Cannot drag link nodes - drag the original node instead";
                return result;
            }

            var sourceNode = source.OriginalNode;
            var targetNode = target.OriginalNode;

            // Determine the new parent based on drop position
            DialogNode? newParent = null;
            int insertIndex = 0;

            switch (position)
            {
                case DropPosition.Into:
                    newParent = targetNode;
                    insertIndex = 0; // Insert as first child
                    break;

                case DropPosition.Before:
                case DropPosition.After:
                    // Get target's parent - for Before/After, we're inserting as sibling
                    newParent = GetParentNode(target);
                    insertIndex = GetSiblingIndex(target, newParent, position == DropPosition.After);
                    break;
            }

            // Validate NPC/PC alternation rules
            var validationError = ValidateNodePlacement(sourceNode, newParent);
            if (validationError != null)
            {
                result.ErrorMessage = validationError;
                return result;
            }

            // Find the TreeViewSafeNode for the new parent
            result.NewParent = newParent != null ? FindTreeViewNode(target, newParent) : null;
            result.InsertIndex = insertIndex;
            result.IsValid = true;

            return result;
        }

        /// <summary>
        /// Validates NPC/PC alternation rules for node placement.
        /// </summary>
        private string? ValidateNodePlacement(DialogNode source, DialogNode? newParent)
        {
            if (newParent == null)
            {
                // Dropping at root level - only NPC Entries allowed
                if (source.Type != DialogNodeType.Entry)
                {
                    return "Only NPC Entry nodes can be at root level";
                }
                return null;
            }

            // Check parent-child type compatibility
            if (newParent.Type == DialogNodeType.Entry)
            {
                // NPC Entry parent can only have PC Reply children
                if (source.Type != DialogNodeType.Reply)
                {
                    return "NPC Entry nodes can only have PC Reply children";
                }
            }
            else // Reply
            {
                // PC Reply parent can only have NPC Entry children
                if (source.Type != DialogNodeType.Entry)
                {
                    return "PC Reply nodes can only have NPC Entry children";
                }
            }

            return null;
        }

        /// <summary>
        /// Checks if target is a descendant of source (would create circular reference).
        /// </summary>
        private bool IsDescendantOf(TreeViewSafeNode target, TreeViewSafeNode source)
        {
            var current = target;
            while (current != null)
            {
                if (current.OriginalNode == source.OriginalNode)
                    return true;

                // Walk up to parent - need to find parent in tree
                current = FindParentTreeNode(current);
            }
            return false;
        }

        /// <summary>
        /// Gets the DialogNode parent of a TreeViewSafeNode.
        /// </summary>
        private DialogNode? GetParentNode(TreeViewSafeNode node)
        {
            // For root nodes, parent is null
            if (node is TreeViewRootNode)
                return null;

            // The source pointer tells us where this node came from
            var sourcePointer = node.SourcePointer;
            if (sourcePointer != null)
            {
                // The pointer's parent is in the dialog structure
                // We need to find which node contains this pointer
                var dialog = node.OriginalNode.Parent;
                if (dialog != null)
                {
                    // Search entries and replies for the node containing this pointer
                    foreach (var entry in dialog.Entries)
                    {
                        if (entry.Pointers.Contains(sourcePointer))
                            return entry;
                    }
                    foreach (var reply in dialog.Replies)
                    {
                        if (reply.Pointers.Contains(sourcePointer))
                            return reply;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Gets the index where a node should be inserted relative to target.
        /// </summary>
        private int GetSiblingIndex(TreeViewSafeNode target, DialogNode? parent, bool insertAfter)
        {
            if (parent == null)
            {
                // Root level - index in Starts list
                var dialog = target.OriginalNode.Parent;
                if (dialog != null)
                {
                    var index = dialog.Starts.FindIndex(s => s.Node == target.OriginalNode);
                    return insertAfter ? index + 1 : index;
                }
                return 0;
            }

            // Find index in parent's pointers - use Node property which references the actual DialogNode
            var pointerIndex = parent.Pointers.FindIndex(p => p.Node == target.OriginalNode);

            return insertAfter ? pointerIndex + 1 : Math.Max(0, pointerIndex);
        }

        /// <summary>
        /// Finds the parent TreeViewSafeNode for a given node.
        /// </summary>
        private TreeViewSafeNode? FindParentTreeNode(TreeViewSafeNode node)
        {
            // This would need access to the tree structure
            // For now, return null - full implementation requires tree traversal
            return null;
        }

        /// <summary>
        /// Finds a TreeViewSafeNode by its underlying DialogNode.
        /// </summary>
        private TreeViewSafeNode? FindTreeViewNode(TreeViewSafeNode searchRoot, DialogNode target)
        {
            if (searchRoot.OriginalNode == target)
                return searchRoot;

            var children = searchRoot.Children;
            if (children != null)
            {
                foreach (var child in children)
                {
                    var found = FindTreeViewNode(child, target);
                    if (found != null)
                        return found;
                }
            }

            return null;
        }

        /// <summary>
        /// Calculates drop position based on pointer Y position within the target item.
        /// </summary>
        public DropPosition CalculateDropPosition(Point pointerPosition, Rect targetBounds)
        {
            var relativeY = pointerPosition.Y - targetBounds.Y;
            var itemHeight = targetBounds.Height;

            // Divide the item into 3 zones: top 25% = Before, middle 50% = Into, bottom 25% = After
            var topZone = itemHeight * 0.25;
            var bottomZone = itemHeight * 0.75;

            if (relativeY < topZone)
                return DropPosition.Before;
            else if (relativeY > bottomZone)
                return DropPosition.After;
            else
                return DropPosition.Into;
        }
    }
}
