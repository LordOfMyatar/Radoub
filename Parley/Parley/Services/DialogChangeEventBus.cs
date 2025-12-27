using System;
using Radoub.Formats.Logging;
using DialogEditor.Models;

namespace DialogEditor.Services
{
    /// <summary>
    /// Defines the types of changes that can occur to a dialog structure.
    /// </summary>
    public enum DialogChangeType
    {
        /// <summary>A new node was added to the dialog.</summary>
        NodeAdded,

        /// <summary>A node was deleted from the dialog.</summary>
        NodeDeleted,

        /// <summary>A node was moved (reordered or reparented).</summary>
        NodeMoved,

        /// <summary>The selected node changed.</summary>
        SelectionChanged,

        /// <summary>A node's properties were modified.</summary>
        NodeModified,

        /// <summary>The entire dialog structure was refreshed.</summary>
        DialogRefreshed,

        /// <summary>A dialog file was loaded.</summary>
        DialogLoaded,

        /// <summary>A dialog file was closed.</summary>
        DialogClosed,

        /// <summary>A node was collapsed (children hidden) in a view.</summary>
        NodeCollapsed,

        /// <summary>A node was expanded (children shown) in a view.</summary>
        NodeExpanded,

        /// <summary>All nodes were collapsed in a view.</summary>
        AllCollapsed,

        /// <summary>All nodes were expanded in a view.</summary>
        AllExpanded
    }

    /// <summary>
    /// Event arguments containing details about a dialog change.
    /// </summary>
    public class DialogChangeEventArgs : EventArgs
    {
        /// <summary>The type of change that occurred.</summary>
        public DialogChangeType ChangeType { get; }

        /// <summary>The node that was affected (added, deleted, moved, or selected).</summary>
        public DialogNode? AffectedNode { get; }

        /// <summary>For move operations, the new parent node.</summary>
        public DialogNode? NewParent { get; }

        /// <summary>For move operations, the previous parent node.</summary>
        public DialogNode? OldParent { get; }

        /// <summary>For selection changes, the previously selected node.</summary>
        public DialogNode? PreviousSelection { get; }

        /// <summary>Optional additional context about the change.</summary>
        public string? Context { get; }

        public DialogChangeEventArgs(
            DialogChangeType changeType,
            DialogNode? affectedNode = null,
            DialogNode? newParent = null,
            DialogNode? oldParent = null,
            DialogNode? previousSelection = null,
            string? context = null)
        {
            ChangeType = changeType;
            AffectedNode = affectedNode;
            NewParent = newParent;
            OldParent = oldParent;
            PreviousSelection = previousSelection;
            Context = context;
        }
    }

    /// <summary>
    /// Centralized event bus for dialog structure changes.
    /// Enables loose coupling between TreeView, FlowView, and other components
    /// that need to stay synchronized when the dialog changes.
    /// </summary>
    /// <remarks>
    /// Usage pattern:
    /// 1. Components subscribe to DialogChanged event on startup
    /// 2. When a change occurs (add/delete/move node), call Publish()
    /// 3. All subscribers receive notification and can update their views
    ///
    /// This replaces the fragile boolean flag approach with a proper event-driven architecture.
    /// </remarks>
    public class DialogChangeEventBus
    {
        private static DialogChangeEventBus? _instance;
        private static readonly object _lock = new();

        /// <summary>
        /// Singleton instance of the event bus.
        /// </summary>
        public static DialogChangeEventBus Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new DialogChangeEventBus();
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// Event fired when any dialog change occurs.
        /// Subscribe to this event to receive all change notifications.
        /// </summary>
        public event EventHandler<DialogChangeEventArgs>? DialogChanged;

        /// <summary>
        /// Flag to prevent recursive event firing during sync operations.
        /// </summary>
        public bool IsSuppressed { get; private set; }

        private DialogChangeEventBus()
        {
        }

        /// <summary>
        /// Publish a change event to all subscribers.
        /// </summary>
        /// <param name="args">Details about the change.</param>
        public void Publish(DialogChangeEventArgs args)
        {
            if (IsSuppressed)
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG,
                    $"DialogChangeEventBus: Suppressed {args.ChangeType} event");
                return;
            }

            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                $"DialogChangeEventBus: Publishing {args.ChangeType} event" +
                (args.AffectedNode != null ? $" for node '{args.AffectedNode.DisplayText.Substring(0, Math.Min(30, args.AffectedNode.DisplayText.Length))}'" : ""));

            DialogChanged?.Invoke(this, args);
        }

        /// <summary>
        /// Publish a node added event.
        /// </summary>
        public void PublishNodeAdded(DialogNode newNode, DialogNode? parent = null)
        {
            Publish(new DialogChangeEventArgs(
                DialogChangeType.NodeAdded,
                affectedNode: newNode,
                newParent: parent));
        }

        /// <summary>
        /// Publish a node deleted event.
        /// </summary>
        public void PublishNodeDeleted(DialogNode deletedNode, DialogNode? oldParent = null)
        {
            Publish(new DialogChangeEventArgs(
                DialogChangeType.NodeDeleted,
                affectedNode: deletedNode,
                oldParent: oldParent));
        }

        /// <summary>
        /// Publish a node moved event.
        /// </summary>
        public void PublishNodeMoved(DialogNode movedNode, DialogNode? newParent, DialogNode? oldParent)
        {
            Publish(new DialogChangeEventArgs(
                DialogChangeType.NodeMoved,
                affectedNode: movedNode,
                newParent: newParent,
                oldParent: oldParent));
        }

        /// <summary>
        /// Publish a selection changed event.
        /// </summary>
        public void PublishSelectionChanged(DialogNode? newSelection, DialogNode? previousSelection = null)
        {
            Publish(new DialogChangeEventArgs(
                DialogChangeType.SelectionChanged,
                affectedNode: newSelection,
                previousSelection: previousSelection));
        }

        /// <summary>
        /// Publish a node modified event (text, speaker, or other property changed).
        /// </summary>
        public void PublishNodeModified(DialogNode modifiedNode, string? context = null)
        {
            Publish(new DialogChangeEventArgs(
                DialogChangeType.NodeModified,
                affectedNode: modifiedNode,
                context: context));
        }

        /// <summary>
        /// Publish a dialog refreshed event (tree was rebuilt).
        /// </summary>
        public void PublishDialogRefreshed(string? context = null)
        {
            Publish(new DialogChangeEventArgs(
                DialogChangeType.DialogRefreshed,
                context: context));
        }

        /// <summary>
        /// Publish a dialog loaded event.
        /// </summary>
        public void PublishDialogLoaded(string? filePath = null)
        {
            Publish(new DialogChangeEventArgs(
                DialogChangeType.DialogLoaded,
                context: filePath));
        }

        /// <summary>
        /// Publish a dialog closed event.
        /// </summary>
        public void PublishDialogClosed()
        {
            Publish(new DialogChangeEventArgs(DialogChangeType.DialogClosed));
        }

        /// <summary>
        /// Publish a node collapsed event.
        /// </summary>
        /// <param name="collapsedNode">The node that was collapsed.</param>
        /// <param name="source">Source of the collapse (e.g., "FlowView", "TreeView").</param>
        public void PublishNodeCollapsed(DialogNode collapsedNode, string? source = null)
        {
            Publish(new DialogChangeEventArgs(
                DialogChangeType.NodeCollapsed,
                affectedNode: collapsedNode,
                context: source));
        }

        /// <summary>
        /// Publish a node expanded event.
        /// </summary>
        /// <param name="expandedNode">The node that was expanded.</param>
        /// <param name="source">Source of the expand (e.g., "FlowView", "TreeView").</param>
        public void PublishNodeExpanded(DialogNode expandedNode, string? source = null)
        {
            Publish(new DialogChangeEventArgs(
                DialogChangeType.NodeExpanded,
                affectedNode: expandedNode,
                context: source));
        }

        /// <summary>
        /// Publish an all-collapsed event.
        /// </summary>
        /// <param name="source">Source of the collapse all (e.g., "FlowView", "TreeView").</param>
        public void PublishAllCollapsed(string? source = null)
        {
            Publish(new DialogChangeEventArgs(
                DialogChangeType.AllCollapsed,
                context: source));
        }

        /// <summary>
        /// Publish an all-expanded event.
        /// </summary>
        /// <param name="source">Source of the expand all (e.g., "FlowView", "TreeView").</param>
        public void PublishAllExpanded(string? source = null)
        {
            Publish(new DialogChangeEventArgs(
                DialogChangeType.AllExpanded,
                context: source));
        }

        /// <summary>
        /// Temporarily suppress event publishing.
        /// Use this when performing batch operations to avoid multiple updates.
        /// </summary>
        /// <returns>A disposable that restores normal operation when disposed.</returns>
        public IDisposable SuppressEvents()
        {
            return new EventSuppressor(this);
        }

        private class EventSuppressor : IDisposable
        {
            private readonly DialogChangeEventBus _bus;
            private bool _disposed;

            public EventSuppressor(DialogChangeEventBus bus)
            {
                _bus = bus;
                _bus.IsSuppressed = true;
                UnifiedLogger.LogApplication(LogLevel.DEBUG,
                    "DialogChangeEventBus: Events suppressed");
            }

            public void Dispose()
            {
                if (!_disposed)
                {
                    _bus.IsSuppressed = false;
                    _disposed = true;
                    UnifiedLogger.LogApplication(LogLevel.DEBUG,
                        "DialogChangeEventBus: Events restored");
                }
            }
        }

        /// <summary>
        /// Clear all subscribers. Useful for testing or cleanup.
        /// </summary>
        public void ClearSubscribers()
        {
            DialogChanged = null;
        }
    }
}
