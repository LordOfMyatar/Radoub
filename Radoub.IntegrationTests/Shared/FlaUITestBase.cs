using System.Diagnostics;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
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
        Directory.CreateDirectory(parleySettingsDir);
        Directory.CreateDirectory(manifestSettingsDir);

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
        processInfo.Environment["RADOUB_SETTINGS_DIR"] = _isolatedSettingsDir;
        processInfo.Environment["PARLEY_SETTINGS_DIR"] = parleySettingsDir;
        processInfo.Environment["MANIFEST_SETTINGS_DIR"] = manifestSettingsDir;

        App = Application.Launch(processInfo);

        // Wait for main window to appear
        MainWindow = App.GetMainWindow(Automation, DefaultTimeout);
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
    /// Sends keyboard shortcut to the application.
    /// </summary>
    protected void SendKeys(string keys)
    {
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

                // Use graceful close via Alt+F4 to let Avalonia shutdown properly
                // This avoids SkiaSharp canvas flush crashes during mid-render close
                try
                {
                    MainWindow?.Focus();
                    Thread.Sleep(100);
                    FlaUI.Core.Input.Keyboard.TypeSimultaneously(
                        FlaUI.Core.WindowsAPI.VirtualKeyShort.ALT,
                        FlaUI.Core.WindowsAPI.VirtualKeyShort.F4);
                }
                catch
                {
                    // Fallback to programmatic close
                    App.Close();
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
