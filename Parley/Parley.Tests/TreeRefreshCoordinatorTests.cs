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
