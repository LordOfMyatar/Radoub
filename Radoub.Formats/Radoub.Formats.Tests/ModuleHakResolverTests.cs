using Radoub.Formats.Ifo;
using Radoub.Formats.Resolver;
using Xunit;

namespace Radoub.Formats.Tests;

/// <summary>
/// Tests for ModuleHakResolver - resolves module.ifo HakList to file paths.
/// </summary>
public class ModuleHakResolverTests : IDisposable
{
    private readonly string _testDir;

    public ModuleHakResolverTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"ModuleHakResolverTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            try { Directory.Delete(_testDir, recursive: true); }
            catch { /* cleanup best-effort */ }
        }
    }

    #region Helpers

    private string CreateModuleDir(List<string> hakNames)
    {
        var moduleDir = Path.Combine(_testDir, "module");
        Directory.CreateDirectory(moduleDir);

        var ifo = new IfoFile();
        foreach (var hak in hakNames)
            ifo.HakList.Add(hak);

        IfoWriter.Write(ifo, Path.Combine(moduleDir, "module.ifo"));
        return moduleDir;
    }

    private string CreateHakDir(params string[] hakFileNames)
    {
        var hakDir = Path.Combine(_testDir, $"hak_{Guid.NewGuid():N}");
        Directory.CreateDirectory(hakDir);
        foreach (var name in hakFileNames)
        {
            File.WriteAllBytes(Path.Combine(hakDir, name), Array.Empty<byte>());
        }
        return hakDir;
    }

    #endregion

    #region Basic Resolution

    [Fact]
    public void ResolveModuleHakPaths_ReturnsEmpty_WhenModuleDirIsNull()
    {
        var result = ModuleHakResolver.ResolveModuleHakPaths(null!, new[] { "somepath" });
        Assert.Empty(result);
    }

    [Fact]
    public void ResolveModuleHakPaths_ReturnsEmpty_WhenModuleDirDoesNotExist()
    {
        var result = ModuleHakResolver.ResolveModuleHakPaths("/nonexistent/path", new[] { "somepath" });
        Assert.Empty(result);
    }

    [Fact]
    public void ResolveModuleHakPaths_ReturnsEmpty_WhenNoModuleIfo()
    {
        var emptyDir = Path.Combine(_testDir, "empty");
        Directory.CreateDirectory(emptyDir);

        var result = ModuleHakResolver.ResolveModuleHakPaths(emptyDir, new[] { "somepath" });
        Assert.Empty(result);
    }

    [Fact]
    public void ResolveModuleHakPaths_ReturnsEmpty_WhenNoHaksInIfo()
    {
        var moduleDir = CreateModuleDir(new List<string>());
        var hakDir = CreateHakDir("something.hak");

        var result = ModuleHakResolver.ResolveModuleHakPaths(moduleDir, new[] { hakDir });
        Assert.Empty(result);
    }

    [Fact]
    public void ResolveModuleHakPaths_ResolvesHakNamesToFilePaths()
    {
        var moduleDir = CreateModuleDir(new List<string> { "cep2_top", "cep2_add" });
        var hakDir = CreateHakDir("cep2_top.hak", "cep2_add.hak", "other.hak");

        var result = ModuleHakResolver.ResolveModuleHakPaths(moduleDir, new[] { hakDir });

        Assert.Equal(2, result.Count);
        Assert.EndsWith("cep2_top.hak", result[0]);
        Assert.EndsWith("cep2_add.hak", result[1]);
    }

    [Fact]
    public void ResolveModuleHakPaths_SkipsMissingHaks()
    {
        var moduleDir = CreateModuleDir(new List<string> { "exists", "missing" });
        var hakDir = CreateHakDir("exists.hak");

        var result = ModuleHakResolver.ResolveModuleHakPaths(moduleDir, new[] { hakDir });

        Assert.Single(result);
        Assert.EndsWith("exists.hak", result[0]);
    }

    [Fact]
    public void ResolveModuleHakPaths_PreservesIfoOrder()
    {
        var moduleDir = CreateModuleDir(new List<string> { "third", "first", "second" });
        var hakDir = CreateHakDir("first.hak", "second.hak", "third.hak");

        var result = ModuleHakResolver.ResolveModuleHakPaths(moduleDir, new[] { hakDir });

        Assert.Equal(3, result.Count);
        Assert.EndsWith("third.hak", result[0]);
        Assert.EndsWith("first.hak", result[1]);
        Assert.EndsWith("second.hak", result[2]);
    }

    #endregion

    #region Multiple Search Directories

    [Fact]
    public void ResolveModuleHakPaths_SearchesMultipleDirectories()
    {
        var moduleDir = CreateModuleDir(new List<string> { "hak_a", "hak_b" });
        var hakDir1 = CreateHakDir("hak_a.hak");
        var hakDir2 = CreateHakDir("hak_b.hak");

        var result = ModuleHakResolver.ResolveModuleHakPaths(moduleDir, new[] { hakDir1, hakDir2 });

        Assert.Equal(2, result.Count);
        Assert.Contains(result, p => p.EndsWith("hak_a.hak"));
        Assert.Contains(result, p => p.EndsWith("hak_b.hak"));
    }

    [Fact]
    public void ResolveModuleHakPaths_FirstSearchDirWins()
    {
        var moduleDir = CreateModuleDir(new List<string> { "shared" });
        var hakDir1 = CreateHakDir("shared.hak");
        var hakDir2 = CreateHakDir("shared.hak");

        var result = ModuleHakResolver.ResolveModuleHakPaths(moduleDir, new[] { hakDir1, hakDir2 });

        Assert.Single(result);
        Assert.StartsWith(hakDir1, result[0]);
    }

    [Fact]
    public void ResolveModuleHakPaths_SkipsNonexistentSearchDirs()
    {
        var moduleDir = CreateModuleDir(new List<string> { "test" });
        var hakDir = CreateHakDir("test.hak");

        var result = ModuleHakResolver.ResolveModuleHakPaths(moduleDir, new[] { "/nonexistent", hakDir });

        Assert.Single(result);
        Assert.EndsWith("test.hak", result[0]);
    }

    [Fact]
    public void ResolveModuleHakPaths_ReturnsEmpty_WhenNoValidSearchPaths()
    {
        var moduleDir = CreateModuleDir(new List<string> { "test" });

        var result = ModuleHakResolver.ResolveModuleHakPaths(moduleDir, new[] { "/nonexistent1", "/nonexistent2" });
        Assert.Empty(result);
    }

    #endregion

    #region Case Sensitivity

    [Fact]
    public void ResolveModuleHakPaths_CaseInsensitiveHakName()
    {
        // module.ifo says "CEP2_Top" but file on disk may have different casing
        var moduleDir = CreateModuleDir(new List<string> { "CEP2_Top" });
        var hakDir = CreateHakDir("cep2_top.hak");

        var result = ModuleHakResolver.ResolveModuleHakPaths(moduleDir, new[] { hakDir });

        Assert.Single(result);
        // On Windows, case-insensitive FS finds the file regardless of casing
        // On Linux, the fallback enumeration handles case mismatch
        Assert.EndsWith(".hak", result[0]);
        Assert.Contains("cep2_top", result[0], StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void ResolveModuleHakPaths_EmptySearchPaths()
    {
        var moduleDir = CreateModuleDir(new List<string> { "test" });

        var result = ModuleHakResolver.ResolveModuleHakPaths(moduleDir, Array.Empty<string>());
        Assert.Empty(result);
    }

    [Fact]
    public void ResolveModuleHakPaths_EmptyString_ModulePath()
    {
        var result = ModuleHakResolver.ResolveModuleHakPaths("", new[] { "somepath" });
        Assert.Empty(result);
    }

    #endregion
}
