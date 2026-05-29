using System;
using System.IO;
using System.Text;
using Radoub.Formats.Common;
using Xunit;

namespace Radoub.Formats.Tests;

/// <summary>
/// Tests for AtomicFile.Replace — the shared cross-OS atomic file-replace helper (#2256).
///
/// The helper consolidates the atomic write-then-replace pattern that each tool was
/// re-solving inline. It must behave correctly on Windows (NTFS) and Unix (POSIX):
/// File.Move(overwrite:true) maps to MoveFileEx / rename(2), which is atomic on the
/// same volume on every OS. The race window (process killed mid-rename) cannot be
/// observed in a synchronous test without a kill harness, so these tests pin the
/// post-condition contracts that guarantee the success path is correct end-to-end.
/// </summary>
public class AtomicFileTests
{
    private static string NewTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "RadoubAtomic_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public void Replace_ExistingDestination_GetsSourceContent_SourceConsumed()
    {
        var dir = NewTempDir();
        try
        {
            var dest = Path.Combine(dir, "store.utm");
            var source = dest + ".tmp";
            File.WriteAllText(dest, "OLD");
            File.WriteAllText(source, "NEW");

            AtomicFile.Replace(source, dest, backupPath: null);

            Assert.True(File.Exists(dest));
            Assert.Equal("NEW", File.ReadAllText(dest));
            Assert.False(File.Exists(source), "Source (temp) must be consumed by the move.");
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void Replace_DestinationMissing_MovesSourceIntoPlace()
    {
        var dir = NewTempDir();
        try
        {
            var dest = Path.Combine(dir, "newstore.utm");
            var source = dest + ".tmp";
            File.WriteAllText(source, "FIRST");

            // Destination does not exist yet (brand-new file).
            Assert.False(File.Exists(dest));

            AtomicFile.Replace(source, dest, backupPath: null);

            Assert.True(File.Exists(dest));
            Assert.Equal("FIRST", File.ReadAllText(dest));
            Assert.False(File.Exists(source));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void Replace_WithBackupPath_BackupContainsPreviousContent()
    {
        var dir = NewTempDir();
        try
        {
            var dest = Path.Combine(dir, "store.utm");
            var source = dest + ".tmp";
            var backup = dest + ".bak";
            File.WriteAllText(dest, "ORIGINAL");
            File.WriteAllText(source, "REPLACEMENT");

            AtomicFile.Replace(source, dest, backupPath: backup);

            Assert.Equal("REPLACEMENT", File.ReadAllText(dest));
            Assert.True(File.Exists(backup), "Backup must be created when destination existed.");
            Assert.Equal("ORIGINAL", File.ReadAllText(backup));
            Assert.False(File.Exists(source));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void Replace_WithBackupPath_DestinationMissing_NoBackupCreated()
    {
        var dir = NewTempDir();
        try
        {
            var dest = Path.Combine(dir, "newstore.utm");
            var source = dest + ".tmp";
            var backup = dest + ".bak";
            File.WriteAllText(source, "DATA");

            AtomicFile.Replace(source, dest, backupPath: backup);

            Assert.True(File.Exists(dest));
            Assert.False(File.Exists(backup), "No backup when there was nothing to back up.");
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void Replace_OverwritesExistingBackup()
    {
        var dir = NewTempDir();
        try
        {
            var dest = Path.Combine(dir, "store.utm");
            var source = dest + ".tmp";
            var backup = dest + ".bak";
            File.WriteAllText(backup, "STALE_BACKUP");
            File.WriteAllText(dest, "CURRENT");
            File.WriteAllText(source, "NEXT");

            AtomicFile.Replace(source, dest, backupPath: backup);

            Assert.Equal("CURRENT", File.ReadAllText(backup));
            Assert.Equal("NEXT", File.ReadAllText(dest));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void Replace_MissingSource_Throws()
    {
        var dir = NewTempDir();
        try
        {
            var dest = Path.Combine(dir, "store.utm");
            var source = dest + ".tmp"; // never created
            File.WriteAllText(dest, "EXISTING");

            Assert.Throws<FileNotFoundException>(() =>
                AtomicFile.Replace(source, dest, backupPath: null));

            // Destination must be untouched on a precondition failure.
            Assert.Equal("EXISTING", File.ReadAllText(dest));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void Replace_NullOrEmptyArguments_Throws()
    {
        Assert.Throws<ArgumentException>(() => AtomicFile.Replace("", "dest", null));
        Assert.Throws<ArgumentException>(() => AtomicFile.Replace("src", "", null));
        Assert.Throws<ArgumentException>(() => AtomicFile.Replace(null!, "dest", null));
    }

    [Fact]
    public void Replace_PreservesBinaryContentExactly()
    {
        var dir = NewTempDir();
        try
        {
            var dest = Path.Combine(dir, "store.utm");
            var source = dest + ".tmp";
            var payload = new byte[] { 0x00, 0xFF, 0xAA, 0x55, 0x01, 0x02, 0x03 };
            File.WriteAllText(dest, "junk-to-be-replaced");
            File.WriteAllBytes(source, payload);

            AtomicFile.Replace(source, dest, backupPath: null);

            Assert.Equal(payload, File.ReadAllBytes(dest));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }
}
