using Radoub.UI.Services;
using Xunit;

namespace Radoub.UI.Tests.Services;

public class BackupCleanupServiceTests : IDisposable
{
    private readonly string _tempRoot;

    public BackupCleanupServiceTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "RadoubBackupTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, true);
    }

    private string CreateBatchBackup(string moduleName, DateTime timestamp)
    {
        var dir = Path.Combine(_tempRoot, moduleName, timestamp.ToString("yyyyMMdd_HHmmss"));
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "test.dlg.bak"), "backup data");
        return dir;
    }

    private string CreateSearchReplaceBackup(string fileName, DateTime timestamp)
    {
        var dir = Path.Combine(_tempRoot, "SearchReplace");
        Directory.CreateDirectory(dir);
        var ext = Path.GetExtension(fileName);
        var name = Path.GetFileNameWithoutExtension(fileName);
        var path = Path.Combine(dir, $"{name}_{timestamp:yyyyMMdd_HHmmss}{ext}");
        File.WriteAllText(path, "backup data");
        return path;
    }

    [Fact]
    public void CleanupExpiredBackups_DeletesOldBatchBackups()
    {
        var now = DateTime.Now;
        CreateBatchBackup("LNS", now.AddDays(-60));
        CreateBatchBackup("LNS", now.AddDays(-5));

        BackupCleanupService.CleanupExpiredBackups(30, _tempRoot);

        var remaining = Directory.GetDirectories(Path.Combine(_tempRoot, "LNS"));
        Assert.Single(remaining);
    }

    [Fact]
    public void CleanupExpiredBackups_DeletesOldSearchReplaceBackups()
    {
        var now = DateTime.Now;
        CreateSearchReplaceBackup("test.dlg", now.AddDays(-60));
        CreateSearchReplaceBackup("other.dlg", now.AddDays(-5));

        BackupCleanupService.CleanupExpiredBackups(30, _tempRoot);

        var remaining = Directory.GetFiles(Path.Combine(_tempRoot, "SearchReplace"));
        Assert.Single(remaining);
    }

    [Fact]
    public void CleanupExpiredBackups_RemovesEmptyModuleDirectories()
    {
        var now = DateTime.Now;
        CreateBatchBackup("OldModule", now.AddDays(-60));

        BackupCleanupService.CleanupExpiredBackups(30, _tempRoot);

        Assert.False(Directory.Exists(Path.Combine(_tempRoot, "OldModule")));
    }

    [Fact]
    public void CleanupExpiredBackups_KeepsRecentBackups()
    {
        var now = DateTime.Now;
        CreateBatchBackup("LNS", now.AddDays(-1));
        CreateBatchBackup("LNS", now.AddDays(-10));
        CreateBatchBackup("LNS", now.AddDays(-29));

        BackupCleanupService.CleanupExpiredBackups(30, _tempRoot);

        var remaining = Directory.GetDirectories(Path.Combine(_tempRoot, "LNS"));
        Assert.Equal(3, remaining.Length);
    }

    [Fact]
    public void CleanupExpiredBackups_HandlesNonExistentDirectory()
    {
        var nonExistent = Path.Combine(_tempRoot, "nonexistent");

        // Should not throw
        BackupCleanupService.CleanupExpiredBackups(30, nonExistent);
    }

    [Fact]
    public void DeleteAllBackups_RemovesEverything()
    {
        var now = DateTime.Now;
        CreateBatchBackup("LNS", now.AddDays(-5));
        CreateBatchBackup("LNS", now.AddDays(-60));
        CreateSearchReplaceBackup("test.dlg", now.AddDays(-1));

        BackupCleanupService.DeleteAllBackups(_tempRoot);

        // Directory should exist but be empty
        Assert.True(Directory.Exists(_tempRoot));
        Assert.Empty(Directory.GetDirectories(_tempRoot));
        Assert.Empty(Directory.GetFiles(_tempRoot));
    }

    [Fact]
    public void DeleteAllBackups_HandlesNonExistentDirectory()
    {
        var nonExistent = Path.Combine(_tempRoot, "nonexistent");

        // Should not throw
        BackupCleanupService.DeleteAllBackups(nonExistent);
    }

    [Fact]
    public void GetBackupSummary_ReturnsCorrectCounts()
    {
        var now = DateTime.Now;
        CreateBatchBackup("LNS", now.AddDays(-5));
        CreateBatchBackup("LNS", now.AddDays(-10));
        CreateSearchReplaceBackup("test.dlg", now.AddDays(-1));

        var (fileCount, totalBytes) = BackupCleanupService.GetBackupSummary(_tempRoot);

        // 2 batch backup files + 1 search replace file = 3
        Assert.Equal(3, fileCount);
        Assert.True(totalBytes > 0);
    }

    [Fact]
    public void GetBackupSummary_ReturnsZeroForEmptyDirectory()
    {
        var (fileCount, totalBytes) = BackupCleanupService.GetBackupSummary(_tempRoot);

        Assert.Equal(0, fileCount);
        Assert.Equal(0, totalBytes);
    }

    [Fact]
    public void GetBackupSummary_ReturnsZeroForNonExistentDirectory()
    {
        var nonExistent = Path.Combine(_tempRoot, "nonexistent");

        var (fileCount, totalBytes) = BackupCleanupService.GetBackupSummary(nonExistent);

        Assert.Equal(0, fileCount);
        Assert.Equal(0, totalBytes);
    }
}
