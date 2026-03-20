using System;
using System.IO;
using Radoub.UI.Services;
using Xunit;

namespace Radoub.UI.Tests;

/// <summary>
/// Tests for ProjectPathResolver — resolves --project + --file to absolute paths (#1781).
/// Uses temp directories to simulate NWN module structure.
/// </summary>
public class ProjectPathResolverTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _modulesDir;

    public ProjectPathResolverTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"radoub-test-{Guid.NewGuid():N}");
        _modulesDir = Path.Combine(_tempDir, "modules");
        Directory.CreateDirectory(_modulesDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void Resolve_AbsoluteFilePath_ReturnsAsIs()
    {
        var absPath = Path.Combine(_tempDir, "test.dlg");
        File.WriteAllText(absPath, "");

        var result = ProjectPathResolver.ResolveFilePath("LNS", absPath, _modulesDir);
        Assert.Equal(absPath, result);
    }

    [Fact]
    public void Resolve_ProjectAndRelativeFile_CombinesPath()
    {
        var moduleDir = Path.Combine(_modulesDir, "LNS");
        Directory.CreateDirectory(moduleDir);
        var expectedPath = Path.Combine(moduleDir, "dialog.dlg");
        File.WriteAllText(expectedPath, "");

        var result = ProjectPathResolver.ResolveFilePath("LNS", "dialog.dlg", _modulesDir);
        Assert.Equal(expectedPath, result);
    }

    [Fact]
    public void Resolve_ProjectOnly_NoFile_ReturnsNull()
    {
        var moduleDir = Path.Combine(_modulesDir, "LNS");
        Directory.CreateDirectory(moduleDir);

        var result = ProjectPathResolver.ResolveFilePath("LNS", null, _modulesDir);
        Assert.Null(result);
    }

    [Fact]
    public void Resolve_ProjectAndFile_FileDoesNotExist_ReturnsResolvedPath()
    {
        var moduleDir = Path.Combine(_modulesDir, "LNS");
        Directory.CreateDirectory(moduleDir);

        var result = ProjectPathResolver.ResolveFilePath("LNS", "missing.dlg", _modulesDir);
        var expectedPath = Path.Combine(moduleDir, "missing.dlg");
        Assert.Equal(expectedPath, result);
    }

    [Fact]
    public void Resolve_NoProject_ReturnsNull()
    {
        var result = ProjectPathResolver.ResolveFilePath(null, "dialog.dlg", _modulesDir);
        Assert.Null(result);
    }

    [Fact]
    public void Resolve_ProjectDoesNotExist_ReturnsResolvedPath()
    {
        var result = ProjectPathResolver.ResolveFilePath("NoSuchMod", "dialog.dlg", _modulesDir);
        var expectedPath = Path.Combine(_modulesDir, "NoSuchMod", "dialog.dlg");
        Assert.Equal(expectedPath, result);
    }

    [Fact]
    public void ResolveModulePath_WithProject_ReturnsModuleDir()
    {
        var moduleDir = Path.Combine(_modulesDir, "LNS");
        Directory.CreateDirectory(moduleDir);

        var result = ProjectPathResolver.ResolveModulePath("LNS", _modulesDir);
        Assert.Equal(moduleDir, result);
    }

    [Fact]
    public void ResolveModulePath_NoProject_ReturnsNull()
    {
        var result = ProjectPathResolver.ResolveModulePath(null, _modulesDir);
        Assert.Null(result);
    }
}
