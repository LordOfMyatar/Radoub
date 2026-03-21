using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Radoub.UI.Services;

namespace Radoub.UI.Converters;

/// <summary>
/// Converts HasErrors/HasError bool to border brush for validation feedback.
/// True (has errors) = Error brush, False = Transparent (default border).
/// Uses theme resources via BrushManager for colorblind accessibility.
/// </summary>
public class BoolToErrorBrushConverter : IValueConverter
{
    public static readonly BoolToErrorBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool hasErrors)
        {
            return hasErrors ? BrushManager.GetErrorBrush() : Brushes.Transparent;
        }
        return Brushes.Transparent;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
