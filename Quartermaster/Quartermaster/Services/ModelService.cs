using System;
using System.Collections.Generic;
using System.Linq;
using Radoub.Formats.Common;
using Radoub.Formats.Logging;
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
    private MdlModel? _currentSkeleton;

    public ModelService(IGameDataService gameDataService)
    {
        _gameDataService = gameDataService;
    }

    /// <summary>
    /// Load the model for a creature based on its appearance settings.
    /// For part-based models, loads and combines body part models.
    /// </summary>
    public MdlModel? LoadCreatureModel(UtcFile creature)
    {
        var appearanceId = creature.AppearanceType;
        var gender = creature.Gender;
        var phenotype = creature.Phenotype;

        // Check if this is a part-based model
        var modelType = _gameDataService.Get2DAValue("appearance", appearanceId, "MODELTYPE");
        if (modelType?.ToUpperInvariant().Contains("P") == true)
        {
            // Part-based model - load body parts
            return LoadPartBasedCreatureModel(creature);
        }

        // Simple/Full model - load single model
        return LoadModelForAppearance(appearanceId, gender, phenotype);
    }

    /// <summary>
    /// Load a part-based creature model by combining body part models.
    /// </summary>
    private MdlModel? LoadPartBasedCreatureModel(UtcFile creature)
    {
        var appearanceId = creature.AppearanceType;
        var race = _gameDataService.Get2DAValue("appearance", appearanceId, "RACE");
        if (string.IsNullOrEmpty(race) || race == "****")
            return null;

        // Build base model prefix: p{gender}{race}{phenotype}
        // e.g., pfo0 for playable female O phenotype 0
        var genderChar = creature.Gender == 1 ? 'f' : 'm';
        var basePrefix = $"p{genderChar}{race.ToLowerInvariant()}{creature.Phenotype}";

        UnifiedLogger.LogApplication(LogLevel.INFO, $"LoadPartBasedCreatureModel: basePrefix={basePrefix}");

        // Load the base skeleton model first - it defines bone positions for body parts
        UnifiedLogger.LogApplication(LogLevel.DEBUG, $"LoadPartBasedCreatureModel: Attempting to load skeleton model '{basePrefix}'...");
        var skeletonModel = LoadModel(basePrefix);
        UnifiedLogger.LogApplication(LogLevel.DEBUG, $"LoadPartBasedCreatureModel: LoadModel returned {(skeletonModel != null ? "model" : "null")}");
        if (skeletonModel != null)
        {
            UnifiedLogger.LogApplication(LogLevel.INFO, $"LoadPartBasedCreatureModel: Loaded skeleton model {basePrefix}");
            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"  GeometryRoot: {skeletonModel.GeometryRoot?.Name ?? "null"}, children={skeletonModel.GeometryRoot?.Children.Count ?? 0}");
            // Log ALL skeleton nodes regardless of position
            var nodeCount = 0;
            foreach (var node in skeletonModel.EnumerateAllNodes())
            {
                nodeCount++;
                UnifiedLogger.LogApplication(LogLevel.INFO,
                    $"  Skeleton node '{node.Name}': type={node.NodeType}, pos={node.Position}, children={node.Children.Count}");
            }
            UnifiedLogger.LogApplication(LogLevel.INFO, $"  Total skeleton nodes: {nodeCount}");
        }
        else
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, $"LoadPartBasedCreatureModel: Skeleton model {basePrefix} not found");
        }

        // Create a composite model to hold all parts
        var compositeModel = new MdlModel
        {
            Name = basePrefix,
            IsBinary = true
        };

        // Store skeleton for bone position lookup
        _currentSkeleton = skeletonModel;

        // Load each body part
        // Head is special - uses AppearanceHead
        TryAddBodyPart(compositeModel, basePrefix, "head", creature.AppearanceHead);

        // Standard body parts
        TryAddBodyPart(compositeModel, basePrefix, "neck", creature.BodyPart_Neck);
        TryAddBodyPart(compositeModel, basePrefix, "chest", creature.BodyPart_Torso);
        TryAddBodyPart(compositeModel, basePrefix, "pelvis", creature.BodyPart_Pelvis);
        TryAddBodyPart(compositeModel, basePrefix, "belt", creature.BodyPart_Belt);

        // Arms
        TryAddBodyPart(compositeModel, basePrefix, "lshoul", creature.BodyPart_LShoul);
        TryAddBodyPart(compositeModel, basePrefix, "rshoul", creature.BodyPart_RShoul);
        TryAddBodyPart(compositeModel, basePrefix, "lbicep", creature.BodyPart_LBicep);
        TryAddBodyPart(compositeModel, basePrefix, "rbicep", creature.BodyPart_RBicep);
        TryAddBodyPart(compositeModel, basePrefix, "lfarm", creature.BodyPart_LFArm);
        TryAddBodyPart(compositeModel, basePrefix, "rfarm", creature.BodyPart_RFArm);
        TryAddBodyPart(compositeModel, basePrefix, "lhand", creature.BodyPart_LHand);
        TryAddBodyPart(compositeModel, basePrefix, "rhand", creature.BodyPart_RHand);

        // Legs
        TryAddBodyPart(compositeModel, basePrefix, "lthigh", creature.BodyPart_LThigh);
        TryAddBodyPart(compositeModel, basePrefix, "rthigh", creature.BodyPart_RThigh);
        TryAddBodyPart(compositeModel, basePrefix, "lshin", creature.BodyPart_LShin);
        TryAddBodyPart(compositeModel, basePrefix, "rshin", creature.BodyPart_RShin);
        TryAddBodyPart(compositeModel, basePrefix, "lfoot", creature.BodyPart_LFoot);
        TryAddBodyPart(compositeModel, basePrefix, "rfoot", creature.BodyPart_RFoot);

        // Calculate combined bounding box
        UpdateCompositeBounds(compositeModel);

        var meshCount = compositeModel.GetMeshNodes().Count();
        UnifiedLogger.LogApplication(LogLevel.INFO,
            $"LoadPartBasedCreatureModel: Composite model has {meshCount} meshes, bounds={compositeModel.BoundingMin}-{compositeModel.BoundingMax}");

        return meshCount > 0 ? compositeModel : null;
    }

    private void TryAddBodyPart(MdlModel compositeModel, string basePrefix, string partType, byte partNumber)
    {
        if (partNumber == 0)
        {
            UnifiedLogger.LogApplication(LogLevel.INFO, $"TryAddBodyPart: Skipping {partType} (partNumber=0)");
            return; // 0 often means "none" for optional parts
        }

        // Format: {basePrefix}_{partType}{partNumber:D3}
        // e.g., pfo0_head001
        var partName = $"{basePrefix}_{partType}{partNumber:D3}";
        var partModel = LoadModel(partName);

        if (partModel != null)
        {
            // Body part MDL files have geometry at local origin
            // We need to position them at the corresponding bone location from the skeleton
            var bonePosition = GetBonePositionForPart(partType);

            var meshCount = 0;
            // Add all mesh nodes from this part to the composite model
            foreach (var node in partModel.EnumerateAllNodes())
            {
                if (node is Radoub.Formats.Mdl.MdlTrimeshNode trimesh)
                {
                    // Position the mesh at the bone location
                    trimesh.Position = bonePosition;

                    // Add to composite's root (or create root if needed)
                    if (compositeModel.GeometryRoot == null)
                    {
                        compositeModel.GeometryRoot = new Radoub.Formats.Mdl.MdlNode { Name = "composite_root" };
                    }
                    compositeModel.GeometryRoot.Children.Add(node);
                    meshCount++;
                    // Log vertex bounds for debugging
                    if (trimesh.Vertices.Length > 0)
                    {
                        var minV = trimesh.Vertices[0];
                        var maxV = trimesh.Vertices[0];
                        foreach (var v in trimesh.Vertices)
                        {
                            minV = new System.Numerics.Vector3(
                                Math.Min(minV.X, v.X), Math.Min(minV.Y, v.Y), Math.Min(minV.Z, v.Z));
                            maxV = new System.Numerics.Vector3(
                                Math.Max(maxV.X, v.X), Math.Max(maxV.Y, v.Y), Math.Max(maxV.Z, v.Z));
                        }
                        UnifiedLogger.LogApplication(LogLevel.INFO,
                            $"TryAddBodyPart: Added mesh '{trimesh.Name}' from {partName}, verts={trimesh.Vertices.Length}, faces={trimesh.Faces.Length}, vertBounds={minV}-{maxV}, bonePos={bonePosition}");
                    }
                    else
                    {
                        UnifiedLogger.LogApplication(LogLevel.INFO,
                            $"TryAddBodyPart: Added mesh '{trimesh.Name}' from {partName}, verts=0, faces={trimesh.Faces.Length}");
                    }
                }
            }
            UnifiedLogger.LogApplication(LogLevel.INFO, $"TryAddBodyPart: {partName} added {meshCount} meshes");
        }
        else
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, $"TryAddBodyPart: {partName} not found");
        }
    }

    /// <summary>
    /// Get the world position for a body part by looking up its bone in the skeleton.
    /// Body part names map to skeleton bone names with _g suffix.
    /// </summary>
    private System.Numerics.Vector3 GetBonePositionForPart(string partType)
    {
        if (_currentSkeleton?.GeometryRoot == null)
            return System.Numerics.Vector3.Zero;

        // Map body part type to skeleton bone name
        // Body parts use names like "head", "neck", "chest", etc.
        // Skeleton uses names like "head_g", "neck_g", "torso_g", etc.
        var boneName = partType switch
        {
            "head" => "head_g",
            "neck" => "neck_g",
            "chest" => "torso_g",
            "pelvis" => "pelvis_g",
            "belt" => "belt_g",
            "lshoul" => "lshoulder_g",
            "rshoul" => "rshoulder_g",
            "lbicep" => "lbicep_g",
            "rbicep" => "rbicep_g",
            "lfarm" => "lforearm_g",
            "rfarm" => "rforearm_g",
            "lhand" => "lhand_g",
            "rhand" => "rhand_g",
            "lthigh" => "lthigh_g",
            "rthigh" => "rthigh_g",
            "lshin" => "lshin_g",
            "rshin" => "rshin_g",
            "lfoot" => "lfoot_g",
            "rfoot" => "rfoot_g",
            _ => partType + "_g"
        };

        // Find the bone in the skeleton and calculate its world position
        var bone = FindBoneByName(_currentSkeleton.GeometryRoot, boneName);
        if (bone == null)
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"GetBonePositionForPart: Bone '{boneName}' not found for part '{partType}'");
            return System.Numerics.Vector3.Zero;
        }

        // Calculate cumulative position by walking up the hierarchy
        var worldPos = GetBoneWorldPosition(bone);
        UnifiedLogger.LogApplication(LogLevel.DEBUG, $"GetBonePositionForPart: '{partType}' -> bone '{boneName}' worldPos={worldPos}");
        return worldPos;
    }

    private MdlNode? FindBoneByName(MdlNode root, string name)
    {
        if (root.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            return root;

        foreach (var child in root.Children)
        {
            var found = FindBoneByName(child, name);
            if (found != null)
                return found;
        }

        return null;
    }

    /// <summary>
    /// Calculate the world position of a bone by accumulating positions up the hierarchy.
    /// </summary>
    private System.Numerics.Vector3 GetBoneWorldPosition(MdlNode bone)
    {
        var position = bone.Position;
        var current = bone.Parent;

        while (current != null)
        {
            position += current.Position;
            current = current.Parent;
        }

        return position;
    }

    private void UpdateCompositeBounds(MdlModel model)
    {
        var minX = float.MaxValue;
        var minY = float.MaxValue;
        var minZ = float.MaxValue;
        var maxX = float.MinValue;
        var maxY = float.MinValue;
        var maxZ = float.MinValue;

        foreach (var mesh in model.GetMeshNodes())
        {
            // Include mesh position offset (bone position) when calculating world-space bounds
            var meshPos = mesh.Position;

            foreach (var vertex in mesh.Vertices)
            {
                var worldX = vertex.X + meshPos.X;
                var worldY = vertex.Y + meshPos.Y;
                var worldZ = vertex.Z + meshPos.Z;

                minX = Math.Min(minX, worldX);
                minY = Math.Min(minY, worldY);
                minZ = Math.Min(minZ, worldZ);
                maxX = Math.Max(maxX, worldX);
                maxY = Math.Max(maxY, worldY);
                maxZ = Math.Max(maxZ, worldZ);
            }
        }

        if (minX != float.MaxValue)
        {
            model.BoundingMin = new System.Numerics.Vector3(minX, minY, minZ);
            model.BoundingMax = new System.Numerics.Vector3(maxX, maxY, maxZ);
            model.Radius = (model.BoundingMax - model.BoundingMin).Length() / 2f;

            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"UpdateCompositeBounds: bounds=({minX:F2},{minY:F2},{minZ:F2}) to ({maxX:F2},{maxY:F2},{maxZ:F2}), radius={model.Radius:F2}");
        }
    }

    /// <summary>
    /// Load a model for a given appearance ID, gender, and phenotype.
    /// </summary>
    public MdlModel? LoadModelForAppearance(ushort appearanceId, byte gender = 0, int phenotype = 0)
    {
        // Get the model type and race from appearance.2da
        var modelType = _gameDataService.Get2DAValue("appearance", appearanceId, "MODELTYPE");
        var race = _gameDataService.Get2DAValue("appearance", appearanceId, "RACE");

        UnifiedLogger.LogApplication(LogLevel.INFO,
            $"LoadModelForAppearance: appearanceId={appearanceId}, modelType={modelType ?? "null"}, race={race ?? "null"}, gender={gender}, phenotype={phenotype}");

        if (string.IsNullOrEmpty(race) || race == "****")
        {
            UnifiedLogger.LogApplication(LogLevel.INFO, "LoadModelForAppearance: No valid race found");
            return null;
        }

        // Determine model name based on model type
        string modelName;
        if (modelType?.ToUpperInvariant().Contains("P") == true)
        {
            // Part-based model - RACE is just a letter (e.g., "O" for Half-Elf)
            // Model name format: p{gender}{race}{phenotype}
            // Examples: pmo0 (playable male O phenotype 0), pfo0 (playable female O phenotype 0)
            var genderChar = gender == 1 ? 'f' : 'm'; // 0=male, 1=female
            modelName = $"p{genderChar}{race.ToLowerInvariant()}{phenotype}";
        }
        else
        {
            // Simple/Full model - RACE is the model name directly
            modelName = race;
        }

        UnifiedLogger.LogApplication(LogLevel.INFO, $"LoadModelForAppearance: Loading model '{modelName}'");
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
        {
            UnifiedLogger.LogApplication(LogLevel.INFO, $"LoadModel: '{resRef}' from cache, hasModel={cached != null}");
            return cached;
        }

        // Try to load from game resources
        var modelData = _gameDataService.FindResource(resRef, ResourceTypes.Mdl);
        if (modelData == null || modelData.Length == 0)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, $"LoadModel: '{resRef}' not found in game resources");
            _modelCache[resRef] = null;
            return null;
        }

        UnifiedLogger.LogApplication(LogLevel.INFO, $"LoadModel: '{resRef}' found, {modelData.Length} bytes");

        try
        {
            var model = _mdlReader.Parse(modelData);
            var meshCount = model.GetMeshNodes().Count();
            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"LoadModel: '{resRef}' parsed OK, meshes={meshCount}, bounds={model.BoundingMin}-{model.BoundingMax}");
            _modelCache[resRef] = model;
            return model;
        }
        catch (Exception ex)
        {
            // Model parsing failed
            UnifiedLogger.LogApplication(LogLevel.WARN, $"LoadModel: '{resRef}' parse failed: {ex.Message}");
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
