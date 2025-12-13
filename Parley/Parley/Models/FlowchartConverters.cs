using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Styling;
using DialogEditor.Utils;

namespace DialogEditor.Models
{
    /// <summary>
    /// Converts FlowchartNode properties to background brush color.
    /// Uses theme-aware backgrounds with speaker colors shown via thick borders.
    /// This preserves text readability while showing speaker identity.
    /// </summary>
    public class FlowchartNodeBackgroundConverter : IMultiValueConverter
    {
        public static readonly FlowchartNodeBackgroundConverter Instance = new();

        // Theme-aware backgrounds that ensure text readability
        private static readonly IBrush LightThemeBg = new SolidColorBrush(Color.Parse("#FAFAFA")); // Off-white
        private static readonly IBrush DarkThemeBg = new SolidColorBrush(Color.Parse("#2D2D2D")); // Dark gray
        private static readonly IBrush LinkBrushLight = new SolidColorBrush(Color.Parse("#F0F0F0")); // Lighter gray for links
        private static readonly IBrush LinkBrushDark = new SolidColorBrush(Color.Parse("#383838")); // Slightly different for links
        private static readonly IBrush RootBrushLight = new SolidColorBrush(Color.Parse("#F1F8E9")); // Very light green tint
        private static readonly IBrush RootBrushDark = new SolidColorBrush(Color.Parse("#263238")); // Dark with green tint

        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            // Values: NodeType, IsLink, Speaker, ActualThemeVariant
            bool isDark = values.Count >= 4 && values[3] is ThemeVariant tv && tv == ThemeVariant.Dark;

            if (values.Count < 3)
                return isDark ? DarkThemeBg : LightThemeBg;

            var nodeType = values[0] as FlowchartNodeType? ?? FlowchartNodeType.Entry;
            var isLink = values[1] as bool? ?? false;

            // Link nodes get distinct muted background
            if (isLink)
                return isDark ? LinkBrushDark : LinkBrushLight;

            // Root node gets subtle green tint
            if (nodeType == FlowchartNodeType.Root)
                return isDark ? RootBrushDark : RootBrushLight;

            // All other nodes use theme background
            // Speaker identity is shown via the thick border (see FlowchartNodeBorderConverter)
            return isDark ? DarkThemeBg : LightThemeBg;
        }
    }

    /// <summary>
    /// Converts FlowchartNode properties to border brush color.
    /// Uses SpeakerVisualHelper for dynamic speaker-based colors.
    /// </summary>
    public class FlowchartNodeBorderConverter : IMultiValueConverter
    {
        public static readonly FlowchartNodeBorderConverter Instance = new();

        private static readonly IBrush LinkBorder = new SolidColorBrush(Color.Parse("#9E9E9E")); // Gray
        private static readonly IBrush RootBorder = new SolidColorBrush(Color.Parse("#4CAF50")); // Green

        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            // Values: NodeType, IsLink, Speaker, ActualThemeVariant (4th param for theme reactivity, not used for border color)
            if (values.Count < 3)
                return CreateDefaultBrush(FlowchartNodeType.Entry);

            var nodeType = values[0] as FlowchartNodeType? ?? FlowchartNodeType.Entry;
            var isLink = values[1] as bool? ?? false;
            var speaker = values[2] as string ?? string.Empty;

            if (isLink)
                return LinkBorder;

            if (nodeType == FlowchartNodeType.Root)
                return RootBorder;

            // Use SpeakerVisualHelper for consistent coloring with TreeView
            bool isPC = nodeType == FlowchartNodeType.Reply;
            string hexColor = SpeakerVisualHelper.GetSpeakerColor(speaker, isPC);

            try
            {
                return new SolidColorBrush(Color.Parse(hexColor));
            }
            catch
            {
                return CreateDefaultBrush(nodeType);
            }
        }

        private static IBrush CreateDefaultBrush(FlowchartNodeType nodeType)
        {
            return nodeType switch
            {
                FlowchartNodeType.Reply => new SolidColorBrush(Color.Parse("#2196F3")), // Blue
                _ => new SolidColorBrush(Color.Parse("#FF9800")) // Orange
            };
        }
    }

    /// <summary>
    /// Converts IsLink boolean to border thickness (dashed appearance for links).
    /// </summary>
    public class FlowchartLinkBorderThicknessConverter : IValueConverter
    {
        public static readonly FlowchartLinkBorderThicknessConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var isLink = value as bool? ?? false;
            // Links get thinner border, regular nodes get thicker solid border
            return isLink ? new Thickness(1) : new Thickness(2);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts IsLink boolean to border dash array for dashed links.
    /// </summary>
    public class FlowchartLinkBorderDashConverter : IValueConverter
    {
        public static readonly FlowchartLinkBorderDashConverter Instance = new();

        // Dashed pattern: 4 units on, 2 units off
        private static readonly AvaloniaList<double> DashedPattern = new() { 4, 2 };
        private static readonly AvaloniaList<double> SolidPattern = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var isLink = value as bool? ?? false;
            return isLink ? DashedPattern : SolidPattern;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts IsLink boolean to node opacity for visual distinction.
    /// </summary>
    public class FlowchartLinkOpacityConverter : IValueConverter
    {
        public static readonly FlowchartLinkOpacityConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var isLink = value as bool? ?? false;
            // Links are slightly translucent to indicate they're references
            return isLink ? 0.7 : 1.0;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts node selection state to border thickness for highlighting.
    /// Uses thick borders (3px) to show speaker colors prominently.
    /// Compares node ID with selected ID from ViewModel.
    /// </summary>
    public class FlowchartNodeSelectionBorderConverter : IMultiValueConverter
    {
        public static readonly FlowchartNodeSelectionBorderConverter Instance = new();

        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values.Count < 3)
                return new Thickness(3);

            var nodeId = values[0] as string;
            var isLink = values[1] as bool? ?? false;
            var selectedNodeId = values[2] as string;

            // Check if this node is selected
            bool isSelected = !string.IsNullOrEmpty(nodeId) &&
                              !string.IsNullOrEmpty(selectedNodeId) &&
                              nodeId == selectedNodeId;

            // Selected nodes get extra thick border for emphasis
            if (isSelected)
                return new Thickness(5);

            // Links get thinner border, regular nodes get thick border for speaker color visibility
            return isLink ? new Thickness(2) : new Thickness(3);
        }
    }

    /// <summary>
    /// Converts node selection state to highlight brush.
    /// Selected nodes get a bright highlight color.
    /// </summary>
    public class FlowchartNodeSelectionBrushConverter : IMultiValueConverter
    {
        public static readonly FlowchartNodeSelectionBrushConverter Instance = new();

        private static readonly IBrush SelectionHighlight = new SolidColorBrush(Color.Parse("#FFC107")); // Amber highlight

        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values.Count < 4)
                return null;

            var nodeId = values[0] as string;
            var isLink = values[1] as bool? ?? false;
            var speaker = values[2] as string ?? string.Empty;
            var selectedNodeId = values[3] as string;

            // Check if this node is selected
            bool isSelected = !string.IsNullOrEmpty(nodeId) &&
                              !string.IsNullOrEmpty(selectedNodeId) &&
                              nodeId == selectedNodeId;

            // Selected nodes get amber highlight border
            if (isSelected)
                return SelectionHighlight;

            // Non-selected nodes use normal border coloring (handled by FlowchartNodeBorderConverter)
            return null;
        }
    }
}
