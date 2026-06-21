using Avalonia;
using ItemEditor.Services;
using System;
using System.Collections.Generic;
using Radoub.Formats.Logging;
using Radoub.Formats.Settings;
using Radoub.UI.Services;

namespace ItemEditor;

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

        // Set app name early so any pre-configuration logging goes to correct directory
        UnifiedLogger.SetAppName("Relique");

        // Initialize unified logging with shared settings
        var sharedSettings = RadoubSettings.Instance;
        UnifiedLogger.Configure(new LoggerConfig
        {
            AppName = "Relique",
            LogLevel = sharedSettings.UseSharedLogging ? sharedSettings.SharedLogLevel : LogLevel.INFO,
            RetainSessions = sharedSettings.SharedLogRetentionSessions
        });

        // Build stamp: log the running assembly's version + build time so a session's log
        // unambiguously identifies which binary produced it (build/profile confusion guard).
        LogBuildStamp();

        // Start GUI application
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        return 0;
    }

    /// <summary>
    /// Log the running assembly's informational version and last-write time of the DLL, so the
    /// session log states exactly which build is active. Cheap, best-effort.
    /// </summary>
    private static void LogBuildStamp()
    {
        try
        {
            var asm = System.Reflection.Assembly.GetExecutingAssembly();
            var version = asm.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
                is object[] attrs && attrs.Length > 0
                ? ((System.Reflection.AssemblyInformationalVersionAttribute)attrs[0]).InformationalVersion
                : asm.GetName().Version?.ToString() ?? "unknown";
            var buildTime = System.IO.File.GetLastWriteTime(asm.Location).ToString("yyyy-MM-dd HH:mm:ss");
            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"[BuildStamp] Relique version={version} builtAt={buildTime} dll={asm.Location}");
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, $"[BuildStamp] failed: {ex.Message}");
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        var builder = AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

        // Linux: Explicitly set rendering modes (no Vulkan).
        // Vulkan GPU drivers reserve ~256GB of virtual address space, which
        // triggers the OOM killer on memory-constrained systems.
        // EGL is needed for OpenGlControlBase on Wayland/XWayland where GLX
        // contexts may not initialize for offscreen GL controls (#2074).
        if (OperatingSystem.IsLinux())
        {
            builder = builder.With(new X11PlatformOptions
            {
                RenderingMode = new List<X11RenderingMode>
                {
                    X11RenderingMode.Egl,
                    X11RenderingMode.Glx,
                    X11RenderingMode.Software
                }
            });
        }

        return builder;
    }
}
