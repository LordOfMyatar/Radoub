using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Radoub.UI.Services;

namespace ItemEditor.ViewModels;

/// <summary>
/// Converts HasError bool to border brush for validation feedback.
/// True = Error brush, False = Transparent.
/// </summary>
public class BoolToErrorBrushConverter : IValueConverter
{
    public static readonly BoolToErrorBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool hasError)
        {
            return hasError ? BrushManager.GetErrorBrush() : Brushes.Transparent;
        }
        return Brushes.Transparent;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
