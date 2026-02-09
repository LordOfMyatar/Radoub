using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using DialogEditor.Models;
using Radoub.Formats.Logging;

namespace DialogEditor.Services
{
    /// <summary>
    /// Handles serialization, deserialization, persistence, and migration
    /// of scrap data. Extracted from ScrapManager for single responsibility (#1271).
    /// </summary>
    public class ScrapSerializationService
    {
        private readonly string _scrapFilePath;
        private readonly JsonSerializerOptions _jsonOptions;

        public ScrapSerializationService(string scrapFilePath)
        {
            _scrapFilePath = scrapFilePath;
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
        }

        public string SerializeNode(DialogNode node)
        {
            try
            {
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
                };

                return JsonSerializer.Serialize(simplified, _jsonOptions);
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to serialize node for scrap: {ex.Message}");
                return "{}";
            }
        }

        public DialogNode? DeserializeNode(string json)
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
                    Pointers = new List<DialogPtr>()
                };

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

        public ScrapData LoadScrapData()
        {
            try
            {
                if (File.Exists(_scrapFilePath))
                {
                    var json = File.ReadAllText(_scrapFilePath);
                    var data = JsonSerializer.Deserialize<ScrapData>(json, _jsonOptions);
                    if (data != null)
                    {
                        MigrateLegacyEntries(data);
                        return data;
                    }
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to load scrap data: {ex.Message}");
            }

            return new ScrapData();
        }

        public void SaveScrapData(ScrapData scrapData)
        {
            try
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"SaveScrapData: Saving {scrapData.Entries.Count} entries to {_scrapFilePath}");

                // Clean up old entries (older than 30 days)
                var cutoffDate = DateTime.UtcNow.AddDays(-30);
                var removed = scrapData.Entries.RemoveAll(e => e.Timestamp < cutoffDate);
                if (removed > 0)
                {
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"SaveScrapData: Removed {removed} old entries");
                }

                var json = JsonSerializer.Serialize(scrapData, _jsonOptions);
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"SaveScrapData: Serialized {json.Length} characters");

                File.WriteAllText(_scrapFilePath, json);
                UnifiedLogger.LogApplication(LogLevel.INFO, $"SaveScrapData: Successfully saved {scrapData.Entries.Count} entries");
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to save scrap data: {ex.Message}");
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Migrates and repairs scrap entries with inconsistent batch tracking.
        /// - Legacy entries (pre-0.1.78) without DeletionBatchId are treated as batch roots
        /// - Entries with ParentEntryId should never be batch roots (#476 fix)
        /// - Ensures each batch has exactly one root
        /// </summary>
        private void MigrateLegacyEntries(ScrapData data)
        {
            var migrated = 0;
            var repaired = 0;

            foreach (var entry in data.Entries)
            {
                if (string.IsNullOrEmpty(entry.DeletionBatchId))
                {
                    entry.DeletionBatchId = entry.Id;
                    entry.IsBatchRoot = true;
                    migrated++;
                }
            }

            foreach (var entry in data.Entries)
            {
                if (!string.IsNullOrEmpty(entry.ParentEntryId) && entry.IsBatchRoot)
                {
                    entry.IsBatchRoot = false;
                    repaired++;
                    UnifiedLogger.LogApplication(LogLevel.DEBUG,
                        $"Fixed child entry incorrectly marked as root: {entry.NodeText}");
                }
            }

            var batches = data.Entries
                .Where(e => !string.IsNullOrEmpty(e.DeletionBatchId))
                .GroupBy(e => e.DeletionBatchId);

            foreach (var batch in batches)
            {
                if (!batch.Any(e => e.IsBatchRoot))
                {
                    var newRoot = batch.FirstOrDefault(e =>
                        string.IsNullOrEmpty(e.ParentEntryId) ||
                        !batch.Any(b => b.Id == e.ParentEntryId));

                    if (newRoot != null)
                    {
                        newRoot.IsBatchRoot = true;
                        newRoot.ChildCount = batch.Count() - 1;
                        repaired++;
                        UnifiedLogger.LogApplication(LogLevel.DEBUG,
                            $"Assigned batch root: {newRoot.NodeText}");
                    }
                }
            }

            if (migrated > 0)
            {
                UnifiedLogger.LogApplication(LogLevel.INFO,
                    $"Migrated {migrated} legacy scrap entries to batch format");
            }
            if (repaired > 0)
            {
                UnifiedLogger.LogApplication(LogLevel.INFO,
                    $"Repaired {repaired} scrap entries with inconsistent batch tracking");
            }
        }
    }
}
