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
    /// Analyzes real-world dialog files to determine typical conversation depths
    /// This provides empirical data about actual dialog tree structures in NWN
    /// </summary>
    public class DialogDepthAnalyzer
    {
        private readonly ITestOutputHelper _output;
        private readonly DialogFileService _dialogService;
        private readonly string _testFilesPath;

        public DialogDepthAnalyzer(ITestOutputHelper output)
        {
            _output = output;
            _dialogService = new DialogFileService();
            _testFilesPath = Path.Combine(Directory.GetCurrentDirectory(),
                "..", "..", "..", "..", "TestingTools", "TestFiles");
        }

        [Fact]
        public async Task AnalyzeRealWorldDialogDepths()
        {
            var testFiles = Directory.GetFiles(_testFilesPath, "*.dlg")
                .Where(f => !f.Contains("_exported") && !f.Contains("_fixed") && !f.Contains("test") && !f.Contains("deep"))
                .Take(10); // Analyze first 10 non-test files (excluding stress test files)

            var depthStats = new List<(string filename, int maxDepth, int nodeCount, int entryCount, int replyCount)>();

            foreach (var file in testFiles)
            {
                try
                {
                    var dialog = await _dialogService.LoadFromFileAsync(file);
                    if (dialog != null)
                    {
                        var maxDepth = CalculateMaxDepth(dialog);
                        var filename = Path.GetFileName(file);
                        var nodeCount = dialog.Entries.Count + dialog.Replies.Count;

                        depthStats.Add((filename, maxDepth, nodeCount, dialog.Entries.Count, dialog.Replies.Count));
                        _output.WriteLine($"{filename}: Max Depth={maxDepth}, Total Nodes={nodeCount}, Entries={dialog.Entries.Count}, Replies={dialog.Replies.Count}");
                    }
                }
                catch (Exception ex)
                {
                    _output.WriteLine($"Error analyzing {Path.GetFileName(file)}: {ex.Message}");
                }
            }

            // Calculate statistics
            if (depthStats.Any())
            {
                var avgDepth = depthStats.Average(s => s.maxDepth);
                var maxDepth = depthStats.Max(s => s.maxDepth);
                var minDepth = depthStats.Min(s => s.maxDepth);
                var avgNodes = depthStats.Average(s => s.nodeCount);

                _output.WriteLine("\n=== Statistics ===");
                _output.WriteLine($"Files Analyzed: {depthStats.Count}");
                _output.WriteLine($"Average Max Depth: {avgDepth:F1}");
                _output.WriteLine($"Maximum Depth Found: {maxDepth}");
                _output.WriteLine($"Minimum Depth Found: {minDepth}");
                _output.WriteLine($"Average Node Count: {avgNodes:F0}");

                // Group by depth ranges
                _output.WriteLine("\n=== Depth Distribution ===");
                _output.WriteLine($"1-5 levels: {depthStats.Count(s => s.maxDepth <= 5)} files");
                _output.WriteLine($"6-10 levels: {depthStats.Count(s => s.maxDepth > 5 && s.maxDepth <= 10)} files");
                _output.WriteLine($"11-15 levels: {depthStats.Count(s => s.maxDepth > 10 && s.maxDepth <= 15)} files");
                _output.WriteLine($"16-20 levels: {depthStats.Count(s => s.maxDepth > 15 && s.maxDepth <= 20)} files");
                _output.WriteLine($">20 levels: {depthStats.Count(s => s.maxDepth > 20)} files");

                // Verify our assumption about typical depths
                Assert.True(maxDepth <= 30, $"Found dialog with depth {maxDepth} - much deeper than expected!");
                Assert.True(avgDepth <= 15, $"Average depth {avgDepth} is higher than typical expected range");
            }
        }

        private int CalculateMaxDepth(Dialog dialog)
        {
            int maxDepth = 0;
            var visited = new HashSet<DialogNode>();

            // Start from each starting node
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
            // Avoid infinite recursion on cycles
            if (visited.Contains(node))
                return currentDepth;

            visited.Add(node);
            int maxChildDepth = currentDepth;

            // Check all child pointers
            foreach (var ptr in node.Pointers)
            {
                if (ptr.Node != null)
                {
                    int childDepth = CalculateNodeDepth(ptr.Node, visited, currentDepth + 1);
                    maxChildDepth = Math.Max(maxChildDepth, childDepth);
                }
            }

            visited.Remove(node); // Allow node to be visited via different path
            return maxChildDepth;
        }

        [Theory]
        [InlineData("chef.dlg")]
        [InlineData("lista.dlg")]
        [InlineData("convolutedconvo.dlg")]
        public async Task AnalyzeSpecificDialog(string filename)
        {
            var filePath = Path.Combine(_testFilesPath, filename);
            if (!File.Exists(filePath))
            {
                _output.WriteLine($"File not found: {filename}");
                return;
            }

            var dialog = await _dialogService.LoadFromFileAsync(filePath);
            if (dialog != null)
            {
                var maxDepth = CalculateMaxDepth(dialog);
                var branchingFactor = CalculateAverageBranchingFactor(dialog);

                _output.WriteLine($"\n=== Analysis of {filename} ===");
                _output.WriteLine($"Max Depth: {maxDepth}");
                _output.WriteLine($"Total Entries: {dialog.Entries.Count}");
                _output.WriteLine($"Total Replies: {dialog.Replies.Count}");
                _output.WriteLine($"Starting Points: {dialog.Starts.Count}");
                _output.WriteLine($"Average Branching Factor: {branchingFactor:F2}");

                // Check for shared nodes (nodes with multiple incoming links)
                var sharedNodes = CountSharedNodes(dialog);
                _output.WriteLine($"Shared Nodes: {sharedNodes}");
            }
        }

        private double CalculateAverageBranchingFactor(Dialog dialog)
        {
            var totalPointers = 0;
            var nodesWithPointers = 0;

            foreach (var entry in dialog.Entries)
            {
                if (entry.Pointers.Any())
                {
                    totalPointers += entry.Pointers.Count;
                    nodesWithPointers++;
                }
            }

            foreach (var reply in dialog.Replies)
            {
                if (reply.Pointers.Any())
                {
                    totalPointers += reply.Pointers.Count;
                    nodesWithPointers++;
                }
            }

            return nodesWithPointers > 0 ? (double)totalPointers / nodesWithPointers : 0;
        }

        private int CountSharedNodes(Dialog dialog)
        {
            dialog.LinkRegistry.RebuildFromDialog(dialog);
            int sharedCount = 0;

            foreach (var entry in dialog.Entries)
            {
                var links = dialog.LinkRegistry.GetLinksTo(entry);
                if (links.Count > 1)
                    sharedCount++;
            }

            foreach (var reply in dialog.Replies)
            {
                var links = dialog.LinkRegistry.GetLinksTo(reply);
                if (links.Count > 1)
                    sharedCount++;
            }

            return sharedCount;
        }
    }
}