using System;
using System.IO;

namespace Manifest.Services;

/// <summary>
/// Command line options for Manifest
/// </summary>
public class CommandLineOptions
{
    /// <summary>
    /// JRL file path to open on startup
    /// </summary>
    public string? FilePath { get; set; }

    /// <summary>
    /// Quest tag to navigate to after opening
    /// </summary>
    public string? QuestTag { get; set; }

    /// <summary>
    /// Entry ID to select (requires QuestTag)
    /// </summary>
    public uint? EntryId { get; set; }

    /// <summary>
    /// Show help and exit
    /// </summary>
    public bool ShowHelp { get; set; }
}

/// <summary>
/// Service for handling command line arguments.
/// Enables cross-tool integration (e.g., Parley's "Open in Manifest" feature).
/// </summary>
public static class CommandLineService
{
    private static CommandLineOptions? _options;

    /// <summary>
    /// Parsed command line options
    /// </summary>
    public static CommandLineOptions Options => _options ?? new CommandLineOptions();

    /// <summary>
    /// Parse command line arguments
    /// </summary>
    public static CommandLineOptions Parse(string[] args)
    {
        var options = new CommandLineOptions();

        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            if (arg == "--help" || arg == "-h" || arg == "/?")
            {
                options.ShowHelp = true;
            }
            else if ((arg == "--file" || arg == "-f") && i + 1 < args.Length)
            {
                options.FilePath = args[++i];
            }
            else if ((arg == "--quest" || arg == "-q") && i + 1 < args.Length)
            {
                options.QuestTag = args[++i];
            }
            else if ((arg == "--entry" || arg == "-e") && i + 1 < args.Length)
            {
                if (uint.TryParse(args[++i], out var entryId))
                {
                    options.EntryId = entryId;
                }
            }
            else if (!arg.StartsWith("-") && arg.EndsWith(".jrl", StringComparison.OrdinalIgnoreCase))
            {
                // Positional argument: file path ending in .jrl
                options.FilePath = arg;
            }
            else if (!arg.StartsWith("-") && File.Exists(arg))
            {
                // Positional argument: existing file path
                options.FilePath = arg;
            }
        }

        _options = options;
        return options;
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
  -f, --file <path>       Path to JRL file to open
  -q, --quest <tag>       Quest tag to navigate to after opening
  -e, --entry <id>        Entry ID to select (requires --quest)

Examples:
  Manifest module.jrl                    Open journal file
  Manifest --file module.jrl             Same as above
  Manifest module.jrl --quest my_quest   Open and navigate to quest
  Manifest module.jrl -q my_quest -e 100 Open, navigate to quest, select entry 100
");
    }
}
