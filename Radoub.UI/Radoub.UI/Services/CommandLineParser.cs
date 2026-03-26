using System;
using System.IO;
using Radoub.Formats.Settings;

namespace Radoub.UI.Services;

/// <summary>
/// Base command line options shared by all Radoub tools.
/// Tool-specific options should inherit from this class.
/// </summary>
public class CommandLineOptions
{
    public bool ShowHelp { get; set; }
    public bool SafeMode { get; set; }
    public string? FilePath { get; set; }
    public string? ModuleName { get; set; }
}

/// <summary>
/// Shared command line argument parser for all Radoub tools.
/// Handles --help, --safemode, --file, and bare file path arguments.
/// Tools with additional flags should use the customHandler callback.
/// </summary>
public static class CommandLineParser
{
    /// <summary>
    /// Callback for tool-specific flag handling.
    /// Returns the number of additional arguments consumed (0 if unrecognized).
    /// </summary>
    public delegate int CustomFlagHandler(string flag, string[] args, int currentIndex, CommandLineOptions options);

    /// <summary>
    /// Parse command line arguments into the specified options type.
    /// Handles common flags (--help, --safemode, --file) and delegates
    /// unknown flags to the customHandler if provided.
    /// </summary>
    /// <param name="args">Command line arguments</param>
    /// <param name="customHandler">Optional handler for tool-specific flags</param>
    /// <param name="fileExtension">Optional file extension (e.g. ".dlg") to accept as positional arg even if file doesn't exist</param>
    public static T Parse<T>(string[] args, CustomFlagHandler? customHandler = null, string? fileExtension = null)
        where T : CommandLineOptions, new()
    {
        var options = new T();

        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            switch (arg.ToLowerInvariant())
            {
                case "--help":
                case "-h":
                case "-?":
                case "/?":
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

                case "--mod":
                case "-m":
                    if (i + 1 < args.Length)
                    {
                        options.ModuleName = args[++i];
                    }
                    break;

                default:
                    // Try tool-specific handler first
                    if (customHandler != null)
                    {
                        int consumed = customHandler(arg, args, i, options);
                        if (consumed > 0)
                        {
                            i += consumed;
                            break;
                        }
                    }

                    // Bare file path: accept by extension match or file existence
                    if (!arg.StartsWith("-"))
                    {
                        if (fileExtension != null && arg.EndsWith(fileExtension, StringComparison.OrdinalIgnoreCase))
                        {
                            options.FilePath = arg;
                        }
                        else if (File.Exists(arg))
                        {
                            options.FilePath = arg;
                        }
                    }
                    break;
            }
        }

        return options;
    }

    /// <summary>
    /// Resolve module name to file path and set current module in RadoubSettings.
    /// Shared logic used by all tools after parsing command line arguments.
    /// </summary>
    public static void ResolveModuleName(CommandLineOptions options)
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
