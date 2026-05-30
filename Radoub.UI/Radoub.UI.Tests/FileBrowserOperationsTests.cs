using System.IO;
using Radoub.UI.Services;
using Xunit;

namespace Radoub.UI.Tests;

/// <summary>
/// Tests for FileBrowserOperations — pure-logic path computation and validation
/// for the shared file-browser Copy and Rename context-menu actions (#2320).
/// The disk I/O (File.Copy / File.Move) and dialog UI live in
/// FileBrowserPanelBase; this covers the testable decision logic.
///
/// Paths are built from a rooted base (Path.GetTempPath) and expectations are
/// computed with the same Path.GetFullPath the implementation uses, so the
/// tests pass identically on Windows and Linux (CI runs both). A bare
/// Path.Combine("C:", ...) is a relative path on Linux and breaks GetFullPath.
/// </summary>
public class FileBrowserOperationsTests
{
    // Rooted directory that exists on both OSes (e.g. C:\Temp\... or /tmp/...).
    private static readonly string ModDir = Path.Combine(Path.GetTempPath(), "radoub_mod");
    private static readonly string SubDir = Path.Combine(ModDir, "sub");

    private static string Expected(string dir, string stem, string ext)
        => Path.GetFullPath(Path.Combine(dir, stem + ext));

    // ---- ResolveCopyDestination ---------------------------------------

    [Fact]
    public void ResolveCopyDestination_BuildsPathFromNewResRefAndExtension()
    {
        var result = FileBrowserOperations.ResolveCopyDestination(
            sourcePath: Path.Combine(ModDir, "sword.uti"),
            newResRef: "sword_copy",
            extension: ".uti");

        Assert.True(result.IsValid);
        Assert.Equal(Expected(ModDir, "sword_copy", ".uti"), result.DestinationPath);
    }

    [Fact]
    public void ResolveCopyDestination_DestinationInSameDirectoryAsSource()
    {
        var result = FileBrowserOperations.ResolveCopyDestination(
            sourcePath: Path.Combine(SubDir, "ring.uti"),
            newResRef: "ring2",
            extension: ".uti");

        Assert.True(result.IsValid);
        Assert.Equal(
            Path.GetFullPath(SubDir),
            Path.GetDirectoryName(result.DestinationPath));
    }

    [Fact]
    public void ResolveCopyDestination_InvalidResRef_IsInvalid()
    {
        var result = FileBrowserOperations.ResolveCopyDestination(
            sourcePath: Path.Combine(ModDir, "sword.uti"),
            newResRef: "Has Spaces",
            extension: ".uti");

        Assert.False(result.IsValid);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public void ResolveCopyDestination_TooLongResRef_IsInvalid()
    {
        var result = FileBrowserOperations.ResolveCopyDestination(
            sourcePath: Path.Combine(ModDir, "sword.uti"),
            newResRef: "this_name_is_way_too_long",
            extension: ".uti");

        Assert.False(result.IsValid);
    }

    [Fact]
    public void ResolveCopyDestination_EmptyResRef_IsInvalid()
    {
        var result = FileBrowserOperations.ResolveCopyDestination(
            sourcePath: Path.Combine(ModDir, "sword.uti"),
            newResRef: "",
            extension: ".uti");

        Assert.False(result.IsValid);
    }

    // ---- ResolveRenameDestination -------------------------------------

    [Fact]
    public void ResolveRenameDestination_BuildsPathInSameDirectory()
    {
        var result = FileBrowserOperations.ResolveRenameDestination(
            sourcePath: Path.Combine(ModDir, "old_name.utc"),
            newResRef: "new_name",
            extension: ".utc");

        Assert.True(result.IsValid);
        Assert.Equal(Expected(ModDir, "new_name", ".utc"), result.DestinationPath);
    }

    [Fact]
    public void ResolveRenameDestination_InvalidResRef_IsInvalid()
    {
        var result = FileBrowserOperations.ResolveRenameDestination(
            sourcePath: Path.Combine(ModDir, "old_name.utc"),
            newResRef: "BadName",
            extension: ".utc");

        Assert.False(result.IsValid);
    }

    [Fact]
    public void ResolveRenameDestination_BackslashTraversalAttempt_IsInvalid()
    {
        // Backslash is rejected by the Aurora character check; on Linux it's a
        // valid filename char but still not in [a-z0-9_], so still invalid.
        var result = FileBrowserOperations.ResolveRenameDestination(
            sourcePath: Path.Combine(ModDir, "old_name.utc"),
            newResRef: "..\\evil",
            extension: ".utc");

        Assert.False(result.IsValid);
    }

    [Fact]
    public void ResolveRenameDestination_ForwardSlashTraversalAttempt_IsInvalid()
    {
        // Forward slash is a path separator on both OSes and not an Aurora-legal
        // character — must be rejected so a name can't escape the directory.
        var result = FileBrowserOperations.ResolveRenameDestination(
            sourcePath: Path.Combine(ModDir, "old_name.utc"),
            newResRef: "../evil",
            extension: ".utc");

        Assert.False(result.IsValid);
    }

    [Fact]
    public void ResolveRenameDestination_SameName_IsInvalid()
    {
        // Renaming to the identical stem is a no-op and should be rejected.
        var result = FileBrowserOperations.ResolveRenameDestination(
            sourcePath: Path.Combine(ModDir, "old_name.utc"),
            newResRef: "old_name",
            extension: ".utc");

        Assert.False(result.IsValid);
    }
}
