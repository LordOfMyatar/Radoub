using System;
using System.Collections.Generic;
using System.Linq;
using Radoub.Formats.Common;
using Radoub.Formats.Logging;
using Radoub.Formats.Mdl;
using Radoub.Formats.Resolver;
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
    private readonly AppearanceService _appearanceService;
    private readonly MdlReader _mdlReader = new();
    // ConcurrentDictionary so the background attachment-list filter (#1485) can call LoadModel
    // while the UI thread renders without corrupting a plain Dictionary.
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, MdlModel?> _modelCache = new();
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, ResourceSource> _modelSourceCache = new();
    private readonly MdlPartComposer _composer;

    /// <summary>
    /// Source of the most recent model resolved by <see cref="LoadModel"/>. Drives the
    /// preview's texture-source policy (#1758): a HAK/Module creature should use its own
    /// pack's textures, a base/Override creature keeps the #1867 BIF-prefer behavior.
    /// Defaults to <see cref="ResourceSource.Bif"/> so part-based player bodies (composed
    /// from base parts) keep BIF-preferred textures.
    /// </summary>
    public ResourceSource LastLoadedModelSource { get; private set; } = ResourceSource.Bif;

    public ModelService(IGameDataService gameDataService)
    {
        _gameDataService = gameDataService;
        _appearanceService = new AppearanceService(gameDataService);
        _composer = new MdlPartComposer(_gameDataService, (resRef, _) => LoadModel(resRef));
    }

    /// <summary>
    /// Build the base model prefix for a part-based creature.
    /// Format: p{gender}{race}{phenotype} (e.g., "pfo0" = playable female, race O, phenotype 0).
    /// Gender resolves via <see cref="CreatureModelResolver.ResolveGenderFlavors"/> — every value
    /// other than 1 (Female) uses the male flavor, since NWN ships no other body-model flavors;
    /// the cross-gender fallback in <see cref="AppendPart"/> covers single-variant content (#2541).
    /// </summary>
    internal static string BuildModelPrefix(int gender, string race, int phenotype)
    {
        var flavor = CreatureModelResolver.ResolveGenderFlavors(gender)[0];
        return CreatureModelResolver.BuildPrefix(flavor, race, phenotype);
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

                var partModel = LoadPartBasedCreatureModel(creature, armorOverrides);
                // Part-based player/creature bodies are composed from base-game parts;
                // keep BIF-preferred textures (#1867) regardless of which part loaded last.
                LastLoadedModelSource = ResourceSource.Bif;
                return partModel;
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

        // Robe is armor-only (no creature body part); only added if armor has a robe override.
        var robePartNumber = (armorOverrides != null && armorOverrides.TryGetValue("Robe", out var robeValue) && robeValue > 0)
            ? robeValue : (byte)0;

        // #1989: load the robe FIRST and only suppress the body parts it covers if it actually
        // resolved. Otherwise (robe model missing for this mannequin) we'd strip the torso/arms/
        // legs AND have no robe to replace them — an empty render (the dana/pmo0 case).
        if (robePartNumber > 0)
            AppendPart(parts, basePrefix, "robe", robePartNumber);
        var robeEntry = parts.FirstOrDefault(p => p.PartType.Equals("robe", System.StringComparison.OrdinalIgnoreCase));
        bool robeActive = robeEntry.ResRef != null;
        if (robePartNumber > 0 && !robeActive)
            UnifiedLogger.LogApplication(LogLevel.WARN,
                $"LoadPartBasedCreatureModel: robe #{robePartNumber} did not resolve — keeping body parts (no suppression)");

        // #2541 Phase 2: only suppress the creature's arm parts if the robe supplies RENDERABLE
        // arm geometry. Robes like pfh0_robe005 (Dana) have Render=false arm trimeshes and an
        // armless torso+legs skin — suppressing the creature arms there leaves the creature with
        // no arms (#2398/#2116). Torso/legs are always suppressed when a robe is active.
        bool robeHasRenderableArms = true;
        if (robeActive)
        {
            robeHasRenderableArms = RobeArmGeometry.HasRenderableArmGeometry(LoadModel(robeEntry.ResRef));
            if (!robeHasRenderableArms)
                UnifiedLogger.LogApplication(LogLevel.INFO,
                    $"LoadPartBasedCreatureModel: robe '{robeEntry.ResRef}' has no renderable arm geometry — keeping creature arms");
        }

        // A robe is a near-complete posed body covering torso/pelvis/limbs; skip those individual
        // parts when a robe actually loaded — loading them additively causes z-fighting and gaps.
        void AddPart(string partType, byte partNumber)
        {
            if (RobePartSuppression.IsSuppressedByRobe(partType, robeActive, robeHasRenderableArms))
            {
                UnifiedLogger.LogApplication(LogLevel.INFO,
                    $"LoadPartBasedCreatureModel: skipping '{partType}' (covered by robe #{robePartNumber})");
                return;
            }
            AppendPart(parts, basePrefix, partType, partNumber);
        }

        // Head — special, uses AppearanceHead, never overridden by armor
        AddPart("head", creature.AppearanceHead);

        AddPart("neck", GetPartNumber("Neck", creature.BodyPart_Neck));
        AddPart("chest", GetPartNumber("Torso", creature.BodyPart_Torso));

        AddPart("pelvis", GetPartNumber("Pelvis", creature.BodyPart_Pelvis));
        AddPart("belt", GetPartNumber("Belt", creature.BodyPart_Belt));

        AddPart("shol", GetPartNumber("LShoul", creature.BodyPart_LShoul));
        AddPart("shor", GetPartNumber("RShoul", creature.BodyPart_RShoul));
        AddPart("bicepl", GetPartNumber("LBicep", creature.BodyPart_LBicep));
        AddPart("bicepr", GetPartNumber("RBicep", creature.BodyPart_RBicep));
        AddPart("forel", GetPartNumber("LFArm", creature.BodyPart_LFArm));
        AddPart("forer", GetPartNumber("RFArm", creature.BodyPart_RFArm));
        AddPart("handl", GetPartNumber("LHand", creature.BodyPart_LHand));
        AddPart("handr", GetPartNumber("RHand", creature.BodyPart_RHand));

        AddPart("legl", GetPartNumber("LThigh", creature.BodyPart_LThigh));
        AddPart("legr", GetPartNumber("RThigh", creature.BodyPart_RThigh));
        AddPart("shinl", GetPartNumber("LShin", creature.BodyPart_LShin));
        AddPart("shinr", GetPartNumber("RShin", creature.BodyPart_RShin));
        AddPart("footl", GetPartNumber("LFoot", creature.BodyPart_LFoot));
        AddPart("footr", GetPartNumber("RFoot", creature.BodyPart_RFoot));

        // Wings/tail (#1485): standalone supermodels (wingmodel/tailmodel.2da MODEL column),
        // scaled by the appearance's WING_TAIL_SCALE. Only meaningful for part-based (P) bodies —
        // full-body creatures bake wings into their single MDL and never reach this method.
        var supermodels = new List<(string Tag, string ResRef, float Scale)>();
        var wingTailScale = GetWingTailScale(_gameDataService, appearanceId);
        var wingRes = _appearanceService.GetWingModel(creature.Wings);
        if (!string.IsNullOrEmpty(wingRes))
        {
            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"[WingTail] wings={creature.Wings} -> '{wingRes}' scale={wingTailScale:F2}");
            supermodels.Add(("wings", wingRes!, wingTailScale));
        }
        var tailRes = _appearanceService.GetTailModel(creature.Tail);
        if (!string.IsNullOrEmpty(tailRes))
        {
            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"[WingTail] tail={creature.Tail} -> '{tailRes}' scale={wingTailScale:F2}");
            supermodels.Add(("tail", tailRes!, wingTailScale));
        }

        return _composer.Compose(basePrefix, parts, adjustSeams: true,
            supermodels: supermodels.Count > 0 ? supermodels : null);
    }

    /// <summary>
    /// Read appearance.2da WING_TAIL_SCALE for this appearance (the size factor for attached
    /// wings/tail). Defaults to 1.0 when the column is missing, "****", or unparseable.
    /// </summary>
    internal static float GetWingTailScale(IGameDataService gameDataService, ushort appearanceId)
    {
        var raw = gameDataService.Get2DAValue("appearance", appearanceId, "WING_TAIL_SCALE");
        if (!string.IsNullOrEmpty(raw) && raw != "****" &&
            float.TryParse(raw, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var scale) && scale > 0f)
        {
            return scale;
        }
        return 1.0f;
    }

    /// <summary>
    /// True if <paramref name="model"/> has a top-level geometry node named
    /// <paramref name="connectorNode"/> (case-insensitive). Wing/tail MDLs carry a 'wings'/'tail'
    /// connector node that the engine attaches to the body's matching bone; mounts (horses) reused
    /// in tailmodel.2da do not. Used to filter real wings/tails from the attachment lists (#1485).
    /// </summary>
    internal static bool HasConnectorNode(MdlModel? model, string connectorNode)
    {
        if (model?.GeometryRoot == null)
            return false;

        foreach (var child in model.GeometryRoot.Children)
        {
            if (string.Equals(child.Name, connectorNode, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Predicate for AppearanceService.GetAllWings/GetAllTails (#1485): true if the MODEL resref
    /// loads and is a real attachment (has the named connector node). Loads through the model cache,
    /// so repeated calls during list population are cheap. Tolerates load failures (returns false).
    /// </summary>
    public bool IsRealAttachment(string modelResRef, string connectorNode)
    {
        if (string.IsNullOrEmpty(modelResRef))
            return false;
        // Preserve the render texture-source policy: this list-filter probe must not let its
        // LoadModel calls clobber LastLoadedModelSource for the next real creature render (#1485).
        var savedSource = LastLoadedModelSource;
        try
        {
            return HasConnectorNode(LoadModel(modelResRef), connectorNode);
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                $"IsRealAttachment: '{modelResRef}' check failed: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
        finally
        {
            LastLoadedModelSource = savedSource;
        }
    }

    /// <summary>
    /// Resolve a body part ResRef through the data-driven fallback chain (#2541):
    /// race-specific → same-gender human → cross-gender race-specific → cross-gender human.
    /// Skips parts with partNumber=0 (none/invisible). Adds the resolved (partType, resRef) tuple
    /// to the parts list if any model file exists, logging which fallback path fired so custom
    /// content authors can see when a part missed its own race/gender model.
    /// </summary>
    private void AppendPart(List<(string PartType, string ResRef)> parts, string basePrefix, string partType, byte partNumber)
    {
        if (partNumber == 0)
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"AppendPart: skipping {partType} (partNumber=0)");
            return;
        }

        var resolution = CreatureModelResolver.ResolvePart(
            basePrefix, partType, partNumber, resRef => LoadModel(resRef) != null);

        if (resolution == null)
        {
            var attempted = MdlPartNaming.BuildBodyPartName(basePrefix, partType, partNumber);
            UnifiedLogger.LogApplication(LogLevel.WARN,
                $"AppendPart: '{attempted}' not found (no race/human/cross-gender fallback resolved)");
            return;
        }

        if (resolution.Path != PartResolutionPath.RaceSpecific)
        {
            var attempted = MdlPartNaming.BuildBodyPartName(basePrefix, partType, partNumber);
            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"AppendPart: '{attempted}' missing — using {resolution.Path} fallback '{resolution.ResRef}'");
        }

        parts.Add((partType, resolution.ResRef));
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
            if (_modelSourceCache.TryGetValue(resRef, out var cachedSource))
                LastLoadedModelSource = cachedSource;
            return cached;
        }

        var resourceResult = _gameDataService.FindResourceWithSource(resRef, ResourceTypes.Mdl);
        if (resourceResult == null || resourceResult.Data.Length == 0)
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"LoadModel: '{resRef}' not found in game resources");
            _modelCache[resRef] = null;
            return null;
        }

        LastLoadedModelSource = resourceResult.Source;
        _modelSourceCache[resRef] = resourceResult.Source;
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
