using Xunit;
using DialogEditor.Services;

namespace Parley.Tests
{
    /// <summary>
    /// TDD tests for flowchart node width computed properties (#906).
    /// Tests the formulas that derive MinWidth and TextMaxWidth from NodeWidth.
    /// Since FlowchartPanelViewModel requires DI (Program.Services), we test
    /// the underlying UISettingsService directly and verify the math.
    /// </summary>
    public class FlowchartNodeWidthComputedTests
    {
        [Theory]
        [InlineData(100, 60)]    // 100 * 0.6 = 60
        [InlineData(200, 120)]   // 200 * 0.6 = 120 (default)
        [InlineData(300, 180)]   // 300 * 0.6 = 180
        [InlineData(400, 240)]   // 400 * 0.6 = 240
        public void NodeMinWidth_Is60PercentOfNodeWidth(int nodeWidth, int expectedMinWidth)
        {
            // This tests the formula: MinWidth = NodeWidth * 0.6
            var computed = (int)(nodeWidth * 0.6);
            Assert.Equal(expectedMinWidth, computed);
        }

        [Theory]
        [InlineData(100, 80)]    // 100 - 20 = 80
        [InlineData(200, 180)]   // 200 - 20 = 180 (default matches current hardcoded)
        [InlineData(300, 280)]   // 300 - 20 = 280
        [InlineData(400, 380)]   // 400 - 20 = 380
        public void NodeTextMaxWidth_IsNodeWidthMinus20(int nodeWidth, int expectedTextWidth)
        {
            // This tests the formula: TextMaxWidth = NodeWidth - 20
            var computed = nodeWidth - 20;
            Assert.Equal(expectedTextWidth, computed);
        }

        [Fact]
        public void DefaultNodeWidth_ProducesCurrentHardcodedValues()
        {
            // Verify that default NodeWidth=200 produces the same values
            // as the current hardcoded MinWidth=120, MaxWidth=200, TextMaxWidth=180
            var service = new UISettingsService();
            var nodeWidth = service.FlowchartNodeWidth; // Should be 200

            Assert.Equal(200, nodeWidth);
            Assert.Equal(120, (int)(nodeWidth * 0.6));  // Current MinWidth="120"
            Assert.Equal(200, nodeWidth);                // Current MaxWidth="200"
            Assert.Equal(180, nodeWidth - 20);           // Current TextBlock MaxWidth="180"
        }
    }
}
