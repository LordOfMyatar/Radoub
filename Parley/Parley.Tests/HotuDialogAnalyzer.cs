using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using DialogEditor.Services;
using DialogEditor.Models;

namespace Parley.Tests
{
    /// <summary>
    /// Analyzes official BioWare HOTU Chapter 2 dialog files to determine professional dialog depths
    /// This provides empirical data about actual dialog tree structures from official content
    /// </summary>
    public class HotuDialogAnalyzer
    {
        private readonly ITestOutputHelper _output;
        private readonly DialogFileService _dialogService;

        public HotuDialogAnalyzer(ITestOutputHelper output)
        {
            _output = output;
            _dialogService = new DialogFileService();
        }

        [Fact]
        public async Task AnalyzeHotuChapter2Dialogs()
        {
            var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var hotuPath = Path.Combine(homeDir, "Documents", "Neverwinter Nights", "modules", "XP2_Chapter2");

            // Select key dialogs - major NPCs and complex conversations
            var keyDialogs = new[]
            {
                "x2_hen_deekin.dlg",      // Deekin companion
                "x2_hen_nathyrra.dlg",    // Nathyrra companion
                "x2_hen_valen.dlg",       // Valen companion
                "q2aherald.dlg",          // Herald NPC
                "q2aseer.dlg",            // Seer of the Reliquary
                "q2amaeviir.dlg",         // Matron Mae'viir
                "q2azesyyr.dlg",          // Zesyyr
                "q2delderbrain.dlg",      // Elder Brain
                "q3c_planetar.dlg",       // Planetar
                "q2a_merchant.dlg"        // Merchant
            };

            var depthStats = new List<(string filename, int maxDepth, int nodeCount, int branchingFactor)>();
            var missingFiles = new List<string>();

            foreach (var dialogName in keyDialogs)
            {
                var filePath = Path.Combine(hotuPath, dialogName);
                if (!File.Exists(filePath))
                {
                    // Try without x2_ prefix
                    filePath = Path.Combine(hotuPath, dialogName.Replace("x2_", ""));
                    if (!File.Exists(filePath))
                    {
                        missingFiles.Add(dialogName);
                        continue;
                    }
                }

                try
                {
                    var dialog = await _dialogService.LoadFromFileAsync(filePath);
                    if (dialog != null)
                    {
                        var maxDepth = CalculateMaxDepth(dialog);
                        var nodeCount = dialog.Entries.Count + dialog.Replies.Count;
                        var branchingFactor = CalculateMaxBranchingFactor(dialog);

                        depthStats.Add((dialogName, maxDepth, nodeCount, branchingFactor));
                        _output.WriteLine($"{dialogName,-25} Depth: {maxDepth,3} | Nodes: {nodeCount,4} | Max Branching: {branchingFactor}");
                    }
                }
                catch (Exception ex)
                {
                    _output.WriteLine($"Error loading {dialogName}: {ex.Message}");
                }
            }

            // Also analyze all dialogs to get overall statistics
            _output.WriteLine("\n=== Analyzing ALL Chapter 2 Dialogs ===");
            var allFiles = Directory.GetFiles(hotuPath, "*.dlg");
            var allDepths = new List<int>();
            var allNodeCounts = new List<int>();

            foreach (var file in allFiles.Take(50)) // Sample first 50 for performance
            {
                try
                {
                    var dialog = await _dialogService.LoadFromFileAsync(file);
                    if (dialog != null)
                    {
                        var depth = CalculateMaxDepth(dialog);
                        var nodeCount = dialog.Entries.Count + dialog.Replies.Count;
                        allDepths.Add(depth);
                        allNodeCounts.Add(nodeCount);
                    }
                }
                catch
                {
                    // Skip files with errors
                }
            }

            if (allDepths.Any())
            {
                _output.WriteLine($"\n=== Overall Statistics (Sample of {allDepths.Count} dialogs) ===");
                _output.WriteLine($"Average Depth: {allDepths.Average():F1}");
                _output.WriteLine($"Maximum Depth: {allDepths.Max()}");
                _output.WriteLine($"Minimum Depth: {allDepths.Min()}");
                _output.WriteLine($"90th Percentile Depth: {allDepths.OrderBy(d => d).Skip((int)(allDepths.Count * 0.9)).First()}");

                _output.WriteLine($"\n=== Depth Distribution ===");
                _output.WriteLine($"1-5 levels: {allDepths.Count(d => d <= 5)} ({100.0 * allDepths.Count(d => d <= 5) / allDepths.Count:F1}%)");
                _output.WriteLine($"6-10 levels: {allDepths.Count(d => d > 5 && d <= 10)} ({100.0 * allDepths.Count(d => d > 5 && d <= 10) / allDepths.Count:F1}%)");
                _output.WriteLine($"11-15 levels: {allDepths.Count(d => d > 10 && d <= 15)} ({100.0 * allDepths.Count(d => d > 10 && d <= 15) / allDepths.Count:F1}%)");
                _output.WriteLine($"16-20 levels: {allDepths.Count(d => d > 15 && d <= 20)} ({100.0 * allDepths.Count(d => d > 15 && d <= 20) / allDepths.Count:F1}%)");
                _output.WriteLine($">20 levels: {allDepths.Count(d => d > 20)} ({100.0 * allDepths.Count(d => d > 20) / allDepths.Count:F1}%)");

                _output.WriteLine($"\n=== Node Count Distribution ===");
                _output.WriteLine($"Average Nodes per Dialog: {allNodeCounts.Average():F0}");
                _output.WriteLine($"Maximum Nodes: {allNodeCounts.Max()}");
                _output.WriteLine($"Dialogs with >100 nodes: {allNodeCounts.Count(n => n > 100)}");
                _output.WriteLine($"Dialogs with >500 nodes: {allNodeCounts.Count(n => n > 500)}");
            }

            if (missingFiles.Any())
            {
                _output.WriteLine($"\n=== Files not found (may be in different chapters) ===");
                foreach (var missing in missingFiles)
                {
                    _output.WriteLine($"  - {missing}");
                }
            }
        }

        private int CalculateMaxDepth(Dialog dialog)
        {
            if (!dialog.Starts.Any())
                return 0;

            int maxDepth = 0;
            var visited = new HashSet<DialogNode>();

            foreach (var start in dialog.Starts)
            {
                if (start.Node != null)
                {
                    int depth = CalculateNodeDepth(start.Node, visited, 1);
                    maxDepth = Math.Max(maxDepth, depth);
                }
            }

            return maxDepth;
        }

        private int CalculateNodeDepth(DialogNode node, HashSet<DialogNode> visited, int currentDepth)
        {
            if (visited.Contains(node))
                return currentDepth;

            visited.Add(node);
            int maxChildDepth = currentDepth;

            foreach (var ptr in node.Pointers)
            {
                if (ptr.Node != null)
                {
                    int childDepth = CalculateNodeDepth(ptr.Node, visited, currentDepth + 1);
                    maxChildDepth = Math.Max(maxChildDepth, childDepth);
                }
            }

            visited.Remove(node);
            return maxChildDepth;
        }

        private int CalculateMaxBranchingFactor(Dialog dialog)
        {
            int maxBranching = 0;

            foreach (var entry in dialog.Entries)
            {
                maxBranching = Math.Max(maxBranching, entry.Pointers.Count);
            }

            foreach (var reply in dialog.Replies)
            {
                maxBranching = Math.Max(maxBranching, reply.Pointers.Count);
            }

            return maxBranching;
        }
    }
}