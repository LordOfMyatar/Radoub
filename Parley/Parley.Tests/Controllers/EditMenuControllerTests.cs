using Avalonia.Headless.XUnit;
using DialogEditor.Models;
using DialogEditor.ViewModels;
using Parley.Views.Helpers;
using Xunit;

namespace Parley.Tests.Controllers
{
    /// <summary>
    /// Unit tests for EditMenuController.
    /// Tests constructor validation and null-node guard paths.
    /// Clipboard operations require a running Avalonia UI and are covered in headless tests.
    /// </summary>
    public class EditMenuControllerTests
    {
        private readonly MainViewModel _viewModel;

        public EditMenuControllerTests()
        {
            _viewModel = new MainViewModel();
        }

        #region Constructor Validation

        [AvaloniaFact]
        public void Constructor_NullWindow_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>("window", () =>
                new EditMenuController(null!, () => _viewModel, () => null));
        }

        [AvaloniaFact]
        public void Constructor_NullGetViewModel_ThrowsArgumentNullException()
        {
            var window = new Avalonia.Controls.Window();
            Assert.Throws<ArgumentNullException>("getViewModel", () =>
                new EditMenuController(window, null!, () => null));
        }

        [AvaloniaFact]
        public void Constructor_NullGetSelectedNode_ThrowsArgumentNullException()
        {
            var window = new Avalonia.Controls.Window();
            Assert.Throws<ArgumentNullException>("getSelectedNode", () =>
                new EditMenuController(window, () => _viewModel, null!));
        }

        [AvaloniaFact]
        public void Constructor_ValidArgs_CreatesInstance()
        {
            var controller = CreateController();
            Assert.NotNull(controller);
        }

        #endregion

        #region Undo/Redo

        [AvaloniaFact]
        public void OnUndoClick_DelegatesToViewModel()
        {
            var controller = CreateController();

            // Should not throw - delegates to ViewModel.Undo()
            controller.OnUndoClick(null, null!);
        }

        [AvaloniaFact]
        public void OnRedoClick_DelegatesToViewModel()
        {
            var controller = CreateController();

            // Should not throw - delegates to ViewModel.Redo()
            controller.OnRedoClick(null, null!);
        }

        #endregion

        #region Cut/Copy/Paste - No Selection

        [AvaloniaFact]
        public void OnCutNodeClick_NoSelection_SetsStatusMessage()
        {
            var controller = CreateController(selectedNode: null);

            controller.OnCutNodeClick(null, null!);

            Assert.Equal("Please select a node to cut", _viewModel.StatusMessage);
        }

        [AvaloniaFact]
        public void OnCopyNodeClick_NoSelection_SetsStatusMessage()
        {
            var controller = CreateController(selectedNode: null);

            controller.OnCopyNodeClick(null, null!);

            Assert.Equal("Please select a node to copy", _viewModel.StatusMessage);
        }

        [AvaloniaFact]
        public void OnPasteAsDuplicateClick_NoSelection_SetsStatusMessage()
        {
            var controller = CreateController(selectedNode: null);

            controller.OnPasteAsDuplicateClick(null, null!);

            Assert.Equal("Please select a parent node to paste under", _viewModel.StatusMessage);
        }

        [AvaloniaFact]
        public void OnPasteAsLinkClick_NoSelection_SetsStatusMessage()
        {
            var controller = CreateController(selectedNode: null);

            controller.OnPasteAsLinkClick(null, null!);

            Assert.Equal("Please select a parent node to paste link under", _viewModel.StatusMessage);
        }

        #endregion

        #region Cut/Copy - With Selection

        [AvaloniaFact]
        public void OnCutNodeClick_WithSelection_DelegatesToViewModel()
        {
            var node = CreateTestNode("Test node");
            var controller = CreateController(selectedNode: node);

            // Should not throw - delegates to ViewModel.CutNode(node)
            controller.OnCutNodeClick(null, null!);
        }

        [AvaloniaFact]
        public void OnCopyNodeClick_WithSelection_DelegatesToViewModel()
        {
            var node = CreateTestNode("Test node");
            var controller = CreateController(selectedNode: node);

            // Should not throw - delegates to ViewModel.CopyNode(node)
            controller.OnCopyNodeClick(null, null!);
        }

        [AvaloniaFact]
        public void OnPasteAsDuplicateClick_WithSelection_DelegatesToViewModel()
        {
            var node = CreateTestNode("Parent node");
            var controller = CreateController(selectedNode: node);

            // Should not throw - delegates to ViewModel.PasteAsDuplicate(node)
            controller.OnPasteAsDuplicateClick(null, null!);
        }

        #endregion

        #region Copy to Clipboard - No Selection

        [AvaloniaFact]
        public void OnCopyNodeTextClick_NoSelection_SetsStatusMessage()
        {
            var controller = CreateController(selectedNode: null);

            controller.OnCopyNodeTextClick(null, null!);

            Assert.Equal("No node selected or node has no text", _viewModel.StatusMessage);
        }

        [AvaloniaFact]
        public void OnCopyNodePropertiesClick_NoSelection_SetsStatusMessage()
        {
            var controller = CreateController(selectedNode: null);

            controller.OnCopyNodePropertiesClick(null, null!);

            Assert.Equal("No node selected", _viewModel.StatusMessage);
        }

        #endregion

        #region Helper

        private EditMenuController CreateController(TreeViewSafeNode? selectedNode = null)
        {
            var window = new Avalonia.Controls.Window();
            return new EditMenuController(
                window,
                () => _viewModel,
                () => selectedNode);
        }

        private static TreeViewSafeNode CreateTestNode(string text)
        {
            var dialogNode = new DialogNode
            {
                Type = DialogNodeType.Entry,
                Text = new LocString()
            };
            dialogNode.Text.Add(0, text);
            return new TreeViewSafeNode(dialogNode);
        }

        #endregion
    }
}
