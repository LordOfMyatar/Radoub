using Avalonia;
using RadoubLauncher.Services;
using System;
using Radoub.Formats.Logging;
using Radoub.Formats.Settings;
using Radoub.UI.Services;

namespace RadoubLauncher;

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
            SafeMode = new SafeModeService("Trebuchet");
            SafeMode.ActivateSafeMode(clearParameterCache: false, clearPluginData: false);
        }

        // Initialize unified logging FIRST with defaults (before any code that might log)
        UnifiedLogger.Configure(new LoggerConfig
        {
            AppName = "Trebuchet",
            LogLevel = LogLevel.INFO,
            RetainSessions = 10
        });

        // Then apply shared settings if enabled
        var sharedSettings = RadoubSettings.Instance;
        if (sharedSettings.UseSharedLogging)
        {
            UnifiedLogger.SetLogLevel(sharedSettings.SharedLogLevel);
        }

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
