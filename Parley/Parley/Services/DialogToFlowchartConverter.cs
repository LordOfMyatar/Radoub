using System;
using System.Collections.Generic;
using DialogEditor.Models;

namespace DialogEditor.Services
{
    /// <summary>
    /// Converts Dialog tree structure to FlowchartGraph for visualization.
    /// Handles circular references and link nodes gracefully.
    /// </summary>
    public class DialogToFlowchartConverter
    {
        private readonly HashSet<DialogNode> _visitedNodes = new();
        private readonly Dictionary<DialogNode, string> _nodeIdMap = new();
        private int _linkCounter = 0;

        /// <summary>
        /// Convert a Dialog to a FlowchartGraph
        /// </summary>
        /// <param name="dialog">The dialog to convert</param>
        /// <param name="fileName">Optional filename for display</param>
        /// <returns>A FlowchartGraph representation of the dialog</returns>
        public FlowchartGraph Convert(Dialog? dialog, string? fileName = null)
        {
            var graph = new FlowchartGraph
            {
                SourceFileName = fileName
            };

            if (dialog == null)
            {
                return graph;
            }

            // Reset state for new conversion
            _visitedNodes.Clear();
            _nodeIdMap.Clear();
            _linkCounter = 0;

            // First pass: assign IDs to all nodes
            AssignNodeIds(dialog);

            // Create ROOT node - all starting entries connect from ROOT
            const string rootId = "ROOT";
            var rootNode = new FlowchartNode(
                id: rootId,
                nodeType: FlowchartNodeType.Root,
                text: System.IO.Path.GetFileNameWithoutExtension(fileName) ?? "ROOT",
                speaker: string.Empty,
                hasCondition: false,
                hasAction: false,
                isLink: false,
                linkTargetId: null,
                originalNode: null,
                originalPointer: null
            );
            graph.AddNode(rootNode);
            graph.RootNodeIds.Add(rootId);

            // Process starting points (entries that branch from ROOT)
            foreach (var startPtr in dialog.Starts)
            {
                if (startPtr?.Node == null) continue;

                var nodeId = GetNodeId(startPtr.Node);
                var isConditional = !string.IsNullOrEmpty(startPtr.ScriptAppears);

                // Edge from ROOT to starting entry
                graph.AddEdge(new FlowchartEdge(
                    sourceId: rootId,
                    targetId: nodeId,
                    isConditional: isConditional,
                    isLinkEdge: false,
                    label: isConditional ? startPtr.ScriptAppears : null
                ));

                // Process this starting node and its children
                ProcessNode(startPtr.Node, startPtr, graph);
            }

            return graph;
        }

        /// <summary>
        /// Assign unique IDs to all nodes in the dialog
        /// </summary>
        private void AssignNodeIds(Dialog dialog)
        {
            // Assign IDs to entries
            for (int i = 0; i < dialog.Entries.Count; i++)
            {
                var node = dialog.Entries[i];
                _nodeIdMap[node] = $"E{i}";
            }

            // Assign IDs to replies
            for (int i = 0; i < dialog.Replies.Count; i++)
            {
                var node = dialog.Replies[i];
                _nodeIdMap[node] = $"R{i}";
            }
        }

        /// <summary>
        /// Get the ID for a node
        /// </summary>
        private string GetNodeId(DialogNode node)
        {
            if (_nodeIdMap.TryGetValue(node, out var id))
            {
                return id;
            }

            // Fallback for nodes not in the main lists (shouldn't happen in valid dialogs)
            var fallbackId = $"U{node.GetHashCode():X8}";
            _nodeIdMap[node] = fallbackId;
            return fallbackId;
        }

        /// <summary>
        /// Process a node and its children recursively
        /// </summary>
        private void ProcessNode(DialogNode node, DialogPtr? sourcePointer, FlowchartGraph graph)
        {
            var nodeId = GetNodeId(node);

            // Check for circular reference
            if (_visitedNodes.Contains(node))
            {
                // Already processed - don't recurse, but we still need to handle
                // incoming edges from other nodes (handled by caller)
                return;
            }

            _visitedNodes.Add(node);

            // Create flowchart node
            var flowchartNode = CreateFlowchartNode(node, nodeId, sourcePointer);
            graph.AddNode(flowchartNode);

            // Process children (pointers to other nodes)
            foreach (var childPtr in node.Pointers)
            {
                if (childPtr?.Node == null) continue;

                var childNodeId = GetNodeId(childPtr.Node);
                var isConditional = !string.IsNullOrEmpty(childPtr.ScriptAppears);

                if (childPtr.IsLink)
                {
                    // This is a link to an existing node
                    // Create a link node that points to the target
                    var linkId = $"L{_linkCounter++}";
                    var linkNode = new FlowchartNode(
                        id: linkId,
                        nodeType: FlowchartNodeType.Link,
                        text: childPtr.Node.DisplayText,
                        speaker: childPtr.Node.Speaker,  // Use raw Speaker tag (SpeakerVisualHelper expects empty for Owner)
                        hasCondition: isConditional,
                        hasAction: false,
                        isLink: true,
                        linkTargetId: childNodeId,
                        originalNode: childPtr.Node,
                        originalPointer: childPtr
                    );
                    graph.AddNode(linkNode);

                    // Edge from parent to link node
                    graph.AddEdge(new FlowchartEdge(
                        sourceId: nodeId,
                        targetId: linkId,
                        isConditional: isConditional,
                        isLinkEdge: true,
                        label: isConditional ? childPtr.ScriptAppears : null
                    ));

                    // Ensure the target node exists (it may only be reachable via links)
                    // Process it if not already visited - this creates the node in the graph
                    if (!_visitedNodes.Contains(childPtr.Node))
                    {
                        ProcessNode(childPtr.Node, childPtr, graph);
                    }

                    // Note: We don't create an edge from link node to actual target
                    // The link node is just a visual indicator that this path leads to
                    // an already-existing node elsewhere in the graph. Adding an edge
                    // would incorrectly affect the Sugiyama layout by creating a
                    // parent-child relationship where the target should remain in its
                    // original position. The linkTargetId on the node provides the reference.
                }
                else
                {
                    // Regular child node
                    // Add edge from parent to child
                    graph.AddEdge(new FlowchartEdge(
                        sourceId: nodeId,
                        targetId: childNodeId,
                        isConditional: isConditional,
                        isLinkEdge: false,
                        label: isConditional ? childPtr.ScriptAppears : null
                    ));

                    // Recursively process the child (if not already visited)
                    ProcessNode(childPtr.Node, childPtr, graph);
                }
            }
        }

        /// <summary>
        /// Create a FlowchartNode from a DialogNode
        /// </summary>
        private FlowchartNode CreateFlowchartNode(DialogNode node, string nodeId, DialogPtr? sourcePointer)
        {
            var nodeType = node.Type == DialogNodeType.Entry
                ? FlowchartNodeType.Entry
                : FlowchartNodeType.Reply;

            var hasCondition = sourcePointer != null && !string.IsNullOrEmpty(sourcePointer.ScriptAppears);
            var hasAction = !string.IsNullOrEmpty(node.ScriptAction);

            return new FlowchartNode(
                id: nodeId,
                nodeType: nodeType,
                text: node.DisplayText,
                speaker: node.Speaker,  // Use raw Speaker tag (SpeakerVisualHelper expects empty for Owner)
                hasCondition: hasCondition,
                hasAction: hasAction,
                isLink: false,
                linkTargetId: null,
                originalNode: node,
                originalPointer: sourcePointer
            );
        }

        /// <summary>
        /// Create an empty graph (utility method for testing/UI)
        /// </summary>
        public static FlowchartGraph CreateEmpty(string? fileName = null)
        {
            return new FlowchartGraph
            {
                SourceFileName = fileName
            };
        }
    }
}
