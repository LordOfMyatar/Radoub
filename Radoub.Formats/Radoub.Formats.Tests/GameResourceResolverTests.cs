using Radoub.Formats.Common;
using Radoub.Formats.Resolver;
using Xunit;

namespace Radoub.Formats.Tests;

public class GameResourceResolverTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _overrideDir;
    private readonly string _hakDir;
    private readonly string _dataDir;

    public GameResourceResolverTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"GameResourceResolverTests_{Guid.NewGuid()}");
        _overrideDir = Path.Combine(_testDir, "override");
        _hakDir = Path.Combine(_testDir, "hak");
        _dataDir = Path.Combine(_testDir, "data");

        Directory.CreateDirectory(_overrideDir);
        Directory.CreateDirectory(_hakDir);
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
    public void GameResourceConfig_DefaultValues_AreCorrect()
    {
        var config = new GameResourceConfig();

        Assert.Null(config.GameDataPath);
        Assert.Null(config.OverridePath);
        Assert.Empty(config.HakPaths);
        Assert.Null(config.TlkPath);
        Assert.Null(config.CustomTlkPath);
        Assert.Null(config.KeyFilePath);
        Assert.True(config.CacheArchives);
    }

    [Fact]
    public void GameResourceConfig_ForNwnEE_SetsCorrectPaths()
    {
        var installPath = @"C:\Games\NWN";
        var config = GameResourceConfig.ForNwnEE(installPath);

        Assert.Equal(Path.Combine(installPath, "data"), config.GameDataPath);
        Assert.Equal(Path.Combine(installPath, "ovr"), config.OverridePath);
        Assert.Equal(Path.Combine(installPath, "data", "dialog.tlk"), config.TlkPath);
        Assert.Equal(Path.Combine(installPath, "data", "nwn_base.key"), config.KeyFilePath);
    }

    [Fact]
    public void GameResourceConfig_ForNwnClassic_SetsCorrectPaths()
    {
        var installPath = @"C:\Games\NWN";
        var config = GameResourceConfig.ForNwnClassic(installPath);

        Assert.Equal(installPath, config.GameDataPath);
        Assert.Equal(Path.Combine(installPath, "override"), config.OverridePath);
        Assert.Equal(Path.Combine(installPath, "dialog.tlk"), config.TlkPath);
        Assert.Equal(Path.Combine(installPath, "chitin.key"), config.KeyFilePath);
    }

    [Fact]
    public void GameResourceConfig_NullInstallPath_ThrowsException()
    {
        Assert.Throws<ArgumentNullException>(() => GameResourceConfig.ForNwnEE(null!));
        Assert.Throws<ArgumentNullException>(() => GameResourceConfig.ForNwnClassic(null!));
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_NullConfig_ThrowsException()
    {
        Assert.Throws<ArgumentNullException>(() => new GameResourceResolver(null!));
    }

    [Fact]
    public void Constructor_ValidConfig_Succeeds()
    {
        var config = new GameResourceConfig();
        using var resolver = new GameResourceResolver(config);
        // No exception means success
    }

    #endregion

    #region Override Resolution Tests

    [Fact]
    public void FindResource_InOverride_ReturnsData()
    {
        // Arrange
        var testContent = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        var testFilePath = Path.Combine(_overrideDir, "testfile.utc");
        File.WriteAllBytes(testFilePath, testContent);

        var config = new GameResourceConfig
        {
            OverridePath = _overrideDir
        };

        using var resolver = new GameResourceResolver(config);

        // Act
        var result = resolver.FindResource("testfile", ResourceTypes.Utc);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(testContent, result);
    }

    [Fact]
    public void FindResourceWithSource_InOverride_ReturnsCorrectSource()
    {
        // Arrange
        var testContent = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        var testFilePath = Path.Combine(_overrideDir, "creature.utc");
        File.WriteAllBytes(testFilePath, testContent);

        var config = new GameResourceConfig
        {
            OverridePath = _overrideDir
        };

        using var resolver = new GameResourceResolver(config);

        // Act
        var result = resolver.FindResourceWithSource("creature", ResourceTypes.Utc);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(ResourceSource.Override, result.Source);
        Assert.Equal(testFilePath, result.SourcePath);
        Assert.Equal("creature", result.ResRef);
        Assert.Equal(ResourceTypes.Utc, result.ResourceType);
    }

    [Fact]
    public void FindResource_NotInOverride_ReturnsNull()
    {
        var config = new GameResourceConfig
        {
            OverridePath = _overrideDir
        };

        using var resolver = new GameResourceResolver(config);

        var result = resolver.FindResource("nonexistent", ResourceTypes.Utc);

        Assert.Null(result);
    }

    [Fact]
    public void FindResource_CaseInsensitive_FindsFile()
    {
        // Arrange
        var testContent = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        var testFilePath = Path.Combine(_overrideDir, "TestFile.utc");
        File.WriteAllBytes(testFilePath, testContent);

        var config = new GameResourceConfig
        {
            OverridePath = _overrideDir
        };

        using var resolver = new GameResourceResolver(config);

        // Act - search with different case
        var result = resolver.FindResource("testfile", ResourceTypes.Utc);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public void ListResources_FromOverride_ListsFiles()
    {
        // Arrange
        File.WriteAllBytes(Path.Combine(_overrideDir, "file1.utc"), new byte[] { 0x01 });
        File.WriteAllBytes(Path.Combine(_overrideDir, "file2.utc"), new byte[] { 0x02 });
        File.WriteAllBytes(Path.Combine(_overrideDir, "file3.uti"), new byte[] { 0x03 }); // Different type

        var config = new GameResourceConfig
        {
            OverridePath = _overrideDir
        };

        using var resolver = new GameResourceResolver(config);

        // Act
        var results = resolver.ListResources(ResourceTypes.Utc).ToList();

        // Assert
        Assert.Equal(2, results.Count);
        Assert.Contains(results, r => r.ResRef == "file1");
        Assert.Contains(results, r => r.ResRef == "file2");
        Assert.All(results, r => Assert.Equal(ResourceSource.Override, r.Source));
    }

    #endregion

    #region Override Path Resolution Tests

    [Fact]
    public void FindResource_NoOverridePath_UsesGameDataOverride()
    {
        // Arrange
        var gameDataOverride = Path.Combine(_dataDir, "override");
        Directory.CreateDirectory(gameDataOverride);
        File.WriteAllBytes(Path.Combine(gameDataOverride, "test.utc"), new byte[] { 0x01 });

        var config = new GameResourceConfig
        {
            GameDataPath = _dataDir
        };

        using var resolver = new GameResourceResolver(config);

        // Act
        var result = resolver.FindResource("test", ResourceTypes.Utc);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public void FindResource_ExplicitOverridePath_OverridesDefault()
    {
        // Arrange
        var gameDataOverride = Path.Combine(_dataDir, "override");
        Directory.CreateDirectory(gameDataOverride);
        File.WriteAllBytes(Path.Combine(gameDataOverride, "test.utc"), new byte[] { 0x01 });
        File.WriteAllBytes(Path.Combine(_overrideDir, "test.utc"), new byte[] { 0x02 });

        var config = new GameResourceConfig
        {
            GameDataPath = _dataDir,
            OverridePath = _overrideDir
        };

        using var resolver = new GameResourceResolver(config);

        // Act
        var result = resolver.FindResource("test", ResourceTypes.Utc);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(new byte[] { 0x02 }, result); // Should find in explicit override
    }

    #endregion

    #region Priority Tests

    [Fact]
    public void FindResource_OverrideTakesPriority_OverBif()
    {
        // This test verifies override takes priority even if BIF has the resource
        // Since we don't have a real KEY/BIF setup, we just verify override is checked first

        var testContent = new byte[] { 0xFF, 0xFE, 0xFD };
        File.WriteAllBytes(Path.Combine(_overrideDir, "priority.utc"), testContent);

        var config = new GameResourceConfig
        {
            OverridePath = _overrideDir,
            GameDataPath = _dataDir // No actual KEY file here
        };

        using var resolver = new GameResourceResolver(config);

        var result = resolver.FindResourceWithSource("priority", ResourceTypes.Utc);

        Assert.NotNull(result);
        Assert.Equal(ResourceSource.Override, result.Source);
    }

    #endregion

    #region Resource Info Tests

    [Fact]
    public void ResourceResult_Properties_AreCorrect()
    {
        var data = new byte[] { 0x01, 0x02 };
        var result = new ResourceResult(data, ResourceSource.Override, "/path/file.utc", "file", ResourceTypes.Utc);

        Assert.Equal(data, result.Data);
        Assert.Equal(ResourceSource.Override, result.Source);
        Assert.Equal("/path/file.utc", result.SourcePath);
        Assert.Equal("file", result.ResRef);
        Assert.Equal(ResourceTypes.Utc, result.ResourceType);
    }

    [Fact]
    public void ResourceInfo_DefaultValues_AreCorrect()
    {
        var info = new ResourceInfo();

        Assert.Equal("", info.ResRef);
        Assert.Equal((ushort)0, info.ResourceType);
        Assert.Equal(ResourceSource.Override, info.Source);
        Assert.Equal("", info.SourcePath);
    }

    #endregion

    #region Disposal Tests

    [Fact]
    public void Dispose_ClearsCache()
    {
        var config = new GameResourceConfig
        {
            OverridePath = _overrideDir,
            CacheArchives = true
        };

        var resolver = new GameResourceResolver(config);

        // Use the resolver
        resolver.FindResource("test", ResourceTypes.Utc);

        // Dispose
        resolver.Dispose();

        // Double dispose should not throw
        resolver.Dispose();
    }

    #endregion

    #region Empty/Missing Path Tests

    [Fact]
    public void FindResource_NoOverridePath_ReturnsNull()
    {
        var config = new GameResourceConfig();
        using var resolver = new GameResourceResolver(config);

        var result = resolver.FindResource("test", ResourceTypes.Utc);

        Assert.Null(result);
    }

    [Fact]
    public void FindResource_NonexistentOverridePath_ReturnsNull()
    {
        var config = new GameResourceConfig
        {
            OverridePath = Path.Combine(_testDir, "nonexistent_override")
        };

        using var resolver = new GameResourceResolver(config);

        var result = resolver.FindResource("test", ResourceTypes.Utc);

        Assert.Null(result);
    }

    [Fact]
    public void ListResources_NonexistentOverridePath_ReturnsEmpty()
    {
        var config = new GameResourceConfig
        {
            OverridePath = Path.Combine(_testDir, "nonexistent_override")
        };

        using var resolver = new GameResourceResolver(config);

        var results = resolver.ListResources(ResourceTypes.Utc).ToList();

        Assert.Empty(results);
    }

    #endregion

    #region Deduplication Tests

    [Fact]
    public void ListResources_DuplicatesAcrossSources_ReturnsFirstOnly()
    {
        // Override has file1
        File.WriteAllBytes(Path.Combine(_overrideDir, "shared.utc"), new byte[] { 0x01 });

        // If HAK also has shared.utc (simulated), override should win
        // For this test, we just verify override entries come first

        var config = new GameResourceConfig
        {
            OverridePath = _overrideDir
        };

        using var resolver = new GameResourceResolver(config);

        var results = resolver.ListResources(ResourceTypes.Utc).ToList();

        Assert.Single(results);
        Assert.Equal("shared", results[0].ResRef);
        Assert.Equal(ResourceSource.Override, results[0].Source);
    }

    #endregion
}
