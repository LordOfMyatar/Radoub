using System;
using Radoub.UI.Services;

namespace RadoubLauncher.Services;

/// <summary>
/// Trebuchet-specific command line options.
/// </summary>
public class TrebuchetOptions : CommandLineOptions
{
    public string? ModulePath { get; set; }
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

        internal static Options FromParsed(TrebuchetOptions parsed) => new()
        {
            ShowHelp = parsed.ShowHelp,
            SafeMode = parsed.SafeMode,
            ModulePath = parsed.ModulePath
        };
    }

    public static Options Parse(string[] args)
    {
        var parsed = CommandLineParser.Parse<TrebuchetOptions>(args, HandleCustomFlag);
        return Options.FromParsed(parsed);
    }

    private static int HandleCustomFlag(string flag, string[] args, int currentIndex, CommandLineOptions options)
    {
        switch (flag.ToLowerInvariant())
        {
            case "-m":
            case "--module":
                if (currentIndex + 1 < args.Length)
                {
                    ((TrebuchetOptions)options).ModulePath = args[currentIndex + 1];
                    return 1;
                }
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
        Console.WriteLine("  -h, --help        Show this help message");
        Console.WriteLine("  --safemode        Start with default theme and font settings");
        Console.WriteLine("  -m, --module      Path to module file to open");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  Trebuchet");
        Console.WriteLine("  Trebuchet --module \"~/modules/my_module.mod\"");
        Console.WriteLine("  Trebuchet --safemode");
    }
}
