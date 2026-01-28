using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Radoub.Formats.Logging;

namespace Radoub.UI.Services;

/// <summary>
/// Centralized brush manager for theme-aware semantic colors.
/// Provides consistent Success, Warning, Error, Info, and Disabled brushes across all Radoub tools.
/// Theme colors are loaded from theme JSON files (success, warning, error, info, disabled fields).
/// If a theme doesn't define a color, hardcoded fallbacks are used and logged at DEBUG level.
/// </summary>
public static class BrushManager
{
    // Fallback colors when theme doesn't define them (Material Design palette)
    private static readonly IBrush FallbackSuccess = Brushes.Green;
    private static readonly IBrush FallbackWarning = Brushes.Orange;
    private static readonly IBrush FallbackError = Brushes.Red;
    private static readonly IBrush FallbackInfo = Brushes.DodgerBlue;
    private static readonly IBrush FallbackDisabled = new SolidColorBrush(Color.Parse("#9E9E9E")); // Gray 500

    /// <summary>
    /// Gets the theme-aware success brush (green).
    /// Uses ThemeSuccess resource if available.
    /// </summary>
    public static IBrush GetSuccessBrush(IResourceHost? host = null)
        => GetBrush(host, "ThemeSuccess", FallbackSuccess);

    /// <summary>
    /// Gets the theme-aware warning brush (orange).
    /// Uses ThemeWarning resource if available.
    /// </summary>
    public static IBrush GetWarningBrush(IResourceHost? host = null)
        => GetBrush(host, "ThemeWarning", FallbackWarning);

    /// <summary>
    /// Gets the theme-aware error brush (red).
    /// Uses ThemeError resource if available.
    /// </summary>
    public static IBrush GetErrorBrush(IResourceHost? host = null)
        => GetBrush(host, "ThemeError", FallbackError);

    /// <summary>
    /// Gets the theme-aware info brush (blue).
    /// Uses ThemeInfo resource if available.
    /// </summary>
    public static IBrush GetInfoBrush(IResourceHost? host = null)
        => GetBrush(host, "ThemeInfo", FallbackInfo);

    /// <summary>
    /// Gets the theme-aware disabled brush (gray).
    /// Uses ThemeDisabled resource if available.
    /// </summary>
    public static IBrush GetDisabledBrush(IResourceHost? host = null)
        => GetBrush(host, "ThemeDisabled", FallbackDisabled);

    /// <summary>
    /// Gets a brush from theme resources with fallback.
    /// First checks the host, then Application.Current.
    /// Logs at DEBUG level when fallback is used.
    /// </summary>
    private static IBrush GetBrush(IResourceHost? host, string key, IBrush fallback)
    {
        // Try host first (allows control-specific theming)
        if (host?.TryFindResource(key, out var resource) == true && resource is IBrush brush)
            return brush;

        // Fall back to application resources
        if (Application.Current?.TryFindResource(key, out resource) == true && resource is IBrush appBrush)
            return appBrush;

        // Log when using fallback (helps debug theme issues)
        UnifiedLogger.LogApplication(LogLevel.DEBUG, $"BrushManager: {key} not in theme, using fallback");
        return fallback;
    }
}
