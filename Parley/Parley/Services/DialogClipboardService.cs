using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using DialogEditor.Models;
using DialogEditor.Services;

namespace Parley.Services
{
    /// <summary>
    /// Service responsible for clipboard operations (copy, cut, paste).
    /// Extracted from MainViewModel to improve separation of concerns.
    /// </summary>
    public class DialogClipboardService
    {
        private DialogNode? _copiedNode = null;
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
        /// Gets the copied/cut node from clipboard (null if empty)
        /// </summary>
        public DialogNode? ClipboardNode => _copiedNode;

        /// <summary>
        /// Copy a node to the clipboard
        /// </summary>
        public void CopyNode(DialogNode node, Dialog sourceDialog)
        {
            if (node == null)
                throw new ArgumentNullException(nameof(node));
            if (sourceDialog == null)
                throw new ArgumentNullException(nameof(sourceDialog));

            // Deep clone the node for clipboard
            _copiedNode = CloneNode(node);
            _wasCut = false;
            _sourceDialog = sourceDialog;

            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"Copied node to clipboard: Type={node.Type}");
        }

        /// <summary>
        /// Cut a node to the clipboard (mark for move operation)
        /// </summary>
        public void CutNode(DialogNode node, Dialog sourceDialog)
        {
            if (node == null)
                throw new ArgumentNullException(nameof(node));
            if (sourceDialog == null)
                throw new ArgumentNullException(nameof(sourceDialog));

            // Store reference for move operation (not deep clone)
            _copiedNode = node;
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

            DialogNode newNode;

            if (_wasCut && _sourceDialog == dialog)
            {
                // Move operation within same dialog - use original node
                newNode = _copiedNode;

                // Remove from old parent (will be done by caller)
                // Just need to add to new location
            }
            else
            {
                // Copy operation or cross-dialog move - create clone
                newNode = CloneNode(_copiedNode);

                // Add to appropriate list
                if (newNode.Type == DialogNodeType.Entry)
                {
                    dialog.Entries.Add(newNode);
                }
                else
                {
                    dialog.Replies.Add(newNode);
                }
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
        /// Paste as a link (creates pointer to existing node)
        /// </summary>
        public DialogPtr? PasteAsLink(Dialog dialog, DialogNode? parentNode)
        {
            if (_copiedNode == null)
                return null;
            if (dialog == null)
                throw new ArgumentNullException(nameof(dialog));

            // Can only link within same dialog
            if (_sourceDialog != dialog)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN,
                    "Cannot paste as link across different dialogs");
                return null;
            }

            // Find the index of the node in the dialog
            uint nodeIndex;
            if (_copiedNode.Type == DialogNodeType.Entry)
            {
                int index = dialog.Entries.IndexOf(_copiedNode);
                if (index < 0)
                {
                    UnifiedLogger.LogApplication(LogLevel.ERROR,
                        "Node not found in dialog Entries list");
                    return null;
                }
                nodeIndex = (uint)index;
            }
            else
            {
                int index = dialog.Replies.IndexOf(_copiedNode);
                if (index < 0)
                {
                    UnifiedLogger.LogApplication(LogLevel.ERROR,
                        "Node not found in dialog Replies list");
                    return null;
                }
                nodeIndex = (uint)index;
            }

            // Create link pointer
            var linkPtr = new DialogPtr
            {
                Parent = dialog,
                Type = _copiedNode.Type,
                Index = nodeIndex,
                IsLink = true,
                Node = _copiedNode
            };

            // Add link to parent or START nodes
            if (parentNode != null)
            {
                parentNode.Pointers.Add(linkPtr);
            }
            else if (_copiedNode.Type == DialogNodeType.Entry)
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
                $"Pasted node as link: Type={_copiedNode.Type}, Index={nodeIndex}");

            return linkPtr;
        }

        /// <summary>
        /// Clear the clipboard
        /// </summary>
        public void ClearClipboard()
        {
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
            // Serialize and deserialize for deep clone
            var json = JsonSerializer.Serialize(original, new JsonSerializerOptions
            {
                WriteIndented = false,
                ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
            });

            var clone = JsonSerializer.Deserialize<DialogNode>(json);
            if (clone == null)
                throw new InvalidOperationException("Failed to clone node");

            // Clear pointers - they'll be rebuilt
            clone.Pointers = new List<DialogPtr>();

            // Recursively clone child nodes if needed
            if (original.Pointers.Count > 0)
            {
                CloneChildNodes(original, clone, new Dictionary<DialogNode, DialogNode>(), 0);
            }

            return clone;
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
                    // Create new clone
                    var json = JsonSerializer.Serialize(originalPtr.Node, new JsonSerializerOptions
                    {
                        WriteIndented = false,
                        ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
                    });

                    var deserializedChild = JsonSerializer.Deserialize<DialogNode>(json);
                    if (deserializedChild == null)
                        continue;
                    clonedChild = deserializedChild;

                    clonedChild.Pointers = new List<DialogPtr>();
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