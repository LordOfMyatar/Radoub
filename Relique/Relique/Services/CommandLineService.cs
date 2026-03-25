using System;
using Radoub.UI.Services;

namespace ItemEditor.Services;

/// <summary>
/// ItemEditor-specific command line options extending the shared base.
/// </summary>
public class ItemEditorOptions : CommandLineOptions
{
    /// <summary>
    /// When true, open the New Item wizard on startup (--new / -n).
    /// </summary>
    public bool NewItem { get; set; }
}

/// <summary>
/// Command line argument parsing for ItemEditor.
/// Delegates common flag parsing to shared CommandLineParser.
/// </summary>
public static class CommandLineService
{
    public static ItemEditorOptions Options { get; private set; } = new();

    public static ItemEditorOptions Parse(string[] args)
    {
        // customHandler returns 0 for --new/-n because it's a boolean flag that consumes no
        // additional args. The parser treats 0 as "unrecognized" and falls through to bare-path
        // logic, but --new starts with "-" so the bare-path check safely skips it.
        Options = CommandLineParser.Parse<ItemEditorOptions>(
            args,
            customHandler: (flag, _, _, options) =>
            {
                if ((flag == "--new" || flag == "-n") && options is ItemEditorOptions itemOptions)
                {
                    itemOptions.NewItem = true;
                    return 0;
                }
                return 0;
            },
            fileExtension: ".uti");
        CommandLineParser.ResolveModuleName(Options);
        return Options;
    }

    public static void PrintHelp()
    {
        Console.WriteLine("Relique - Item Blueprint Editor for Neverwinter Nights");
        Console.WriteLine();
        Console.WriteLine("Usage: ItemEditor [options] [file]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --file, -f <path>      Open the specified UTI file");
        Console.WriteLine("  --mod, -m <name>       Set module context (resolves relative --file paths)");
        Console.WriteLine("  --new, -n              Open the New Item wizard on startup");
        Console.WriteLine("  --safemode, -s         Start in SafeMode (reset theme and fonts to defaults)");
        Console.WriteLine("  --help, -h             Show this help message");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  ItemEditor                             Start with empty editor");
        Console.WriteLine("  ItemEditor sword.uti                   Open sword.uti");
        Console.WriteLine("  ItemEditor --file armor.uti            Open armor.uti");
        Console.WriteLine("  ItemEditor -m LNS --file sword.uti     Open LNS/sword.uti");
        Console.WriteLine("  ItemEditor --new                       Open the New Item wizard");
        Console.WriteLine("  ItemEditor --safemode                  Start with default visual settings");
    }
}
