using System;
using Radoub.UI.Services;

namespace MerchantEditor.Services;

/// <summary>
/// Command line argument parsing for Fence.
/// Delegates common flag parsing to shared CommandLineParser.
/// </summary>
public static class CommandLineService
{
    public static CommandLineOptions Options { get; private set; } = new();

    public static CommandLineOptions Parse(string[] args)
    {
        Options = CommandLineParser.Parse<CommandLineOptions>(args);
        return Options;
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
