using Avalonia;
using DialogEditor.Services;
using Xunit;

namespace Parley.Tests
{
    /// <summary>
    /// Regression tests for the expanded-TreeViewItem drop-zone bug (#2382 spot-check):
    /// an expanded item's Bounds.Height includes its whole child subtree, so the header
    /// row falls inside the top-20% "Before" sliver of a tall item and same-type Before
    /// drops get rejected. The zone calc must run against the HEADER height, not the
    /// full item height.
    /// </summary>
    public class DropZoneHeightTests
    {
        [Fact]
        public void ResolveZoneHeight_HeaderSmallerThanItem_UsesHeader()
        {
            // Expanded item: full height 200 (header + children), header 24.
            Assert.Equal(24, DropZoneHeightService.ResolveZoneHeight(fullItemHeight: 200, headerHeight: 24));
        }

        [Fact]
        public void ResolveZoneHeight_NoHeaderMeasurement_FallsBackToItem()
        {
            Assert.Equal(200, DropZoneHeightService.ResolveZoneHeight(fullItemHeight: 200, headerHeight: null));
        }

        [Fact]
        public void ResolveZoneHeight_HeaderZeroOrNegative_FallsBackToItem()
        {
            Assert.Equal(200, DropZoneHeightService.ResolveZoneHeight(fullItemHeight: 200, headerHeight: 0));
            Assert.Equal(200, DropZoneHeightService.ResolveZoneHeight(fullItemHeight: 200, headerHeight: -5));
        }

        [Fact]
        public void ResolveZoneHeight_HeaderEqualsItem_Collapsed_UsesHeader()
        {
            // Collapsed item: header == full height. Either is fine; use header.
            Assert.Equal(24, DropZoneHeightService.ResolveZoneHeight(fullItemHeight: 24, headerHeight: 24));
        }

        [Fact]
        public void HeaderRowMidpoint_OnExpandedItem_IsIntoZone_NotBefore()
        {
            // Pointer in the middle of a 24px header on a 200px expanded item.
            var service = new TreeViewDragDropService();
            var zoneHeight = DropZoneHeightService.ResolveZoneHeight(200, 24);
            var pos = service.CalculateDropPosition(new Point(10, 12), new Rect(0, 0, 100, zoneHeight));
            Assert.Equal(DropPosition.Into, pos);
        }

        [Fact]
        public void HeaderRowMidpoint_WithBuggyFullHeight_WouldBeBefore()
        {
            // Documents the bug: using the full 200px height puts the header midpoint (12px)
            // in the top-20% Before sliver (< 40px), which is what rejected same-type drops.
            var service = new TreeViewDragDropService();
            var pos = service.CalculateDropPosition(new Point(10, 12), new Rect(0, 0, 100, 200));
            Assert.Equal(DropPosition.Before, pos);
        }
    }
}
