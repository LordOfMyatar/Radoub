using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Radoub.Formats.Common;
using Radoub.Formats.Erf;
using RadoubLauncher.Services;
using Xunit;

namespace Trebuchet.Tests;

public class ErfImportServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _targetDir;
    private readonly string _erfPath;
    private readonly ErfImportService _service;

    public ErfImportServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ErfImportTest_" + Guid.NewGuid().ToString("N")[..8]);
        _targetDir = Path.Combine(_tempDir, "module");
        Directory.CreateDirectory(_targetDir);
        _erfPath = Path.Combine(_tempDir, "test.erf");
        _service = new ErfImportService();

        CreateTestErf();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private void CreateTestErf()
    {
        var erf = new ErfFile { FileType = "ERF ", FileVersion = "V1.0" };
        var resourceData = new Dictionary<(string ResRef, ushort Type), byte[]>();

        AddResource(erf, resourceData, "test_script", ResourceTypes.Ncs, new byte[] { 0x4E, 0x43, 0x53, 0x20 });
        AddResource(erf, resourceData, "test_item", ResourceTypes.Uti, new byte[] { 0x47, 0x46, 0x46, 0x20 });
        AddResource(erf, resourceData, "test_store", ResourceTypes.Utm, new byte[] { 0x47, 0x46, 0x46, 0x20, 0x01 });

        ErfWriter.Write(erf, _erfPath, resourceData);
    }

    private static void AddResource(ErfFile erf, Dictionary<(string ResRef, ushort Type), byte[]> data,
        string resRef, ushort type, byte[] content)
    {
        erf.Resources.Add(new ErfResourceEntry
        {
            ResRef = resRef,
            ResourceType = type,
            ResId = (uint)erf.Resources.Count,
            Size = (uint)content.Length
        });
        data[(resRef, type)] = content;
    }

    #region DetectConflicts

    [Fact]
    public void DetectConflicts_NoExistingFiles_ReturnsEmpty()
    {
        var erf = ErfReader.ReadMetadataOnly(_erfPath);

        var conflicts = _service.DetectConflicts(erf.Resources, _targetDir);

        Assert.Empty(conflicts);
    }

    [Fact]
    public void DetectConflicts_WithExistingFile_ReturnsConflict()
    {
        File.WriteAllBytes(Path.Combine(_targetDir, "test_item.uti"), new byte[] { 0x00 });
        var erf = ErfReader.ReadMetadataOnly(_erfPath);

        var conflicts = _service.DetectConflicts(erf.Resources, _targetDir);

        Assert.Single(conflicts);
        Assert.Contains("test_item", conflicts);
    }

    [Fact]
    public void DetectConflicts_MultipleConflicts_ReturnsAll()
    {
        File.WriteAllBytes(Path.Combine(_targetDir, "test_item.uti"), new byte[] { 0x00 });
        File.WriteAllBytes(Path.Combine(_targetDir, "test_store.utm"), new byte[] { 0x00 });
        var erf = ErfReader.ReadMetadataOnly(_erfPath);

        var conflicts = _service.DetectConflicts(erf.Resources, _targetDir);

        Assert.Equal(2, conflicts.Count);
        Assert.Contains("test_item", conflicts);
        Assert.Contains("test_store", conflicts);
    }

    #endregion

    #region ImportResourcesAsync

    [Fact]
    public async Task ImportResources_AllNew_ImportsAll()
    {
        var erf = ErfReader.ReadMetadataOnly(_erfPath);

        var result = await _service.ImportResourcesAsync(_erfPath, erf.Resources, _targetDir, overwriteExisting: false, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(3, result.ImportedCount);
        Assert.Equal(0, result.OverwrittenCount);
        Assert.Equal(0, result.SkippedCount);
        Assert.Equal(0, result.ErrorCount);
        Assert.True(File.Exists(Path.Combine(_targetDir, "test_script.ncs")));
        Assert.True(File.Exists(Path.Combine(_targetDir, "test_item.uti")));
        Assert.True(File.Exists(Path.Combine(_targetDir, "test_store.utm")));
    }

    [Fact]
    public async Task ImportResources_ConflictWithoutOverwrite_SkipsConflicting()
    {
        var existingContent = new byte[] { 0xFF, 0xFF };
        File.WriteAllBytes(Path.Combine(_targetDir, "test_item.uti"), existingContent);
        var erf = ErfReader.ReadMetadataOnly(_erfPath);

        var result = await _service.ImportResourcesAsync(_erfPath, erf.Resources, _targetDir, overwriteExisting: false, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(2, result.ImportedCount);
        Assert.Equal(0, result.OverwrittenCount);
        Assert.Equal(1, result.SkippedCount);
        // Verify the existing file was NOT overwritten
        var actual = await File.ReadAllBytesAsync(Path.Combine(_targetDir, "test_item.uti"), TestContext.Current.CancellationToken);
        Assert.Equal(existingContent, actual);
    }

    [Fact]
    public async Task ImportResources_ConflictWithOverwrite_OverwritesConflicting()
    {
        File.WriteAllBytes(Path.Combine(_targetDir, "test_item.uti"), new byte[] { 0xFF, 0xFF });
        var erf = ErfReader.ReadMetadataOnly(_erfPath);

        var result = await _service.ImportResourcesAsync(_erfPath, erf.Resources, _targetDir, overwriteExisting: true, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(2, result.ImportedCount);
        Assert.Equal(1, result.OverwrittenCount);
        Assert.Equal(3, result.TotalWritten);
        Assert.Equal(0, result.SkippedCount);
        var actual = await File.ReadAllBytesAsync(Path.Combine(_targetDir, "test_item.uti"), TestContext.Current.CancellationToken);
        Assert.NotEqual(new byte[] { 0xFF, 0xFF }, actual);
    }

    [Fact]
    public async Task ImportResources_EmptyList_ReturnsZeroCounts()
    {
        var result = await _service.ImportResourcesAsync(_erfPath, Array.Empty<ErfResourceEntry>(), _targetDir, overwriteExisting: false, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(0, result.ImportedCount);
        Assert.Equal(0, result.SkippedCount);
        Assert.Equal(0, result.ErrorCount);
    }

    [Fact]
    public async Task ImportResources_Cancellation_ThrowsOperationCanceled()
    {
        var erf = ErfReader.ReadMetadataOnly(_erfPath);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _service.ImportResourcesAsync(_erfPath, erf.Resources, _targetDir, overwriteExisting: false, cancellationToken: cts.Token));
    }

    [Fact]
    public async Task ImportResources_ReportsProgress()
    {
        var erf = ErfReader.ReadMetadataOnly(_erfPath);
        var progressReports = new List<ImportProgress>();
        var progress = new Progress<ImportProgress>(p => progressReports.Add(p));

        await _service.ImportResourcesAsync(_erfPath, erf.Resources, _targetDir, overwriteExisting: false, progress: progress, cancellationToken: TestContext.Current.CancellationToken);

        // Progress may be reported asynchronously, so allow a small delay
        await Task.Delay(100, TestContext.Current.CancellationToken);
        Assert.True(progressReports.Count >= 1);
    }

    [Fact]
    public async Task ImportResources_SelectiveImport_OnlyImportsSpecified()
    {
        var erf = ErfReader.ReadMetadataOnly(_erfPath);
        var selected = new List<ErfResourceEntry> { erf.Resources[0] }; // Only test_script

        var result = await _service.ImportResourcesAsync(_erfPath, selected, _targetDir, overwriteExisting: false, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(1, result.ImportedCount);
        Assert.True(File.Exists(Path.Combine(_targetDir, "test_script.ncs")));
        Assert.False(File.Exists(Path.Combine(_targetDir, "test_item.uti")));
        Assert.False(File.Exists(Path.Combine(_targetDir, "test_store.utm")));
    }

    #endregion

    #region Path Traversal (#2245)

    // Security: a crafted ERF/HAK with traversal sequences in ResRef
    // (e.g. "../../tmp/pwned") must not be allowed to write outside targetDirectory.
    // ErfReader reads ResRef as raw ASCII without filtering "/", "\", or "..".
    // Path.Combine alone does NOT reject absolute or traversal segments.

    [Theory]
    [InlineData("../pwned")]
    [InlineData("..\\pwned")]
    [InlineData("../../pwned")]
    [InlineData("..\\..\\pwned")]
    [InlineData("foo/bar")]
    [InlineData("foo\\bar")]
    public void DetectConflicts_MaliciousResRef_DoesNotEscape(string maliciousResRef)
    {
        var entries = new List<ErfResourceEntry>
        {
            new() { ResRef = maliciousResRef, ResourceType = ResourceTypes.Uti, ResId = 0, Offset = 0, Size = 4 }
        };

        // Plant a decoy file at the would-be-escaped location so a naive
        // Path.Combine would report a conflict. Validated impl must NOT.
        var escapedPath = Path.GetFullPath(Path.Combine(_targetDir, maliciousResRef + ".uti"));
        Directory.CreateDirectory(Path.GetDirectoryName(escapedPath)!);
        File.WriteAllBytes(escapedPath, new byte[] { 0xAA });

        var conflicts = _service.DetectConflicts(entries, _targetDir);

        Assert.Empty(conflicts);
    }

    [Theory]
    [InlineData("../pwned")]
    [InlineData("..\\pwned")]
    [InlineData("../../../../tmp/pwned")]
    [InlineData("foo/bar")]
    public async Task ImportResources_MaliciousResRef_DoesNotWriteOutsideTarget(string maliciousResRef)
    {
        var sourceErf = ErfReader.ReadMetadataOnly(_erfPath);
        var realEntry = sourceErf.Resources[0];

        // Inject malicious ResRef while keeping valid Offset/Size so
        // ExtractResource reads real bytes (worst-case scenario).
        var entries = new List<ErfResourceEntry>
        {
            new()
            {
                ResRef = maliciousResRef,
                ResourceType = realEntry.ResourceType,
                ResId = realEntry.ResId,
                Offset = realEntry.Offset,
                Size = realEntry.Size
            }
        };

        var escapedPath = Path.GetFullPath(Path.Combine(_targetDir, maliciousResRef + ResourceTypes.GetExtension(realEntry.ResourceType)));
        var escapedExistedBefore = File.Exists(escapedPath);
        var escapedSizeBefore = escapedExistedBefore ? new FileInfo(escapedPath).Length : -1L;

        var result = await _service.ImportResourcesAsync(_erfPath, entries, _targetDir, overwriteExisting: true, cancellationToken: TestContext.Current.CancellationToken);

        // Either skipped via validation or errored — must NOT count as imported.
        Assert.Equal(0, result.ImportedCount);
        Assert.Equal(0, result.OverwrittenCount);

        // No file written outside targetDirectory.
        if (escapedExistedBefore)
        {
            Assert.Equal(escapedSizeBefore, new FileInfo(escapedPath).Length);
        }
        else
        {
            Assert.False(File.Exists(escapedPath), $"File leaked outside target: {escapedPath}");
        }
    }

    [Theory]
    [InlineData("valid_name")]
    [InlineData("test_item")]
    [InlineData("a")]
    [InlineData("abcdefghij012345")] // 16 chars — Aurora ResRef max
    [InlineData("name_with-dash")]
    public async Task ImportResources_LegitimateResRef_StillWorks(string legitResRef)
    {
        var sourceErf = ErfReader.ReadMetadataOnly(_erfPath);
        var realEntry = sourceErf.Resources[0];

        var entries = new List<ErfResourceEntry>
        {
            new()
            {
                ResRef = legitResRef,
                ResourceType = realEntry.ResourceType,
                ResId = realEntry.ResId,
                Offset = realEntry.Offset,
                Size = realEntry.Size
            }
        };

        var result = await _service.ImportResourcesAsync(_erfPath, entries, _targetDir, overwriteExisting: false, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(1, result.ImportedCount);
        Assert.True(File.Exists(Path.Combine(_targetDir, legitResRef + ResourceTypes.GetExtension(realEntry.ResourceType))));
    }

    #endregion
}
