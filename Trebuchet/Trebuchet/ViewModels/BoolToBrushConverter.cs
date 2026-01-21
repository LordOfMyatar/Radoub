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

    // Fallback for disabled (not in BrushManager)
    private static readonly IBrush DisabledBrush = new SolidColorBrush(Color.Parse("#9E9E9E"));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isAvailable)
        {
            return isAvailable
                ? BrushManager.GetSuccessBrush()
                : DisabledBrush;
        }
        return DisabledBrush;
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

    // Semi-transparent dark for valid (visible contrast against accent header)
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
