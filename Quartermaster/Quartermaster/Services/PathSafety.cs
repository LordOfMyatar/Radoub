using System;
using System.IO;

namespace Quartermaster.Services;

/// <summary>
/// Path containment checks used by rename/move flows.
/// </summary>
public static class PathSafety
{
    /// <summary>
    /// True if <paramref name="candidatePath"/> resolves to a file located inside
    /// <paramref name="directory"/> (or any of its subdirectories).
    /// Both inputs are resolved via <see cref="Path.GetFullPath(string)"/> and the
    /// directory is suffixed with <see cref="Path.DirectorySeparatorChar"/> before
    /// the prefix comparison so sibling directories sharing a name prefix
    /// (e.g. "C:\mod" vs "C:\modfiles") cannot pass the check.
    /// </summary>
    public static bool IsPathWithinDirectory(string candidatePath, string directory)
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
