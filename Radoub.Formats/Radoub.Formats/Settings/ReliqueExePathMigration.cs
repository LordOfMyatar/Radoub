namespace Radoub.Formats.Settings;

/// <summary>
/// One-shot migration helper for the ItemEditor.exe → Relique.exe rename (#2080).
/// Applies to a stored ReliquePath value loaded from RadoubSettings.json — if the
/// value still points at the legacy ItemEditor binary, rewrite the final segment
/// in-place so the cached path resolves to the new executable name. Path layout
/// is otherwise preserved (directory, separators, case of surrounding segments).
///
/// Pure function. No I/O. Caller decides when to invoke.
/// </summary>
public static class ReliqueExePathMigration
{
    /// <summary>
    /// Returns a migrated path if <paramref name="path"/> ends with the legacy
    /// "ItemEditor.exe" (Windows) or "ItemEditor" (Linux/macOS) filename;
    /// otherwise returns the input unchanged. Null/empty passes through.
    /// </summary>
    public static string? Migrate(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return path;

        // Find the last path separator to identify the filename portion.
        int lastSep = -1;
        for (int i = path.Length - 1; i >= 0; i--)
        {
            var c = path[i];
            if (c == '/' || c == '\\')
            {
                lastSep = i;
                break;
            }
        }

        var fileName = lastSep >= 0 ? path.Substring(lastSep + 1) : path;
        var prefix = lastSep >= 0 ? path.Substring(0, lastSep + 1) : "";

        if (fileName.Equals("ItemEditor.exe", System.StringComparison.OrdinalIgnoreCase))
            return prefix + "Relique.exe";

        if (fileName.Equals("ItemEditor", System.StringComparison.Ordinal))
            return prefix + "Relique";

        return path;
    }
}
