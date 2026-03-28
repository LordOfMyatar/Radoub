using Avalonia;
using System;
using System.IO;
using System.Runtime.InteropServices;
using DialogEditor.Services;
using Microsoft.Extensions.DependencyInjection;
using Radoub.Formats.Logging;
using Radoub.Formats.Settings;
using Radoub.UI.Services;

namespace DialogEditor;

sealed class Program
{
    // Windows console attachment for CLI mode
    // WinExe apps don't have a console by default - we need to attach to parent's console
    // See: https://stackoverflow.com/questions/54536/win32-gui-app-that-writes-usage-text-to-stdout-when-invoked-as-app-exe-help
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FreeConsole();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetStdHandle(int nStdHandle, IntPtr hHandle);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CreateFile(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    private const int ATTACH_PARENT_PROCESS = -1;
    private const int STD_OUTPUT_HANDLE = -11;
    private const int STD_ERROR_HANDLE = -12;
    private const uint GENERIC_WRITE = 0x40000000;
    private const uint FILE_SHARE_WRITE = 0x00000002;
    private const uint OPEN_EXISTING = 3;

    /// <summary>
    /// SafeMode service instance - available to App.axaml.cs for applying resets
    /// </summary>
    public static SafeModeService? SafeMode { get; private set; }

    /// <summary>
    /// DI service provider - available to App and MainWindow for resolving services.
    /// #1231: Sprint 3.2 - Dependency injection container.
    /// </summary>
    public static IServiceProvider Services { get; set; } = null!;

    /// <summary>
    /// Attach to parent console for CLI output on Windows
    /// This allows --help and --screenplay to output to the terminal
    /// </summary>
    private static bool AttachToParentConsole()
    {
        if (!OperatingSystem.IsWindows())
            return true; // Non-Windows platforms don't need this

        // Try to attach to parent process's console
        if (!AttachConsole(ATTACH_PARENT_PROCESS))
            return false;

        // Redirect stdout and stderr to the console
        var conout = CreateFile("CONOUT$", GENERIC_WRITE, FILE_SHARE_WRITE,
            IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);

        if (conout != IntPtr.Zero && conout != new IntPtr(-1))
        {
            SetStdHandle(STD_OUTPUT_HANDLE, conout);
            SetStdHandle(STD_ERROR_HANDLE, conout);

            // Reopen Console streams to use the new handles
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
            Console.SetError(new StreamWriter(Console.OpenStandardError()) { AutoFlush = true });
        }

        return true;
    }

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static int Main(string[] args)
    {
        // Parse command line arguments
        var options = CommandLineService.Parse(args);

        // For CLI operations (--help, --screenplay), attach to parent console on Windows
        if (options.ShowHelp || options.ExportScreenplay)
        {
            AttachToParentConsole();
        }

        // Handle --help
        if (options.ShowHelp)
        {
            CommandLineService.PrintHelp();
            return 0;
        }

        // Handle --screenplay (console mode, no GUI)
        if (options.ExportScreenplay)
        {
            if (string.IsNullOrEmpty(options.FilePath))
            {
                Console.Error.WriteLine("Error: --screenplay requires a DLG file path");
                CommandLineService.PrintHelp();
                return 1;
            }
            return CommandLineService.ExportScreenplayAsync(options.FilePath, options.OutputFile).GetAwaiter().GetResult();
        }

        // SafeMode: Reset visual settings to defaults (theme, fonts, flowview)
        // and clear caches/plugin data. This must happen BEFORE SettingsService is initialized.
        if (options.SafeMode)
        {
            SafeMode = new SafeModeService("Parley");
            SafeMode.ActivateSafeMode(clearParameterCache: true, clearPluginData: true);
        }

        // Set app name early so any pre-configuration logging goes to correct directory
        UnifiedLogger.SetAppName("Parley");

        // Initialize unified logging with shared settings
        var sharedSettings = RadoubSettings.Instance;
        UnifiedLogger.Configure(new LoggerConfig
        {
            AppName = "Parley",
            LogLevel = sharedSettings.UseSharedLogging ? sharedSettings.SharedLogLevel : LogLevel.INFO,
            RetainSessions = sharedSettings.SharedLogRetentionSessions
        });

        // Configure dependency injection container (#1231)
        Services = ConfigureServices();

        // Start GUI application
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        return 0;
    }

    /// <summary>
    /// Configure the DI container with service registrations.
    /// #1233: Sprint 3.4 - All services created by DI container, no static Instance access.
    /// </summary>
    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Sub-services used internally by SettingsService
        services.AddSingleton<RecentFilesService>();
        services.AddSingleton<UISettingsService>();
        services.AddSingleton<WindowLayoutService>();
        services.AddSingleton<SpeakerPreferencesService>();
        services.AddSingleton<ParameterCacheService>();
        services.AddSingleton<LoggingSettingsService>();
        services.AddSingleton<ModulePathsService>();
        services.AddSingleton<EditorPreferencesService>();

        // Core services - DI creates instances via constructor injection
        services.AddSingleton<SettingsService>();
        services.AddSingleton<ISettingsService>(sp => sp.GetRequiredService<SettingsService>());
        services.AddSingleton<DialogContextService>();
        services.AddSingleton<IDialogContextService>(sp => sp.GetRequiredService<DialogContextService>());
        services.AddSingleton<ScriptService>();
        services.AddSingleton<IScriptService>(sp => sp.GetRequiredService<ScriptService>());
        services.AddSingleton<PortraitService>();
        services.AddSingleton<IPortraitService>(sp => sp.GetRequiredService<PortraitService>());
        services.AddSingleton<JournalService>();
        services.AddSingleton<IJournalService>(sp => sp.GetRequiredService<JournalService>());

        // Services that were previously accessed via .Instance (#1233)
        services.AddSingleton<GameResourceService>();
        services.AddSingleton<ExternalEditorService>();
        services.AddSingleton<SoundCache>();
        services.AddSingleton<CoverageTracker>();
        // TTS factory deferred to first use — DI singleton only calls Create() on first GetRequiredService (#1961)
        services.AddSingleton<ITtsService>(sp => TtsServiceFactory.Create());

        return services.BuildServiceProvider();
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
