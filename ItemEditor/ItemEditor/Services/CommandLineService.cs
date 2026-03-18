using System;
using Radoub.UI.Services;

namespace ItemEditor.Services;

/// <summary>
/// Command line argument parsing for ItemEditor.
/// Delegates common flag parsing to shared CommandLineParser.
/// </summary>
public static class CommandLineService
{
    public static CommandLineOptions Options { get; private set; } = new();

    public static CommandLineOptions Parse(string[] args)
    {
        Options = CommandLineParser.Parse<CommandLineOptions>(args, fileExtension: ".uti");
        return Options;
    }

    public static void PrintHelp()
    {
        Console.WriteLine("ItemEditor - Item Blueprint Editor for Neverwinter Nights");
        Console.WriteLine();
        Console.WriteLine("Usage: ItemEditor [options] [file]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --file, -f <path>  Open the specified UTI file");
        Console.WriteLine("  --safemode, -s     Start in SafeMode (reset theme and fonts to defaults)");
        Console.WriteLine("  --help, -h         Show this help message");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  ItemEditor                     Start with empty editor");
        Console.WriteLine("  ItemEditor sword.uti           Open sword.uti");
        Console.WriteLine("  ItemEditor --file armor.uti    Open armor.uti");
        Console.WriteLine("  ItemEditor --safemode          Start with default visual settings");
    }
}
