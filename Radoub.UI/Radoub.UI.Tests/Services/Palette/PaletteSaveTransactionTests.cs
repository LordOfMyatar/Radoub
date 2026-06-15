using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Radoub.UI.Services.Palette;
using Xunit;

namespace Radoub.UI.Tests.Services.Palette;

/// <summary>
/// Rollback/commit tests for the N-file atomic save transaction (#2476). This is the
/// highest-corruption-risk path (an .itp plus every touched blueprint), so it gets
/// dedicated all-or-nothing tests. Uses a real temp directory; no FlaUI.
/// </summary>
public sealed class PaletteSaveTransactionTests : IDisposable
{
    private readonly string _dir;

    public PaletteSaveTransactionTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "RadoubPaletteTxn_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); }
        catch { /* best-effort cleanup */ }
    }

    private string Path_(string name) => System.IO.Path.Combine(_dir, name);

    /// <summary>A successful write: produces bytes and re-reads them as valid.</summary>
    private static PaletteFileWrite Write(string path, byte[] bytes) =>
        new(path, () => bytes, b => b.Length == bytes.Length);

    [Fact]
    public void Commit_AllValid_WritesEveryFileAndReportsSuccess()
    {
        var itpPath = Path_("itempalcus.itp");
        var bpPath = Path_("wpn_sword.uti");
        var writes = new[]
        {
            Write(itpPath, new byte[] { 1, 2, 3 }),
            Write(bpPath, new byte[] { 4, 5, 6, 7 }),
        };

        var result = PaletteSaveTransaction.Commit(writes);

        Assert.True(result.Success);
        Assert.Null(result.Error);
        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(itpPath));
        Assert.Equal(new byte[] { 4, 5, 6, 7 }, File.ReadAllBytes(bpPath));
        Assert.Empty(StaleTempFiles());
    }

    [Fact]
    public void Commit_OneWriterThrows_NoOriginalIsModified()
    {
        var itpPath = Path_("itempalcus.itp");
        var bpPath = Path_("bad.uti");
        File.WriteAllBytes(itpPath, new byte[] { 9, 9 });   // pre-existing originals
        File.WriteAllBytes(bpPath, new byte[] { 8, 8 });

        var writes = new[]
        {
            Write(itpPath, new byte[] { 1, 2, 3 }),
            new PaletteFileWrite(bpPath, () => throw new InvalidOperationException("boom"), _ => true),
        };

        var result = PaletteSaveTransaction.Commit(writes);

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        // All-or-nothing: neither original changed.
        Assert.Equal(new byte[] { 9, 9 }, File.ReadAllBytes(itpPath));
        Assert.Equal(new byte[] { 8, 8 }, File.ReadAllBytes(bpPath));
        Assert.Empty(StaleTempFiles());
    }

    [Fact]
    public void Commit_ReReadValidationFails_AbortsWholeCommit()
    {
        var itpPath = Path_("itempalcus.itp");
        var bpPath = Path_("wpn_sword.uti");
        File.WriteAllBytes(itpPath, new byte[] { 9 });

        var writes = new[]
        {
            Write(itpPath, new byte[] { 1, 2, 3 }),
            // bytes produced fine, but re-read validation rejects them
            new PaletteFileWrite(bpPath, () => new byte[] { 4, 5 }, _ => false),
        };

        var result = PaletteSaveTransaction.Commit(writes);

        Assert.False(result.Success);
        Assert.Equal(new byte[] { 9 }, File.ReadAllBytes(itpPath)); // original untouched
        Assert.False(File.Exists(bpPath));                          // never created
        Assert.Empty(StaleTempFiles());
    }

    [Fact]
    public void Commit_Empty_IsNoopSuccess()
    {
        var result = PaletteSaveTransaction.Commit(Array.Empty<PaletteFileWrite>());
        Assert.True(result.Success);
    }

    [Fact]
    public void Commit_FailureDuringReplaceStage_RestoresAlreadyReplacedOriginals()
    {
        // Two pre-existing originals. The second's destination is made un-replaceable by holding
        // an open handle, forcing File.Delete/Move to throw in stage 2 *after* the first original
        // has already been replaced — exercising the per-file backup restore path.
        var firstPath = Path_("itempalcus.itp");
        var lockedPath = Path_("locked.uti");
        File.WriteAllBytes(firstPath, new byte[] { 9, 9 });
        File.WriteAllBytes(lockedPath, new byte[] { 8, 8 });

        using var hold = new FileStream(lockedPath, FileMode.Open, FileAccess.Read, FileShare.Read);

        var writes = new[]
        {
            Write(firstPath, new byte[] { 1, 2, 3 }),
            Write(lockedPath, new byte[] { 4, 5, 6 }),
        };

        var result = PaletteSaveTransaction.Commit(writes);

        Assert.False(result.Success);
        // First original was replaced then rolled back to its pre-commit bytes.
        Assert.Equal(new byte[] { 9, 9 }, File.ReadAllBytes(firstPath));
        Assert.Empty(StaleTempFiles());
        Assert.DoesNotContain(Directory.EnumerateFiles(_dir),
            f => f.EndsWith(".bak", StringComparison.OrdinalIgnoreCase));
    }

    // No *.tmp staging files should be left behind on any path.
    private IEnumerable<string> StaleTempFiles() =>
        Directory.EnumerateFiles(_dir).Where(f => f.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase));
}
