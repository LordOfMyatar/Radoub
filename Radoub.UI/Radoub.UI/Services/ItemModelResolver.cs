using Radoub.Formats.Common;
using Radoub.Formats.Services;
using Radoub.Formats.Uti;

namespace Radoub.UI.Services;

/// <summary>
/// Resolution result for an item's 3D model preview. Returned ResRefs are guaranteed
/// to exist as MDL resources via <see cref="IGameDataService.FindResource"/>; callers
/// can pass them straight to the MDL loader without further existence checks.
/// </summary>
public sealed record ItemModelResolution(
    IReadOnlyList<string> MdlResRefs,
    bool HasArmorParts,
    bool HasColorFields,
    bool HasModel);

/// <summary>
/// Maps a UTI item blueprint to the MDL ResRefs needed to render its 3D preview.
/// Pure logic; no I/O beyond IGameDataService resource existence checks.
///
/// Naming conventions follow BioWare Aurora Item Format Section 4.1:
/// - Simple/Layered: ItemClass_NNN (e.g., "w_swrd_005", "cloak_003")
/// - Composite: ItemClass_p_NNN where p is b/m/t for ModelPart1/2/3 (e.g., "wdbsw_b_011")
/// - Armor: prefix_bodypartNNN where prefix is creature-derived (e.g., "pmh0_chest005").
///   Item editing has no creature context, so a configurable mannequin prefix is used.
/// </summary>
public sealed class ItemModelResolver
{
    private readonly BaseItemTypeService _baseItemTypeService;
    private readonly IGameDataService _gameDataService;
    private readonly string _armorMannequinPrefix;

    private static readonly Dictionary<string, string> ArmorPartKeyToMdlSuffix =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Belt"] = "belt",
            ["LBicep"] = "bicepl",
            ["RBicep"] = "bicepr",
            ["Torso"] = "chest",
            ["LFoot"] = "footl",
            ["RFoot"] = "footr",
            ["LFArm"] = "forel",
            ["RFArm"] = "forer",
            ["LHand"] = "handl",
            ["RHand"] = "handr",
            ["LThigh"] = "legl",
            ["RThigh"] = "legr",
            ["Neck"] = "neck",
            ["Pelvis"] = "pelvis",
            ["Robe"] = "robe",
            ["LShin"] = "shinl",
            ["RShin"] = "shinr",
            ["LShoul"] = "shol",
            ["RShoul"] = "shor",
        };

    public ItemModelResolver(
        BaseItemTypeService baseItemTypeService,
        IGameDataService gameDataService,
        string armorMannequinPrefix = "pmh0")
    {
        _baseItemTypeService = baseItemTypeService;
        _gameDataService = gameDataService;
        _armorMannequinPrefix = armorMannequinPrefix;
    }

    public ItemModelResolution Resolve(UtiFile uti)
    {
        ArgumentNullException.ThrowIfNull(uti);

        var typeInfo = _baseItemTypeService
            .GetBaseItemTypes()
            .FirstOrDefault(t => t.BaseItemIndex == uti.BaseItem);

        if (typeInfo == null)
            return new ItemModelResolution(Array.Empty<string>(), false, false, false);

        var itemClass = _gameDataService.Get2DAValue("baseitems", uti.BaseItem, "ItemClass");
        var hasArmorParts = typeInfo.HasArmorParts;
        var hasColorFields = typeInfo.HasColorFields;

        IReadOnlyList<string> resRefs = typeInfo.ModelType switch
        {
            0 or 1 => ResolveSingle(itemClass, uti.ModelPart1),
            2 => ResolveComposite(itemClass, uti.ModelPart1, uti.ModelPart2, uti.ModelPart3),
            3 => ResolveArmor(uti.ArmorParts),
            _ => Array.Empty<string>(),
        };

        return new ItemModelResolution(resRefs, hasArmorParts, hasColorFields, resRefs.Count > 0);
    }

    private IReadOnlyList<string> ResolveSingle(string? itemClass, byte partNumber)
    {
        if (string.IsNullOrEmpty(itemClass) || itemClass == "****" || partNumber == 0)
            return Array.Empty<string>();

        var resRef = $"{itemClass}_{partNumber:D3}".ToLowerInvariant();
        return ResourceExists(resRef) ? new[] { resRef } : Array.Empty<string>();
    }

    private IReadOnlyList<string> ResolveComposite(string? itemClass, byte b, byte m, byte t)
    {
        if (string.IsNullOrEmpty(itemClass) || itemClass == "****")
            return Array.Empty<string>();

        var candidates = new[]
        {
            ("b", b),
            ("m", m),
            ("t", t),
        };

        var found = new List<string>();
        foreach (var (position, partNumber) in candidates)
        {
            if (partNumber == 0)
                continue;

            var resRef = $"{itemClass}_{position}_{partNumber:D3}".ToLowerInvariant();
            if (ResourceExists(resRef))
                found.Add(resRef);
        }

        return found;
    }

    private IReadOnlyList<string> ResolveArmor(Dictionary<string, byte> armorParts)
    {
        if (armorParts.Count == 0)
            return Array.Empty<string>();

        var found = new List<string>();
        foreach (var (key, partNumber) in armorParts)
        {
            if (partNumber == 0)
                continue;

            if (!ArmorPartKeyToMdlSuffix.TryGetValue(key, out var suffix))
                continue;

            var resRef = $"{_armorMannequinPrefix}_{suffix}{partNumber:D3}".ToLowerInvariant();
            if (ResourceExists(resRef))
                found.Add(resRef);
        }

        return found;
    }

    private bool ResourceExists(string resRef) =>
        _gameDataService.FindResource(resRef, ResourceTypes.Mdl) != null;
}
