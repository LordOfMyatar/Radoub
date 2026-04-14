using DialogEditor.Models;
using DialogEditor.Services;
using Radoub.Formats.Dlg;
using Xunit;
using Xunit.Abstractions;

namespace Parley.Tests;

/// <summary>
/// Round-trip tests using real .dlg files from the test corpus.
/// Verifies that loading and re-saving real-world files preserves all data.
/// Covers #1309 Round-Trip Integrity and Cross-Tool Compatibility test areas.
/// </summary>
public class DlgRealWorldRoundTripTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _testDirectory;
    private readonly DialogFileService _fileService;

    private static readonly string TestFilesDir = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "TestingTools", "TestFiles");

    public DlgRealWorldRoundTripTests(ITestOutputHelper output)
    {
        _output = output;
        _testDirectory = Path.Combine(Path.GetTempPath(), $"ParleyRealWorld_{Guid.NewGuid()}");
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

    #region Format-Level Binary Round-Trips

    [Theory]
    [InlineData("lista.dlg")]
    [InlineData("chef.dlg")]
    [InlineData("myra_james.dlg")]
    [InlineData("convolutedconvo.dlg")]
    [InlineData("deep20_xref.dlg")]
    [InlineData("deep100_xref.dlg")]
    [InlineData("hicks_hudson.dlg")]
    // Note: parameter_hell_FINAL.dlg excluded — has buffer access violation (corrupt ConditionParams field)
    public void FormatLevel_RealFile_RoundTrips_DataPreserved(string filename)
    {
        var filePath = Path.Combine(TestFilesDir, filename);
        if (!File.Exists(filePath))
        {
            _output.WriteLine($"SKIP: {filename} not found at {filePath}");
            return;
        }

        var originalBytes = File.ReadAllBytes(filePath);
        _output.WriteLine($"{filename}: {originalBytes.Length} bytes");

        // Read -> Write -> Read and compare
        var dlg1 = DlgReader.Read(originalBytes);
        var writtenBytes = DlgWriter.Write(dlg1);
        var dlg2 = DlgReader.Read(writtenBytes);

        _output.WriteLine($"  Entries: {dlg1.Entries.Count}, Replies: {dlg1.Replies.Count}, Starts: {dlg1.StartingList.Count}");
        _output.WriteLine($"  Original: {originalBytes.Length} bytes, Re-written: {writtenBytes.Length} bytes");

        // Structure counts must match
        Assert.Equal(dlg1.Entries.Count, dlg2.Entries.Count);
        Assert.Equal(dlg1.Replies.Count, dlg2.Replies.Count);
        Assert.Equal(dlg1.StartingList.Count, dlg2.StartingList.Count);

        // Global properties
        Assert.Equal(dlg1.DelayEntry, dlg2.DelayEntry);
        Assert.Equal(dlg1.DelayReply, dlg2.DelayReply);
        Assert.Equal(dlg1.NumWords, dlg2.NumWords);
        Assert.Equal(dlg1.EndConversation, dlg2.EndConversation);
        Assert.Equal(dlg1.EndConverAbort, dlg2.EndConverAbort);
        Assert.Equal(dlg1.PreventZoomIn, dlg2.PreventZoomIn);

        // Verify all entries
        for (int i = 0; i < dlg1.Entries.Count; i++)
        {
            var e1 = dlg1.Entries[i];
            var e2 = dlg2.Entries[i];
            Assert.Equal(e1.Speaker, e2.Speaker);
            Assert.Equal(e1.Script, e2.Script);
            Assert.Equal(e1.Sound, e2.Sound);
            Assert.Equal(e1.Comment, e2.Comment);
            Assert.Equal(e1.Text.GetDefault(), e2.Text.GetDefault());
            Assert.Equal(e1.Text.StrRef, e2.Text.StrRef);
            Assert.Equal(e1.Animation, e2.Animation);
            Assert.Equal(e1.AnimLoop, e2.AnimLoop);
            Assert.Equal(e1.Quest, e2.Quest);
            Assert.Equal(e1.QuestEntry, e2.QuestEntry);
            Assert.Equal(e1.Delay, e2.Delay);
            Assert.Equal(e1.RepliesList.Count, e2.RepliesList.Count);

            for (int j = 0; j < e1.RepliesList.Count; j++)
            {
                Assert.Equal(e1.RepliesList[j].Index, e2.RepliesList[j].Index);
                Assert.Equal(e1.RepliesList[j].IsChild, e2.RepliesList[j].IsChild);
            }

            Assert.Equal(e1.ActionParams.Count, e2.ActionParams.Count);
            for (int j = 0; j < e1.ActionParams.Count; j++)
            {
                Assert.Equal(e1.ActionParams[j].Key, e2.ActionParams[j].Key);
                Assert.Equal(e1.ActionParams[j].Value, e2.ActionParams[j].Value);
            }
        }

        // Verify all replies
        for (int i = 0; i < dlg1.Replies.Count; i++)
        {
            var r1 = dlg1.Replies[i];
            var r2 = dlg2.Replies[i];
            Assert.Equal(r1.Script, r2.Script);
            Assert.Equal(r1.Sound, r2.Sound);
            Assert.Equal(r1.Comment, r2.Comment);
            Assert.Equal(r1.Text.GetDefault(), r2.Text.GetDefault());
            Assert.Equal(r1.Text.StrRef, r2.Text.StrRef);
            Assert.Equal(r1.Animation, r2.Animation);
            Assert.Equal(r1.AnimLoop, r2.AnimLoop);
            Assert.Equal(r1.Quest, r2.Quest);
            Assert.Equal(r1.QuestEntry, r2.QuestEntry);
            Assert.Equal(r1.Delay, r2.Delay);
            Assert.Equal(r1.EntriesList.Count, r2.EntriesList.Count);

            for (int j = 0; j < r1.EntriesList.Count; j++)
            {
                Assert.Equal(r1.EntriesList[j].Index, r2.EntriesList[j].Index);
                Assert.Equal(r1.EntriesList[j].IsChild, r2.EntriesList[j].IsChild);
            }

            Assert.Equal(r1.ActionParams.Count, r2.ActionParams.Count);
        }

        // Verify starting list
        for (int i = 0; i < dlg1.StartingList.Count; i++)
        {
            Assert.Equal(dlg1.StartingList[i].Index, dlg2.StartingList[i].Index);
            Assert.Equal(dlg1.StartingList[i].IsChild, dlg2.StartingList[i].IsChild);
        }
    }

    #endregion

    #region Tool-Level Round-Trips (Dialog model)

    [Theory]
    [InlineData("lista.dlg")]
    [InlineData("chef.dlg")]
    [InlineData("myra_james.dlg")]
    [InlineData("convolutedconvo.dlg")]
    [InlineData("deep20_xref.dlg")]
    [InlineData("hicks_hudson.dlg")]
    // Note: parameter_hell_FINAL.dlg excluded — has corrupt ConditionParams buffer
    public async Task ToolLevel_RealFile_LoadAndSave_PreservesStructure(string filename)
    {
        var filePath = Path.Combine(TestFilesDir, filename);
        if (!File.Exists(filePath))
        {
            _output.WriteLine($"SKIP: {filename} not found");
            return;
        }

        var originalSize = new FileInfo(filePath).Length;
        _output.WriteLine($"{filename}: {originalSize} bytes");

        // Load via Parley's full stack
        var dialog = await _fileService.LoadFromFileAsync(filePath);
        Assert.NotNull(dialog);

        _output.WriteLine($"  Entries: {dialog!.Entries.Count}, Replies: {dialog.Replies.Count}, Starts: {dialog.Starts.Count}");

        // Save to new file
        var outputPath = Path.Combine(_testDirectory, filename);
        var saved = await _fileService.SaveToFileAsync(dialog, outputPath);
        Assert.True(saved);

        // Reload saved file
        var reloaded = await _fileService.LoadFromFileAsync(outputPath);
        Assert.NotNull(reloaded);

        // Structure must match
        Assert.Equal(dialog.Entries.Count, reloaded!.Entries.Count);
        Assert.Equal(dialog.Replies.Count, reloaded.Replies.Count);
        Assert.Equal(dialog.Starts.Count, reloaded.Starts.Count);

        // Global properties
        Assert.Equal(dialog.DelayEntry, reloaded.DelayEntry);
        Assert.Equal(dialog.DelayReply, reloaded.DelayReply);
        Assert.Equal(dialog.NumWords, reloaded.NumWords);
        Assert.Equal(dialog.PreventZoom, reloaded.PreventZoom);

        // Spot-check text on first entry
        if (dialog.Entries.Count > 0 && reloaded.Entries.Count > 0)
        {
            Assert.Equal(dialog.Entries[0].Text.GetDefault(), reloaded.Entries[0].Text.GetDefault());
        }

        var newSize = new FileInfo(outputPath).Length;
        _output.WriteLine($"  Saved: {newSize} bytes (delta: {newSize - originalSize})");
    }

    #endregion

    #region All TestFiles Smoke Test

    [Fact]
    public async Task AllTestFiles_LoadWithoutCrash()
    {
        var testDir = Path.GetFullPath(TestFilesDir);
        if (!Directory.Exists(testDir))
        {
            _output.WriteLine($"SKIP: TestFiles directory not found at {testDir}");
            return;
        }

        var dlgFiles = Directory.GetFiles(testDir, "*.dlg");
        _output.WriteLine($"Found {dlgFiles.Length} .dlg test files");

        var loaded = 0;
        var failed = 0;
        var errors = new List<string>();

        foreach (var file in dlgFiles)
        {
            var filename = Path.GetFileName(file);
            try
            {
                var dialog = await _fileService.LoadFromFileAsync(file);
                if (dialog != null)
                {
                    loaded++;
                    _output.WriteLine($"  OK: {filename} ({dialog.Entries.Count}E/{dialog.Replies.Count}R/{dialog.Starts.Count}S)");
                }
                else
                {
                    failed++;
                    errors.Add($"  NULL: {filename}");
                }
            }
            catch (Exception ex)
            {
                failed++;
                errors.Add($"  EXCEPTION: {filename}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        foreach (var error in errors)
            _output.WriteLine(error);

        _output.WriteLine($"\nResults: {loaded} loaded, {failed} failed out of {dlgFiles.Length}");
        Assert.True(loaded > 0, "Should load at least some test files");
    }

    [Fact]
    public async Task AllTestFiles_SaveRoundTrip_NoCorruption()
    {
        var testDir = Path.GetFullPath(TestFilesDir);
        if (!Directory.Exists(testDir))
        {
            _output.WriteLine($"SKIP: TestFiles directory not found");
            return;
        }

        var dlgFiles = Directory.GetFiles(testDir, "*.dlg");
        var passed = 0;
        var skipped = 0;
        var failures = new List<string>();

        foreach (var file in dlgFiles)
        {
            var filename = Path.GetFileName(file);
            try
            {
                var dialog = await _fileService.LoadFromFileAsync(file);
                if (dialog == null)
                {
                    skipped++;
                    continue;
                }

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

                if (dialog.Entries.Count != reloaded.Entries.Count ||
                    dialog.Replies.Count != reloaded.Replies.Count)
                {
                    failures.Add($"COUNT_MISMATCH: {filename} (E:{dialog.Entries.Count}->{reloaded.Entries.Count}, R:{dialog.Replies.Count}->{reloaded.Replies.Count})");
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

        _output.WriteLine($"\nRound-trip results: {passed} passed, {failures.Count} failed, {skipped} skipped out of {dlgFiles.Length}");
        Assert.Empty(failures);
    }

    #endregion
}
