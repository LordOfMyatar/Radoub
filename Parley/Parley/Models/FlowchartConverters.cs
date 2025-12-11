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
    /// Uses SpeakerVisualHelper for dynamic speaker-based colors.
    /// Creates lightened/darkened versions of speaker colors based on theme.
    /// </summary>
    public class FlowchartNodeBackgroundConverter : IMultiValueConverter
    {
        public static readonly FlowchartNodeBackgroundConverter Instance = new();

        private static readonly IBrush LinkBrushLight = new SolidColorBrush(Color.Parse("#F5F5F5")); // Light gray
        private static readonly IBrush LinkBrushDark = new SolidColorBrush(Color.Parse("#424242")); // Dark gray

        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values.Count < 3)
                return CreateDefaultBrush(FlowchartNodeType.Entry, IsDarkTheme());

            var nodeType = values[0] as FlowchartNodeType? ?? FlowchartNodeType.Entry;
            var isLink = values[1] as bool? ?? false;
            var speaker = values[2] as string ?? string.Empty;

            bool isDark = IsDarkTheme();

            if (isLink)
                return isDark ? LinkBrushDark : LinkBrushLight;

            // Use SpeakerVisualHelper for consistent coloring with TreeView
            bool isPC = nodeType == FlowchartNodeType.Reply;
            string hexColor = SpeakerVisualHelper.GetSpeakerColor(speaker, isPC);

            // Create lightened/darkened background from speaker color based on theme
            return isDark ? CreateDarkenedBrush(hexColor) : CreateLightenedBrush(hexColor);
        }

        /// <summary>
        /// Checks if the current theme is dark
        /// </summary>
        private static bool IsDarkTheme()
        {
            var app = Application.Current;
            if (app == null) return false;
            return app.ActualThemeVariant == ThemeVariant.Dark;
        }

        /// <summary>
        /// Creates a lightened (pastel) version of a color for light theme backgrounds
        /// </summary>
        private static IBrush CreateLightenedBrush(string hexColor)
        {
            try
            {
                var color = Color.Parse(hexColor);
                // Mix with white to create pastel/lightened version (85% white, 15% original)
                var lightened = Color.FromRgb(
                    (byte)(color.R + (255 - color.R) * 0.85),
                    (byte)(color.G + (255 - color.G) * 0.85),
                    (byte)(color.B + (255 - color.B) * 0.85)
                );
                return new SolidColorBrush(lightened);
            }
            catch
            {
                return CreateDefaultBrush(FlowchartNodeType.Entry, false);
            }
        }

        /// <summary>
        /// Creates a darkened version of a color for dark theme backgrounds
        /// </summary>
        private static IBrush CreateDarkenedBrush(string hexColor)
        {
            try
            {
                var color = Color.Parse(hexColor);
                // Mix with dark gray to create muted dark version (70% dark, 30% original)
                var darkened = Color.FromRgb(
                    (byte)(color.R * 0.3 + 40),
                    (byte)(color.G * 0.3 + 40),
                    (byte)(color.B * 0.3 + 40)
                );
                return new SolidColorBrush(darkened);
            }
            catch
            {
                return CreateDefaultBrush(FlowchartNodeType.Entry, true);
            }
        }

        private static IBrush CreateDefaultBrush(FlowchartNodeType nodeType, bool isDark)
        {
            if (isDark)
            {
                return nodeType switch
                {
                    FlowchartNodeType.Reply => new SolidColorBrush(Color.Parse("#1A3A5C")), // Dark blue
                    _ => new SolidColorBrush(Color.Parse("#5C3A1A")) // Dark orange
                };
            }
            return nodeType switch
            {
                FlowchartNodeType.Reply => new SolidColorBrush(Color.Parse("#E3F2FD")), // Light blue
                _ => new SolidColorBrush(Color.Parse("#FFF3E0")) // Light orange
            };
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

        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values.Count < 3)
                return CreateDefaultBrush(FlowchartNodeType.Entry);

            var nodeType = values[0] as FlowchartNodeType? ?? FlowchartNodeType.Entry;
            var isLink = values[1] as bool? ?? false;
            var speaker = values[2] as string ?? string.Empty;

            if (isLink)
                return LinkBorder;

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
    /// Compares node ID with selected ID from ViewModel.
    /// </summary>
    public class FlowchartNodeSelectionBorderConverter : IMultiValueConverter
    {
        public static readonly FlowchartNodeSelectionBorderConverter Instance = new();

        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values.Count < 3)
                return new Thickness(2);

            var nodeId = values[0] as string;
            var isLink = values[1] as bool? ?? false;
            var selectedNodeId = values[2] as string;

            // Check if this node is selected
            bool isSelected = !string.IsNullOrEmpty(nodeId) &&
                              !string.IsNullOrEmpty(selectedNodeId) &&
                              nodeId == selectedNodeId;

            // Selected nodes get thicker border
            if (isSelected)
                return new Thickness(4);

            // Links get thinner border, regular nodes get standard
            return isLink ? new Thickness(1) : new Thickness(2);
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
