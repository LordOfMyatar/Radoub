using System;
using Radoub.UI.Services;

namespace Manifest.Services;

/// <summary>
/// Manifest-specific command line options.
/// Extends shared CommandLineOptions with quest/entry navigation for cross-tool integration.
/// </summary>
public class ManifestCommandLineOptions : CommandLineOptions
{
    /// <summary>
    /// Quest tag to navigate to after opening
    /// </summary>
    public string? QuestTag { get; set; }

    /// <summary>
    /// Entry ID to select (requires QuestTag)
    /// </summary>
    public uint? EntryId { get; set; }
}

/// <summary>
/// Service for handling command line arguments.
/// Enables cross-tool integration (e.g., Parley's "Open in Manifest" feature).
/// Delegates common flag parsing to shared CommandLineParser.
/// </summary>
public static class CommandLineService
{
    private static ManifestCommandLineOptions? _options;

    /// <summary>
    /// Whether Parse() has been called. Guards against accessing Options before parsing.
    /// </summary>
    public static bool IsParsed => _options != null;

    /// <summary>
    /// Parsed command line options. Must call Parse() first.
    /// </summary>
    public static ManifestCommandLineOptions Options =>
        _options ?? throw new InvalidOperationException("CommandLineService.Parse() must be called before accessing Options.");

    /// <summary>
    /// Parse command line arguments
    /// </summary>
    public static ManifestCommandLineOptions Parse(string[] args)
    {
        _options = CommandLineParser.Parse<ManifestCommandLineOptions>(args, HandleCustomFlag, ".jrl");
        return _options;
    }

    private static int HandleCustomFlag(string flag, string[] args, int currentIndex, CommandLineOptions options)
    {
        var manifestOptions = (ManifestCommandLineOptions)options;

        switch (flag.ToLowerInvariant())
        {
            case "--quest":
            case "-q":
                if (currentIndex + 1 < args.Length)
                {
                    manifestOptions.QuestTag = args[currentIndex + 1];
                    return 1;
                }
                return 0;

            case "--entry":
            case "-e":
                if (currentIndex + 1 < args.Length)
                {
                    if (uint.TryParse(args[currentIndex + 1], out var entryId))
                    {
                        manifestOptions.EntryId = entryId;
                    }
                    return 1;
                }
                return 0;

            default:
                return 0;
        }
    }

    /// <summary>
    /// Print help text to console
    /// </summary>
    public static void PrintHelp()
    {
        Console.WriteLine(@"Manifest - NWN Journal Editor

Usage: Manifest [options] [file.jrl]

Options:
  -h, --help              Show this help message
  -s, --safemode          Start in SafeMode (reset theme and fonts to defaults)
  -f, --file <path>       Path to JRL file to open
  -q, --quest <tag>       Quest tag to navigate to after opening
  -e, --entry <id>        Entry ID to select (requires --quest)

Examples:
  Manifest module.jrl                    Open journal file
  Manifest --file module.jrl             Same as above
  Manifest --safemode                    Start with default visual settings
  Manifest module.jrl --quest my_quest   Open and navigate to quest
  Manifest module.jrl -q my_quest -e 100 Open, navigate to quest, select entry 100
");
    }
}
