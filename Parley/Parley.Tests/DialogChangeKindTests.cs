using DialogEditor.Models;
using DialogEditor.Services;
using Xunit;

namespace Parley.Tests
{
    /// <summary>
    /// Tests for DialogChangeKind — the gate that lets text-only edits update
    /// visible labels without forcing a full tree/flow rebuild (#2032).
    /// </summary>
    public class DialogChangeKindTests
    {
        [Fact]
        public void DialogChangeEventArgs_DefaultsChangeKindToStructural()
        {
            // Arrange: construct an event args using the existing minimal ctor.
            // Back-compat: callers that don't pass a kind must still get Structural
            // behavior so existing rebuild paths keep working.
            var args = new DialogChangeEventArgs(DialogChangeType.NodeModified);

            // Assert
            Assert.Equal(DialogChangeKind.Structural, args.ChangeKind);
        }

        [Fact]
        public void DialogChangeEventArgs_AcceptsTextOnlyChangeKind()
        {
            // Arrange
            var args = new DialogChangeEventArgs(
                DialogChangeType.NodeModified,
                changeKind: DialogChangeKind.TextOnly);

            // Assert
            Assert.Equal(DialogChangeKind.TextOnly, args.ChangeKind);
        }

        [Fact]
        public void PublishNodeModified_WithoutKind_DefaultsToStructural()
        {
            // Arrange
            var bus = DialogChangeEventBus.Instance;
            DialogChangeEventArgs? received = null;
            void Handler(object? s, DialogChangeEventArgs e) => received = e;
            bus.DialogChanged += Handler;

            try
            {
                // Act: existing call shape must keep working
                bus.PublishNodeModified(new DialogNode(), "TextChanged");

                // Assert
                Assert.NotNull(received);
                Assert.Equal(DialogChangeType.NodeModified, received!.ChangeType);
                Assert.Equal(DialogChangeKind.Structural, received.ChangeKind);
            }
            finally
            {
                bus.DialogChanged -= Handler;
            }
        }

        [Fact]
        public void PublishNodeModified_WithTextOnlyKind_PropagatesKind()
        {
            // Arrange
            var bus = DialogChangeEventBus.Instance;
            DialogChangeEventArgs? received = null;
            void Handler(object? s, DialogChangeEventArgs e) => received = e;
            bus.DialogChanged += Handler;

            try
            {
                // Act
                bus.PublishNodeModified(new DialogNode(), "TextChanged", DialogChangeKind.TextOnly);

                // Assert
                Assert.NotNull(received);
                Assert.Equal(DialogChangeKind.TextOnly, received!.ChangeKind);
            }
            finally
            {
                bus.DialogChanged -= Handler;
            }
        }
    }
}
