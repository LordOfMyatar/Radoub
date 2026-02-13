using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Radoub.UI.Services;

namespace RadoubLauncher.ViewModels;

/// <summary>
/// Converts a reputation value (0-100) to a theme-aware background brush.
/// 0 = Error brush (warm/hostile), 50 = neutral (transparent), 100 = Success brush (cool/friendly).
/// Interpolates between theme colors for intermediate values.
/// </summary>
public class ReputationColorConverter : IValueConverter
{
    public static readonly ReputationColorConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        uint rep = value switch
        {
            uint u => u,
            int i => (uint)Math.Max(0, i),
            _ => 50u
        };

        if (rep > 100) rep = 100;

        var errorBrush = BrushManager.GetErrorBrush() as SolidColorBrush;
        var successBrush = BrushManager.GetSuccessBrush() as SolidColorBrush;

        if (errorBrush == null || successBrush == null)
            return Brushes.Transparent;

        var hostileColor = errorBrush.Color;
        var friendlyColor = successBrush.Color;

        // 0 = full hostile color, 50 = neutral (faded), 100 = full friendly color
        // Use opacity to fade toward neutral at 50
        if (rep <= 50)
        {
            // 0-50: hostile color with decreasing opacity
            // At 0: full opacity (hostile). At 50: transparent (neutral)
            double t = rep / 50.0;
            byte opacity = (byte)(180 * (1.0 - t));
            return new SolidColorBrush(Color.FromArgb(opacity, hostileColor.R, hostileColor.G, hostileColor.B));
        }
        else
        {
            // 51-100: friendly color with increasing opacity
            // At 50: transparent (neutral). At 100: full opacity (friendly)
            double t = (rep - 50) / 50.0;
            byte opacity = (byte)(180 * t);
            return new SolidColorBrush(Color.FromArgb(opacity, friendlyColor.R, friendlyColor.G, friendlyColor.B));
        }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts a reputation value (0-100) to a contrasting foreground color.
/// Ensures text is readable on the reputation-colored background.
/// </summary>
public class ReputationForegroundConverter : IValueConverter
{
    public static readonly ReputationForegroundConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // Since we use semi-transparent backgrounds, the underlying theme text color
        // should work in most cases. Only override for very strong colors.
        uint rep = value switch
        {
            uint u => u,
            int i => (uint)Math.Max(0, i),
            _ => 50u
        };

        if (rep > 100) rep = 100;

        // At extremes (near 0 or 100), the background is stronger - use white for contrast
        if (rep <= 10 || rep >= 90)
            return new SolidColorBrush(Colors.White);

        // For mid-range values, let the theme text color work
        return null; // Falls through to default foreground
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
