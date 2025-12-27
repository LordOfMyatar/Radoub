using System;
using Avalonia.Threading;
using DialogEditor.Models;
using DialogEditor.Services;
using Radoub.Formats.Logging;

namespace DialogEditor.ViewModels
{
    /// <summary>
    /// MainViewModel partial - Edit Operations (Undo, Redo, Copy, Cut, Paste)
    /// </summary>
    public partial class MainViewModel
    {
        /// <summary>
        /// Saves current state to undo stack before making changes.
        /// Issue #74: Made public to allow view to save state before property edits.
        /// Issue #252: Now also saves tree UI state (selection, expansion) for proper restoration
        /// </summary>
        public void SaveUndoState(string description)
        {
            if (CurrentDialog != null && !_undoRedoService.IsRestoring)
            {
                // Issue #252: Capture current tree state to restore on undo
                var treeState = CaptureTreeState();
                _undoRedoService.SaveState(CurrentDialog, description, treeState);
            }
        }

        public void Undo()
        {
            if (CurrentDialog == null || !_undoRedoService.CanUndo)
            {
                StatusMessage = "Nothing to undo";
                return;
            }

            // Issue #252: Capture current tree state (will be saved to redo stack)
            var currentTreeState = CaptureTreeState();

            var previousState = _undoRedoService.Undo(CurrentDialog, currentTreeState);
            if (previousState.Success && previousState.RestoredDialog != null)
            {
                CurrentDialog = previousState.RestoredDialog;
                // CRITICAL: Rebuild LinkRegistry after undo to fix Issue #28 (IsLink corruption)
                CurrentDialog.RebuildLinkRegistry();

                // Issue #356: Remove scrap entries for nodes that were restored by undo
                if (!string.IsNullOrEmpty(CurrentFileName))
                {
                    _scrapManager.RemoveRestoredNodes(CurrentFileName, CurrentDialog);
                    OnPropertyChanged(nameof(ScrapCount));
                    OnPropertyChanged(nameof(ScrapTabHeader));
                }

                // CRITICAL FIX: Extend IsRestoring to cover async tree rebuild.
                // Without this, tree restoration triggers SaveUndoState causing infinite loop.
                _undoRedoService.SetRestoring(true);

                // Issue #252: Use the tree state that was SAVED with the undo state
                // This restores selection to what it was BEFORE the action that was undone
                var savedTreeState = previousState.TreeState;

                // CRITICAL FIX (Issue #28): Don't use RefreshTreeView - it tries to restore
                // expansion state using old node references that don't exist after undo.
                // Instead, rebuild tree without expansion logic, then restore using paths.
                Dispatcher.UIThread.Post(() =>
                {
                    PopulateDialogNodes(skipAutoSelect: true);

                    // Restore expansion and selection using the SAVED tree state
                    Dispatcher.UIThread.Post(() =>
                    {
                        RestoreTreeState(savedTreeState);
                        // Clear restoring flag after tree state is fully restored
                        _undoRedoService.SetRestoring(false);
                    }, global::Avalonia.Threading.DispatcherPriority.Loaded);
                });

                HasUnsavedChanges = true;
            }

            StatusMessage = previousState.StatusMessage;
        }

        public void Redo()
        {
            if (CurrentDialog == null || !_undoRedoService.CanRedo)
            {
                StatusMessage = "Nothing to redo";
                return;
            }

            // Capture current dialog state BEFORE redo to detect deleted nodes (#370)
            var dialogBeforeRedo = CurrentDialog;

            // Capture current tree state to pass to redo (will be saved on undo stack)
            var currentTreeState = CaptureTreeState();

            var nextState = _undoRedoService.Redo(CurrentDialog, currentTreeState);
            if (nextState.Success && nextState.RestoredDialog != null)
            {
                CurrentDialog = nextState.RestoredDialog;
                // CRITICAL: Rebuild LinkRegistry after redo to fix Issue #28 (IsLink corruption)
                CurrentDialog.RebuildLinkRegistry();

                // Issue #370: Re-add nodes to scrap that were deleted by redo
                if (!string.IsNullOrEmpty(CurrentFileName))
                {
                    _scrapManager.RestoreDeletedNodesToScrap(CurrentFileName, dialogBeforeRedo, CurrentDialog);
                    OnPropertyChanged(nameof(ScrapCount));
                    OnPropertyChanged(nameof(ScrapTabHeader));
                }

                // Issue #252: Use the tree state that was saved WITH the redo state
                // This restores selection/expansion to what it was AFTER the original action
                var savedTreeState = nextState.TreeState;

                // CRITICAL FIX: Extend IsRestoring to cover async tree rebuild.
                // Without this, tree restoration triggers SaveUndoState causing infinite loop.
                _undoRedoService.SetRestoring(true);

                // CRITICAL FIX (Issue #28): Don't use RefreshTreeView - it tries to restore
                // expansion state using old node references that don't exist after redo.
                // Instead, rebuild tree without expansion logic, then restore using paths.
                Dispatcher.UIThread.Post(() =>
                {
                    PopulateDialogNodes(skipAutoSelect: true);

                    // Issue #252: Restore expansion and selection from the SAVED tree state
                    Dispatcher.UIThread.Post(() =>
                    {
                        RestoreTreeState(savedTreeState);
                        // Clear restoring flag after tree state is fully restored
                        _undoRedoService.SetRestoring(false);
                    }, global::Avalonia.Threading.DispatcherPriority.Loaded);
                });

                HasUnsavedChanges = true;
            }

            StatusMessage = nextState.StatusMessage;
        }

        public bool CanUndo => _undoRedoService.CanUndo;
        public bool CanRedo => _undoRedoService.CanRedo;

        public void CopyNode(TreeViewSafeNode? nodeToCopy)
        {
            if (nodeToCopy == null || nodeToCopy is TreeViewRootNode)
            {
                StatusMessage = "Cannot copy ROOT node";
                return;
            }

            if (CurrentDialog == null) return;

            var node = nodeToCopy.OriginalNode;
            var sourcePointer = nodeToCopy.SourcePointer;
            _clipboardService.CopyNode(node, CurrentDialog, sourcePointer);

            StatusMessage = $"Node copied: {node.DisplayText}";
            UnifiedLogger.LogApplication(LogLevel.INFO, $"Copied node: {node.DisplayText}");
        }

        public void CutNode(TreeViewSafeNode? nodeToCut)
        {
            if (CurrentDialog == null) return;
            if (nodeToCut == null || nodeToCut is TreeViewRootNode)
            {
                StatusMessage = "Cannot cut ROOT node";
                return;
            }

            var node = nodeToCut.OriginalNode;
            var sourcePointer = nodeToCut.SourcePointer;

            // Find sibling to focus BEFORE cutting
            var siblingToFocus = FindSiblingForFocus(node);

            // Save state for undo before cutting
            SaveUndoState("Cut Node");

            // Store node for pasting in clipboard service (include source pointer for scripts)
            _clipboardService.CutNode(node, CurrentDialog, sourcePointer);

            // CRITICAL: Check for other references BEFORE detaching
            // We need to count while the current reference is still there
            bool hasOtherReferences = _referenceManager.HasOtherReferences(CurrentDialog, node);

            // Detach from parent
            _referenceManager.DetachNodeFromParent(CurrentDialog, node);

            // If only had 1 reference (the one we just removed), remove from dialog lists
            // If had multiple references, keep it (still linked from elsewhere)
            if (!hasOtherReferences)
            {
                if (node.Type == DialogNodeType.Entry)
                {
                    CurrentDialog!.Entries.Remove(node);
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Removed cut Entry from list (was only reference): {node.DisplayText}");
                }
                else
                {
                    CurrentDialog!.Replies.Remove(node);
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Removed cut Reply from list (was only reference): {node.DisplayText}");
                }
                // CRITICAL: Recalculate all pointer indices after removing from list
                _indexManager.RecalculatePointerIndices(CurrentDialog);
            }
            else
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Kept cut node in list (has {hasOtherReferences} other references): {node.DisplayText}");
            }

            // NOTE: Do NOT add the cut node to scrap - it's stored in clipboard service for pasting
            // Cut is a move operation, not a delete operation
            // The node is intentionally detached and will be reattached on paste

            // Refresh tree and restore focus to sibling
            if (siblingToFocus != null)
            {
                RefreshTreeViewAndSelectNode(siblingToFocus);
            }
            else
            {
                RefreshTreeView();
            }

            HasUnsavedChanges = true;
            StatusMessage = $"Cut node: {node.DisplayText}";
            UnifiedLogger.LogApplication(LogLevel.INFO, $"Cut node (detached from parent): {node.DisplayText}");
        }

        public void PasteAsDuplicate(TreeViewSafeNode? parent)
        {
            if (CurrentDialog == null) return;

            // Save state for undo before pasting
            SaveUndoState("Paste Node");

            // Delegate to PasteOperationsManager
            var result = _pasteManager.PasteAsDuplicate(CurrentDialog, parent);

            // Update UI state
            StatusMessage = result.StatusMessage;

            if (result.Success)
            {
                // Issue #122: Focus on the newly pasted node instead of sibling
                if (result.PastedNode != null)
                {
                    NodeToSelectAfterRefresh = result.PastedNode;
                }
                RefreshTreeViewAndMarkDirty();
            }
        }

        public void PasteAsLink(TreeViewSafeNode? parent)
        {
            if (CurrentDialog == null) return;
            if (!_clipboardService.HasClipboardContent)
            {
                StatusMessage = "No node copied. Use Copy Node first.";
                return;
            }
            if (parent == null)
            {
                StatusMessage = "Select a parent node to paste link under";
                return;
            }

            // Check if pasting to ROOT
            if (parent is TreeViewRootNode)
            {
                StatusMessage = "Cannot paste as link to ROOT - use Paste as Duplicate instead";
                UnifiedLogger.LogApplication(LogLevel.WARN, "Blocked paste as link to ROOT - links not supported at ROOT level");
                return;
            }

            // Issue #11: Check type compatibility before attempting paste
            var clipboardNode = _clipboardService.ClipboardNode;
            if (clipboardNode != null && clipboardNode.Type == parent.OriginalNode.Type)
            {
                string parentType = parent.OriginalNode.Type == DialogNodeType.Entry ? "NPC Entry" : "PC Reply";
                string nodeType = clipboardNode.Type == DialogNodeType.Entry ? "NPC Entry" : "PC Reply";
                StatusMessage = $"Invalid link: Cannot link {nodeType} under {parentType} (same types not allowed)";
                return;
            }

            // Save state for undo before creating link
            SaveUndoState("Paste as Link");

            // Delegate to clipboard service
            var linkPtr = _clipboardService.PasteAsLink(CurrentDialog, parent.OriginalNode);

            if (linkPtr == null)
            {
                // Service already logged the reason (Cut operation, different dialog, node not found, etc.)
                StatusMessage = "Cannot paste as link - operation failed";
                return;
            }

            // Register the link pointer with LinkRegistry
            CurrentDialog.LinkRegistry.RegisterLink(linkPtr);

            // Issue #122: Focus on parent node (link is under parent, not standalone)
            NodeToSelectAfterRefresh = parent.OriginalNode;
            RefreshTreeViewAndMarkDirty();
            StatusMessage = $"Pasted link under {parent.DisplayText}: {linkPtr.Node?.DisplayText}";
        }

        // Phase 1 Step 8: Copy Operations (Clipboard)
        public string? GetNodeText(TreeViewSafeNode? node)
        {
            if (node == null || node is TreeViewRootNode)
                return null;

            return node.OriginalNode.Text?.GetDefault() ?? "";
        }

        public string? GetNodeProperties(TreeViewSafeNode? node)
        {
            if (node == null || node is TreeViewRootNode)
                return null;

            var dialogNode = node.OriginalNode;
            var sourcePointer = node.SourcePointer;
            var sb = new System.Text.StringBuilder();

            sb.AppendLine($"=== Node Properties ===");
            sb.AppendLine($"Type: {(dialogNode.Type == DialogNodeType.Entry ? "Entry (NPC)" : "Reply")}");
            sb.AppendLine($"Speaker: {dialogNode.Speaker ?? "(none)"}");
            sb.AppendLine($"Text: {dialogNode.Text?.GetDefault() ?? "(empty)"}");
            sb.AppendLine();
            sb.AppendLine($"Animation: {dialogNode.Animation}");
            sb.AppendLine($"Animation Loop: {dialogNode.AnimationLoop}");
            sb.AppendLine($"Sound: {dialogNode.Sound ?? "(none)"}");
            sb.AppendLine($"Delay: {(dialogNode.Delay == uint.MaxValue ? "(none)" : dialogNode.Delay.ToString())}");
            sb.AppendLine();
            sb.AppendLine($"Script (Appears When): {sourcePointer?.ScriptAppears ?? "(none)"}");
            if (sourcePointer?.ConditionParams != null && sourcePointer.ConditionParams.Count > 0)
            {
                sb.AppendLine($"Condition Parameters:");
                foreach (var param in sourcePointer.ConditionParams)
                {
                    sb.AppendLine($"  {param.Key} = {param.Value}");
                }
            }
            sb.AppendLine($"Script (Action): {dialogNode.ScriptAction ?? "(none)"}");
            if (dialogNode.ActionParams != null && dialogNode.ActionParams.Count > 0)
            {
                sb.AppendLine($"Action Parameters:");
                foreach (var param in dialogNode.ActionParams)
                {
                    sb.AppendLine($"  {param.Key} = {param.Value}");
                }
            }
            sb.AppendLine();
            sb.AppendLine($"Quest: {dialogNode.Quest ?? "(none)"}");
            sb.AppendLine($"Quest Entry: {(dialogNode.QuestEntry == uint.MaxValue ? "(none)" : dialogNode.QuestEntry.ToString())}");
            sb.AppendLine();
            sb.AppendLine($"Comment: {dialogNode.Comment ?? "(none)"}");
            sb.AppendLine($"Children: {dialogNode.Pointers.Count}");

            return sb.ToString();
        }

        public string? GetTreeStructure()
        {
            if (CurrentDialog == null)
                return null;

            // Issue #197: Use screenplay format for cleaner, more readable output
            return CommandLineService.GenerateScreenplay(CurrentDialog, CurrentFileName);
        }
    }
}
