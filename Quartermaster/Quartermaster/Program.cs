using Avalonia;
using Quartermaster.Services;
using System;
using Radoub.Formats.Logging;
using Radoub.Formats.Settings;
using Radoub.UI.Services;

namespace Quartermaster;

sealed class Program
{
    /// <summary>
    /// SafeMode service instance - available to App.axaml.cs for applying resets
    /// </summary>
    public static SafeModeService? SafeMode { get; private set; }

    [STAThread]
    public static int Main(string[] args)
    {
        // Parse command line arguments
        var options = CommandLineService.Parse(args);

        // Handle --help
        if (options.ShowHelp)
        {
            CommandLineService.PrintHelp();
            return 0;
        }

        // SafeMode: Reset visual settings to defaults (theme, fonts)
        if (options.SafeMode)
        {
            SafeMode = new SafeModeService("Quartermaster");
            SafeMode.ActivateSafeMode(clearParameterCache: false, clearPluginData: false);
        }

        // Set app name early so any pre-configuration logging goes to correct directory
        UnifiedLogger.SetAppName("Quartermaster");

        // Initialize unified logging with shared settings
        var sharedSettings = RadoubSettings.Instance;
        UnifiedLogger.Configure(new LoggerConfig
        {
            AppName = "Quartermaster",
            LogLevel = sharedSettings.UseSharedLogging ? sharedSettings.SharedLogLevel : LogLevel.INFO,
            RetainSessions = sharedSettings.SharedLogRetentionSessions
        });

        // Set up global exception handlers to catch crashes
        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            var ex = e.ExceptionObject as Exception;
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"UNHANDLED EXCEPTION: {ex?.Message}\n{ex?.StackTrace}");
        };

        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (sender, e) =>
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"UNOBSERVED TASK EXCEPTION: {e.Exception?.Message}\n{e.Exception?.StackTrace}");
            e.SetObserved(); // Prevent app crash
        };

        // Start GUI application
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        return 0;
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
