using System;
using DialogEditor.Services;
using Xunit;

namespace DialogEditor.Tests
{
    /// <summary>
    /// TDD tests for configurable flowchart node width (#906).
    /// Tests UISettingsService.FlowchartNodeWidth property behavior.
    /// </summary>
    public class UISettingsServiceNodeWidthTests
    {
        [Fact]
        public void FlowchartNodeWidth_DefaultValue_Is200()
        {
            // Arrange
            var service = new UISettingsService();

            // Assert
            Assert.Equal(200, service.FlowchartNodeWidth);
        }

        [Fact]
        public void FlowchartNodeWidth_SetValidValue_Updates()
        {
            // Arrange
            var service = new UISettingsService();

            // Act
            service.FlowchartNodeWidth = 300;

            // Assert
            Assert.Equal(300, service.FlowchartNodeWidth);
        }

        [Fact]
        public void FlowchartNodeWidth_BelowMinimum_ClampsTo100()
        {
            // Arrange
            var service = new UISettingsService();

            // Act
            service.FlowchartNodeWidth = 50;

            // Assert
            Assert.Equal(100, service.FlowchartNodeWidth);
        }

        [Fact]
        public void FlowchartNodeWidth_AboveMaximum_ClampsTo400()
        {
            // Arrange
            var service = new UISettingsService();

            // Act
            service.FlowchartNodeWidth = 500;

            // Assert
            Assert.Equal(400, service.FlowchartNodeWidth);
        }

        [Theory]
        [InlineData(100)]
        [InlineData(200)]
        [InlineData(400)]
        public void FlowchartNodeWidth_BoundaryValues_Accepted(int width)
        {
            // Arrange
            var service = new UISettingsService();

            // Act
            service.FlowchartNodeWidth = width;

            // Assert
            Assert.Equal(width, service.FlowchartNodeWidth);
        }

        [Fact]
        public void FlowchartNodeWidth_SetValue_FiresSettingsChanged()
        {
            // Arrange
            var service = new UISettingsService();
            bool eventFired = false;
            service.SettingsChanged += () => eventFired = true;

            // Act
            service.FlowchartNodeWidth = 300;

            // Assert
            Assert.True(eventFired);
        }

        [Fact]
        public void FlowchartNodeWidth_SetSameValue_DoesNotFireSettingsChanged()
        {
            // Arrange
            var service = new UISettingsService();
            service.FlowchartNodeWidth = 200; // Set to default first (triggers event)
            bool eventFired = false;
            service.SettingsChanged += () => eventFired = true;

            // Act - set to same value
            service.FlowchartNodeWidth = 200;

            // Assert
            Assert.False(eventFired);
        }

        [Fact]
        public void FlowchartNodeWidth_FiresPropertyChanged()
        {
            // Arrange
            var service = new UISettingsService();
            string? changedProperty = null;
            service.PropertyChanged += (s, e) => changedProperty = e.PropertyName;

            // Act
            service.FlowchartNodeWidth = 300;

            // Assert
            Assert.Equal(nameof(UISettingsService.FlowchartNodeWidth), changedProperty);
        }

        [Fact]
        public void Initialize_SetsFlowchartNodeWidth()
        {
            // Arrange
            var service = new UISettingsService();

            // Act
            service.Initialize("Floating", false,
                flowchartNodeMaxLines: 3, treeViewWordWrap: false,
                flowchartNodeWidth: 250);

            // Assert
            Assert.Equal(250, service.FlowchartNodeWidth);
        }

        [Fact]
        public void Initialize_ClampsFlowchartNodeWidth()
        {
            // Arrange
            var service = new UISettingsService();

            // Act
            service.Initialize("Floating", false,
                flowchartNodeMaxLines: 3, treeViewWordWrap: false,
                flowchartNodeWidth: 999);

            // Assert
            Assert.Equal(400, service.FlowchartNodeWidth);
        }

        [Fact]
        public void Initialize_DefaultFlowchartNodeWidth_Is200()
        {
            // Arrange
            var service = new UISettingsService();

            // Act - call without flowchartNodeWidth parameter (uses default)
            service.Initialize("Floating", false);

            // Assert
            Assert.Equal(200, service.FlowchartNodeWidth);
        }
    }
}
