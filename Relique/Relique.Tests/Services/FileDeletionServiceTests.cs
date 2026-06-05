using System.IO;
using System.Threading.Tasks;
using ItemEditor.Services;
using Radoub.UI.Services.Search;
using Xunit;

namespace ItemEditor.Tests.Services;

/// <summary>
/// Tests for FileDeletionService — Relique item delete must back up the file
/// before removing it (#2347). Previously delete was a bare File.Delete with no
/// backup, making it unrecoverable despite the "cannot be undone" confirm text.
/// </summary>
public class FileDeletionServiceTests : System.IDisposable
{
    private readonly string _root;
    private readonly string _backupRoot;

    public FileDeletionServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"reldel-{System.Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
        _backupRoot = Path.Combine(_root, "Backups");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task DeleteWithBackupAsync_RemovesFile_AndLeavesRecoverableBackup()
    {
        var filePath = Path.Combine(_root, "louis.uti");
        var content = "ORIGINAL ITEM BYTES";
        await File.WriteAllTextAsync(filePath, content);

        var backupService = new BackupService(_backupRoot);
        var manifest = await FileDeletionService.DeleteWithBackupAsync(filePath, "LNS_DLG", backupService);

        // File gone from disk
        Assert.False(File.Exists(filePath));

        // A recoverable backup exists with the original content
        Assert.Single(manifest.Entries);
        var backupPath = manifest.Entries[0].BackupPath;
        Assert.True(File.Exists(backupPath));
        Assert.Equal(content, await File.ReadAllTextAsync(backupPath));
    }

    [Fact]
    public async Task DeleteWithBackupAsync_BackupRestores_OriginalFile()
    {
        var filePath = Path.Combine(_root, "apples.uti");
        var content = "RECOVER ME";
        await File.WriteAllTextAsync(filePath, content);

        var backupService = new BackupService(_backupRoot);
        var manifest = await FileDeletionService.DeleteWithBackupAsync(filePath, "LNS_DLG", backupService);

        var restored = await backupService.RestoreAsync(manifest);

        Assert.True(restored);
        Assert.True(File.Exists(filePath));
        Assert.Equal(content, await File.ReadAllTextAsync(filePath));
    }
}
