using RadoubLauncher.Services;

namespace Trebuchet.Tests;

/// <summary>
/// Tests for ModuleFileLockService — detects whether a .mod file
/// is locked by another process (e.g., Aurora Toolset).
/// </summary>
public class ModuleFileLockServiceTests : IDisposable
{
    private readonly string _testDirectory;

    public ModuleFileLockServiceTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"LockTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        try { Directory.Delete(_testDirectory, true); } catch { }
    }

    [Fact]
    public void IsFileLocked_NonExistentFile_ReturnsFalse()
    {
        var path = Path.Combine(_testDirectory, "nonexistent.mod");
        Assert.False(ModuleFileLockService.IsFileLocked(path));
    }

    [Fact]
    public void IsFileLocked_UnlockedFile_ReturnsFalse()
    {
        var path = Path.Combine(_testDirectory, "test.mod");
        File.WriteAllBytes(path, new byte[] { 0x01, 0x02, 0x03 });

        Assert.False(ModuleFileLockService.IsFileLocked(path));
    }

    [Fact]
    public void IsFileLocked_FileLockedExclusive_ReturnsTrue()
    {
        var path = Path.Combine(_testDirectory, "locked.mod");
        File.WriteAllBytes(path, new byte[] { 0x01, 0x02, 0x03 });

        // Simulate Aurora holding the file with no sharing
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);

        Assert.True(ModuleFileLockService.IsFileLocked(path));
    }

    [Fact]
    public void IsFileLocked_FileLockedReadShareOnly_ReturnsTrue()
    {
        var path = Path.Combine(_testDirectory, "locked_readshare.mod");
        File.WriteAllBytes(path, new byte[] { 0x01, 0x02, 0x03 });

        // Simulate Aurora holding the file with read sharing (still blocks writes)
        using var fs = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);

        Assert.True(ModuleFileLockService.IsFileLocked(path));
    }

    [Fact]
    public void IsFileLocked_FileSharedForReadWrite_ReturnsFalse()
    {
        var path = Path.Combine(_testDirectory, "shared.mod");
        File.WriteAllBytes(path, new byte[] { 0x01, 0x02, 0x03 });

        // File open with full sharing — should not be considered locked
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

        Assert.False(ModuleFileLockService.IsFileLocked(path));
    }

    [Fact]
    public void IsFileLocked_NullOrEmptyPath_ReturnsFalse()
    {
        Assert.False(ModuleFileLockService.IsFileLocked(null));
        Assert.False(ModuleFileLockService.IsFileLocked(""));
    }
}
