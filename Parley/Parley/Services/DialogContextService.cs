using System;
using System.Collections.Generic;
using DialogEditor.Models;

namespace DialogEditor.Services
{
    /// <summary>
    /// Singleton service providing access to the current dialog context.
    /// Used by plugin services to query dialog state without direct ViewModel coupling.
    /// Epic 3 / Issue #227
    /// </summary>
    public class DialogContextService
    {
        private static DialogContextService? _instance;
        public static DialogContextService Instance => _instance ??= new DialogContextService();

        private Dialog? _currentDialog;
        private string? _currentFileName;
        private string? _selectedNodeId;

        /// <summary>
        /// Current loaded dialog (may be null if no dialog is open)
        /// </summary>
        public Dialog? CurrentDialog
        {
            get => _currentDialog;
            set
            {
                _currentDialog = value;
                DialogChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Current dialog file name (without path)
        /// </summary>
        public string? CurrentFileName
        {
            get => _currentFileName;
            set => _currentFileName = value;
        }

        /// <summary>
        /// Currently selected node ID in the tree view
        /// </summary>
        public string? SelectedNodeId
        {
            get => _selectedNodeId;
            set
            {
                _selectedNodeId = value;
                NodeSelectionChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Event fired when dialog changes (load, close, modify)
        /// </summary>
        public event EventHandler? DialogChanged;

        /// <summary>
        /// Event fired when selected node changes
        /// </summary>
        public event EventHandler? NodeSelectionChanged;

        /// <summary>
        /// Event fired when a plugin requests node selection (Epic 40 Phase 3 / #234).
        /// The View layer subscribes to this to update the TreeView selection.
        /// </summary>
        public event EventHandler<NodeSelectionRequestedEventArgs>? NodeSelectionRequested;

        /// <summary>
        /// Get dialog structure as nodes and links for flowchart visualization.
        /// Returns the dialog tree in a format suitable for D3.js rendering.
        /// </summary>
        public (List<DialogNodeInfo> Nodes, List<DialogLinkInfo> Links) GetDialogStructure()
        {
            var nodes = new List<DialogNodeInfo>();
            var links = new List<DialogLinkInfo>();

            if (_currentDialog == null)
                return (nodes, links);

            // Track processed nodes to avoid duplicates
            var processedEntries = new HashSet<int>();
            var processedReplies = new HashSet<int>();

            // Add root node
            nodes.Add(new DialogNodeInfo
            {
                Id = "root",
                Type = "root",
                Text = "Dialog Start",
                Speaker = ""
            });

            // Process starting entries
            foreach (var start in _currentDialog.Starts)
            {
                var entryIndex = (int)start.Index;
                if (entryIndex < _currentDialog.Entries.Count)
                {
                    var entry = _currentDialog.Entries[entryIndex];
                    var entryId = $"entry_{entryIndex}";

                    // Link from root to entry
                    links.Add(new DialogLinkInfo { Source = "root", Target = entryId });

                    // Process entry and its children
                    ProcessEntry(entry, entryIndex, nodes, links, processedEntries, processedReplies);
                }
            }

            return (nodes, links);
        }

        private void ProcessEntry(DialogNode entry, int entryIndex, List<DialogNodeInfo> nodes,
            List<DialogLinkInfo> links, HashSet<int> processedEntries, HashSet<int> processedReplies)
        {
            if (processedEntries.Contains(entryIndex))
                return;

            processedEntries.Add(entryIndex);
            var entryId = $"entry_{entryIndex}";

            // Get entry text, with fallback for empty strings
            var entryText = entry.Text?.GetDefault() ?? "";
            if (string.IsNullOrWhiteSpace(entryText))
            {
                // Empty entry - show [Continue] or [End Dialog] based on whether it has children
                entryText = entry.Pointers.Count > 0 ? "[Continue]" : "[End Dialog]";
            }

            // Add entry node with script indicators
            var hasAction = !string.IsNullOrEmpty(entry.ScriptAction);
            nodes.Add(new DialogNodeInfo
            {
                Id = entryId,
                Type = "npc",
                Text = entryText,
                Speaker = entry.Speaker ?? "",
                HasAction = hasAction,
                ActionScript = entry.ScriptAction ?? ""
            });

            // Process reply children
            foreach (var pointer in entry.Pointers)
            {
                var replyIndex = (int)pointer.Index;
                if (_currentDialog != null && replyIndex < _currentDialog.Replies.Count)
                {
                    var replyId = $"reply_{replyIndex}";

                    // Check for condition on this pointer
                    var hasCondition = !string.IsNullOrEmpty(pointer.ScriptAppears);
                    var conditionScript = pointer.ScriptAppears ?? "";

                    // Check if this is a link reference
                    if (pointer.IsLink)
                    {
                        // Get the target reply's text for display
                        var targetReply = _currentDialog.Replies[replyIndex];
                        var linkText = targetReply.Text?.GetDefault() ?? "";
                        if (string.IsNullOrWhiteSpace(linkText))
                            linkText = "[Continue]";

                        // Add link node indicator with condition info
                        var linkNodeId = $"link_{entryIndex}_{replyIndex}";
                        nodes.Add(new DialogNodeInfo
                        {
                            Id = linkNodeId,
                            Type = "link",
                            Text = linkText,
                            IsLink = true,
                            LinkTarget = replyId,
                            HasCondition = hasCondition,
                            ConditionScript = conditionScript
                        });
                        links.Add(new DialogLinkInfo { Source = entryId, Target = linkNodeId });
                    }
                    else
                    {
                        links.Add(new DialogLinkInfo
                        {
                            Source = entryId,
                            Target = replyId,
                            HasCondition = hasCondition,
                            ConditionScript = conditionScript
                        });
                        ProcessReply(_currentDialog.Replies[replyIndex], replyIndex, nodes, links,
                            processedEntries, processedReplies);
                    }
                }
            }
        }

        private void ProcessReply(DialogNode reply, int replyIndex, List<DialogNodeInfo> nodes,
            List<DialogLinkInfo> links, HashSet<int> processedEntries, HashSet<int> processedReplies)
        {
            if (processedReplies.Contains(replyIndex))
                return;

            processedReplies.Add(replyIndex);
            var replyId = $"reply_{replyIndex}";

            // Get reply text, with fallback for empty strings
            var replyText = reply.Text?.GetDefault() ?? "";
            if (string.IsNullOrWhiteSpace(replyText))
            {
                // Empty reply - show [Continue] or [End Dialog] based on whether it has children
                replyText = reply.Pointers.Count > 0 ? "[Continue]" : "[End Dialog]";
            }

            // Add reply node with script indicators
            var hasAction = !string.IsNullOrEmpty(reply.ScriptAction);
            nodes.Add(new DialogNodeInfo
            {
                Id = replyId,
                Type = "pc",
                Text = replyText,
                Speaker = "",
                HasAction = hasAction,
                ActionScript = reply.ScriptAction ?? ""
            });

            // Process entry children
            foreach (var pointer in reply.Pointers)
            {
                var entryIndex = (int)pointer.Index;
                if (_currentDialog != null && entryIndex < _currentDialog.Entries.Count)
                {
                    var entryId = $"entry_{entryIndex}";

                    // Check for condition on this pointer
                    var hasCondition = !string.IsNullOrEmpty(pointer.ScriptAppears);
                    var conditionScript = pointer.ScriptAppears ?? "";

                    // Check if this is a link reference
                    if (pointer.IsLink)
                    {
                        // Get the target entry's text for display
                        var targetEntry = _currentDialog.Entries[entryIndex];
                        var linkText = targetEntry.Text?.GetDefault() ?? "";
                        if (string.IsNullOrWhiteSpace(linkText))
                            linkText = "[Continue]";

                        // Add link node indicator with condition info
                        var linkNodeId = $"link_{replyIndex}_{entryIndex}";
                        nodes.Add(new DialogNodeInfo
                        {
                            Id = linkNodeId,
                            Type = "link",
                            Text = linkText,
                            IsLink = true,
                            LinkTarget = entryId,
                            HasCondition = hasCondition,
                            ConditionScript = conditionScript
                        });
                        links.Add(new DialogLinkInfo { Source = replyId, Target = linkNodeId });
                    }
                    else
                    {
                        links.Add(new DialogLinkInfo
                        {
                            Source = replyId,
                            Target = entryId,
                            HasCondition = hasCondition,
                            ConditionScript = conditionScript
                        });
                        ProcessEntry(_currentDialog.Entries[entryIndex], entryIndex, nodes, links,
                            processedEntries, processedReplies);
                    }
                }
            }
        }

        /// <summary>
        /// Request selection of a node from a plugin (Epic 40 Phase 3 / #234).
        /// Raises NodeSelectionRequested event for the View layer to handle.
        /// </summary>
        /// <param name="nodeId">Node ID to select (e.g., "entry_0", "reply_3")</param>
        /// <returns>True if request was raised, false if invalid node ID</returns>
        public bool RequestNodeSelection(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId) || _currentDialog == null)
                return false;

            // Validate node ID format and existence
            if (!IsValidNodeId(nodeId))
                return false;

            // Raise event for View layer to handle the actual selection
            NodeSelectionRequested?.Invoke(this, new NodeSelectionRequestedEventArgs(nodeId));
            return true;
        }

        /// <summary>
        /// Validate that a node ID exists in the current dialog.
        /// </summary>
        private bool IsValidNodeId(string nodeId)
        {
            if (_currentDialog == null)
                return false;

            // Parse node ID format: "entry_N", "reply_N", "root", "link_X_Y"
            if (nodeId == "root")
                return true;

            if (nodeId.StartsWith("entry_"))
            {
                if (int.TryParse(nodeId.Substring(6), out int index))
                    return index >= 0 && index < _currentDialog.Entries.Count;
            }
            else if (nodeId.StartsWith("reply_"))
            {
                if (int.TryParse(nodeId.Substring(6), out int index))
                    return index >= 0 && index < _currentDialog.Replies.Count;
            }
            else if (nodeId.StartsWith("link_"))
            {
                // Link nodes are virtual - they reference existing entries/replies
                // Format: "link_parentIndex_targetIndex"
                return true; // Accept link nodes as valid
            }

            return false;
        }

        private DialogContextService() { }
    }

    /// <summary>
    /// Event args for node selection requests from plugins (Epic 40 Phase 3 / #234)
    /// </summary>
    public class NodeSelectionRequestedEventArgs : EventArgs
    {
        public string NodeId { get; }

        public NodeSelectionRequestedEventArgs(string nodeId)
        {
            NodeId = nodeId;
        }
    }

    /// <summary>
    /// Dialog node information for flowchart rendering
    /// </summary>
    public class DialogNodeInfo
    {
        public string Id { get; set; } = "";
        public string Type { get; set; } = "";
        public string Text { get; set; } = "";
        public string Speaker { get; set; } = "";
        public bool IsLink { get; set; }
        public string LinkTarget { get; set; } = "";
        public bool HasCondition { get; set; }
        public bool HasAction { get; set; }
        public string ConditionScript { get; set; } = "";
        public string ActionScript { get; set; } = "";
    }

    /// <summary>
    /// Dialog link information for flowchart rendering
    /// </summary>
    public class DialogLinkInfo
    {
        public string Source { get; set; } = "";
        public string Target { get; set; } = "";
        public bool HasCondition { get; set; }
        public string ConditionScript { get; set; } = "";
    }
}
