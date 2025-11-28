using System.Diagnostics;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;

namespace Radoub.UITests.Shared;

/// <summary>
/// Base class for all FlaUI-based UI tests.
/// Handles application launch and teardown.
/// </summary>
public abstract class FlaUITestBase : IDisposable
{
    protected Application? App { get; private set; }
    protected UIA3Automation? Automation { get; private set; }
    protected Window? MainWindow { get; set; }

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

        var processInfo = new ProcessStartInfo
        {
            FileName = ApplicationPath,
            Arguments = arguments ?? string.Empty,
            UseShellExecute = false
        };

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

        // Click first menu to open it
        var menu = MainWindow.FindFirstDescendant(cf => cf.ByName(menuPath[0]));
        if (menu == null)
        {
            throw new InvalidOperationException($"Menu '{menuPath[0]}' not found in window");
        }
        menu.AsMenuItem().Click();

        // Small delay for menu to open
        Thread.Sleep(100);

        // Click subsequent items
        for (int i = 1; i < menuPath.Length; i++)
        {
            var item = MainWindow.FindFirstDescendant(cf => cf.ByName(menuPath[i]));
            if (item == null)
            {
                throw new InvalidOperationException($"Menu item '{menuPath[i]}' not found");
            }
            item.AsMenuItem().Click();
            Thread.Sleep(50);
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
    /// </summary>
    protected void StopApplication()
    {
        try
        {
            if (App != null && !App.HasExited)
            {
                App.Close();
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

        App = null;
        Automation = null;
        MainWindow = null;
    }

    public void Dispose()
    {
        StopApplication();
        GC.SuppressFinalize(this);
    }
}
