using System;
using Avalonia.Headless.XUnit;
using DialogEditor.Models;
using DialogEditor.ViewModels;
using Xunit;

namespace Parley.Tests.GUI
{
    /// <summary>
    /// Tests for AddNodeWithUndoAndRefresh rollback (#2260).
    /// If CoordinatedRefreshAndSelect throws mid-refresh after the new node is attached,
    /// the model must be rolled back (node removed) rather than left half-attached with
    /// HasUnsavedChanges=true. Mirrors the Relique #2166 rollback precedent.
    /// </summary>
    public class AddNodeRollbackTests
    {
        /// <summary>
        /// MainViewModel subclass that simulates a refresh failure after node attachment.
        /// </summary>
        private sealed class FaultingRefreshViewModel : MainViewModel
        {
            public bool FaultEnabled { get; set; } = true;

            internal override void CoordinatedRefreshAndSelect(DialogNode target)
            {
                if (FaultEnabled)
                    throw new InvalidOperationException("Simulated refresh failure");
                base.CoordinatedRefreshAndSelect(target);
            }
        }

        [AvaloniaFact]
        public void AddEntryNode_WhenRefreshThrows_RollsBackModelAndDirtyFlag()
        {
            // Arrange
            var viewModel = new FaultingRefreshViewModel();
            viewModel.NewDialog();
            Assert.NotNull(viewModel.CurrentDialog);

            int entriesBefore = viewModel.CurrentDialog.Entries.Count;
            int startsBefore = viewModel.CurrentDialog.Starts.Count;
            bool dirtyBefore = viewModel.HasUnsavedChanges;

            // Act: refresh throws after the node is attached
            viewModel.AddEntryNode(null);

            // Assert: model rolled back - no leftover node, no spurious dirty flag
            Assert.Equal(entriesBefore, viewModel.CurrentDialog.Entries.Count);
            Assert.Equal(startsBefore, viewModel.CurrentDialog.Starts.Count);
            Assert.Equal(dirtyBefore, viewModel.HasUnsavedChanges);
        }

        [AvaloniaFact]
        public void AddPCReplyNode_WhenRefreshThrows_RollsBackReply()
        {
            // Arrange: seed one entry with the fault disabled, then enable it.
            var viewModel = new FaultingRefreshViewModel { FaultEnabled = false };
            viewModel.NewDialog();
            viewModel.AddEntryNode(null);
            viewModel.FaultEnabled = true;
            Assert.NotNull(viewModel.CurrentDialog);

            var rootNode = viewModel.DialogNodes[0] as TreeViewRootNode;
            var entryNode = rootNode?.Children?.Count > 0 ? rootNode.Children[0] : null;
            Assert.NotNull(entryNode);

            int repliesBefore = viewModel.CurrentDialog.Replies.Count;
            bool dirtyBefore = viewModel.HasUnsavedChanges;

            // Act
            viewModel.AddPCReplyNode(entryNode!);

            // Assert: reply removed on rollback
            Assert.Equal(repliesBefore, viewModel.CurrentDialog.Replies.Count);
            Assert.Equal(dirtyBefore, viewModel.HasUnsavedChanges);
        }
    }
}
