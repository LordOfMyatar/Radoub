using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace DialogEditor.Models
{
    /// <summary>
    /// Converts bool to opacity: true (quest end) = 0.5 (faded), false/null = 1.0 (normal)
    /// Handles null gracefully for nodes without quest data
    /// </summary>
    public class BoolToOpacityConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            // Null or false = normal opacity (1.0)
            if (value == null || value is not bool boolValue)
                return 1.0;

            // true (End = quest complete) = faded (0.5)
            // false (End = quest in progress) = normal (1.0)
            return boolValue ? 0.5 : 1.0;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException("BoolToOpacityConverter is one-way only");
        }
    }
}
