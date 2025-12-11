using System.Linq;
using Xunit;
using DialogEditor.Models;
using DialogEditor.Services;

namespace Parley.Tests
{
    /// <summary>
    /// Unit tests for DialogToFlowchartConverter
    /// Tests cover the Sprint 1 acceptance criteria for native flowchart foundation
    /// </summary>
    public class DialogToFlowchartConverterTests
    {
        private readonly DialogToFlowchartConverter _converter;

        public DialogToFlowchartConverterTests()
        {
            _converter = new DialogToFlowchartConverter();
        }

        #region Empty Dialog Tests

        [Fact]
        public void Convert_NullDialog_ReturnsEmptyGraph()
        {
            // Act
            var graph = _converter.Convert(null);

            // Assert
            Assert.NotNull(graph);
            Assert.True(graph.IsEmpty);
            Assert.Empty(graph.Nodes);
            Assert.Empty(graph.Edges);
            Assert.Empty(graph.RootNodeIds);
        }

        [Fact]
        public void Convert_EmptyDialog_ReturnsEmptyGraph()
        {
            // Arrange
            var dialog = new Dialog();

            // Act
            var graph = _converter.Convert(dialog);

            // Assert
            Assert.NotNull(graph);
            Assert.True(graph.IsEmpty);
            Assert.Empty(graph.Nodes);
            Assert.Empty(graph.Edges);
            Assert.Empty(graph.RootNodeIds);
        }

        [Fact]
        public void Convert_WithFileName_SetsSourceFileName()
        {
            // Arrange
            var dialog = new Dialog();
            var fileName = "test_dialog.dlg";

            // Act
            var graph = _converter.Convert(dialog, fileName);

            // Assert
            Assert.Equal(fileName, graph.SourceFileName);
        }

        #endregion

        #region Single Node Tests

        [Fact]
        public void Convert_SingleEntry_ReturnsSingleNode()
        {
            // Arrange
            var dialog = new Dialog();
            var startPtr = dialog.Add(); // Creates a starting entry node
            startPtr!.Node!.Text.Add(0, "Hello, adventurer!");
            dialog.AddNodeInternal(startPtr.Node, DialogNodeType.Entry);

            // Act
            var graph = _converter.Convert(dialog);

            // Assert
            Assert.Single(graph.Nodes);
            Assert.Empty(graph.Edges); // Single node has no outgoing edges
            Assert.Single(graph.RootNodeIds);

            var node = graph.Nodes.Values.First();
            Assert.Equal(FlowchartNodeType.Entry, node.NodeType);
            Assert.Equal("Hello, adventurer!", node.Text);
            Assert.False(node.IsLink);
        }

        [Fact]
        public void Convert_EntryNode_HasCorrectSpeaker()
        {
            // Arrange
            var dialog = new Dialog();
            var startPtr = dialog.Add();
            startPtr!.Node!.Text.Add(0, "Greetings.");
            startPtr.Node.Speaker = "Merchant";
            dialog.AddNodeInternal(startPtr.Node, DialogNodeType.Entry);

            // Act
            var graph = _converter.Convert(dialog);

            // Assert
            var node = graph.Nodes.Values.First();
            Assert.Equal("Merchant", node.Speaker);
        }

        [Fact]
        public void Convert_EntryWithAction_SetsHasAction()
        {
            // Arrange
            var dialog = new Dialog();
            var startPtr = dialog.Add();
            startPtr!.Node!.Text.Add(0, "Let me open my shop.");
            startPtr.Node.ScriptAction = "open_store";
            dialog.AddNodeInternal(startPtr.Node, DialogNodeType.Entry);

            // Act
            var graph = _converter.Convert(dialog);

            // Assert
            var node = graph.Nodes.Values.First();
            Assert.True(node.HasAction);
        }

        #endregion

        #region Entry With Replies Tests

        [Fact]
        public void Convert_EntryWithReplies_CreatesNodesAndEdges()
        {
            // Arrange
            var dialog = new Dialog();

            // Create entry node
            var startPtr = dialog.Add();
            startPtr!.Node!.Text.Add(0, "What do you want?");
            dialog.AddNodeInternal(startPtr.Node, DialogNodeType.Entry);

            // Add reply 1
            var reply1 = dialog.CreateNode(DialogNodeType.Reply);
            reply1!.Text.Add(0, "I want to buy something.");
            dialog.AddNodeInternal(reply1, DialogNodeType.Reply);

            var replyPtr1 = dialog.CreatePtr();
            replyPtr1!.Type = DialogNodeType.Reply;
            replyPtr1.Node = reply1;
            startPtr.Node.Pointers.Add(replyPtr1);

            // Add reply 2
            var reply2 = dialog.CreateNode(DialogNodeType.Reply);
            reply2!.Text.Add(0, "Never mind.");
            dialog.AddNodeInternal(reply2, DialogNodeType.Reply);

            var replyPtr2 = dialog.CreatePtr();
            replyPtr2!.Type = DialogNodeType.Reply;
            replyPtr2.Node = reply2;
            startPtr.Node.Pointers.Add(replyPtr2);

            // Act
            var graph = _converter.Convert(dialog);

            // Assert
            Assert.Equal(3, graph.Nodes.Count); // 1 entry + 2 replies
            Assert.Equal(2, graph.Edges.Count); // 2 edges from entry to replies
            Assert.Single(graph.RootNodeIds);

            // Verify node types
            var entryNodes = graph.Nodes.Values.Where(n => n.NodeType == FlowchartNodeType.Entry).ToList();
            var replyNodes = graph.Nodes.Values.Where(n => n.NodeType == FlowchartNodeType.Reply).ToList();
            Assert.Single(entryNodes);
            Assert.Equal(2, replyNodes.Count);

            // Verify edges connect entry to replies
            var entryNode = entryNodes.First();
            var outgoingEdges = graph.GetOutgoingEdges(entryNode.Id).ToList();
            Assert.Equal(2, outgoingEdges.Count);
        }

        [Fact]
        public void Convert_ConditionalReply_SetsIsConditional()
        {
            // Arrange
            var dialog = new Dialog();

            var startPtr = dialog.Add();
            startPtr!.Node!.Text.Add(0, "Hello");
            dialog.AddNodeInternal(startPtr.Node, DialogNodeType.Entry);

            var reply = dialog.CreateNode(DialogNodeType.Reply);
            reply!.Text.Add(0, "Secret option");
            dialog.AddNodeInternal(reply, DialogNodeType.Reply);

            var replyPtr = dialog.CreatePtr();
            replyPtr!.Type = DialogNodeType.Reply;
            replyPtr.Node = reply;
            replyPtr.ScriptAppears = "check_secret_knowledge";
            startPtr.Node.Pointers.Add(replyPtr);

            // Act
            var graph = _converter.Convert(dialog);

            // Assert
            var edge = graph.Edges.First();
            Assert.True(edge.IsConditional);
            Assert.Equal("check_secret_knowledge", edge.Label);
        }

        #endregion

        #region Link Node Tests

        [Fact]
        public void Convert_LinkNode_CreatesLinkNodeAndEdges()
        {
            // Arrange
            var dialog = new Dialog();

            // Create starting entry
            var startPtr = dialog.Add();
            startPtr!.Node!.Text.Add(0, "Let me explain...");
            dialog.AddNodeInternal(startPtr.Node, DialogNodeType.Entry);

            // Create reply that user can choose
            var reply = dialog.CreateNode(DialogNodeType.Reply);
            reply!.Text.Add(0, "Tell me more.");
            dialog.AddNodeInternal(reply, DialogNodeType.Reply);

            var replyPtr = dialog.CreatePtr();
            replyPtr!.Type = DialogNodeType.Reply;
            replyPtr.Node = reply;
            startPtr.Node.Pointers.Add(replyPtr);

            // Create link from reply back to start (common pattern for loops)
            var linkPtr = dialog.CreatePtr();
            linkPtr!.Type = DialogNodeType.Entry;
            linkPtr.Node = startPtr.Node; // Link back to the starting entry
            linkPtr.IsLink = true;
            reply.Pointers.Add(linkPtr);

            // Act
            var graph = _converter.Convert(dialog);

            // Assert
            // Should have: start entry, reply, and a link node (for the back-link)
            Assert.Equal(3, graph.Nodes.Count);

            var linkNodes = graph.Nodes.Values.Where(n => n.IsLink).ToList();
            Assert.Single(linkNodes);

            var linkNode = linkNodes.First();
            Assert.Equal(FlowchartNodeType.Link, linkNode.NodeType);
            Assert.NotNull(linkNode.LinkTargetId);

            // Verify edges: start->reply, reply->link
            // Note: We no longer create link->target edges (it was causing Sugiyama layout issues)
            // The linkTargetId property on the link node provides the reference instead
            Assert.Equal(2, graph.Edges.Count);
            var linkEdges = graph.Edges.Where(e => e.IsLinkEdge).ToList();
            Assert.Single(linkEdges); // Only reply->link edge is marked as link edge
        }

        #endregion

        #region Circular Reference Tests

        [Fact]
        public void Convert_CircularReference_DoesNotInfiniteLoop()
        {
            // Arrange
            var dialog = new Dialog();

            // Create entry A
            var entryA = dialog.CreateNode(DialogNodeType.Entry);
            entryA!.Text.Add(0, "Entry A");
            dialog.AddNodeInternal(entryA, DialogNodeType.Entry);

            // Create reply that points back to entry A
            var reply = dialog.CreateNode(DialogNodeType.Reply);
            reply!.Text.Add(0, "Go back");
            dialog.AddNodeInternal(reply, DialogNodeType.Reply);

            // Entry A -> Reply
            var replyPtr = dialog.CreatePtr();
            replyPtr!.Type = DialogNodeType.Reply;
            replyPtr.Node = reply;
            entryA.Pointers.Add(replyPtr);

            // Reply -> Entry A (circular, using IsLink)
            var backPtr = dialog.CreatePtr();
            backPtr!.Type = DialogNodeType.Entry;
            backPtr.Node = entryA;
            backPtr.IsLink = true; // Mark as link to indicate circular reference
            reply.Pointers.Add(backPtr);

            // Create start pointer
            var startPtr = dialog.CreatePtr();
            startPtr!.Type = DialogNodeType.Entry;
            startPtr.Node = entryA;
            dialog.Starts.Add(startPtr);

            // Act - This should complete without infinite loop
            var graph = _converter.Convert(dialog);

            // Assert - Graph should be valid
            Assert.NotNull(graph);
            Assert.True(graph.Nodes.Count >= 2); // At least entry A and reply

            // The link back should create a link node, not duplicate entry A
            var entryANodes = graph.Nodes.Values.Where(n =>
                n.NodeType == FlowchartNodeType.Entry && !n.IsLink && n.Text == "Entry A").ToList();
            Assert.Single(entryANodes); // Entry A should appear only once as a real node
        }

        [Fact]
        public void Convert_DeepCircularReference_HandlesGracefully()
        {
            // Arrange - Create a chain: A -> B -> C -> A (circular)
            var dialog = new Dialog();

            var nodeA = dialog.CreateNode(DialogNodeType.Entry);
            nodeA!.Text.Add(0, "Node A");
            dialog.AddNodeInternal(nodeA, DialogNodeType.Entry);

            var nodeB = dialog.CreateNode(DialogNodeType.Reply);
            nodeB!.Text.Add(0, "Node B");
            dialog.AddNodeInternal(nodeB, DialogNodeType.Reply);

            var nodeC = dialog.CreateNode(DialogNodeType.Entry);
            nodeC!.Text.Add(0, "Node C");
            dialog.AddNodeInternal(nodeC, DialogNodeType.Entry);

            // A -> B
            var ptrAtoB = dialog.CreatePtr();
            ptrAtoB!.Type = DialogNodeType.Reply;
            ptrAtoB.Node = nodeB;
            nodeA.Pointers.Add(ptrAtoB);

            // B -> C
            var ptrBtoC = dialog.CreatePtr();
            ptrBtoC!.Type = DialogNodeType.Entry;
            ptrBtoC.Node = nodeC;
            nodeB.Pointers.Add(ptrBtoC);

            // C -> A (circular link)
            var ptrCtoA = dialog.CreatePtr();
            ptrCtoA!.Type = DialogNodeType.Entry;
            ptrCtoA.Node = nodeA;
            ptrCtoA.IsLink = true;
            nodeC.Pointers.Add(ptrCtoA);

            // Start at A
            var startPtr = dialog.CreatePtr();
            startPtr!.Type = DialogNodeType.Entry;
            startPtr.Node = nodeA;
            dialog.Starts.Add(startPtr);

            // Act
            var graph = _converter.Convert(dialog);

            // Assert
            Assert.NotNull(graph);
            // Should have A, B, C as real nodes, plus a link node for C->A
            Assert.True(graph.Nodes.Count >= 3);
        }

        #endregion

        #region Node ID Tests

        [Fact]
        public void Convert_Nodes_HaveUniqueIds()
        {
            // Arrange
            var dialog = new Dialog();

            for (int i = 0; i < 5; i++)
            {
                var entry = dialog.CreateNode(DialogNodeType.Entry);
                entry!.Text.Add(0, $"Entry {i}");
                dialog.AddNodeInternal(entry, DialogNodeType.Entry);
            }

            var startPtr = dialog.CreatePtr();
            startPtr!.Type = DialogNodeType.Entry;
            startPtr.Node = dialog.Entries[0];
            dialog.Starts.Add(startPtr);

            // Chain entries together
            for (int i = 0; i < 4; i++)
            {
                var ptr = dialog.CreatePtr();
                ptr!.Type = DialogNodeType.Entry;
                ptr.Node = dialog.Entries[i + 1];
                dialog.Entries[i].Pointers.Add(ptr);
            }

            // Act
            var graph = _converter.Convert(dialog);

            // Assert
            var ids = graph.Nodes.Values.Select(n => n.Id).ToList();
            Assert.Equal(ids.Count, ids.Distinct().Count()); // All IDs should be unique
        }

        #endregion

        #region Original Node Reference Tests

        [Fact]
        public void Convert_FlowchartNodes_RetainOriginalNodeReferences()
        {
            // Arrange
            var dialog = new Dialog();
            var startPtr = dialog.Add();
            startPtr!.Node!.Text.Add(0, "Test node");
            dialog.AddNodeInternal(startPtr.Node, DialogNodeType.Entry);

            // Act
            var graph = _converter.Convert(dialog);

            // Assert
            var flowchartNode = graph.Nodes.Values.First();
            Assert.NotNull(flowchartNode.OriginalNode);
            Assert.Same(startPtr.Node, flowchartNode.OriginalNode);
        }

        #endregion

        #region Static Factory Method Tests

        [Fact]
        public void CreateEmpty_ReturnsEmptyGraph()
        {
            // Act
            var graph = DialogToFlowchartConverter.CreateEmpty();

            // Assert
            Assert.NotNull(graph);
            Assert.True(graph.IsEmpty);
        }

        [Fact]
        public void CreateEmpty_WithFileName_SetsSourceFileName()
        {
            // Act
            var graph = DialogToFlowchartConverter.CreateEmpty("test.dlg");

            // Assert
            Assert.Equal("test.dlg", graph.SourceFileName);
        }

        #endregion
    }
}
