using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Radoub.UI.Services;

namespace Radoub.UI.Converters;

/// <summary>
/// Converts a bool to a foreground brush for status text. True = theme warning
/// brush (so validator rejections stand out, #2182); False = the normal
/// medium-emphasis foreground. ConverterParameter optionally overrides the
/// "normal" theme resource key (default SystemControlForegroundBaseMediumBrush).
/// Uses BrushManager / theme resources so the color follows the active theme.
/// </summary>
public class BoolToWarningBrushConverter : IValueConverter
{
    public static readonly BoolToWarningBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isWarning && isWarning)
            return BrushManager.GetWarningBrush();

        var normalKey = parameter as string ?? "SystemControlForegroundBaseMediumBrush";
        if (Application.Current?.Resources.TryGetResource(normalKey, Application.Current.ActualThemeVariant, out var res) == true
            && res is IBrush brush)
            return brush;
        return AvaloniaProperty.UnsetValue;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
