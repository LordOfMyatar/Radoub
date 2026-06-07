using DialogEditor.Models;
using DialogEditor.Services;
using Xunit;

namespace Parley.Tests
{
    /// <summary>
    /// Tests for the property-panel flush guard (#2382 data-loss bug).
    ///
    /// Repro: drag a reply thread between two conversations and back. After the move,
    /// the TreeView restored selection to a sibling ("No") while the property panel still
    /// displayed the dragged node ("Yes"). The next SaveCurrentNodeProperties flushed the
    /// stale "Yes" TextBox onto the "No" DialogNode — overwriting No.Text with "Yes".
    ///
    /// Guard: only flush the panel back to the model when the panel was last populated FROM
    /// the same DialogNode that is currently selected. If they diverge, the panel is stale
    /// and a flush would corrupt the wrong node.
    /// </summary>
    public class PropertyFlushGuardTests
    {
        private static DialogNode MakeNode(Dialog dialog, DialogNodeType type, string text)
        {
            var n = dialog.CreateNode(type)!;
            n.Text.Add(0, text);
            dialog.AddNodeInternal(n, type);
            return n;
        }

        [Fact]
        public void ShouldFlush_SameNode_True()
        {
            var dialog = new Dialog();
            var yes = MakeNode(dialog, DialogNodeType.Reply, "Yes");
            Assert.True(PropertyFlushGuard.ShouldFlush(selectedNode: yes, lastPopulatedNode: yes));
        }

        [Fact]
        public void ShouldFlush_DivergedNodes_False()
        {
            var dialog = new Dialog();
            var yes = MakeNode(dialog, DialogNodeType.Reply, "Yes");
            var no = MakeNode(dialog, DialogNodeType.Reply, "No");
            // Panel shows Yes, selection is No → flushing would write Yes into No. Block it.
            Assert.False(PropertyFlushGuard.ShouldFlush(selectedNode: no, lastPopulatedNode: yes));
        }

        [Fact]
        public void ShouldFlush_NullSelected_False()
        {
            var dialog = new Dialog();
            var yes = MakeNode(dialog, DialogNodeType.Reply, "Yes");
            Assert.False(PropertyFlushGuard.ShouldFlush(selectedNode: null, lastPopulatedNode: yes));
        }

        [Fact]
        public void ShouldFlush_NullLastPopulated_False()
        {
            var dialog = new Dialog();
            var yes = MakeNode(dialog, DialogNodeType.Reply, "Yes");
            // Panel never populated for this selection → nothing safe to flush.
            Assert.False(PropertyFlushGuard.ShouldFlush(selectedNode: yes, lastPopulatedNode: null));
        }
    }
}
