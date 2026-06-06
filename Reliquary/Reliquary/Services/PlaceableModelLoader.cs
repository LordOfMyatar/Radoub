using Radoub.Formats.Common;
using Radoub.Formats.Logging;
using Radoub.Formats.Mdl;
using Radoub.Formats.Services;

namespace PlaceableEditor.Services;

/// <summary>
/// Loads a placeable's 3D model for preview: appearance id → model name (placeables.2da) →
/// raw MDL bytes (BIF/HAK/Override) → parsed <see cref="MdlModel"/>. This is the lightweight
/// raw-MDL path the Sprint 1 spike confirmed (no ModelService creature-composition needed);
/// placeables are static, so super-model animation merging is skipped.
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

        var bytes = _gameData.FindResource(modelName, ResourceTypes.Mdl);
        if (bytes is null || bytes.Length == 0) return null;

        try
        {
            return new MdlReader().Parse(bytes);
        }
        catch (System.Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN,
                $"Reliquary: failed to parse model '{modelName}' (appearance {appearanceId}): {ex.Message}");
            return null;
        }
    }
}
