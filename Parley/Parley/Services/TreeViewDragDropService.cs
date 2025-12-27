using System;
using Radoub.Formats.Logging;
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
        /// <summary>
        /// The actual DialogNode for the new parent (used when TreeViewSafeNode lookup fails).
        /// </summary>
        public DialogNode? NewParentDialogNode { get; set; }
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
        public event Action<TreeViewSafeNode, DialogNode?, DropPosition, int>? DropCompleted;

        /// <summary>
        /// Event fired when drop position changes (for visual feedback).
        /// </summary>
        public event Action<DragDropValidation?>? DropPositionChanged;

        /// <summary>
        /// Notifies listeners of drop position changes.
        /// </summary>
        public void NotifyDropPositionChanged(DragDropValidation? validation)
        {
            DropPositionChanged?.Invoke(validation);
        }

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
                // Always reset before starting a new potential drag
                // This prevents stuck states from previous operations
                if (_draggedNode != null || _isDragging)
                {
                    UnifiedLogger.LogApplication(LogLevel.DEBUG,
                        $"TreeViewDragDrop.OnPointerPressed: Clearing previous state (was dragging={_isDragging}, had node={_draggedNode != null})");
                    Reset();
                }

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

                    // Use NewParentDialogNode directly (NewParent TreeViewSafeNode lookup often fails)
                    var newParent = validation.NewParent?.OriginalNode ?? validation.NewParentDialogNode;
                    DropCompleted?.Invoke(_draggedNode, newParent, position, validation.InsertIndex);
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
            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                $"TreeViewDragDrop.ValidateDrop: source='{source.DisplayText}' (Type={source.OriginalNode.Type}, IsChild={source.IsChild}), " +
                $"target='{target.DisplayText}' (Type={target.OriginalNode.Type}), position={position}");

            var result = new DragDropValidation
            {
                TargetNode = target,
                Position = position
            };

            // Can't drop on self
            if (source == target)
            {
                result.ErrorMessage = "Cannot drop node on itself";
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"TreeViewDragDrop.ValidateDrop: REJECTED - {result.ErrorMessage}");
                return result;
            }

            // Can't drop on descendant (would create circular reference)
            if (IsDescendantOf(target, source))
            {
                result.ErrorMessage = "Cannot drop node on its own descendant";
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"TreeViewDragDrop.ValidateDrop: REJECTED - {result.ErrorMessage}");
                return result;
            }

            // Can't drop link nodes (they're references, not actual nodes)
            if (source.IsChild)
            {
                result.ErrorMessage = "Cannot drag link nodes - drag the original node instead";
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"TreeViewDragDrop.ValidateDrop: REJECTED - {result.ErrorMessage}");
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
                    // "Into" means dropping directly onto a node to become its child
                    if (sourceNode.Type == targetNode.Type)
                    {
                        // Same type = can't be parent/child (NPC Entry can't have NPC Entry children)
                        // Reject - use Before/After for sibling reordering
                        result.ErrorMessage = "Use Before/After to reorder siblings - drop onto parent to add as child";
                        UnifiedLogger.LogApplication(LogLevel.DEBUG,
                            $"TreeViewDragDrop.ValidateDrop: REJECTED - same type Into not allowed");
                        return result;
                    }
                    else
                    {
                        // Different type = reparenting (making source a child of target)
                        // This is the intuitive case: drop Reply onto Entry = add Reply as last child
                        newParent = targetNode;
                        // Insert at end of children (matches Aurora Engine default behavior)
                        insertIndex = targetNode.Pointers?.Count ?? 0;
                        UnifiedLogger.LogApplication(LogLevel.DEBUG,
                            $"TreeViewDragDrop: Into (different type) -> adding as last child, " +
                            $"parent='{newParent.DisplayText}', insertIndex={insertIndex}");
                    }
                    break;

                case DropPosition.Before:
                case DropPosition.After:
                    // Before/After only works for same-type nodes (true siblings)
                    // Different types can't be siblings in Aurora dialog structure
                    if (sourceNode.Type != targetNode.Type)
                    {
                        // Different type Before/After is confusing - reject and guide user
                        result.ErrorMessage = "Drop onto the node to add as child (center zone)";
                        UnifiedLogger.LogApplication(LogLevel.DEBUG,
                            $"TreeViewDragDrop.ValidateDrop: REJECTED - different type Before/After not allowed");
                        return result;
                    }

                    // Same type = true siblings, use target's parent
                    var targetParent = GetParentNode(target);
                    newParent = targetParent;
                    insertIndex = GetSiblingIndex(target, newParent, position == DropPosition.After);
                    UnifiedLogger.LogApplication(LogLevel.DEBUG,
                        $"TreeViewDragDrop: Before/After (same type) - target='{target.DisplayText}', " +
                        $"parent='{newParent?.DisplayText ?? "NULL"}'");
                    break;
            }

            // Validate NPC/PC alternation rules
            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                $"TreeViewDragDrop: Validating source='{sourceNode.DisplayText}' (Type={sourceNode.Type}) " +
                $"under parent='{newParent?.DisplayText ?? "ROOT"}' (Type={newParent?.Type})");
            var validationError = ValidateNodePlacement(sourceNode, newParent);
            if (validationError != null)
            {
                result.ErrorMessage = validationError;
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"TreeViewDragDrop.ValidateDrop: REJECTED - {result.ErrorMessage}");
                return result;
            }

            // Find the TreeViewSafeNode for the new parent
            result.NewParent = newParent != null ? FindTreeViewNode(target, newParent) : null;
            // Also store the DialogNode directly in case TreeViewSafeNode lookup fails
            result.NewParentDialogNode = newParent;
            result.InsertIndex = insertIndex;
            result.IsValid = true;

            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                $"TreeViewDragDrop.ValidateDrop: ACCEPTED - NewParent={result.NewParent?.DisplayText ?? "NULL"}, " +
                $"NewParentDialogNode={result.NewParentDialogNode?.DisplayText ?? "ROOT"}, InsertIndex={insertIndex}");

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
        /// Returns null only if the node is at root level (child of ROOT container).
        /// </summary>
        private DialogNode? GetParentNode(TreeViewSafeNode node)
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                $"TreeViewDragDrop.GetParentNode: Finding parent for '{node.DisplayText}' (Type={node.OriginalNode.Type})");

            // TreeViewRootNode is the ROOT container - it has no parent
            if (node is TreeViewRootNode)
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG, "TreeViewDragDrop.GetParentNode: Node is TreeViewRootNode - returning null");
                return null;
            }

            // The source pointer tells us where this node came from
            var sourcePointer = node.SourcePointer;
            var dialog = node.OriginalNode.Parent;

            if (dialog == null)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN, "TreeViewDragDrop.GetParentNode: dialog is null - returning null");
                return null;
            }

            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                $"TreeViewDragDrop.GetParentNode: sourcePointer={sourcePointer?.Node?.DisplayText ?? "null"}, dialog has {dialog.Entries.Count} entries, {dialog.Replies.Count} replies");

            // Check if this node is a root-level entry (in Starts list)
            // Root-level entries have no parent DialogNode (they're children of ROOT container)
            foreach (var start in dialog.Starts)
            {
                if (start.Node == node.OriginalNode)
                {
                    // This is a root-level entry - no parent DialogNode
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, "TreeViewDragDrop.GetParentNode: Node is in Starts list - returning null (root level)");
                    return null;
                }
            }

            // For non-root nodes, find the parent by searching who has a pointer to this node
            if (sourcePointer != null)
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG, "TreeViewDragDrop.GetParentNode: Searching by sourcePointer reference...");
                // Search entries for the node containing this pointer
                foreach (var entry in dialog.Entries)
                {
                    if (entry.Pointers.Contains(sourcePointer))
                    {
                        UnifiedLogger.LogApplication(LogLevel.DEBUG, $"TreeViewDragDrop.GetParentNode: Found parent Entry '{entry.DisplayText}' by sourcePointer");
                        return entry;
                    }
                }
                // Search replies for the node containing this pointer
                foreach (var reply in dialog.Replies)
                {
                    if (reply.Pointers.Contains(sourcePointer))
                    {
                        UnifiedLogger.LogApplication(LogLevel.DEBUG, $"TreeViewDragDrop.GetParentNode: Found parent Reply '{reply.DisplayText}' by sourcePointer");
                        return reply;
                    }
                }
                UnifiedLogger.LogApplication(LogLevel.DEBUG, "TreeViewDragDrop.GetParentNode: sourcePointer not found in any node's Pointers list, trying fallback...");
            }

            // Fallback: Search all nodes for any pointer pointing to this node
            // This handles cases where sourcePointer doesn't match (e.g., recreated pointers)
            UnifiedLogger.LogApplication(LogLevel.DEBUG, "TreeViewDragDrop.GetParentNode: Fallback search by target node reference...");
            var targetNode = node.OriginalNode;
            foreach (var entry in dialog.Entries)
            {
                foreach (var ptr in entry.Pointers)
                {
                    if (ptr.Node == targetNode)
                    {
                        UnifiedLogger.LogApplication(LogLevel.DEBUG, $"TreeViewDragDrop.GetParentNode: Found parent Entry '{entry.DisplayText}' by fallback search");
                        return entry;
                    }
                }
            }
            foreach (var reply in dialog.Replies)
            {
                foreach (var ptr in reply.Pointers)
                {
                    if (ptr.Node == targetNode)
                    {
                        UnifiedLogger.LogApplication(LogLevel.DEBUG, $"TreeViewDragDrop.GetParentNode: Found parent Reply '{reply.DisplayText}' by fallback search");
                        return reply;
                    }
                }
            }

            // If we get here, the node is likely a root-level entry that wasn't in Starts
            // (shouldn't happen in valid dialogs, but handle gracefully)
            UnifiedLogger.LogApplication(LogLevel.WARN,
                $"TreeViewDragDrop.GetParentNode: Could not find parent for node '{node.DisplayText}' - treating as root level");
            return null;
        }

        /// <summary>
        /// Gets the DialogNode parent of a DialogNode (for grandparent lookups).
        /// Similar to GetParentNode but works directly with DialogNode.
        /// </summary>
        private DialogNode? GetParentNodeForDialogNode(DialogNode node)
        {
            var dialog = node.Parent;
            if (dialog == null)
                return null;

            // Check if this is a root-level entry
            foreach (var start in dialog.Starts)
            {
                if (start.Node == node)
                    return null; // Root level
            }

            // Search for parent by finding who has a pointer to this node
            foreach (var entry in dialog.Entries)
            {
                foreach (var ptr in entry.Pointers)
                {
                    if (ptr.Node == node)
                        return entry;
                }
            }
            foreach (var reply in dialog.Replies)
            {
                foreach (var ptr in reply.Pointers)
                {
                    if (ptr.Node == node)
                        return reply;
                }
            }

            return null;
        }

        /// <summary>
        /// Gets sibling index for a DialogNode relative to its parent.
        /// Used when we need to insert relative to a DialogNode (not TreeViewSafeNode).
        /// </summary>
        private int GetSiblingIndexForDialogNode(DialogNode? sibling, DialogNode? parent, bool insertAfter)
        {
            if (sibling == null)
                return 0;

            if (parent == null)
            {
                // Root level - index in Starts list
                var dialog = sibling.Parent;
                if (dialog != null)
                {
                    var index = dialog.Starts.FindIndex(s => s.Node == sibling);
                    return insertAfter ? index + 1 : Math.Max(0, index);
                }
                return 0;
            }

            // Find index in parent's pointers
            var pointerIndex = parent.Pointers.FindIndex(p => p.Node == sibling);
            return insertAfter ? pointerIndex + 1 : Math.Max(0, pointerIndex);
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
                    UnifiedLogger.LogApplication(LogLevel.DEBUG,
                        $"TreeViewDragDrop.GetSiblingIndex: ROOT level, target='{target.OriginalNode.DisplayText}', " +
                        $"index={index}, insertAfter={insertAfter}, result={( insertAfter ? index + 1 : index )}");
                    return insertAfter ? index + 1 : index;
                }
                UnifiedLogger.LogApplication(LogLevel.WARN,
                    $"TreeViewDragDrop.GetSiblingIndex: ROOT level but dialog is NULL for target='{target.OriginalNode.DisplayText}'");
                return 0;
            }

            // Find index in parent's pointers - use Node property which references the actual DialogNode
            var pointerIndex = parent.Pointers.FindIndex(p => p.Node == target.OriginalNode);
            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                $"TreeViewDragDrop.GetSiblingIndex: Parent='{parent.DisplayText}', target='{target.OriginalNode.DisplayText}', " +
                $"pointerIndex={pointerIndex}, insertAfter={insertAfter}, result={( insertAfter ? pointerIndex + 1 : Math.Max(0, pointerIndex) )}");

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
            // pointerPosition.Y is already relative to target (0 = top of item)
            // targetBounds should be 0,0-based when passed from MainWindow
            var relativeY = pointerPosition.Y;
            var itemHeight = targetBounds.Height;

            // Divide the item into 3 zones: top 20% = Before, middle 60% = Into, bottom 20% = After
            // Wider middle zone makes it easier to drop "onto" a node
            var topZone = itemHeight * 0.20;
            var bottomZone = itemHeight * 0.80;

            UnifiedLogger.LogApplication(LogLevel.TRACE,
                $"CalculateDropPosition: pointerY={pointerPosition.Y:F1}, height={itemHeight:F1}, " +
                $"topZone={topZone:F1}, bottomZone={bottomZone:F1}");

            if (relativeY < topZone)
                return DropPosition.Before;
            else if (relativeY > bottomZone)
                return DropPosition.After;
            else
                return DropPosition.Into;
        }
    }
}
