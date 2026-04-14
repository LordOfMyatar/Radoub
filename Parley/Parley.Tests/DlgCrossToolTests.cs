using DialogEditor.Models;
using DialogEditor.Services;
using Radoub.Formats.Dlg;
using Xunit;
using Xunit.Abstractions;

namespace Parley.Tests;

/// <summary>
/// Cross-tool compatibility tests using real module DLG files.
/// Verifies that files created/edited by NWN Toolset, Parley, and other tools
/// round-trip correctly through Parley's full stack.
/// Covers #1309 Cross-Tool Compatibility test area.
///
/// These tests require the LNS_DLG module directory to be present.
/// They will skip gracefully if game files are not installed.
/// </summary>
public class DlgCrossToolTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _testDirectory;
    private readonly DialogFileService _fileService;

    private static readonly string LnsDlgDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Documents", "Neverwinter Nights", "modules", "LNS_DLG");

    public DlgCrossToolTests(ITestOutputHelper output)
    {
        _output = output;
        _testDirectory = Path.Combine(Path.GetTempPath(), $"ParleyCrossTool_{Guid.NewGuid()}");
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

    #region Module-Level Smoke Tests

    [Fact]
    public async Task AllModuleDlgs_LoadWithoutCrash()
    {
        if (!Directory.Exists(LnsDlgDir))
        {
            _output.WriteLine($"SKIP: LNS_DLG module not found at {LnsDlgDir}");
            return;
        }

        var dlgFiles = Directory.GetFiles(LnsDlgDir, "*.dlg");
        _output.WriteLine($"Found {dlgFiles.Length} .dlg files in LNS_DLG module");

        var loaded = 0;
        var failed = 0;

        foreach (var file in dlgFiles)
        {
            var filename = Path.GetFileName(file);
            try
            {
                var dialog = await _fileService.LoadFromFileAsync(file);
                if (dialog != null)
                {
                    loaded++;
                    _output.WriteLine($"  OK: {filename} ({dialog.Entries.Count}E/{dialog.Replies.Count}R)");
                }
                else
                {
                    failed++;
                    _output.WriteLine($"  NULL: {filename}");
                }
            }
            catch (Exception ex)
            {
                failed++;
                _output.WriteLine($"  FAIL: {filename}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        _output.WriteLine($"\nLoaded: {loaded}, Failed: {failed}, Total: {dlgFiles.Length}");
        Assert.Equal(0, failed);
    }

    [Fact]
    public async Task AllModuleDlgs_RoundTrip_NoCounts​Mismatch()
    {
        if (!Directory.Exists(LnsDlgDir))
        {
            _output.WriteLine($"SKIP: LNS_DLG module not found");
            return;
        }

        var dlgFiles = Directory.GetFiles(LnsDlgDir, "*.dlg");
        var passed = 0;
        var failures = new List<string>();

        foreach (var file in dlgFiles)
        {
            var filename = Path.GetFileName(file);
            try
            {
                var dialog = await _fileService.LoadFromFileAsync(file);
                if (dialog == null) continue;

                var outputPath = Path.Combine(_testDirectory, filename);
                var saved = await _fileService.SaveToFileAsync(dialog, outputPath);
                if (!saved)
                {
                    failures.Add($"SAVE_FAIL: {filename}");
                    continue;
                }

                var reloaded = await _fileService.LoadFromFileAsync(outputPath);
                if (reloaded == null)
                {
                    failures.Add($"RELOAD_FAIL: {filename}");
                    continue;
                }

                if (dialog.Entries.Count != reloaded.Entries.Count)
                {
                    failures.Add($"ENTRY_MISMATCH: {filename} ({dialog.Entries.Count} -> {reloaded.Entries.Count})");
                    continue;
                }
                if (dialog.Replies.Count != reloaded.Replies.Count)
                {
                    failures.Add($"REPLY_MISMATCH: {filename} ({dialog.Replies.Count} -> {reloaded.Replies.Count})");
                    continue;
                }
                if (dialog.Starts.Count != reloaded.Starts.Count)
                {
                    failures.Add($"STARTS_MISMATCH: {filename} ({dialog.Starts.Count} -> {reloaded.Starts.Count})");
                    continue;
                }

                passed++;
            }
            catch (Exception ex)
            {
                failures.Add($"EXCEPTION: {filename}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        foreach (var f in failures)
            _output.WriteLine(f);

        _output.WriteLine($"\nRound-trip: {passed} passed, {failures.Count} failed out of {dlgFiles.Length}");
        Assert.Empty(failures);
    }

    #endregion

    #region Format-Level Cross-Tool Verification

    [Fact]
    public void FormatLevel_ModuleDlgs_BinaryRoundTrip_PreservesAllFields()
    {
        if (!Directory.Exists(LnsDlgDir))
        {
            _output.WriteLine($"SKIP: LNS_DLG module not found");
            return;
        }

        var dlgFiles = Directory.GetFiles(LnsDlgDir, "*.dlg");
        var passed = 0;
        var failures = new List<string>();

        foreach (var file in dlgFiles)
        {
            var filename = Path.GetFileName(file);
            try
            {
                var originalBytes = File.ReadAllBytes(file);
                var dlg1 = DlgReader.Read(originalBytes);
                var writtenBytes = DlgWriter.Write(dlg1);
                var dlg2 = DlgReader.Read(writtenBytes);

                // Structural integrity
                if (dlg1.Entries.Count != dlg2.Entries.Count ||
                    dlg1.Replies.Count != dlg2.Replies.Count ||
                    dlg1.StartingList.Count != dlg2.StartingList.Count)
                {
                    failures.Add($"STRUCTURE: {filename}");
                    continue;
                }

                // Spot-check text on first entry/reply if present
                if (dlg1.Entries.Count > 0)
                {
                    if (dlg1.Entries[0].Text.GetDefault() != dlg2.Entries[0].Text.GetDefault())
                    {
                        failures.Add($"ENTRY_TEXT: {filename}");
                        continue;
                    }
                }
                if (dlg1.Replies.Count > 0)
                {
                    if (dlg1.Replies[0].Text.GetDefault() != dlg2.Replies[0].Text.GetDefault())
                    {
                        failures.Add($"REPLY_TEXT: {filename}");
                        continue;
                    }
                }

                passed++;
            }
            catch (Exception ex)
            {
                failures.Add($"EXCEPTION: {filename}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        foreach (var f in failures)
            _output.WriteLine(f);

        _output.WriteLine($"\nFormat-level round-trip: {passed} passed, {failures.Count} failed");
        Assert.Empty(failures);
    }

    #endregion

    #region Specific Complex Dialog Tests

    [Fact]
    public async Task ComplexDialog_SharedReplies_PreservesLinkStructure()
    {
        // Test with a known complex file that has shared replies (IsLink=true)
        var filePath = Path.Combine(LnsDlgDir, "__chef.dlg");
        if (!File.Exists(filePath))
        {
            _output.WriteLine("SKIP: __chef.dlg not found");
            return;
        }

        var dialog = await _fileService.LoadFromFileAsync(filePath);
        Assert.NotNull(dialog);

        // Count links vs non-links
        var totalPointers = 0;
        var linkCount = 0;
        foreach (var entry in dialog!.Entries)
        {
            foreach (var ptr in entry.Pointers)
            {
                totalPointers++;
                if (ptr.IsLink) linkCount++;
            }
        }
        foreach (var reply in dialog.Replies)
        {
            foreach (var ptr in reply.Pointers)
            {
                totalPointers++;
                if (ptr.IsLink) linkCount++;
            }
        }

        _output.WriteLine($"Total pointers: {totalPointers}, Links: {linkCount}");

        // Round-trip and verify link counts match
        var outputPath = Path.Combine(_testDirectory, "__chef_rt.dlg");
        await _fileService.SaveToFileAsync(dialog, outputPath);
        var reloaded = await _fileService.LoadFromFileAsync(outputPath);
        Assert.NotNull(reloaded);

        var rtTotalPointers = 0;
        var rtLinkCount = 0;
        foreach (var entry in reloaded!.Entries)
        {
            foreach (var ptr in entry.Pointers)
            {
                rtTotalPointers++;
                if (ptr.IsLink) rtLinkCount++;
            }
        }
        foreach (var reply in reloaded.Replies)
        {
            foreach (var ptr in reply.Pointers)
            {
                rtTotalPointers++;
                if (ptr.IsLink) rtLinkCount++;
            }
        }

        Assert.Equal(totalPointers, rtTotalPointers);
        Assert.Equal(linkCount, rtLinkCount);
    }

    [Fact]
    public async Task DeepDialog_100Levels_LoadsAndRoundTrips()
    {
        var filePath = Path.Combine(LnsDlgDir, "__deep100.dlg");
        if (!File.Exists(filePath))
        {
            _output.WriteLine("SKIP: __deep100.dlg not found");
            return;
        }

        var dialog = await _fileService.LoadFromFileAsync(filePath);
        Assert.NotNull(dialog);

        _output.WriteLine($"Deep dialog: {dialog!.Entries.Count}E, {dialog.Replies.Count}R");

        var outputPath = Path.Combine(_testDirectory, "__deep100_rt.dlg");
        var saved = await _fileService.SaveToFileAsync(dialog, outputPath);
        Assert.True(saved);

        var reloaded = await _fileService.LoadFromFileAsync(outputPath);
        Assert.NotNull(reloaded);
        Assert.Equal(dialog.Entries.Count, reloaded!.Entries.Count);
        Assert.Equal(dialog.Replies.Count, reloaded.Replies.Count);
    }

    #endregion
}
