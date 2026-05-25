using Radoub.Formats.Settings;
using Xunit;

namespace Radoub.Formats.Tests.Settings;

/// <summary>
/// Tests for the legacy ItemEditor.exe → Relique.exe path migration (#2080).
/// Applies to cached ReliquePath values loaded from RadoubSettings.json.
/// </summary>
public class ReliqueExePathMigrationTests
{
    [Fact]
    public void Migrate_WindowsLegacyPath_RewritesToRelique()
    {
        var result = ReliqueExePathMigration.Migrate(@"C:\Radoub\ItemEditor.exe");
        Assert.Equal(@"C:\Radoub\Relique.exe", result);
    }

    [Fact]
    public void Migrate_WindowsLegacyPath_CaseInsensitiveExtension()
    {
        var result = ReliqueExePathMigration.Migrate(@"C:\Radoub\ItemEditor.EXE");
        Assert.Equal(@"C:\Radoub\Relique.exe", result);
    }

    [Fact]
    public void Migrate_LinuxLegacyPath_RewritesToRelique()
    {
        // Use /opt/ rather than /home/<user>/ so CI's privacy-scan regex doesn't
        // false-positive on the literal sample path.
        var result = ReliqueExePathMigration.Migrate("/opt/Radoub/ItemEditor");
        Assert.Equal("/opt/Radoub/Relique", result);
    }

    [Fact]
    public void Migrate_AlreadyMigratedPath_ReturnsUnchanged()
    {
        var input = @"C:\Radoub\Relique.exe";
        Assert.Equal(input, ReliqueExePathMigration.Migrate(input));
    }

    [Fact]
    public void Migrate_DirectoryNamedItemEditor_NotRewritten()
    {
        // The "ItemEditor" segment is mid-path (a directory), not the filename.
        var input = @"C:\Radoub\ItemEditor\SomeOtherFile.exe";
        Assert.Equal(input, ReliqueExePathMigration.Migrate(input));
    }

    [Fact]
    public void Migrate_Null_ReturnsNull()
    {
        Assert.Null(ReliqueExePathMigration.Migrate(null));
    }

    [Fact]
    public void Migrate_Empty_ReturnsEmpty()
    {
        Assert.Equal("", ReliqueExePathMigration.Migrate(""));
    }

    [Fact]
    public void Migrate_UnrelatedExe_ReturnsUnchanged()
    {
        var input = @"C:\Radoub\Parley.exe";
        Assert.Equal(input, ReliqueExePathMigration.Migrate(input));
    }

    [Fact]
    public void Migrate_PreservesDirectoryWithMixedSeparators()
    {
        var result = ReliqueExePathMigration.Migrate(@"C:/Radoub\bin/ItemEditor.exe");
        Assert.Equal(@"C:/Radoub\bin/Relique.exe", result);
    }
}
