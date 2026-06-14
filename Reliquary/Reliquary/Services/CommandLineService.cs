using System;
using Radoub.UI.Services;

namespace PlaceableEditor.Services;

/// <summary>
/// PlaceableEditor command line options. No tool-specific flags yet (the New
/// Placeable wizard lands in a later sprint); uses the shared base flags.
/// </summary>
public class PlaceableEditorOptions : CommandLineOptions
{
}

/// <summary>
/// Command line argument parsing for Reliquary (PlaceableEditor).
/// Delegates common flag parsing to the shared CommandLineParser.
/// </summary>
public static class CommandLineService
{
    public static PlaceableEditorOptions Options { get; private set; } = new();

    public static PlaceableEditorOptions Parse(string[] args)
    {
        Options = CommandLineParser.Parse<PlaceableEditorOptions>(
            args,
            fileExtension: ".utp");
        CommandLineParser.ResolveModuleName(Options);
        return Options;
    }

    public static void PrintHelp()
    {
        Console.WriteLine("Reliquary - Placeable Blueprint Editor for Neverwinter Nights");
        Console.WriteLine();
        Console.WriteLine("Usage: Reliquary [options] [file]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --file, -f <path>      Open the specified UTP file");
        Console.WriteLine("  --mod, -m <name>       Set module context (resolves relative --file paths)");
        Console.WriteLine("  --help, -h             Show this help message");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  Reliquary                              Start with empty editor");
        Console.WriteLine("  Reliquary boulder001.utp               Open boulder001.utp");
        Console.WriteLine("  Reliquary --file chest.utp             Open chest.utp");
        Console.WriteLine("  Reliquary -m LNS --file door.utp       Open LNS/door.utp");
    }
}
