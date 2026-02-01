using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using DialogEditor.Services;

namespace DialogEditor.Models
{
    /// <summary>
    /// Converts boolean to TextWrapping value
    /// true = Wrap, false = NoWrap
    /// </summary>
    public class BoolToTextWrappingConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? TextWrapping.Wrap : TextWrapping.NoWrap;
            }
            return TextWrapping.NoWrap;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is TextWrapping wrapping)
            {
                return wrapping == TextWrapping.Wrap;
            }
            return false;
        }
    }

    /// <summary>
    /// Converts null check to boolean
    /// null = false, not null = true
    /// </summary>
    public class NotNullConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value != null;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts boolean to MaxWidth value for word wrap support (#903, #1158).
    /// true = dynamic width from UISettingsService.TreeViewTextMaxWidth
    /// false = double.PositiveInfinity (no limit)
    /// Issue #1158: Now uses dynamic width that updates when the TreeView panel resizes.
    /// </summary>
    public class BoolToMaxWidthConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool boolValue && boolValue)
            {
                // #1158: Use dynamic width from UISettingsService instead of fixed value
                return UISettingsService.Instance.TreeViewTextMaxWidth;
            }
            return double.PositiveInfinity;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}