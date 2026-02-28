using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using DialogEditor.Models;
using DialogEditor.Services;
using Xunit;

namespace Parley.Tests
{
    /// <summary>
    /// Tests for ScrapSerializationService: serialization round-trip,
    /// persistence, migration, and edge cases.
    /// </summary>
    public class ScrapSerializationServiceTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly string _scrapFilePath;
        private readonly ScrapSerializationService _service;

        // Match the service's JSON options for writing test data
        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public ScrapSerializationServiceTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"ScrapTests_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tempDir);
            _scrapFilePath = Path.Combine(_tempDir, "scrap.json");
            _service = new ScrapSerializationService(_scrapFilePath);
        }

        public void Dispose()
        {
            try { Directory.Delete(_tempDir, true); } catch { }
        }

        #region Serialize/Deserialize Round-Trip

        [Fact]
        public void RoundTrip_SimpleEntryNode_PreservesAllFields()
        {
            var node = new DialogNode
            {
                Type = DialogNodeType.Entry,
                Speaker = "Elminister",
                Comment = "Wise old wizard",
                Sound = "vo_elminster_01",
                ScriptAction = "nw_act_quest01",
                Animation = DialogAnimation.Bow,
                AnimationLoop = true,
                Delay = 3000,
                Quest = "q_main",
                QuestEntry = 5
            };
            node.Text = new LocString();
            node.Text.Add(0, "Greetings, adventurer.");

            var json = _service.SerializeNode(node);
            var restored = _service.DeserializeNode(json);

            Assert.NotNull(restored);
            Assert.Equal(DialogNodeType.Entry, restored!.Type);
            Assert.Equal("Elminister", restored.Speaker);
            Assert.Equal("Wise old wizard", restored.Comment);
            Assert.Equal("vo_elminster_01", restored.Sound);
            Assert.Equal("nw_act_quest01", restored.ScriptAction);
            Assert.Equal(DialogAnimation.Bow, restored.Animation);
            Assert.True(restored.AnimationLoop);
            Assert.Equal(3000u, restored.Delay);
            Assert.Equal("q_main", restored.Quest);
            Assert.Equal(5u, restored.QuestEntry);
            Assert.Equal("Greetings, adventurer.", restored.Text?.GetDefault());
        }

        [Fact]
        public void RoundTrip_ReplyNode_PreservesType()
        {
            var node = new DialogNode
            {
                Type = DialogNodeType.Reply,
                Speaker = "",
                Comment = ""
            };
            node.Text = new LocString();
            node.Text.Add(0, "I accept your quest.");

            var json = _service.SerializeNode(node);
            var restored = _service.DeserializeNode(json);

            Assert.NotNull(restored);
            Assert.Equal(DialogNodeType.Reply, restored!.Type);
            Assert.Equal("I accept your quest.", restored.Text?.GetDefault());
        }

        [Fact]
        public void RoundTrip_MultiLanguageText_PreservesAllLanguages()
        {
            var node = new DialogNode { Type = DialogNodeType.Entry };
            node.Text = new LocString();
            node.Text.Add(0, "Hello");    // English
            node.Text.Add(4, "Hallo");    // German
            node.Text.Add(2, "Bonjour");  // French

            var json = _service.SerializeNode(node);
            var restored = _service.DeserializeNode(json);

            Assert.NotNull(restored);
            Assert.Equal("Hello", restored!.Text?.Get(0));
            Assert.Equal("Hallo", restored.Text?.Get(4));
            Assert.Equal("Bonjour", restored.Text?.Get(2));
        }

        [Fact]
        public void RoundTrip_ActionParams_PreservesDictionary()
        {
            var node = new DialogNode
            {
                Type = DialogNodeType.Entry,
                ActionParams = new Dictionary<string, string>
                {
                    ["Param1"] = "value1",
                    ["GoldAmount"] = "500"
                }
            };
            node.Text = new LocString();

            var json = _service.SerializeNode(node);
            var restored = _service.DeserializeNode(json);

            Assert.NotNull(restored);
            Assert.NotNull(restored!.ActionParams);
            Assert.Equal("value1", restored.ActionParams["Param1"]);
            Assert.Equal("500", restored.ActionParams["GoldAmount"]);
        }

        [Fact]
        public void RoundTrip_EmptyNode_HandlesDefaults()
        {
            var node = new DialogNode
            {
                Type = DialogNodeType.Entry,
                Speaker = "",
                Comment = "",
                Sound = "",
                ScriptAction = "",
                Quest = ""
            };
            node.Text = new LocString();

            var json = _service.SerializeNode(node);
            var restored = _service.DeserializeNode(json);

            Assert.NotNull(restored);
            Assert.Equal(DialogNodeType.Entry, restored!.Type);
            Assert.Equal("", restored.Speaker);
        }

        #endregion

        #region Deserialize Edge Cases

        [Fact]
        public void DeserializeNode_InvalidJson_ReturnsNull()
        {
            var result = _service.DeserializeNode("not valid json");
            Assert.Null(result);
        }

        [Fact]
        public void DeserializeNode_EmptyObject_ReturnsNodeWithDefaults()
        {
            var result = _service.DeserializeNode("{\"type\": \"Entry\"}");

            Assert.NotNull(result);
            Assert.Equal(DialogNodeType.Entry, result!.Type);
            Assert.Equal("", result.Speaker);
            Assert.Equal("", result.Comment);
        }

        [Fact]
        public void DeserializeNode_MissingOptionalFields_HandlesGracefully()
        {
            // Simulates old scrap data that might not have all fields
            var json = "{\"type\": \"Reply\", \"speaker\": \"NPC1\"}";
            var result = _service.DeserializeNode(json);

            Assert.NotNull(result);
            Assert.Equal(DialogNodeType.Reply, result!.Type);
            Assert.Equal("NPC1", result.Speaker);
            Assert.Equal("", result.Sound);
            Assert.Equal(0u, result.Delay);
        }

        #endregion

        #region Persistence (Save/Load ScrapData)

        [Fact]
        public void SaveAndLoad_RoundTrip_PreservesEntries()
        {
            var data = new ScrapData
            {
                Version = 1,
                Entries = new List<ScrapEntry>
                {
                    new ScrapEntry
                    {
                        Id = "entry1",
                        FilePath = "test.dlg",
                        Timestamp = DateTime.UtcNow,
                        Operation = "Delete",
                        NodeType = "Entry",
                        NodeText = "Hello world",
                        Speaker = "NPC1",
                        OriginalIndex = 0,
                        SerializedNode = "{\"type\":\"Entry\"}",
                        DeletionBatchId = "batch1",
                        IsBatchRoot = true,
                        ChildCount = 2
                    }
                }
            };

            _service.SaveScrapData(data);
            var loaded = _service.LoadScrapData();

            Assert.Single(loaded.Entries);
            Assert.Equal("entry1", loaded.Entries[0].Id);
            Assert.Equal("Hello world", loaded.Entries[0].NodeText);
            Assert.Equal("NPC1", loaded.Entries[0].Speaker);
            Assert.Equal("batch1", loaded.Entries[0].DeletionBatchId);
            Assert.True(loaded.Entries[0].IsBatchRoot);
            Assert.Equal(2, loaded.Entries[0].ChildCount);
        }

        [Fact]
        public void LoadScrapData_NoFile_ReturnsEmptyData()
        {
            var loaded = _service.LoadScrapData();

            Assert.NotNull(loaded);
            Assert.Empty(loaded.Entries);
        }

        [Fact]
        public void LoadScrapData_CorruptFile_ReturnsEmptyData()
        {
            File.WriteAllText(_scrapFilePath, "not valid json at all{{{");

            var loaded = _service.LoadScrapData();

            Assert.NotNull(loaded);
            Assert.Empty(loaded.Entries);
        }

        [Fact]
        public void SaveScrapData_RemovesOldEntries()
        {
            var data = new ScrapData
            {
                Entries = new List<ScrapEntry>
                {
                    new ScrapEntry
                    {
                        Id = "old",
                        Timestamp = DateTime.UtcNow.AddDays(-31),
                        SerializedNode = "{}"
                    },
                    new ScrapEntry
                    {
                        Id = "recent",
                        Timestamp = DateTime.UtcNow,
                        SerializedNode = "{}"
                    }
                }
            };

            _service.SaveScrapData(data);
            var loaded = _service.LoadScrapData();

            Assert.Single(loaded.Entries);
            Assert.Equal("recent", loaded.Entries[0].Id);
        }

        #endregion

        #region Legacy Migration

        [Fact]
        public void LoadScrapData_LegacyEntries_AssignsBatchIds()
        {
            // Legacy entries don't have DeletionBatchId
            var data = new ScrapData
            {
                Entries = new List<ScrapEntry>
                {
                    new ScrapEntry
                    {
                        Id = "legacy1",
                        Timestamp = DateTime.UtcNow,
                        DeletionBatchId = null,
                        SerializedNode = "{\"type\":\"Entry\"}"
                    },
                    new ScrapEntry
                    {
                        Id = "legacy2",
                        Timestamp = DateTime.UtcNow,
                        DeletionBatchId = null,
                        SerializedNode = "{\"type\":\"Reply\"}"
                    }
                }
            };

            // Save directly, then load to trigger migration
            var json = JsonSerializer.Serialize(data, _jsonOptions);
            File.WriteAllText(_scrapFilePath, json);

            var loaded = _service.LoadScrapData();

            // Each legacy entry gets its own Id as DeletionBatchId
            Assert.All(loaded.Entries, e =>
            {
                Assert.Equal(e.Id, e.DeletionBatchId);
                Assert.True(e.IsBatchRoot);
            });
        }

        [Fact]
        public void LoadScrapData_ChildMarkedAsRoot_GetsRepaired()
        {
            // A child entry should not be marked as batch root
            var data = new ScrapData
            {
                Entries = new List<ScrapEntry>
                {
                    new ScrapEntry
                    {
                        Id = "parent",
                        DeletionBatchId = "batch1",
                        IsBatchRoot = true,
                        ParentEntryId = null,
                        Timestamp = DateTime.UtcNow,
                        SerializedNode = "{\"type\":\"Entry\"}"
                    },
                    new ScrapEntry
                    {
                        Id = "child",
                        DeletionBatchId = "batch1",
                        IsBatchRoot = true, // Incorrectly marked as root
                        ParentEntryId = "parent",
                        Timestamp = DateTime.UtcNow,
                        SerializedNode = "{\"type\":\"Reply\"}"
                    }
                }
            };

            var json = JsonSerializer.Serialize(data, _jsonOptions);
            File.WriteAllText(_scrapFilePath, json);

            var loaded = _service.LoadScrapData();

            var parent = loaded.Entries.Find(e => e.Id == "parent");
            var child = loaded.Entries.Find(e => e.Id == "child");

            Assert.True(parent!.IsBatchRoot);
            Assert.False(child!.IsBatchRoot); // Repaired
        }

        [Fact]
        public void LoadScrapData_BatchWithNoRoot_AssignsRootToOrphan()
        {
            // A batch where no entry is marked as root
            var data = new ScrapData
            {
                Entries = new List<ScrapEntry>
                {
                    new ScrapEntry
                    {
                        Id = "orphan1",
                        DeletionBatchId = "batch1",
                        IsBatchRoot = false,
                        ParentEntryId = null, // No parent = likely root
                        Timestamp = DateTime.UtcNow,
                        SerializedNode = "{\"type\":\"Entry\"}"
                    },
                    new ScrapEntry
                    {
                        Id = "orphan2",
                        DeletionBatchId = "batch1",
                        IsBatchRoot = false,
                        ParentEntryId = "orphan1",
                        Timestamp = DateTime.UtcNow,
                        SerializedNode = "{\"type\":\"Reply\"}"
                    }
                }
            };

            var json = JsonSerializer.Serialize(data, _jsonOptions);
            File.WriteAllText(_scrapFilePath, json);

            var loaded = _service.LoadScrapData();

            // orphan1 should become the root (no parent, or parent not in batch)
            var root = loaded.Entries.Find(e => e.Id == "orphan1");
            Assert.True(root!.IsBatchRoot);
        }

        #endregion
    }
}
