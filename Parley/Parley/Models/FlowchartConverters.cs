using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace DialogEditor.Models
{
    /// <summary>
    /// Converts FlowchartNode properties to background brush color.
    /// Entry nodes = orange/warm, Reply nodes = blue, Link nodes = gray
    /// </summary>
    public class FlowchartNodeBackgroundConverter : IMultiValueConverter
    {
        public static readonly FlowchartNodeBackgroundConverter Instance = new();

        // Colors matching the original plugin flowchart
        private static readonly IBrush EntryBrush = new SolidColorBrush(Color.Parse("#FFF3E0")); // Light orange
        private static readonly IBrush ReplyBrush = new SolidColorBrush(Color.Parse("#E3F2FD")); // Light blue
        private static readonly IBrush LinkBrush = new SolidColorBrush(Color.Parse("#F5F5F5")); // Light gray

        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values.Count < 2)
                return EntryBrush;

            var nodeType = values[0] as FlowchartNodeType? ?? FlowchartNodeType.Entry;
            var isLink = values[1] as bool? ?? false;

            if (isLink)
                return LinkBrush;

            return nodeType switch
            {
                FlowchartNodeType.Entry => EntryBrush,
                FlowchartNodeType.Reply => ReplyBrush,
                FlowchartNodeType.Link => LinkBrush,
                _ => EntryBrush
            };
        }
    }

    /// <summary>
    /// Converts FlowchartNode properties to border brush color.
    /// </summary>
    public class FlowchartNodeBorderConverter : IMultiValueConverter
    {
        public static readonly FlowchartNodeBorderConverter Instance = new();

        private static readonly IBrush EntryBorder = new SolidColorBrush(Color.Parse("#FF9800")); // Orange
        private static readonly IBrush ReplyBorder = new SolidColorBrush(Color.Parse("#2196F3")); // Blue
        private static readonly IBrush LinkBorder = new SolidColorBrush(Color.Parse("#9E9E9E")); // Gray

        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values.Count < 2)
                return EntryBorder;

            var nodeType = values[0] as FlowchartNodeType? ?? FlowchartNodeType.Entry;
            var isLink = values[1] as bool? ?? false;

            if (isLink)
                return LinkBorder;

            return nodeType switch
            {
                FlowchartNodeType.Entry => EntryBorder,
                FlowchartNodeType.Reply => ReplyBorder,
                FlowchartNodeType.Link => LinkBorder,
                _ => EntryBorder
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
            // Links get thinner dashed border, regular nodes get solid border
            return isLink ? new Thickness(1) : new Thickness(2);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
