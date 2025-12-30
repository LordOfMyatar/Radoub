using System.Diagnostics;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;

namespace Radoub.IntegrationTests.Shared;

/// <summary>
/// Base class for all FlaUI-based UI tests.
/// Handles application launch and teardown.
/// Uses isolated settings directories to prevent tests from modifying user preferences.
/// </summary>
public abstract class FlaUITestBase : IDisposable
{
    protected Application? App { get; private set; }
    protected UIA3Automation? Automation { get; private set; }
    protected Window? MainWindow { get; set; }

    /// <summary>
    /// Isolated settings directory for this test run. Cleaned up on dispose.
    /// </summary>
    private string? _isolatedSettingsDir;

    /// <summary>
    /// Path to the application executable. Override in derived classes.
    /// </summary>
    protected abstract string ApplicationPath { get; }

    /// <summary>
    /// Timeout for finding elements.
    /// </summary>
    protected virtual TimeSpan DefaultTimeout => TimeSpan.FromSeconds(5);

    /// <summary>
    /// Timeout for waiting for file operations to complete.
    /// </summary>
    protected virtual TimeSpan FileOperationTimeout => TimeSpan.FromSeconds(10);

    /// <summary>
    /// Launches the application and gets the main window.
    /// Call this at the start of tests.
    /// Uses isolated settings directories to prevent tests from modifying user preferences.
    /// </summary>
    protected void StartApplication(string? arguments = null)
    {
        if (string.IsNullOrEmpty(ApplicationPath))
        {
            throw new InvalidOperationException("ApplicationPath must be set before starting the application.");
        }

        if (!File.Exists(ApplicationPath))
        {
            throw new FileNotFoundException($"Application not found at: {ApplicationPath}");
        }

        Automation = new UIA3Automation();

        // Create isolated settings directory for this test run
        _isolatedSettingsDir = Path.Combine(Path.GetTempPath(), "Radoub.IntegrationTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_isolatedSettingsDir);

        // Create tool-specific subdirectories for settings
        var parleySettingsDir = Path.Combine(_isolatedSettingsDir, "Parley");
        var manifestSettingsDir = Path.Combine(_isolatedSettingsDir, "Manifest");
        var quartermasterSettingsDir = Path.Combine(_isolatedSettingsDir, "Quartermaster");
        Directory.CreateDirectory(parleySettingsDir);
        Directory.CreateDirectory(manifestSettingsDir);
        Directory.CreateDirectory(quartermasterSettingsDir);

        // Pre-seed Parley settings with test-friendly defaults
        // SideBySide layout is most stable for automated testing (no separate windows)
        var parleySettings = @"{
  ""FlowchartLayout"": ""SideBySide"",
  ""FlowchartVisible"": false
}";
        File.WriteAllText(Path.Combine(parleySettingsDir, "ParleySettings.json"), parleySettings);

        // Pre-seed Manifest settings with test-friendly defaults
        var manifestSettings = @"{}";
        File.WriteAllText(Path.Combine(manifestSettingsDir, "ManifestSettings.json"), manifestSettings);

        // Pre-seed Quartermaster settings with test-friendly defaults
        var quartermasterSettings = @"{}";
        File.WriteAllText(Path.Combine(quartermasterSettingsDir, "QuartermasterSettings.json"), quartermasterSettings);

        var processInfo = new ProcessStartInfo
        {
            FileName = ApplicationPath,
            Arguments = arguments ?? string.Empty,
            UseShellExecute = false
        };

        // Set environment variables for isolated settings
        // RADOUB_SETTINGS_DIR: ~/Radoub equivalent (RadoubSettings.json)
        // PARLEY_SETTINGS_DIR: ~/Radoub/Parley equivalent (ParleySettings.json)
        // MANIFEST_SETTINGS_DIR: ~/Radoub/Manifest equivalent (ManifestSettings.json)
        // QUARTERMASTER_SETTINGS_DIR: ~/Radoub/Quartermaster equivalent
        processInfo.Environment["RADOUB_SETTINGS_DIR"] = _isolatedSettingsDir;
        processInfo.Environment["PARLEY_SETTINGS_DIR"] = parleySettingsDir;
        processInfo.Environment["MANIFEST_SETTINGS_DIR"] = manifestSettingsDir;
        processInfo.Environment["QUARTERMASTER_SETTINGS_DIR"] = quartermasterSettingsDir;

        App = Application.Launch(processInfo);

        // Wait for main window to appear
        MainWindow = App.GetMainWindow(Automation, DefaultTimeout);

        // Explicitly focus the main window to prevent keyboard input going to other apps
        // This fixes issues where VSCode or other apps steal focus during test startup
        if (MainWindow != null)
        {
            MainWindow.Focus();
            Thread.Sleep(100); // Brief delay to ensure focus is established
        }
    }

    /// <summary>
    /// Waits for the window title to contain the specified text.
    /// Useful for waiting for file loads to complete.
    /// </summary>
    protected bool WaitForTitleContains(string text, TimeSpan? timeout = null)
    {
        var endTime = DateTime.Now + (timeout ?? DefaultTimeout);
        while (DateTime.Now < endTime)
        {
            // Refresh window reference to get updated title
            MainWindow = App?.GetMainWindow(Automation!, TimeSpan.FromMilliseconds(500));
            if (MainWindow?.Title?.Contains(text, StringComparison.OrdinalIgnoreCase) == true)
            {
                return true;
            }
            Thread.Sleep(100);
        }
        return false;
    }

    /// <summary>
    /// Waits for the window title to NOT contain the specified text.
    /// Useful for waiting for unsaved indicator to disappear after save.
    /// Returns true if text not found OR if app has exited.
    /// </summary>
    protected bool WaitForTitleNotContains(string text, TimeSpan? timeout = null)
    {
        var endTime = DateTime.Now + (timeout ?? DefaultTimeout);
        while (DateTime.Now < endTime)
        {
            try
            {
                // Check if app has exited
                if (App == null || App.HasExited)
                {
                    return true; // App exited, text definitely not in title
                }

                MainWindow = App.GetMainWindow(Automation!, TimeSpan.FromMilliseconds(500));
                if (MainWindow?.Title?.Contains(text, StringComparison.OrdinalIgnoreCase) != true)
                {
                    return true;
                }
            }
            catch
            {
                // App may have exited during check
                return true;
            }
            Thread.Sleep(100);
        }
        return false;
    }

    /// <summary>
    /// Clicks a menu item by navigating through the menu hierarchy.
    /// Example: ClickMenu("File", "Save") clicks File menu then Save item.
    /// </summary>
    protected void ClickMenu(params string[] menuPath)
    {
        if (menuPath.Length == 0) return;
        if (MainWindow == null)
        {
            throw new InvalidOperationException("MainWindow is null - cannot click menu");
        }

        const int maxRetries = 5;
        const int retryDelayMs = 300;

        // Click first menu to open it (with retry)
        FlaUI.Core.AutomationElements.AutomationElement? menu = null;
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            menu = MainWindow.FindFirstDescendant(cf => cf.ByName(menuPath[0]));
            if (menu != null) break;
            Thread.Sleep(retryDelayMs);
            // Refresh window reference in case UI hasn't fully loaded
            if (App != null && !App.HasExited && Automation != null)
            {
                MainWindow = App.GetMainWindow(Automation, TimeSpan.FromMilliseconds(500));
            }
        }
        if (menu == null)
        {
            throw new InvalidOperationException($"Menu '{menuPath[0]}' not found in window after {maxRetries} attempts");
        }
        menu.AsMenuItem().Click();

        // Wait for menu dropdown to fully render (Avalonia animations + resource pressure)
        Thread.Sleep(300);

        // Click subsequent items (with retry for each)
        for (int i = 1; i < menuPath.Length; i++)
        {
            FlaUI.Core.AutomationElements.AutomationElement? item = null;
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                // For submenus, search from the desktop to find popup menus
                // Avalonia creates popup menus as separate top-level elements
                var desktop = Automation?.GetDesktop();
                if (desktop != null)
                {
                    item = desktop.FindFirstDescendant(cf => cf.ByName(menuPath[i]));
                }
                // Fallback to searching from MainWindow
                if (item == null && MainWindow != null)
                {
                    item = MainWindow.FindFirstDescendant(cf => cf.ByName(menuPath[i]));
                }
                if (item != null) break;
                Thread.Sleep(retryDelayMs);
            }
            if (item == null)
            {
                throw new InvalidOperationException($"Menu item '{menuPath[i]}' not found after {maxRetries} attempts");
            }
            item.AsMenuItem().Click();
            Thread.Sleep(100);
        }
    }

    /// <summary>
    /// Ensures the main window has keyboard focus before performing actions.
    /// Call this before any keyboard input to prevent keystrokes going to other apps (like VSCode).
    /// </summary>
    /// <param name="maxRetries">Number of focus attempts before giving up</param>
    /// <returns>True if focus was obtained, false otherwise</returns>
    /// <remarks>
    /// Note: Avalonia windows don't reliably report HasKeyboardFocus via UIA automation.
    /// This method uses SetForeground() and Focus() which work correctly, then trusts
    /// the result rather than strictly verifying via automation properties.
    /// </remarks>
    protected bool EnsureFocused(int maxRetries = 3)
    {
        if (MainWindow == null)
        {
            return false;
        }

        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                // Use both SetForeground and Focus for maximum compatibility
                // SetForeground brings window to front, Focus sets keyboard focus
                MainWindow.SetForeground();
                Thread.Sleep(50);
                MainWindow.Focus();
                Thread.Sleep(100); // Allow focus to settle

                // Check if window is visible and not minimized - that's our best indicator
                // HasKeyboardFocus is unreliable with Avalonia windows via UIA
                if (!MainWindow.Properties.IsOffscreen.ValueOrDefault)
                {
                    // Window is visible and we've called focus - trust it worked
                    return true;
                }
            }
            catch
            {
                // Window may have closed or become invalid
            }

            Thread.Sleep(200); // Wait before retry
        }

        // Last resort: try clicking the window title bar area to force focus
        try
        {
            // Click near top-center of window (title bar area) to grab focus
            var bounds = MainWindow.BoundingRectangle;
            var clickPoint = new System.Drawing.Point(
                bounds.X + bounds.Width / 2,
                bounds.Y + 20); // 20px down from top (title bar)
            FlaUI.Core.Input.Mouse.Click(clickPoint);
            Thread.Sleep(150);
            return !MainWindow.Properties.IsOffscreen.ValueOrDefault;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Sends a keyboard shortcut (e.g., Ctrl+S, Ctrl+Z) with automatic focus verification.
    /// ALWAYS use this instead of direct Keyboard.TypeSimultaneously calls.
    /// </summary>
    /// <param name="keys">Virtual key codes to press simultaneously</param>
    /// <exception cref="InvalidOperationException">Thrown if focus cannot be obtained</exception>
    protected void SendKeyboardShortcut(params VirtualKeyShort[] keys)
    {
        if (!EnsureFocused())
        {
            throw new InvalidOperationException(
                "Failed to focus main window before keyboard input. " +
                "This prevents keystrokes from going to the wrong application.");
        }

        FlaUI.Core.Input.Keyboard.TypeSimultaneously(keys);
    }

    #region Common Keyboard Shortcuts
    // These methods provide focus-safe wrappers for common keyboard shortcuts.
    // Always use these instead of direct Keyboard.TypeSimultaneously calls.

    /// <summary>Sends Ctrl+S (Save) with focus verification.</summary>
    protected void SendCtrlS() => SendKeyboardShortcut(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_S);

    /// <summary>Sends Ctrl+Z (Undo) with focus verification.</summary>
    protected void SendCtrlZ() => SendKeyboardShortcut(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);

    /// <summary>Sends Ctrl+Y (Redo) with focus verification.</summary>
    protected void SendCtrlY() => SendKeyboardShortcut(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Y);

    /// <summary>Sends Ctrl+D (Duplicate/Add) with focus verification.</summary>
    protected void SendCtrlD() => SendKeyboardShortcut(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_D);

    /// <summary>Sends Ctrl+N (New) with focus verification.</summary>
    protected void SendCtrlN() => SendKeyboardShortcut(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_N);

    /// <summary>Sends Ctrl+O (Open) with focus verification.</summary>
    protected void SendCtrlO() => SendKeyboardShortcut(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_O);

    /// <summary>Sends Delete key with focus verification.</summary>
    protected void SendDelete() => SendKeyboardShortcut(VirtualKeyShort.DELETE);

    /// <summary>Sends Escape key with focus verification.</summary>
    protected void SendEscape() => SendKeyboardShortcut(VirtualKeyShort.ESCAPE);

    #endregion

    /// <summary>
    /// Sends keyboard shortcut to the application.
    /// DEPRECATED: Use SendKeyboardShortcut() instead for focus-safe keyboard input.
    /// </summary>
    [Obsolete("Use SendKeyboardShortcut() for focus-safe keyboard input")]
    protected void SendKeys(string keys)
    {
        if (!EnsureFocused())
        {
            throw new InvalidOperationException(
                "Failed to focus main window before keyboard input.");
        }
        FlaUI.Core.Input.Keyboard.Type(keys);
    }

    /// <summary>
    /// Closes the application and cleans up resources.
    /// Handles cases where app may have already exited.
    /// Uses graceful shutdown to avoid Avalonia/SkiaSharp render crashes.
    /// </summary>
    protected void StopApplication()
    {
        try
        {
            if (App != null && !App.HasExited)
            {
                // Give Avalonia time to finish any pending renders before closing
                Thread.Sleep(200);

                // Close the application gracefully
                // Previously used Alt+F4 keystroke, but that could close the wrong window
                // if our window wasn't properly focused (e.g., VSCode instead of the test app)
                // See #593: FlaUI tests close VSCode instead of just the app
                try
                {
                    // Use WM_CLOSE via App.Close() - targets our specific process
                    // This is safer than keyboard shortcuts which go to focused window
                    App.Close();
                }
                catch
                {
                    // Ignore close errors - may already be closing
                }

                // Wait for process to fully exit (prevents resource conflicts between tests)
                var timeout = TimeSpan.FromSeconds(5);
                var startTime = DateTime.Now;
                while (!App.HasExited && (DateTime.Now - startTime) < timeout)
                {
                    Thread.Sleep(100);
                }

                // Force kill if still running
                if (!App.HasExited)
                {
                    try { App.Kill(); } catch { }
                }
            }
        }
        catch
        {
            // App may have already exited, ignore
        }

        try
        {
            Automation?.Dispose();
        }
        catch
        {
            // Ignore disposal errors
        }

        // Brief delay to ensure OS releases all handles
        Thread.Sleep(500);

        App = null;
        Automation = null;
        MainWindow = null;
    }

    public void Dispose()
    {
        StopApplication();
        CleanupIsolatedSettings();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Cleans up the isolated settings directory created for this test run.
    /// </summary>
    private void CleanupIsolatedSettings()
    {
        if (string.IsNullOrEmpty(_isolatedSettingsDir))
            return;

        try
        {
            if (Directory.Exists(_isolatedSettingsDir))
            {
                Directory.Delete(_isolatedSettingsDir, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup - ignore errors (file locks, etc.)
        }

        _isolatedSettingsDir = null;
    }
}
