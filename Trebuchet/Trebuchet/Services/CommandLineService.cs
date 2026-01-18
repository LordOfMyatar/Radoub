using System;
using System.Linq;

namespace RadoubLauncher.Services;

/// <summary>
/// Parses command line arguments for Trebuchet.
/// </summary>
public static class CommandLineService
{
    public class Options
    {
        public bool ShowHelp { get; set; }
        public bool SafeMode { get; set; }
        public string? ModulePath { get; set; }
    }

    public static Options Parse(string[] args)
    {
        var options = new Options();

        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i].ToLowerInvariant();

            switch (arg)
            {
                case "-h":
                case "--help":
                    options.ShowHelp = true;
                    break;

                case "--safemode":
                case "--safe-mode":
                    options.SafeMode = true;
                    break;

                case "-m":
                case "--module":
                    if (i + 1 < args.Length)
                    {
                        options.ModulePath = args[++i];
                    }
                    break;
            }
        }

        return options;
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
