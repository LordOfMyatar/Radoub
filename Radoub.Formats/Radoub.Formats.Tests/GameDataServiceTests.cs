using Radoub.Formats.Common;
using Radoub.Formats.Resolver;
using Radoub.Formats.Services;
using Xunit;

namespace Radoub.Formats.Tests;

public class GameDataServiceTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _overrideDir;
    private readonly string _dataDir;

    public GameDataServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"GameDataServiceTests_{Guid.NewGuid()}");
        _overrideDir = Path.Combine(_testDir, "override");
        _dataDir = Path.Combine(_testDir, "data");

        Directory.CreateDirectory(_overrideDir);
        Directory.CreateDirectory(_dataDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, recursive: true);
        }
    }

    #region Configuration Tests

    [Fact]
    public void Constructor_WithConfig_IsConfigured()
    {
        var config = new GameResourceConfig
        {
            OverridePath = _overrideDir
        };

        using var service = new GameDataService(config);

        Assert.True(service.IsConfigured);
    }

    [Fact]
    public void Constructor_EmptyConfig_IsConfigured()
    {
        // Even with empty config, the service is "configured" (just has no paths)
        var config = new GameResourceConfig();

        using var service = new GameDataService(config);

        Assert.True(service.IsConfigured);
    }

    #endregion

    #region 2DA Tests

    [Fact]
    public void Get2DA_FromOverride_ReturnsParsed2DA()
    {
        // Arrange - create a minimal 2DA file
        var twoDAContent = @"2DA V2.0

          LABEL       VALUE
0         Row0        100
1         Row1        200
";
        File.WriteAllText(Path.Combine(_overrideDir, "test.2da"), twoDAContent);

        var config = new GameResourceConfig { OverridePath = _overrideDir };
        using var service = new GameDataService(config);

        // Act
        var twoDA = service.Get2DA("test");

        // Assert
        Assert.NotNull(twoDA);
        Assert.Equal(2, twoDA.ColumnCount);
        Assert.Equal(2, twoDA.RowCount);
        Assert.Equal("100", twoDA.GetValue(0, "VALUE"));
        Assert.Equal("200", twoDA.GetValue(1, "VALUE"));
    }

    [Fact]
    public void Get2DA_CachesResult()
    {
        // Arrange
        var twoDAContent = @"2DA V2.0

          COL1
0         Value1
";
        File.WriteAllText(Path.Combine(_overrideDir, "cached.2da"), twoDAContent);

        var config = new GameResourceConfig { OverridePath = _overrideDir };
        using var service = new GameDataService(config);

        // Act
        var first = service.Get2DA("cached");
        var second = service.Get2DA("cached");

        // Assert - should be same instance
        Assert.Same(first, second);
    }

    [Fact]
    public void Get2DA_NotFound_ReturnsNull()
    {
        var config = new GameResourceConfig { OverridePath = _overrideDir };
        using var service = new GameDataService(config);

        var result = service.Get2DA("nonexistent");

        Assert.Null(result);
    }

    [Fact]
    public void Get2DA_NotFound_CachesNegativeResult()
    {
        var config = new GameResourceConfig { OverridePath = _overrideDir };
        using var service = new GameDataService(config);

        // First call - not found
        var first = service.Get2DA("missing");
        Assert.Null(first);

        // Create the file after first call
        File.WriteAllText(Path.Combine(_overrideDir, "missing.2da"), "2DA V2.0\n\n  COL\n0 Val\n");

        // Second call - still null due to negative cache
        var second = service.Get2DA("missing");
        Assert.Null(second);
    }

    [Fact]
    public void Get2DAValue_ReturnsCorrectValue()
    {
        // Arrange
        var twoDAContent = @"2DA V2.0

          Name        Cost
0         Sword       50
1         Shield      30
";
        File.WriteAllText(Path.Combine(_overrideDir, "items.2da"), twoDAContent);

        var config = new GameResourceConfig { OverridePath = _overrideDir };
        using var service = new GameDataService(config);

        // Act
        var cost = service.Get2DAValue("items", 0, "Cost");
        var name = service.Get2DAValue("items", 1, "Name");

        // Assert
        Assert.Equal("50", cost);
        Assert.Equal("Shield", name);
    }

    [Fact]
    public void Get2DAValue_InvalidRow_ReturnsNull()
    {
        var twoDAContent = @"2DA V2.0

          COL
0         Val
";
        File.WriteAllText(Path.Combine(_overrideDir, "small.2da"), twoDAContent);

        var config = new GameResourceConfig { OverridePath = _overrideDir };
        using var service = new GameDataService(config);

        var result = service.Get2DAValue("small", 99, "COL");

        Assert.Null(result);
    }

    [Fact]
    public void Has2DA_ExistingFile_ReturnsTrue()
    {
        File.WriteAllText(Path.Combine(_overrideDir, "exists.2da"), "2DA V2.0\n\n  COL\n0 Val\n");

        var config = new GameResourceConfig { OverridePath = _overrideDir };
        using var service = new GameDataService(config);

        Assert.True(service.Has2DA("exists"));
    }

    [Fact]
    public void Has2DA_MissingFile_ReturnsFalse()
    {
        var config = new GameResourceConfig { OverridePath = _overrideDir };
        using var service = new GameDataService(config);

        Assert.False(service.Has2DA("doesnotexist"));
    }

    [Fact]
    public void ClearCache_ClearsNegativeCache()
    {
        var config = new GameResourceConfig { OverridePath = _overrideDir };
        using var service = new GameDataService(config);

        // First call - not found (negative cached)
        var first = service.Get2DA("latefile");
        Assert.Null(first);

        // Create the file
        File.WriteAllText(Path.Combine(_overrideDir, "latefile.2da"), "2DA V2.0\n\n  COL\n0 Val\n");

        // Clear cache
        service.ClearCache();

        // Now should find it
        var second = service.Get2DA("latefile");
        Assert.NotNull(second);
    }

    #endregion

    #region TLK Tests

    [Fact]
    public void GetString_NullInput_ReturnsNull()
    {
        var config = new GameResourceConfig { OverridePath = _overrideDir };
        using var service = new GameDataService(config);

        Assert.Null(service.GetString((string?)null));
        Assert.Null(service.GetString(""));
        Assert.Null(service.GetString("****"));
    }

    [Fact]
    public void GetString_InvalidNumber_ReturnsNull()
    {
        var config = new GameResourceConfig { OverridePath = _overrideDir };
        using var service = new GameDataService(config);

        Assert.Null(service.GetString("not_a_number"));
        Assert.Null(service.GetString("12.34"));
    }

    [Fact]
    public void GetString_NoTlkLoaded_ReturnsNull()
    {
        var config = new GameResourceConfig { OverridePath = _overrideDir };
        using var service = new GameDataService(config);

        Assert.Null(service.GetString(1234u));
    }

    [Fact]
    public void HasCustomTlk_NoCustomTlk_ReturnsFalse()
    {
        var config = new GameResourceConfig { OverridePath = _overrideDir };
        using var service = new GameDataService(config);

        Assert.False(service.HasCustomTlk);
    }

    [Fact]
    public void SetCustomTlk_NonexistentFile_DoesNotThrow()
    {
        var config = new GameResourceConfig { OverridePath = _overrideDir };
        using var service = new GameDataService(config);

        // Should not throw
        service.SetCustomTlk(Path.Combine(_testDir, "nonexistent.tlk"));
        Assert.False(service.HasCustomTlk);
    }

    #endregion

    #region Resource Access Tests

    [Fact]
    public void FindResource_InOverride_ReturnsData()
    {
        var testData = new byte[] { 0x01, 0x02, 0x03 };
        File.WriteAllBytes(Path.Combine(_overrideDir, "testres.utc"), testData);

        var config = new GameResourceConfig { OverridePath = _overrideDir };
        using var service = new GameDataService(config);

        var result = service.FindResource("testres", ResourceTypes.Utc);

        Assert.NotNull(result);
        Assert.Equal(testData, result);
    }

    [Fact]
    public void FindResource_NotFound_ReturnsNull()
    {
        var config = new GameResourceConfig { OverridePath = _overrideDir };
        using var service = new GameDataService(config);

        var result = service.FindResource("missing", ResourceTypes.Utc);

        Assert.Null(result);
    }

    [Fact]
    public void ListResources_ReturnsOverrideResources()
    {
        File.WriteAllBytes(Path.Combine(_overrideDir, "res1.utc"), new byte[] { 0x01 });
        File.WriteAllBytes(Path.Combine(_overrideDir, "res2.utc"), new byte[] { 0x02 });

        var config = new GameResourceConfig { OverridePath = _overrideDir };
        using var service = new GameDataService(config);

        var results = service.ListResources(ResourceTypes.Utc).ToList();

        Assert.Equal(2, results.Count);
        Assert.Contains(results, r => r.ResRef == "res1");
        Assert.Contains(results, r => r.ResRef == "res2");
        Assert.All(results, r => Assert.Equal(GameResourceSource.Override, r.Source));
    }

    #endregion

    #region Disposal Tests

    [Fact]
    public void Dispose_MultipleDispose_DoesNotThrow()
    {
        var config = new GameResourceConfig { OverridePath = _overrideDir };
        var service = new GameDataService(config);

        service.Dispose();
        service.Dispose(); // Should not throw
    }

    [Fact]
    public void Get2DA_AfterDispose_ThrowsObjectDisposedException()
    {
        var config = new GameResourceConfig { OverridePath = _overrideDir };
        var service = new GameDataService(config);
        service.Dispose();

        Assert.Throws<ObjectDisposedException>(() => service.Get2DA("test"));
    }

    [Fact]
    public void FindResource_AfterDispose_ThrowsObjectDisposedException()
    {
        var config = new GameResourceConfig { OverridePath = _overrideDir };
        var service = new GameDataService(config);
        service.Dispose();

        Assert.Throws<ObjectDisposedException>(() => service.FindResource("test", ResourceTypes.Utc));
    }

    #endregion

    #region GameResourceInfo Tests

    [Fact]
    public void GameResourceInfo_RequiredProperties_AreSet()
    {
        var info = new GameResourceInfo
        {
            ResRef = "test",
            ResourceType = ResourceTypes.Utc,
            Source = GameResourceSource.Override,
            SourcePath = "/path/test.utc"
        };

        Assert.Equal("test", info.ResRef);
        Assert.Equal(ResourceTypes.Utc, info.ResourceType);
        Assert.Equal(GameResourceSource.Override, info.Source);
        Assert.Equal("/path/test.utc", info.SourcePath);
    }

    #endregion
}
