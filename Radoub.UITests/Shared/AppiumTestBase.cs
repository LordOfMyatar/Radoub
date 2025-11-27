using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;

namespace Radoub.UITests.Shared;

/// <summary>
/// Base class for all Appium-based UI tests.
/// Handles WinAppDriver session setup and teardown.
/// </summary>
public abstract class AppiumTestBase : IDisposable
{
    protected WindowsDriver? Driver { get; private set; }

    /// <summary>
    /// Default WinAppDriver URL. WinAppDriver must be running on this address.
    /// </summary>
    protected virtual string WinAppDriverUrl => "http://127.0.0.1:4723";

    /// <summary>
    /// Path to the application executable. Override in derived classes.
    /// </summary>
    protected abstract string ApplicationPath { get; }

    /// <summary>
    /// Timeout for finding elements (seconds).
    /// </summary>
    protected virtual int ImplicitWaitSeconds => 5;

    /// <summary>
    /// Starts the application and creates the Appium session.
    /// Call this in test setup or at the start of tests.
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

        var options = new AppiumOptions
        {
            PlatformName = "Windows",
            AutomationName = "Windows"
        };
        options.AddAdditionalAppiumOption("app", ApplicationPath);

        Driver = new WindowsDriver(new Uri(WinAppDriverUrl), options);
        Driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(ImplicitWaitSeconds);
    }

    /// <summary>
    /// Closes the application and ends the session.
    /// </summary>
    protected void StopApplication()
    {
        Driver?.Quit();
        Driver = null;
    }

    public void Dispose()
    {
        StopApplication();
        GC.SuppressFinalize(this);
    }
}
