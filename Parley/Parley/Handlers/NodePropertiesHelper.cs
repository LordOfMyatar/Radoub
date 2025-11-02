using System;
using System.Collections.Generic;
using System.Text;
using DialogEditor.Models;
using DialogEditor.Utils;
using DialogEditor.Services;

namespace DialogEditor.Handlers
{
    /// <summary>
    /// Helper for building node property displays
    /// </summary>
    public static class NodePropertiesHelper
    {
        public static void AppendNodePropertiesToStringBuilder(StringBuilder sb, DialogNode? node, DialogPtr? ptr, Dialog? currentDialog)
        {
            if (node == null)
            {
                sb.AppendLine("Node: NULL");
                return;
            }

            sb.AppendLine($"Node Type: {node.TypeDisplay} ({node.Type})");
            sb.AppendLine($"Speaker: {node.Speaker ?? "(none)"}");
            sb.AppendLine($"Text: \"{node.DisplayText ?? ""}\"");
            sb.AppendLine($"Comment: \"{node.Comment ?? ""}\"");
            sb.AppendLine();

            // Animation and Audio
            sb.AppendLine("=== Animation & Audio ===");
            sb.AppendLine($"Animation: {node.Animation}");
            sb.AppendLine($"Animation Loop: {node.AnimationLoop}");
            sb.AppendLine($"Sound: \"{node.Sound ?? ""}\"");
            sb.AppendLine($"Delay: {(node.Delay == uint.MaxValue ? "Default" : node.Delay.ToString())}");
            sb.AppendLine();

            // Scripts
            sb.AppendLine("=== Scripts ===");
            sb.AppendLine($"Script (Action): \"{node.ScriptAction ?? ""}\"");

            if (ptr != null)
            {
                sb.AppendLine($"Script (Appears When): \"{ptr.ScriptAppears ?? ""}\"");
            }
            else
            {
                var incomingScripts = FindIncomingConditionalScripts(node, currentDialog);
                if (incomingScripts.Count > 0)
                {
                    sb.AppendLine("Scripts (Incoming Conditions):");
                    for (int i = 0; i < incomingScripts.Count; i++)
                    {
                        sb.AppendLine($"  [{i}] \"{incomingScripts[i]}\"");
                    }
                }
                else
                {
                    sb.AppendLine("Script (Appears When): (no incoming conditions found)");
                }

                var outgoingScripts = FindOutgoingConditionalScripts(node);
                if (outgoingScripts.Count > 0)
                {
                    sb.AppendLine("Scripts (Outgoing Conditions):");
                    for (int i = 0; i < outgoingScripts.Count; i++)
                    {
                        sb.AppendLine($"  [{i}] \"{outgoingScripts[i]}\"");
                    }
                }
            }
            sb.AppendLine();

            // Quest System
            sb.AppendLine("=== Quest System ===");
            sb.AppendLine($"Quest Tag: \"{node.Quest ?? ""}\"");
            sb.AppendLine($"Quest Entry: {(node.QuestEntry == uint.MaxValue ? "(none)" : node.QuestEntry.ToString())}");
            sb.AppendLine();

            // Link Properties
            if (ptr != null)
            {
                sb.AppendLine("=== Link Properties ===");
                sb.AppendLine($"Is Link: {ptr.IsLink}");
                sb.AppendLine($"Link Comment: \"{ptr.LinkComment ?? ""}\"");
                sb.AppendLine();
            }

            // Script Parameters
            sb.AppendLine("=== Script Parameters ===");

            if (ptr != null && ptr.ConditionParams.Count > 0)
            {
                sb.AppendLine("Condition Parameters:");
                foreach (var kvp in ptr.ConditionParams)
                {
                    sb.AppendLine($"  {kvp.Key} = \"{kvp.Value}\"");
                }
            }
            else
            {
                var incomingParams = FindIncomingConditionParameters(node, currentDialog);
                if (incomingParams.Count > 0)
                {
                    sb.AppendLine("Incoming Condition Parameters:");
                    for (int i = 0; i < incomingParams.Count; i++)
                    {
                        sb.AppendLine($"  [{i}] Parameters:");
                        foreach (var kvp in incomingParams[i])
                        {
                            sb.AppendLine($"    {kvp.Key} = \"{kvp.Value}\"");
                        }
                    }
                }
                else
                {
                    sb.AppendLine("Condition Parameters: (none)");
                }

                var outgoingParams = FindOutgoingConditionParameters(node);
                if (outgoingParams.Count > 0)
                {
                    sb.AppendLine("Outgoing Condition Parameters:");
                    for (int i = 0; i < outgoingParams.Count; i++)
                    {
                        sb.AppendLine($"  [{i}] Parameters:");
                        foreach (var kvp in outgoingParams[i])
                        {
                            sb.AppendLine($"    {kvp.Key} = \"{kvp.Value}\"");
                        }
                    }
                }
            }

            if (node.ActionParams.Count > 0)
            {
                sb.AppendLine("Action Parameters:");
                foreach (var kvp in node.ActionParams)
                {
                    sb.AppendLine($"  {kvp.Key} = \"{kvp.Value}\"");
                }
            }
            else
            {
                sb.AppendLine("Action Parameters: (none)");
            }
            sb.AppendLine();

            // Pointers/Children
            sb.AppendLine("=== Pointers/Children ===");
            sb.AppendLine($"Number of child pointers: {node.Pointers.Count}");
            if (node.Pointers.Count > 0)
            {
                for (int i = 0; i < node.Pointers.Count; i++)
                {
                    var childPtr = node.Pointers[i];
                    var childNode = childPtr.Node;
                    var text = childNode?.DisplayText ?? "";
                    var preview = string.IsNullOrEmpty(text) ? "[Empty]" : text;
                    sb.AppendLine($"  [{i}] -> {childNode?.TypeDisplay ?? "NULL"}: \"{preview}\"");
                }
            }
        }

        private static List<string> FindIncomingConditionalScripts(DialogNode targetNode, Dialog? currentDialog)
        {
            var scripts = new List<string>();

            if (currentDialog == null)
                return scripts;

            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Searching for incoming scripts to node: '{targetNode.DisplayText}'");

            bool isStartingEntry = false;
            foreach (var start in currentDialog.Starts)
            {
                if (start.Node == targetNode)
                {
                    isStartingEntry = true;
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Node is a starting entry, checking start pointer ScriptAppears='{start.ScriptAppears}'");
                    if (!string.IsNullOrEmpty(start.ScriptAppears))
                    {
                        scripts.Add(start.ScriptAppears);
                    }

                    if (!string.IsNullOrEmpty(targetNode.Comment) && targetNode.Comment.Contains("[Script:"))
                    {
                        var match = System.Text.RegularExpressions.Regex.Match(targetNode.Comment, @"\[Script:([^\]]+)\]");
                        if (match.Success)
                        {
                            string nodeScript = match.Groups[1].Value;
                            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Found node-level script in comment: '{nodeScript}'");
                            scripts.Add(nodeScript);
                        }
                    }
                    break;
                }
            }

            if (!isStartingEntry)
            {
                foreach (var entry in currentDialog.Entries)
                {
                    foreach (var pointer in entry.Pointers)
                    {
                        if (pointer.Node == targetNode)
                        {
                            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Found pointer from entry '{entry.DisplayText}' to target node, ScriptAppears='{pointer.ScriptAppears}'");
                            if (!string.IsNullOrEmpty(pointer.ScriptAppears))
                            {
                                scripts.Add(pointer.ScriptAppears);
                            }
                        }
                    }
                }

                foreach (var reply in currentDialog.Replies)
                {
                    foreach (var pointer in reply.Pointers)
                    {
                        if (pointer.Node == targetNode)
                        {
                            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Found pointer from reply '{reply.DisplayText}' to target node, ScriptAppears='{pointer.ScriptAppears}'");
                            if (!string.IsNullOrEmpty(pointer.ScriptAppears))
                            {
                                scripts.Add(pointer.ScriptAppears);
                            }
                        }
                    }
                }
            }

            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Found {scripts.Count} incoming scripts for node '{targetNode.DisplayText}' (isStartingEntry: {isStartingEntry})");
            return scripts;
        }

        private static List<string> FindOutgoingConditionalScripts(DialogNode sourceNode)
        {
            var scripts = new List<string>();

            if (sourceNode?.Pointers == null)
                return scripts;

            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Searching for outgoing scripts from node: '{sourceNode.DisplayText}'");

            foreach (var pointer in sourceNode.Pointers)
            {
                if (!string.IsNullOrEmpty(pointer.ScriptAppears))
                {
                    scripts.Add(pointer.ScriptAppears);
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Found outgoing script: '{pointer.ScriptAppears}' on pointer to {pointer.Type} index {pointer.Index}");
                }
            }

            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Found {scripts.Count} outgoing scripts from node '{sourceNode.DisplayText}'");
            return scripts;
        }

        private static List<Dictionary<string, string>> FindOutgoingConditionParameters(DialogNode sourceNode)
        {
            var paramSets = new List<Dictionary<string, string>>();

            if (sourceNode?.Pointers == null)
                return paramSets;

            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Searching for outgoing condition parameters from node: '{sourceNode.DisplayText}'");

            foreach (var pointer in sourceNode.Pointers)
            {
                if (pointer.ConditionParams.Count > 0)
                {
                    paramSets.Add(new Dictionary<string, string>(pointer.ConditionParams));
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Found {pointer.ConditionParams.Count} outgoing condition parameters on pointer to {pointer.Type} index {pointer.Index}");
                }
            }

            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Found {paramSets.Count} sets of outgoing condition parameters from node '{sourceNode.DisplayText}'");
            return paramSets;
        }

        private static List<Dictionary<string, string>> FindIncomingConditionParameters(DialogNode targetNode, Dialog? currentDialog)
        {
            var paramSets = new List<Dictionary<string, string>>();

            if (currentDialog == null)
                return paramSets;

            bool isStartingEntry = false;
            foreach (var start in currentDialog.Starts)
            {
                if (start.Node == targetNode)
                {
                    isStartingEntry = true;
                    if (start.ConditionParams.Count > 0)
                    {
                        paramSets.Add(new Dictionary<string, string>(start.ConditionParams));
                    }

                    if (!string.IsNullOrEmpty(targetNode.Comment))
                    {
                        var matches = System.Text.RegularExpressions.Regex.Matches(targetNode.Comment, @"\[([^=]+)=([^\]]+)\]");
                        if (matches.Count > 0)
                        {
                            var nodeParams = new Dictionary<string, string>();
                            foreach (System.Text.RegularExpressions.Match match in matches)
                            {
                                string key = match.Groups[1].Value;
                                string value = match.Groups[2].Value;
                                nodeParams[key] = value;
                            }
                            if (nodeParams.Count > 0)
                            {
                                paramSets.Add(nodeParams);
                            }
                        }
                    }
                    break;
                }
            }

            if (!isStartingEntry)
            {
                foreach (var entry in currentDialog.Entries)
                {
                    foreach (var pointer in entry.Pointers)
                    {
                        if (pointer.Node == targetNode && pointer.ConditionParams.Count > 0)
                        {
                            paramSets.Add(new Dictionary<string, string>(pointer.ConditionParams));
                        }
                    }
                }

                foreach (var reply in currentDialog.Replies)
                {
                    foreach (var pointer in reply.Pointers)
                    {
                        if (pointer.Node == targetNode && pointer.ConditionParams.Count > 0)
                        {
                            paramSets.Add(new Dictionary<string, string>(pointer.ConditionParams));
                        }
                    }
                }
            }

            return paramSets;
        }
    }
}
