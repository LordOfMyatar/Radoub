using Avalonia.Headless.XUnit;
using DialogEditor.Models;
using DialogEditor.Utils;
using DialogEditor.ViewModels;
using Xunit;

namespace Parley.Tests.GUI
{
    /// <summary>
    /// Avalonia.Headless tests for node creation workflows (Issue #81 Phase 1)
    /// Tests AddSmartNode, AddEntryNode, AddPCReplyNode operations
    /// Note: DialogNodes[0] is ROOT node; actual entries are in DialogNodes[0].Children
    /// </summary>
    public class NodeCreationHeadlessTests
    {
        // Helper to get first entry node (ROOT's first child)
        private static TreeViewSafeNode? GetFirstEntryNode(MainViewModel viewModel)
        {
            var rootNode = viewModel.DialogNodes[0] as TreeViewRootNode;
            return rootNode?.Children?.Count > 0 ? rootNode.Children[0] : null;
        }

        // Helper to get entry node at index (from ROOT's children)
        private static TreeViewSafeNode? GetEntryNodeAt(MainViewModel viewModel, int index)
        {
            var rootNode = viewModel.DialogNodes[0] as TreeViewRootNode;
            return rootNode?.Children?.Count > index ? rootNode.Children[index] : null;
        }

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

        [AvaloniaFact]
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

        [AvaloniaFact]
        public void AddPCReplyNode_ToEntry_CreatesReplyInDialog()
        {
            // Arrange
            var viewModel = new MainViewModel();
            viewModel.NewDialog();
            viewModel.AddEntryNode(null);

            var entryNode = GetFirstEntryNode(viewModel);
            Assert.NotNull(entryNode);

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

        [AvaloniaFact]
        public void AddSmartNode_UnderEntry_CreatesReply()
        {
            // Arrange
            var viewModel = new MainViewModel();
            viewModel.NewDialog();
            viewModel.AddEntryNode(null);

            var entryNode = GetFirstEntryNode(viewModel);
            Assert.NotNull(entryNode);

            // Act: AddSmartNode under entry
            viewModel.AddSmartNode(entryNode);

            // Assert: Should create Reply (smart choice under entry)
            Assert.NotNull(viewModel.CurrentDialog);
            Assert.NotEmpty(viewModel.CurrentDialog.Replies);
        }

        [AvaloniaFact]
        public void AddSmartNode_UnderReply_CreatesEntry()
        {
            // Arrange
            var viewModel = new MainViewModel();
            viewModel.NewDialog();
            viewModel.AddEntryNode(null);

            var entryNode = GetFirstEntryNode(viewModel);
            Assert.NotNull(entryNode);
            viewModel.AddPCReplyNode(entryNode);

            // Find reply node in tree (need to re-get entryNode after tree refresh)
            entryNode = GetFirstEntryNode(viewModel);
            Assert.NotNull(entryNode);
            // LAZY LOADING: Must expand node to populate children
            entryNode.IsExpanded = true;
            Assert.NotNull(entryNode.Children);
            Assert.NotEmpty(entryNode.Children);
            var replyNode = entryNode.Children[0];
            Assert.IsNotType<TreeViewPlaceholderNode>(replyNode); // Verify not a placeholder

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
