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

            // Add entry node with script indicators
            var hasAction = !string.IsNullOrEmpty(entry.ScriptAction);
            nodes.Add(new DialogNodeInfo
            {
                Id = entryId,
                Type = "npc",
                Text = entry.Text?.GetDefault() ?? "",
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
                        // Add link node indicator with condition info
                        var linkNodeId = $"link_{entryIndex}_{replyIndex}";
                        nodes.Add(new DialogNodeInfo
                        {
                            Id = linkNodeId,
                            Type = "link",
                            Text = $"-> Reply {replyIndex}",
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

            // Add reply node with script indicators
            var hasAction = !string.IsNullOrEmpty(reply.ScriptAction);
            nodes.Add(new DialogNodeInfo
            {
                Id = replyId,
                Type = "pc",
                Text = reply.Text?.GetDefault() ?? "",
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
                        // Add link node indicator with condition info
                        var linkNodeId = $"link_{replyIndex}_{entryIndex}";
                        nodes.Add(new DialogNodeInfo
                        {
                            Id = linkNodeId,
                            Type = "link",
                            Text = $"-> Entry {entryIndex}",
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

        private DialogContextService() { }
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
