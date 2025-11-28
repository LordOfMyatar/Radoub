using System;
using System.Collections.Generic;
using System.Linq;
using DialogEditor.Models;
using DialogEditor.Services;
using DialogEditor.Utils;

namespace DialogEditor.Services
{
    /// <summary>
    /// Service responsible for clipboard operations (copy, cut, paste).
    /// Extracted from MainViewModel to improve separation of concerns.
    /// </summary>
    public class DialogClipboardService
    {
        private DialogNode? _originalNode = null;  // Original node reference (for PasteAsLink)
        private DialogNode? _copiedNode = null;    // Cloned node (for PasteAsDuplicate)
        private bool _wasCut = false;
        private Dialog? _sourceDialog = null;

        /// <summary>
        /// Gets whether there is a node in the clipboard
        /// </summary>
        public bool HasClipboardContent => _copiedNode != null;

        /// <summary>
        /// Gets whether the clipboard content was from a cut operation
        /// </summary>
        public bool WasCutOperation => _wasCut;

        /// <summary>
        /// Gets the copied/cut node clone from clipboard (null if empty)
        /// </summary>
        public DialogNode? ClipboardNode => _copiedNode;

        /// <summary>
        /// Gets the original node reference for linking (null if empty)
        /// </summary>
        public DialogNode? OriginalNode => _originalNode;

        /// <summary>
        /// Copy a node to the clipboard
        /// </summary>
        public void CopyNode(DialogNode node, Dialog sourceDialog)
        {
            if (node == null)
                throw new ArgumentNullException(nameof(node));
            if (sourceDialog == null)
                throw new ArgumentNullException(nameof(sourceDialog));

            // Store both original (for PasteAsLink) and clone (for PasteAsDuplicate)
            _originalNode = node;
            _copiedNode = CloneNode(node);
            _wasCut = false;
            _sourceDialog = sourceDialog;

            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"Copied node to clipboard: Type={node.Type}");
        }

        /// <summary>
        /// Cut a node to the clipboard (Copy + mark for deletion)
        /// </summary>
        public void CutNode(DialogNode node, Dialog sourceDialog)
        {
            if (node == null)
                throw new ArgumentNullException(nameof(node));
            if (sourceDialog == null)
                throw new ArgumentNullException(nameof(sourceDialog));

            // Cut is now just Copy + mark for deletion
            // Store both original (for PasteAsLink) and clone (for PasteAsDuplicate)
            _originalNode = node;
            _copiedNode = CloneNode(node);
            _wasCut = true;
            _sourceDialog = sourceDialog;

            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"Cut node to clipboard: Type={node.Type}");
        }

        /// <summary>
        /// Paste as a duplicate (creates new node)
        /// </summary>
        public DialogNode? PasteAsDuplicate(Dialog dialog, DialogNode? parentNode, DialogPtr? parentPtr)
        {
            if (_copiedNode == null)
                return null;
            if (dialog == null)
                throw new ArgumentNullException(nameof(dialog));

            // Always use the clone (already created in Copy/Cut)
            // This makes Copy and Cut behavior consistent
            DialogNode newNode = _copiedNode;

            // Add to appropriate list
            if (newNode.Type == DialogNodeType.Entry)
            {
                dialog.Entries.Add(newNode);
            }
            else
            {
                dialog.Replies.Add(newNode);
            }

            // Get the index of the new node
            uint newIndex = newNode.Type == DialogNodeType.Entry
                ? (uint)(dialog.Entries.IndexOf(newNode))
                : (uint)(dialog.Replies.IndexOf(newNode));

            // Create pointer to new node
            if (parentNode != null)
            {
                var newPtr = new DialogPtr
                {
                    Parent = dialog,
                    Type = newNode.Type,
                    Index = newIndex,
                    IsLink = false,
                    Node = newNode
                };
                parentNode.Pointers.Add(newPtr);
            }
            else if (newNode.Type == DialogNodeType.Entry)
            {
                // Add as START node if no parent and it's an Entry
                var startPtr = new DialogPtr
                {
                    Parent = dialog,
                    Type = DialogNodeType.Entry,
                    Index = newIndex,
                    IsStart = true,
                    IsLink = false,
                    Node = newNode
                };
                dialog.Starts.Add(startPtr);
            }

            // Clear clipboard after successful cut/paste
            if (_wasCut)
            {
                ClearClipboard();
            }

            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"Pasted node as duplicate: Type={newNode.Type}, WasCut={_wasCut}");

            return newNode;
        }

        /// <summary>
        /// Paste as a link (creates pointer to original node)
        /// </summary>
        public DialogPtr? PasteAsLink(Dialog dialog, DialogNode? parentNode)
        {
            if (_originalNode == null)
                return null;
            if (dialog == null)
                throw new ArgumentNullException(nameof(dialog));

            // CRITICAL: Cannot paste as link after Cut (source will be deleted)
            if (_wasCut)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN,
                    "Cannot paste as link after Cut - source node will be deleted");
                return null;
            }

            // Can only link within same dialog
            if (_sourceDialog != dialog)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN,
                    "Cannot paste as link across different dialogs");
                return null;
            }

            // Find the index of the ORIGINAL node in the dialog
            uint nodeIndex;
            if (_originalNode.Type == DialogNodeType.Entry)
            {
                int index = dialog.Entries.IndexOf(_originalNode);
                if (index < 0)
                {
                    UnifiedLogger.LogApplication(LogLevel.ERROR,
                        "Original node not found in dialog Entries list");
                    return null;
                }
                nodeIndex = (uint)index;
            }
            else
            {
                int index = dialog.Replies.IndexOf(_originalNode);
                if (index < 0)
                {
                    UnifiedLogger.LogApplication(LogLevel.ERROR,
                        "Original node not found in dialog Replies list");
                    return null;
                }
                nodeIndex = (uint)index;
            }

            // Create link pointer to ORIGINAL node
            var linkPtr = new DialogPtr
            {
                Parent = dialog,
                Type = _originalNode.Type,
                Index = nodeIndex,
                IsLink = true,
                Node = _originalNode
            };

            // Add link to parent or START nodes
            if (parentNode != null)
            {
                parentNode.Pointers.Add(linkPtr);
            }
            else if (_originalNode.Type == DialogNodeType.Entry)
            {
                linkPtr.IsStart = true;
                dialog.Starts.Add(linkPtr);
            }
            else
            {
                UnifiedLogger.LogApplication(LogLevel.WARN,
                    "Cannot paste Reply as START node");
                return null;
            }

            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"Pasted node as link: Type={_originalNode.Type}, Index={nodeIndex}");

            return linkPtr;
        }

        /// <summary>
        /// Clear the clipboard
        /// </summary>
        public void ClearClipboard()
        {
            _originalNode = null;
            _copiedNode = null;
            _wasCut = false;
            _sourceDialog = null;

            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                "Clipboard cleared");
        }

        /// <summary>
        /// Deep clone a dialog node (including children)
        /// </summary>
        private DialogNode CloneNode(DialogNode original)
        {
            // CRITICAL: Create shallow clone without Pointers to avoid circular serialization
            // Pointers will be rebuilt recursively afterward
            var shallowClone = CloningHelper.CreateShallowNodeClone(original);

            // Recursively clone child nodes if needed
            if (original.Pointers.Count > 0)
            {
                CloneChildNodes(original, shallowClone, new Dictionary<DialogNode, DialogNode>(), 0);
            }

            return shallowClone;
        }

        /// <summary>
        /// Recursively clone child nodes and rebuild pointer structure
        /// </summary>
        private void CloneChildNodes(DialogNode originalParent, DialogNode cloneParent,
            Dictionary<DialogNode, DialogNode> cloneMap, int depth)
        {
            // Prevent stack overflow with depth limit
            const int MAX_DEPTH = 100;
            if (depth >= MAX_DEPTH)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN,
                    $"Maximum clone depth ({MAX_DEPTH}) reached - stopping recursion to prevent stack overflow");
                return;
            }

            foreach (var originalPtr in originalParent.Pointers)
            {
                if (originalPtr.Node == null)
                    continue;

                DialogNode clonedChild;

                // Check if we've already cloned this node (handle circular references)
                if (cloneMap.ContainsKey(originalPtr.Node))
                {
                    clonedChild = cloneMap[originalPtr.Node];
                }
                else
                {
                    // Create shallow clone of child node using shared helper
                    clonedChild = CloningHelper.CreateShallowNodeClone(originalPtr.Node);

                    cloneMap[originalPtr.Node] = clonedChild;

                    // Recursively clone children with incremented depth
                    if (originalPtr.Node.Pointers.Count > 0)
                    {
                        CloneChildNodes(originalPtr.Node, clonedChild, cloneMap, depth + 1);
                    }
                }

                // Create new pointer to cloned child
                var clonedPtr = new DialogPtr
                {
                    Type = originalPtr.Type,
                    Index = originalPtr.Index, // Will be updated when added to dialog
                    IsLink = originalPtr.IsLink,
                    Node = clonedChild,
                    ScriptAppears = originalPtr.ScriptAppears,
                    Comment = originalPtr.Comment
                };

                // Clone condition params
                if (originalPtr.ConditionParams != null && originalPtr.ConditionParams.Count > 0)
                {
                    clonedPtr.ConditionParams = new Dictionary<string, string>(originalPtr.ConditionParams);
                }

                cloneParent.Pointers.Add(clonedPtr);
            }
        }
    }
}