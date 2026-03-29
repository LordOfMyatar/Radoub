using Xunit;
using Radoub.UI.Services;

namespace Radoub.UI.Tests;

public class QuickTokenServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _configPath;

    public QuickTokenServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"radoub-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _configPath = Path.Combine(_tempDir, "quick-tokens.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public void Load_MissingFile_ReturnsEmptySlots()
    {
        var service = new QuickTokenService(_configPath);
        var slots = service.Load();
        Assert.Equal(3, slots.Length);
        Assert.All(slots, s => Assert.Null(s.Token));
    }

    [Fact]
    public void SaveAndLoad_RoundTrips()
    {
        var service = new QuickTokenService(_configPath);
        var slots = service.Load();
        slots[0] = new QuickTokenSlot(1, "<CUSTOM1001>", "Red");
        slots[2] = new QuickTokenSlot(3, "<FirstName>", "FirstName");
        service.Save(slots);

        var loaded = service.Load();
        Assert.Equal("<CUSTOM1001>", loaded[0].Token);
        Assert.Equal("Red", loaded[0].Label);
        Assert.Null(loaded[1].Token);
        Assert.Equal("<FirstName>", loaded[2].Token);
    }

    [Fact]
    public void Load_MalformedJson_ReturnsEmptySlots()
    {
        File.WriteAllText(_configPath, "not json{{{");
        var service = new QuickTokenService(_configPath);
        var slots = service.Load();
        Assert.Equal(3, slots.Length);
        Assert.All(slots, s => Assert.Null(s.Token));
    }

    [Fact]
    public void Load_InvalidSlotNumbers_Ignored()
    {
        var json = """
        {
          "quickSlots": [
            { "slot": 0, "token": "<Bad>", "label": "Bad" },
            { "slot": 1, "token": "<Good>", "label": "Good" },
            { "slot": 4, "token": "<Also Bad>", "label": "Also Bad" }
          ]
        }
        """;
        File.WriteAllText(_configPath, json);
        var service = new QuickTokenService(_configPath);
        var slots = service.Load();
        Assert.Equal("<Good>", slots[0].Token);
        Assert.Null(slots[1].Token);
        Assert.Null(slots[2].Token);
    }

    [Fact]
    public void Load_DuplicateSlotNumbers_LastWins()
    {
        var json = """
        {
          "quickSlots": [
            { "slot": 1, "token": "<First>", "label": "First" },
            { "slot": 1, "token": "<Second>", "label": "Second" }
          ]
        }
        """;
        File.WriteAllText(_configPath, json);
        var service = new QuickTokenService(_configPath);
        var slots = service.Load();
        Assert.Equal("<Second>", slots[0].Token);
    }
}
