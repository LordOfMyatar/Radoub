using Radoub.UI.Services;
using Xunit;

namespace Radoub.UI.Tests.Services;

public class ItemEditorLauncherTests : IDisposable
{
    private readonly string _tempDir;

    public ItemEditorLauncherTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ItemEditorLauncherTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public void ResolveUtiPath_FileExists_ReturnsFullPath()
    {
        File.WriteAllBytes(Path.Combine(_tempDir, "my_sword.uti"), [0]);
        var result = ItemEditorLauncher.ResolveUtiPath("my_sword", _tempDir);
        Assert.NotNull(result);
        Assert.EndsWith("my_sword.uti", result);
    }

    [Fact]
    public void ResolveUtiPath_FileNotFound_ReturnsNull()
    {
        var result = ItemEditorLauncher.ResolveUtiPath("nonexistent", _tempDir);
        Assert.Null(result);
    }

    [Fact]
    public void ResolveUtiPath_CaseInsensitive_FindsFile()
    {
        File.WriteAllBytes(Path.Combine(_tempDir, "My_Sword.uti"), [0]);
        var result = ItemEditorLauncher.ResolveUtiPath("my_sword", _tempDir);
        Assert.NotNull(result);
    }

    [Fact]
    public void ResolveUtiPath_EmptyResRef_ReturnsNull()
    {
        var result = ItemEditorLauncher.ResolveUtiPath("", _tempDir);
        Assert.Null(result);
    }

    [Fact]
    public void ResolveUtiPath_NullDirectory_ReturnsNull()
    {
        var result = ItemEditorLauncher.ResolveUtiPath("my_sword", null!);
        Assert.Null(result);
    }

    [Fact]
    public void ResolveUtiPath_NonexistentDirectory_ReturnsNull()
    {
        var result = ItemEditorLauncher.ResolveUtiPath("my_sword", "/nonexistent/path");
        Assert.Null(result);
    }
}
