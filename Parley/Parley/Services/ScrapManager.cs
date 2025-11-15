using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using DialogEditor.Models;
using DialogEditor.Services;

namespace Parley.Services
{
    /// <summary>
    /// Manages the Scrap Tab functionality - storing deleted/cut nodes in user preferences
    /// rather than polluting the DLG file with orphan containers
    /// </summary>
    public class ScrapManager
    {
        private readonly string _scrapFilePath;
        private ScrapData _scrapData;
        private readonly JsonSerializerOptions _jsonOptions;

        public ObservableCollection<ScrapEntry> ScrapEntries { get; }
        public event EventHandler<int>? ScrapCountChanged;

        public ScrapManager()
        {
            // Store in user's ~/Parley folder (same as other settings)
            var parleyPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Parley"
            );
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"ScrapManager: Parley folder = {parleyPath}");

            Directory.CreateDirectory(parleyPath);
            _scrapFilePath = Path.Combine(parleyPath, "scrap.json");
            UnifiedLogger.LogApplication(LogLevel.INFO, $"ScrapManager: Scrap file path = {_scrapFilePath}");

            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            ScrapEntries = new ObservableCollection<ScrapEntry>();
            _scrapData = LoadScrapData();
            UpdateScrapEntries();
            UnifiedLogger.LogApplication(LogLevel.INFO, $"ScrapManager initialized with {ScrapEntries.Count} entries");
        }

        /// <summary>
        /// Add nodes to the scrap for a specific file
        /// </summary>
        public void AddToScrap(string filePath, List<DialogNode> nodes, string operation = "deleted",
            Dictionary<DialogNode, (int level, DialogNode? parent)>? hierarchyInfo = null)
        {
            if (nodes == null || nodes.Count == 0) return;

            var sanitizedPath = SanitizePath(filePath);
            var timestamp = DateTime.UtcNow;

            foreach (var node in nodes)
            {
                var entry = new ScrapEntry
                {
                    Id = Guid.NewGuid().ToString(),
                    FilePath = sanitizedPath,
                    Timestamp = timestamp,
                    Operation = operation,
                    NodeType = node.Type.ToString(),
                    NodeText = GetNodePreviewText(node),
                    Speaker = node.Speaker,
                    OriginalIndex = GetNodeIndex(node),
                    SerializedNode = SerializeNode(node)
                };

                // Add hierarchy information if available
                if (hierarchyInfo != null && hierarchyInfo.TryGetValue(node, out var info))
                {
                    entry.NestingLevel = info.level;
                    entry.ParentNodeText = info.parent != null ? GetNodePreviewText(info.parent) : null;
                }

                _scrapData.Entries.Add(entry);
            }

            SaveScrapData();
            UpdateScrapEntries();
            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"Added {nodes.Count} nodes to scrap from {sanitizedPath} ({operation})");
        }

        /// <summary>
        /// Restore a specific scrap entry back to the dialog
        /// Returns the deserialized node for re-insertion
        /// </summary>
        public DialogNode? RestoreFromScrap(string entryId)
        {
            var entry = _scrapData.Entries.FirstOrDefault(e => e.Id == entryId);
            if (entry == null) return null;

            try
            {
                var node = DeserializeNode(entry.SerializedNode);
                if (node != null)
                {
                    // Remove from scrap after successful restoration
                    _scrapData.Entries.Remove(entry);
                    SaveScrapData();
                    UpdateScrapEntries();

                    UnifiedLogger.LogApplication(LogLevel.INFO,
                        $"Restored node from scrap: {entry.NodeText}");
                }
                return node;
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to restore node from scrap: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Clear all scrap entries for a specific file
        /// </summary>
        public void ClearScrapForFile(string filePath)
        {
            var sanitizedPath = SanitizePath(filePath);
            var removed = _scrapData.Entries.RemoveAll(e => e.FilePath == sanitizedPath);

            if (removed > 0)
            {
                SaveScrapData();
                UpdateScrapEntries();
                UnifiedLogger.LogApplication(LogLevel.INFO,
                    $"Cleared {removed} scrap entries for {sanitizedPath}");
            }
        }

        /// <summary>
        /// Clear all scrap entries
        /// </summary>
        public void ClearAllScrap()
        {
            var count = _scrapData.Entries.Count;
            _scrapData.Entries.Clear();
            SaveScrapData();
            UpdateScrapEntries();
            UnifiedLogger.LogApplication(LogLevel.INFO, $"Cleared all {count} scrap entries");
        }

        /// <summary>
        /// Get scrap entries for the current file
        /// </summary>
        public List<ScrapEntry> GetScrapForFile(string filePath)
        {
            var sanitizedPath = SanitizePath(filePath);
            return _scrapData.Entries
                .Where(e => e.FilePath == sanitizedPath)
                .OrderByDescending(e => e.Timestamp)
                .ToList();
        }

        /// <summary>
        /// Get count of scrap entries for a specific file
        /// </summary>
        public int GetScrapCount(string filePath)
        {
            var sanitizedPath = SanitizePath(filePath);
            return _scrapData.Entries.Count(e => e.FilePath == sanitizedPath);
        }

        private void UpdateScrapEntries()
        {
            ScrapEntries.Clear();
            foreach (var entry in _scrapData.Entries.OrderByDescending(e => e.Timestamp))
            {
                ScrapEntries.Add(entry);
            }
            ScrapCountChanged?.Invoke(this, ScrapEntries.Count);
        }

        private string SanitizePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return "";

            // Replace user home directory with ~
            var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (path.StartsWith(homeDir, StringComparison.OrdinalIgnoreCase))
            {
                path = "~" + path.Substring(homeDir.Length);
            }

            // Normalize path separators
            return path.Replace('\\', '/');
        }

        private string GetNodePreviewText(DialogNode node)
        {
            // Get text from the LocString dictionary
            var text = "";
            if (node.Text != null && node.Text.Strings.Count > 0)
            {
                // Try to get English (0) or first available language
                if (node.Text.Strings.TryGetValue(0, out var englishText))
                {
                    text = englishText;
                }
                else
                {
                    text = node.Text.Strings.Values.FirstOrDefault() ?? "";
                }
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                text = node.Comment ?? "[No text]";
            }

            // Truncate for preview
            if (text.Length > 50)
            {
                text = text.Substring(0, 47) + "...";
            }

            return text;
        }

        private int GetNodeIndex(DialogNode node)
        {
            // This would need access to the current dialog to get the actual index
            // For now, return -1 as unknown
            return -1;
        }

        private string SerializeNode(DialogNode node)
        {
            try
            {
                // Create a simplified version for serialization to avoid circular references
                var simplified = new
                {
                    Type = node.Type.ToString(),
                    Text = node.Text?.Strings.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                    Speaker = node.Speaker,
                    Comment = node.Comment,
                    Sound = node.Sound,
                    ScriptAction = node.ScriptAction,
                    Animation = node.Animation,
                    AnimationLoop = node.AnimationLoop,
                    Delay = node.Delay,
                    Quest = node.Quest,
                    QuestEntry = node.QuestEntry,
                    ActionParams = node.ActionParams,
                    // Don't serialize pointers to avoid circular references
                };

                return JsonSerializer.Serialize(simplified, _jsonOptions);
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to serialize node for scrap: {ex.Message}");
                return "{}";
            }
        }

        private DialogNode? DeserializeNode(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var node = new DialogNode
                {
                    Type = Enum.Parse<DialogNodeType>(root.GetProperty("type").GetString() ?? "Entry"),
                    Speaker = root.TryGetProperty("speaker", out var speaker) ? speaker.GetString() ?? "" : "",
                    Comment = root.TryGetProperty("comment", out var comment) ? comment.GetString() ?? "" : "",
                    Sound = root.TryGetProperty("sound", out var sound) ? sound.GetString() ?? "" : "",
                    ScriptAction = root.TryGetProperty("scriptAction", out var scriptAction) ? scriptAction.GetString() ?? "" : "",
                    Animation = root.TryGetProperty("animation", out var animation) ? (DialogAnimation)animation.GetInt32() : DialogAnimation.None,
                    AnimationLoop = root.TryGetProperty("animationLoop", out var animationLoop) ? animationLoop.GetBoolean() : false,
                    Delay = root.TryGetProperty("delay", out var delay) ? delay.GetUInt32() : 0u,
                    Quest = root.TryGetProperty("quest", out var quest) ? quest.GetString() ?? "" : "",
                    QuestEntry = root.TryGetProperty("questEntry", out var questEntry) ? questEntry.GetUInt32() : 0u,
                    Pointers = new List<DialogPtr>() // Empty pointers for restored node
                };

                // Restore text if present
                if (root.TryGetProperty("text", out var textProp) && textProp.ValueKind == JsonValueKind.Object)
                {
                    node.Text = new LocString();
                    foreach (var kvp in textProp.EnumerateObject())
                    {
                        if (int.TryParse(kvp.Name, out var langId))
                        {
                            node.Text.Strings[langId] = kvp.Value.GetString() ?? "";
                        }
                    }
                }

                // Restore action params if present
                if (root.TryGetProperty("actionParams", out var paramsProp) && paramsProp.ValueKind == JsonValueKind.Object)
                {
                    node.ActionParams = new Dictionary<string, string>();
                    foreach (var kvp in paramsProp.EnumerateObject())
                    {
                        node.ActionParams[kvp.Name] = kvp.Value.GetString() ?? "";
                    }
                }

                return node;
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to deserialize node from scrap: {ex.Message}");
                return null;
            }
        }

        private ScrapData LoadScrapData()
        {
            try
            {
                if (File.Exists(_scrapFilePath))
                {
                    var json = File.ReadAllText(_scrapFilePath);
                    var data = JsonSerializer.Deserialize<ScrapData>(json, _jsonOptions);
                    return data ?? new ScrapData();
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to load scrap data: {ex.Message}");
            }

            return new ScrapData();
        }

        private void SaveScrapData()
        {
            try
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"SaveScrapData: Saving {_scrapData.Entries.Count} entries to {_scrapFilePath}");

                // Clean up old entries (older than 30 days)
                var cutoffDate = DateTime.UtcNow.AddDays(-30);
                var removed = _scrapData.Entries.RemoveAll(e => e.Timestamp < cutoffDate);
                if (removed > 0)
                {
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"SaveScrapData: Removed {removed} old entries");
                }

                var json = JsonSerializer.Serialize(_scrapData, _jsonOptions);
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"SaveScrapData: Serialized {json.Length} characters");

                File.WriteAllText(_scrapFilePath, json);
                UnifiedLogger.LogApplication(LogLevel.INFO, $"SaveScrapData: Successfully saved {_scrapData.Entries.Count} entries");
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to save scrap data: {ex.Message}");
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Stack trace: {ex.StackTrace}");
            }
        }
    }

    /// <summary>
    /// Container for all scrap data
    /// </summary>
    public class ScrapData
    {
        public List<ScrapEntry> Entries { get; set; } = new List<ScrapEntry>();
        public int Version { get; set; } = 1;
    }

    /// <summary>
    /// Represents a single scrapped node entry
    /// </summary>
    public class ScrapEntry
    {
        public string Id { get; set; } = "";
        public string FilePath { get; set; } = "";
        public DateTime Timestamp { get; set; }
        public string Operation { get; set; } = "";
        public string NodeType { get; set; } = "";
        public string NodeText { get; set; } = "";
        public string? Speaker { get; set; }
        public int OriginalIndex { get; set; }
        public string SerializedNode { get; set; } = "";
        public int NestingLevel { get; set; } = 0;
        public string? ParentNodeText { get; set; }

        [JsonIgnore]
        public string DisplayText => $"[{NodeTypeDisplay}] {NodeText}";

        [JsonIgnore]
        public string NodeTypeDisplay
        {
            get
            {
                // Better labels for node types
                return NodeType switch
                {
                    "Entry" when NestingLevel == 0 => "NPC Entry",  // Root level entry
                    "Entry" => "NPC Reply",  // Nested entry (NPC response)
                    "Reply" => "PC Reply",   // Player response
                    _ => NodeType
                };
            }
        }

        [JsonIgnore]
        public string HierarchyInfo
        {
            get
            {
                if (NestingLevel == 0) return "Root level";
                if (!string.IsNullOrEmpty(ParentNodeText))
                {
                    var parentPreview = ParentNodeText?.Length > 30
                        ? ParentNodeText.Substring(0, 27) + "..."
                        : ParentNodeText;
                    return $"Under: {parentPreview}";
                }
                return $"Depth: {NestingLevel}";
            }
        }

        [JsonIgnore]
        public string TimeAgo
        {
            get
            {
                var elapsed = DateTime.UtcNow - Timestamp;
                if (elapsed.TotalMinutes < 1) return "just now";
                if (elapsed.TotalMinutes < 60) return $"{(int)elapsed.TotalMinutes}m ago";
                if (elapsed.TotalHours < 24) return $"{(int)elapsed.TotalHours}h ago";
                if (elapsed.TotalDays < 7) return $"{(int)elapsed.TotalDays}d ago";
                return Timestamp.ToString("yyyy-MM-dd");
            }
        }
    }
}