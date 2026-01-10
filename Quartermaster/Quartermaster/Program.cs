using Avalonia;
using Quartermaster.Services;
using System;
using Radoub.Formats.Logging;
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

        // Initialize unified logging
        UnifiedLogger.Configure(new LoggerConfig
        {
            AppName = "Quartermaster",
            LogLevel = LogLevel.INFO,
            RetainSessions = 10
        });

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
