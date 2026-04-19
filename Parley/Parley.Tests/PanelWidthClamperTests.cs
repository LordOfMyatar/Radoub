using DialogEditor.Services;
using Xunit;

namespace Parley.Tests
{
    /// <summary>
    /// Tests for PanelWidthClamper — ensures the side-by-side flowchart panel
    /// never restores to an off-screen or unusable width (#2049).
    /// </summary>
    public class PanelWidthClamperTests
    {
        // Contract (from sprint plan #2063 / #2049):
        //   Reset-if-invalid: storedWidth < 100 OR storedWidth > 0.95 * windowWidth -> 0.50 * windowWidth
        //   Belt-and-suspenders clamp: min(max(width, 200), windowWidth - 400)
        //
        //   Net effect per (storedWidth, windowWidth):
        //     valid range  = [100, 0.95 * windowWidth] and [200, windowWidth - 400] after clamp
        //     reset path   = chooses 0.50 * windowWidth, then clamps to same hard bounds

        [Fact]
        public void ReturnsStoredWidth_WhenInValidRange()
        {
            // Arrange: stored width sits squarely inside both reset-valid and hard-clamp ranges
            double storedWidth = 500;
            double windowWidth = 1600;

            // Act
            var result = PanelWidthClamper.Clamp(storedWidth, windowWidth);

            // Assert
            Assert.Equal(500, result);
        }

        [Fact]
        public void ResetsToHalfWidth_WhenStoredBelowMinimum()
        {
            // Arrange: stored width is corrupt-low (< 100)
            double storedWidth = 10;
            double windowWidth = 1600;

            // Act
            var result = PanelWidthClamper.Clamp(storedWidth, windowWidth);

            // Assert: resets to 50% of window (800), which is also inside hard-clamp bounds
            Assert.Equal(800, result);
        }

        [Fact]
        public void ResetsToHalfWidth_WhenStoredExceeds95Percent()
        {
            // Arrange: stored width is corrupt-high (> 95% of window)
            double storedWidth = 1580; // 98.75% of 1600
            double windowWidth = 1600;

            // Act
            var result = PanelWidthClamper.Clamp(storedWidth, windowWidth);

            // Assert: resets to 50% of window
            Assert.Equal(800, result);
        }

        [Fact]
        public void ClampsToMin200_WhenWindowTooSmall()
        {
            // Arrange: tiny window where 50% reset would still be below hard min 200
            // Window = 300, reset would be 150, hard min is 200
            double storedWidth = 50; // triggers reset path
            double windowWidth = 300;

            // Act
            var result = PanelWidthClamper.Clamp(storedWidth, windowWidth);

            // Assert: clamped up to hard min 200
            Assert.Equal(200, result);
        }

        [Fact]
        public void ClampsToMaxLeavingLeftPanel_WhenWindowShrinks()
        {
            // Arrange: valid stored width, but window shrinks so stored > windowWidth - 400
            // storedWidth 900, windowWidth 1000 -> hard max is 1000 - 400 = 600
            // 900 is inside reset-valid range (< 95% of 1000 = 950), so we reach the clamp
            double storedWidth = 900;
            double windowWidth = 1000;

            // Act
            var result = PanelWidthClamper.Clamp(storedWidth, windowWidth);

            // Assert: clamped down to windowWidth - 400
            Assert.Equal(600, result);
        }
    }
}
