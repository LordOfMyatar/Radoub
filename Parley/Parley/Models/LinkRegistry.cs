using System;
using System.Collections.Generic;
using System.Linq;
using DialogEditor.Services;
using Radoub.Formats.Logging;

namespace DialogEditor.Models
{
    /// <summary>
    /// Tracks all DialogPtr references between nodes to ensure proper index management
    /// during copy/paste/delete operations. Part of the fix for Issue #6.
    /// </summary>
    public class LinkRegistry
    {
        // Track all pointers that reference each node
        private readonly Dictionary<DialogNode, List<DialogPtr>> _incomingLinks = new();

        // Track all pointers from each node
        private readonly Dictionary<DialogNode, List<DialogPtr>> _outgoingLinks = new();

        // Track which nodes are originals vs links for shared content
        private readonly Dictionary<DialogNode, DialogPtr> _originalPointers = new();

        /// <summary>
        /// Register a pointer in the link tracking system
        /// </summary>
        public void RegisterLink(DialogPtr ptr)
        {
            if (ptr?.Node == null) return;

            // Track outgoing link from parent node
            if (ptr.Parent != null)
            {
                // Find the node that contains this pointer
                DialogNode? parentNode = FindParentNode(ptr, ptr.Parent);
                if (parentNode != null)
                {
                    if (!_outgoingLinks.ContainsKey(parentNode))
                        _outgoingLinks[parentNode] = new List<DialogPtr>();

                    if (!_outgoingLinks[parentNode].Contains(ptr))
                        _outgoingLinks[parentNode].Add(ptr);
                }
            }

            // Track incoming link to target node
            if (!_incomingLinks.ContainsKey(ptr.Node))
                _incomingLinks[ptr.Node] = new List<DialogPtr>();

            if (!_incomingLinks[ptr.Node].Contains(ptr))
                _incomingLinks[ptr.Node].Add(ptr);

            // Track original vs link status - respect existing IsLink flags
            // CRITICAL FIX (Issue #28): Don't infer from registration order, trust the flags
            if (!_originalPointers.ContainsKey(ptr.Node))
            {
                if (!ptr.IsLink)
                {
                    _originalPointers[ptr.Node] = ptr;
                }
            }

            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                $"LinkRegistry: Registered {(ptr.IsLink ? "link" : "original")} pointer to node '{ptr.Node.DisplayText}'");
        }

        /// <summary>
        /// Unregister a pointer from the tracking system
        /// </summary>
        public void UnregisterLink(DialogPtr ptr)
        {
            if (ptr?.Node == null) return;

            // Remove from outgoing links
            if (ptr.Parent != null)
            {
                DialogNode? parentNode = FindParentNode(ptr, ptr.Parent);
                if (parentNode != null && _outgoingLinks.ContainsKey(parentNode))
                {
                    _outgoingLinks[parentNode].Remove(ptr);
                    if (_outgoingLinks[parentNode].Count == 0)
                        _outgoingLinks.Remove(parentNode);
                }
            }

            // Remove from incoming links
            if (_incomingLinks.ContainsKey(ptr.Node))
            {
                _incomingLinks[ptr.Node].Remove(ptr);
                if (_incomingLinks[ptr.Node].Count == 0)
                    _incomingLinks.Remove(ptr.Node);
            }

            // Update original pointer tracking
            if (!ptr.IsLink && _originalPointers.ContainsKey(ptr.Node) && _originalPointers[ptr.Node] == ptr)
            {
                _originalPointers.Remove(ptr.Node);

                // If there are other non-link pointers, promote one to be the original
                var remainingPointers = GetLinksTo(ptr.Node).Where(p => !p.IsLink).ToList();
                if (remainingPointers.Any())
                {
                    _originalPointers[ptr.Node] = remainingPointers.First();
                }
            }

            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                $"LinkRegistry: Unregistered pointer to node '{ptr.Node.DisplayText}'");
        }

        /// <summary>
        /// Get all pointers that reference a specific node
        /// </summary>
        public List<DialogPtr> GetLinksTo(DialogNode node)
        {
            return _incomingLinks.ContainsKey(node)
                ? new List<DialogPtr>(_incomingLinks[node])
                : new List<DialogPtr>();
        }

        /// <summary>
        /// Get all pointers from a specific node
        /// </summary>
        public List<DialogPtr> GetLinksFrom(DialogNode node)
        {
            return _outgoingLinks.ContainsKey(node)
                ? new List<DialogPtr>(_outgoingLinks[node])
                : new List<DialogPtr>();
        }

        /// <summary>
        /// Update all pointer indices when a node's position changes in its collection
        /// </summary>
        public void UpdateNodeIndex(DialogNode node, uint newIndex, DialogNodeType nodeType)
        {
            var incomingPointers = GetLinksTo(node);
            var updateCount = 0;

            foreach (var ptr in incomingPointers)
            {
                if (ptr.Type == nodeType && ptr.Index != newIndex)
                {
                    UnifiedLogger.LogApplication(LogLevel.DEBUG,
                        $"LinkRegistry: Updating pointer index from {ptr.Index} to {newIndex} for node '{node.DisplayText}'");

                    ptr.Index = newIndex;
                    updateCount++;
                }
            }

            if (updateCount > 0)
            {
                UnifiedLogger.LogApplication(LogLevel.INFO,
                    $"LinkRegistry: Updated {updateCount} pointer indices for node '{node.DisplayText}'");
            }
        }

        /// <summary>
        /// Rebuild the entire registry from a Dialog structure
        /// </summary>
        public void RebuildFromDialog(Dialog dialog)
        {
            Clear();

            // Register all entry pointers
            foreach (var entry in dialog.Entries)
            {
                foreach (var ptr in entry.Pointers)
                {
                    RegisterLink(ptr);
                }
            }

            // Register all reply pointers
            foreach (var reply in dialog.Replies)
            {
                foreach (var ptr in reply.Pointers)
                {
                    RegisterLink(ptr);
                }
            }

            // Register all start pointers
            foreach (var startPtr in dialog.Starts)
            {
                RegisterLink(startPtr);
            }

            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"LinkRegistry: Rebuilt with {_incomingLinks.Count} nodes tracked, " +
                $"{_incomingLinks.Values.Sum(l => l.Count)} total pointers");
        }

        /// <summary>
        /// Check if a node has any incoming links
        /// </summary>
        public bool HasIncomingLinks(DialogNode node)
        {
            return _incomingLinks.ContainsKey(node) && _incomingLinks[node].Count > 0;
        }

        /// <summary>
        /// Check if a node is referenced only by links (no original pointer)
        /// </summary>
        public bool IsOrphanedLink(DialogNode node)
        {
            var links = GetLinksTo(node);
            return links.Any() && links.All(p => p.IsLink);
        }

        /// <summary>
        /// Get the original (non-link) pointer to a node, if any
        /// </summary>
        public DialogPtr? GetOriginalPointer(DialogNode node)
        {
            return _originalPointers.ContainsKey(node) ? _originalPointers[node] : null;
        }

        /// <summary>
        /// Validate all pointer indices match actual positions in collections
        /// </summary>
        public List<string> ValidateIndices(Dialog dialog)
        {
            var errors = new List<string>();

            // Check entry indices
            for (uint i = 0; i < dialog.Entries.Count; i++)
            {
                var entry = dialog.Entries[(int)i];
                var pointers = GetLinksTo(entry).Where(p => p.Type == DialogNodeType.Entry);

                foreach (var ptr in pointers)
                {
                    if (ptr.Index != i)
                    {
                        errors.Add($"Entry '{entry.DisplayText}' at position {i} has pointer with index {ptr.Index}");
                    }
                }
            }

            // Check reply indices
            for (uint i = 0; i < dialog.Replies.Count; i++)
            {
                var reply = dialog.Replies[(int)i];
                var pointers = GetLinksTo(reply).Where(p => p.Type == DialogNodeType.Reply);

                foreach (var ptr in pointers)
                {
                    if (ptr.Index != i)
                    {
                        errors.Add($"Reply '{reply.DisplayText}' at position {i} has pointer with index {ptr.Index}");
                    }
                }
            }

            return errors;
        }

        /// <summary>
        /// Clear all tracked links
        /// </summary>
        public void Clear()
        {
            _incomingLinks.Clear();
            _outgoingLinks.Clear();
            _originalPointers.Clear();
        }

        /// <summary>
        /// Find which node contains a specific pointer
        /// </summary>
        private DialogNode? FindParentNode(DialogPtr ptr, Dialog dialog)
        {
            // Check entries
            foreach (var entry in dialog.Entries)
            {
                if (entry.Pointers.Contains(ptr))
                    return entry;
            }

            // Check replies
            foreach (var reply in dialog.Replies)
            {
                if (reply.Pointers.Contains(ptr))
                    return reply;
            }

            return null;
        }

        /// <summary>
        /// Get statistics about the link registry
        /// </summary>
        public (int NodeCount, int PointerCount, int LinkCount, int OrphanCount) GetStatistics()
        {
            var nodeCount = _incomingLinks.Count;
            var pointerCount = _incomingLinks.Values.Sum(l => l.Count);
            var linkCount = _incomingLinks.Values.SelectMany(l => l).Count(p => p.IsLink);
            var orphanCount = _incomingLinks.Keys.Count(n => IsOrphanedLink(n));

            return (nodeCount, pointerCount, linkCount, orphanCount);
        }
    }
}