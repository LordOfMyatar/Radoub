using System.IO;
using Radoub.UI.Services;
using Xunit;

namespace Radoub.UI.Tests;

/// <summary>
/// Tests for FileBrowserOperations — pure-logic path computation and validation
/// for the shared file-browser Copy and Rename context-menu actions (#2320).
/// The disk I/O (File.Copy / File.Move) and dialog UI live in
/// FileBrowserPanelBase; this covers the testable decision logic.
/// </summary>
public class FileBrowserOperationsTests
{
    // ---- ResolveCopyDestination ---------------------------------------

    [Fact]
    public void ResolveCopyDestination_BuildsPathFromNewResRefAndExtension()
    {
        var result = FileBrowserOperations.ResolveCopyDestination(
            sourcePath: Path.Combine("C:", "mod", "sword.uti"),
            newResRef: "sword_copy",
            extension: ".uti");

        Assert.True(result.IsValid);
        Assert.Equal(Path.Combine("C:", "mod", "sword_copy.uti"), result.DestinationPath);
    }

    [Fact]
    public void ResolveCopyDestination_DestinationInSameDirectoryAsSource()
    {
        var result = FileBrowserOperations.ResolveCopyDestination(
            sourcePath: Path.Combine("C:", "mod", "sub", "ring.uti"),
            newResRef: "ring2",
            extension: ".uti");

        Assert.True(result.IsValid);
        Assert.Equal(
            Path.GetDirectoryName(Path.Combine("C:", "mod", "sub", "ring.uti")),
            Path.GetDirectoryName(result.DestinationPath));
    }

    [Fact]
    public void ResolveCopyDestination_InvalidResRef_IsInvalid()
    {
        var result = FileBrowserOperations.ResolveCopyDestination(
            sourcePath: Path.Combine("C:", "mod", "sword.uti"),
            newResRef: "Has Spaces",
            extension: ".uti");

        Assert.False(result.IsValid);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public void ResolveCopyDestination_TooLongResRef_IsInvalid()
    {
        var result = FileBrowserOperations.ResolveCopyDestination(
            sourcePath: Path.Combine("C:", "mod", "sword.uti"),
            newResRef: "this_name_is_way_too_long",
            extension: ".uti");

        Assert.False(result.IsValid);
    }

    [Fact]
    public void ResolveCopyDestination_EmptyResRef_IsInvalid()
    {
        var result = FileBrowserOperations.ResolveCopyDestination(
            sourcePath: Path.Combine("C:", "mod", "sword.uti"),
            newResRef: "",
            extension: ".uti");

        Assert.False(result.IsValid);
    }

    // ---- ResolveRenameDestination -------------------------------------

    [Fact]
    public void ResolveRenameDestination_BuildsPathInSameDirectory()
    {
        var result = FileBrowserOperations.ResolveRenameDestination(
            sourcePath: Path.Combine("C:", "mod", "old_name.utc"),
            newResRef: "new_name",
            extension: ".utc");

        Assert.True(result.IsValid);
        Assert.Equal(Path.Combine("C:", "mod", "new_name.utc"), result.DestinationPath);
    }

    [Fact]
    public void ResolveRenameDestination_InvalidResRef_IsInvalid()
    {
        var result = FileBrowserOperations.ResolveRenameDestination(
            sourcePath: Path.Combine("C:", "mod", "old_name.utc"),
            newResRef: "BadName",
            extension: ".utc");

        Assert.False(result.IsValid);
    }

    [Fact]
    public void ResolveRenameDestination_TraversalAttempt_IsInvalid()
    {
        // A new name that escapes the source directory must be rejected even if
        // it would pass the Aurora character check after Path.Combine resolves it.
        var result = FileBrowserOperations.ResolveRenameDestination(
            sourcePath: Path.Combine("C:", "mod", "old_name.utc"),
            newResRef: "..\\evil",
            extension: ".utc");

        Assert.False(result.IsValid);
    }

    [Fact]
    public void ResolveRenameDestination_SameName_IsInvalid()
    {
        // Renaming to the identical stem is a no-op and should be rejected.
        var result = FileBrowserOperations.ResolveRenameDestination(
            sourcePath: Path.Combine("C:", "mod", "old_name.utc"),
            newResRef: "old_name",
            extension: ".utc");

        Assert.False(result.IsValid);
    }
}
