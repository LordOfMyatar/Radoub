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

// BoolToErrorBrushConverter moved to Radoub.UI.Converters 2026-03-21 (#1780)
// ModuleValidityToBgConverter removed 2026-02-17 — unreferenced dead code (#1392)
