using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace RadoubLauncher.ViewModels;

/// <summary>
/// Converts a boolean to a brush color.
/// True = Green (available), False = Gray (not found)
/// </summary>
public class BoolToBrushConverter : IValueConverter
{
    public static readonly BoolToBrushConverter Instance = new();

    private static readonly IBrush AvailableBrush = new SolidColorBrush(Color.Parse("#4CAF50"));
    private static readonly IBrush UnavailableBrush = new SolidColorBrush(Color.Parse("#9E9E9E"));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isAvailable)
        {
            return isAvailable ? AvailableBrush : UnavailableBrush;
        }
        return UnavailableBrush;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts module validity to foreground color.
/// True = White (valid), False = Warning yellow (invalid)
/// </summary>
public class ModuleValidityToForegroundConverter : IValueConverter
{
    public static readonly ModuleValidityToForegroundConverter Instance = new();

    private static readonly IBrush ValidBrush = new SolidColorBrush(Colors.White);
    private static readonly IBrush InvalidBrush = new SolidColorBrush(Color.Parse("#FFCC00"));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isValid)
        {
            return isValid ? ValidBrush : InvalidBrush;
        }
        return ValidBrush;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
