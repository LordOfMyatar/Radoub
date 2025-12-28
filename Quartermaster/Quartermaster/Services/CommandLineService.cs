using System;

namespace Quartermaster.Services;

/// <summary>
/// Command line argument parsing for Quartermaster.
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
        Console.WriteLine("Quartermaster - Creature and Inventory Editor for Neverwinter Nights");
        Console.WriteLine();
        Console.WriteLine("Usage: Quartermaster [options] [file]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --file, -f <path>  Open the specified UTC/BIC file");
        Console.WriteLine("  --help, -h         Show this help message");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  Quartermaster                     Start with empty editor");
        Console.WriteLine("  Quartermaster creature.utc        Open creature.utc");
        Console.WriteLine("  Quartermaster --file player.bic   Open player.bic");
    }
}

public class CommandLineOptions
{
    public bool ShowHelp { get; set; }
    public string? FilePath { get; set; }
}
