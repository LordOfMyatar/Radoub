using System;
using System.Collections.Generic;
using DialogEditor.Models;
using DialogEditor.Services;
using DialogEditor.Utils;

namespace Parley.Services
{
    /// <summary>
    /// Service responsible for managing dialog pointer indices.
    /// Extracted from MainViewModel to improve separation of concerns.
    ///
    /// Handles:
    /// - Recalculating pointer indices after node list changes
    /// - Updating pointers during move operations
    /// - Validating pointer index integrity
    /// - Performing node reordering with index tracking
    /// </summary>
    public class IndexManager
    {
        /// <summary>
        /// CRITICAL: Recalculates all pointer indices to match current list positions.
        /// This must be called after any operation that removes nodes from Entries/Replies lists.
        /// </summary>
        public void RecalculatePointerIndices(Dialog dialog)
        {
            if (dialog == null) return;

            UnifiedLogger.LogApplication(LogLevel.DEBUG, "Recalculating all pointer indices using LinkRegistry");

            // Rebuild the LinkRegistry from current dialog state
            dialog.RebuildLinkRegistry();

            // Update all Entry node indices
            for (uint i = 0; i < dialog.Entries.Count; i++)
            {
                var entry = dialog.Entries[(int)i];
                dialog.LinkRegistry.UpdateNodeIndex(entry, i, DialogNodeType.Entry);
            }

            // Update all Reply node indices
            for (uint i = 0; i < dialog.Replies.Count; i++)
            {
                var reply = dialog.Replies[(int)i];
                dialog.LinkRegistry.UpdateNodeIndex(reply, i, DialogNodeType.Reply);
            }

            // Validate all indices are correct
            var errors = dialog.ValidatePointerIndices();
            if (errors.Count > 0)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN, $"Index validation found {errors.Count} issues after recalculation:");
                foreach (var error in errors)
                {
                    UnifiedLogger.LogApplication(LogLevel.WARN, $"  - {error}");
                }
            }
            else
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG, "All pointer indices validated successfully");
            }
        }

        /// <summary>
        /// Updates pointer indices during a move operation using an IndexUpdateTracker.
        /// </summary>
        public void UpdatePointersForMove(Dialog dialog, IndexUpdateTracker tracker)
        {
            if (dialog == null) return;

            int updateCount = 0;

            // Update StartingList pointers (if moving entries)
            if (tracker.ListType == DialogNodeType.Entry)
            {
                foreach (var start in dialog.Starts)
                {
                    if (tracker.TryGetUpdatedIndex(start.Index, out uint newIdx))
                    {
                        UnifiedLogger.LogApplication(LogLevel.DEBUG,
                            $"StartingList: Index {start.Index} → {newIdx}");
                        start.Index = newIdx;
                        updateCount++;
                    }
                }
            }

            // Update Entry → Reply pointers (if moving replies)
            if (tracker.ListType == DialogNodeType.Reply)
            {
                foreach (var entry in dialog.Entries)
                {
                    foreach (var ptr in entry.Pointers)
                    {
                        if (tracker.TryGetUpdatedIndex(ptr.Index, out uint newIdx))
                        {
                            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                                $"Entry '{entry.DisplayText}' → Reply: Index {ptr.Index} → {newIdx}");
                            ptr.Index = newIdx;
                            updateCount++;
                        }
                    }
                }
            }

            // Update Reply → Entry pointers (if moving entries)
            if (tracker.ListType == DialogNodeType.Entry)
            {
                foreach (var reply in dialog.Replies)
                {
                    foreach (var ptr in reply.Pointers)
                    {
                        if (tracker.TryGetUpdatedIndex(ptr.Index, out uint newIdx))
                        {
                            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                                $"Reply '{reply.DisplayText}' → Entry: Index {ptr.Index} → {newIdx}");
                            ptr.Index = newIdx;
                            updateCount++;
                        }
                    }
                }
            }

            UnifiedLogger.LogApplication(LogLevel.INFO, $"Updated {updateCount} pointer indices");
        }

        /// <summary>
        /// Validates that all pointer indices match their target nodes in the dialog lists.
        /// Returns true if validation passes, false if corruption is detected.
        /// </summary>
        public bool ValidateMoveIntegrity(Dialog dialog)
        {
            if (dialog == null) return false;

            try
            {
                // Validate StartingList pointers
                foreach (var start in dialog.Starts)
                {
                    if (start.Index >= dialog.Entries.Count)
                    {
                        UnifiedLogger.LogApplication(LogLevel.ERROR,
                            $"Invalid Start Index: {start.Index} >= Entry count {dialog.Entries.Count}");
                        return false;
                    }

                    if (start.Node != dialog.Entries[(int)start.Index])
                    {
                        UnifiedLogger.LogApplication(LogLevel.ERROR,
                            $"Start pointer mismatch: Index {start.Index} does not point to expected node");
                        return false;
                    }
                }

                // Validate Entry → Reply pointers
                foreach (var entry in dialog.Entries)
                {
                    foreach (var ptr in entry.Pointers)
                    {
                        if (ptr.Index >= dialog.Replies.Count)
                        {
                            UnifiedLogger.LogApplication(LogLevel.ERROR,
                                $"Invalid Reply Index in Entry '{entry.DisplayText}': {ptr.Index} >= Reply count {dialog.Replies.Count}");
                            return false;
                        }

                        if (ptr.Node != dialog.Replies[(int)ptr.Index])
                        {
                            UnifiedLogger.LogApplication(LogLevel.ERROR,
                                $"Entry pointer mismatch: Index {ptr.Index} does not point to expected node");
                            return false;
                        }
                    }
                }

                // Validate Reply → Entry pointers
                foreach (var reply in dialog.Replies)
                {
                    foreach (var ptr in reply.Pointers)
                    {
                        if (ptr.Index >= dialog.Entries.Count)
                        {
                            UnifiedLogger.LogApplication(LogLevel.ERROR,
                                $"Invalid Entry Index in Reply '{reply.DisplayText}': {ptr.Index} >= Entry count {dialog.Entries.Count}");
                            return false;
                        }

                        if (ptr.Node != dialog.Entries[(int)ptr.Index])
                        {
                            UnifiedLogger.LogApplication(LogLevel.ERROR,
                                $"Reply pointer mismatch: Index {ptr.Index} does not point to expected node");
                            return false;
                        }
                    }
                }

                UnifiedLogger.LogApplication(LogLevel.DEBUG, "Move integrity validation PASSED");
                return true;
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Validation exception: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Performs a node move operation with full index tracking and validation.
        /// Returns true if move succeeded, false if validation failed.
        /// </summary>
        public bool PerformMove(Dialog dialog, List<DialogNode> list, DialogNodeType nodeType, uint oldIdx, uint newIdx)
        {
            if (dialog == null) return false;

            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"Starting move operation: {nodeType} [{oldIdx}] → [{newIdx}]");

            // Create index tracker
            var tracker = new IndexUpdateTracker();
            tracker.CalculateMapping(oldIdx, newIdx, nodeType);

            UnifiedLogger.LogApplication(LogLevel.DEBUG, tracker.GetMoveDescription());

            // Save the node we're moving for later focus restoration
            var movedNode = list[(int)oldIdx];
            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                $"Moving node: '{movedNode.Text}' (type={movedNode.Type})");

            // Update all affected pointers
            UpdatePointersForMove(dialog, tracker);

            // Perform actual list reorder
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"BEFORE MOVE - List order:");
            for (int i = 0; i < list.Count; i++)
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"  [{i}] = '{list[i].Text}'");
            }

            list.RemoveAt((int)oldIdx);
            list.Insert((int)newIdx, movedNode);

            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"AFTER MOVE - List order:");
            for (int i = 0; i < list.Count; i++)
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"  [{i}] = '{list[i].Text}'");
            }

            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                $"List reordered: removed at {oldIdx}, inserted at {newIdx}");

            // Validate integrity
            if (!ValidateMoveIntegrity(dialog))
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR,
                    "Move validation FAILED - index corruption detected!");
                return false;
            }

            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"Move completed successfully: {nodeType} [{oldIdx}] → [{newIdx}]");
            return true;
        }
    }
}
