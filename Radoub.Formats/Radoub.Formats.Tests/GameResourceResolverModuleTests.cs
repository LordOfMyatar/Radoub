using Radoub.Formats.Common;
using Radoub.Formats.Resolver;
using Radoub.Formats.Services;
using Xunit;

namespace Radoub.Formats.Tests;

public class GameResourceResolverModuleTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _moduleDir;
    private readonly string _overrideDir;

    public GameResourceResolverModuleTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"GameResourceResolverModuleTests_{Guid.NewGuid()}");
        _moduleDir = Path.Combine(_testDir, "module");
        _overrideDir = Path.Combine(_testDir, "override");

        Directory.CreateDirectory(_moduleDir);
        Directory.CreateDirectory(_overrideDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, recursive: true);
        }
    }

    #region FindResource — Module Directory

    [Fact]
    public void FindResource_ModuleDirectory_FindsLooseFile()
    {
        // Arrange
        var testContent = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        File.WriteAllBytes(Path.Combine(_moduleDir, "custom_armor.uti"), testContent);

        var config = new GameResourceConfig { ModuleDirectory = _moduleDir };
        using var resolver = new GameResourceResolver(config);

        // Act
        var result = resolver.FindResource("custom_armor", ResourceTypes.Uti);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(testContent, result);
    }

    [Fact]
    public void FindResourceWithSource_ModuleDirectory_ReturnsModuleSource()
    {
        // Arrange
        var testContent = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        var filePath = Path.Combine(_moduleDir, "custom_armor.uti");
        File.WriteAllBytes(filePath, testContent);

        var config = new GameResourceConfig { ModuleDirectory = _moduleDir };
        using var resolver = new GameResourceResolver(config);

        // Act
        var result = resolver.FindResourceWithSource("custom_armor", ResourceTypes.Uti);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(ResourceSource.Module, result.Source);
        Assert.Equal(filePath, result.SourcePath);
        Assert.Equal("custom_armor", result.ResRef);
        Assert.Equal(ResourceTypes.Uti, result.ResourceType);
    }

    [Fact]
    public void FindResource_ModuleDirectory_HighestPriority()
    {
        // Arrange — same resref in both module dir and override; module should win
        var moduleContent = new byte[] { 0xAA, 0xBB };
        var overrideContent = new byte[] { 0xCC, 0xDD };
        File.WriteAllBytes(Path.Combine(_moduleDir, "shared_item.uti"), moduleContent);
        File.WriteAllBytes(Path.Combine(_overrideDir, "shared_item.uti"), overrideContent);

        var config = new GameResourceConfig
        {
            ModuleDirectory = _moduleDir,
            OverridePath = _overrideDir
        };
        using var resolver = new GameResourceResolver(config);

        // Act
        var result = resolver.FindResourceWithSource("shared_item", ResourceTypes.Uti);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(ResourceSource.Module, result.Source);
        Assert.Equal(moduleContent, result.Data);
    }

    [Fact]
    public void FindResource_ModuleDirectory_CaseInsensitive()
    {
        // Arrange — file has different case than resref
        var testContent = new byte[] { 0x01, 0x02 };
        File.WriteAllBytes(Path.Combine(_moduleDir, "Custom_Armor.uti"), testContent);

        var config = new GameResourceConfig { ModuleDirectory = _moduleDir };
        using var resolver = new GameResourceResolver(config);

        // Act — lowercase resref should find mixed-case file
        var result = resolver.FindResource("custom_armor", ResourceTypes.Uti);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(testContent, result);
    }

    [Fact]
    public void FindResource_NoModuleDirectory_FallsThrough()
    {
        // Arrange — no module dir set, file in override should still be found
        var testContent = new byte[] { 0x01, 0x02, 0x03 };
        File.WriteAllBytes(Path.Combine(_overrideDir, "override_item.uti"), testContent);

        var config = new GameResourceConfig { OverridePath = _overrideDir };
        using var resolver = new GameResourceResolver(config);

        // Act
        var result = resolver.FindResourceWithSource("override_item", ResourceTypes.Uti);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(ResourceSource.Override, result.Source);
    }

    [Fact]
    public void SetModuleDirectory_Null_DisablesModuleSearch()
    {
        // Arrange — set module dir, then clear it
        var testContent = new byte[] { 0x01, 0x02 };
        File.WriteAllBytes(Path.Combine(_moduleDir, "module_item.uti"), testContent);

        var config = new GameResourceConfig { ModuleDirectory = _moduleDir };
        using var resolver = new GameResourceResolver(config);

        // Verify it's found first
        Assert.NotNull(resolver.FindResource("module_item", ResourceTypes.Uti));

        // Act — clear module directory
        resolver.SetModuleDirectory(null);

        // Assert — no longer found
        Assert.Null(resolver.FindResource("module_item", ResourceTypes.Uti));
    }

    [Fact]
    public void FindBaseResource_DoesNotCheckModule()
    {
        // Arrange — file only in module directory
        var testContent = new byte[] { 0x01, 0x02 };
        File.WriteAllBytes(Path.Combine(_moduleDir, "module_only.uti"), testContent);

        var config = new GameResourceConfig { ModuleDirectory = _moduleDir };
        using var resolver = new GameResourceResolver(config);

        // Act — FindBaseResource should NOT check module directory
        var result = resolver.FindBaseResource("module_only", ResourceTypes.Uti);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region ListResources — Module Directory

    [Fact]
    public void ListResources_IncludesModuleFiles()
    {
        // Arrange
        File.WriteAllBytes(Path.Combine(_moduleDir, "mod_item1.uti"), new byte[] { 0x01 });
        File.WriteAllBytes(Path.Combine(_moduleDir, "mod_item2.uti"), new byte[] { 0x02 });

        var config = new GameResourceConfig { ModuleDirectory = _moduleDir };
        using var resolver = new GameResourceResolver(config);

        // Act
        var resources = resolver.ListResources(ResourceTypes.Uti).ToList();

        // Assert
        Assert.Equal(2, resources.Count);
        Assert.All(resources, r => Assert.Equal(ResourceSource.Module, r.Source));
        Assert.Contains(resources, r => r.ResRef == "mod_item1");
        Assert.Contains(resources, r => r.ResRef == "mod_item2");
    }

    #endregion

    #region MapSource

    [Fact]
    public void MapSource_Module_ReturnsGameResourceSourceModule()
    {
        // Arrange — use GameDataService.ListResources to test MapSource indirectly
        // MapSource converts ResourceSource.Module → GameResourceSource.Module
        var testContent = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        File.WriteAllBytes(Path.Combine(_moduleDir, "test_item.uti"), testContent);

        var config = new GameResourceConfig { ModuleDirectory = _moduleDir };
        using var service = new GameDataService(config);

        // Act
        var resources = service.ListResources(ResourceTypes.Uti).ToList();

        // Assert
        var item = resources.FirstOrDefault(r => r.ResRef == "test_item");
        Assert.NotNull(item);
        Assert.Equal(GameResourceSource.Module, item.Source);
    }

    #endregion

    #region ConfigureModuleHaks — Module Directory Wiring

    [Fact]
    public void ConfigureModuleHaks_SetsModuleDirectory()
    {
        // Arrange — create a module dir with a loose UTI file
        var testContent = new byte[] { 0x01, 0x02 };
        File.WriteAllBytes(Path.Combine(_moduleDir, "module_armor.uti"), testContent);

        var config = new GameResourceConfig();
        using var service = new GameDataService(config);

        // Act — configure with module directory
        service.ConfigureModuleHaks(_moduleDir);

        // Assert — module-local file should now be findable
        var result = service.FindResource("module_armor", ResourceTypes.Uti);
        Assert.NotNull(result);
    }

    [Fact]
    public void ConfigureModuleHaks_InvalidDir_ClearsModuleDirectory()
    {
        // Arrange — set module dir with a file, then clear with invalid dir
        var testContent = new byte[] { 0x01, 0x02 };
        File.WriteAllBytes(Path.Combine(_moduleDir, "module_armor.uti"), testContent);

        var config = new GameResourceConfig { ModuleDirectory = _moduleDir };
        using var service = new GameDataService(config);

        // Verify it's found
        Assert.NotNull(service.FindResource("module_armor", ResourceTypes.Uti));

        // Act — configure with invalid directory
        service.ConfigureModuleHaks("");

        // Assert — module-local file should no longer be findable
        Assert.Null(service.FindResource("module_armor", ResourceTypes.Uti));
    }

    #endregion
}
