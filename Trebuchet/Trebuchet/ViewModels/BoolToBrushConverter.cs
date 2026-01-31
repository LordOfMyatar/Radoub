using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Radoub.UI.Services;

namespace RadoubLauncher.ViewModels;

/// <summary>
/// Converts a boolean to a brush color.
/// True = Theme success (green), False = Theme disabled (gray)
/// Uses theme resources for colorblind accessibility.
/// </summary>
public class BoolToBrushConverter : IValueConverter
{
    public static readonly BoolToBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isAvailable)
        {
            return isAvailable
                ? BrushManager.GetSuccessBrush()
                : BrushManager.GetDisabledBrush();
        }
        return BrushManager.GetDisabledBrush();
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts module validity to foreground color.
/// True = White (valid), False = Warning (invalid)
/// Uses theme resources for colorblind accessibility.
/// </summary>
public class ModuleValidityToForegroundConverter : IValueConverter
{
    public static readonly ModuleValidityToForegroundConverter Instance = new();

    private static readonly IBrush ValidBrush = new SolidColorBrush(Colors.White);

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isValid)
        {
            return isValid ? ValidBrush : BrushManager.GetWarningBrush();
        }
        return ValidBrush;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts module validity to background color for the module name badge.
/// True = Semi-transparent dark (valid), False = Theme warning (invalid)
/// Uses theme resources for colorblind accessibility.
/// </summary>
public class ModuleValidityToBgConverter : IValueConverter
{
    public static readonly ModuleValidityToBgConverter Instance = new();

    // Semi-transparent black (50% opacity) for valid state badge background
    // Provides contrast against accent header color without being too dark
    private static readonly IBrush ValidBrush = new SolidColorBrush(Color.Parse("#80000000"));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isValid)
        {
            return isValid ? ValidBrush : BrushManager.GetWarningBrush();
        }
        return ValidBrush;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts HasErrors bool to border brush for validation feedback.
/// True (has errors) = Error brush, False = Transparent (default border)
/// Uses theme resources for colorblind accessibility.
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
