using System.Linq;
using Xunit;
using AvaloniaGraphControl;
using DialogEditor.Models;
using DialogEditor.Services;

namespace Parley.Tests
{
    /// <summary>
    /// Unit tests for FlowchartGraphAdapter
    /// Tests the conversion from FlowchartGraph to AvaloniaGraphControl.Graph
    /// Sprint 4: Issues #336, #337
    /// </summary>
    public class FlowchartGraphAdapterTests
    {
        #region Basic Conversion Tests

        [Fact]
        public void ToAvaloniaGraph_NullGraph_ReturnsEmptyGraph()
        {
            // Act
            var result = FlowchartGraphAdapter.ToAvaloniaGraph(null);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result.Edges);
        }

        [Fact]
        public void ToAvaloniaGraph_EmptyGraph_ReturnsEmptyGraph()
        {
            // Arrange
            var flowchartGraph = new FlowchartGraph();

            // Act
            var result = FlowchartGraphAdapter.ToAvaloniaGraph(flowchartGraph);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result.Edges);
        }

        [Fact]
        public void ToAvaloniaGraph_SingleNode_CreatesSelfEdge()
        {
            // Arrange - Single node with no edges needs self-edge to appear in graph
            var flowchartGraph = new FlowchartGraph();
            flowchartGraph.AddNode(new FlowchartNode(
                id: "E0",
                nodeType: FlowchartNodeType.Entry,
                text: "Hello",
                speaker: "Owner"
            ));

            // Act
            var result = FlowchartGraphAdapter.ToAvaloniaGraph(flowchartGraph);

            // Assert
            Assert.Single(result.Edges);
            var edge = result.Edges.First();
            Assert.Same(edge.Tail, edge.Head); // Self-referencing edge
            Assert.Equal(Edge.Symbol.None, edge.HeadSymbol); // No arrow on self-edge
        }

        [Fact]
        public void ToAvaloniaGraph_TwoConnectedNodes_CreatesEdgeWithArrow()
        {
            // Arrange
            var flowchartGraph = new FlowchartGraph();
            flowchartGraph.AddNode(new FlowchartNode("ROOT", FlowchartNodeType.Root, "Root", ""));
            flowchartGraph.AddNode(new FlowchartNode("E0", FlowchartNodeType.Entry, "Hello", "Owner"));
            flowchartGraph.AddEdge(new FlowchartEdge("ROOT", "E0"));

            // Act
            var result = FlowchartGraphAdapter.ToAvaloniaGraph(flowchartGraph);

            // Assert
            Assert.Single(result.Edges);
            var edge = result.Edges.First();
            Assert.Equal(Edge.Symbol.Arrow, edge.HeadSymbol);
        }

        #endregion

        #region Edge Ordering Tests (Issue #336)

        [Fact]
        public void ToAvaloniaGraph_MultipleEdges_ReversesOrderForLeftToRightReading()
        {
            // Arrange - First edge in source should appear leftmost in result
            var flowchartGraph = new FlowchartGraph();
            flowchartGraph.AddNode(new FlowchartNode("ROOT", FlowchartNodeType.Root, "Root", ""));
            flowchartGraph.AddNode(new FlowchartNode("E0", FlowchartNodeType.Entry, "First", "Owner"));
            flowchartGraph.AddNode(new FlowchartNode("E1", FlowchartNodeType.Entry, "Second", "Owner"));
            flowchartGraph.AddNode(new FlowchartNode("E2", FlowchartNodeType.Entry, "Third", "Owner"));

            // Edges added in order: E0, E1, E2
            flowchartGraph.AddEdge(new FlowchartEdge("ROOT", "E0"));
            flowchartGraph.AddEdge(new FlowchartEdge("ROOT", "E1"));
            flowchartGraph.AddEdge(new FlowchartEdge("ROOT", "E2"));

            // Act
            var result = FlowchartGraphAdapter.ToAvaloniaGraph(flowchartGraph);

            // Assert
            Assert.Equal(3, result.Edges.Count);

            // Due to reverse ordering, E2 edge should be first in result (added last = leftmost in MSAGL)
            // E0 edge should be last (added first = rightmost after reversal, but MSAGL places later-added to left)
            // This is the reversal logic that makes first-in-source appear leftmost in visual
            var edgeTargets = result.Edges.Select(e => ((FlowchartNode)e.Head).Id).ToList();

            // First in result should be E2 (last from source, reversed)
            Assert.Equal("E2", edgeTargets[0]);
            Assert.Equal("E1", edgeTargets[1]);
            Assert.Equal("E0", edgeTargets[2]);
        }

        #endregion

        #region Complex Graph Tests

        [Fact]
        public void ToAvaloniaGraph_EntryWithReplies_CreatesAllEdges()
        {
            // Arrange
            var flowchartGraph = new FlowchartGraph();
            flowchartGraph.AddNode(new FlowchartNode("ROOT", FlowchartNodeType.Root, "Root", ""));
            flowchartGraph.AddNode(new FlowchartNode("E0", FlowchartNodeType.Entry, "What do you want?", "Owner"));
            flowchartGraph.AddNode(new FlowchartNode("R0", FlowchartNodeType.Reply, "Buy something", "PC"));
            flowchartGraph.AddNode(new FlowchartNode("R1", FlowchartNodeType.Reply, "Nevermind", "PC"));

            flowchartGraph.AddEdge(new FlowchartEdge("ROOT", "E0"));
            flowchartGraph.AddEdge(new FlowchartEdge("E0", "R0"));
            flowchartGraph.AddEdge(new FlowchartEdge("E0", "R1"));

            // Act
            var result = FlowchartGraphAdapter.ToAvaloniaGraph(flowchartGraph);

            // Assert
            Assert.Equal(3, result.Edges.Count);
        }

        [Fact]
        public void ToAvaloniaGraph_MixedConnectedAndOrphanNodes_HandlesCorrectly()
        {
            // Arrange - Some nodes connected, one orphan
            var flowchartGraph = new FlowchartGraph();
            flowchartGraph.AddNode(new FlowchartNode("ROOT", FlowchartNodeType.Root, "Root", ""));
            flowchartGraph.AddNode(new FlowchartNode("E0", FlowchartNodeType.Entry, "Connected", "Owner"));
            flowchartGraph.AddNode(new FlowchartNode("E1", FlowchartNodeType.Entry, "Orphan", "Owner"));

            flowchartGraph.AddEdge(new FlowchartEdge("ROOT", "E0"));
            // E1 has no edges - should get self-edge

            // Act
            var result = FlowchartGraphAdapter.ToAvaloniaGraph(flowchartGraph);

            // Assert
            Assert.Equal(2, result.Edges.Count);

            // One normal edge (ROOT -> E0)
            var normalEdge = result.Edges.First(e => e.Tail != e.Head);
            Assert.Equal(Edge.Symbol.Arrow, normalEdge.HeadSymbol);

            // One self-edge for orphan (E1 -> E1)
            var selfEdge = result.Edges.First(e => e.Tail == e.Head);
            Assert.Equal(Edge.Symbol.None, selfEdge.HeadSymbol);
        }

        [Fact]
        public void ToAvaloniaGraph_InvalidEdgeReference_SkipsInvalidEdge()
        {
            // Arrange - Edge references non-existent node
            var flowchartGraph = new FlowchartGraph();
            flowchartGraph.AddNode(new FlowchartNode("E0", FlowchartNodeType.Entry, "Exists", "Owner"));
            flowchartGraph.AddEdge(new FlowchartEdge("E0", "NONEXISTENT"));

            // Act
            var result = FlowchartGraphAdapter.ToAvaloniaGraph(flowchartGraph);

            // Assert - Invalid edge is skipped (target doesn't exist)
            // Note: Orphan detection checks FlowchartGraph.Edges, not result.Edges
            // So E0 appears in an edge and doesn't get a self-edge
            Assert.Empty(result.Edges);
        }

        #endregion

        #region Node Data Preservation Tests

        [Fact]
        public void ToAvaloniaGraph_PreservesFlowchartNodeAsEdgeData()
        {
            // Arrange
            var flowchartGraph = new FlowchartGraph();
            var rootNode = new FlowchartNode("ROOT", FlowchartNodeType.Root, "Root", "");
            var entryNode = new FlowchartNode("E0", FlowchartNodeType.Entry, "Hello", "Merchant");

            flowchartGraph.AddNode(rootNode);
            flowchartGraph.AddNode(entryNode);
            flowchartGraph.AddEdge(new FlowchartEdge("ROOT", "E0"));

            // Act
            var result = FlowchartGraphAdapter.ToAvaloniaGraph(flowchartGraph);

            // Assert - Edge head/tail should be our FlowchartNode objects
            var edge = result.Edges.First();
            Assert.IsType<FlowchartNode>(edge.Head);
            Assert.IsType<FlowchartNode>(edge.Tail);

            var headNode = (FlowchartNode)edge.Head;
            Assert.Equal("E0", headNode.Id);
            Assert.Equal("Merchant", headNode.Speaker);
        }

        #endregion
    }
}