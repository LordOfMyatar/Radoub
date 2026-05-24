using System;

namespace RadoubLauncher.Services;

/// <summary>
/// Pure staleness check for Marlinspike's cached search/item-resolution
/// services (#2072). The panel records the module working-directory's
/// last-write time at the moment it builds its services; on each search,
/// it compares the current mtime against that snapshot and rebuilds the
/// services if the directory changed.
///
/// Catches:
///   - ERF imports (write new files into the working directory)
///   - External edits / file additions / deletions
///   - File restores that bump mtime forward
///
/// Does NOT catch:
///   - In-place edits that don't bump the *directory* mtime (e.g. a file
///     overwritten with the same name). The HAK / palette cache layer
///     handles that separately.
/// </summary>
public static class SearchIndexStaleness
{
    /// <summary>
    /// Returns true if the index needs to be rebuilt.
    /// </summary>
    /// <param name="currentMtime">
    /// Current last-write time of the module working directory, or null if
    /// the directory no longer exists / cannot be stat'd.
    /// </param>
    /// <param name="lastIndexedMtime">
    /// The directory's last-write time at the moment the cached services
    /// were created, or null if no index has been built yet.
    /// </param>
    public static bool IsStale(DateTime? currentMtime, DateTime? lastIndexedMtime)
    {
        if (lastIndexedMtime == null) return true;
        if (currentMtime == null) return true;
        return currentMtime.Value > lastIndexedMtime.Value;
    }
}
