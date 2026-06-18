namespace Radoub.UI.Services.Palette;

/// <summary>
/// Reads and rewrites the <c>PaletteID</c> byte of a single blueprint file, abstracting the four
/// concrete formats (UTI/UTC/UTP/UTM) away from <see cref="LooseFileBlueprintStore"/>. Keeps the
/// store unit-testable (a fake gateway needs no disk) and confines format dispatch to one place.
/// </summary>
public interface IBlueprintFileGateway
{
    /// <summary>Read the blueprint file's current <c>PaletteID</c>.</summary>
    byte ReadPaletteId(string filePath);

    /// <summary>Produce the file's bytes with <paramref name="paletteId"/> applied (the original
    /// file is not modified — the bytes are staged by the save transaction).</summary>
    byte[] ProduceBytesWithPaletteId(string filePath, byte paletteId);

    /// <summary>Re-read the PaletteID from already-serialized blueprint bytes (the save-validate
    /// guard re-reads <see cref="ProduceBytesWithPaletteId"/> output before commit).</summary>
    byte ReadPaletteIdFromBytes(byte[] bytes);
}
