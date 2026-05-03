using System;
using System.Collections.Generic;
using System.Linq;
using Radoub.Formats.Common;
using Radoub.Formats.Logging;
using Radoub.Formats.Mdl;
using Radoub.Formats.Services;
using Radoub.Formats.Utc;
using Radoub.Formats.Uti;
using Radoub.UI.Services;

namespace Quartermaster.Services;

/// <summary>
/// Service for loading and managing 3D models for creature preview.
/// Handles creature-specific resolution from appearance.2da and equipped-armor lookup,
/// then delegates part composition to the shared <see cref="MdlPartComposer"/>.
/// </summary>
public class ModelService
{
    private readonly IGameDataService _gameDataService;
    private readonly MdlReader _mdlReader = new();
    private readonly Dictionary<string, MdlModel?> _modelCache = new();
    private readonly MdlPartComposer _composer;

    public ModelService(IGameDataService gameDataService)
    {
        _gameDataService = gameDataService;
        _composer = new MdlPartComposer(_gameDataService, (resRef, _) => LoadModel(resRef));
    }

    /// <summary>
    /// Build the base model prefix for a part-based creature.
    /// Format: p{gender}{race}{phenotype} (e.g., "pfo0" = playable female, race O, phenotype 0)
    /// </summary>
    internal static string BuildModelPrefix(int gender, string race, int phenotype)
    {
        var genderChar = gender == 1 ? 'f' : 'm';
        return $"p{genderChar}{race.ToLowerInvariant()}{phenotype}";
    }

    /// <summary>
    /// Load the model for a creature based on its appearance settings.
    /// For part-based models, loads and combines body part models.
    /// Equipped armor can override certain body parts.
    /// </summary>
    public MdlModel? LoadCreatureModel(UtcFile creature)
    {
        try
        {
            var appearanceId = creature.AppearanceType;
            var gender = creature.Gender;
            var phenotype = creature.Phenotype;

            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"LoadCreatureModel: {creature.EquipItemList.Count} equipped items");
            foreach (var equip in creature.EquipItemList)
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG,
                    $"  Slot {equip.Slot}: '{equip.EquipRes}'");
            }

            var modelType = _gameDataService.Get2DAValue("appearance", appearanceId, "MODELTYPE");
            if (modelType?.ToUpperInvariant().Contains("P") == true)
            {
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

                return LoadPartBasedCreatureModel(creature, armorOverrides);
            }

            return LoadModelForAppearance(appearanceId, gender, phenotype);
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR,
                $"LoadCreatureModel failed for appearance {creature.AppearanceType}: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
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
    /// Returns null if no armor equipped. Color index 0 is valid (first palette entry).
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
    /// Load a part-based creature model by combining body part models via the shared composer.
    /// </summary>
    /// <param name="creature">The creature to load</param>
    /// <param name="armorOverrides">Optional armor-provided body part overrides</param>
    private MdlModel? LoadPartBasedCreatureModel(UtcFile creature, Dictionary<string, byte>? armorOverrides = null)
    {
        var appearanceId = creature.AppearanceType;
        var race = _gameDataService.Get2DAValue("appearance", appearanceId, "RACE");
        if (string.IsNullOrEmpty(race) || race == "****")
            return null;

        var basePrefix = BuildModelPrefix(creature.Gender, race, creature.Phenotype);
        UnifiedLogger.LogApplication(LogLevel.INFO, $"LoadPartBasedCreatureModel: basePrefix={basePrefix}");

        // Helper: armor override beats creature value, but creature value of 0 (none/invisible)
        // always wins regardless of armor.
        byte GetPartNumber(string armorKey, byte creatureValue)
        {
            if (creatureValue == 0)
                return 0;

            if (armorOverrides != null && armorOverrides.TryGetValue(armorKey, out var armorValue) && armorValue > 0)
                return armorValue;

            return creatureValue;
        }

        var parts = new List<(string PartType, string ResRef)>();

        // Head — special, uses AppearanceHead, never overridden by armor
        AppendPart(parts, basePrefix, "head", creature.AppearanceHead);

        AppendPart(parts, basePrefix, "neck", GetPartNumber("Neck", creature.BodyPart_Neck));
        AppendPart(parts, basePrefix, "chest", GetPartNumber("Torso", creature.BodyPart_Torso));

        // Robe is armor-only (no creature body part); only added if armor has a robe override
        var robePartNumber = (armorOverrides != null && armorOverrides.TryGetValue("Robe", out var robeValue) && robeValue > 0)
            ? robeValue : (byte)0;
        AppendPart(parts, basePrefix, "robe", robePartNumber);

        AppendPart(parts, basePrefix, "pelvis", GetPartNumber("Pelvis", creature.BodyPart_Pelvis));
        AppendPart(parts, basePrefix, "belt", GetPartNumber("Belt", creature.BodyPart_Belt));

        AppendPart(parts, basePrefix, "shol", GetPartNumber("LShoul", creature.BodyPart_LShoul));
        AppendPart(parts, basePrefix, "shor", GetPartNumber("RShoul", creature.BodyPart_RShoul));
        AppendPart(parts, basePrefix, "bicepl", GetPartNumber("LBicep", creature.BodyPart_LBicep));
        AppendPart(parts, basePrefix, "bicepr", GetPartNumber("RBicep", creature.BodyPart_RBicep));
        AppendPart(parts, basePrefix, "forel", GetPartNumber("LFArm", creature.BodyPart_LFArm));
        AppendPart(parts, basePrefix, "forer", GetPartNumber("RFArm", creature.BodyPart_RFArm));
        AppendPart(parts, basePrefix, "handl", GetPartNumber("LHand", creature.BodyPart_LHand));
        AppendPart(parts, basePrefix, "handr", GetPartNumber("RHand", creature.BodyPart_RHand));

        AppendPart(parts, basePrefix, "legl", GetPartNumber("LThigh", creature.BodyPart_LThigh));
        AppendPart(parts, basePrefix, "legr", GetPartNumber("RThigh", creature.BodyPart_RThigh));
        AppendPart(parts, basePrefix, "shinl", GetPartNumber("LShin", creature.BodyPart_LShin));
        AppendPart(parts, basePrefix, "shinr", GetPartNumber("RShin", creature.BodyPart_RShin));
        AppendPart(parts, basePrefix, "footl", GetPartNumber("LFoot", creature.BodyPart_LFoot));
        AppendPart(parts, basePrefix, "footr", GetPartNumber("RFoot", creature.BodyPart_RFoot));

        return _composer.Compose(basePrefix, parts);
    }

    /// <summary>
    /// Resolve a body part ResRef with race-specific lookup first, falling back to human
    /// (pmh0/pfh0). Skips parts with partNumber=0 (none/invisible). Adds the resolved
    /// (partType, resRef) tuple to the parts list if a model file exists.
    /// </summary>
    private void AppendPart(List<(string PartType, string ResRef)> parts, string basePrefix, string partType, byte partNumber)
    {
        if (partNumber == 0)
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"AppendPart: skipping {partType} (partNumber=0)");
            return;
        }

        var raceResRef = MdlPartNaming.BuildBodyPartName(basePrefix, partType, partNumber);
        if (LoadModel(raceResRef) != null)
        {
            parts.Add((partType, raceResRef));
            return;
        }

        // Human fallback (pmh0/pfh0). NWN shares many body part models across races
        // using the human models as the default reference set.
        if (basePrefix.Length >= 2)
        {
            var genderChar = basePrefix[1];
            var humanPrefix = $"p{genderChar}h0";
            if (humanPrefix != basePrefix)
            {
                var humanResRef = MdlPartNaming.BuildBodyPartName(humanPrefix, partType, partNumber);
                if (LoadModel(humanResRef) != null)
                {
                    UnifiedLogger.LogApplication(LogLevel.INFO,
                        $"AppendPart: using human fallback '{humanResRef}' for '{raceResRef}'");
                    parts.Add((partType, humanResRef));
                    return;
                }
            }
        }

        UnifiedLogger.LogApplication(LogLevel.WARN, $"AppendPart: '{raceResRef}' not found (no human fallback either)");
    }

    /// <summary>
    /// Load a model for a given appearance ID, gender, and phenotype.
    /// </summary>
    public MdlModel? LoadModelForAppearance(ushort appearanceId, byte gender = 0, int phenotype = 0)
    {
        var modelType = _gameDataService.Get2DAValue("appearance", appearanceId, "MODELTYPE");
        var race = _gameDataService.Get2DAValue("appearance", appearanceId, "RACE");

        UnifiedLogger.LogApplication(LogLevel.INFO,
            $"LoadModelForAppearance: appearanceId={appearanceId}, modelType={modelType ?? "null"}, race={race ?? "null"}, gender={gender}, phenotype={phenotype}");

        if (string.IsNullOrEmpty(race) || race == "****")
        {
            UnifiedLogger.LogApplication(LogLevel.INFO, "LoadModelForAppearance: No valid race found");
            return null;
        }

        string modelName;
        if (modelType?.ToUpperInvariant().Contains("P") == true)
        {
            modelName = BuildModelPrefix(gender, race, phenotype);
        }
        else
        {
            modelName = race;
        }

        UnifiedLogger.LogApplication(LogLevel.INFO, $"LoadModelForAppearance: Loading model '{modelName}'");
        return LoadModel(modelName);
    }

    /// <summary>
    /// Load a model by ResRef name. Cached.
    /// </summary>
    public MdlModel? LoadModel(string resRef)
    {
        if (string.IsNullOrEmpty(resRef))
            return null;

        resRef = resRef.ToLowerInvariant();

        if (_modelCache.TryGetValue(resRef, out var cached))
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"LoadModel: '{resRef}' from cache, hasModel={cached != null}");
            return cached;
        }

        var resourceResult = _gameDataService.FindResourceWithSource(resRef, ResourceTypes.Mdl);
        if (resourceResult == null || resourceResult.Data.Length == 0)
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"LoadModel: '{resRef}' not found in game resources");
            _modelCache[resRef] = null;
            return null;
        }

        var modelData = resourceResult.Data;
        var sourceFile = System.IO.Path.GetFileName(resourceResult.SourcePath);
        UnifiedLogger.LogApplication(LogLevel.INFO, $"LoadModel: '{resRef}' from {resourceResult.Source} ({sourceFile}), {modelData.Length} bytes");

        try
        {
            var model = _mdlReader.Parse(modelData);
            var meshCount = model.GetMeshNodes().Count();
            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"LoadModel: '{resRef}' parsed OK, meshes={meshCount}, bounds={model.BoundingMin}-{model.BoundingMax}");
            MergeSuperModelAnimations(model, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            _modelCache[resRef] = model;
            return model;
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, $"LoadModel: '{resRef}' parse failed: {ex.Message}");
            _modelCache[resRef] = null;
            return null;
        }
    }

    /// <summary>
    /// NWN creatures inherit animations from a supermodel chain (e.g. a_ba, a_fa).
    /// Walk the chain and append every animation found to the leaf so the preview can play
    /// idle/walk/attack. Missing supermodels are logged and skipped (#2124).
    /// </summary>
    private void MergeSuperModelAnimations(MdlModel model, HashSet<string> visited)
    {
        var parentName = model.SuperModel;
        if (string.IsNullOrWhiteSpace(parentName) ||
            parentName.Equals("NULL", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        if (!visited.Add(parentName.ToLowerInvariant()))
        {
            UnifiedLogger.LogApplication(LogLevel.WARN,
                $"MergeSuperModelAnimations: cycle detected at '{parentName}' — stopping");
            return;
        }

        var parent = LoadModel(parentName);
        if (parent == null)
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                $"MergeSuperModelAnimations: supermodel '{parentName}' not loadable");
            return;
        }

        var existing = new HashSet<string>(
            model.Animations.Select(a => a.Name),
            StringComparer.OrdinalIgnoreCase);
        int added = 0;
        foreach (var anim in parent.Animations)
        {
            if (existing.Add(anim.Name))
            {
                model.Animations.Add(anim);
                added++;
            }
        }
        UnifiedLogger.LogApplication(LogLevel.INFO,
            $"MergeSuperModelAnimations: '{model.Name}' inherited {added} animations from '{parentName}'");
    }

    public void ClearCache()
    {
        _modelCache.Clear();
    }
}
