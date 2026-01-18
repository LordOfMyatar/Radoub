namespace Radoub.Formats.Itp;

/// <summary>
/// Represents an ITP (Palette) file used by Aurora Engine toolset.
/// ITP files define palette categories and blueprint organization.
/// Reference: BioWare Aurora ITP format spec, neverwinter.nim
/// </summary>
public class ItpFile
{
    /// <summary>
    /// File type signature - should be "ITP "
    /// </summary>
    public string FileType { get; set; } = "ITP ";

    /// <summary>
    /// File version - typically "V3.2"
    /// </summary>
    public string FileVersion { get; set; } = "V3.2";

    /// <summary>
    /// Resource type this palette is for (e.g., 2027 for UTC).
    /// Only present in skeleton palettes.
    /// </summary>
    public ushort? ResType { get; set; }

    /// <summary>
    /// Next usable category ID. Only present in skeleton palettes.
    /// </summary>
    public byte? NextUseableId { get; set; }

    /// <summary>
    /// Root nodes (MAIN list) of the palette tree.
    /// </summary>
    public List<PaletteNode> MainNodes { get; set; } = new();

    /// <summary>
    /// Get all category nodes from the palette tree (flattened).
    /// </summary>
    public IEnumerable<PaletteCategoryNode> GetCategories()
    {
        return FlattenCategories(MainNodes);
    }

    private static IEnumerable<PaletteCategoryNode> FlattenCategories(List<PaletteNode> nodes)
    {
        foreach (var node in nodes)
        {
            if (node is PaletteCategoryNode category)
            {
                yield return category;
            }
            else if (node is PaletteBranchNode branch)
            {
                foreach (var child in FlattenCategories(branch.Children))
                {
                    yield return child;
                }
            }
        }
    }
}

/// <summary>
/// Base class for palette tree nodes.
/// </summary>
public abstract class PaletteNode
{
    /// <summary>
    /// TLK string reference for node name.
    /// </summary>
    public uint? StrRef { get; set; }

    /// <summary>
    /// Direct name (used when StrRef is not present).
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// DELETE_ME field (for editing convenience, same as StrRef text).
    /// Only present in skeleton palettes.
    /// </summary>
    public string? DeleteMe { get; set; }

    /// <summary>
    /// Display type (0=DISPLAY_IF_NOT_EMPTY, 1=NEVER, 2=CUSTOM).
    /// </summary>
    public byte? DisplayType { get; set; }
}

/// <summary>
/// A branch node that can contain other branches or categories.
/// </summary>
public class PaletteBranchNode : PaletteNode
{
    /// <summary>
    /// Child nodes (can be branches or categories).
    /// </summary>
    public List<PaletteNode> Children { get; set; } = new();
}

/// <summary>
/// A category node that blueprints can be assigned to.
/// </summary>
public class PaletteCategoryNode : PaletteNode
{
    /// <summary>
    /// The unique ID for this category (matches PaletteID in blueprints).
    /// </summary>
    public byte Id { get; set; }

    /// <summary>
    /// Blueprint nodes under this category (in standard/custom palettes).
    /// </summary>
    public List<PaletteBlueprintNode> Blueprints { get; set; } = new();
}

/// <summary>
/// A blueprint node representing an actual game object.
/// Only present in standard and custom palettes, not skeleton.
/// </summary>
public class PaletteBlueprintNode : PaletteNode
{
    /// <summary>
    /// ResRef of the blueprint file.
    /// </summary>
    public string ResRef { get; set; } = string.Empty;

    /// <summary>
    /// Challenge rating (creature palettes only).
    /// </summary>
    public float? ChallengeRating { get; set; }

    /// <summary>
    /// Faction name (creature palettes only).
    /// </summary>
    public string? Faction { get; set; }
}

/// <summary>
/// Display type constants for palette nodes.
/// </summary>
public static class PaletteDisplayType
{
    /// <summary>
    /// Node appears only if it has children.
    /// </summary>
    public const byte DisplayIfNotEmpty = 0;

    /// <summary>
    /// Node never appears in toolset.
    /// </summary>
    public const byte DisplayNever = 1;

    /// <summary>
    /// Node only appears in custom palette or when assigning categories.
    /// </summary>
    public const byte DisplayCustom = 2;
}
