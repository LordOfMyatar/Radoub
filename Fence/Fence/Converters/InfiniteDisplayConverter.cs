using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace MerchantEditor.Converters;

/// <summary>
/// Converts a boolean Infinite flag to a display string.
/// True → "Yes ∞", False → "No". Mirrors Fence's pre-extraction display
/// in <c>MainWindow.ItemDetails.cs</c>.
/// </summary>
public sealed class InfiniteDisplayConverter : IValueConverter
{
    public static readonly InfiniteDisplayConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b)
            return b ? "Yes ∞" : "No";
        return string.Empty;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
