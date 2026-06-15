namespace Radoub.UI.Services.Palette;

/// <summary>
/// Staging seam for the blueprint half of the palette dual-write (#2476).
///
/// A blueprint's category membership lives in two places that must agree: the
/// <c>.itp</c> tree entry and the blueprint file's own <c>PaletteID</c> byte
/// (carried by <c>UtiFile</c>/<c>UtpFile</c>/<c>UtmFile</c>/<c>UtcFile</c>).
/// <see cref="PaletteReorgMutator"/> mutates the in-memory <c>.itp</c> tree and
/// records the matching <c>PaletteID</c> change through this interface, keeping
/// the mutator pure and format-agnostic — it never touches disk and never sees
/// the four concrete blueprint formats.
///
/// Implementations stage changes in memory; nothing reaches disk until
/// <see cref="PaletteSaveTransaction"/> commits the whole N-file set atomically.
/// </summary>
public interface IBlueprintPaletteStore
{
    /// <summary>
    /// The current (staged) <c>PaletteID</c> for the blueprint with this ResRef,
    /// or null if the pool has no such blueprint.
    /// </summary>
    byte? GetPaletteId(string resRef);

    /// <summary>
    /// Stage a new <c>PaletteID</c> for the blueprint with this ResRef. Returns
    /// false if the pool has no such blueprint (nothing staged). The change is
    /// in-memory only until the save transaction commits.
    /// </summary>
    bool SetPaletteId(string resRef, byte paletteId);

    /// <summary>
    /// True if the pool contains a blueprint with this ResRef.
    /// </summary>
    bool Contains(string resRef);
}
