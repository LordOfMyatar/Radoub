using System;
using System.IO;
using System.Threading.Tasks;
using DialogEditor.Models;
using DialogEditor.Services;

namespace Parley.Services
{
    /// <summary>
    /// Command line options for Parley
    /// </summary>
    public class CommandLineOptions
    {
        /// <summary>
        /// DLG file path to open on startup
        /// </summary>
        public string? FilePath { get; set; }

        /// <summary>
        /// Safe mode - disable themes and plugins
        /// </summary>
        public bool SafeMode { get; set; }

        /// <summary>
        /// Export screenplay to stdout and exit
        /// </summary>
        public bool ExportScreenplay { get; set; }

        /// <summary>
        /// Show help and exit
        /// </summary>
        public bool ShowHelp { get; set; }

        /// <summary>
        /// Output file for screenplay export (optional, defaults to stdout)
        /// </summary>
        public string? OutputFile { get; set; }
    }

    /// <summary>
    /// Service for handling command line arguments
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
                else if (arg == "--safe-mode" || arg == "-s")
                {
                    options.SafeMode = true;
                }
                else if (arg == "--screenplay")
                {
                    options.ExportScreenplay = true;
                }
                else if ((arg == "--output" || arg == "-o") && i + 1 < args.Length)
                {
                    options.OutputFile = args[++i];
                }
                else if (!arg.StartsWith("-") && arg.EndsWith(".dlg", StringComparison.OrdinalIgnoreCase))
                {
                    options.FilePath = arg;
                }
                else if (!arg.StartsWith("-") && File.Exists(arg))
                {
                    // Also accept file paths that exist even without .dlg extension
                    options.FilePath = arg;
                }
            }

            _options = options;
            return options;
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
  -h, --help          Show this help message
  -s, --safe-mode     Start in safe mode (no themes, no plugins)
  --screenplay        Export dialog as screenplay text and exit
  -o, --output FILE   Output file for screenplay (default: stdout)

Examples:
  Parley dialog.dlg           Open dialog.dlg in editor
  Parley --safe-mode          Start editor in safe mode
  Parley --screenplay test.dlg    Export dialog as screenplay
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
            DialogEditor.Services.UnifiedLogger.SetLogLevel(DialogEditor.Services.LogLevel.ERROR);

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
        /// Generate screenplay format from dialog
        /// </summary>
        private static string GenerateScreenplay(Dialog dialog, string filePath)
        {
            var sb = new System.Text.StringBuilder();
            var visited = new System.Collections.Generic.HashSet<DialogNode>();

            sb.AppendLine($"# {Path.GetFileNameWithoutExtension(filePath)}");
            sb.AppendLine();

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
