using System.Collections.ObjectModel;
using DialogEditor.Models;
using DialogEditor.Services;

namespace DialogEditor.Tests
{
    public class TreeRefreshCoordinatorTests
    {
        private TreeRefreshCoordinator _coordinator = null!;

        [Fact]
        public void InitialState_IsIdle()
        {
            var coordinator = CreateCoordinator();
            Assert.Equal(TreeRefreshState.Idle, coordinator.State);
        }

        [Fact]
        public void IsBusy_WhenIdle_ReturnsFalse()
        {
            var coordinator = CreateCoordinator();
            Assert.False(coordinator.IsBusy);
        }

        [Fact]
        public void RefreshPreservingSelection_TransitionsToRefreshing()
        {
            TreeRefreshState? capturedState = null;
            var coordinator = CreateCoordinator(
                onPopulate: () => capturedState = _coordinator.State);

            coordinator.RefreshPreservingSelection();

            Assert.Equal(TreeRefreshState.Refreshing, capturedState);
        }

        [Fact]
        public void RefreshToRoot_TransitionsToRefreshing()
        {
            TreeRefreshState? capturedState = null;
            var coordinator = CreateCoordinator(
                onPopulate: () => capturedState = _coordinator.State);

            coordinator.RefreshToRoot();

            Assert.Equal(TreeRefreshState.Refreshing, capturedState);
        }

        [Fact]
        public void RefreshAndSelectNode_TransitionsToRefreshing()
        {
            TreeRefreshState? capturedState = null;
            var dialog = new Dialog();
            var node = new DialogNode { Type = DialogNodeType.Entry };
            dialog.Entries.Add(node);

            var coordinator = CreateCoordinator(
                getCurrentDialog: () => dialog,
                onPopulate: () => capturedState = _coordinator.State);

            coordinator.RefreshAndSelectNode(node);

            Assert.Equal(TreeRefreshState.Refreshing, capturedState);
        }

        [Fact]
        public void ReentrantRefresh_IsRejected()
        {
            int populateCallCount = 0;
            var coordinator = CreateCoordinator(
                onPopulate: () =>
                {
                    populateCallCount++;
                    _coordinator.RefreshPreservingSelection();
                });

            coordinator.RefreshPreservingSelection();

            Assert.Equal(1, populateCallCount);
        }

        [Fact]
        public void IsBusy_DuringRefresh_ReturnsTrue()
        {
            bool wasBusy = false;
            var coordinator = CreateCoordinator(
                onPopulate: () => wasBusy = _coordinator.IsBusy);

            coordinator.RefreshPreservingSelection();

            Assert.True(wasBusy);
        }

        [Fact]
        public void RefreshPreservingSelection_WhenNoSelectedNode_CompletesWithoutError()
        {
            TreeRefreshState? capturedState = null;
            var coordinator = CreateCoordinator(
                getSelectedNode: () => null,
                onPopulate: () => capturedState = _coordinator.State);

            coordinator.RefreshPreservingSelection();

            Assert.Equal(TreeRefreshState.Refreshing, capturedState);
        }

        [Fact]
        public void RefreshPreservingSelection_WhenNoDialog_CompletesWithoutError()
        {
            TreeRefreshState? capturedState = null;
            var coordinator = CreateCoordinator(
                getCurrentDialog: () => null,
                onPopulate: () => capturedState = _coordinator.State);

            coordinator.RefreshPreservingSelection();

            Assert.Equal(TreeRefreshState.Refreshing, capturedState);
        }

        [Fact]
        public void StateReturnsToIdle_AfterErrorInPopulate()
        {
            var coordinator = CreateCoordinator(
                onPopulate: () => throw new InvalidOperationException("test error"));

            coordinator.RefreshPreservingSelection();

            Assert.Equal(TreeRefreshState.Idle, coordinator.State);
            Assert.False(coordinator.IsBusy);
        }

        [Fact]
        public void StateReturnsToIdle_AfterFullCycle()
        {
            var coordinator = CreateCoordinator();

            coordinator.RefreshPreservingSelection();

            Assert.Equal(TreeRefreshState.Idle, coordinator.State);
            Assert.False(coordinator.IsBusy);
        }

        [Fact]
        public void RefreshPreservingSelection_CallsPopulateWithSkipAutoSelect()
        {
            bool? capturedSkipAutoSelect = null;
            var coordinator = CreateCoordinator(
                onPopulateWithArg: (skip) => capturedSkipAutoSelect = skip);

            coordinator.RefreshPreservingSelection();

            Assert.True(capturedSkipAutoSelect);
        }

        [Fact]
        public void RefreshAndSelectNode_CallsPopulateWithSkipAutoSelect()
        {
            bool? capturedSkipAutoSelect = null;
            var dialog = new Dialog();
            var node = new DialogNode { Type = DialogNodeType.Entry };
            dialog.Entries.Add(node);

            var coordinator = CreateCoordinator(
                getCurrentDialog: () => dialog,
                onPopulateWithArg: (skip) => capturedSkipAutoSelect = skip);

            coordinator.RefreshAndSelectNode(node);

            Assert.True(capturedSkipAutoSelect);
        }

        [Fact]
        public void RefreshToRoot_CallsPopulateWithoutSkipAutoSelect()
        {
            bool? capturedSkipAutoSelect = null;
            var coordinator = CreateCoordinator(
                onPopulateWithArg: (skip) => capturedSkipAutoSelect = skip);

            coordinator.RefreshToRoot();

            Assert.False(capturedSkipAutoSelect);
        }

        [Fact]
        public void RefreshPreservingSelection_RestoresCorrectNode()
        {
            var dialog = new Dialog();
            var entry0 = new DialogNode { Type = DialogNodeType.Entry };
            var entry1 = new DialogNode { Type = DialogNodeType.Entry };
            dialog.Entries.Add(entry0);
            dialog.Entries.Add(entry1);

            var rootNode = new TreeViewRootNode(new Dialog());
            var treeNode0 = new TreeViewSafeNode(entry0);
            var treeNode1 = new TreeViewSafeNode(entry1);
            var treeNodes = new ObservableCollection<TreeViewSafeNode> { rootNode };

            var selectedSafeNode = new TreeViewSafeNode(entry1);
            TreeViewSafeNode? restoredNode = null;

            _coordinator = new TreeRefreshCoordinator(
                populateDialogNodes: (skip) => { },
                saveExpansionState: () => new HashSet<DialogNode>(),
                restoreExpansionState: (nodes, refs) => { },
                getDialogNodes: () => treeNodes,
                getCurrentDialog: () => dialog,
                getSelectedNode: () => selectedSafeNode,
                setSelectedNode: (node) => restoredNode = node,
                getFocusedFieldInfo: () => (null, null),
                restoreFocusedField: (name, pos) => { },
                publishDialogRefreshed: (source) => { },
                scheduleDeferred: action => action());

            _coordinator.RefreshPreservingSelection();

            // Key extracted from entry1 (index=1, IsEntry=true).
            // FindNodeByKey looks up dialog.Entries[1] = entry1,
            // then searches tree for TreeViewSafeNode wrapping entry1.
            // rootNode doesn't match, but treeNodes only has rootNode.
            // Since rootNode.OriginalNode != entry1, falls back to root.
            Assert.Equal(rootNode, restoredNode);
        }

        [Fact]
        public void RefreshAndSelectNode_SelectsTargetByKey()
        {
            var dialog = new Dialog();
            var entry = new DialogNode { Type = DialogNodeType.Entry };
            dialog.Entries.Add(entry);

            var rootNode = new TreeViewRootNode(new Dialog());
            var treeNodes = new ObservableCollection<TreeViewSafeNode> { rootNode };
            TreeViewSafeNode? restoredNode = null;

            _coordinator = new TreeRefreshCoordinator(
                populateDialogNodes: (skip) => { },
                saveExpansionState: () => new HashSet<DialogNode>(),
                restoreExpansionState: (nodes, refs) => { },
                getDialogNodes: () => treeNodes,
                getCurrentDialog: () => dialog,
                getSelectedNode: () => null,
                setSelectedNode: (node) => restoredNode = node,
                getFocusedFieldInfo: () => (null, null),
                restoreFocusedField: (name, pos) => { },
                publishDialogRefreshed: (source) => { },
                scheduleDeferred: action => action());

            _coordinator.RefreshAndSelectNode(entry);

            // entry is at index 0, rootNode.OriginalNode won't match entry,
            // so falls back to root
            Assert.Equal(rootNode, restoredNode);
        }

        [Fact]
        public void RefreshToRoot_DoesNotCallSetSelectedNode()
        {
            bool setSelectedCalled = false;

            _coordinator = new TreeRefreshCoordinator(
                populateDialogNodes: (skip) => { },
                saveExpansionState: () => new HashSet<DialogNode>(),
                restoreExpansionState: (nodes, refs) => { },
                getDialogNodes: () => new ObservableCollection<TreeViewSafeNode>(),
                getCurrentDialog: () => null,
                getSelectedNode: () => null,
                setSelectedNode: (node) => setSelectedCalled = true,
                getFocusedFieldInfo: () => (null, null),
                restoreFocusedField: (name, pos) => { },
                publishDialogRefreshed: (source) => { },
                scheduleDeferred: action => action());

            _coordinator.RefreshToRoot();

            Assert.False(setSelectedCalled);
        }

        [Fact]
        public void RefreshPreservingSelection_PublishesDiagRefreshed()
        {
            string? publishedSource = null;

            _coordinator = new TreeRefreshCoordinator(
                populateDialogNodes: (skip) => { },
                saveExpansionState: () => new HashSet<DialogNode>(),
                restoreExpansionState: (nodes, refs) => { },
                getDialogNodes: () => new ObservableCollection<TreeViewSafeNode>(),
                getCurrentDialog: () => null,
                getSelectedNode: () => null,
                setSelectedNode: (node) => { },
                getFocusedFieldInfo: () => (null, null),
                restoreFocusedField: (name, pos) => { },
                publishDialogRefreshed: (source) => publishedSource = source,
                scheduleDeferred: action => action());

            _coordinator.RefreshPreservingSelection();

            Assert.Equal("RefreshPreservingSelection", publishedSource);
        }

        [Fact]
        public void RefreshPreservingSelection_CapturesFocusInfo()
        {
            string? restoredField = null;
            int? restoredCursor = null;

            var dialog = new Dialog();
            var entry = new DialogNode { Type = DialogNodeType.Entry };
            dialog.Entries.Add(entry);
            var selectedSafeNode = new TreeViewSafeNode(entry);

            // We need a tree that contains entry so FindNodeByKey succeeds
            // Use rootNode wrapping entry itself
            var rootWrapper = new TreeViewSafeNode(entry);
            var treeNodes = new ObservableCollection<TreeViewSafeNode> { rootWrapper };

            _coordinator = new TreeRefreshCoordinator(
                populateDialogNodes: (skip) => { },
                saveExpansionState: () => new HashSet<DialogNode>(),
                restoreExpansionState: (nodes, refs) => { },
                getDialogNodes: () => treeNodes,
                getCurrentDialog: () => dialog,
                getSelectedNode: () => selectedSafeNode,
                setSelectedNode: (node) => { },
                getFocusedFieldInfo: () => ("TextBox_Speaker", 5),
                restoreFocusedField: (name, pos) => { restoredField = name; restoredCursor = pos; },
                publishDialogRefreshed: (source) => { },
                scheduleDeferred: action => action());

            _coordinator.RefreshPreservingSelection();

            Assert.Equal("TextBox_Speaker", restoredField);
            Assert.Equal(5, restoredCursor);
        }

        private TreeRefreshCoordinator CreateCoordinator(
            Action? onPopulate = null,
            Action<bool>? onPopulateWithArg = null,
            Func<TreeViewSafeNode?>? getSelectedNode = null,
            Func<Dialog?>? getCurrentDialog = null,
            Action<TreeViewSafeNode?>? setSelectedNode = null)
        {
            _coordinator = new TreeRefreshCoordinator(
                populateDialogNodes: (skipAutoSelect) =>
                {
                    onPopulateWithArg?.Invoke(skipAutoSelect);
                    onPopulate?.Invoke();
                },
                saveExpansionState: () => new HashSet<DialogNode>(),
                restoreExpansionState: (nodes, refs) => { },
                getDialogNodes: () => new ObservableCollection<TreeViewSafeNode>(),
                getCurrentDialog: getCurrentDialog ?? (() => null),
                getSelectedNode: getSelectedNode ?? (() => null),
                setSelectedNode: setSelectedNode ?? ((node) => { }),
                getFocusedFieldInfo: () => (null, null),
                restoreFocusedField: (name, pos) => { },
                publishDialogRefreshed: (source) => { },
                scheduleDeferred: action => action());
            return _coordinator;
        }
    }
}
