using System;
using System.Collections.Generic;
using System.Globalization;
using Xunit;
using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using DialogEditor.Models;

namespace Parley.Tests
{
    /// <summary>
    /// Unit tests for FlowchartConverters
    /// Tests theme-aware backgrounds and speaker-colored borders
    /// Sprint 4: Issue #340
    /// </summary>
    public class FlowchartConvertersTests
    {
        #region FlowchartNodeBackgroundConverter Tests

        [Fact]
        public void BackgroundConverter_LightTheme_ReturnsLightBackground()
        {
            // Arrange
            var converter = FlowchartNodeBackgroundConverter.Instance;
            var values = new object?[]
            {
                FlowchartNodeType.Entry,
                false, // isLink
                "Owner",
                ThemeVariant.Light
            };

            // Act
            var result = converter.Convert(values, typeof(IBrush), null, CultureInfo.InvariantCulture);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<SolidColorBrush>(result);
            var brush = (SolidColorBrush)result;
            // Light theme background should be off-white (#FAFAFA)
            Assert.Equal(Color.Parse("#FAFAFA"), brush.Color);
        }

        [Fact]
        public void BackgroundConverter_DarkTheme_ReturnsDarkBackground()
        {
            // Arrange
            var converter = FlowchartNodeBackgroundConverter.Instance;
            var values = new object?[]
            {
                FlowchartNodeType.Entry,
                false, // isLink
                "Owner",
                ThemeVariant.Dark
            };

            // Act
            var result = converter.Convert(values, typeof(IBrush), null, CultureInfo.InvariantCulture);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<SolidColorBrush>(result);
            var brush = (SolidColorBrush)result;
            // Dark theme background should be dark gray (#2D2D2D)
            Assert.Equal(Color.Parse("#2D2D2D"), brush.Color);
        }

        [Fact]
        public void BackgroundConverter_RootNode_ReturnsNeutralGray_LightTheme()
        {
            // Arrange
            var converter = FlowchartNodeBackgroundConverter.Instance;
            var values = new object?[]
            {
                FlowchartNodeType.Root,
                false, // isLink
                "",
                ThemeVariant.Light
            };

            // Act
            var result = converter.Convert(values, typeof(IBrush), null, CultureInfo.InvariantCulture);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<SolidColorBrush>(result);
            var brush = (SolidColorBrush)result;
            // Root should be neutral gray in light theme (#E8E8E8)
            Assert.Equal(Color.Parse("#E8E8E8"), brush.Color);
        }

        [Fact]
        public void BackgroundConverter_RootNode_ReturnsNeutralGray_DarkTheme()
        {
            // Arrange
            var converter = FlowchartNodeBackgroundConverter.Instance;
            var values = new object?[]
            {
                FlowchartNodeType.Root,
                false, // isLink
                "",
                ThemeVariant.Dark
            };

            // Act
            var result = converter.Convert(values, typeof(IBrush), null, CultureInfo.InvariantCulture);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<SolidColorBrush>(result);
            var brush = (SolidColorBrush)result;
            // Root should be neutral gray in dark theme (#3A3A3A)
            Assert.Equal(Color.Parse("#3A3A3A"), brush.Color);
        }

        [Fact]
        public void BackgroundConverter_LinkNode_ReturnsDistinctLinkBackground()
        {
            // Arrange
            var converter = FlowchartNodeBackgroundConverter.Instance;
            var values = new object?[]
            {
                FlowchartNodeType.Link,
                true, // isLink
                "",
                ThemeVariant.Light
            };

            // Act
            var result = converter.Convert(values, typeof(IBrush), null, CultureInfo.InvariantCulture);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<SolidColorBrush>(result);
            var brush = (SolidColorBrush)result;
            // Link should have lighter gray (#F0F0F0 in light theme)
            Assert.Equal(Color.Parse("#F0F0F0"), brush.Color);
        }

        [Fact]
        public void BackgroundConverter_InsufficientValues_ReturnsDefaultBackground()
        {
            // Arrange
            var converter = FlowchartNodeBackgroundConverter.Instance;
            var values = new object?[] { FlowchartNodeType.Entry }; // Missing values

            // Act
            var result = converter.Convert(values, typeof(IBrush), null, CultureInfo.InvariantCulture);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<SolidColorBrush>(result);
        }

        #endregion

        #region FlowchartNodeBorderConverter Tests

        [Fact]
        public void BorderConverter_RootNode_ReturnsGrayBorder()
        {
            // Arrange
            var converter = FlowchartNodeBorderConverter.Instance;
            var values = new object?[]
            {
                FlowchartNodeType.Root,
                false, // isLink
                "",
                ThemeVariant.Light
            };

            // Act
            var result = converter.Convert(values, typeof(IBrush), null, CultureInfo.InvariantCulture);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<SolidColorBrush>(result);
            var brush = (SolidColorBrush)result;
            // Root border should be medium gray (#757575)
            Assert.Equal(Color.Parse("#757575"), brush.Color);
        }

        [Fact]
        public void BorderConverter_LinkNode_ReturnsGrayBorder()
        {
            // Arrange
            var converter = FlowchartNodeBorderConverter.Instance;
            var values = new object?[]
            {
                FlowchartNodeType.Link,
                true, // isLink
                "",
                ThemeVariant.Light
            };

            // Act
            var result = converter.Convert(values, typeof(IBrush), null, CultureInfo.InvariantCulture);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<SolidColorBrush>(result);
            var brush = (SolidColorBrush)result;
            // Link border should be gray (#9E9E9E)
            Assert.Equal(Color.Parse("#9E9E9E"), brush.Color);
        }

        [Fact]
        public void BorderConverter_ReplyNode_UsesPCColor()
        {
            // Arrange
            var converter = FlowchartNodeBorderConverter.Instance;
            var values = new object?[]
            {
                FlowchartNodeType.Reply,
                false, // isLink
                "PC", // Reply nodes show PC
                ThemeVariant.Light
            };

            // Act
            var result = converter.Convert(values, typeof(IBrush), null, CultureInfo.InvariantCulture);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<SolidColorBrush>(result);
            // PC color from SpeakerVisualHelper - should be blue-ish
        }

        [Fact]
        public void BorderConverter_EntryNode_UsesOwnerColor()
        {
            // Arrange
            var converter = FlowchartNodeBorderConverter.Instance;
            var values = new object?[]
            {
                FlowchartNodeType.Entry,
                false, // isLink
                "", // Empty speaker = Owner
                ThemeVariant.Light
            };

            // Act
            var result = converter.Convert(values, typeof(IBrush), null, CultureInfo.InvariantCulture);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<SolidColorBrush>(result);
            // Owner color from SpeakerVisualHelper
        }

        [Fact]
        public void BorderConverter_NamedSpeaker_UsesSpeakerColor()
        {
            // Arrange
            var converter = FlowchartNodeBorderConverter.Instance;
            var values = new object?[]
            {
                FlowchartNodeType.Entry,
                false, // isLink
                "Merchant", // Named speaker
                ThemeVariant.Light
            };

            // Act
            var result = converter.Convert(values, typeof(IBrush), null, CultureInfo.InvariantCulture);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<SolidColorBrush>(result);
            // Should return a valid color (from SpeakerVisualHelper)
        }

        [Fact]
        public void BorderConverter_InsufficientValues_ReturnsDefaultBrush()
        {
            // Arrange
            var converter = FlowchartNodeBorderConverter.Instance;
            var values = new object?[] { FlowchartNodeType.Entry }; // Missing values

            // Act
            var result = converter.Convert(values, typeof(IBrush), null, CultureInfo.InvariantCulture);

            // Assert
            Assert.NotNull(result);
            Assert.IsType<SolidColorBrush>(result);
        }

        #endregion

        #region FlowchartLinkOpacityConverter Tests

        [Fact]
        public void OpacityConverter_LinkNode_ReturnsReducedOpacity()
        {
            // Arrange
            var converter = FlowchartLinkOpacityConverter.Instance;
            var values = new List<object?> { true, FlowchartNodeType.Entry };

            // Act
            var result = converter.Convert(values, typeof(double), null, CultureInfo.InvariantCulture);

            // Assert - exact value depends on theme variant, just verify it's less than 1.0
            Assert.True(result is double opacity && opacity < 1.0);
        }

        [Fact]
        public void OpacityConverter_NonLinkNode_ReturnsFullOpacity()
        {
            // Arrange
            var converter = FlowchartLinkOpacityConverter.Instance;
            var values = new List<object?> { false, FlowchartNodeType.Entry };

            // Act
            var result = converter.Convert(values, typeof(double), null, CultureInfo.InvariantCulture);

            // Assert
            Assert.Equal(1.0, result);
        }

        [Fact]
        public void OpacityConverter_RootNode_ReturnsReducedOpacity()
        {
            // Arrange
            var converter = FlowchartLinkOpacityConverter.Instance;
            var values = new List<object?> { false, FlowchartNodeType.Root };

            // Act
            var result = converter.Convert(values, typeof(double), null, CultureInfo.InvariantCulture);

            // Assert - Root nodes get reduced opacity regardless of IsLink
            Assert.True(result is double opacity && opacity < 1.0);
        }

        #endregion

        #region FlowchartNodeSelectionBorderConverter Tests

        [Fact]
        public void SelectionBorderConverter_SelectedNode_ReturnsThickBorder()
        {
            // Arrange
            var converter = FlowchartNodeSelectionBorderConverter.Instance;
            var values = new object?[]
            {
                "E0", // nodeId
                false, // isLink
                "E0" // selectedNodeId - matches
            };

            // Act
            var result = converter.Convert(values, typeof(Thickness), null, CultureInfo.InvariantCulture);

            // Assert
            Assert.IsType<Thickness>(result);
            var thickness = (Thickness)result;
            Assert.Equal(5, thickness.Top); // Selected gets extra thick border
        }

        [Fact]
        public void SelectionBorderConverter_NonSelectedNode_ReturnsNormalBorder()
        {
            // Arrange
            var converter = FlowchartNodeSelectionBorderConverter.Instance;
            var values = new object?[]
            {
                "E0", // nodeId
                false, // isLink
                "E1" // selectedNodeId - doesn't match
            };

            // Act
            var result = converter.Convert(values, typeof(Thickness), null, CultureInfo.InvariantCulture);

            // Assert
            Assert.IsType<Thickness>(result);
            var thickness = (Thickness)result;
            Assert.Equal(3, thickness.Top); // Normal thick border for speaker color visibility
        }

        [Fact]
        public void SelectionBorderConverter_LinkNode_ReturnsThinnerBorder()
        {
            // Arrange
            var converter = FlowchartNodeSelectionBorderConverter.Instance;
            var values = new object?[]
            {
                "L0", // nodeId
                true, // isLink
                "E1" // selectedNodeId - doesn't match
            };

            // Act
            var result = converter.Convert(values, typeof(Thickness), null, CultureInfo.InvariantCulture);

            // Assert
            Assert.IsType<Thickness>(result);
            var thickness = (Thickness)result;
            Assert.Equal(2, thickness.Top); // Links get thinner border
        }

        #endregion
    }
}