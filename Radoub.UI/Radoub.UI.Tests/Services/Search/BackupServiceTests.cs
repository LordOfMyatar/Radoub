using Radoub.UI.Services.Search;
using Xunit;

namespace Radoub.UI.Tests.Services.Search;

public class BackupServiceTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _backupDir;
    private readonly BackupService _service;

    public BackupServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"radoub_backup_test_{Guid.NewGuid():N}");
        _backupDir = Path.Combine(_testDir, "Backups");
        Directory.CreateDirectory(_testDir);

        _service = new BackupService(_backupDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    private string CreateTestFile(string name, string content)
    {
        var dir = Path.Combine(_testDir, "module");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, name);
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public async Task BackupFiles_CopiesFilesToBackupLocation()
    {
        var file1 = CreateTestFile("test.dlg", "dialog content");
        var file2 = CreateTestFile("creature.utc", "creature content");

        var manifest = await _service.BackupFilesAsync(new[] { file1, file2 }, "TestModule");

        Assert.NotNull(manifest);
        Assert.Equal(2, manifest.Entries.Count);
        Assert.All(manifest.Entries, e => Assert.True(File.Exists(e.BackupPath)));
    }

    [Fact]
    public async Task BackupFiles_CreatesManifestWithHashes()
    {
        var file1 = CreateTestFile("test.dlg", "dialog content");

        var manifest = await _service.BackupFilesAsync(new[] { file1 }, "TestModule");

        var entry = Assert.Single(manifest.Entries);
        Assert.Equal(file1, entry.OriginalPath);
        Assert.NotEmpty(entry.Sha256Hash);
        Assert.True(entry.Sha256Hash.Length == 64); // SHA256 hex string
    }

    [Fact]
    public async Task BackupFiles_BackupLocationUnderModuleName()
    {
        var file1 = CreateTestFile("test.dlg", "dialog content");

        var manifest = await _service.BackupFilesAsync(new[] { file1 }, "MyModule");

        Assert.Contains("MyModule", manifest.BackupDirectory);
        Assert.True(Directory.Exists(manifest.BackupDirectory));
    }

    [Fact]
    public async Task BackupFiles_TimestampInPath()
    {
        var file1 = CreateTestFile("test.dlg", "dialog content");

        var manifest = await _service.BackupFilesAsync(new[] { file1 }, "TestModule");

        // Backup dir should contain a timestamp folder
        var dirName = Path.GetFileName(manifest.BackupDirectory);
        Assert.Matches(@"\d{8}_\d{6}", dirName);
    }

    [Fact]
    public async Task BackupFiles_PreservesContent()
    {
        var content = "original dialog content here";
        var file1 = CreateTestFile("test.dlg", content);

        var manifest = await _service.BackupFilesAsync(new[] { file1 }, "TestModule");

        var backupContent = await File.ReadAllTextAsync(manifest.Entries[0].BackupPath);
        Assert.Equal(content, backupContent);
    }

    [Fact]
    public async Task Restore_CopiesBackupsToOriginalLocations()
    {
        var file1 = CreateTestFile("test.dlg", "original");

        var manifest = await _service.BackupFilesAsync(new[] { file1 }, "TestModule");

        // Modify the original
        await File.WriteAllTextAsync(file1, "modified");
        Assert.Equal("modified", await File.ReadAllTextAsync(file1));

        // Restore
        var restored = await _service.RestoreAsync(manifest);

        Assert.True(restored);
        Assert.Equal("original", await File.ReadAllTextAsync(file1));
    }

    [Fact]
    public async Task Restore_VerifiesHashIntegrity()
    {
        var file1 = CreateTestFile("test.dlg", "original");

        var manifest = await _service.BackupFilesAsync(new[] { file1 }, "TestModule");

        // Corrupt the backup
        await File.WriteAllTextAsync(manifest.Entries[0].BackupPath, "corrupted");

        // Restore should fail due to hash mismatch
        var restored = await _service.RestoreAsync(manifest);

        Assert.False(restored);
    }

    [Fact]
    public async Task BackupFiles_EmptyList_ReturnsEmptyManifest()
    {
        var manifest = await _service.BackupFilesAsync(Array.Empty<string>(), "TestModule");

        Assert.NotNull(manifest);
        Assert.Empty(manifest.Entries);
    }

    [Fact]
    public async Task BackupArchive_CopiesSingleFile()
    {
        var archive = CreateTestFile("mymodule.mod", "module archive content");

        var manifest = await _service.BackupArchiveAsync(archive, "MyModule");

        Assert.NotNull(manifest);
        var entry = Assert.Single(manifest.Entries);
        Assert.True(File.Exists(entry.BackupPath));
        Assert.Equal("module archive content", await File.ReadAllTextAsync(entry.BackupPath));
    }
}
