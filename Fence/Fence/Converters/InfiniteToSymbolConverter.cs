using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace MerchantEditor.Converters;

/// <summary>
/// Converts boolean Infinite flag to display symbol.
/// Shows "∞" when true, empty string when false.
/// </summary>
public class InfiniteToSymbolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isInfinite && isInfinite)
            return "∞";
        return string.Empty;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // Not used for one-way binding
        throw new NotImplementedException();
    }
}
