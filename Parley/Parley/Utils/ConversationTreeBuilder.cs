using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DialogEditor.Models;
using DialogEditor.Services;

namespace DialogEditor.Utils
{
    /// <summary>
    /// Builds conversation trees using flat-load-then-connect approach
    /// This avoids traversal issues with complex/looping conversations
    /// </summary>
    public class ConversationTreeBuilder
    {
        private readonly Dialog _dialog;

        public ConversationTreeBuilder(Dialog dialog)
        {
            _dialog = dialog;
        }

        /// <summary>
        /// Build complete conversation tree representation
        /// Uses flat data that's already loaded and linked
        /// </summary>
        public string BuildTreeString(string fileName)
        {
            var sb = new StringBuilder();

            // Header
            sb.AppendLine($"Conversation Tree Structure for {fileName}.dlg:");
            sb.AppendLine("==============================");

            // Diagnostics
            AddPointerDiagnostics(sb);
            sb.AppendLine("==============================");

            // Root node
            sb.AppendLine("- Root");

            // Build tree from starting points
            if (_dialog.Starts != null)
            {
                foreach (var start in _dialog.Starts)
                {
                    if (start.Node != null)
                    {
                        BuildNodeSubtree(sb, start.Node, 1);
                    }
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Display all nodes in a completely flat structure - no recursion!
        /// Shows each node and what it immediately connects to without traversing
        /// </summary>
        private void BuildNodeSubtree(StringBuilder sb, DialogNode node, int depth)
        {
            var indent = new string(' ', depth * 2);

            // Get speaker and text
            string speaker = GetSpeakerLabel(node);
            string text = string.IsNullOrEmpty(node.DisplayText) ? "[CONTINUE]" : node.DisplayText;

            // Output the node
            sb.AppendLine($"{indent}- {speaker} \"{text}\"");

            // Show what this node points to (flat display - no recursion)
            if (node.Pointers?.Count > 0)
            {
                sb.AppendLine($"{indent}  [Has {node.Pointers.Count} pointer(s)]");

                for (int i = 0; i < node.Pointers.Count; i++)
                {
                    var pointer = node.Pointers[i];
                    if (pointer.Node != null)
                    {
                        var targetSpeaker = GetSpeakerLabel(pointer.Node);
                        var targetText = string.IsNullOrEmpty(pointer.Node.DisplayText) ? "[CONTINUE]" : pointer.Node.DisplayText;

                        // Show conditions if present
                        var conditionsText = pointer.ConditionParams?.Count > 0
                            ? $" (Conditions: {string.Join(", ", pointer.ConditionParams.Select(kvp => $"{kvp.Key}={kvp.Value}"))})"
                            : "";

                        if (pointer.IsLink)
                        {
                            sb.AppendLine($"{indent}    -> [LINK] {targetSpeaker} \"{targetText}\"{conditionsText}");
                        }
                        else
                        {
                            sb.AppendLine($"{indent}    -> {targetSpeaker} \"{targetText}\"{conditionsText}");

                            // Show what the target has (flat preview only)
                            if (pointer.Node.Pointers?.Count > 0)
                            {
                                sb.AppendLine($"{indent}        [Target has {pointer.Node.Pointers.Count} pointer(s)]");

                                // Show first few for context, but don't recurse
                                for (int j = 0; j < Math.Min(pointer.Node.Pointers.Count, 2); j++)
                                {
                                    var nextPtr = pointer.Node.Pointers[j];
                                    if (nextPtr.Node != null)
                                    {
                                        var nextSpeaker = GetSpeakerLabel(nextPtr.Node);
                                        var nextText = string.IsNullOrEmpty(nextPtr.Node.DisplayText) ? "[CONTINUE]" : nextPtr.Node.DisplayText;
                                        sb.AppendLine($"{indent}          -> {nextSpeaker} \"{nextText}\"");
                                    }
                                }

                                if (pointer.Node.Pointers.Count > 2)
                                {
                                    sb.AppendLine($"{indent}          -> ... {pointer.Node.Pointers.Count - 2} more");
                                }
                            }
                            else
                            {
                                sb.AppendLine($"{indent}        [Target ends conversation]");
                            }
                        }
                    }
                    else
                    {
                        sb.AppendLine($"{indent}    -> [NULL POINTER]");
                    }
                }
            }
            else
            {
                sb.AppendLine($"{indent}  [No pointers - conversation ends]");
            }
        }


        /// <summary>
        /// Get appropriate speaker label for a node
        /// </summary>
        private string GetSpeakerLabel(DialogNode node)
        {
            if (node.Type == DialogNodeType.Reply)
            {
                return "[PC]";
            }
            else if (!string.IsNullOrEmpty(node.Speaker))
            {
                return $"[{node.Speaker}]";
            }
            else
            {
                return "[Owner]";
            }
        }

        /// <summary>
        /// Write tree structure to session log file
        /// </summary>
        public void WriteToSessionLog(string fileName)
        {
            try
            {
                var treeString = BuildTreeString(fileName);
                var sessionDir = UnifiedLogger.GetSessionDirectory();
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var logFileName = $"TreeView_{fileName}_{timestamp}.txt";
                var logPath = System.IO.Path.Combine(sessionDir, logFileName);

                System.IO.File.WriteAllText(logPath, treeString);
                UnifiedLogger.LogApplication(LogLevel.INFO, $"Tree structure exported to: {logFileName}");
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to export tree structure: {ex.Message}");
            }
        }

        /// <summary>
        /// Add pointer diagnostics to output
        /// </summary>
        private void AddPointerDiagnostics(StringBuilder sb)
        {
            sb.AppendLine("🔍 POINTER DIAGNOSTICS:");

            int nullPointers = 0;
            int totalPointers = 0;

            // Check entries
            foreach (var entry in _dialog.Entries)
            {
                foreach (var ptr in entry.Pointers)
                {
                    totalPointers++;
                    if (ptr.Node == null)
                    {
                        nullPointers++;
                        sb.AppendLine($"❌ Entry[{_dialog.Entries.IndexOf(entry)}] -> Index={ptr.Index} NULL NODE");
                    }
                }
            }

            // Check replies
            foreach (var reply in _dialog.Replies)
            {
                foreach (var ptr in reply.Pointers)
                {
                    totalPointers++;
                    if (ptr.Node == null)
                    {
                        nullPointers++;
                        sb.AppendLine($"❌ Reply[{_dialog.Replies.IndexOf(reply)}] -> Index={ptr.Index} NULL NODE");
                    }
                }
            }

            sb.AppendLine($"📊 {nullPointers} null pointers out of {totalPointers} total");
        }
    }
}