using System;
using System.IO;
using System.Threading.Tasks;
using DialogEditor.Models;
using DialogEditor.Services;
using Radoub.Formats.Logging;
using Radoub.UI.Services;

namespace DialogEditor.Services
{
    /// <summary>
    /// Parley-specific command line options.
    /// Extends shared CommandLineOptions with screenplay export flags.
    /// </summary>
    public class ParleyCommandLineOptions : CommandLineOptions
    {
        /// <summary>
        /// Export screenplay to stdout and exit
        /// </summary>
        public bool ExportScreenplay { get; set; }

        /// <summary>
        /// Output file for screenplay export (optional, defaults to stdout)
        /// </summary>
        public string? OutputFile { get; set; }
    }

    /// <summary>
    /// Service for handling command line arguments.
    /// Delegates common flag parsing to shared CommandLineParser.
    /// </summary>
    public static class CommandLineService
    {
        private static ParleyCommandLineOptions? _options;

        /// <summary>
        /// Parsed command line options
        /// </summary>
        public static ParleyCommandLineOptions Options => _options ?? new ParleyCommandLineOptions();

        /// <summary>
        /// Parse command line arguments
        /// </summary>
        public static ParleyCommandLineOptions Parse(string[] args)
        {
            _options = CommandLineParser.Parse<ParleyCommandLineOptions>(args, HandleCustomFlag, ".dlg");
            CommandLineParser.ResolveModuleName(_options);
            return _options;
        }

        private static int HandleCustomFlag(string flag, string[] args, int currentIndex, CommandLineOptions options)
        {
            var parleyOptions = (ParleyCommandLineOptions)options;

            switch (flag.ToLowerInvariant())
            {
                case "--screenplay":
                    parleyOptions.ExportScreenplay = true;
                    return 0;

                case "--output":
                case "-o":
                    if (currentIndex + 1 < args.Length)
                    {
                        parleyOptions.OutputFile = args[currentIndex + 1];
                        return 1;
                    }
                    return 0;

                default:
                    return 0;
            }
        }

        /// <summary>
        /// Print help text to console
        /// Note: Program.cs attaches to parent console on Windows before calling this
        /// </summary>
        public static void PrintHelp()
        {
            Console.WriteLine(@"Parley - NWN Dialog Editor

Usage: Parley [options] [file.dlg]

Options:
  -h, --help              Show this help message
  -s, --safemode          Start in SafeMode (reset theme/fonts, clear caches)
  -m, --mod <name>        Set module context (resolves relative --file paths)
  --screenplay            Export dialog as screenplay text and exit
  -o, --output FILE       Output file for screenplay (default: stdout)

Examples:
  Parley dialog.dlg                        Open dialog.dlg in editor
  Parley -m LNS --file conv_smith.dlg      Open LNS/conv_smith.dlg
  Parley --safemode                        Start in SafeMode with default settings
  Parley --screenplay test.dlg             Export dialog as screenplay
  Parley --screenplay -o out.txt dialog.dlg   Export to file
");
        }

        /// <summary>
        /// Export dialog as screenplay format
        /// Adapted from TreeNavigationManager.CaptureTreeStructure
        /// </summary>
        public static async Task<int> ExportScreenplayAsync(string filePath, string? outputFile)
        {
            // Suppress logging for CLI mode - only show errors
            UnifiedLogger.SetLogLevel(LogLevel.ERROR);

            try
            {
                if (!File.Exists(filePath))
                {
                    Console.Error.WriteLine($"Error: File not found: {filePath}");
                    return 1;
                }

                // Load dialog
                var dialogService = new DialogFileService();
                var dialog = await dialogService.LoadFromFileAsync(filePath);

                if (dialog == null)
                {
                    Console.Error.WriteLine($"Error: Failed to load dialog: {filePath}");
                    return 1;
                }

                // Link pointers
                LinkDialogPointers(dialog);

                // Generate screenplay
                var screenplay = GenerateScreenplay(dialog, filePath);

                // Output
                if (!string.IsNullOrEmpty(outputFile))
                {
                    await File.WriteAllTextAsync(outputFile, screenplay);
                    Console.Error.WriteLine($"Screenplay exported to: {outputFile}");
                }
                else
                {
                    Console.WriteLine(screenplay);
                }

                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                return 1;
            }
        }

        /// <summary>
        /// Link dialog pointers to their target nodes
        /// </summary>
        private static void LinkDialogPointers(Dialog dialog)
        {
            // Link start pointers
            foreach (var start in dialog.Starts)
            {
                if (start.Index < dialog.Entries.Count)
                {
                    start.Node = dialog.Entries[(int)start.Index];
                    start.Type = DialogNodeType.Entry;
                }
            }

            // Link entry pointers to replies
            foreach (var entry in dialog.Entries)
            {
                foreach (var pointer in entry.Pointers)
                {
                    if (pointer.Index < dialog.Replies.Count)
                    {
                        pointer.Node = dialog.Replies[(int)pointer.Index];
                        pointer.Type = DialogNodeType.Reply;
                    }
                }
            }

            // Link reply pointers to entries
            foreach (var reply in dialog.Replies)
            {
                foreach (var pointer in reply.Pointers)
                {
                    if (pointer.Index < dialog.Entries.Count)
                    {
                        pointer.Node = dialog.Entries[(int)pointer.Index];
                        pointer.Type = DialogNodeType.Entry;
                    }
                }
            }
        }

        /// <summary>
        /// Generate screenplay format from dialog.
        /// Public for reuse from GUI (Copy Tree Structure command).
        /// </summary>
        public static string GenerateScreenplay(Dialog dialog, string? filePath = null)
        {
            var sb = new System.Text.StringBuilder();
            var visited = new System.Collections.Generic.HashSet<DialogNode>();

            if (!string.IsNullOrEmpty(filePath))
            {
                sb.AppendLine($"# {Path.GetFileNameWithoutExtension(filePath)}");
                sb.AppendLine();
            }

            // Process each starting entry
            foreach (var start in dialog.Starts)
            {
                if (start.Node != null)
                {
                    GenerateNodeScreenplay(start.Node, sb, 0, visited);
                    sb.AppendLine();
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Generate screenplay for a single node and its children
        /// </summary>
        private static void GenerateNodeScreenplay(
            DialogNode node,
            System.Text.StringBuilder sb,
            int depth,
            System.Collections.Generic.HashSet<DialogNode> visited)
        {
            if (node == null) return;

            // Prevent infinite loops
            if (visited.Contains(node))
            {
                var indent = new string(' ', depth * 2);
                sb.AppendLine($"{indent}[LINK TO: {GetNodeDisplayText(node)}]");
                return;
            }
            visited.Add(node);

            var indentation = new string(' ', depth * 2);
            var speaker = GetSpeaker(node);
            var text = GetNodeDisplayText(node);

            // Format: SPEAKER: Text
            if (!string.IsNullOrWhiteSpace(text))
            {
                sb.AppendLine($"{indentation}{speaker}: {text}");
            }

            // Process children
            foreach (var ptr in node.Pointers)
            {
                if (ptr.Node != null)
                {
                    GenerateNodeScreenplay(ptr.Node, sb, depth + 1, visited);
                }
            }

            // Remove from visited to allow same node in different branches
            visited.Remove(node);
        }

        /// <summary>
        /// Get speaker name for a node
        /// </summary>
        private static string GetSpeaker(DialogNode node)
        {
            if (node.Type == DialogNodeType.Reply)
            {
                return "PC";
            }

            // NPC Entry - use Speaker tag if available
            if (!string.IsNullOrWhiteSpace(node.Speaker))
            {
                return node.Speaker.ToUpperInvariant();
            }

            return "NPC";
        }

        /// <summary>
        /// Get display text for a node
        /// </summary>
        private static string GetNodeDisplayText(DialogNode node)
        {
            var text = node.Text?.GetDefault();
            if (string.IsNullOrWhiteSpace(text))
            {
                return "[CONTINUE]";
            }
            return text.Replace("\n", " ").Replace("\r", "");
        }
    }
}
