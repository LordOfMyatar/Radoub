using System;
using Radoub.Formats.Settings;
using Radoub.UI.Services;

namespace Quartermaster.Services;

/// <summary>
/// Command line argument parsing for Quartermaster.
/// Delegates common flag parsing to shared CommandLineParser.
/// </summary>
public static class CommandLineService
{
    public static CommandLineOptions Options { get; private set; } = new();

    public static CommandLineOptions Parse(string[] args)
    {
        Options = CommandLineParser.Parse<CommandLineOptions>(args);
        ResolveModuleName(Options);
        return Options;
    }

    public static void PrintHelp()
    {
        Console.WriteLine("Quartermaster - Creature and Inventory Editor for Neverwinter Nights");
        Console.WriteLine();
        Console.WriteLine("Usage: Quartermaster [options] [file]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --file, -f <path>      Open the specified UTC/BIC file");
        Console.WriteLine("  --mod, -m <name>       Set module context (resolves relative --file paths)");
        Console.WriteLine("  --safemode, -s         Start in SafeMode (reset theme and fonts to defaults)");
        Console.WriteLine("  --help, -h             Show this help message");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  Quartermaster                              Start with empty editor");
        Console.WriteLine("  Quartermaster creature.utc                 Open creature.utc");
        Console.WriteLine("  Quartermaster --file player.bic            Open player.bic");
        Console.WriteLine("  Quartermaster -m LNS --file goblin.utc    Open LNS/goblin.utc");
        Console.WriteLine("  Quartermaster --safemode                   Start with default visual settings");
    }

    private static void ResolveModuleName(CommandLineOptions options)
    {
        if (string.IsNullOrEmpty(options.ModuleName))
            return;

        var resolved = ProjectPathResolver.ResolveFilePath(options.ModuleName, options.FilePath);
        if (resolved != null)
            options.FilePath = resolved;

        var modulePath = ProjectPathResolver.ResolveModulePath(options.ModuleName);
        if (!string.IsNullOrEmpty(modulePath))
            RadoubSettings.Instance.CurrentModulePath = modulePath;
    }
}
