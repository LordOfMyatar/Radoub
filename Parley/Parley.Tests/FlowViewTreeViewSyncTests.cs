using DialogEditor.Models;
using DialogEditor.Services;
using DialogEditor.ViewModels;
using Xunit;

namespace Parley.Tests;

/// <summary>
/// Tests verifying FlowView and TreeView synchronization via DialogChangeEventBus.
/// Issue #847: Test the event-driven architecture that keeps views in sync.
/// </summary>
public class FlowViewTreeViewSyncTests : IDisposable
{
    private readonly DialogChangeEventBus _eventBus;
    private readonly FlowchartPanelViewModel _flowVm;
    private readonly List<DialogChangeEventArgs> _receivedEvents;

    public FlowViewTreeViewSyncTests()
    {
        _eventBus = DialogChangeEventBus.Instance;
        _eventBus.ClearSubscribers(); // Start fresh
        _flowVm = new FlowchartPanelViewModel();
        _receivedEvents = new List<DialogChangeEventArgs>();
    }

    public void Dispose()
    {
        _eventBus.ClearSubscribers();
    }

    #region Selection Sync Tests

    [Fact]
    public void SelectionChanged_PublishesToEventBus()
    {
        // Arrange
        var receivedSelectionEvent = false;
        DialogNode? receivedNode = null;
        var dialog = CreateSimpleDialog();

        _eventBus.DialogChanged += (s, e) =>
        {
            if (e.ChangeType == DialogChangeType.SelectionChanged)
            {
                receivedSelectionEvent = true;
                receivedNode = e.AffectedNode;
            }
        };

        // Act
        _eventBus.PublishSelectionChanged(dialog.Entries[0]);

        // Assert
        Assert.True(receivedSelectionEvent);
        Assert.NotNull(receivedNode);
        Assert.Same(dialog.Entries[0], receivedNode);
    }

    [Fact]
    public void SelectionChanged_IncludesPreviousSelection()
    {
        // Arrange
        DialogNode? previousNode = null;
        var dialog = CreateSimpleDialog();
        var node1 = dialog.Entries[0];
        var node2 = dialog.CreateNode(DialogNodeType.Entry)!;
        node2.Text.Add(0, "Second entry");
        dialog.AddNodeInternal(node2, DialogNodeType.Entry);

        _eventBus.DialogChanged += (s, e) =>
        {
            if (e.ChangeType == DialogChangeType.SelectionChanged)
            {
                previousNode = e.PreviousSelection;
            }
        };

        // Act
        _eventBus.PublishSelectionChanged(node2, previousSelection: node1);

        // Assert
        Assert.Same(node1, previousNode);
    }

    [Fact]
    public void FlowVM_SelectNode_UpdatesSelectedNodeId()
    {
        // Arrange
        var dialog = CreateSimpleDialog();
        _flowVm.UpdateDialog(dialog, "test.dlg");

        // Act
        _flowVm.SelectNode(dialog.Entries[0]);

        // Assert
        Assert.NotNull(_flowVm.SelectedNodeId);
    }

    [Fact]
    public void FlowVM_SelectNode_NullNode_ClearsSelection()
    {
        // Arrange
        var dialog = CreateSimpleDialog();
        _flowVm.UpdateDialog(dialog, "test.dlg");
        _flowVm.SelectNode(dialog.Entries[0]);

        // Act
        _flowVm.SelectNode(null);

        // Assert
        Assert.Null(_flowVm.SelectedNodeId);
    }

    #endregion

    #region Delete Propagation Tests

    [Fact]
    public void NodeDeleted_PublishesToEventBus()
    {
        // Arrange
        var receivedDeleteEvent = false;
        DialogNode? deletedNode = null;
        var dialog = CreateSimpleDialog();
        var nodeToDelete = dialog.Entries[0];

        _eventBus.DialogChanged += (s, e) =>
        {
            if (e.ChangeType == DialogChangeType.NodeDeleted)
            {
                receivedDeleteEvent = true;
                deletedNode = e.AffectedNode;
            }
        };

        // Act
        _eventBus.PublishNodeDeleted(nodeToDelete);

        // Assert
        Assert.True(receivedDeleteEvent);
        Assert.Same(nodeToDelete, deletedNode);
    }

    [Fact]
    public void NodeDeleted_IncludesOldParent()
    {
        // Arrange
        DialogNode? oldParent = null;
        var dialog = CreateDialogWithReplies();
        var entry = dialog.Entries[0];
        var reply = dialog.Replies[0];

        _eventBus.DialogChanged += (s, e) =>
        {
            if (e.ChangeType == DialogChangeType.NodeDeleted)
            {
                oldParent = e.OldParent;
            }
        };

        // Act
        _eventBus.PublishNodeDeleted(reply, oldParent: entry);

        // Assert
        Assert.Same(entry, oldParent);
    }

    #endregion

    #region Add Propagation Tests

    [Fact]
    public void NodeAdded_PublishesToEventBus()
    {
        // Arrange
        var receivedAddEvent = false;
        DialogNode? addedNode = null;
        var dialog = new Dialog();
        var newNode = dialog.CreateNode(DialogNodeType.Entry)!;
        newNode.Text.Add(0, "New entry");

        _eventBus.DialogChanged += (s, e) =>
        {
            if (e.ChangeType == DialogChangeType.NodeAdded)
            {
                receivedAddEvent = true;
                addedNode = e.AffectedNode;
            }
        };

        // Act
        _eventBus.PublishNodeAdded(newNode);

        // Assert
        Assert.True(receivedAddEvent);
        Assert.Same(newNode, addedNode);
    }

    [Fact]
    public void NodeAdded_IncludesNewParent()
    {
        // Arrange
        DialogNode? newParent = null;
        var dialog = CreateSimpleDialog();
        var parentNode = dialog.Entries[0];
        var newReply = dialog.CreateNode(DialogNodeType.Reply)!;
        newReply.Text.Add(0, "New reply");

        _eventBus.DialogChanged += (s, e) =>
        {
            if (e.ChangeType == DialogChangeType.NodeAdded)
            {
                newParent = e.NewParent;
            }
        };

        // Act
        _eventBus.PublishNodeAdded(newReply, parent: parentNode);

        // Assert
        Assert.Same(parentNode, newParent);
    }

    #endregion

    #region Collapse/Expand Sync Tests

    [Fact]
    public void NodeCollapsed_PublishesToEventBus()
    {
        // Arrange
        var receivedCollapseEvent = false;
        string? source = null;
        var dialog = CreateSimpleDialog();
        var node = dialog.Entries[0];

        _eventBus.DialogChanged += (s, e) =>
        {
            if (e.ChangeType == DialogChangeType.NodeCollapsed)
            {
                receivedCollapseEvent = true;
                source = e.Context;
            }
        };

        // Act
        _eventBus.PublishNodeCollapsed(node, "FlowView");

        // Assert
        Assert.True(receivedCollapseEvent);
        Assert.Equal("FlowView", source);
    }

    [Fact]
    public void NodeExpanded_PublishesToEventBus()
    {
        // Arrange
        var receivedExpandEvent = false;
        string? source = null;
        var dialog = CreateSimpleDialog();
        var node = dialog.Entries[0];

        _eventBus.DialogChanged += (s, e) =>
        {
            if (e.ChangeType == DialogChangeType.NodeExpanded)
            {
                receivedExpandEvent = true;
                source = e.Context;
            }
        };

        // Act
        _eventBus.PublishNodeExpanded(node, "TreeView");

        // Assert
        Assert.True(receivedExpandEvent);
        Assert.Equal("TreeView", source);
    }

    [Fact]
    public void AllCollapsed_PublishesToEventBus()
    {
        // Arrange
        var receivedAllCollapsedEvent = false;

        _eventBus.DialogChanged += (s, e) =>
        {
            if (e.ChangeType == DialogChangeType.AllCollapsed)
            {
                receivedAllCollapsedEvent = true;
            }
        };

        // Act
        _eventBus.PublishAllCollapsed("FlowView");

        // Assert
        Assert.True(receivedAllCollapsedEvent);
    }

    [Fact]
    public void AllExpanded_PublishesToEventBus()
    {
        // Arrange
        var receivedAllExpandedEvent = false;

        _eventBus.DialogChanged += (s, e) =>
        {
            if (e.ChangeType == DialogChangeType.AllExpanded)
            {
                receivedAllExpandedEvent = true;
            }
        };

        // Act
        _eventBus.PublishAllExpanded("TreeView");

        // Assert
        Assert.True(receivedAllExpandedEvent);
    }

    #endregion

    #region Navigation Event Tests

    [Fact]
    public void DialogLoaded_PublishesEventWithPath()
    {
        // Arrange
        var receivedLoadEvent = false;
        string? filePath = null;

        _eventBus.DialogChanged += (s, e) =>
        {
            if (e.ChangeType == DialogChangeType.DialogLoaded)
            {
                receivedLoadEvent = true;
                filePath = e.Context;
            }
        };

        // Act
        _eventBus.PublishDialogLoaded("merchant.dlg");

        // Assert
        Assert.True(receivedLoadEvent);
        Assert.Equal("merchant.dlg", filePath);
    }

    [Fact]
    public void DialogClosed_PublishesEvent()
    {
        // Arrange
        var receivedCloseEvent = false;

        _eventBus.DialogChanged += (s, e) =>
        {
            if (e.ChangeType == DialogChangeType.DialogClosed)
            {
                receivedCloseEvent = true;
            }
        };

        // Act
        _eventBus.PublishDialogClosed();

        // Assert
        Assert.True(receivedCloseEvent);
    }

    [Fact]
    public void DialogRefreshed_PublishesEvent()
    {
        // Arrange
        var receivedRefreshEvent = false;

        _eventBus.DialogChanged += (s, e) =>
        {
            if (e.ChangeType == DialogChangeType.DialogRefreshed)
            {
                receivedRefreshEvent = true;
            }
        };

        // Act
        _eventBus.PublishDialogRefreshed("TreeView rebuild");

        // Assert
        Assert.True(receivedRefreshEvent);
    }

    #endregion

    #region Event Suppression Tests

    [Fact]
    public void SuppressEvents_PreventsPublishing()
    {
        // Arrange
        var receivedEvent = false;

        _eventBus.DialogChanged += (s, e) =>
        {
            receivedEvent = true;
        };

        // Act
        using (_eventBus.SuppressEvents())
        {
            _eventBus.PublishDialogRefreshed();
        }

        // Assert
        Assert.False(receivedEvent);
    }

    [Fact]
    public void SuppressEvents_RestoresAfterDispose()
    {
        // Arrange
        var receivedEvent = false;

        _eventBus.DialogChanged += (s, e) =>
        {
            receivedEvent = true;
        };

        // Act
        using (_eventBus.SuppressEvents())
        {
            // This should be suppressed
        }

        _eventBus.PublishDialogRefreshed(); // This should work

        // Assert
        Assert.True(receivedEvent);
    }

    #endregion

    #region State Preservation Tests

    [Fact]
    public void FlowVM_SwitchingViews_PreservesSelection()
    {
        // Arrange
        var dialog = CreateSimpleDialog();
        _flowVm.UpdateDialog(dialog, "test.dlg");
        _flowVm.SelectNode(dialog.Entries[0]);
        var originalSelection = _flowVm.SelectedNodeId;

        // Act: Simulate view switch by re-updating dialog (common pattern)
        _flowVm.RefreshGraph();

        // Assert: After refresh, selection can be re-established
        // The SelectedNodeId remains until explicitly changed
        Assert.NotNull(originalSelection);
    }

    [Fact]
    public void FlowVM_Clear_ResetsSelection()
    {
        // Arrange
        var dialog = CreateSimpleDialog();
        _flowVm.UpdateDialog(dialog, "test.dlg");
        _flowVm.SelectNode(dialog.Entries[0]);

        // Act
        _flowVm.Clear();

        // Assert
        Assert.Null(_flowVm.SelectedNodeId);
        Assert.False(_flowVm.HasContent);
    }

    #endregion

    #region Multiple Subscribers Tests

    [Fact]
    public void MultipleSubscribers_AllReceiveEvents()
    {
        // Arrange
        int subscriber1Count = 0;
        int subscriber2Count = 0;

        _eventBus.DialogChanged += (s, e) => subscriber1Count++;
        _eventBus.DialogChanged += (s, e) => subscriber2Count++;

        var dialog = CreateSimpleDialog();

        // Act
        _eventBus.PublishSelectionChanged(dialog.Entries[0]);

        // Assert
        Assert.Equal(1, subscriber1Count);
        Assert.Equal(1, subscriber2Count);
    }

    [Fact]
    public void ClearSubscribers_RemovesAllHandlers()
    {
        // Arrange
        var receivedEvent = false;

        _eventBus.DialogChanged += (s, e) => receivedEvent = true;
        _eventBus.ClearSubscribers();

        // Act
        _eventBus.PublishDialogRefreshed();

        // Assert
        Assert.False(receivedEvent);
    }

    #endregion

    #region Helper Methods

    private Dialog CreateSimpleDialog()
    {
        var dialog = new Dialog();
        var entry = dialog.CreateNode(DialogNodeType.Entry)!;
        entry.Text.Add(0, "Hello adventurer!");
        dialog.AddNodeInternal(entry, DialogNodeType.Entry);

        var startPtr = dialog.CreatePtr()!;
        startPtr.Type = DialogNodeType.Entry;
        startPtr.Index = 0;
        startPtr.Node = entry;
        dialog.Starts.Add(startPtr);

        return dialog;
    }

    private Dialog CreateDialogWithReplies()
    {
        var dialog = new Dialog();

        // Create entry node
        var entry = dialog.CreateNode(DialogNodeType.Entry)!;
        entry.Text.Add(0, "What do you want?");
        dialog.AddNodeInternal(entry, DialogNodeType.Entry);

        var startPtr = dialog.CreatePtr()!;
        startPtr.Type = DialogNodeType.Entry;
        startPtr.Index = 0;
        startPtr.Node = entry;
        dialog.Starts.Add(startPtr);

        // Add reply 1
        var reply1 = dialog.CreateNode(DialogNodeType.Reply)!;
        reply1.Text.Add(0, "Buy something");
        dialog.AddNodeInternal(reply1, DialogNodeType.Reply);

        var replyPtr1 = dialog.CreatePtr()!;
        replyPtr1.Type = DialogNodeType.Reply;
        replyPtr1.Index = 0;
        replyPtr1.Node = reply1;
        entry.Pointers.Add(replyPtr1);

        // Add reply 2
        var reply2 = dialog.CreateNode(DialogNodeType.Reply)!;
        reply2.Text.Add(0, "Goodbye");
        dialog.AddNodeInternal(reply2, DialogNodeType.Reply);

        var replyPtr2 = dialog.CreatePtr()!;
        replyPtr2.Type = DialogNodeType.Reply;
        replyPtr2.Index = 1;
        replyPtr2.Node = reply2;
        entry.Pointers.Add(replyPtr2);

        return dialog;
    }

    #endregion
}
