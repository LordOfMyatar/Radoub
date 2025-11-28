using System.Collections.Generic;
using DialogEditor.Models;
using DialogEditor.Services;
using Parley.Models;

namespace DialogEditor.Services
{
    /// <summary>
    /// Result of a paste operation with status information
    /// </summary>
    public class PasteResult
    {
        public bool Success { get; set; }
        public string StatusMessage { get; set; } = "";
        public DialogNode? PastedNode { get; set; }
    }

    /// <summary>
    /// Service responsible for paste operations (duplicate and link).
    /// Extracted from MainViewModel to improve separation of concerns.
    ///
    /// Handles:
    /// - Paste as Duplicate logic with validation
    /// - Paste to ROOT with type conversion
    /// - Paste to normal parents with type checking
    /// - Integration with clipboard, cloning, and index services
    /// </summary>
    public class PasteOperationsManager
    {
        private readonly DialogClipboardService _clipboardService;
        private readonly NodeCloningService _cloningService;
        private readonly IndexManager _indexManager;

        public PasteOperationsManager(
            DialogClipboardService clipboardService,
            NodeCloningService cloningService,
            IndexManager indexManager)
        {
            _clipboardService = clipboardService;
            _cloningService = cloningService;
            _indexManager = indexManager;
        }

        /// <summary>
        /// Pastes a node as a duplicate (or moves if it was cut).
        /// Handles validation, type conversion for ROOT, and pointer creation.
        /// </summary>
        public PasteResult PasteAsDuplicate(Dialog dialog, TreeViewSafeNode? parent)
        {
            if (dialog == null)
                return new PasteResult { Success = false, StatusMessage = "No dialog loaded" };

            if (_clipboardService.ClipboardNode == null)
                return new PasteResult { Success = false, StatusMessage = "No node copied. Use Copy Node first." };

            if (parent == null)
                return new PasteResult { Success = false, StatusMessage = "Select a parent node to paste under" };

            // Check if pasting to ROOT
            if (parent is TreeViewRootNode)
            {
                return PasteToRoot(dialog);
            }
            else
            {
                return PasteToParent(dialog, parent);
            }
        }

        /// <summary>
        /// Handles pasting a node to ROOT level
        /// </summary>
        private PasteResult PasteToRoot(Dialog dialog)
        {
            // PC Replies can NEVER be at ROOT (they only respond to NPCs)
            if (_clipboardService.ClipboardNode!.Type == DialogNodeType.Reply &&
                string.IsNullOrEmpty(_clipboardService.ClipboardNode.Speaker))
            {
                UnifiedLogger.LogApplication(LogLevel.WARN, "Blocked PC Reply paste to ROOT");
                return new PasteResult
                {
                    Success = false,
                    StatusMessage = "Cannot paste PC Reply to ROOT - PC can only respond to NPC statements"
                };
            }

            // For Cut operation, reuse the node; for Copy, clone it
            var duplicate = _clipboardService.WasCutOperation
                ? _clipboardService.ClipboardNode
                : _cloningService.CloneNode(_clipboardService.ClipboardNode, dialog);

            // Convert NPC Reply nodes to Entry when pasting to ROOT (GFF requirement)
            if (duplicate.Type == DialogNodeType.Reply)
            {
                // This is an NPC Reply (has Speaker set) - convert to Entry
                duplicate.Type = DialogNodeType.Entry;
                UnifiedLogger.LogApplication(LogLevel.INFO, $"Auto-converted NPC Reply to Entry for ROOT level");
            }

            // If cut, ensure node is in the appropriate list (may have been removed during cut)
            if (_clipboardService.WasCutOperation)
            {
                var list = duplicate.Type == DialogNodeType.Entry ? dialog.Entries : dialog.Replies;
                if (!list.Contains(duplicate))
                {
                    dialog.AddNodeInternal(duplicate, duplicate.Type);
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Re-added cut node to list: {duplicate.DisplayText}");
                }
            }
            else
            {
                // For copy, always add (will be a new clone)
                dialog.AddNodeInternal(duplicate, duplicate.Type);
            }

            // Get the correct index after adding
            var duplicateIndex = (uint)dialog.GetNodeIndex(duplicate, duplicate.Type);

            // Create start pointer (preserve source script info from clipboard)
            var startPtr = new DialogPtr
            {
                Node = duplicate,
                Type = DialogNodeType.Entry,
                Index = duplicateIndex,
                IsLink = false,
                IsStart = true,
                ScriptAppears = _clipboardService.SourceScriptAppears,
                ConditionParams = _clipboardService.SourceConditionParams,
                Comment = "",
                Parent = dialog
            };

            dialog.Starts.Add(startPtr);

            // Register the start pointer with LinkRegistry
            dialog.LinkRegistry.RegisterLink(startPtr);

            // CRITICAL: Recalculate indices in case recursive cloning added multiple nodes
            _indexManager.RecalculatePointerIndices(dialog);

            var opType = _clipboardService.WasCutOperation ? "Moved" : "Pasted duplicate";
            UnifiedLogger.LogApplication(LogLevel.INFO, $"{opType} Entry to ROOT: {duplicate.DisplayText}");

            return new PasteResult
            {
                Success = true,
                StatusMessage = $"{opType} Entry at ROOT: {duplicate.DisplayText}",
                PastedNode = duplicate
            };
        }

        /// <summary>
        /// Handles pasting a node to a normal (non-ROOT) parent
        /// </summary>
        private PasteResult PasteToParent(Dialog dialog, TreeViewSafeNode parent)
        {
            var parentNode = parent.OriginalNode;

            // CRITICAL: Validate parent/child type compatibility (Aurora rule)
            // Entry (NPC) can only have Reply (PC) children
            // Reply (PC) can only have Entry (NPC) children
            if (parentNode.Type == _clipboardService.ClipboardNode!.Type)
            {
                string parentTypeName = parentNode.Type == DialogNodeType.Entry ? "NPC" : "PC";
                string childTypeName = _clipboardService.ClipboardNode.Type == DialogNodeType.Entry ? "NPC" : "PC";
                UnifiedLogger.LogApplication(LogLevel.WARN,
                    $"Blocked invalid paste: {childTypeName} node under {parentTypeName} parent");

                return new PasteResult
                {
                    Success = false,
                    StatusMessage = $"Cannot paste {childTypeName} under {parentTypeName} - conversation must alternate NPC/PC"
                };
            }

            // For Cut operation, reuse the node; for Copy, clone it
            var duplicateNode = _clipboardService.WasCutOperation
                ? _clipboardService.ClipboardNode
                : _cloningService.CloneNode(_clipboardService.ClipboardNode, dialog);

            // If cut, ensure node is in the appropriate list (may have been removed during cut)
            if (_clipboardService.WasCutOperation)
            {
                var list = duplicateNode.Type == DialogNodeType.Entry ? dialog.Entries : dialog.Replies;
                if (!list.Contains(duplicateNode))
                {
                    dialog.AddNodeInternal(duplicateNode, duplicateNode.Type);
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Re-added cut node to list: {duplicateNode.DisplayText}");
                }
            }
            else
            {
                // For copy, always add (will be a new clone)
                dialog.AddNodeInternal(duplicateNode, duplicateNode.Type);
            }

            // Get the correct index after adding
            var nodeIndex = (uint)dialog.GetNodeIndex(duplicateNode, duplicateNode.Type);

            // Create pointer from parent to duplicate (preserve source script info from clipboard)
            var newPtr = new DialogPtr
            {
                Node = duplicateNode,
                Type = duplicateNode.Type,
                Index = nodeIndex,
                IsLink = false,
                ScriptAppears = _clipboardService.SourceScriptAppears,
                ConditionParams = _clipboardService.SourceConditionParams,
                Comment = "",
                Parent = dialog
            };

            parent.OriginalNode.Pointers.Add(newPtr);

            // Register the new pointer with LinkRegistry
            dialog.LinkRegistry.RegisterLink(newPtr);

            // CRITICAL: Recalculate indices in case recursive cloning added multiple nodes
            _indexManager.RecalculatePointerIndices(dialog);

            var operation = _clipboardService.WasCutOperation ? "Moved" : "Pasted duplicate";
            UnifiedLogger.LogApplication(LogLevel.INFO, $"Pasted duplicate: {duplicateNode.DisplayText} under {parent.DisplayText}");

            return new PasteResult
            {
                Success = true,
                StatusMessage = $"{operation} node under {parent.DisplayText}: {duplicateNode.DisplayText}",
                PastedNode = duplicateNode
            };
        }
    }
}
