using System;
using Radoub.Formats.Uti;
using Radoub.Formats.Utc;
using Radoub.Formats.Utp;
using Radoub.Formats.Utm;

namespace Radoub.UI.Services.Palette;

/// <summary>Real <see cref="IBlueprintFileGateway"/> dispatching to the four blueprint formats by
/// resource type. The only class in the palette editor that knows the concrete UTI/UTC/UTP/UTM
/// readers and writers. Readers throw on bad data (non-nullable); writers expose
/// <c>Write(file)</c> returning <c>byte[]</c> directly.</summary>
public sealed class BlueprintFileGateway : IBlueprintFileGateway
{
    private readonly PaletteResourceType _type;
    public BlueprintFileGateway(PaletteResourceType type) => _type = type;

    public byte ReadPaletteId(string filePath) => _type switch
    {
        PaletteResourceType.Item      => UtiReader.Read(filePath).PaletteID,
        PaletteResourceType.Creature  => UtcReader.Read(filePath).PaletteID,
        PaletteResourceType.Placeable => UtpReader.Read(filePath).PaletteID,
        PaletteResourceType.Store     => UtmReader.Read(filePath).PaletteID,
        _ => throw new ArgumentOutOfRangeException(),
    };

    public byte ReadPaletteIdFromBytes(byte[] bytes) => _type switch
    {
        PaletteResourceType.Item      => UtiReader.Read(bytes).PaletteID,
        PaletteResourceType.Creature  => UtcReader.Read(bytes).PaletteID,
        PaletteResourceType.Placeable => UtpReader.Read(bytes).PaletteID,
        PaletteResourceType.Store     => UtmReader.Read(bytes).PaletteID,
        _ => throw new ArgumentOutOfRangeException(),
    };

    public byte[] ProduceBytesWithPaletteId(string filePath, byte paletteId) => _type switch
    {
        PaletteResourceType.Item =>
            UtiWriter.Write(WithId(UtiReader.Read(filePath), f => f.PaletteID = paletteId)),
        PaletteResourceType.Creature =>
            UtcWriter.Write(WithId(UtcReader.Read(filePath), f => f.PaletteID = paletteId)),
        PaletteResourceType.Placeable =>
            UtpWriter.Write(WithId(UtpReader.Read(filePath), f => f.PaletteID = paletteId)),
        PaletteResourceType.Store =>
            UtmWriter.Write(WithId(UtmReader.Read(filePath), f => f.PaletteID = paletteId)),
        _ => throw new ArgumentOutOfRangeException(),
    };

    private static T WithId<T>(T file, Action<T> setId) { setId(file); return file; }
}
