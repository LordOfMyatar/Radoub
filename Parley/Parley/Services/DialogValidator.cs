using System;
using System.Collections.Generic;
using System.Linq;
using DialogEditor.Models;
using DialogEditor.Parsers;

namespace DialogEditor.Services
{
    /// <summary>
    /// Validates dialog structure and integrity.
    /// Extracts validation logic from DialogParser for cleaner separation of concerns.
    /// Phase 4 of parser refactoring - Oct 28, 2025
    /// </summary>
    public class DialogValidator
    {
        /// <summary>
        /// Validates the overall structure of a dialog.
        /// </summary>
        /// <param name="dialog">Dialog to validate</param>
        /// <returns>ParserResult with validation status and messages</returns>
        public ParserResult ValidateStructure(Dialog dialog)
        {
            if (dialog == null)
            {
                return ParserResult.CreateError("Dialog is null");
            }

            var result = ParserResult.CreateSuccess();

            // Validate basic requirements
            if (dialog.Entries == null)
            {
                return ParserResult.CreateError("Entries collection is null");
            }

            if (dialog.Replies == null)
            {
                return ParserResult.CreateError("Replies collection is null");
            }

            if (dialog.Starts == null)
            {
                return ParserResult.CreateError("Starts collection is null");
            }

            // Validate pointer integrity
            var pointerErrors = ValidatePointerIntegrity(dialog);
            foreach (var error in pointerErrors)
            {
                result.AddWarning(error);
            }

            // Validate node counts
            if (dialog.Entries.Count == 0 && dialog.Replies.Count == 0)
            {
                result.AddWarning("Dialog has no entries or replies");
            }

            if (dialog.Starts.Count == 0)
            {
                result.AddWarning("Dialog has no starting points");
            }

            // Mark as failed if there are pointer integrity errors
            if (pointerErrors.Count > 0)
            {
                result.Success = false;
                result.ErrorMessage = $"Found {pointerErrors.Count} pointer integrity errors";
            }

            return result;
        }

        /// <summary>
        /// Validates that all pointers reference valid nodes.
        /// </summary>
        /// <param name="dialog">Dialog to validate</param>
        /// <returns>List of error messages (empty if valid)</returns>
        public List<string> ValidatePointerIntegrity(Dialog dialog)
        {
            var errors = new List<string>();

            if (dialog == null)
            {
                errors.Add("Dialog is null");
                return errors;
            }

            // Validate entry pointers
            for (int i = 0; i < dialog.Entries.Count; i++)
            {
                var entry = dialog.Entries[i];
                foreach (var ptr in entry.Pointers)
                {
                    if (ptr.Index >= dialog.Replies.Count)
                    {
                        errors.Add($"Entry[{i}] has pointer to Reply[{ptr.Index}] but only {dialog.Replies.Count} replies exist");
                    }
                }
            }

            // Validate reply pointers
            for (int i = 0; i < dialog.Replies.Count; i++)
            {
                var reply = dialog.Replies[i];
                foreach (var ptr in reply.Pointers)
                {
                    if (ptr.Index >= dialog.Entries.Count)
                    {
                        errors.Add($"Reply[{i}] has pointer to Entry[{ptr.Index}] but only {dialog.Entries.Count} entries exist");
                    }
                }
            }

            // Validate start pointers
            for (int i = 0; i < dialog.Starts.Count; i++)
            {
                var start = dialog.Starts[i];
                if (start.Index >= dialog.Entries.Count)
                {
                    errors.Add($"Start[{i}] points to Entry[{start.Index}] but only {dialog.Entries.Count} entries exist");
                }
            }

            return errors;
        }

        /// <summary>
        /// Checks if a dialog has circular references in its conversation flow.
        /// </summary>
        /// <param name="dialog">Dialog to check</param>
        /// <returns>True if circular references exist, false otherwise</returns>
        public bool HasCircularReferences(Dialog dialog)
        {
            if (dialog == null)
                return false;

            // Circular references are actually valid in dialogs (conversation loops)
            // This method detects them for informational purposes
            var visited = new HashSet<string>();

            foreach (var start in dialog.Starts)
            {
                if (HasCircularPath(dialog, start.Index, true, visited, new HashSet<string>()))
                    return true;
            }

            return false;
        }

        private bool HasCircularPath(Dialog dialog, uint nodeIndex, bool isEntry, HashSet<string> globalVisited, HashSet<string> currentPath)
        {
            string nodeKey = $"{(isEntry ? "E" : "R")}{nodeIndex}";

            if (currentPath.Contains(nodeKey))
                return true; // Found a cycle

            if (globalVisited.Contains(nodeKey))
                return false; // Already checked this path

            currentPath.Add(nodeKey);
            globalVisited.Add(nodeKey);

            // Check children
            if (isEntry && nodeIndex < dialog.Entries.Count)
            {
                var entry = dialog.Entries[(int)nodeIndex];
                foreach (var ptr in entry.Pointers)
                {
                    if (HasCircularPath(dialog, ptr.Index, false, globalVisited, new HashSet<string>(currentPath)))
                        return true;
                }
            }
            else if (!isEntry && nodeIndex < dialog.Replies.Count)
            {
                var reply = dialog.Replies[(int)nodeIndex];
                foreach (var ptr in reply.Pointers)
                {
                    if (HasCircularPath(dialog, ptr.Index, true, globalVisited, new HashSet<string>(currentPath)))
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Gets summary statistics about a dialog.
        /// </summary>
        /// <param name="dialog">Dialog to analyze</param>
        /// <returns>Dictionary of statistic names and values</returns>
        public Dictionary<string, object> GetDialogStatistics(Dialog dialog)
        {
            if (dialog == null)
                return new Dictionary<string, object>();

            var stats = new Dictionary<string, object>
            {
                ["EntryCount"] = dialog.Entries?.Count ?? 0,
                ["ReplyCount"] = dialog.Replies?.Count ?? 0,
                ["StartCount"] = dialog.Starts?.Count ?? 0,
                ["TotalPointers"] = (dialog.Entries?.Sum(e => e.Pointers.Count) ?? 0) +
                                   (dialog.Replies?.Sum(r => r.Pointers.Count) ?? 0),
                ["HasCircularReferences"] = HasCircularReferences(dialog),
                ["HasEndConversationScript"] = !string.IsNullOrEmpty(dialog.ScriptEnd),
                ["HasAbortScript"] = !string.IsNullOrEmpty(dialog.ScriptAbort),
                ["PreventZoom"] = dialog.PreventZoom,
                ["WordCount"] = dialog.NumWords
            };

            return stats;
        }
    }
}
