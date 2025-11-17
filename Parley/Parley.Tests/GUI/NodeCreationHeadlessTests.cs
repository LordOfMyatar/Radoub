using Avalonia.Headless.XUnit;
using DialogEditor.Models;
using DialogEditor.ViewModels;
using Xunit;

namespace Parley.Tests.GUI
{
    /// <summary>
    /// Avalonia.Headless tests for node creation workflows (Issue #81 Phase 1)
    /// Tests AddSmartNode, AddEntryNode, AddPCReplyNode operations
    /// </summary>
    public class NodeCreationHeadlessTests
    {
        [AvaloniaFact]
        public void AddEntryNode_ToRoot_CreatesEntryInDialog()
        {
            // Arrange
            var viewModel = new MainViewModel();
            viewModel.NewDialog();

            // Act: Add entry node at root level
            viewModel.AddEntryNode(null);

            // Assert: Dialog has new entry
            Assert.NotNull(viewModel.CurrentDialog);
            Assert.Single(viewModel.CurrentDialog.Entries);
            Assert.Single(viewModel.CurrentDialog.Starts);
        }

        [AvaloniaFact(Skip = "Requires DialogNodes tree rebuild trigger - see Issue #130")]
        public void AddEntryNode_UpdatesDialogNodesCollection()
        {
            // Arrange
            var viewModel = new MainViewModel();
            viewModel.NewDialog();

            var initialCount = viewModel.DialogNodes.Count;

            // Act
            viewModel.AddEntryNode(null);

            // Assert: DialogNodes updated
            Assert.True(viewModel.DialogNodes.Count > initialCount);
        }

        [AvaloniaFact(Skip = "Requires DialogNodes tree rebuild trigger - see Issue #130")]
        public void AddPCReplyNode_ToEntry_CreatesReplyInDialog()
        {
            // Arrange
            var viewModel = new MainViewModel();
            viewModel.NewDialog();
            viewModel.AddEntryNode(null);

            var entryNode = viewModel.DialogNodes[0]; // First node should be entry

            // Act: Add PC Reply under entry
            viewModel.AddPCReplyNode(entryNode);

            // Assert: Dialog has reply
            Assert.NotNull(viewModel.CurrentDialog);
            Assert.Single(viewModel.CurrentDialog.Replies);

            // Entry should have pointer to reply
            var entry = viewModel.CurrentDialog.Entries[0];
            Assert.Single(entry.Pointers);
            Assert.Equal(DialogNodeType.Reply, entry.Pointers[0].Type);
        }

        [AvaloniaFact]
        public void AddSmartNode_AtRoot_CreatesEntry()
        {
            // Arrange
            var viewModel = new MainViewModel();
            viewModel.NewDialog();

            // Act: AddSmartNode with no selection (root context)
            viewModel.AddSmartNode(null);

            // Assert: Should create Entry (smart choice for root)
            Assert.NotNull(viewModel.CurrentDialog);
            Assert.NotEmpty(viewModel.CurrentDialog.Entries);
        }

        [AvaloniaFact(Skip = "Requires DialogNodes tree rebuild trigger - see Issue #130")]
        public void AddSmartNode_UnderEntry_CreatesReply()
        {
            // Arrange
            var viewModel = new MainViewModel();
            viewModel.NewDialog();
            viewModel.AddEntryNode(null);

            var entryNode = viewModel.DialogNodes[0];

            // Act: AddSmartNode under entry
            viewModel.AddSmartNode(entryNode);

            // Assert: Should create Reply (smart choice under entry)
            Assert.NotNull(viewModel.CurrentDialog);
            Assert.NotEmpty(viewModel.CurrentDialog.Replies);
        }

        [AvaloniaFact(Skip = "Requires DialogNodes tree rebuild trigger - see Issue #130")]
        public void AddSmartNode_UnderReply_CreatesEntry()
        {
            // Arrange
            var viewModel = new MainViewModel();
            viewModel.NewDialog();
            viewModel.AddEntryNode(null);

            var entryNode = viewModel.DialogNodes[0];
            viewModel.AddPCReplyNode(entryNode);

            // Find reply node in tree
            var replyNode = entryNode.Children[0];

            // Act: AddSmartNode under reply
            viewModel.AddSmartNode(replyNode);

            // Assert: Should create Entry (smart choice under reply)
            Assert.NotNull(viewModel.CurrentDialog);
            Assert.Equal(2, viewModel.CurrentDialog.Entries.Count); // Original + new
        }

        [AvaloniaFact]
        public void MultipleNodeCreation_MaintainsCorrectIndices()
        {
            // Arrange
            var viewModel = new MainViewModel();
            viewModel.NewDialog();

            // Act: Create multiple nodes
            viewModel.AddEntryNode(null); // Entry 0
            viewModel.AddEntryNode(null); // Entry 1
            viewModel.AddEntryNode(null); // Entry 2

            // Assert: Entries have correct indices
            Assert.NotNull(viewModel.CurrentDialog);
            Assert.Equal(3, viewModel.CurrentDialog.Entries.Count);
            Assert.Equal(3, viewModel.CurrentDialog.Starts.Count);

            // Verify indices are 0, 1, 2
            for (int i = 0; i < 3; i++)
            {
                var startPtr = viewModel.CurrentDialog.Starts[i];
                Assert.Equal((uint)i, startPtr.Index);
            }
        }
    }
}
