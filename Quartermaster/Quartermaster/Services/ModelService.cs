using System;
using System.Collections.Generic;
using Radoub.Formats.Common;
using Radoub.Formats.Mdl;
using Radoub.Formats.Services;
using Radoub.Formats.Utc;

namespace Quartermaster.Services;

/// <summary>
/// Service for loading and managing 3D models for creature preview.
/// Handles model resolution from appearance.2da and resource loading.
/// </summary>
public class ModelService
{
    private readonly IGameDataService _gameDataService;
    private readonly MdlReader _mdlReader = new();
    private readonly Dictionary<string, MdlModel?> _modelCache = new();

    public ModelService(IGameDataService gameDataService)
    {
        _gameDataService = gameDataService;
    }

    /// <summary>
    /// Load the model for a creature based on its appearance settings.
    /// </summary>
    public MdlModel? LoadCreatureModel(UtcFile creature)
    {
        var appearanceId = creature.AppearanceType;
        return LoadModelForAppearance(appearanceId, creature.Phenotype);
    }

    /// <summary>
    /// Load a model for a given appearance ID and phenotype.
    /// </summary>
    public MdlModel? LoadModelForAppearance(ushort appearanceId, int phenotype = 0)
    {
        // Get the model type and race from appearance.2da
        var modelType = _gameDataService.Get2DAValue("appearance", appearanceId, "MODELTYPE");
        var race = _gameDataService.Get2DAValue("appearance", appearanceId, "RACE");

        if (string.IsNullOrEmpty(race) || race == "****")
            return null;

        // Determine model name based on model type
        string modelName;
        if (modelType?.ToUpperInvariant().Contains("P") == true)
        {
            // Part-based model - the RACE column is the base (e.g., "pfd0")
            // For preview, we just load the base skeleton model
            modelName = race;
        }
        else
        {
            // Simple/Full model - RACE is the model name directly
            modelName = race;
        }

        return LoadModel(modelName);
    }

    /// <summary>
    /// Load a model by ResRef name.
    /// </summary>
    public MdlModel? LoadModel(string resRef)
    {
        if (string.IsNullOrEmpty(resRef))
            return null;

        resRef = resRef.ToLowerInvariant();

        // Check cache
        if (_modelCache.TryGetValue(resRef, out var cached))
            return cached;

        // Try to load from game resources
        var modelData = _gameDataService.FindResource(resRef, ResourceTypes.Mdl);
        if (modelData == null || modelData.Length == 0)
        {
            _modelCache[resRef] = null;
            return null;
        }

        try
        {
            var model = _mdlReader.Parse(modelData);
            _modelCache[resRef] = model;
            return model;
        }
        catch (Exception)
        {
            // Model parsing failed
            _modelCache[resRef] = null;
            return null;
        }
    }

    /// <summary>
    /// Load a body part model for a part-based creature.
    /// </summary>
    /// <param name="baseRace">Base race string from appearance.2da (e.g., "pfd0")</param>
    /// <param name="partType">Part type: head, chest, neck, etc.</param>
    /// <param name="partNumber">Part variation number (e.g., 1 for _head001)</param>
    public MdlModel? LoadBodyPartModel(string baseRace, string partType, int partNumber)
    {
        if (string.IsNullOrEmpty(baseRace))
            return null;

        // Format: {baseRace}_{partType}{partNumber:D3}
        // e.g., pfd0_head001, pfd0_chest002
        var partName = $"{baseRace}_{partType}{partNumber:D3}";
        return LoadModel(partName);
    }

    /// <summary>
    /// Get the base race model reference for an appearance.
    /// </summary>
    public string? GetRaceModelRef(ushort appearanceId)
    {
        var race = _gameDataService.Get2DAValue("appearance", appearanceId, "RACE");
        if (string.IsNullOrEmpty(race) || race == "****")
            return null;
        return race.ToLowerInvariant();
    }

    /// <summary>
    /// Check if an appearance is part-based (dynamic body parts).
    /// </summary>
    public bool IsPartBasedAppearance(ushort appearanceId)
    {
        var modelType = _gameDataService.Get2DAValue("appearance", appearanceId, "MODELTYPE");
        return modelType?.ToUpperInvariant().Contains("P") == true;
    }

    /// <summary>
    /// Clear the model cache.
    /// </summary>
    public void ClearCache()
    {
        _modelCache.Clear();
    }
}
