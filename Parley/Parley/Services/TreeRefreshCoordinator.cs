using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using DialogEditor.Models;
using Radoub.Formats.Logging;

namespace DialogEditor.Services
{
    public enum TreeRefreshState
    {
        Idle,
        Refreshing,
        RestoringState
    }

    public class TreeRefreshCoordinator
    {
        private readonly Action<bool> _populateDialogNodes;
        private readonly Func<HashSet<DialogNode>> _saveExpansionState;
        private readonly Action<ObservableCollection<TreeViewSafeNode>, HashSet<DialogNode>> _restoreExpansionState;
        private readonly Func<ObservableCollection<TreeViewSafeNode>> _getDialogNodes;
        private readonly Func<Dialog?> _getCurrentDialog;
        private readonly Func<TreeViewSafeNode?> _getSelectedNode;
        private readonly Action<TreeViewSafeNode?> _setSelectedNode;
        private readonly Func<(string? fieldName, int? cursorPosition)> _getFocusedFieldInfo;
        private readonly Action<string?, int?> _restoreFocusedField;
        private readonly Action<string> _publishDialogRefreshed;
        private readonly Action<Action> _scheduleDeferred;

        public TreeRefreshState State { get; private set; } = TreeRefreshState.Idle;
        public bool IsBusy => State != TreeRefreshState.Idle;

        public TreeRefreshCoordinator(
            Action<bool> populateDialogNodes,
            Func<HashSet<DialogNode>> saveExpansionState,
            Action<ObservableCollection<TreeViewSafeNode>, HashSet<DialogNode>> restoreExpansionState,
            Func<ObservableCollection<TreeViewSafeNode>> getDialogNodes,
            Func<Dialog?> getCurrentDialog,
            Func<TreeViewSafeNode?> getSelectedNode,
            Action<TreeViewSafeNode?> setSelectedNode,
            Func<(string? fieldName, int? cursorPosition)> getFocusedFieldInfo,
            Action<string?, int?> restoreFocusedField,
            Action<string> publishDialogRefreshed,
            Action<Action>? scheduleDeferred = null)
        {
            _populateDialogNodes = populateDialogNodes;
            _saveExpansionState = saveExpansionState;
            _restoreExpansionState = restoreExpansionState;
            _getDialogNodes = getDialogNodes;
            _getCurrentDialog = getCurrentDialog;
            _getSelectedNode = getSelectedNode;
            _setSelectedNode = setSelectedNode;
            _getFocusedFieldInfo = getFocusedFieldInfo;
            _restoreFocusedField = restoreFocusedField;
            _publishDialogRefreshed = publishDialogRefreshed;
            _scheduleDeferred = scheduleDeferred
                ?? (action => Avalonia.Threading.Dispatcher.UIThread.Post(action, Avalonia.Threading.DispatcherPriority.Loaded));
        }

        public void RefreshPreservingSelection()
        {
            if (!TryBeginRefresh("RefreshPreservingSelection")) return;

            try
            {
                var selectedNode = _getSelectedNode();
                var dialog = _getCurrentDialog();
                TreeSelectionKey? selectionKey = null;

                if (selectedNode?.OriginalNode != null && dialog != null)
                {
                    var (focusedField, cursorPos) = _getFocusedFieldInfo();
                    selectionKey = TreeSelectionKey.FromDialogNode(
                        selectedNode.OriginalNode, dialog, focusedField, cursorPos);
                    UnifiedLogger.LogApplication(LogLevel.DEBUG,
                        $"TreeRefreshCoordinator: Captured key — " +
                        $"Index={selectionKey?.NodeIndex}, IsEntry={selectionKey?.IsEntry}, " +
                        $"Field={selectionKey?.FocusedFieldName}, Cursor={selectionKey?.CursorPosition}");
                }

                var expansionState = _saveExpansionState();
                _populateDialogNodes(true);

                ScheduleStateRestoration(expansionState, selectionKey, "RefreshPreservingSelection");
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN,
                    $"TreeRefreshCoordinator: RefreshPreservingSelection failed — {ex.Message}");
                TransitionTo(TreeRefreshState.Idle, "RefreshPreservingSelection (error recovery)");
            }
        }

        public void RefreshAndSelectNode(DialogNode target)
        {
            if (!TryBeginRefresh("RefreshAndSelectNode")) return;

            try
            {
                var dialog = _getCurrentDialog();
                TreeSelectionKey? selectionKey = null;

                if (dialog != null)
                {
                    selectionKey = TreeSelectionKey.FromDialogNode(target, dialog, focusedField: null, cursorPosition: null);
                    UnifiedLogger.LogApplication(LogLevel.DEBUG,
                        $"TreeRefreshCoordinator: RefreshAndSelectNode — " +
                        $"Target Index={selectionKey?.NodeIndex}, IsEntry={selectionKey?.IsEntry}");
                }

                var expansionState = _saveExpansionState();
                _populateDialogNodes(true);

                ScheduleStateRestoration(expansionState, selectionKey, "RefreshAndSelectNode");
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN,
                    $"TreeRefreshCoordinator: RefreshAndSelectNode failed — {ex.Message}");
                TransitionTo(TreeRefreshState.Idle, "RefreshAndSelectNode (error recovery)");
            }
        }

        public void RefreshToRoot()
        {
            if (!TryBeginRefresh("RefreshToRoot")) return;

            try
            {
                var expansionState = _saveExpansionState();
                _populateDialogNodes(false);

                ScheduleStateRestoration(expansionState, selectionKey: null, "RefreshToRoot");
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN,
                    $"TreeRefreshCoordinator: RefreshToRoot failed — {ex.Message}");
                TransitionTo(TreeRefreshState.Idle, "RefreshToRoot (error recovery)");
            }
        }

        private bool TryBeginRefresh(string caller)
        {
            if (State != TreeRefreshState.Idle)
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG,
                    $"TreeRefreshCoordinator: {caller} rejected — state is {State}");
                return false;
            }
            TransitionTo(TreeRefreshState.Refreshing, caller);
            return true;
        }

        private void ScheduleStateRestoration(
            HashSet<DialogNode> expansionState,
            TreeSelectionKey? selectionKey,
            string source)
        {
            _scheduleDeferred(() =>
            {
                try
                {
                    TransitionTo(TreeRefreshState.RestoringState, source);

                    var dialogNodes = _getDialogNodes();
                    _restoreExpansionState(dialogNodes, expansionState);

                    if (selectionKey != null)
                    {
                        var restoredNode = FindNodeByKey(dialogNodes, selectionKey);
                        _setSelectedNode(restoredNode);

                        if (restoredNode != null && selectionKey.FocusedFieldName != null)
                        {
                            _restoreFocusedField(selectionKey.FocusedFieldName, selectionKey.CursorPosition);
                        }
                    }

                    TransitionTo(TreeRefreshState.Idle, source);
                    _publishDialogRefreshed(source);
                }
                catch (Exception ex)
                {
                    UnifiedLogger.LogApplication(LogLevel.WARN,
                        $"TreeRefreshCoordinator: State restoration failed — {ex.Message}");
                    TransitionTo(TreeRefreshState.Idle, $"{source} (restoration error recovery)");
                }
            });
        }

        private TreeViewSafeNode? FindNodeByKey(
            ObservableCollection<TreeViewSafeNode> dialogNodes,
            TreeSelectionKey key)
        {
            var dialog = _getCurrentDialog();
            if (dialog == null) return GetRootNode(dialogNodes);

            var targetList = key.IsEntry ? dialog.Entries : dialog.Replies;
            if (key.NodeIndex < 0 || key.NodeIndex >= targetList.Count)
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG,
                    $"TreeRefreshCoordinator: Key index {key.NodeIndex} out of range, falling back to root");
                return GetRootNode(dialogNodes);
            }

            var targetDialogNode = targetList[key.NodeIndex];

            var found = FindTreeViewNodeByDialogNode(dialogNodes, targetDialogNode);
            if (found != null)
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG,
                    $"TreeRefreshCoordinator: Restored to original node (Index={key.NodeIndex})");
                return found;
            }

            return FindFallbackNode(dialogNodes, key, dialog);
        }

        private TreeViewSafeNode? FindFallbackNode(
            ObservableCollection<TreeViewSafeNode> dialogNodes,
            TreeSelectionKey key,
            Dialog dialog)
        {
            var targetList = key.IsEntry ? dialog.Entries : dialog.Replies;

            if (key.NodeIndex < targetList.Count)
            {
                var nextSibling = targetList[key.NodeIndex];
                var found = FindTreeViewNodeByDialogNode(dialogNodes, nextSibling);
                if (found != null)
                {
                    UnifiedLogger.LogApplication(LogLevel.DEBUG,
                        $"TreeRefreshCoordinator: Fell back to next sibling (Index={key.NodeIndex})");
                    return found;
                }
            }

            if (key.NodeIndex > 0 && key.NodeIndex - 1 < targetList.Count)
            {
                var prevSibling = targetList[key.NodeIndex - 1];
                var found = FindTreeViewNodeByDialogNode(dialogNodes, prevSibling);
                if (found != null)
                {
                    UnifiedLogger.LogApplication(LogLevel.DEBUG,
                        $"TreeRefreshCoordinator: Fell back to previous sibling (Index={key.NodeIndex - 1})");
                    return found;
                }
            }

            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                $"TreeRefreshCoordinator: No siblings found, falling back to root");
            return GetRootNode(dialogNodes);
        }

        private TreeViewSafeNode? FindTreeViewNodeByDialogNode(
            ObservableCollection<TreeViewSafeNode> nodes,
            DialogNode target)
        {
            foreach (var node in nodes)
            {
                var found = FindTreeViewNodeRecursive(node, target);
                if (found != null) return found;
            }
            return null;
        }

        private TreeViewSafeNode? FindTreeViewNodeRecursive(
            TreeViewSafeNode parent,
            DialogNode target,
            int maxDepth = 30)
        {
            if (maxDepth <= 0) return null;

            if (parent.OriginalNode == target)
                return parent;

            if (parent.Children != null)
            {
                foreach (var child in parent.Children)
                {
                    if (child is TreeViewPlaceholderNode) continue;
                    var found = FindTreeViewNodeRecursive(child, target, maxDepth - 1);
                    if (found != null)
                    {
                        parent.IsExpanded = true;
                        return found;
                    }
                }
            }
            return null;
        }

        private TreeViewSafeNode? GetRootNode(ObservableCollection<TreeViewSafeNode> dialogNodes)
        {
            return dialogNodes.Count > 0 ? dialogNodes[0] : null;
        }

        private void TransitionTo(TreeRefreshState newState, string context)
        {
            var oldState = State;
            State = newState;
            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                $"TreeRefreshCoordinator: {oldState} → {newState} ({context})");
        }
    }
}
