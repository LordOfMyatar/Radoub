using System.IO;
using Quartermaster.Services;
using Xunit;

namespace Quartermaster.Tests;

public class PathSafetyTests
{
    [Fact]
    public void FileInsideDirectory_ReturnsTrue()
    {
        var dir = Path.Combine(Path.GetTempPath(), "qm_safety_a");
        var file = Path.Combine(dir, "creature.utc");
        Assert.True(PathSafety.IsPathWithinDirectory(file, dir));
    }

    [Fact]
    public void FileInSubdirectory_ReturnsTrue()
    {
        var dir = Path.Combine(Path.GetTempPath(), "qm_safety_b");
        var file = Path.Combine(dir, "sub", "creature.utc");
        Assert.True(PathSafety.IsPathWithinDirectory(file, dir));
    }

    [Fact]
    public void SiblingDirectoryWithPrefix_ReturnsFalse()
    {
        // Regression for #2252 — old StartsWith without trailing separator
        // accepted "C:\modfiles\evil.utc" when the guard directory was "C:\mod".
        var dir = Path.Combine(Path.GetTempPath(), "mod");
        var sibling = Path.Combine(Path.GetTempPath(), "modfiles", "evil.utc");
        Assert.False(PathSafety.IsPathWithinDirectory(sibling, dir));
    }

    [Fact]
    public void ParentDirectoryEscape_ReturnsFalse()
    {
        var dir = Path.Combine(Path.GetTempPath(), "qm_safety_c", "child");
        var escaped = Path.Combine(dir, "..", "..", "evil.utc");
        Assert.False(PathSafety.IsPathWithinDirectory(escaped, dir));
    }

    [Fact]
    public void DirectoryWithTrailingSeparator_NormalizesEquivalently()
    {
        var dir = Path.Combine(Path.GetTempPath(), "qm_safety_d") + Path.DirectorySeparatorChar;
        var file = Path.Combine(Path.GetTempPath(), "qm_safety_d", "creature.utc");
        Assert.True(PathSafety.IsPathWithinDirectory(file, dir));
    }

    [Fact]
    public void NullOrEmptyInputs_ReturnFalse()
    {
        Assert.False(PathSafety.IsPathWithinDirectory(null!, "C:\\mod"));
        Assert.False(PathSafety.IsPathWithinDirectory("C:\\mod\\f.utc", null!));
        Assert.False(PathSafety.IsPathWithinDirectory("", "C:\\mod"));
        Assert.False(PathSafety.IsPathWithinDirectory("C:\\mod\\f.utc", ""));
    }

    [Fact]
    public void CaseInsensitive_ReturnsTrue()
    {
        // Windows path comparison should be case-insensitive.
        var dir = Path.Combine(Path.GetTempPath(), "QM_Safety_E");
        var file = Path.Combine(Path.GetTempPath(), "qm_safety_e", "creature.utc");
        Assert.True(PathSafety.IsPathWithinDirectory(file, dir));
    }
}
