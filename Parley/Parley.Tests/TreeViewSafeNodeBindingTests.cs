using DialogEditor.Models;
using Xunit;

namespace Parley.Tests
{
    /// <summary>
    /// Tests that TreeViewSafeNode can refresh its DisplayText binding without
    /// a full tree rebuild — the per-node signal used when a text-only change
    /// updates the underlying DialogNode (#2032).
    /// </summary>
    public class TreeViewSafeNodeBindingTests
    {
        [Fact]
        public void NotifyTextChanged_RaisesPropertyChangedForDisplayText()
        {
            // Arrange: construct a node and a safe wrapper
            var originalNode = new DialogNode
            {
                Type = DialogNodeType.Entry,
                Speaker = "Guard"
            };
            originalNode.Text.Add(0, "original");
            var safe = new TreeViewSafeNode(originalNode);

            var propertyChanges = new List<string>();
            safe.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName != null) propertyChanges.Add(e.PropertyName);
            };

            // Act: simulate an external TextOnly update — the underlying node text changes
            // and we ask the safe wrapper to notify its bindings without rebuilding the tree.
            originalNode.Text.Add(0, "edited");
            safe.NotifyTextChanged();

            // Assert: binding target gets woken up
            Assert.Contains(nameof(TreeViewSafeNode.DisplayText), propertyChanges);
        }
    }
}
