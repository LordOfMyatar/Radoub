using Radoub.Dictionary.Models;
using Xunit;

namespace Radoub.Dictionary.Tests;

/// <summary>
/// Tests for user-dictionary discovery: malformed files are skipped (not crashed on),
/// valid JSON custom dictionaries are surfaced, and the scan cache behaves (#2264).
/// </summary>
public class DictionaryDiscoveryTests : IDisposable
{
    private readonly string _dir;

    public DictionaryDiscoveryTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"RadoubDictDisc_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); }
        catch { }
    }

    [Fact]
    public void ScanForDictionaries_MalformedJson_IsSkippedNotThrown()
    {
        // A garbage .dic file in the user folder must not abort discovery.
        File.WriteAllText(Path.Combine(_dir, "broken.dic"), "{ this is not valid json ");

        var discovery = new DictionaryDiscovery(_dir);

        // Should not throw; broken file simply absent from results.
        var results = discovery.GetAvailableCustomDictionaries();
        Assert.DoesNotContain(results, d => d.Id == "broken");
    }

    [Fact]
    public void ScanForDictionaries_ValidCustomJson_IsDiscovered()
    {
        var dict = new CustomDictionary
        {
            Source = "Test Pack",
            Description = "unit-test dictionary",
            Words = new List<string> { "Aribeth", "Fenthick" }
        };
        var json = System.Text.Json.JsonSerializer.Serialize(dict,
            new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            });
        File.WriteAllText(Path.Combine(_dir, "testpack.dic"), json);

        var discovery = new DictionaryDiscovery(_dir);
        var results = discovery.GetAvailableCustomDictionaries();

        var found = Assert.Single(results, d => d.Id == "testpack");
        Assert.Equal("Test Pack", found.Name);
        Assert.Equal(2, found.WordCount);
    }

    [Fact]
    public void ScanForDictionaries_SkipsSettingsAndCustomFiles()
    {
        File.WriteAllText(Path.Combine(_dir, "settings.json"), "{}");
        File.WriteAllText(Path.Combine(_dir, "custom.dic"), "{\"words\":[\"x\"]}");

        var discovery = new DictionaryDiscovery(_dir);
        var results = discovery.GetAvailableCustomDictionaries();

        Assert.DoesNotContain(results, d => d.Id == "settings");
        Assert.DoesNotContain(results, d => d.Id == "custom");
    }

    [Fact]
    public void ScanForDictionaries_CachesUntilCleared()
    {
        var discovery = new DictionaryDiscovery(_dir);
        var first = discovery.ScanForDictionaries();

        // Add a file after the first scan — cached result must not see it.
        File.WriteAllText(Path.Combine(_dir, "late.dic"), "{\"source\":\"Late\",\"words\":[\"z\"]}");
        var cached = discovery.ScanForDictionaries();
        Assert.Equal(first.Count, cached.Count);

        // After clearing, the rescan picks it up.
        discovery.ClearCache();
        var rescanned = discovery.ScanForDictionaries();
        Assert.Contains(rescanned, d => d.Id == "late");
    }
}
