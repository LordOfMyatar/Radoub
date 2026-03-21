using System;
using System.IO;
using Radoub.UI.Services;
using Xunit;

namespace Radoub.UI.Tests;

[Collection("FileSessionLock")]
public class FileSessionLockServiceTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _testFile;

    public FileSessionLockServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "radoub-lock-tests-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);
        _testFile = Path.Combine(_testDir, "test.utm");
        File.WriteAllText(_testFile, "test");

        // Clean up any prior state
        FileSessionLockService.ReleaseAllLocks();
    }

    public void Dispose()
    {
        FileSessionLockService.ReleaseAllLocks();
        try { Directory.Delete(_testDir, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    [Fact]
    public void AcquireLock_CreatesLockFile()
    {
        var result = FileSessionLockService.AcquireLock(_testFile, "Fence");
        Assert.Equal(LockResult.Acquired, result);
        Assert.True(File.Exists(FileSessionLockService.GetLockFilePath(_testFile)));
    }

    [Fact]
    public void AcquireLock_SameProcess_ReturnsAlreadyOwned()
    {
        FileSessionLockService.AcquireLock(_testFile, "Fence");
        var result = FileSessionLockService.AcquireLock(_testFile, "Fence");
        Assert.Equal(LockResult.AlreadyOwned, result);
    }

    [Fact]
    public void ReleaseLock_RemovesLockFile()
    {
        FileSessionLockService.AcquireLock(_testFile, "Fence");
        FileSessionLockService.ReleaseLock(_testFile);
        Assert.False(File.Exists(FileSessionLockService.GetLockFilePath(_testFile)));
    }

    [Fact]
    public void CheckLock_NoLock_ReturnsNull()
    {
        var info = FileSessionLockService.CheckLock(_testFile);
        Assert.Null(info);
    }

    [Fact]
    public void CheckLock_WithLock_ReturnsInfo()
    {
        FileSessionLockService.AcquireLock(_testFile, "Fence");
        var info = FileSessionLockService.CheckLock(_testFile);
        Assert.NotNull(info);
        Assert.Equal("Fence", info!.ToolName);
    }

    [Fact]
    public void AcquireLock_StaleLock_CleanedUp()
    {
        // Write a lock file with a PID that cannot exist
        var lockPath = FileSessionLockService.GetLockFilePath(_testFile);
        File.WriteAllText(lockPath, """{"pid":2147483647,"tool":"OtherTool","timestamp":"2026-01-01T00:00:00Z","machine":"TEST"}""");

        var result = FileSessionLockService.AcquireLock(_testFile, "Fence");
        Assert.Equal(LockResult.Acquired, result);
    }

    [Fact]
    public void GetLockFilePath_ReturnsExpectedSuffix()
    {
        var path = FileSessionLockService.GetLockFilePath("/some/path/file.dlg");
        Assert.EndsWith(".radoub.lock", path);
    }

    [Fact]
    public void ReleaseAllLocks_CleansUpAll()
    {
        var file2 = Path.Combine(_testDir, "test2.utm");
        File.WriteAllText(file2, "test2");

        FileSessionLockService.AcquireLock(_testFile, "Fence");
        FileSessionLockService.AcquireLock(file2, "Fence");

        FileSessionLockService.ReleaseAllLocks();

        Assert.False(File.Exists(FileSessionLockService.GetLockFilePath(_testFile)));
        Assert.False(File.Exists(FileSessionLockService.GetLockFilePath(file2)));
    }

    [Fact]
    public void AcquireLock_NullOrEmptyPath_DoesNotThrow()
    {
        var result = FileSessionLockService.AcquireLock("", "Fence");
        Assert.Equal(LockResult.Acquired, result);

        var result2 = FileSessionLockService.AcquireLock(null!, "Fence");
        Assert.Equal(LockResult.Acquired, result2);
    }

    [Fact]
    public void AcquireLock_WritesProcessName()
    {
        FileSessionLockService.AcquireLock(_testFile, "Fence");
        var info = FileSessionLockService.CheckLock(_testFile);
        Assert.NotNull(info);
        Assert.False(string.IsNullOrEmpty(info!.ProcessName));
    }

    [Fact]
    public void AcquireLock_StaleLock_WrongProcessName_CleanedUp()
    {
        // Write a lock with the current PID but a fake process name.
        // The PID is alive (it's us!) but the process name won't match,
        // so the lock should be treated as stale (PID reuse scenario).
        var lockPath = FileSessionLockService.GetLockFilePath(Path.GetFullPath(_testFile));
        var fakeLock = $"{{\"pid\":{System.Environment.ProcessId},\"tool\":\"OtherTool\",\"processName\":\"definitely_not_a_real_process_xyzzy\",\"timestamp\":\"2026-01-01T00:00:00Z\",\"machine\":\"TEST\"}}";
        File.WriteAllText(lockPath, fakeLock);

        var result = FileSessionLockService.AcquireLock(_testFile, "Fence");
        Assert.Equal(LockResult.Acquired, result);
    }
}
