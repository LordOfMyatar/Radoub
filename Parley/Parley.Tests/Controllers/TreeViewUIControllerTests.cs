using Avalonia.Headless.XUnit;
using DialogEditor.Models;
using DialogEditor.Services;
using DialogEditor.ViewModels;
using Parley.Views.Helpers;
using Xunit;

namespace Parley.Tests.Controllers
{
    /// <summary>
    /// Unit tests for TreeViewUIController.
    /// Tests selection handling logic, expand/collapse, and go-to-parent navigation.
    /// Drag-drop UI operations require headless/integration tests.
    /// </summary>
    public class TreeViewUIControllerTests
    {
        private readonly MainViewModel _viewModel;
        private TreeViewSafeNode? _selectedNode;
        private bool _isSettingSelectionProgrammatically;
        private readonly List<TreeViewSafeNode> _populatedNodes = new();
        private bool _propertiesSaved;
        private DialogNode? _syncedNode;

        public TreeViewUIControllerTests()
        {
            _viewModel = new MainViewModel();
        }

        #region Constructor

        [AvaloniaFact]
        public void Constructor_ValidArgs_CreatesInstance()
        {
            var controller = CreateController();
            Assert.NotNull(controller);
        }

        #endregion

        #region Expand/Collapse Subnodes

        [AvaloniaFact]
        public void OnExpandSubnodesClick_NoTreeViewControl_Throws()
        {
            // GetSelectedTreeNode() calls FindControl("DialogTreeView") which throws
            // without NameScope on a bare Window in unit tests.
            var controller = CreateController();

            Assert.Throws<InvalidOperationException>(() =>
                controller.OnExpandSubnodesClick(null, null!));
        }

        [AvaloniaFact]
        public void OnCollapseSubnodesClick_NoTreeViewControl_Throws()
        {
            var controller = CreateController();

            Assert.Throws<InvalidOperationException>(() =>
                controller.OnCollapseSubnodesClick(null, null!));
        }

        #endregion

        #region Go To Parent Node

        [AvaloniaFact]
        public void OnGoToParentNodeClick_NoTreeViewControl_Throws()
        {
            var controller = CreateController();

            Assert.Throws<InvalidOperationException>(() =>
                controller.OnGoToParentNodeClick(null, null!));
        }

        #endregion

        #region Double-Tap Toggle

        [AvaloniaFact]
        public void OnTreeViewItemDoubleTapped_NoSelection_DoesNotThrow()
        {
            _selectedNode = null;
            var controller = CreateController();

            // Should not throw when no node selected
            controller.OnTreeViewItemDoubleTapped(null, null!);
        }

        [AvaloniaFact]
        public void OnTreeViewItemDoubleTapped_WithSelection_TogglesExpansion()
        {
            var node = CreateTestNode("Test");
            node.IsExpanded = false;
            _selectedNode = node;
            var controller = CreateController();

            controller.OnTreeViewItemDoubleTapped(null, null!);

            Assert.True(node.IsExpanded);
        }

        [AvaloniaFact]
        public void OnTreeViewItemDoubleTapped_ExpandedNode_Collapses()
        {
            var node = CreateTestNode("Test");
            node.IsExpanded = true;
            _selectedNode = node;
            var controller = CreateController();

            controller.OnTreeViewItemDoubleTapped(null, null!);

            Assert.False(node.IsExpanded);
        }

        #endregion

        #region Selection Changed

        [AvaloniaFact]
        public void OnDialogTreeViewSelectionChanged_ProgrammaticSelection_Skips()
        {
            _isSettingSelectionProgrammatically = true;
            _propertiesSaved = false;
            var controller = CreateController();

            controller.OnDialogTreeViewSelectionChanged(null, null!);

            // Should not save properties when programmatic
            Assert.False(_propertiesSaved);
        }

        #endregion

        #region DragDrop Setup

        [AvaloniaFact]
        public void ClearDropIndicator_NoPriorIndicator_DoesNotThrow()
        {
            var controller = CreateController();

            // Should not throw when no prior indicator
            controller.ClearDropIndicator();
        }

        #endregion

        #region Pointer Handlers

        [AvaloniaFact]
        public void OnTreeViewItemPointerReleased_NotDragging_DoesNotThrow()
        {
            var controller = CreateController();

            // Should not throw when no drag in progress
            controller.OnTreeViewItemPointerReleased(null, null!);
        }

        #endregion

        #region Helper

        private TreeViewUIController CreateController()
        {
            var window = new Avalonia.Controls.Window();
            var controls = new SafeControlFinder(window);
            var dragDropService = new TreeViewDragDropService();

            return new TreeViewUIController(
                window,
                controls,
                dragDropService,
                () => _viewModel,
                () => _selectedNode,
                node => _selectedNode = node,
                node => _populatedNodes.Add(node),
                () => _propertiesSaved = true,
                () => { },
                () => _isSettingSelectionProgrammatically,
                node => _syncedNode = node);
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
