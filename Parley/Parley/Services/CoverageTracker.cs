using System;
using Radoub.Formats.Logging;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DialogEditor.Services
{
    /// <summary>
    /// Tracks conversation path coverage for the Conversation Simulator.
    /// Persists coverage data per dialog file, similar to ScrapManager pattern.
    /// Issue #478 - Conversation Simulator Sprint 1
    /// </summary>
    public class CoverageTracker
    {
        private static CoverageTracker? _instance;
        public static CoverageTracker Instance => _instance ??= new CoverageTracker();

        private readonly string _coverageFilePath;
        private CoverageData _coverageData;
        private readonly JsonSerializerOptions _jsonOptions;

        public event EventHandler<CoverageChangedEventArgs>? CoverageChanged;

        private CoverageTracker()
        {
            var cachePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Radoub", "Parley", "Cache"
            );
            Directory.CreateDirectory(cachePath);
            _coverageFilePath = Path.Combine(cachePath, "coverage.json");

            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            _coverageData = LoadCoverageData();
            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"CoverageTracker initialized with {_coverageData.Files.Count} tracked files");
        }

        /// <summary>
        /// Record a visited node for a dialog file.
        /// Node key format: "E{index}" for entries, "R{index}" for replies.
        /// </summary>
        public void RecordVisitedNode(string filePath, string nodeKey)
        {
            if (string.IsNullOrEmpty(filePath) || string.IsNullOrEmpty(nodeKey))
                return;

            var sanitizedPath = SanitizePath(filePath);

            if (!_coverageData.Files.TryGetValue(sanitizedPath, out var fileData))
            {
                fileData = new FileCoverageData();
                _coverageData.Files[sanitizedPath] = fileData;
            }

            if (fileData.VisitedNodes.Add(nodeKey))
            {
                fileData.LastUpdated = DateTime.UtcNow;
                SaveCoverageData();

                UnifiedLogger.LogApplication(LogLevel.DEBUG,
                    $"Coverage: Recorded node {nodeKey} for {sanitizedPath}");

                CoverageChanged?.Invoke(this, new CoverageChangedEventArgs(sanitizedPath));
            }
        }

        /// <summary>
        /// Get coverage statistics for a dialog file.
        /// </summary>
        /// <param name="filePath">Path to the dialog file</param>
        /// <param name="totalReplies">Total number of reply nodes in the dialog</param>
        /// <param name="rootEntryIndices">Entry indices that are root entries (conversation starters)</param>
        /// <param name="repliesPerRootEntry">Mapping of root entry index to set of reply indices under that entry</param>
        public CoverageStats GetCoverageStats(
            string filePath,
            int totalReplies = 0,
            IReadOnlyList<int>? rootEntryIndices = null,
            IReadOnlyDictionary<int, HashSet<int>>? repliesPerRootEntry = null)
        {
            var sanitizedPath = SanitizePath(filePath);

            if (!_coverageData.Files.TryGetValue(sanitizedPath, out var fileData))
            {
                return new CoverageStats(0, 0, totalReplies, 0, rootEntryIndices?.Count ?? 0, new Dictionary<int, (int visited, int total)>());
            }

            // Count visited replies (keys starting with "R")
            var visitedReplies = fileData.VisitedNodes.Count(k => k.StartsWith("R"));

            // Count visited root entries and per-entry coverage
            var visitedRootEntries = 0;
            var totalRootEntries = rootEntryIndices?.Count ?? 0;
            var perEntryCoverage = new Dictionary<int, (int visited, int total)>();

            if (rootEntryIndices != null)
            {
                foreach (var entryIndex in rootEntryIndices)
                {
                    var key = $"E{entryIndex}";
                    if (fileData.VisitedNodes.Contains(key))
                    {
                        visitedRootEntries++;
                    }

                    // Calculate per-entry reply coverage
                    if (repliesPerRootEntry != null && repliesPerRootEntry.TryGetValue(entryIndex, out var replyIndices))
                    {
                        var visitedCount = replyIndices.Count(ri => fileData.VisitedNodes.Contains($"R{ri}"));
                        perEntryCoverage[entryIndex] = (visitedCount, replyIndices.Count);
                    }
                }
            }

            return new CoverageStats(fileData.VisitedNodes.Count, visitedReplies, totalReplies, visitedRootEntries, totalRootEntries, perEntryCoverage);
        }

        /// <summary>
        /// Check if a specific node has been visited.
        /// </summary>
        public bool IsNodeVisited(string filePath, string nodeKey)
        {
            var sanitizedPath = SanitizePath(filePath);

            if (_coverageData.Files.TryGetValue(sanitizedPath, out var fileData))
            {
                return fileData.VisitedNodes.Contains(nodeKey);
            }

            return false;
        }

        /// <summary>
        /// Get all visited node keys for a dialog file.
        /// </summary>
        public HashSet<string> GetVisitedNodes(string filePath)
        {
            var sanitizedPath = SanitizePath(filePath);

            if (_coverageData.Files.TryGetValue(sanitizedPath, out var fileData))
            {
                return new HashSet<string>(fileData.VisitedNodes);
            }

            return new HashSet<string>();
        }

        /// <summary>
        /// Clear all coverage data for a specific dialog file.
        /// </summary>
        public void ClearCoverage(string filePath)
        {
            var sanitizedPath = SanitizePath(filePath);

            if (_coverageData.Files.Remove(sanitizedPath))
            {
                SaveCoverageData();
                UnifiedLogger.LogApplication(LogLevel.INFO,
                    $"Coverage: Cleared coverage for {sanitizedPath}");

                CoverageChanged?.Invoke(this, new CoverageChangedEventArgs(sanitizedPath));
            }
        }

        /// <summary>
        /// Clear all coverage data.
        /// </summary>
        public void ClearAllCoverage()
        {
            var count = _coverageData.Files.Count;
            _coverageData.Files.Clear();
            SaveCoverageData();

            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"Coverage: Cleared all coverage data ({count} files)");
        }

        private string SanitizePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return "";

            var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (path.StartsWith(homeDir, StringComparison.OrdinalIgnoreCase))
            {
                path = "~" + path.Substring(homeDir.Length);
            }

            return path.Replace('\\', '/');
        }

        private CoverageData LoadCoverageData()
        {
            try
            {
                if (File.Exists(_coverageFilePath))
                {
                    var json = File.ReadAllText(_coverageFilePath);
                    var data = JsonSerializer.Deserialize<CoverageData>(json, _jsonOptions);
                    if (data != null)
                    {
                        return data;
                    }
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR,
                    $"Failed to load coverage data: {ex.Message}");
            }

            return new CoverageData();
        }

        private void SaveCoverageData()
        {
            try
            {
                // Clean up old entries (older than 90 days)
                var cutoffDate = DateTime.UtcNow.AddDays(-90);
                var keysToRemove = _coverageData.Files
                    .Where(kvp => kvp.Value.LastUpdated < cutoffDate)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in keysToRemove)
                {
                    _coverageData.Files.Remove(key);
                }

                var json = JsonSerializer.Serialize(_coverageData, _jsonOptions);
                File.WriteAllText(_coverageFilePath, json);
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR,
                    $"Failed to save coverage data: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Container for all coverage data.
    /// </summary>
    public class CoverageData
    {
        public Dictionary<string, FileCoverageData> Files { get; set; } = new();
        public int Version { get; set; } = 1;
    }

    /// <summary>
    /// Coverage data for a single dialog file.
    /// </summary>
    public class FileCoverageData
    {
        /// <summary>
        /// Set of visited node keys in format "E{index}" for entries, "R{index}" for replies.
        /// Tracks which individual nodes have been visited during simulation.
        /// </summary>
        public HashSet<string> VisitedNodes { get; set; } = new();

        /// <summary>
        /// Legacy: complete path signatures. Kept for backwards compatibility.
        /// </summary>
        public HashSet<string> VisitedPaths { get; set; } = new();

        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Coverage statistics for display.
    /// </summary>
    public class CoverageStats
    {
        public int VisitedNodes { get; }
        public int VisitedReplies { get; }
        public int TotalReplies { get; }
        public int VisitedRootEntries { get; }
        public int TotalRootEntries { get; }

        /// <summary>
        /// Per-entry coverage: entryIndex -> (visited replies, total replies under that entry)
        /// </summary>
        public IReadOnlyDictionary<int, (int visited, int total)> PerEntryCoverage { get; }

        public CoverageStats(
            int visitedNodes,
            int visitedReplies,
            int totalReplies,
            int visitedRootEntries,
            int totalRootEntries,
            IReadOnlyDictionary<int, (int visited, int total)> perEntryCoverage)
        {
            VisitedNodes = visitedNodes;
            VisitedReplies = visitedReplies;
            TotalReplies = totalReplies;
            VisitedRootEntries = visitedRootEntries;
            TotalRootEntries = totalRootEntries;
            PerEntryCoverage = perEntryCoverage;
        }

        public bool IsComplete => TotalReplies > 0 && VisitedReplies >= TotalReplies;
        public bool RootEntriesComplete => TotalRootEntries > 0 && VisitedRootEntries >= TotalRootEntries;

        /// <summary>
        /// Check if a specific root entry has full coverage.
        /// </summary>
        public bool IsEntryComplete(int entryIndex)
        {
            if (PerEntryCoverage.TryGetValue(entryIndex, out var coverage))
            {
                return coverage.total > 0 && coverage.visited >= coverage.total;
            }
            return false;
        }

        /// <summary>
        /// Get coverage text for a specific entry (e.g., "2/3 replies")
        /// </summary>
        public string GetEntryCoverageText(int entryIndex)
        {
            if (PerEntryCoverage.TryGetValue(entryIndex, out var coverage))
            {
                return $"{coverage.visited}/{coverage.total}";
            }
            return "0/0";
        }

        public string DisplayText
        {
            get
            {
                var parts = new List<string>();

                // Root entries (primary metric)
                if (TotalRootEntries > 0)
                {
                    parts.Add($"{VisitedRootEntries}/{TotalRootEntries} starts");
                }

                // Replies (secondary metric)
                if (TotalReplies > 0)
                {
                    parts.Add($"{VisitedReplies}/{TotalReplies} replies");
                }

                if (parts.Count == 0)
                {
                    return VisitedNodes == 1
                        ? "1 node visited"
                        : $"{VisitedNodes} nodes visited";
                }

                var result = string.Join(", ", parts);
                if (IsComplete && RootEntriesComplete)
                {
                    result += " - Complete!";
                }

                return result;
            }
        }
    }

    /// <summary>
    /// Event args for coverage changes.
    /// </summary>
    public class CoverageChangedEventArgs : EventArgs
    {
        public string FilePath { get; }

        public CoverageChangedEventArgs(string filePath)
        {
            FilePath = filePath;
        }
    }
}
