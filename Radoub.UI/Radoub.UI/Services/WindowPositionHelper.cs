using Avalonia;
using Avalonia.Controls;

namespace Radoub.UI.Services;

/// <summary>
/// Helper for saving and restoring window position, size, and maximized state.
/// Eliminates duplicated window lifecycle code across tools.
///
/// Usage in MainWindow:
///   Constructor:  WindowPositionHelper.Restore(this, settings);
///   OnClosing:    WindowPositionHelper.Save(this, settings);
///
/// Panel sizes are tool-specific and should be handled separately.
/// </summary>
public static class WindowPositionHelper
{
    /// <summary>
    /// Restores window position, size, and maximized state from settings.
    /// Call from the MainWindow constructor.
    /// </summary>
    /// <param name="window">The window to restore</param>
    /// <param name="settings">Settings service with window properties</param>
    /// <param name="validateBounds">If true, validates the saved position is reasonable (default: false)</param>
    public static void Restore(Window window, IWindowSettings settings, bool validateBounds = false)
    {
        var left = settings.WindowLeft;
        var top = settings.WindowTop;
        var width = settings.WindowWidth;
        var height = settings.WindowHeight;

        if (validateBounds)
        {
            // Ensure window is at least partially visible
            if (left < 0 || top < 0 || width <= 100 || height <= 100)
                return; // Use defaults
        }

        window.Position = new PixelPoint((int)left, (int)top);
        window.Width = width;
        window.Height = height;

        if (settings.WindowMaximized)
        {
            window.WindowState = WindowState.Maximized;
        }
    }

    /// <summary>
    /// Saves window position, size, and maximized state to settings.
    /// Call from the OnWindowClosing handler.
    /// Only saves position/size when window is in Normal state.
    /// </summary>
    /// <param name="window">The window to save</param>
    /// <param name="settings">Settings service with window properties</param>
    public static void Save(Window window, IWindowSettings settings)
    {
        settings.WindowMaximized = window.WindowState == WindowState.Maximized;

        if (window.WindowState == WindowState.Normal)
        {
            settings.WindowLeft = window.Position.X;
            settings.WindowTop = window.Position.Y;
            settings.WindowWidth = window.Width;
            settings.WindowHeight = window.Height;
        }
    }
}

/// <summary>
/// Interface for settings services that store window position/size.
/// BaseToolSettingsService implements this automatically.
/// </summary>
public interface IWindowSettings
{
    double WindowLeft { get; set; }
    double WindowTop { get; set; }
    double WindowWidth { get; set; }
    double WindowHeight { get; set; }
    bool WindowMaximized { get; set; }
}
