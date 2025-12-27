using System;
using System.Collections.Generic;
using System.Linq;
using DialogEditor.Models;
using DialogEditor.Services;
using Radoub.Formats.Logging;

namespace DialogEditor.Services
{
    /// <summary>
    /// Service responsible for dialog node editing operations (add, delete, move).
    /// Extracted from MainViewModel to improve separation of concerns.
    /// </summary>
    public class DialogEditorService
    {
        /// <summary>
        /// Add a smart node based on context (NPC Entry or PC Reply)
        /// </summary>
        public DialogNode AddSmartNode(Dialog dialog, DialogNode? parentNode, DialogPtr? parentPtr)
        {
            if (dialog == null)
                throw new ArgumentNullException(nameof(dialog));

            DialogNode newNode;

            // Determine node type based on parent
            if (parentNode == null)
            {
                // No parent selected, add at root level
                newNode = AddEntryNode(dialog, null, null);
            }
            else if (parentNode.Type == DialogNodeType.Entry)
            {
                // Parent is NPC, add PC Reply
                newNode = AddPCReplyNode(dialog, parentNode, parentPtr);
            }
            else
            {
                // Parent is PC, add NPC Entry
                newNode = AddEntryNode(dialog, parentNode, parentPtr);
            }

            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"Added smart node: Type={newNode.Type}, Parent={parentNode?.Type}");

            return newNode;
        }

        /// <summary>
        /// Add an NPC Entry node
        /// </summary>
        public DialogNode AddEntryNode(Dialog dialog, DialogNode? parentNode, DialogPtr? parentPtr)
        {
            if (dialog == null)
                throw new ArgumentNullException(nameof(dialog));

            var newNode = new DialogNode
            {
                Type = DialogNodeType.Entry,
                Text = new LocString(),
                Speaker = "" // Empty = "Owner" display (default NWN behavior)
            };

            dialog.Entries.Add(newNode);
            uint newIndex = (uint)(dialog.Entries.Count - 1);

            if (parentNode != null)
            {
                // Add pointer from parent to new node
                var newPtr = new DialogPtr
                {
                    Parent = dialog,
                    Type = DialogNodeType.Entry,
                    Index = newIndex,
                    IsLink = false,
                    Node = newNode
                };
                parentNode.Pointers.Add(newPtr);

                UnifiedLogger.LogApplication(LogLevel.DEBUG,
                    $"Added Entry node with index {newIndex} as child of {parentNode.Type}");
            }
            else
            {
                // Add as START node if no parent
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

                UnifiedLogger.LogApplication(LogLevel.DEBUG,
                    $"Added Entry node with index {newIndex} as START node");
            }

            return newNode;
        }

        /// <summary>
        /// Add a PC Reply node
        /// </summary>
        public DialogNode AddPCReplyNode(Dialog dialog, DialogNode parentNode, DialogPtr? parentPtr)
        {
            if (dialog == null)
                throw new ArgumentNullException(nameof(dialog));
            if (parentNode == null)
                throw new ArgumentNullException(nameof(parentNode));

            var newNode = new DialogNode
            {
                Type = DialogNodeType.Reply,
                Text = new LocString(),
                Speaker = "" // Empty = "PC" display (NWN default for Reply nodes)
            };

            dialog.Replies.Add(newNode);
            uint newIndex = (uint)(dialog.Replies.Count - 1);

            // Add pointer from parent to new node
            var newPtr = new DialogPtr
            {
                Parent = dialog,
                Type = DialogNodeType.Reply,
                Index = newIndex,
                IsLink = false,
                Node = newNode
            };
            parentNode.Pointers.Add(newPtr);

            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                $"Added Reply node with index {newIndex} as child of {parentNode.Type}");

            return newNode;
        }

        /// <summary>
        /// Delete a node and clean up references
        /// </summary>
        public void DeleteNode(Dialog dialog, DialogNode nodeToDelete, DialogNode? parentNode,
            Dictionary<DialogNode, (int level, DialogNode? parent)>? hierarchyInfo = null,
            ScrapManager? scrapManager = null, string? filePath = null)
        {
            if (dialog == null)
                throw new ArgumentNullException(nameof(dialog));
            if (nodeToDelete == null)
                throw new ArgumentNullException(nameof(nodeToDelete));

            // Collect all nodes to delete (including children)
            var nodesToDelete = new List<DialogNode> { nodeToDelete };
            CollectChildNodes(nodeToDelete, nodesToDelete);

            // Add to scrap if manager provided
            if (scrapManager != null && !string.IsNullOrEmpty(filePath))
            {
                scrapManager.AddToScrap(filePath, nodesToDelete, "deleted", hierarchyInfo);
            }

            // Remove pointer from parent
            if (parentNode != null)
            {
                parentNode.Pointers.RemoveAll(ptr => ptr.Node == nodeToDelete);
            }
            else
            {
                // Remove from START nodes if no parent
                dialog.Starts.RemoveAll(ptr => ptr.Node == nodeToDelete);
            }

            // Remove all nodes from dialog lists
            foreach (var node in nodesToDelete)
            {
                if (node.Type == DialogNodeType.Entry)
                {
                    dialog.Entries.Remove(node);
                }
                else
                {
                    dialog.Replies.Remove(node);
                }
            }

            // Update indices after removal
            UpdateNodeIndices(dialog);

            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"Deleted {nodesToDelete.Count} nodes (including children)");
        }

        /// <summary>
        /// Move a node up in its parent's pointer list
        /// </summary>
        public bool MoveNodeUp(DialogNode parentNode, DialogNode nodeToMove)
        {
            if (parentNode == null)
                throw new ArgumentNullException(nameof(parentNode));
            if (nodeToMove == null)
                throw new ArgumentNullException(nameof(nodeToMove));

            var ptr = parentNode.Pointers.FirstOrDefault(p => p.Node == nodeToMove);
            if (ptr == null)
                return false;

            int index = parentNode.Pointers.IndexOf(ptr);
            if (index > 0)
            {
                parentNode.Pointers.RemoveAt(index);
                parentNode.Pointers.Insert(index - 1, ptr);

                UnifiedLogger.LogApplication(LogLevel.DEBUG,
                    $"Moved node up from index {index} to {index - 1}");
                return true;
            }

            return false;
        }

        /// <summary>
        /// Move a node down in its parent's pointer list
        /// </summary>
        public bool MoveNodeDown(DialogNode parentNode, DialogNode nodeToMove)
        {
            if (parentNode == null)
                throw new ArgumentNullException(nameof(parentNode));
            if (nodeToMove == null)
                throw new ArgumentNullException(nameof(nodeToMove));

            var ptr = parentNode.Pointers.FirstOrDefault(p => p.Node == nodeToMove);
            if (ptr == null)
                return false;

            int index = parentNode.Pointers.IndexOf(ptr);
            if (index < parentNode.Pointers.Count - 1)
            {
                parentNode.Pointers.RemoveAt(index);
                parentNode.Pointers.Insert(index + 1, ptr);

                UnifiedLogger.LogApplication(LogLevel.DEBUG,
                    $"Moved node down from index {index} to {index + 1}");
                return true;
            }

            return false;
        }

        /// <summary>
        /// Update all node indices after modifications
        /// </summary>
        private void UpdateNodeIndices(Dialog dialog)
        {
            // Update Entry indices
            for (int i = 0; i < dialog.Entries.Count; i++)
            {
                var entry = dialog.Entries[i];
                // Update any pointers that reference this node
                UpdatePointersToNode(dialog, entry, DialogNodeType.Entry, (uint)i);
            }

            // Update Reply indices
            for (int i = 0; i < dialog.Replies.Count; i++)
            {
                var reply = dialog.Replies[i];
                // Update any pointers that reference this node
                UpdatePointersToNode(dialog, reply, DialogNodeType.Reply, (uint)i);
            }
        }

        /// <summary>
        /// Update all pointers that reference a specific node
        /// </summary>
        private void UpdatePointersToNode(Dialog dialog, DialogNode node, DialogNodeType type, uint newIndex)
        {
            // Update in START nodes
            foreach (var ptr in dialog.Starts.Where(p => p.Node == node))
            {
                ptr.Index = newIndex;
                ptr.Type = type;
            }

            // Update in all node pointers
            foreach (var entry in dialog.Entries)
            {
                foreach (var ptr in entry.Pointers.Where(p => p.Node == node))
                {
                    ptr.Index = newIndex;
                    ptr.Type = type;
                }
            }

            foreach (var reply in dialog.Replies)
            {
                foreach (var ptr in reply.Pointers.Where(p => p.Node == node))
                {
                    ptr.Index = newIndex;
                    ptr.Type = type;
                }
            }
        }

        /// <summary>
        /// Recursively collect all child nodes
        /// </summary>
        private void CollectChildNodes(DialogNode parent, List<DialogNode> collection)
        {
            foreach (var ptr in parent.Pointers)
            {
                if (ptr.Node != null && !collection.Contains(ptr.Node))
                {
                    collection.Add(ptr.Node);
                    CollectChildNodes(ptr.Node, collection);
                }
            }
        }
    }
}