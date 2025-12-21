using System;
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
        /// Record a completed path for a dialog file.
        /// Path signature format: "0→2→5→7" (node indices visited)
        /// </summary>
        public void RecordPath(string filePath, string pathSignature)
        {
            if (string.IsNullOrEmpty(filePath) || string.IsNullOrEmpty(pathSignature))
                return;

            var sanitizedPath = SanitizePath(filePath);

            if (!_coverageData.Files.TryGetValue(sanitizedPath, out var fileData))
            {
                fileData = new FileCoverageData();
                _coverageData.Files[sanitizedPath] = fileData;
            }

            if (fileData.VisitedPaths.Add(pathSignature))
            {
                fileData.LastUpdated = DateTime.UtcNow;
                SaveCoverageData();

                UnifiedLogger.LogApplication(LogLevel.DEBUG,
                    $"Coverage: Recorded path {pathSignature} for {sanitizedPath}");

                CoverageChanged?.Invoke(this, new CoverageChangedEventArgs(sanitizedPath));
            }
        }

        /// <summary>
        /// Get coverage statistics for a dialog file.
        /// </summary>
        public CoverageStats GetCoverageStats(string filePath, int totalPaths)
        {
            var sanitizedPath = SanitizePath(filePath);

            if (!_coverageData.Files.TryGetValue(sanitizedPath, out var fileData))
            {
                return new CoverageStats(0, totalPaths);
            }

            return new CoverageStats(fileData.VisitedPaths.Count, totalPaths);
        }

        /// <summary>
        /// Get all visited path signatures for a dialog file.
        /// </summary>
        public HashSet<string> GetVisitedPaths(string filePath)
        {
            var sanitizedPath = SanitizePath(filePath);

            if (_coverageData.Files.TryGetValue(sanitizedPath, out var fileData))
            {
                return new HashSet<string>(fileData.VisitedPaths);
            }

            return new HashSet<string>();
        }

        /// <summary>
        /// Check if a specific path has been visited.
        /// </summary>
        public bool IsPathVisited(string filePath, string pathSignature)
        {
            var sanitizedPath = SanitizePath(filePath);

            if (_coverageData.Files.TryGetValue(sanitizedPath, out var fileData))
            {
                return fileData.VisitedPaths.Contains(pathSignature);
            }

            return false;
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
        public HashSet<string> VisitedPaths { get; set; } = new();
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Coverage statistics for display.
    /// </summary>
    public class CoverageStats
    {
        public int VisitedPaths { get; }
        public int TotalPaths { get; }
        public double Percentage => TotalPaths > 0 ? (double)VisitedPaths / TotalPaths * 100 : 0;

        public CoverageStats(int visited, int total)
        {
            VisitedPaths = visited;
            TotalPaths = total;
        }

        public string DisplayText => TotalPaths > 0
            ? $"{VisitedPaths}/{TotalPaths} paths ({Percentage:F0}%)"
            : "0 paths";
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
