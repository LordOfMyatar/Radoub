using System;
using System.Collections.Generic;

namespace DialogEditor.Models
{
    /// <summary>
    /// Node types for flowchart visualization
    /// </summary>
    public enum FlowchartNodeType
    {
        /// <summary>NPC/Owner dialog entry</summary>
        Entry,
        /// <summary>PC response option</summary>
        Reply,
        /// <summary>Link to another node (not a real node, just a reference)</summary>
        Link
    }

    /// <summary>
    /// Represents a node in the flowchart visualization.
    /// This is a view model for AvaloniaGraphControl - the graph panel uses
    /// DataTemplates to render nodes based on their type.
    /// </summary>
    public class FlowchartNode
    {
        /// <summary>
        /// Unique identifier for this node (matches DialogNode index in parent collection)
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// The type of node (Entry, Reply, or Link)
        /// </summary>
        public FlowchartNodeType NodeType { get; }

        /// <summary>
        /// Display text for the node (truncated dialog text)
        /// </summary>
        public string Text { get; }

        /// <summary>
        /// Speaker tag (for Entry nodes: NPC tag or "Owner"; for Reply: "PC")
        /// </summary>
        public string Speaker { get; }

        /// <summary>
        /// True if this node has a conditional script (ScriptAppears)
        /// </summary>
        public bool HasCondition { get; }

        /// <summary>
        /// True if this node has an action script (ScriptAction)
        /// </summary>
        public bool HasAction { get; }

        /// <summary>
        /// True if this is a link node (points to another node rather than containing content)
        /// </summary>
        public bool IsLink { get; }

        /// <summary>
        /// For link nodes: the ID of the target node
        /// </summary>
        public string? LinkTargetId { get; }

        /// <summary>
        /// Reference to the original DialogNode (for selection sync)
        /// </summary>
        public DialogNode? OriginalNode { get; }

        /// <summary>
        /// Reference to the original DialogPtr (for link nodes)
        /// </summary>
        public DialogPtr? OriginalPointer { get; }

        public FlowchartNode(
            string id,
            FlowchartNodeType nodeType,
            string text,
            string speaker,
            bool hasCondition = false,
            bool hasAction = false,
            bool isLink = false,
            string? linkTargetId = null,
            DialogNode? originalNode = null,
            DialogPtr? originalPointer = null)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            NodeType = nodeType;
            Text = text ?? string.Empty;
            Speaker = speaker ?? string.Empty;
            HasCondition = hasCondition;
            HasAction = hasAction;
            IsLink = isLink;
            LinkTargetId = linkTargetId;
            OriginalNode = originalNode;
            OriginalPointer = originalPointer;
        }

        /// <summary>
        /// Short display text (truncated to ~30 chars for flowchart display)
        /// </summary>
        public string ShortText
        {
            get
            {
                if (string.IsNullOrEmpty(Text))
                    return IsLink ? $"→ {LinkTargetId}" : "[empty]";

                const int maxLength = 30;
                return Text.Length <= maxLength
                    ? Text
                    : Text.Substring(0, maxLength - 3) + "...";
            }
        }

        public override string ToString() => $"{NodeType}:{Id} - {ShortText}";

        public override bool Equals(object? obj)
        {
            return obj is FlowchartNode other && Id == other.Id;
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }
    }

    /// <summary>
    /// Represents an edge (connection) in the flowchart.
    /// Edges connect parent nodes to child nodes following dialog flow.
    /// </summary>
    public class FlowchartEdge
    {
        /// <summary>
        /// Source node ID
        /// </summary>
        public string SourceId { get; }

        /// <summary>
        /// Target node ID
        /// </summary>
        public string TargetId { get; }

        /// <summary>
        /// True if this edge represents a conditional path (has ScriptAppears)
        /// </summary>
        public bool IsConditional { get; }

        /// <summary>
        /// True if this edge leads to a link node
        /// </summary>
        public bool IsLinkEdge { get; }

        /// <summary>
        /// Optional label for the edge (e.g., condition script name)
        /// </summary>
        public string? Label { get; }

        public FlowchartEdge(
            string sourceId,
            string targetId,
            bool isConditional = false,
            bool isLinkEdge = false,
            string? label = null)
        {
            SourceId = sourceId ?? throw new ArgumentNullException(nameof(sourceId));
            TargetId = targetId ?? throw new ArgumentNullException(nameof(targetId));
            IsConditional = isConditional;
            IsLinkEdge = isLinkEdge;
            Label = label;
        }

        public override string ToString() => $"{SourceId} → {TargetId}";

        public override bool Equals(object? obj)
        {
            return obj is FlowchartEdge other
                && SourceId == other.SourceId
                && TargetId == other.TargetId;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(SourceId, TargetId);
        }
    }

    /// <summary>
    /// Complete flowchart graph data structure.
    /// This is an intermediate representation that can be converted to AvaloniaGraphControl's Graph.
    /// </summary>
    public class FlowchartGraph
    {
        /// <summary>
        /// All nodes in the flowchart, keyed by ID for fast lookup
        /// </summary>
        public Dictionary<string, FlowchartNode> Nodes { get; } = new();

        /// <summary>
        /// All edges in the flowchart
        /// </summary>
        public List<FlowchartEdge> Edges { get; } = new();

        /// <summary>
        /// IDs of root nodes (entry points to the dialog - typically StartingList entries)
        /// </summary>
        public List<string> RootNodeIds { get; } = new();

        /// <summary>
        /// Source dialog filename (for display purposes)
        /// </summary>
        public string? SourceFileName { get; set; }

        /// <summary>
        /// True if the graph is empty
        /// </summary>
        public bool IsEmpty => Nodes.Count == 0;

        /// <summary>
        /// Add a node to the graph
        /// </summary>
        public void AddNode(FlowchartNode node)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));
            Nodes[node.Id] = node;
        }

        /// <summary>
        /// Add an edge to the graph
        /// </summary>
        public void AddEdge(FlowchartEdge edge)
        {
            if (edge == null) throw new ArgumentNullException(nameof(edge));
            Edges.Add(edge);
        }

        /// <summary>
        /// Get a node by ID
        /// </summary>
        public FlowchartNode? GetNode(string id)
        {
            return Nodes.TryGetValue(id, out var node) ? node : null;
        }

        /// <summary>
        /// Get all edges originating from a node
        /// </summary>
        public IEnumerable<FlowchartEdge> GetOutgoingEdges(string nodeId)
        {
            foreach (var edge in Edges)
            {
                if (edge.SourceId == nodeId)
                    yield return edge;
            }
        }

        /// <summary>
        /// Get all edges targeting a node
        /// </summary>
        public IEnumerable<FlowchartEdge> GetIncomingEdges(string nodeId)
        {
            foreach (var edge in Edges)
            {
                if (edge.TargetId == nodeId)
                    yield return edge;
            }
        }

        public override string ToString() => $"FlowchartGraph: {Nodes.Count} nodes, {Edges.Count} edges";
    }
}
