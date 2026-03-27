using DialogEditor.Services;
using Xunit;

namespace DialogEditor.Tests.Services;

public class ModuleDirectoryResolverTests
{
    [Fact]
    public void ResolveModuleDirectory_PrefersCurrentModulePath_WhenSetAndExists()
    {
        // Use temp directory as a valid existing path
        var modulePath = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar);
        var filePath = Path.Combine(Path.GetTempPath(), "subdir", "test.dlg");

        var result = ModuleDirectoryResolver.Resolve(modulePath, filePath);

        Assert.Equal(modulePath, result);
    }

    [Fact]
    public void ResolveModuleDirectory_FallsBackToFilePath_WhenModulePathNull()
    {
        var tempDir = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar);
        var filePath = Path.Combine(tempDir, "test.dlg");

        var result = ModuleDirectoryResolver.Resolve(null, filePath);

        Assert.Equal(tempDir, result);
    }

    [Fact]
    public void ResolveModuleDirectory_FallsBackToFilePath_WhenModulePathEmpty()
    {
        var tempDir = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar);
        var filePath = Path.Combine(tempDir, "test.dlg");

        var result = ModuleDirectoryResolver.Resolve("", filePath);

        Assert.Equal(tempDir, result);
    }

    [Fact]
    public void ResolveModuleDirectory_FallsBackToFilePath_WhenModulePathDoesNotExist()
    {
        var nonExistentPath = Path.Combine(Path.GetTempPath(), "nonexistent_" + Guid.NewGuid().ToString("N"));
        var tempDir = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar);
        var filePath = Path.Combine(tempDir, "test.dlg");

        var result = ModuleDirectoryResolver.Resolve(nonExistentPath, filePath);

        Assert.Equal(tempDir, result);
    }

    [Fact]
    public void ResolveModuleDirectory_ReturnsNull_WhenBothNull()
    {
        var result = ModuleDirectoryResolver.Resolve(null, null);

        Assert.Null(result);
    }

    [Fact]
    public void ResolveModuleDirectory_ReturnsNull_WhenBothEmpty()
    {
        var result = ModuleDirectoryResolver.Resolve("", "");

        Assert.Null(result);
    }

    [Fact]
    public void ResolveModuleDirectory_UsesModulePath_WhenNoFileOpen()
    {
        var modulePath = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar);

        var result = ModuleDirectoryResolver.Resolve(modulePath, null);

        Assert.Equal(modulePath, result);
    }

    [Fact]
    public void ResolveModuleDirectory_ResolvesModFileToWorkingDirectory()
    {
        // Create temp structure: modules/TestMod.mod + modules/TestMod/
        var tempRoot = Path.Combine(Path.GetTempPath(), "ModResolverTest_" + Guid.NewGuid().ToString("N"));
        var modFile = Path.Combine(tempRoot, "TestMod.mod");
        var workingDir = Path.Combine(tempRoot, "TestMod");

        try
        {
            Directory.CreateDirectory(workingDir);
            File.WriteAllText(modFile, "fake mod");

            var result = ModuleDirectoryResolver.Resolve(modFile, null);

            Assert.Equal(workingDir, result);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, true);
        }
    }

    [Fact]
    public void ResolveModuleDirectory_ModFileWithoutWorkingDir_FallsBack()
    {
        // Create temp .mod file without unpacked directory
        var tempRoot = Path.Combine(Path.GetTempPath(), "ModResolverTest_" + Guid.NewGuid().ToString("N"));
        var modFile = Path.Combine(tempRoot, "TestMod.mod");
        var tempDir = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar);
        var filePath = Path.Combine(tempDir, "test.dlg");

        try
        {
            Directory.CreateDirectory(tempRoot);
            File.WriteAllText(modFile, "fake mod");

            var result = ModuleDirectoryResolver.Resolve(modFile, filePath);

            // Should fall back to file's parent directory
            Assert.Equal(tempDir, result);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, true);
        }
    }
}
