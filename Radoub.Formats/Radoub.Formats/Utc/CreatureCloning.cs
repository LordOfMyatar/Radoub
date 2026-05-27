using Radoub.Formats.Bic;

namespace Radoub.Formats.Utc;

/// <summary>
/// Helpers for deep-cloning creature files and dispatching writes based on
/// runtime type + target extension. Used by Quartermaster's Down-Level / "save
/// level 1 copy" flow so a BIC source survives a copy → mutate → save cycle
/// without losing BIC-only fields (Age, Gold, Experience, QBList, ReputationList,
/// LvlStatList) and without being saved as UTC bytes under a .bic path.
///
/// Regression coverage: see CreatureCloneTests in Radoub.Formats.Tests.
/// Issue: #2249.
/// </summary>
public static class CreatureCloning
{
    /// <summary>
    /// Deep clone a creature, preserving its runtime type. A BicFile in produces
    /// a BicFile out (with all player-only fields intact); a plain UtcFile in
    /// produces a UtcFile out. The clone is independent of the original — mutating
    /// one does not affect the other.
    /// </summary>
    public static UtcFile Clone(UtcFile source)
    {
        if (source is BicFile bicSource)
        {
            var buffer = BicWriter.Write(bicSource);
            return BicReader.Read(buffer);
        }

        var utcBuffer = UtcWriter.Write(source);
        return UtcReader.Read(utcBuffer);
    }

    /// <summary>
    /// Save a creature to a file path, dispatching to the correct writer based
    /// on file extension. A .bic path requires the runtime type to be BicFile;
    /// any other extension writes UTC.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a .bic path is paired with a non-BIC creature. The caller
    /// should convert the UTC to a BIC via BicFile.FromUtcFile first.
    /// </exception>
    public static void Save(UtcFile creature, string filePath)
    {
        if (filePath.EndsWith(".bic", System.StringComparison.OrdinalIgnoreCase))
        {
            if (creature is BicFile bic)
            {
                BicWriter.Write(bic, filePath);
                return;
            }

            throw new InvalidOperationException(
                $"Cannot save a UTC creature to a .bic path ('{filePath}'). " +
                "Convert via BicFile.FromUtcFile first.");
        }

        UtcWriter.Write(creature, filePath);
    }
}
