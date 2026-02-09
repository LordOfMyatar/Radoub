using System;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace DialogEditor.Services
{
    /// <summary>
    /// Represents a single scrapped node entry
    /// </summary>
    public class ScrapEntry
    {
        public string Id { get; set; } = "";
        public string FilePath { get; set; } = "";
        public DateTime Timestamp { get; set; }
        public string Operation { get; set; } = "";
        public string NodeType { get; set; } = "";
        public string NodeText { get; set; } = "";
        public string? Speaker { get; set; }
        public int OriginalIndex { get; set; }
        public string SerializedNode { get; set; } = "";
        public int NestingLevel { get; set; } = 0;
        public string? ParentNodeText { get; set; }

        // Batch tracking for subtree restoration (#458, #124)
        /// <summary>
        /// Groups nodes deleted in the same operation (e.g., deleting a node with children).
        /// All entries with the same DeletionBatchId were deleted together.
        /// </summary>
        public string? DeletionBatchId { get; set; }

        /// <summary>
        /// Links to the parent entry's Id within the same deletion batch.
        /// Used to reconstruct parent-child relationships during restoration.
        /// Null for root nodes in the batch.
        /// </summary>
        public string? ParentEntryId { get; set; }

        /// <summary>
        /// Number of direct children in the deletion batch.
        /// Used for display purposes (e.g., "[3 children]").
        /// </summary>
        public int ChildCount { get; set; } = 0;

        /// <summary>
        /// True if this is the root node of the deletion batch (first node deleted).
        /// Used to identify the entry to show in batch-grouped UI.
        /// </summary>
        public bool IsBatchRoot { get; set; } = false;

        [JsonIgnore]
        public string DisplayText
        {
            get
            {
                var childInfo = ChildCount > 0 ? $" [+{ChildCount}]" : "";
                return $"[{NodeTypeDisplay}] {NodeText}{childInfo}";
            }
        }

        [JsonIgnore]
        public string NodeTypeDisplay
        {
            get
            {
                // Better labels for node types
                return NodeType switch
                {
                    "Entry" when NestingLevel == 0 => "NPC Entry",  // Root level entry
                    "Entry" => "NPC Reply",  // Nested entry (NPC response)
                    "Reply" => "PC Reply",   // Player response
                    _ => NodeType
                };
            }
        }

        /// <summary>
        /// Display indicator for child count in the deletion batch.
        /// </summary>
        [JsonIgnore]
        public string ChildCountDisplay => ChildCount > 0 ? $"+{ChildCount} children" : "";

        /// <summary>
        /// True if this entry has children in the deletion batch.
        /// </summary>
        [JsonIgnore]
        public bool HasBatchChildren => ChildCount > 0;

        [JsonIgnore]
        public string HierarchyInfo
        {
            get
            {
                // Show batch info for batch roots with children
                if (IsBatchRoot && ChildCount > 0)
                {
                    return $"Subtree ({ChildCount + 1} nodes)";
                }

                if (NestingLevel == 0) return "Root level";
                if (!string.IsNullOrEmpty(ParentNodeText))
                {
                    var parentPreview = ParentNodeText?.Length > 30
                        ? ParentNodeText.Substring(0, 27) + "..."
                        : ParentNodeText;
                    return $"Under: {parentPreview}";
                }
                return $"Depth: {NestingLevel}";
            }
        }

        [JsonIgnore]
        public string TimeAgo
        {
            get
            {
                var elapsed = DateTime.UtcNow - Timestamp;
                if (elapsed.TotalMinutes < 1) return "just now";
                if (elapsed.TotalMinutes < 60) return $"{(int)elapsed.TotalMinutes}m ago";
                if (elapsed.TotalHours < 24) return $"{(int)elapsed.TotalHours}h ago";
                if (elapsed.TotalDays < 7) return $"{(int)elapsed.TotalDays}d ago";
                return Timestamp.ToString("yyyy-MM-dd");
            }
        }

        /// <summary>
        /// Children of this entry within the same deletion batch.
        /// Populated by ScrapManager.BuildChildrenRecursive() for TreeView display.
        /// </summary>
        [JsonIgnore]
        public ObservableCollection<ScrapEntry> Children { get; } = new ObservableCollection<ScrapEntry>();

        /// <summary>
        /// Whether this entry is expanded in the TreeView.
        /// </summary>
        [JsonIgnore]
        public bool IsExpanded { get; set; } = false;
    }
}
