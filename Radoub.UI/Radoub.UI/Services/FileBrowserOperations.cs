using System;
using System.IO;

namespace Radoub.UI.Services;

/// <summary>
/// Result of resolving a Copy or Rename destination path for the shared
/// file-browser context-menu actions (#2320). When <see cref="IsValid"/> is
/// false, <see cref="DestinationPath"/> is null and <see cref="ErrorMessage"/>
/// explains why.
/// </summary>
public sealed record FileBrowserPathResult(bool IsValid, string? DestinationPath, string? ErrorMessage)
{
    public static FileBrowserPathResult Ok(string destinationPath)
        => new(true, destinationPath, null);

    public static FileBrowserPathResult Fail(string message)
        => new(false, null, message);
}

/// <summary>
/// Pure-logic path computation and validation for the shared file-browser
/// Copy and Rename actions. Disk I/O and dialog UI live in
/// <c>FileBrowserPanelBase</c>; this class is the testable decision layer so
/// every tool's browser gets identical, verified validation behavior (#2320).
/// </summary>
public static class FileBrowserOperations
{
    /// <summary>
    /// Resolve the destination path for a Copy. The duplicate copy is written
    /// to the SAME directory as the source with a new ResRef. Validates the
    /// ResRef against Aurora filename rules.
    /// </summary>
    /// <param name="sourcePath">Full path of the file being copied.</param>
    /// <param name="newResRef">New ResRef (filename stem, no extension).</param>
    /// <param name="extension">File extension including the dot (e.g. ".uti").</param>
    public static FileBrowserPathResult ResolveCopyDestination(
        string sourcePath, string newResRef, string extension)
        => ResolveInSameDirectory(sourcePath, newResRef, extension, requireDifferentName: false);

    /// <summary>
    /// Resolve the destination path for a Rename. The renamed file stays in the
    /// SAME directory as the source. Validates the ResRef against Aurora
    /// filename rules, rejects path-traversal, and rejects a no-op rename to the
    /// same stem.
    /// </summary>
    /// <param name="sourcePath">Full path of the file being renamed.</param>
    /// <param name="newResRef">New ResRef (filename stem, no extension).</param>
    /// <param name="extension">File extension including the dot (e.g. ".utc").</param>
    public static FileBrowserPathResult ResolveRenameDestination(
        string sourcePath, string newResRef, string extension)
        => ResolveInSameDirectory(sourcePath, newResRef, extension, requireDifferentName: true);

    private static FileBrowserPathResult ResolveInSameDirectory(
        string sourcePath, string newResRef, string extension, bool requireDifferentName)
    {
        if (string.IsNullOrEmpty(sourcePath))
            return FileBrowserPathResult.Fail("Source path is missing.");

        var trimmed = newResRef?.Trim() ?? string.Empty;

        var filenameResult = AuroraFilenameValidator.Validate(trimmed);
        if (!filenameResult.IsValid)
            return FileBrowserPathResult.Fail(filenameResult.GetErrorMessage());

        var directory = Path.GetDirectoryName(sourcePath);
        if (string.IsNullOrEmpty(directory))
            return FileBrowserPathResult.Fail("Source directory could not be determined.");

        var currentStem = Path.GetFileNameWithoutExtension(sourcePath);
        if (requireDifferentName &&
            string.Equals(trimmed, currentStem, StringComparison.OrdinalIgnoreCase))
        {
            return FileBrowserPathResult.Fail("New name is unchanged.");
        }

        var destPath = Path.GetFullPath(Path.Combine(directory, trimmed + extension));

        // Defense-in-depth: even though AuroraFilenameValidator rejects path
        // separators, re-verify the resolved path stays inside the source
        // directory so a future validator change can't open a traversal hole.
        if (!IsPathWithinDirectory(destPath, directory))
            return FileBrowserPathResult.Fail("The new name resolves outside the source directory.");

        return FileBrowserPathResult.Ok(destPath);
    }

    /// <summary>
    /// True if <paramref name="candidatePath"/> resolves to a file located inside
    /// <paramref name="directory"/>. The directory is suffixed with a separator
    /// before the prefix comparison so sibling directories sharing a name prefix
    /// (e.g. "C:\mod" vs "C:\modfiles") cannot pass.
    /// </summary>
    private static bool IsPathWithinDirectory(string candidatePath, string directory)
    {
        if (string.IsNullOrEmpty(candidatePath) || string.IsNullOrEmpty(directory))
            return false;

        var fullCandidate = Path.GetFullPath(candidatePath);
        var fullDir = Path.GetFullPath(directory);

        if (!fullDir.EndsWith(Path.DirectorySeparatorChar) &&
            !fullDir.EndsWith(Path.AltDirectorySeparatorChar))
        {
            fullDir += Path.DirectorySeparatorChar;
        }

        return fullCandidate.StartsWith(fullDir, StringComparison.OrdinalIgnoreCase);
    }
}
