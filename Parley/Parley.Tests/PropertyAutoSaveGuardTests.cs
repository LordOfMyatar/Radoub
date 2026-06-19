using System.Collections.ObjectModel;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using DialogEditor.Models;
using DialogEditor.Services;
using Xunit;

namespace Parley.Tests
{
    /// <summary>
    /// Guards the focus-change auto-save path against cross-node corruption (#2521).
    ///
    /// Repro: Ctrl+D creates+selects a new (empty) node DURING a tree refresh. The
    /// selection-changed handler that repopulates the properties panel early-returns while
    /// the refresh coordinator is busy, so the shared TextTextBox keeps the PREVIOUS node's
    /// text. When focus then moves, OnFieldLostFocus -> AutoSaveProperty flushes that stale
    /// text into the new node. The other save path (SaveCurrentNodeProperties) already has
    /// the #2382 PropertyFlushGuard; this auto-save path did not.
    ///
    /// Guard: AutoSaveProperty must refuse to write when the panel's source node
    /// (lastPopulatedNode) is not the currently-selected node.
    /// </summary>
    public class PropertyAutoSaveGuardTests
    {
        private static DialogNode MakeNode(Dialog dialog, DialogNodeType type, string text)
        {
            var n = dialog.CreateNode(type)!;
            n.Text.Add(0, text);
            dialog.AddNodeInternal(n, type);
            return n;
        }

        private static PropertyAutoSaveService MakeService(
            TextBox textBox,
            System.Func<DialogNode?> getLastPopulatedNode)
        {
            var coordinator = new TreeRefreshCoordinator(
                populateDialogNodes: _ => { },
                saveExpansionState: () => new HashSet<DialogNode>(),
                restoreExpansionState: (_, _) => { },
                getDialogNodes: () => new ObservableCollection<TreeViewSafeNode>(),
                getCurrentDialog: () => null,
                getSelectedNode: () => null,
                setSelectedNode: _ => { },
                getFocusedFieldInfo: () => (null, null),
                restoreFocusedField: (_, _) => { },
                publishDialogRefreshed: _ => { },
                scheduleDeferred: action => action());

            return new PropertyAutoSaveService(
                findControl: name => name == "TextTextBox" ? textBox : null,
                treeRefreshCoordinator: coordinator,
                loadScriptPreview: (_, _) => { },
                clearScriptPreview: _ => { },
                triggerDebouncedAutoSave: () => { },
                getLastPopulatedNode: getLastPopulatedNode);
        }

        [AvaloniaFact]
        public void AutoSaveProperty_PanelOutOfSyncWithSelection_DoesNotWriteStaleText()
        {
            var dialog = new Dialog();
            var oldNode = MakeNode(dialog, DialogNodeType.Entry, "Previous line");
            var newNode = MakeNode(dialog, DialogNodeType.Entry, ""); // brand-new empty node

            // TextBox still holds the PREVIOUS node's text (panel never repopulated for newNode).
            var textBox = new TextBox { Name = "TextTextBox", Text = "Previous line" };

            // Panel was last populated FROM oldNode, but selection is newNode → out of sync.
            var service = MakeService(textBox, getLastPopulatedNode: () => oldNode);

            var result = service.AutoSaveProperty(new TreeViewSafeNode(newNode), "TextTextBox");

            // The stale text must NOT be captured into the new node.
            Assert.Equal("", newNode.Text.GetDefault());
            Assert.False(result.Success);
        }

        [AvaloniaFact]
        public void AutoSaveProperty_PanelInSyncWithSelection_WritesText()
        {
            var dialog = new Dialog();
            var node = MakeNode(dialog, DialogNodeType.Entry, "old");

            var textBox = new TextBox { Name = "TextTextBox", Text = "edited" };

            // Panel populated FROM the same node that is selected → safe to flush.
            var service = MakeService(textBox, getLastPopulatedNode: () => node);

            var result = service.AutoSaveProperty(new TreeViewSafeNode(node), "TextTextBox");

            Assert.Equal("edited", node.Text.GetDefault());
            Assert.True(result.Success);
        }
    }
}
