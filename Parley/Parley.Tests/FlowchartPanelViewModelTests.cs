using System.Linq;
using Xunit;
using DialogEditor.Models;
using DialogEditor.ViewModels;

namespace Parley.Tests
{
    /// <summary>
    /// Unit tests for FlowchartPanelViewModel
    /// Tests dialog conversion, refresh functionality, and state management
    /// Sprint 4: Issue #340 (color refresh)
    /// </summary>
    public class FlowchartPanelViewModelTests
    {
        #region UpdateDialog Tests

        [Fact]
        public void UpdateDialog_NullDialog_ClearsState()
        {
            // Arrange
            var vm = new FlowchartPanelViewModel();

            // Act
            vm.UpdateDialog(null);

            // Assert
            Assert.Null(vm.Graph);
            Assert.False(vm.HasContent);
            Assert.Equal("No dialog loaded", vm.StatusText);
        }

        [Fact]
        public void UpdateDialog_EmptyDialog_StillCreatesRootNode()
        {
            // Arrange
            var vm = new FlowchartPanelViewModel();
            var dialog = new Dialog();

            // Act
            vm.UpdateDialog(dialog);

            // Assert - Empty dialog still gets ROOT node, so graph exists
            // FlowchartGraph.IsEmpty checks Nodes.Count == 0, but empty dialog has ROOT
            Assert.NotNull(vm.Graph);
            Assert.True(vm.HasContent);
            Assert.Contains("1 nodes", vm.StatusText); // ROOT node
            Assert.Contains("0 edges", vm.StatusText); // No edges
        }

        [Fact]
        public void UpdateDialog_ValidDialog_CreatesGraph()
        {
            // Arrange
            var vm = new FlowchartPanelViewModel();
            var dialog = CreateSimpleDialog();

            // Act
            vm.UpdateDialog(dialog, "test.dlg");

            // Assert
            Assert.NotNull(vm.Graph);
            Assert.True(vm.HasContent);
            Assert.Contains("nodes", vm.StatusText);
            Assert.Contains("edges", vm.StatusText);
        }

        [Fact]
        public void UpdateDialog_SetsFileName()
        {
            // Arrange
            var vm = new FlowchartPanelViewModel();
            var dialog = CreateSimpleDialog();

            // Act
            vm.UpdateDialog(dialog, "merchant.dlg");

            // Assert
            Assert.Equal("merchant.dlg", vm.FileName);
        }

        [Fact]
        public void UpdateDialog_ExposesFlowchartGraph()
        {
            // Arrange
            var vm = new FlowchartPanelViewModel();
            var dialog = CreateSimpleDialog();

            // Act
            vm.UpdateDialog(dialog, "test.dlg");

            // Assert
            Assert.NotNull(vm.FlowchartGraph);
            Assert.False(vm.FlowchartGraph.IsEmpty);
        }

        #endregion

        #region RefreshGraph Tests (Issue #340)

        [Fact]
        public void RefreshGraph_WithNoDialog_DoesNothing()
        {
            // Arrange
            var vm = new FlowchartPanelViewModel();

            // Act - Should not throw
            vm.RefreshGraph();

            // Assert
            Assert.Null(vm.Graph);
        }

        [Fact]
        public void RefreshGraph_WithLoadedDialog_RebuildGraph()
        {
            // Arrange
            var vm = new FlowchartPanelViewModel();
            var dialog = CreateSimpleDialog();
            vm.UpdateDialog(dialog, "test.dlg");
            var originalGraph = vm.Graph;

            // Act
            vm.RefreshGraph();

            // Assert
            Assert.NotNull(vm.Graph);
            Assert.NotSame(originalGraph, vm.Graph); // New graph instance
            Assert.True(vm.HasContent);
        }

        [Fact]
        public void RefreshGraph_PreservesFileName()
        {
            // Arrange
            var vm = new FlowchartPanelViewModel();
            var dialog = CreateSimpleDialog();
            vm.UpdateDialog(dialog, "important.dlg");

            // Act
            vm.RefreshGraph();

            // Assert
            Assert.Equal("important.dlg", vm.FileName);
        }

        [Fact]
        public void RefreshGraph_CreatesNewFlowchartGraph()
        {
            // Arrange
            var vm = new FlowchartPanelViewModel();
            var dialog = CreateSimpleDialog();
            vm.UpdateDialog(dialog, "test.dlg");
            var originalFlowchartGraph = vm.FlowchartGraph;

            // Act
            vm.RefreshGraph();

            // Assert - New FlowchartGraph with new FlowchartNode objects
            Assert.NotSame(originalFlowchartGraph, vm.FlowchartGraph);
        }

        #endregion

        #region Clear Tests

        [Fact]
        public void Clear_ResetsAllState()
        {
            // Arrange
            var vm = new FlowchartPanelViewModel();
            var dialog = CreateSimpleDialog();
            vm.UpdateDialog(dialog, "test.dlg");
            vm.SelectedNodeId = "E0";

            // Act
            vm.Clear();

            // Assert
            Assert.Null(vm.Graph);
            Assert.Null(vm.FileName);
            Assert.Null(vm.SelectedNodeId);
            Assert.Null(vm.FlowchartGraph);
            Assert.False(vm.HasContent);
            Assert.Equal("No dialog loaded", vm.StatusText);
        }

        #endregion

        #region Selection Tests

        [Fact]
        public void SelectNode_FindsCorrectFlowchartNodeId()
        {
            // Arrange
            var vm = new FlowchartPanelViewModel();
            var dialog = CreateSimpleDialog();
            vm.UpdateDialog(dialog, "test.dlg");

            // Get the original DialogNode
            var dialogNode = dialog.Entries.First();

            // Act
            vm.SelectNode(dialogNode);

            // Assert
            Assert.NotNull(vm.SelectedNodeId);
        }

        [Fact]
        public void SelectNode_NullNode_ClearsSelection()
        {
            // Arrange
            var vm = new FlowchartPanelViewModel();
            var dialog = CreateSimpleDialog();
            vm.UpdateDialog(dialog, "test.dlg");
            vm.SelectedNodeId = "E0";

            // Act
            vm.SelectNode(null);

            // Assert
            Assert.Null(vm.SelectedNodeId);
        }

        [Fact]
        public void FindNodeIdForDialogNode_NoGraph_ReturnsNull()
        {
            // Arrange
            var vm = new FlowchartPanelViewModel();
            var dialog = new Dialog();
            var dialogNode = dialog.CreateNode(DialogNodeType.Entry);

            // Act
            var result = vm.FindNodeIdForDialogNode(dialogNode);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void FindNodeIdForDialogNode_NodeNotInGraph_ReturnsNull()
        {
            // Arrange
            var vm = new FlowchartPanelViewModel();
            var dialog = CreateSimpleDialog();
            vm.UpdateDialog(dialog, "test.dlg");

            // Create unrelated node from different dialog
            var otherDialog = new Dialog();
            var unrelatedNode = otherDialog.CreateNode(DialogNodeType.Entry);

            // Act
            var result = vm.FindNodeIdForDialogNode(unrelatedNode);

            // Assert
            Assert.Null(result);
        }

        #endregion

        #region Status Text Tests

        [Fact]
        public void StatusText_ShowsNodeAndEdgeCount()
        {
            // Arrange
            var vm = new FlowchartPanelViewModel();
            var dialog = CreateDialogWithReplies();

            // Act
            vm.UpdateDialog(dialog);

            // Assert
            Assert.Matches(@"\d+ nodes, \d+ edges", vm.StatusText);
        }

        #endregion

        #region Helper Methods

        private Dialog CreateSimpleDialog()
        {
            var dialog = new Dialog();
            var startPtr = dialog.Add();
            startPtr!.Node!.Text.Add(0, "Hello adventurer!");
            dialog.AddNodeInternal(startPtr.Node, DialogNodeType.Entry);
            return dialog;
        }

        private Dialog CreateDialogWithReplies()
        {
            var dialog = new Dialog();

            // Create entry node
            var startPtr = dialog.Add();
            startPtr!.Node!.Text.Add(0, "What do you want?");
            dialog.AddNodeInternal(startPtr.Node, DialogNodeType.Entry);

            // Add reply 1
            var reply1 = dialog.CreateNode(DialogNodeType.Reply);
            reply1!.Text.Add(0, "Buy something");
            dialog.AddNodeInternal(reply1, DialogNodeType.Reply);

            var replyPtr1 = dialog.CreatePtr();
            replyPtr1!.Type = DialogNodeType.Reply;
            replyPtr1.Node = reply1;
            startPtr.Node.Pointers.Add(replyPtr1);

            // Add reply 2
            var reply2 = dialog.CreateNode(DialogNodeType.Reply);
            reply2!.Text.Add(0, "Goodbye");
            dialog.AddNodeInternal(reply2, DialogNodeType.Reply);

            var replyPtr2 = dialog.CreatePtr();
            replyPtr2!.Type = DialogNodeType.Reply;
            replyPtr2.Node = reply2;
            startPtr.Node.Pointers.Add(replyPtr2);

            return dialog;
        }

        #endregion
    }
}