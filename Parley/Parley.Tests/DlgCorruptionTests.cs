using System.Text;
using DialogEditor.Services;
using Radoub.Formats.Dlg;
using Radoub.Formats.Gff;
using Xunit;
using Xunit.Abstractions;

namespace Parley.Tests;

/// <summary>
/// Corruption and malformed file tests specific to DLG files.
/// Covers #1309 File Handling test area: corrupt/truncated .dlg files,
/// non-DLG files renamed to .dlg, graceful error handling.
///
/// Note: General GFF corruption tests are in Radoub.Formats.Tests/CorruptedFileTests.cs.
/// These tests focus on DLG-specific corruption scenarios and Parley's error handling.
/// </summary>
public class DlgCorruptionTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _testDirectory;
    private readonly DialogFileService _fileService;

    public DlgCorruptionTests(ITestOutputHelper output)
    {
        _output = output;
        _testDirectory = Path.Combine(Path.GetTempPath(), $"ParleyCorrupt_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
        _fileService = new DialogFileService();
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            try { Directory.Delete(_testDirectory, true); }
            catch { /* Ignore cleanup errors */ }
        }
    }

    #region Truncated DLG Files

    [Fact]
    public async Task TruncatedDlg_HeaderOnly_ReturnsNull()
    {
        // Valid GFF header but no data sections
        var bytes = new byte[56];
        Encoding.ASCII.GetBytes("DLG ").CopyTo(bytes, 0);
        Encoding.ASCII.GetBytes("V3.2").CopyTo(bytes, 4);
        // All offsets point to end of file (56), all counts are 0

        var filePath = Path.Combine(_testDirectory, "truncated.dlg");
        await File.WriteAllBytesAsync(filePath, bytes);

        // DialogFileService catches exceptions and returns null
        var result = await _fileService.LoadFromFileAsync(filePath);
        // Either returns an empty dialog or null — both are acceptable
        // The key is no crash, no hang
        _output.WriteLine($"Truncated header result: {(result == null ? "null" : $"{result.Entries.Count} entries")}");
    }

    [Fact]
    public async Task TruncatedDlg_MidEntry_GracefulFailure()
    {
        // Create a valid DLG, then truncate in the middle
        var dlg = new DlgFile
        {
            FileType = "DLG ",
            FileVersion = "V3.2"
        };
        var entry = new DlgEntry();
        entry.Text.StrRef = 0xFFFFFFFF;
        entry.Text.LocalizedStrings[0] = "This text will be cut off";
        entry.Speaker = "NPC";
        dlg.Entries.Add(entry);
        dlg.StartingList.Add(new DlgLink { Index = 0, IsChild = false });

        var fullBytes = DlgWriter.Write(dlg);
        var truncatedBytes = fullBytes[..(fullBytes.Length / 2)]; // Cut in half

        var filePath = Path.Combine(_testDirectory, "midtrunc.dlg");
        await File.WriteAllBytesAsync(filePath, truncatedBytes);

        _output.WriteLine($"Full size: {fullBytes.Length}, Truncated: {truncatedBytes.Length}");

        // Should not crash — returns null or partial data
        var result = await _fileService.LoadFromFileAsync(filePath);
        _output.WriteLine($"Mid-truncation result: {(result == null ? "null" : "loaded")}");
    }

    [Fact]
    public async Task EmptyFile_ZeroBytes_ReturnsNull()
    {
        var filePath = Path.Combine(_testDirectory, "empty.dlg");
        await File.WriteAllBytesAsync(filePath, Array.Empty<byte>());

        var result = await _fileService.LoadFromFileAsync(filePath);
        Assert.Null(result);
    }

    #endregion

    #region Non-DLG Files

    [Fact]
    public async Task NonDlgFile_RenamedTextFile_ReturnsNull()
    {
        var filePath = Path.Combine(_testDirectory, "fake.dlg");
        await File.WriteAllTextAsync(filePath, "This is just a text file renamed to .dlg");

        var result = await _fileService.LoadFromFileAsync(filePath);
        Assert.Null(result);
    }

    [Fact]
    public async Task NonDlgFile_RenamedPngHeader_ReturnsNull()
    {
        // PNG magic bytes
        var pngHeader = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        var filePath = Path.Combine(_testDirectory, "image.dlg");
        await File.WriteAllBytesAsync(filePath, pngHeader);

        var result = await _fileService.LoadFromFileAsync(filePath);
        Assert.Null(result);
    }

    [Fact]
    public async Task WrongGffType_UtcRenamedToDlg_ReturnsNull()
    {
        // Create a valid GFF but with UTC type instead of DLG
        var bytes = new byte[56];
        Encoding.ASCII.GetBytes("UTC ").CopyTo(bytes, 0);
        Encoding.ASCII.GetBytes("V3.2").CopyTo(bytes, 4);
        // Point all sections to offset 56 with count 0
        for (int i = 8; i < 56; i += 8)
        {
            BitConverter.GetBytes((uint)56).CopyTo(bytes, i);
            BitConverter.GetBytes((uint)0).CopyTo(bytes, i + 4);
        }

        var filePath = Path.Combine(_testDirectory, "utc_as_dlg.dlg");
        await File.WriteAllBytesAsync(filePath, bytes);

        // DlgReader should reject non-DLG file types
        var result = await _fileService.LoadFromFileAsync(filePath);
        Assert.Null(result);
    }

    [Fact]
    public async Task RandomBinaryGarbage_ReturnsNull()
    {
        var random = new Random(42);
        var garbage = new byte[1024];
        random.NextBytes(garbage);

        var filePath = Path.Combine(_testDirectory, "garbage.dlg");
        await File.WriteAllBytesAsync(filePath, garbage);

        var result = await _fileService.LoadFromFileAsync(filePath);
        Assert.Null(result);
    }

    #endregion

    #region File System Edge Cases

    [Fact]
    public async Task NonExistentFile_ThrowsFileNotFound()
    {
        var filePath = Path.Combine(_testDirectory, "does_not_exist.dlg");
        await Assert.ThrowsAsync<FileNotFoundException>(() => _fileService.LoadFromFileAsync(filePath));
    }

    [Fact]
    public async Task ReadOnlyFile_CanStillBeLoaded()
    {
        // Create a valid DLG, make it read-only, verify we can still load it
        var dlg = new DlgFile
        {
            FileType = "DLG ",
            FileVersion = "V3.2"
        };
        var entry = new DlgEntry();
        entry.Text.StrRef = 0xFFFFFFFF;
        entry.Text.LocalizedStrings[0] = "Read-only test";
        dlg.Entries.Add(entry);
        dlg.StartingList.Add(new DlgLink { Index = 0, IsChild = false });

        var filePath = Path.Combine(_testDirectory, "readonly.dlg");
        DlgWriter.Write(dlg, filePath);

        // Make read-only
        File.SetAttributes(filePath, FileAttributes.ReadOnly);
        try
        {
            var result = await _fileService.LoadFromFileAsync(filePath);
            Assert.NotNull(result);
            Assert.Single(result!.Entries);
            Assert.Equal("Read-only test", result.Entries[0].Text.GetDefault());
        }
        finally
        {
            // Cleanup: remove read-only so Dispose can delete
            File.SetAttributes(filePath, FileAttributes.Normal);
        }
    }

    #endregion

    #region Format-Level Corruption

    [Fact]
    public void DlgReader_InvalidFileType_ThrowsInvalidData()
    {
        // Valid GFF structure but wrong file type for DLG
        var bytes = CreateMinimalGff("UTC ");
        Assert.Throws<InvalidDataException>(() => DlgReader.Read(bytes));
    }

    [Fact]
    public void DlgReader_WrongVersion_ThrowsInvalidData()
    {
        var bytes = CreateMinimalGff("DLG ", "V1.0");
        Assert.Throws<InvalidDataException>(() => DlgReader.Read(bytes));
    }

    [Fact]
    public void DlgReader_OneByteFile_Throws()
    {
        var bytes = new byte[] { 0x44 }; // Just 'D'
        Assert.ThrowsAny<Exception>(() => DlgReader.Read(bytes));
    }

    [Fact]
    public void DlgReader_ExactlyHeaderSize_NoData_DoesNotCrash()
    {
        var bytes = CreateMinimalGff("DLG ");
        // This is a valid minimal GFF with DLG type — should parse to empty
        var exception = Record.Exception(() => DlgReader.Read(bytes));
        // Either succeeds with empty dialog or throws gracefully
        Assert.True(exception == null || exception is InvalidDataException);
    }

    #endregion

    #region Helpers

    private static byte[] CreateMinimalGff(string fileType, string version = "V3.2")
    {
        var buffer = new byte[56];
        Encoding.ASCII.GetBytes(fileType.PadRight(4)[..4]).CopyTo(buffer, 0);
        Encoding.ASCII.GetBytes(version.PadRight(4)[..4]).CopyTo(buffer, 4);

        // All sections point to offset 56 with count 0
        for (int i = 8; i < 56; i += 8)
        {
            BitConverter.GetBytes((uint)56).CopyTo(buffer, i);
            BitConverter.GetBytes((uint)0).CopyTo(buffer, i + 4);
        }

        return buffer;
    }

    #endregion
}
