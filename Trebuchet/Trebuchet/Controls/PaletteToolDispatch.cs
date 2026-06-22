using Radoub.UI.Services.Palette;

namespace RadoubLauncher.Controls;

/// <summary>
/// Maps a palette editor resource type to the Radoub tool that edits that blueprint type, for
/// double-click-to-launch dispatch (#2485). Pure + internal so the mapping is unit-testable without
/// instantiating the UserControl. Mirrors Marlinspike's resource-type → tool dispatch.
/// </summary>
internal static class PaletteToolDispatch
{
    /// <summary>The tool name for a palette resource type, or null if none maps.</summary>
    public static string? ToolNameFor(PaletteResourceType type) => type switch
    {
        PaletteResourceType.Item      => "Relique",
        PaletteResourceType.Creature  => "Quartermaster",
        PaletteResourceType.Placeable => "Reliquary",
        PaletteResourceType.Store     => "Fence",
        _ => null,
    };
}
