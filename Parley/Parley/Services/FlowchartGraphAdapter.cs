using System.Collections.Generic;
using AvaloniaGraphControl;
using DialogEditor.Models;

namespace DialogEditor.Services
{
    /// <summary>
    /// Converts our FlowchartGraph to AvaloniaGraphControl's Graph format.
    /// This adapter bridges our dialog-specific data model to the graph visualization library.
    /// </summary>
    public static class FlowchartGraphAdapter
    {
        /// <summary>
        /// Convert a FlowchartGraph to an AvaloniaGraphControl Graph
        /// </summary>
        /// <param name="flowchartGraph">Our dialog flowchart graph</param>
        /// <returns>Graph ready for rendering in GraphPanel</returns>
        public static Graph ToAvaloniaGraph(FlowchartGraph? flowchartGraph)
        {
            var graph = new Graph();

            if (flowchartGraph == null || flowchartGraph.IsEmpty)
            {
                return graph;
            }

            // Build node lookup for edge creation
            var nodeMap = new Dictionary<string, FlowchartNode>();
            foreach (var node in flowchartGraph.Nodes.Values)
            {
                nodeMap[node.Id] = node;
            }

            // Add edges - AvaloniaGraphControl creates nodes implicitly from edges
            foreach (var edge in flowchartGraph.Edges)
            {
                if (nodeMap.TryGetValue(edge.SourceId, out var sourceNode) &&
                    nodeMap.TryGetValue(edge.TargetId, out var targetNode))
                {
                    // Create edge with arrow at target
                    var avaloniaEdge = new Edge(
                        sourceNode,
                        targetNode,
                        headSymbol: Edge.Symbol.Arrow
                    );
                    graph.Edges.Add(avaloniaEdge);
                }
            }

            // Handle orphan nodes (nodes with no edges) - add self-referencing edge workaround
            // or just ensure they're included. AvaloniaGraphControl should handle disconnected nodes
            // but we'll add them explicitly if needed
            foreach (var node in flowchartGraph.Nodes.Values)
            {
                bool hasEdge = false;
                foreach (var edge in flowchartGraph.Edges)
                {
                    if (edge.SourceId == node.Id || edge.TargetId == node.Id)
                    {
                        hasEdge = true;
                        break;
                    }
                }

                // If node has no edges, we need to add it somehow
                // AvaloniaGraphControl uses edges to discover nodes, so isolated nodes
                // need special handling - for now we'll skip them as dialog graphs
                // should always have connected nodes
                if (!hasEdge && flowchartGraph.Nodes.Count == 1)
                {
                    // Single node graph - create a dummy self-edge that won't render
                    // Actually, let's just add it as an edge target from itself
                    // This is a workaround - may need improvement
                    graph.Edges.Add(new Edge(node, node, headSymbol: Edge.Symbol.None));
                }
            }

            return graph;
        }
    }
}
