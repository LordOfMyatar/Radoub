using System;
using System.IO;
using RadoubLauncher.Services;
using Xunit;

namespace Trebuchet.Tests;

public class ModulePathHelperTests : IDisposable
{
    private readonly string _tempDir;

    public ModulePathHelperTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ModPathTest_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public void IsCurrentModuleArchive_TrueWhenTargetIsOpenModuleMod()
    {
        var modPath = Path.Combine(_tempDir, "mymodule.mod");
        File.WriteAllText(modPath, "x");

        Assert.True(ModulePathHelper.IsCurrentModuleArchive(modPath, modPath));
    }

    [Fact]
    public void IsCurrentModuleArchive_TrueWhenCurrentIsWorkingDir()
    {
        // CurrentModulePath can be the unpacked working dir; the target is the sibling .mod.
        var workingDir = Path.Combine(_tempDir, "mymodule");
        Directory.CreateDirectory(workingDir);
        var modPath = Path.Combine(_tempDir, "mymodule.mod");
        File.WriteAllText(modPath, "x");

        Assert.True(ModulePathHelper.IsCurrentModuleArchive(modPath, workingDir));
    }

    [Fact]
    public void IsCurrentModuleArchive_FalseForDifferentArchive()
    {
        var modPath = Path.Combine(_tempDir, "mymodule.mod");
        File.WriteAllText(modPath, "x");
        var otherErf = Path.Combine(_tempDir, "other.erf");
        File.WriteAllText(otherErf, "x");

        Assert.False(ModulePathHelper.IsCurrentModuleArchive(otherErf, modPath));
    }

    [Fact]
    public void IsCurrentModuleArchive_CaseInsensitive()
    {
        var modPath = Path.Combine(_tempDir, "mymodule.mod");
        File.WriteAllText(modPath, "x");
        var upper = Path.Combine(_tempDir, "MYMODULE.MOD");

        Assert.True(ModulePathHelper.IsCurrentModuleArchive(upper, modPath));
    }

    [Fact]
    public void IsCurrentModuleArchive_FalseWhenNoModuleOpen()
    {
        var modPath = Path.Combine(_tempDir, "mymodule.mod");
        File.WriteAllText(modPath, "x");

        Assert.False(ModulePathHelper.IsCurrentModuleArchive(modPath, null));
        Assert.False(ModulePathHelper.IsCurrentModuleArchive(modPath, ""));
    }
}
