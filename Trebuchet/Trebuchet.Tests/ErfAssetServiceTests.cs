using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Radoub.Formats.Common;
using Radoub.Formats.Erf;
using RadoubLauncher.Services;
using Xunit;

namespace Trebuchet.Tests;

public class ErfAssetServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _backupRoot;
    private readonly string _erfPath;
    private readonly ErfAssetService _service;

    public ErfAssetServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ErfAssetTest_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
        _backupRoot = Path.Combine(_tempDir, "Backups");
        _erfPath = Path.Combine(_tempDir, "archive.erf");
        // Isolate backups to the temp dir so the suite never touches the real ~/Radoub/Backups.
        _service = new ErfAssetService(_backupRoot);

        // Start from an empty ERF (the create-from-scratch output).
        new ErfCreationService().CreateErf(_erfPath, overwrite: true);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private string WriteSourceFile(string name, byte[] content)
    {
        var path = Path.Combine(_tempDir, name);
        File.WriteAllBytes(path, content);
        return path;
    }

    [Fact]
    public void AddFiles_AddsResourceToEmptyErf()
    {
        var src = WriteSourceFile("door_open.ncs", new byte[] { 0x4E, 0x43, 0x53, 0x20 });

        var result = _service.AddFiles(_erfPath, new[] { src }, overwriteExisting: false);

        Assert.Equal(1, result.AddedCount);
        var erf = ErfReader.Read(_erfPath);
        Assert.Contains(erf.Resources,
            r => r.ResRef.Equals("door_open", StringComparison.OrdinalIgnoreCase) && r.ResourceType == ResourceTypes.Ncs);
    }

    [Fact]
    public void AddFiles_PreservesFileContent()
    {
        var content = new byte[] { 1, 2, 3, 4, 5 };
        var src = WriteSourceFile("sword01.uti", content);

        _service.AddFiles(_erfPath, new[] { src }, overwriteExisting: false);

        var erf = ErfReader.Read(_erfPath);
        var entry = erf.FindResource("sword01", ResourceTypes.Uti);
        Assert.NotNull(entry);
        Assert.Equal(content, ErfReader.ExtractResource(_erfPath, entry!));
    }

    [Fact]
    public void AddFiles_AddsMultipleFiles()
    {
        var a = WriteSourceFile("scr_a.ncs", new byte[] { 1 });
        var b = WriteSourceFile("item_b.uti", new byte[] { 2 });

        var result = _service.AddFiles(_erfPath, new[] { a, b }, overwriteExisting: false);

        Assert.Equal(2, result.AddedCount);
        Assert.Equal(2, ErfReader.Read(_erfPath).Resources.Count);
    }

    [Fact]
    public void AddFiles_PreservesExistingResources()
    {
        var first = WriteSourceFile("existing.ncs", new byte[] { 9 });
        _service.AddFiles(_erfPath, new[] { first }, overwriteExisting: false);

        var second = WriteSourceFile("added.uti", new byte[] { 8 });
        _service.AddFiles(_erfPath, new[] { second }, overwriteExisting: false);

        var erf = ErfReader.Read(_erfPath);
        Assert.Equal(2, erf.Resources.Count);
        Assert.NotNull(erf.FindResource("existing", ResourceTypes.Ncs));
        Assert.NotNull(erf.FindResource("added", ResourceTypes.Uti));
    }

    [Fact]
    public void AddFiles_SkipsDuplicateByDefault()
    {
        var first = WriteSourceFile("dup.ncs", new byte[] { 1 });
        _service.AddFiles(_erfPath, new[] { first }, overwriteExisting: false);

        var dupAgain = WriteSourceFile("dup.ncs", new byte[] { 2, 2 });
        var result = _service.AddFiles(_erfPath, new[] { dupAgain }, overwriteExisting: false);

        Assert.Equal(0, result.AddedCount);
        Assert.Equal(1, result.SkippedCount);
        // Original content untouched.
        var erf = ErfReader.Read(_erfPath);
        Assert.Equal(new byte[] { 1 }, ErfReader.ExtractResource(_erfPath, erf.FindResource("dup", ResourceTypes.Ncs)!));
    }

    [Fact]
    public void AddFiles_OverwritesDuplicateWhenRequested()
    {
        var first = WriteSourceFile("dup.ncs", new byte[] { 1 });
        _service.AddFiles(_erfPath, new[] { first }, overwriteExisting: false);

        var dupAgain = WriteSourceFile("dup.ncs", new byte[] { 7, 7, 7 });
        var result = _service.AddFiles(_erfPath, new[] { dupAgain }, overwriteExisting: true);

        Assert.Equal(1, result.AddedCount);
        var erf = ErfReader.Read(_erfPath);
        Assert.Equal(1, erf.Resources.Count);
        Assert.Equal(new byte[] { 7, 7, 7 }, ErfReader.ExtractResource(_erfPath, erf.FindResource("dup", ResourceTypes.Ncs)!));
    }

    [Fact]
    public void AddFiles_RejectsUnknownExtension()
    {
        var src = WriteSourceFile("notes.xyz", new byte[] { 1 });

        var result = _service.AddFiles(_erfPath, new[] { src }, overwriteExisting: false);

        Assert.Equal(0, result.AddedCount);
        Assert.Single(result.Errors);
        Assert.Empty(ErfReader.Read(_erfPath).Resources);
    }

    [Fact]
    public void AddFiles_RejectsTooLongResRef()
    {
        // 17-char stem exceeds Aurora's 16-char ResRef limit.
        var src = WriteSourceFile("thisnameistoolong.ncs", new byte[] { 1 });

        var result = _service.AddFiles(_erfPath, new[] { src }, overwriteExisting: false);

        Assert.Equal(0, result.AddedCount);
        Assert.Single(result.Errors);
    }

    [Fact]
    public void AddFiles_ThrowsWhenErfMissing()
    {
        var missing = Path.Combine(_tempDir, "nope.erf");
        var src = WriteSourceFile("scr.ncs", new byte[] { 1 });

        Assert.Throws<FileNotFoundException>(() => _service.AddFiles(missing, new[] { src }, overwriteExisting: false));
    }

    [Fact]
    public void AddFiles_SkipsMissingSourceFile()
    {
        var missing = Path.Combine(_tempDir, "ghost.ncs");

        var result = _service.AddFiles(_erfPath, new[] { missing }, overwriteExisting: false);

        Assert.Equal(0, result.AddedCount);
        Assert.Single(result.Errors);
    }

    [Fact]
    public void AddFiles_WritesBackupToArchivesBucket()
    {
        var src = WriteSourceFile("scr.ncs", new byte[] { 1 });

        _service.AddFiles(_erfPath, new[] { src }, overwriteExisting: false);

        var archivesDir = Path.Combine(_backupRoot, "Archives");
        Assert.True(Directory.Exists(archivesDir));
        var backups = Directory.GetFiles(archivesDir, "archive_*.erf");
        Assert.Single(backups);
        // Backup is not left next to the working archive.
        Assert.Empty(Directory.GetFiles(_tempDir, "archive_backup_*.erf"));
    }

    [Fact]
    public void AddFiles_NoBackupWhenDisabled()
    {
        var src = WriteSourceFile("scr.ncs", new byte[] { 1 });

        _service.AddFiles(_erfPath, new[] { src }, overwriteExisting: false, createBackup: false);

        var archivesDir = Path.Combine(_backupRoot, "Archives");
        Assert.False(Directory.Exists(archivesDir));
    }

    [Fact]
    public void AddFiles_RapidAddsProduceDistinctBackups()
    {
        var a = WriteSourceFile("a.ncs", new byte[] { 1 });
        var b = WriteSourceFile("b.ncs", new byte[] { 2 });

        // Two adds in the same second must not collide on the backup filename.
        _service.AddFiles(_erfPath, new[] { a }, overwriteExisting: false);
        _service.AddFiles(_erfPath, new[] { b }, overwriteExisting: false);

        var backups = Directory.GetFiles(Path.Combine(_backupRoot, "Archives"), "archive_*.erf");
        Assert.Equal(2, backups.Length);
    }
}
