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
    protected Window? MainWindow { get; private set; }

    /// <summary>
    /// Path to the application executable. Override in derived classes.
    /// </summary>
    protected abstract string ApplicationPath { get; }

    /// <summary>
    /// Timeout for finding elements.
    /// </summary>
    protected virtual TimeSpan DefaultTimeout => TimeSpan.FromSeconds(5);

    /// <summary>
    /// Launches the application and gets the main window.
    /// Call this at the start of tests.
    /// </summary>
    protected void StartApplication()
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
        App = Application.Launch(ApplicationPath);

        // Wait for main window to appear
        MainWindow = App.GetMainWindow(Automation, DefaultTimeout);
    }

    /// <summary>
    /// Closes the application and cleans up resources.
    /// </summary>
    protected void StopApplication()
    {
        App?.Close();
        Automation?.Dispose();
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
