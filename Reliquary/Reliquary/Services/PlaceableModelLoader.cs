using Radoub.Formats.Common;
using Radoub.Formats.Logging;
using Radoub.Formats.Mdl;
using Radoub.Formats.Services;

namespace PlaceableEditor.Services;

/// <summary>
/// Loads a placeable's 3D model for preview: appearance id → model name (placeables.2da) →
/// raw MDL bytes (BIF/HAK/Override) → parsed <see cref="MdlModel"/>. This is the lightweight
/// raw-MDL path the Sprint 1 spike confirmed (no ModelService creature-composition needed).
///
/// Supermodel animations ARE merged (#2595): ~29 placeables (e.g. tnp_list02 → tnp_list01,
/// zlc_ccp_b93 → plc_a07) declare their open/close/on/off state animations only in a supermodel.
/// Without the merge the placeable state selector found no animations and stayed hidden.
/// </summary>
public sealed class PlaceableModelLoader
{
    private readonly IGameDataService _gameData;
    private readonly IPlaceableAppearanceService _appearances;

    public PlaceableModelLoader(IGameDataService gameData, IPlaceableAppearanceService appearances)
    {
        _gameData = gameData;
        _appearances = appearances;
    }

    /// <summary>Resolve and parse the MDL for an appearance id, or null if unavailable.</summary>
    public MdlModel? Load(uint appearanceId)
    {
        var modelName = _appearances.GetModelName(appearanceId);
        if (string.IsNullOrEmpty(modelName)) return null;

        var model = ParseModel(modelName);
        if (model == null) return null;

        // Pull in open/close/on/off animations that live only in a supermodel so the state
        // selector surfaces them (#2595).
        SuperModelAnimationMerger.Merge(model, ParseModel);
        return model;
    }

    /// <summary>Resolve + parse a single MDL by resref (no supermodel merge), or null.</summary>
    private MdlModel? ParseModel(string modelName)
    {
        if (string.IsNullOrEmpty(modelName)) return null;

        var bytes = _gameData.FindResource(modelName, ResourceTypes.Mdl);
        if (bytes is null || bytes.Length == 0) return null;

        try
        {
            return new MdlReader().Parse(bytes);
        }
        catch (System.Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN,
                $"Reliquary: failed to parse model '{modelName}': {ex.Message}");
            return null;
        }
    }
}
