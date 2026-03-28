using System;
using Radoub.Formats.Settings;
using Radoub.UI.Services;

namespace RadoubLauncher.Services;

/// <summary>
/// Trebuchet-specific command line options.
/// </summary>
public class TrebuchetOptions : CommandLineOptions
{
    public string? ModulePath { get; set; }
    public bool OpenSettings { get; set; }
}

/// <summary>
/// Parses command line arguments for Trebuchet.
/// Delegates common flag parsing to shared CommandLineParser.
/// </summary>
public static class CommandLineService
{
    /// <summary>
    /// Backwards-compatible Options type alias.
    /// </summary>
    public class Options
    {
        public bool ShowHelp { get; set; }
        public bool SafeMode { get; set; }
        public string? ModulePath { get; set; }
        public bool OpenSettings { get; set; }

        internal static Options FromParsed(TrebuchetOptions parsed) => new()
        {
            ShowHelp = parsed.ShowHelp,
            SafeMode = parsed.SafeMode,
            ModulePath = parsed.ModulePath,
            OpenSettings = parsed.OpenSettings
        };
    }

    public static Options Parse(string[] args)
    {
        var parsed = CommandLineParser.Parse<TrebuchetOptions>(args, HandleCustomFlag);

        // --project resolves to a module path for Trebuchet
        if (!string.IsNullOrEmpty(parsed.ModuleName) && string.IsNullOrEmpty(parsed.ModulePath))
        {
            var modulePath = ProjectPathResolver.ResolveModulePath(parsed.ModuleName);
            if (!string.IsNullOrEmpty(modulePath))
            {
                parsed.ModulePath = modulePath;
                RadoubSettings.Instance.CurrentModulePath = modulePath;
            }
        }

        return Options.FromParsed(parsed);
    }

    private static int HandleCustomFlag(string flag, string[] args, int currentIndex, CommandLineOptions options)
    {
        switch (flag.ToLowerInvariant())
        {
            case "--module":
                if (currentIndex + 1 < args.Length)
                {
                    ((TrebuchetOptions)options).ModulePath = args[currentIndex + 1];
                    return 1;
                }
                return 0;
            case "--settings":
                ((TrebuchetOptions)options).OpenSettings = true;
                return 0;
            default:
                return 0;
        }
    }

    public static void PrintHelp()
    {
        Console.WriteLine("Trebuchet - Radoub Launcher for Neverwinter Nights");
        Console.WriteLine();
        Console.WriteLine("Usage: Trebuchet [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -h, --help             Show this help message");
        Console.WriteLine("  --safemode             Start with default theme and font settings");
        Console.WriteLine("  --settings             Open settings window on startup");
        Console.WriteLine("  -m, --module <path>    Path to module file to open");
        Console.WriteLine("  --mod <name>             Open module by name (resolves from modules directory)");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  Trebuchet");
        Console.WriteLine("  Trebuchet --module \"~/modules/my_module.mod\"");
        Console.WriteLine("  Trebuchet --mod LNS");
        Console.WriteLine("  Trebuchet --safemode");
    }
}
