using Avalonia;
using CreatureEditor.Services;
using System;
using Radoub.Formats.Logging;

namespace CreatureEditor;

sealed class Program
{
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

        // Initialize unified logging
        UnifiedLogger.Configure(new LoggerConfig
        {
            AppName = "CreatureEditor",
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
