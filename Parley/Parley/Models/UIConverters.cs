using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

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
}