using System.Numerics;
using Radoub.Formats.Common;
using Radoub.Formats.Logging;
using Radoub.Formats.Mdl;
using Radoub.Formats.Services;

namespace Radoub.UI.Services;

/// <summary>
/// Composes a single <see cref="MdlModel"/> from a skeleton + body-part MDLs (armor, creature body)
/// or from a flat list of part MDLs (composite weapons). Generic over the source of parts —
/// callers handle resolution (creature appearance, item ArmorParts dict, composite weapon parts)
/// and pass the resolved <c>(partType, resRef)</c> tuples to <see cref="Compose"/>.
///
/// Behavior preserved from QM's prior creature-renderer:
/// <list type="bullet">
/// <item>Mesh re-parenting under skeleton bones (so animation pose lookup walks the parent chain — #2124)</item>
/// <item>Body-part texture-name override (the "stale Bitmap field" workaround for body parts)</item>
/// <item>Head/neck/chest seam-overlap nudge for thin-overlap races (#1557)</item>
/// <item>Composite-bounds aggregation across all attached meshes</item>
/// </list>
/// </summary>
public sealed class MdlPartComposer
{
    private readonly IGameDataService _gameDataService;
    private readonly Func<string, bool, MdlModel?> _modelLoader;
    private readonly Func<string, string> _boneNameForPart;

    /// <summary>
    /// Minimum overlap in world units between adjacent body parts before the seam nudge kicks in.
    /// Measured: human=0.112, dwarf=0.090, elf=0.048, halfling≈0.05. Target = human-like.
    /// </summary>
    public const float MinSeamOverlap = 0.10f;

    /// <summary>Adjacent (upper, lower) part-type pairs that get the seam-overlap nudge.</summary>
    private static readonly (string Upper, string Lower)[] SeamPairs =
    {
        ("head", "neck"),
        ("neck", "chest"),
    };

    /// <param name="gameDataService">Used to check resource existence (resolution priority Override→HAK→BIF).</param>
    /// <param name="modelLoader">
    ///     Loads a parsed <see cref="MdlModel"/> from a ResRef. Bool argument is "include supermodel
    ///     animations" — true for the skeleton, false for body parts. Returns null if missing or unparseable.
    ///     Injected to allow callers (QM) to share a model cache across multiple composer calls.
    /// </param>
    /// <param name="boneNameForPart">
    ///     Maps a part type (e.g., "chest") to its target skeleton bone name (e.g., "torso_g").
    ///     Defaults to <see cref="MdlPartBoneMap.GetBoneNameForPart"/>.
    /// </param>
    public MdlPartComposer(
        IGameDataService gameDataService,
        Func<string, bool, MdlModel?> modelLoader,
        Func<string, string>? boneNameForPart = null)
    {
        _gameDataService = gameDataService;
        _modelLoader = modelLoader;
        _boneNameForPart = boneNameForPart ?? MdlPartBoneMap.GetBoneNameForPart;
    }

    /// <summary>
    /// Compose a multi-part model with parts attached to a skeleton's bones.
    /// Used for armor, creature body, and any other part-on-skeleton composition.
    /// </summary>
    /// <param name="skeletonResRef">Skeleton MDL ResRef (e.g., "pmh0"). Drives bone hierarchy + animations.</param>
    /// <param name="parts">Resolved (partType, partResRef) tuples. Skip already done — callers pass only existing parts.</param>
    /// <param name="adjustSeams">When true, nudges adjacent body parts together where overlap is below <see cref="MinSeamOverlap"/>.</param>
    /// <returns>Composite model, or null if no meshes were successfully attached.</returns>
    public MdlModel? Compose(
        string skeletonResRef,
        IReadOnlyList<(string PartType, string ResRef)> parts,
        bool adjustSeams = true)
    {
        if (parts.Count == 0)
            return null;

        var skeletonModel = _modelLoader(skeletonResRef, /* withSupermodelAnims */ true);
        var compositeModel = new MdlModel
        {
            Name = skeletonResRef,
            IsBinary = true,
        };

        // Inherit skeleton's animation list (and any merged from supermodel chain) so the
        // composite can play idle/walk/attack in the preview (#2124).
        if (skeletonModel?.Animations != null)
        {
            foreach (var anim in skeletonModel.Animations)
                compositeModel.Animations.Add(anim);
            compositeModel.SuperModel = skeletonModel.SuperModel;
        }

        // Use the skeleton's bone hierarchy as the composite root so animation pose lookup
        // finds matching bone names through the full parent chain (#2124).
        if (skeletonModel?.GeometryRoot != null)
            compositeModel.GeometryRoot = skeletonModel.GeometryRoot;

        var meshPartTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (partType, resRef) in parts)
        {
            TryAddBodyPart(compositeModel, skeletonModel, partType, resRef, meshPartTypes);
        }

        if (adjustSeams)
            AdjustSeamOverlaps(compositeModel, meshPartTypes);

        UpdateCompositeBounds(compositeModel);

        var meshCount = compositeModel.GetMeshNodes().Count();
        UnifiedLogger.LogApplication(LogLevel.INFO,
            $"MdlPartComposer.Compose: skeleton={skeletonResRef}, parts={parts.Count}, meshes={meshCount}, bounds={compositeModel.BoundingMin}-{compositeModel.BoundingMax}");

        return meshCount > 0 ? compositeModel : null;
    }

    /// <summary>
    /// Compose a multi-part model without a skeleton — meshes aggregated under a synthetic root.
    /// Used for composite weapons (twobladed sword, quarterstaff, double axe, dire mace) which
    /// have no skeletal animation; the three parts are fixed-position pieces of one weapon.
    /// </summary>
    public MdlModel? ComposeFlat(IReadOnlyList<string> partResRefs, string compositeName = "composite")
    {
        if (partResRefs.Count == 0)
            return null;

        var compositeModel = new MdlModel
        {
            Name = compositeName,
            IsBinary = true,
            GeometryRoot = new MdlNode
            {
                Name = "composite_root",
                Position = Vector3.Zero,
                Orientation = Quaternion.Identity,
                Scale = 1.0f,
            },
        };

        foreach (var resRef in partResRefs)
        {
            var partModel = _modelLoader(resRef, /* withSupermodelAnims */ false);
            if (partModel == null)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN, $"MdlPartComposer.ComposeFlat: '{resRef}' not loadable");
                continue;
            }

            foreach (var node in partModel.EnumerateAllNodes())
            {
                if (node is MdlTrimeshNode trimesh)
                {
                    node.Parent = compositeModel.GeometryRoot;
                    compositeModel.GeometryRoot!.Children.Add(node);
                }
            }
        }

        UpdateCompositeBounds(compositeModel);

        var meshCount = compositeModel.GetMeshNodes().Count();
        UnifiedLogger.LogApplication(LogLevel.INFO,
            $"MdlPartComposer.ComposeFlat: parts={partResRefs.Count}, meshes={meshCount}");

        return meshCount > 0 ? compositeModel : null;
    }

    private void TryAddBodyPart(
        MdlModel compositeModel,
        MdlModel? skeletonModel,
        string partType,
        string partResRef,
        Dictionary<string, string> meshPartTypes)
    {
        try
        {
            var partModel = _modelLoader(partResRef, /* withSupermodelAnims */ false);
            if (partModel == null)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN, $"MdlPartComposer: part '{partResRef}' not loadable");
                return;
            }

            var boneName = _boneNameForPart(partType);
            MdlNode? bone = null;
            if (skeletonModel?.GeometryRoot != null)
                bone = FindBoneByName(skeletonModel.GeometryRoot, boneName);

            // Compute a fallback world position for parts whose bones can't be found.
            // (The skeleton lookup may miss in synthetic test models or partial skeletons.)
            var fallbackPosition = Vector3.Zero;
            if (bone != null)
            {
                var worldMatrix = GetBoneWorldTransform(bone);
                if (Matrix4x4.Decompose(worldMatrix, out _, out _, out var translation))
                    fallbackPosition = translation;
            }

            foreach (var node in partModel.EnumerateAllNodes())
            {
                if (node is not MdlTrimeshNode trimesh) continue;

                // Body part MDL files have geometry at local origin. Body part bitmap fields
                // often contain stale data from reused file structures — derive the texture
                // name from the part ResRef instead.
                trimesh.Bitmap = partResRef;

                if (compositeModel.GeometryRoot == null)
                {
                    compositeModel.GeometryRoot = new MdlNode
                    {
                        Name = "composite_root",
                        Orientation = Quaternion.Identity,
                        Scale = 1.0f,
                    };
                }

                if (bone != null)
                {
                    // Parent under the bone — its bind position carries the world transform,
                    // so zero out the mesh's own offset to avoid double-application.
                    trimesh.Position = Vector3.Zero;
                    node.Parent = bone;
                    bone.Children.Add(node);
                }
                else
                {
                    // Fallback: skeleton bone missing, attach at the bone's would-be world
                    // position under the composite root.
                    trimesh.Position = fallbackPosition;
                    node.Parent = compositeModel.GeometryRoot;
                    compositeModel.GeometryRoot.Children.Add(node);
                }

                meshPartTypes[trimesh.Name] = partType;
            }
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN,
                $"MdlPartComposer.TryAddBodyPart: failed to add '{partType}' from '{partResRef}': {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Find a node in the bone hierarchy by name (case-insensitive).
    /// Public for tests and for QM's adapter that needs the same lookup.
    /// </summary>
    public static MdlNode? FindBoneByName(MdlNode root, string name)
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
    /// Calculate the full world transform of a bone by accumulating S*R*T matrices up the hierarchy.
    /// Mirrors <c>ModelPreviewGLControl.GetWorldTransform()</c> to handle parent rotations correctly.
    /// </summary>
    public static Matrix4x4 GetBoneWorldTransform(MdlNode bone)
    {
        var worldTransform = Matrix4x4.Identity;
        var current = bone;

        while (current != null)
        {
            var scale = Matrix4x4.CreateScale(current.Scale);
            var rotation = Matrix4x4.CreateFromQuaternion(current.Orientation);
            var translation = Matrix4x4.CreateTranslation(current.Position);

            // Row-major local transform: S * R * T (same as ModelPreviewGLControl.GetWorldTransform)
            var localTransform = scale * rotation * translation;

            // Accumulate: node * parent * grandparent * ... * root
            worldTransform = worldTransform * localTransform;

            current = current.Parent;
        }

        return worldTransform;
    }

    /// <summary>
    /// Adjust adjacent body parts (head/neck, neck/chest) so they overlap by at least
    /// <see cref="MinSeamOverlap"/> world units (#1557). NWN body parts rely on skeletal
    /// deformation for seamless joints; our static preview places rigid meshes that
    /// can leave thin seams visible under perspective projection.
    /// </summary>
    public static void AdjustSeamOverlaps(MdlModel compositeModel, Dictionary<string, string> meshPartTypes)
    {
        var meshes = compositeModel.GetMeshNodes().ToList();

        foreach (var (upperPartType, lowerPartType) in SeamPairs)
        {
            var upperMeshes = meshes.Where(m =>
                meshPartTypes.TryGetValue(m.Name, out var pt) &&
                string.Equals(pt, upperPartType, StringComparison.OrdinalIgnoreCase)).ToList();
            var lowerMeshes = meshes.Where(m =>
                meshPartTypes.TryGetValue(m.Name, out var pt) &&
                string.Equals(pt, lowerPartType, StringComparison.OrdinalIgnoreCase)).ToList();

            if (upperMeshes.Count == 0 || lowerMeshes.Count == 0)
                continue;

            float upperMinZ = float.MaxValue;
            foreach (var mesh in upperMeshes)
            {
                var transform = Matrix4x4.CreateScale(mesh.Scale)
                    * Matrix4x4.CreateFromQuaternion(mesh.Orientation)
                    * Matrix4x4.CreateTranslation(mesh.Position);
                foreach (var vertex in mesh.Vertices)
                {
                    var wv = Vector3.Transform(vertex, transform);
                    if (!float.IsNaN(wv.Z))
                        upperMinZ = Math.Min(upperMinZ, wv.Z);
                }
            }

            float lowerMaxZ = float.MinValue;
            foreach (var mesh in lowerMeshes)
            {
                var transform = Matrix4x4.CreateScale(mesh.Scale)
                    * Matrix4x4.CreateFromQuaternion(mesh.Orientation)
                    * Matrix4x4.CreateTranslation(mesh.Position);
                foreach (var vertex in mesh.Vertices)
                {
                    var wv = Vector3.Transform(vertex, transform);
                    if (!float.IsNaN(wv.Z))
                        lowerMaxZ = Math.Max(lowerMaxZ, wv.Z);
                }
            }

            if (upperMinZ == float.MaxValue || lowerMaxZ == float.MinValue)
                continue;

            float overlap = lowerMaxZ - upperMinZ;
            if (overlap >= MinSeamOverlap)
                continue;

            float deficit = MinSeamOverlap - overlap;
            float halfDeficit = deficit / 2f;

            foreach (var mesh in upperMeshes)
                mesh.Position = new Vector3(mesh.Position.X, mesh.Position.Y, mesh.Position.Z - halfDeficit);
            foreach (var mesh in lowerMeshes)
                mesh.Position = new Vector3(mesh.Position.X, mesh.Position.Y, mesh.Position.Z + halfDeficit);
        }
    }

    /// <summary>
    /// Aggregate the bounding box across all attached meshes, using each mesh's local S*R*T transform.
    /// </summary>
    public static void UpdateCompositeBounds(MdlModel model)
    {
        var minX = float.MaxValue;
        var minY = float.MaxValue;
        var minZ = float.MaxValue;
        var maxX = float.MinValue;
        var maxY = float.MinValue;
        var maxZ = float.MinValue;

        foreach (var mesh in model.GetMeshNodes())
        {
            var localTransform = Matrix4x4.CreateScale(mesh.Scale)
                * Matrix4x4.CreateFromQuaternion(mesh.Orientation)
                * Matrix4x4.CreateTranslation(mesh.Position);

            foreach (var vertex in mesh.Vertices)
            {
                var worldVert = Vector3.Transform(vertex, localTransform);

                minX = Math.Min(minX, worldVert.X);
                minY = Math.Min(minY, worldVert.Y);
                minZ = Math.Min(minZ, worldVert.Z);
                maxX = Math.Max(maxX, worldVert.X);
                maxY = Math.Max(maxY, worldVert.Y);
                maxZ = Math.Max(maxZ, worldVert.Z);
            }
        }

        if (minX != float.MaxValue)
        {
            model.BoundingMin = new Vector3(minX, minY, minZ);
            model.BoundingMax = new Vector3(maxX, maxY, maxZ);
            model.Radius = (model.BoundingMax - model.BoundingMin).Length() / 2f;
        }
    }
}
