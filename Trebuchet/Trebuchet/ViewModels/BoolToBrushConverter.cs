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
/// True = Theme accent foreground (valid), False = Warning (invalid)
/// Uses theme resources for colorblind accessibility.
/// </summary>
public class ModuleValidityToForegroundConverter : IValueConverter
{
    public static readonly ModuleValidityToForegroundConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isValid)
        {
            // Valid: use info brush (readable on any theme background)
            // Invalid: use warning brush for visual alert
            return isValid ? BrushManager.GetInfoBrush() : BrushManager.GetWarningBrush();
        }
        return BrushManager.GetInfoBrush();
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

// ModuleValidityToBgConverter removed 2026-02-17 — unreferenced dead code (#1392)

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
