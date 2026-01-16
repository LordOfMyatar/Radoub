using System;

namespace MerchantEditor.Services;

/// <summary>
/// Command line argument parsing for Fence.
/// Supports --file for opening files at startup.
/// </summary>
public static class CommandLineService
{
    public static CommandLineOptions Options { get; private set; } = new();

    public static CommandLineOptions Parse(string[] args)
    {
        var options = new CommandLineOptions();

        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            switch (arg.ToLowerInvariant())
            {
                case "--help":
                case "-h":
                case "-?":
                    options.ShowHelp = true;
                    break;

                case "--safemode":
                case "--safe-mode":
                case "-s":
                    options.SafeMode = true;
                    break;

                case "--file":
                case "-f":
                    if (i + 1 < args.Length)
                    {
                        options.FilePath = args[++i];
                    }
                    break;

                default:
                    // Treat unrecognized arguments as file paths if they exist
                    if (!arg.StartsWith("-") && System.IO.File.Exists(arg))
                    {
                        options.FilePath = arg;
                    }
                    break;
            }
        }

        Options = options;
        return options;
    }

    public static void PrintHelp()
    {
        Console.WriteLine("Fence - Merchant Editor for Neverwinter Nights");
        Console.WriteLine();
        Console.WriteLine("Usage: Fence [options] [file]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --file, -f <path>  Open the specified UTM file");
        Console.WriteLine("  --safemode, -s     Start in SafeMode (reset theme and fonts to defaults)");
        Console.WriteLine("  --help, -h         Show this help message");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  Fence                     Start with empty editor");
        Console.WriteLine("  Fence merchant.utm        Open merchant.utm");
        Console.WriteLine("  Fence --file store.utm    Open store.utm");
        Console.WriteLine("  Fence --safemode          Start with default visual settings");
    }
}

public class CommandLineOptions
{
    public bool ShowHelp { get; set; }
    public string? FilePath { get; set; }

    /// <summary>
    /// Start in SafeMode - reset visual settings to defaults
    /// </summary>
    public bool SafeMode { get; set; }
}
