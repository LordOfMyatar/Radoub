using System.Collections.Generic;
using System.Linq;
using DialogEditor.Models;
using Radoub.Formats.Logging;

namespace DialogEditor.ViewModels
{
    /// <summary>
    /// Coverage tracking and dialog structure analysis.
    /// </summary>
    public partial class ConversationSimulatorViewModel
    {
        /// <summary>
        /// Clear coverage data for the current file.
        /// </summary>
        public void ClearCoverage()
        {
            _coverageTracker.ClearCoverage(_filePath);
            OnPropertyChanged(nameof(CoverageDisplay));
            OnPropertyChanged(nameof(Coverage));
            OnPropertyChanged(nameof(CoverageComplete));
            StatusMessage = "Coverage cleared.";
            // Refresh replies to remove checkmarks
            UpdateDisplay();
        }

        private void AnalyzeDialogStructure()
        {
            // Count total replies for coverage tracking
            _totalReplies = _dialog.Replies.Count;

            // Collect root entry indices and map replies per root entry
            _rootEntryIndices.Clear();
            _repliesPerRootEntry.Clear();

            foreach (var start in _dialog.Starts)
            {
                if (start.Node != null)
                {
                    var entryIndex = _dialog.GetNodeIndex(start.Node, DialogNodeType.Entry);
                    if (entryIndex >= 0)
                    {
                        _rootEntryIndices.Add(entryIndex);

                        // Collect all reply indices reachable from this root entry
                        var replyIndices = new HashSet<int>();
                        CollectRepliesUnderNode(start.Node, replyIndices, new HashSet<DialogNode>());
                        _repliesPerRootEntry[entryIndex] = replyIndices;
                    }
                }
            }

            // Check for conditionals
            var hasConditionals = HasAnyConditionals();
            ShowNoConditionalsWarning = !hasConditionals;

            // Check for unreachable siblings (multiple NPC entries without conditions)
            ShowUnreachableSiblingsWarning = HasUnreachableSiblings();

            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                $"ConversationSimulator: Analyzed dialog - " +
                $"totalReplies={_totalReplies}, rootEntries={_rootEntryIndices.Count}, " +
                $"hasConditionals={hasConditionals}, unreachableSiblings={ShowUnreachableSiblingsWarning}");
        }

        /// <summary>
        /// Recursively collect all reply indices reachable from a node.
        /// </summary>
        private void CollectRepliesUnderNode(DialogNode node, HashSet<int> replyIndices, HashSet<DialogNode> visited)
        {
            if (visited.Contains(node))
                return; // Avoid infinite loops from links

            visited.Add(node);

            foreach (var ptr in node.Pointers)
            {
                if (ptr.Node == null)
                    continue;

                if (ptr.Type == DialogNodeType.Reply)
                {
                    var replyIndex = _dialog.GetNodeIndex(ptr.Node, DialogNodeType.Reply);
                    if (replyIndex >= 0)
                    {
                        replyIndices.Add(replyIndex);
                    }
                }

                // Continue traversing (both entries and replies can have children)
                CollectRepliesUnderNode(ptr.Node, replyIndices, visited);
            }
        }

        private bool HasAnyConditionals()
        {
            // Check if any pointer has a ScriptAppears condition
            foreach (var start in _dialog.Starts)
            {
                if (!string.IsNullOrEmpty(start.ScriptAppears))
                    return true;

                if (start.Node != null && HasConditionalsRecursive(start.Node))
                    return true;
            }

            return false;
        }

        private bool HasConditionalsRecursive(DialogNode node)
        {
            foreach (var ptr in node.Pointers)
            {
                if (!string.IsNullOrEmpty(ptr.ScriptAppears))
                    return true;

                if (ptr.Node != null && !ptr.IsLink)
                {
                    if (HasConditionalsRecursive(ptr.Node))
                        return true;
                }
            }

            return false;
        }

        private bool HasUnreachableSiblings()
        {
            // Check NPC entries (not PC replies) for siblings without conditions
            // When multiple NPC entries exist as children of the same parent,
            // and none have conditions, only the first is reachable

            foreach (var entry in _dialog.Entries)
            {
                var siblings = entry.Pointers
                    .Where(p => p.Type == DialogNodeType.Entry && !p.IsLink)
                    .ToList();

                if (siblings.Count > 1)
                {
                    var unconditionedCount = siblings.Count(p => string.IsNullOrEmpty(p.ScriptAppears));
                    if (unconditionedCount > 1)
                        return true;
                }
            }

            foreach (var reply in _dialog.Replies)
            {
                var siblings = reply.Pointers
                    .Where(p => p.Type == DialogNodeType.Entry && !p.IsLink)
                    .ToList();

                if (siblings.Count > 1)
                {
                    var unconditionedCount = siblings.Count(p => string.IsNullOrEmpty(p.ScriptAppears));
                    if (unconditionedCount > 1)
                        return true;
                }
            }

            return false;
        }
    }
}
