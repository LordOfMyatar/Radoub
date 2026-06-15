namespace Radoub.UI.Services.Palette;

/// <summary>The Aurora resource type a palette organizes. Each maps to its own loose
/// custom palette, skeleton palette, and blueprint file extension.</summary>
public enum PaletteResourceType { Item, Creature, Placeable, Store }

/// <summary>Static descriptor for a <see cref="PaletteResourceType"/> — the on-disk filenames
/// and the loose-blueprint extension. Filenames follow the Aurora standard set.</summary>
public readonly record struct PaletteResourceDescriptor(
    PaletteResourceType Type,
    string CustomPaletteFile,
    string SkeletonPaletteFile,
    string BlueprintExtension);

/// <summary>Maps each <see cref="PaletteResourceType"/> to its on-disk palette filenames and
/// loose-blueprint extension.</summary>
public static class PaletteResourceTypeInfo
{
    public static PaletteResourceDescriptor For(PaletteResourceType type) => type switch
    {
        PaletteResourceType.Item      => new(type, "itempalcus.itp",      "itempalstd.itp",      "uti"),
        PaletteResourceType.Creature  => new(type, "creaturepalcus.itp",  "creaturepalstd.itp",  "utc"),
        PaletteResourceType.Placeable => new(type, "placeablepalcus.itp", "placeablepalstd.itp", "utp"),
        PaletteResourceType.Store     => new(type, "storepalcus.itp",     "storepalstd.itp",     "utm"),
        _ => throw new System.ArgumentOutOfRangeException(nameof(type)),
    };
}
