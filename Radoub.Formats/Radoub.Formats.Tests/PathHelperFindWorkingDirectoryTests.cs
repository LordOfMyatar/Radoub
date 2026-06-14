using System;
using System.IO;
using Radoub.Formats.Common;
using Xunit;

namespace Radoub.Formats.Tests;

/// <summary>
/// Tests for PathHelper.FindWorkingDirectoryWithFallbacks (#2355).
/// Covers the .mod fallback cascade ({name}, temp0, temp1) and the
/// optional requireModuleIfo gate used by Parley.
/// </summary>
public class PathHelperFindWorkingDirectoryTests : IDisposable
{
    private readonly string _root;

    public PathHelperFindWorkingDirectoryTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"PathHelperWD_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
        catch { /* ignore */ }
    }

    private string CreateModFile(string name = "mymod")
    {
        var modPath = Path.Combine(_root, name + ".mod");
        File.WriteAllText(modPath, "fake mod");
        return modPath;
    }

    [Fact]
    public void NullOrEmpty_ReturnsNull()
    {
        Assert.Null(PathHelper.FindWorkingDirectoryWithFallbacks(null));
        Assert.Null(PathHelper.FindWorkingDirectoryWithFallbacks(""));
    }

    [Fact]
    public void ModFile_WithNamedWorkingDir_ReturnsNamedDir()
    {
        var mod = CreateModFile();
        var named = Path.Combine(_root, "mymod");
        Directory.CreateDirectory(named);

        Assert.Equal(named, PathHelper.FindWorkingDirectoryWithFallbacks(mod));
    }

    [Fact]
    public void ModFile_OnlyTemp0_ReturnsTemp0()
    {
        var mod = CreateModFile();
        var temp0 = Path.Combine(_root, "temp0");
        Directory.CreateDirectory(temp0);

        Assert.Equal(temp0, PathHelper.FindWorkingDirectoryWithFallbacks(mod));
    }

    [Fact]
    public void ModFile_OnlyTemp1_ReturnsTemp1()
    {
        var mod = CreateModFile();
        var temp1 = Path.Combine(_root, "temp1");
        Directory.CreateDirectory(temp1);

        Assert.Equal(temp1, PathHelper.FindWorkingDirectoryWithFallbacks(mod));
    }

    [Fact]
    public void ModFile_NamedDirWins_OverTempDirs()
    {
        var mod = CreateModFile();
        var named = Path.Combine(_root, "mymod");
        Directory.CreateDirectory(named);
        Directory.CreateDirectory(Path.Combine(_root, "temp0"));
        Directory.CreateDirectory(Path.Combine(_root, "temp1"));

        Assert.Equal(named, PathHelper.FindWorkingDirectoryWithFallbacks(mod));
    }

    [Fact]
    public void ModFile_NoWorkingDir_ReturnsNull()
    {
        var mod = CreateModFile();
        Assert.Null(PathHelper.FindWorkingDirectoryWithFallbacks(mod));
    }

    [Fact]
    public void DirectoryPath_ReturnsItself()
    {
        Assert.Equal(_root, PathHelper.FindWorkingDirectoryWithFallbacks(_root));
    }

    [Fact]
    public void MissingPath_ReturnsNull()
    {
        var missing = Path.Combine(_root, "does_not_exist");
        Assert.Null(PathHelper.FindWorkingDirectoryWithFallbacks(missing));
    }

    [Fact]
    public void RequireModuleIfo_SkipsDirWithoutModuleIfo()
    {
        var mod = CreateModFile();
        var named = Path.Combine(_root, "mymod");
        Directory.CreateDirectory(named); // no module.ifo inside

        // Loose (default): dir matches.
        Assert.Equal(named, PathHelper.FindWorkingDirectoryWithFallbacks(mod));

        // Strict (Parley): dir lacks module.ifo, so no match.
        Assert.Null(PathHelper.FindWorkingDirectoryWithFallbacks(mod, requireModuleIfo: true));
    }

    [Fact]
    public void RequireModuleIfo_ReturnsDirWithModuleIfo()
    {
        var mod = CreateModFile();
        var named = Path.Combine(_root, "mymod");
        Directory.CreateDirectory(named);
        File.WriteAllText(Path.Combine(named, "module.ifo"), "ifo");

        Assert.Equal(named, PathHelper.FindWorkingDirectoryWithFallbacks(mod, requireModuleIfo: true));
    }
}
