using System;
using System.Collections.Generic;
using System.Linq;
using DialogEditor.Models;
using DialogEditor.Services;
using Radoub.Formats.Logging;

namespace Parley.Models
{
    /// <summary>
    /// Stores dialog state along with tree UI state for undo/redo
    /// </summary>
    public class UndoState
    {
        public Dialog Dialog { get; set; } = null!;
        public string? SelectedNodePath { get; set; }
        public HashSet<string> ExpandedNodePaths { get; set; } = new();
    }

    /// <summary>
    /// Manages undo/redo operations for dialog editing
    /// Uses memento pattern to store dialog state snapshots
    /// Issue #252: Now also stores tree UI state (selection, expansion)
    /// </summary>
    public class UndoManager
    {
        // LinkedList instead of Stack so that trimming the oldest item when the stack is
        // over capacity is O(1) (RemoveFirst) instead of O(N) array copy (#2260).
        // Newest item is at the tail (Last); oldest is at the head (First).
        private readonly LinkedList<UndoState> _undoStack = new();
        private readonly LinkedList<UndoState> _redoStack = new();
        private readonly int _maxStackSize;
        private bool _isRestoring = false;

        public UndoManager(int maxStackSize = 50)
        {
            _maxStackSize = maxStackSize;
        }

        /// <summary>
        /// Gets whether undo is currently available
        /// </summary>
        public bool CanUndo => _undoStack.Count > 0;

        /// <summary>
        /// Gets whether redo is currently available
        /// </summary>
        public bool CanRedo => _redoStack.Count > 0;

        /// <summary>
        /// Gets whether we're currently restoring from undo/redo (to prevent recursive saves)
        /// </summary>
        public bool IsRestoring => _isRestoring;

        /// <summary>
        /// Sets the restoring state. Used to extend restoring period during async operations.
        /// </summary>
        public void SetRestoring(bool value)
        {
            _isRestoring = value;
        }

        /// <summary>
        /// Saves the current dialog state for undo
        /// Issue #252: Now also saves tree UI state (selection, expansion)
        /// </summary>
        public void SaveState(Dialog dialog, string description = "", string? selectedNodePath = null, HashSet<string>? expandedNodePaths = null)
        {
            if (_isRestoring) return; // Don't save while restoring

            try
            {
                // Deep clone the dialog
                var clone = DeepCloneDialog(dialog);

                // Create undo state with dialog and tree state
                var undoState = new UndoState
                {
                    Dialog = clone,
                    SelectedNodePath = selectedNodePath,
                    ExpandedNodePaths = expandedNodePaths != null ? new HashSet<string>(expandedNodePaths) : new HashSet<string>()
                };

                // Add newest state to the tail
                _undoStack.AddLast(undoState);

                // Clear redo stack when new action is performed
                _redoStack.Clear();

                // Limit stack size to prevent excessive memory usage.
                // O(1) per trim: drop the oldest (head) item.
                while (_undoStack.Count > _maxStackSize)
                {
                    _undoStack.RemoveFirst();
                }

                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Saved undo state: {description} (Stack size: {_undoStack.Count})");
            }
            catch (Exception ex)
            {
                // Failed to save undo state
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to save undo state: {ex.Message}");
            }
        }

        /// <summary>
        /// Restores the previous dialog state
        /// Issue #252: Returns UndoState which includes saved tree state
        /// </summary>
        public UndoState? Undo(Dialog currentDialog, string? currentSelectedPath = null, HashSet<string>? currentExpandedPaths = null)
        {
            if (!CanUndo) return null;

            try
            {
                _isRestoring = true;

                // Save current state to redo stack (with current tree state)
                var currentClone = DeepCloneDialog(currentDialog);
                var currentState = new UndoState
                {
                    Dialog = currentClone,
                    SelectedNodePath = currentSelectedPath,
                    ExpandedNodePaths = currentExpandedPaths != null ? new HashSet<string>(currentExpandedPaths) : new HashSet<string>()
                };
                _redoStack.AddLast(currentState);

                // Pop and restore previous state (newest = tail)
                var previousState = _undoStack.Last!.Value;
                _undoStack.RemoveLast();

                UnifiedLogger.LogApplication(LogLevel.INFO, $"Undo performed (Remaining: {_undoStack.Count}, RestoreSelection: {previousState.SelectedNodePath ?? "none"})");
                return previousState;
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to undo: {ex.Message}");
                return null;
            }
            finally
            {
                _isRestoring = false;
            }
        }

        /// <summary>
        /// Restores the next dialog state
        /// Issue #252: Returns UndoState which includes saved tree state
        /// </summary>
        public UndoState? Redo(Dialog currentDialog, string? currentSelectedPath = null, HashSet<string>? currentExpandedPaths = null)
        {
            if (!CanRedo) return null;

            try
            {
                _isRestoring = true;

                // Save current state to undo stack (with current tree state)
                var currentClone = DeepCloneDialog(currentDialog);
                var currentState = new UndoState
                {
                    Dialog = currentClone,
                    SelectedNodePath = currentSelectedPath,
                    ExpandedNodePaths = currentExpandedPaths != null ? new HashSet<string>(currentExpandedPaths) : new HashSet<string>()
                };
                _undoStack.AddLast(currentState);

                // Pop and restore next state (newest = tail)
                var nextState = _redoStack.Last!.Value;
                _redoStack.RemoveLast();

                UnifiedLogger.LogApplication(LogLevel.INFO, $"Redo performed (Remaining: {_redoStack.Count}, RestoreSelection: {nextState.SelectedNodePath ?? "none"})");
                return nextState;
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to redo: {ex.Message}");
                return null;
            }
            finally
            {
                _isRestoring = false;
            }
        }

        /// <summary>
        /// Deep clones a dialog and all its nodes
        /// </summary>
        private Dialog DeepCloneDialog(Dialog original)
        {
            var clone = new Dialog();

            // Copy simple properties
            clone.DelayEntry = original.DelayEntry;
            clone.DelayReply = original.DelayReply;
            clone.NumWords = original.NumWords;
            clone.ScriptAbort = original.ScriptAbort;
            clone.ScriptEnd = original.ScriptEnd;
            clone.PreventZoom = original.PreventZoom;

            // Track cloned nodes to handle circular references
            var nodeMap = new Dictionary<DialogNode, DialogNode>();

            // First pass: Clone all nodes without pointers
            foreach (var entry in original.Entries)
            {
                var clonedEntry = CloneNodeWithoutPointers(entry, clone);
                clone.Entries.Add(clonedEntry);
                nodeMap[entry] = clonedEntry;
            }

            foreach (var reply in original.Replies)
            {
                var clonedReply = CloneNodeWithoutPointers(reply, clone);
                clone.Replies.Add(clonedReply);
                nodeMap[reply] = clonedReply;
            }

            // Second pass: Clone pointers with correct references
            for (int i = 0; i < original.Entries.Count; i++)
            {
                ClonePointers(original.Entries[i], clone.Entries[i], nodeMap, clone);
            }

            for (int i = 0; i < original.Replies.Count; i++)
            {
                ClonePointers(original.Replies[i], clone.Replies[i], nodeMap, clone);
            }

            // Clone start pointers
            foreach (var start in original.Starts)
            {
                var clonedStart = new DialogPtr
                {
                    Parent = clone,
                    Node = start.Node != null && nodeMap.ContainsKey(start.Node) ? nodeMap[start.Node] : null,
                    Type = start.Type,
                    Index = start.Index,
                    IsLink = start.IsLink,
                    IsStart = start.IsStart,
                    ScriptAppears = start.ScriptAppears,
                    ConditionParams = new Dictionary<string, string>(start.ConditionParams ?? new Dictionary<string, string>()),
                    Comment = start.Comment,
                    LinkComment = start.LinkComment
                };
                clone.Starts.Add(clonedStart);
            }

            return clone;
        }

        private DialogNode CloneNodeWithoutPointers(DialogNode original, Dialog parentDialog)
        {
            return new DialogNode
            {
                Parent = parentDialog,
                Type = original.Type,
                Text = CloneLocString(original.Text),
                Speaker = original.Speaker,
                Comment = original.Comment,
                Sound = original.Sound,
                ScriptAction = original.ScriptAction,
                Animation = original.Animation,
                AnimationLoop = original.AnimationLoop,
                Delay = original.Delay,
                Quest = original.Quest,
                QuestEntry = original.QuestEntry,
                Pointers = new List<DialogPtr>(), // Empty, will be filled in second pass
                ActionParams = new Dictionary<string, string>(original.ActionParams ?? new Dictionary<string, string>())
            };
        }

        private void ClonePointers(DialogNode original, DialogNode clone, Dictionary<DialogNode, DialogNode> nodeMap, Dialog parentDialog)
        {
            foreach (var ptr in original.Pointers)
            {
                var clonedPtr = new DialogPtr
                {
                    Parent = parentDialog,
                    Node = ptr.Node != null && nodeMap.ContainsKey(ptr.Node) ? nodeMap[ptr.Node] : null,
                    Type = ptr.Type,
                    Index = ptr.Index,
                    IsLink = ptr.IsLink,
                    IsStart = ptr.IsStart,
                    ScriptAppears = ptr.ScriptAppears,
                    ConditionParams = new Dictionary<string, string>(ptr.ConditionParams ?? new Dictionary<string, string>()),
                    Comment = ptr.Comment,
                    LinkComment = ptr.LinkComment
                };
                clone.Pointers.Add(clonedPtr);
            }
        }

        private LocString CloneLocString(LocString? original)
        {
            if (original == null) return new LocString();

            var clone = new LocString();

            // Copy all strings using the Add method
            foreach (var kvp in original.GetAllStrings())
            {
                clone.Add(kvp.Key, kvp.Value);
            }
            return clone;
        }

        /// <summary>
        /// Discards the most recently saved undo state (#2260).
        /// Used to revert undo state when an operation rolls back after calling SaveState.
        /// Returns true if a state was discarded.
        /// </summary>
        public bool DiscardLastSavedState()
        {
            if (_undoStack.Count == 0) return false;
            _undoStack.RemoveLast();
            return true;
        }

        /// <summary>
        /// Clears all undo/redo history
        /// </summary>
        public void Clear()
        {
            _undoStack.Clear();
            _redoStack.Clear();
            UnifiedLogger.LogApplication(LogLevel.DEBUG, "Undo/redo history cleared");
        }

        /// <summary>
        /// Gets status information for UI display
        /// </summary>
        public string GetStatus()
        {
            return $"Undo: {_undoStack.Count} | Redo: {_redoStack.Count}";
        }
    }
}