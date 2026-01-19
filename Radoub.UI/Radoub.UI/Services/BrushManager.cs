using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Radoub.UI.Services;

/// <summary>
/// Centralized brush manager for theme-aware semantic colors.
/// Provides consistent Success, Warning, Error, and Info brushes across all Radoub tools.
/// </summary>
public static class BrushManager
{
    /// <summary>
    /// Gets the theme-aware success brush (green).
    /// Uses ThemeSuccess resource if available.
    /// </summary>
    public static IBrush GetSuccessBrush(IResourceHost? host = null)
        => GetBrush(host, "ThemeSuccess", Brushes.Green);

    /// <summary>
    /// Gets the theme-aware warning brush (orange).
    /// Uses ThemeWarning resource if available.
    /// </summary>
    public static IBrush GetWarningBrush(IResourceHost? host = null)
        => GetBrush(host, "ThemeWarning", Brushes.Orange);

    /// <summary>
    /// Gets the theme-aware error brush (red).
    /// Uses ThemeError resource if available.
    /// </summary>
    public static IBrush GetErrorBrush(IResourceHost? host = null)
        => GetBrush(host, "ThemeError", Brushes.Red);

    /// <summary>
    /// Gets the theme-aware info brush (blue).
    /// Uses ThemeInfo resource if available.
    /// </summary>
    public static IBrush GetInfoBrush(IResourceHost? host = null)
        => GetBrush(host, "ThemeInfo", Brushes.DodgerBlue);

    /// <summary>
    /// Gets a brush from theme resources with fallback.
    /// First checks the host, then Application.Current.
    /// </summary>
    private static IBrush GetBrush(IResourceHost? host, string key, IBrush fallback)
    {
        // Try host first (allows control-specific theming)
        if (host?.TryFindResource(key, out var resource) == true && resource is IBrush brush)
            return brush;

        // Fall back to application resources
        if (Application.Current?.TryFindResource(key, out resource) == true && resource is IBrush appBrush)
            return appBrush;

        return fallback;
    }
}
