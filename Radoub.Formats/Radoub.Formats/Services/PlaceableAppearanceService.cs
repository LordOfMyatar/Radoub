using System;
using System.Collections.Generic;
using Radoub.Formats.Logging;

namespace Radoub.Formats.Services;

/// <summary>
/// Reads placeables.2da to resolve placeable appearance IDs to model and display
/// names. Mirrors the access pattern of Quartermaster's AppearanceService
/// (ModelName column, StrRef → GetString, LABEL fallback). #2291.
/// </summary>
public class PlaceableAppearanceService : IPlaceableAppearanceService
{
    private const string TwoDAName = "placeables";

    private readonly IGameDataService _gameDataService;
    private readonly object _cacheLock = new();
    private List<PlaceableAppearance>? _allCache;

    public PlaceableAppearanceService(IGameDataService gameDataService)
    {
        _gameDataService = gameDataService;
    }

    public string? GetModelName(uint id)
    {
        var modelName = _gameDataService.Get2DAValue(TwoDAName, (int)id, "ModelName");
        if (string.IsNullOrEmpty(modelName) || modelName == "****")
            return null;
        return modelName;
    }

    public string GetDisplayName(uint id)
    {
        // TLK name (StrRef) wins.
        var strRef = _gameDataService.Get2DAValue(TwoDAName, (int)id, "StrRef");
        if (!string.IsNullOrEmpty(strRef) && strRef != "****" && uint.TryParse(strRef, out var tlkRef))
        {
            var tlkName = _gameDataService.GetString(tlkRef);
            if (!string.IsNullOrEmpty(tlkName))
                return tlkName;
        }

        // Fall back to LABEL.
        var label = _gameDataService.Get2DAValue(TwoDAName, (int)id, "LABEL");
        if (!string.IsNullOrEmpty(label) && label != "****")
            return label;

        return $"Placeable {id}";
    }

    public PlaceableAppearance? GetById(uint id)
    {
        var label = _gameDataService.Get2DAValue(TwoDAName, (int)id, "LABEL");
        if (string.IsNullOrEmpty(label) || label == "****")
            return null;

        return new PlaceableAppearance(
            id,
            label,
            GetModelName(id) ?? "",
            GetDisplayName(id));
    }

    public IReadOnlyList<PlaceableAppearance> GetAll()
    {
        lock (_cacheLock)
        {
            if (_allCache != null)
                return _allCache;
        }

        var appearances = new List<PlaceableAppearance>();
        int rowCount = _gameDataService.Get2DA(TwoDAName)?.RowCount ?? 0;
        for (int i = 0; i < rowCount; i++)
        {
            try
            {
                var entry = GetById((uint)i);
                if (entry != null)
                    appearances.Add(entry);
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN,
                    $"PlaceableAppearanceService: failed to read placeables.2da row {i}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        lock (_cacheLock)
        {
            _allCache ??= appearances;
            return _allCache;
        }
    }

    public void InvalidateCache()
    {
        lock (_cacheLock)
        {
            _allCache = null;
        }
    }
}
