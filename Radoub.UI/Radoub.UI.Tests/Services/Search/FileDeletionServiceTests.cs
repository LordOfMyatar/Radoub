using Radoub.UI.Services.Search;
using Xunit;

namespace Radoub.UI.Tests.Services.Search;

public class FileDeletionServiceTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _backupDir;
    private readonly BackupService _service;

    public FileDeletionServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"radoub_delete_test_{Guid.NewGuid():N}");
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
    public async Task DeleteWithBackup_RemovesFile()
    {
        var file = CreateTestFile("boulder001.utp", "placeable content");

        await FileDeletionService.DeleteWithBackupAsync(file, "TestModule", _service);

        Assert.False(File.Exists(file));
    }

    [Fact]
    public async Task DeleteWithBackup_BacksUpBeforeDeleting()
    {
        var content = "irreplaceable placeable content";
        var file = CreateTestFile("chest_iron.utp", content);

        var manifest = await FileDeletionService.DeleteWithBackupAsync(file, "TestModule", _service);

        var entry = Assert.Single(manifest.Entries);
        Assert.True(File.Exists(entry.BackupPath));
        Assert.Equal(content, await File.ReadAllTextAsync(entry.BackupPath));
    }

    [Fact]
    public async Task DeleteWithBackup_BackupUnderModuleName()
    {
        var file = CreateTestFile("door_oak.utp", "x");

        var manifest = await FileDeletionService.DeleteWithBackupAsync(file, "MyModule", _service);

        Assert.Contains("MyModule", manifest.BackupDirectory);
    }
}
