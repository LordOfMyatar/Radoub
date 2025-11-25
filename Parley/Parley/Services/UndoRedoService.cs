using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using DialogEditor.Models;
using DialogEditor.Services;
using Parley.Models;

namespace DialogEditor.Services
{
    /// <summary>
    /// Service responsible for undo/redo operations with tree state management.
    /// Extracted from MainViewModel to improve separation of concerns.
    /// Wraps UndoManager with additional UI state handling.
    /// </summary>
    public class UndoRedoService
    {
        private readonly UndoManager _undoManager;

        public UndoRedoService(int maxStackSize = 50)
        {
            _undoManager = new UndoManager(maxStackSize);
        }

        /// <summary>
        /// Gets whether undo is currently available
        /// </summary>
        public bool CanUndo => _undoManager.CanUndo;

        /// <summary>
        /// Gets whether redo is currently available
        /// </summary>
        public bool CanRedo => _undoManager.CanRedo;

        /// <summary>
        /// Gets whether we're currently restoring from undo/redo (to prevent recursive saves)
        /// </summary>
        public bool IsRestoring => _undoManager.IsRestoring;

        /// <summary>
        /// Saves the current dialog state for undo
        /// </summary>
        public void SaveState(Dialog dialog, string description = "")
        {
            if (dialog == null) return;
            _undoManager.SaveState(dialog, description);
        }

        /// <summary>
        /// Performs undo operation with tree state restoration
        /// </summary>
        public UndoRedoResult Undo(Dialog currentDialog, TreeState treeState)
        {
            if (currentDialog == null || !_undoManager.CanUndo)
            {
                return new UndoRedoResult
                {
                    Success = false,
                    StatusMessage = "Nothing to undo",
                    RestoredDialog = null,
                    TreeState = treeState
                };
            }

            var previousState = _undoManager.Undo(currentDialog);
            if (previousState != null)
            {
                UnifiedLogger.LogApplication(LogLevel.INFO, "Undo successful");
                return new UndoRedoResult
                {
                    Success = true,
                    StatusMessage = "Undo successful",
                    RestoredDialog = previousState,
                    TreeState = treeState
                };
            }
            else
            {
                return new UndoRedoResult
                {
                    Success = false,
                    StatusMessage = "Undo failed",
                    RestoredDialog = null,
                    TreeState = treeState
                };
            }
        }

        /// <summary>
        /// Performs redo operation with tree state restoration
        /// </summary>
        public UndoRedoResult Redo(Dialog currentDialog, TreeState treeState)
        {
            if (currentDialog == null || !_undoManager.CanRedo)
            {
                return new UndoRedoResult
                {
                    Success = false,
                    StatusMessage = "Nothing to redo",
                    RestoredDialog = null,
                    TreeState = treeState
                };
            }

            var nextState = _undoManager.Redo(currentDialog);
            if (nextState != null)
            {
                UnifiedLogger.LogApplication(LogLevel.INFO, "Redo successful");
                return new UndoRedoResult
                {
                    Success = true,
                    StatusMessage = "Redo successful",
                    RestoredDialog = nextState,
                    TreeState = treeState
                };
            }
            else
            {
                return new UndoRedoResult
                {
                    Success = false,
                    StatusMessage = "Redo failed",
                    RestoredDialog = null,
                    TreeState = treeState
                };
            }
        }

        /// <summary>
        /// Clears all undo/redo history
        /// </summary>
        public void Clear()
        {
            _undoManager.Clear();
        }

        /// <summary>
        /// Gets status information for UI display
        /// </summary>
        public string GetStatus()
        {
            return _undoManager.GetStatus();
        }
    }

    /// <summary>
    /// Result of an undo/redo operation including dialog state and tree state
    /// </summary>
    public class UndoRedoResult
    {
        public bool Success { get; set; }
        public string StatusMessage { get; set; } = string.Empty;
        public Dialog? RestoredDialog { get; set; }
        public TreeState TreeState { get; set; } = new TreeState();
    }

    /// <summary>
    /// Captures UI tree state for restoration after undo/redo
    /// </summary>
    public class TreeState
    {
        public HashSet<string> ExpandedNodePaths { get; set; } = new HashSet<string>();
        public string? SelectedNodePath { get; set; }
    }
}
