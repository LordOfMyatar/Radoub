using Avalonia.Headless.XUnit;
using DialogEditor.ViewModels;
using Xunit;

namespace Parley.Tests.GUI
{
    /// <summary>
    /// Avalonia.Headless tests for node deletion workflows (Issue #81 Phase 1)
    /// Tests DeleteNode operation and scrap integration
    /// </summary>
    public class NodeDeletionHeadlessTests
    {
        [AvaloniaFact(Skip = "Requires DialogNodes tree rebuild trigger - see Issue #130")]
        public void DeleteNode_MovesToScrapEntries()
        {
            // Arrange
            var viewModel = new MainViewModel();
            viewModel.NewDialog();
            viewModel.AddEntryNode(null);

            var nodeToDelete = viewModel.DialogNodes[0];
            var initialScrapCount = viewModel.ScrapEntries.Count;

            // Act
            viewModel.DeleteNode(nodeToDelete);

            // Assert: Node moved to scrap
            Assert.Equal(initialScrapCount + 1, viewModel.ScrapEntries.Count);
        }

        [AvaloniaFact(Skip = "Requires DialogNodes tree rebuild trigger - see Issue #130")]
        public void DeleteNode_RemovesFromDialogNodes()
        {
            // Arrange
            var viewModel = new MainViewModel();
            viewModel.NewDialog();
            viewModel.AddEntryNode(null);
            viewModel.AddEntryNode(null);

            var initialNodeCount = viewModel.DialogNodes.Count;
            var nodeToDelete = viewModel.DialogNodes[0];

            // Act
            viewModel.DeleteNode(nodeToDelete);

            // Assert: DialogNodes has one less node
            Assert.True(viewModel.DialogNodes.Count < initialNodeCount);
        }

        [AvaloniaFact(Skip = "Requires DialogNodes tree rebuild trigger - see Issue #130")]
        public void DeleteNode_RemovesFromDialog()
        {
            // Arrange
            var viewModel = new MainViewModel();
            viewModel.NewDialog();
            viewModel.AddEntryNode(null);

            var nodeToDelete = viewModel.DialogNodes[0];

            // Act
            viewModel.DeleteNode(nodeToDelete);

            // Assert: Entry removed from dialog
            Assert.NotNull(viewModel.CurrentDialog);
            Assert.Empty(viewModel.CurrentDialog.Entries);
            Assert.Empty(viewModel.CurrentDialog.Starts);
        }

        [AvaloniaFact(Skip = "Requires DialogNodes tree rebuild trigger - see Issue #130")]
        public void DeleteNode_WithChildren_ScrapsAllDescendants()
        {
            // Arrange
            var viewModel = new MainViewModel();
            viewModel.NewDialog();
            viewModel.AddEntryNode(null);

            var entryNode = viewModel.DialogNodes[0];
            viewModel.AddPCReplyNode(entryNode); // Add child

            var initialScrapCount = viewModel.ScrapEntries.Count;

            // Act: Delete parent (should scrap parent + children)
            viewModel.DeleteNode(entryNode);

            // Assert: Multiple nodes scrapped
            Assert.True(viewModel.ScrapEntries.Count > initialScrapCount);
        }

        [AvaloniaFact(Skip = "Requires DialogNodes tree rebuild trigger - see Issue #130")]
        public void DeleteLastStartingNode_EmptiesDialog()
        {
            // Arrange
            var viewModel = new MainViewModel();
            viewModel.NewDialog();
            viewModel.AddEntryNode(null);

            var onlyNode = viewModel.DialogNodes[0];

            // Act: Delete the only starting node
            viewModel.DeleteNode(onlyNode);

            // Assert: Dialog is now empty
            Assert.NotNull(viewModel.CurrentDialog);
            Assert.Empty(viewModel.CurrentDialog.Starts);
            Assert.Empty(viewModel.DialogNodes);
        }

        [AvaloniaFact(Skip = "Requires DialogNodes tree rebuild trigger - see Issue #130")]
        public void DeleteMiddleNode_PreservesOtherNodes()
        {
            // Arrange
            var viewModel = new MainViewModel();
            viewModel.NewDialog();
            viewModel.AddEntryNode(null); // Entry 0
            viewModel.AddEntryNode(null); // Entry 1
            viewModel.AddEntryNode(null); // Entry 2

            var middleNode = viewModel.DialogNodes[1];

            // Act: Delete middle node
            viewModel.DeleteNode(middleNode);

            // Assert: Other nodes still exist
            Assert.NotNull(viewModel.CurrentDialog);
            Assert.Equal(2, viewModel.CurrentDialog.Entries.Count);
            Assert.Equal(2, viewModel.CurrentDialog.Starts.Count);
        }
    }
}
