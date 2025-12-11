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

            // Handle orphan nodes (nodes with no edges) - one-liners and disconnected entries
            // AvaloniaGraphControl discovers nodes through edges, so isolated nodes need
            // a dummy self-edge to appear in the graph
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

                // If node has no edges, add a self-referencing edge so it appears in the graph
                // This handles one-liners and other disconnected nodes
                if (!hasEdge)
                {
                    graph.Edges.Add(new Edge(node, node, headSymbol: Edge.Symbol.None));
                }
            }

            return graph;
        }
    }
}
