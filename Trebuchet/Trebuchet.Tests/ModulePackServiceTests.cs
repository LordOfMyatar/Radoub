using Radoub.Formats.Erf;
using RadoubLauncher.Services;

namespace Trebuchet.Tests;

/// <summary>
/// Tests for ModulePackService — atomic .mod write + backup of prior .mod.
/// Regression: #2246 (crash mid-pack used to destroy the only .mod copy).
/// </summary>
public class ModulePackServiceTests : IDisposable
{
    private readonly string _workingDir;
    private readonly string _modPath;
    private readonly string _backupRoot;

    public ModulePackServiceTests()
    {
        var root = Path.Combine(Path.GetTempPath(), $"ModulePackTest_{Guid.NewGuid():N}");
        _workingDir = Path.Combine(root, "mymodule");
        Directory.CreateDirectory(_workingDir);
        _modPath = Path.Combine(root, "mymodule.mod");
        _backupRoot = Path.Combine(root, "Backups");
    }

    public void Dispose()
    {
        var root = Path.GetDirectoryName(_workingDir);
        if (root != null)
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }

    private void WriteScript(string resRef, string body)
    {
        File.WriteAllText(Path.Combine(_workingDir, resRef + ".nss"), body);
    }

    private static byte[] MakeMarkerModBytes() => System.Text.Encoding.ASCII.GetBytes("ORIGINAL_MOD_CONTENT_DO_NOT_OVERWRITE");

    [Fact]
    public void PackDirectoryToMod_Success_WritesAtomicallyAndBacksUpPriorMod()
    {
        WriteScript("nw_s0_test", "void main() {}");
        WriteScript("nw_s1_test", "void main() {}");
        var marker = MakeMarkerModBytes();
        File.WriteAllBytes(_modPath, marker);

        var count = ModulePackService.PackDirectoryToMod(_workingDir, _modPath, _backupRoot);

        Assert.True(count >= 2);
        Assert.True(File.Exists(_modPath));
        var written = File.ReadAllBytes(_modPath);
        Assert.NotEqual(marker, written); // .mod was replaced

        // Backup of prior .mod present
        var backupModuleDir = Path.Combine(_backupRoot, "mymodule");
        Assert.True(Directory.Exists(backupModuleDir));
        var backupFiles = Directory.GetFiles(backupModuleDir, "mymodule.mod", SearchOption.AllDirectories);
        Assert.NotEmpty(backupFiles);
        Assert.Equal(marker, File.ReadAllBytes(backupFiles[0]));
    }

    [Fact]
    public void PackDirectoryToMod_UnreadableSourceFile_LeavesPreviousModIntact()
    {
        WriteScript("nw_s0_test", "void main() {}");
        var lockedPath = Path.Combine(_workingDir, "locked.nss");
        File.WriteAllText(lockedPath, "void main() {}");
        var marker = MakeMarkerModBytes();
        File.WriteAllBytes(_modPath, marker);

        using var lockStream = new FileStream(lockedPath, FileMode.Open, FileAccess.Read, FileShare.None);

        var ex = Record.Exception(() => ModulePackService.PackDirectoryToMod(_workingDir, _modPath, _backupRoot));

        Assert.NotNull(ex);
        Assert.True(File.Exists(_modPath));
        Assert.Equal(marker, File.ReadAllBytes(_modPath)); // .mod NOT destroyed
    }

    [Fact]
    public void PackDirectoryToMod_NoPreExistingMod_StillWritesNewMod()
    {
        WriteScript("nw_s0_test", "void main() {}");
        Assert.False(File.Exists(_modPath));

        var count = ModulePackService.PackDirectoryToMod(_workingDir, _modPath, _backupRoot);

        Assert.True(count >= 1);
        Assert.True(File.Exists(_modPath));
    }

    [Fact]
    public void PackDirectoryToMod_TempFileCleanedUp_AfterSuccessfulWrite()
    {
        WriteScript("nw_s0_test", "void main() {}");

        ModulePackService.PackDirectoryToMod(_workingDir, _modPath, _backupRoot);

        var tempFiles = Directory.GetFiles(Path.GetDirectoryName(_modPath)!, "*.tmp");
        Assert.Empty(tempFiles);
    }

    [Fact]
    public void PackDirectoryToMod_InjectedReaderThrows_LeavesPreviousModIntact()
    {
        // Deterministic equivalent of the locked-source-file scenario without
        // relying on OS file-lock semantics (Windows shares reads liberally).
        WriteScript("good01", "void main() {}");
        WriteScript("good02", "void main() {}");
        WriteScript("bad01", "void main() {}");
        var marker = MakeMarkerModBytes();
        File.WriteAllBytes(_modPath, marker);

        var failingReader = new ThrowOnNameReader(failOnFileNameContaining: "bad01");

        var ex = Record.Exception(() =>
            ModulePackService.PackDirectoryToMod(_workingDir, _modPath, _backupRoot, failingReader));

        Assert.NotNull(ex);
        Assert.IsType<IOException>(ex);
        // Prior .mod untouched
        Assert.Equal(marker, File.ReadAllBytes(_modPath));
        // No .tmp leftover
        var tempFiles = Directory.GetFiles(Path.GetDirectoryName(_modPath)!, "*.tmp");
        Assert.Empty(tempFiles);
    }

    private sealed class ThrowOnNameReader : IFileBytesReader
    {
        private readonly string _failOnFileNameContaining;
        public ThrowOnNameReader(string failOnFileNameContaining)
        {
            _failOnFileNameContaining = failOnFileNameContaining;
        }
        public byte[] ReadAllBytes(string path)
        {
            if (Path.GetFileName(path).Contains(_failOnFileNameContaining, StringComparison.OrdinalIgnoreCase))
                throw new IOException($"Simulated read failure for {Path.GetFileName(path)}");
            return File.ReadAllBytes(path);
        }
    }

    [Fact]
    public void PackDirectoryToMod_RoundTrip_ProducesValidErf()
    {
        WriteScript("nw_s0_test", "void main() {}");
        WriteScript("nw_s1_other", "void main() {}");

        ModulePackService.PackDirectoryToMod(_workingDir, _modPath, _backupRoot);

        var erf = ErfReader.Read(_modPath);
        Assert.Equal("MOD ", erf.FileType);
        Assert.Equal(2, erf.Resources.Count);
    }
}
