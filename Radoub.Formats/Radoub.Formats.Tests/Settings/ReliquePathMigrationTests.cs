using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace Radoub.Formats.Tests.Settings;

/// <summary>
/// Tests the JSON migration pattern for renaming ItemEditorPath → ReliquePath
/// in RadoubSettings.json. Uses a test DTO that mirrors the production pattern.
/// </summary>
public class ReliquePathMigrationTests
{
    /// <summary>
    /// Mirrors the migration pattern used in RadoubSettings.SettingsData:
    /// - Primary property: ReliquePath (read/write)
    /// - Legacy property: ItemEditorPath (read-only, feeds into ReliquePath)
    /// </summary>
    private class TestSettingsData
    {
        public string? ReliquePath { get; set; }

        /// <summary>
        /// Legacy key — only used for deserialization migration.
        /// Getter always returns null so it won't be serialized (WhenWritingNull).
        /// Setter populates ReliquePath if it hasn't been set yet.
        /// </summary>
        [JsonPropertyName("ItemEditorPath")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? LegacyItemEditorPath
        {
            get => null; // Never serialize
            set
            {
                // Only migrate if new key wasn't already set
                if (value != null && ReliquePath == null)
                    ReliquePath = value;
            }
        }
    }

    [Fact]
    public void Deserialize_NewKey_ReadsReliquePath()
    {
        var json = """{"ReliquePath": "~/Radoub/bin/ItemEditor.exe"}""";
        var data = JsonSerializer.Deserialize<TestSettingsData>(json);

        Assert.Equal("~/Radoub/bin/ItemEditor.exe", data!.ReliquePath);
    }

    [Fact]
    public void Deserialize_OldKey_MigratesToReliquePath()
    {
        // Existing users have "ItemEditorPath" in their RadoubSettings.json
        var json = """{"ItemEditorPath": "~/Radoub/bin/ItemEditor.exe"}""";
        var data = JsonSerializer.Deserialize<TestSettingsData>(json);

        Assert.Equal("~/Radoub/bin/ItemEditor.exe", data!.ReliquePath);
    }

    [Fact]
    public void Deserialize_BothKeys_PrefersNewKey()
    {
        // If both keys exist, new key wins
        var json = """{"ReliquePath": "~/new/path.exe", "ItemEditorPath": "~/old/path.exe"}""";
        var data = JsonSerializer.Deserialize<TestSettingsData>(json);

        Assert.Equal("~/new/path.exe", data!.ReliquePath);
    }

    [Fact]
    public void Serialize_WritesNewKeyOnly()
    {
        var data = new TestSettingsData { ReliquePath = "~/test/path.exe" };
        var json = JsonSerializer.Serialize(data);

        Assert.Contains("\"ReliquePath\"", json);
        Assert.DoesNotContain("\"ItemEditorPath\"", json);
    }

    [Fact]
    public void Deserialize_NeitherKey_ReturnsNull()
    {
        var json = """{"ParleyPath": "~/something"}""";
        var data = JsonSerializer.Deserialize<TestSettingsData>(json);

        Assert.Null(data!.ReliquePath);
    }
}
