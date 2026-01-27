using System;
using System.Collections.Generic;
using System.Linq;
using Radoub.Formats.Common;
using Radoub.Formats.Logging;
using Radoub.Formats.Mdl;
using Radoub.Formats.Services;
using Radoub.Formats.Utc;
using Radoub.Formats.Uti;

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
    /// Equipped armor can override certain body parts.
    /// </summary>
    public MdlModel? LoadCreatureModel(UtcFile creature)
    {
        var appearanceId = creature.AppearanceType;
        var gender = creature.Gender;
        var phenotype = creature.Phenotype;

        // Log equipped items for debugging
        UnifiedLogger.LogApplication(LogLevel.INFO,
            $"LoadCreatureModel: {creature.EquipItemList.Count} equipped items");
        foreach (var equip in creature.EquipItemList)
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                $"  Slot {equip.Slot}: '{equip.EquipRes}'");
        }

        // Check if this is a part-based model
        var modelType = _gameDataService.Get2DAValue("appearance", appearanceId, "MODELTYPE");
        if (modelType?.ToUpperInvariant().Contains("P") == true)
        {
            // Get armor-provided body part overrides
            var armorOverrides = GetArmorPartOverrides(creature);

            if (armorOverrides != null)
            {
                UnifiedLogger.LogApplication(LogLevel.INFO,
                    $"LoadCreatureModel: Armor overrides: {string.Join(", ", armorOverrides.Select(kv => $"{kv.Key}={kv.Value}"))}");
            }
            else
            {
                UnifiedLogger.LogApplication(LogLevel.INFO,
                    $"LoadCreatureModel: No armor overrides (naked creature or no chest armor)");
            }

            // Part-based model - load body parts with armor overrides
            return LoadPartBasedCreatureModel(creature, armorOverrides);
        }

        // Simple/Full model - load single model
        return LoadModelForAppearance(appearanceId, gender, phenotype);
    }

    /// <summary>
    /// Get body part overrides from equipped chest armor.
    /// </summary>
    private Dictionary<string, byte>? GetArmorPartOverrides(UtcFile creature)
    {
        var armor = LoadEquippedArmor(creature);
        if (armor == null)
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                $"GetArmorPartOverrides: No chest armor found");
            return null;
        }

        UnifiedLogger.LogApplication(LogLevel.INFO,
            $"GetArmorPartOverrides: Loaded armor '{armor.TemplateResRef}', BaseItem={armor.BaseItem}, ArmorParts.Count={armor.ArmorParts.Count}");

        if (armor.ArmorParts.Count > 0)
        {
            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"GetArmorPartOverrides: Armor has {armor.ArmorParts.Count} part overrides: {string.Join(", ", armor.ArmorParts.Select(kv => $"{kv.Key}={kv.Value}"))}");
            return armor.ArmorParts;
        }

        UnifiedLogger.LogApplication(LogLevel.DEBUG,
            $"GetArmorPartOverrides: Armor has no ArmorPart_ fields (not a part-based armor?)");
        return null;
    }

    /// <summary>
    /// Get armor colors from equipped chest armor.
    /// Returns null if no armor equipped.
    /// Note: Color index 0 is a valid color (first in palette), so we always return colors if armor exists.
    /// </summary>
    public (byte metal1, byte metal2, byte cloth1, byte cloth2, byte leather1, byte leather2)? GetArmorColors(UtcFile creature)
    {
        var armor = LoadEquippedArmor(creature);
        if (armor == null)
            return null;

        return (armor.Metal1Color, armor.Metal2Color,
                armor.Cloth1Color, armor.Cloth2Color,
                armor.Leather1Color, armor.Leather2Color);
    }

    /// <summary>
    /// Load the equipped chest armor UTI.
    /// </summary>
    private UtiFile? LoadEquippedArmor(UtcFile creature)
    {
        var chestItem = creature.EquipItemList.FirstOrDefault(e => e.Slot == EquipmentSlots.Chest);
        if (chestItem == null || string.IsNullOrEmpty(chestItem.EquipRes))
            return null;

        try
        {
            var utiData = _gameDataService.FindResource(chestItem.EquipRes.ToLowerInvariant(), ResourceTypes.Uti);
            if (utiData == null || utiData.Length == 0)
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"LoadEquippedArmor: Armor UTI '{chestItem.EquipRes}' not found in game resources");
                return null;
            }

            return UtiReader.Read(utiData);
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, $"LoadEquippedArmor: Failed to load armor '{chestItem.EquipRes}': {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Load a part-based creature model by combining body part models.
    /// </summary>
    /// <param name="creature">The creature to load</param>
    /// <param name="armorOverrides">Optional armor-provided body part overrides</param>
    private MdlModel? LoadPartBasedCreatureModel(UtcFile creature, Dictionary<string, byte>? armorOverrides = null)
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

        // Helper to get body part number, preferring armor override
        byte GetPartNumber(string armorKey, byte creatureValue) =>
            (armorOverrides != null && armorOverrides.TryGetValue(armorKey, out var armorValue) && armorValue > 0)
                ? armorValue
                : creatureValue;

        // Load each body part (armor overrides take precedence where applicable)
        // Head is special - uses AppearanceHead, not overridden by armor
        TryAddBodyPart(compositeModel, basePrefix, "head", creature.AppearanceHead);

        // Standard body parts (armor can override these)
        // NWN body part naming: belt, bicepl/bicepr, chest, footl/footr, forel/forer,
        // handl/handr, legl/legr, neck, pelvis, shinl/shinr, shol/shor
        TryAddBodyPart(compositeModel, basePrefix, "neck", GetPartNumber("Neck", creature.BodyPart_Neck));
        TryAddBodyPart(compositeModel, basePrefix, "chest", GetPartNumber("Torso", creature.BodyPart_Torso));
        TryAddBodyPart(compositeModel, basePrefix, "pelvis", GetPartNumber("Pelvis", creature.BodyPart_Pelvis));
        TryAddBodyPart(compositeModel, basePrefix, "belt", GetPartNumber("Belt", creature.BodyPart_Belt));

        // Arms (armor can override these) - NWN uses shol/shor, bicepl/bicepr, forel/forer, handl/handr
        TryAddBodyPart(compositeModel, basePrefix, "shol", GetPartNumber("LShoul", creature.BodyPart_LShoul));
        TryAddBodyPart(compositeModel, basePrefix, "shor", GetPartNumber("RShoul", creature.BodyPart_RShoul));
        TryAddBodyPart(compositeModel, basePrefix, "bicepl", GetPartNumber("LBicep", creature.BodyPart_LBicep));
        TryAddBodyPart(compositeModel, basePrefix, "bicepr", GetPartNumber("RBicep", creature.BodyPart_RBicep));
        TryAddBodyPart(compositeModel, basePrefix, "forel", GetPartNumber("LFArm", creature.BodyPart_LFArm));
        TryAddBodyPart(compositeModel, basePrefix, "forer", GetPartNumber("RFArm", creature.BodyPart_RFArm));
        TryAddBodyPart(compositeModel, basePrefix, "handl", GetPartNumber("LHand", creature.BodyPart_LHand));
        TryAddBodyPart(compositeModel, basePrefix, "handr", GetPartNumber("RHand", creature.BodyPart_RHand));

        // Legs (armor can override these) - NWN uses legl/legr (thighs), shinl/shinr, footl/footr
        TryAddBodyPart(compositeModel, basePrefix, "legl", GetPartNumber("LThigh", creature.BodyPart_LThigh));
        TryAddBodyPart(compositeModel, basePrefix, "legr", GetPartNumber("RThigh", creature.BodyPart_RThigh));
        TryAddBodyPart(compositeModel, basePrefix, "shinl", GetPartNumber("LShin", creature.BodyPart_LShin));
        TryAddBodyPart(compositeModel, basePrefix, "shinr", GetPartNumber("RShin", creature.BodyPart_RShin));
        TryAddBodyPart(compositeModel, basePrefix, "footl", GetPartNumber("LFoot", creature.BodyPart_LFoot));
        TryAddBodyPart(compositeModel, basePrefix, "footr", GetPartNumber("RFoot", creature.BodyPart_RFoot));

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

        // If not found with race-specific prefix, try human fallback
        // NWN shares many body part models across races using pmh0/pfh0 (human male/female)
        if (partModel == null && basePrefix.Length >= 2)
        {
            // Extract gender char (m/f) from position 1
            var genderChar = basePrefix[1];
            var humanPrefix = $"p{genderChar}h0"; // e.g., pmh0 or pfh0
            if (humanPrefix != basePrefix)
            {
                var humanPartName = $"{humanPrefix}_{partType}{partNumber:D3}";
                partModel = LoadModel(humanPartName);
                if (partModel != null)
                {
                    UnifiedLogger.LogApplication(LogLevel.INFO,
                        $"TryAddBodyPart: Using human fallback '{humanPartName}' for '{partName}'");
                    partName = humanPartName; // Update for logging
                }
            }
        }

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

                    // ALWAYS derive texture name for body parts - the texture field in body part MDLs
                    // often contains stale/garbage data from reused file structures. NWN expects
                    // body part textures to follow the naming convention: {prefix}_{partType}{number:D3}
                    // For race-specific parts (head), try the original race prefix first
                    // For limbs (which use human fallback models), use human texture prefix
                    var derivedTexture = partName; // Default to model name (e.g., pme0_head001)

                    // Check if this is a fallback model (human prefix loaded for non-human creature)
                    // If so, use the human texture; otherwise use race-specific texture
                    var isHumanFallback = partName.StartsWith("pmh0_") || partName.StartsWith("pfh0_");
                    if (!isHumanFallback)
                    {
                        // Race-specific model - use race texture first (e.g., pme0_head001)
                        derivedTexture = $"{basePrefix}_{partType}{partNumber:D3}";
                    }
                    // else: human fallback model - partName already has correct texture name

                    var oldBitmap = trimesh.Bitmap;
                    trimesh.Bitmap = derivedTexture;
                    if (!string.IsNullOrEmpty(oldBitmap) && oldBitmap != derivedTexture)
                    {
                        UnifiedLogger.LogApplication(LogLevel.DEBUG,
                            $"TryAddBodyPart: Overriding stale texture '{oldBitmap}' with '{derivedTexture}' for mesh '{trimesh.Name}'");
                    }

                    // Add to composite's root (or create root if needed)
                    if (compositeModel.GeometryRoot == null)
                    {
                        compositeModel.GeometryRoot = new Radoub.Formats.Mdl.MdlNode { Name = "composite_root" };
                    }
                    compositeModel.GeometryRoot.Children.Add(node);
                    meshCount++;
                    // Log vertex bounds and texture info for debugging
                    var hasUVs = trimesh.TextureCoords.Length > 0 && trimesh.TextureCoords[0].Length > 0;
                    UnifiedLogger.LogApplication(LogLevel.INFO,
                        $"TryAddBodyPart: Added mesh '{trimesh.Name}' from {partName}, " +
                        $"bitmap='{trimesh.Bitmap}', hasUVs={hasUVs}, " +
                        $"verts={trimesh.Vertices.Length}, faces={trimesh.Faces.Length}");
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
        // Body parts use NWN naming: bicepl, bicepr, forel, forer, etc.
        // Skeleton uses names like "head_g", "neck_g", "lbicep_g", etc.
        var boneName = partType switch
        {
            "head" => "head_g",
            "neck" => "neck_g",
            "chest" => "torso_g",
            "pelvis" => "pelvis_g",
            "belt" => "belt_g",
            "shol" => "lshoulder_g",
            "shor" => "rshoulder_g",
            "bicepl" => "lbicep_g",
            "bicepr" => "rbicep_g",
            "forel" => "lforearm_g",
            "forer" => "rforearm_g",
            "handl" => "lhand_g",
            "handr" => "rhand_g",
            "legl" => "lthigh_g",
            "legr" => "rthigh_g",
            "shinl" => "lshin_g",
            "shinr" => "rshin_g",
            "footl" => "lfoot_g",
            "footr" => "rfoot_g",
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
    /// Clear the model cache.
    /// </summary>
    public void ClearCache()
    {
        _modelCache.Clear();
    }
}
