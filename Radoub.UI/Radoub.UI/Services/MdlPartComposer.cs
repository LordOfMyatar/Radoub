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
    /// Minimum overlap in world units between adjacent body parts before the seam nudge kicks in,
    /// at human scale. Measured on full-size skeletons: human=0.112, dwarf=0.090, elf=0.048,
    /// halfling≈0.05. Target = human-like. For non-human-scale skeletons this is scaled by model
    /// height — see <see cref="SeamOverlapHeightRatio"/> and <see cref="GetSeamThreshold"/> (#1735).
    /// </summary>
    public const float MinSeamOverlap = 0.10f;

    /// <summary>
    /// Reference body height (world units) the <see cref="MinSeamOverlap"/> constant was tuned for —
    /// a standard NWN human is ≈1.9 tall. The effective seam threshold scales with the actual model
    /// height so a tiny but human-PROPORTIONED creature (Brownie ≈0.45× scale) gets a proportionally
    /// small threshold instead of the full human deficit, which would shove its head into its chest.
    /// </summary>
    public const float ReferenceBodyHeight = 1.9f;

    /// <summary>Seam threshold as a fraction of model height (= MinSeamOverlap / ReferenceBodyHeight).</summary>
    public const float SeamOverlapHeightRatio = MinSeamOverlap / ReferenceBodyHeight;

    /// <summary>
    /// Effective seam threshold for a model of the given world-space Z height. Scales linearly with
    /// height (#1735); falls back to the human-scale constant for degenerate/zero heights.
    /// </summary>
    public static float GetSeamThreshold(float modelHeight)
        => modelHeight > 0f ? modelHeight * SeamOverlapHeightRatio : MinSeamOverlap;

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
        bool adjustSeams = true,
        IReadOnlyList<(string Tag, string ResRef, float Scale)>? supermodels = null)
    {
        if (parts.Count == 0 && (supermodels == null || supermodels.Count == 0))
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

        // Use a CLONE of the skeleton's bone hierarchy as the composite root so animation pose
        // lookup finds matching bone names through the full parent chain (#2124) WITHOUT mutating
        // the cached skeleton model. ModelService caches and reuses parsed MdlModel instances; the
        // composer reparents part meshes onto bones and nudges part positions, so it must never
        // touch the shared cache — otherwise parts accumulate and nudges restack on every re-render
        // (#1735, "models get worse and worse when toggling races").
        if (skeletonModel?.GeometryRoot != null)
            compositeModel.GeometryRoot = CloneNode(skeletonModel.GeometryRoot, parent: null);

        var meshPartTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (partType, resRef) in parts)
        {
            TryAddBodyPart(compositeModel, partType, resRef, meshPartTypes);
        }

        // Wings/tail (#1485): standalone supermodels with their own bone hierarchy + meshes AND
        // their own animation set named to match the body (cwalkf, ...). Grafted at the composite
        // root (like a robe), scaled by WING_TAIL_SCALE, with their animations merged BY NAME into
        // the matching body animation so one active animation drives body + wings together.
        if (supermodels != null)
        {
            foreach (var (tag, resRef, scale) in supermodels)
                GraftSupermodel(compositeModel, resRef, tag, scale, meshPartTypes);
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

            EnsureCompositeRoot(compositeModel);

            // #1989: a robe is not a single body part — it is a near-complete posed body that
            // ships its own nested bone hierarchy (torso_g→rbicep_g→rforearm_g→rhand_g, etc.)
            // plus skin meshes (coat, arms). Splicing its individual meshes onto the skeleton's
            // bones discards the robe's internal local transforms and balloons the arms/legs.
            // Instead graft the robe's whole subtree, preserving its hierarchy, so each mesh keeps
            // the exact world transform Aurora computes from the robe's own node chain.
            if (IsFullBodyPart(partType, partModel))
            {
                GraftPartSubtree(compositeModel, partModel, partType, partResRef, meshPartTypes);
                return;
            }

            // Bone lookup targets the composite's OWN (cloned) skeleton root — never the cached
            // skeleton model. Attaching to / nudging the cached skeleton would corrupt it for the
            // next render (#1735).
            var boneName = _boneNameForPart(partType);
            MdlNode? bone = compositeModel.GeometryRoot != null
                ? FindBoneByName(compositeModel.GeometryRoot, boneName)
                : null;

            var fallbackPosition = Vector3.Zero;
            if (bone != null)
            {
                var worldMatrix = GetBoneWorldTransform(bone);
                if (Matrix4x4.Decompose(worldMatrix, out _, out _, out var translation))
                    fallbackPosition = translation;
            }

            foreach (var node in partModel.EnumerateAllNodes())
            {
                if (node is not MdlTrimeshNode sourceTrimesh) continue;

                // Clone the part mesh before attaching — the source is a cached MdlModel shared
                // across renders; we set Bitmap/Position/Parent on the CLONE only (#1735).
                var trimesh = CloneTrimeshShallow(sourceTrimesh);

                // Body part MDL files have geometry at local origin. Body part bitmap fields
                // often contain stale data from reused file structures — derive the texture
                // name from the part ResRef instead.
                trimesh.Bitmap = partResRef;

                if (bone != null)
                {
                    trimesh.Position = Vector3.Zero;
                    trimesh.Parent = bone;
                    bone.Children.Add(trimesh);
                }
                else
                {
                    trimesh.Position = fallbackPosition;
                    trimesh.Parent = compositeModel.GeometryRoot;
                    compositeModel.GeometryRoot!.Children.Add(trimesh);
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

    private static void EnsureCompositeRoot(MdlModel compositeModel)
    {
        if (compositeModel.GeometryRoot == null)
        {
            compositeModel.GeometryRoot = new MdlNode
            {
                Name = "composite_root",
                Orientation = Quaternion.Identity,
                Scale = 1.0f,
            };
        }
    }

    /// <summary>
    /// A "full body" part carries its own multi-node body hierarchy rather than a single mesh
    /// at the origin. Robes are the case that matters (#1989); detect by part type.
    /// </summary>
    private static bool IsFullBodyPart(string partType, MdlModel partModel) =>
        partType.Equals("robe", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Graft a full-body part's entire node subtree under the composite root, preserving its
    /// internal hierarchy and per-node local transforms. The part's root dummy aligns with the
    /// skeleton root (both at the same world origin), so its children attach directly under the
    /// composite root and reproduce the part's own world transforms (#1989).
    /// </summary>
    private void GraftPartSubtree(
        MdlModel compositeModel,
        MdlModel partModel,
        string partType,
        string partResRef,
        Dictionary<string, string> meshPartTypes)
    {
        if (partModel.GeometryRoot == null)
            return;

        EnsureCompositeRoot(compositeModel);
        var root = compositeModel.GeometryRoot!;

        // Graft the part root's CHILDREN (skip the part's own root dummy, whose translation
        // duplicates the skeleton root's). Each child subtree is cloned with hierarchy intact.
        int grafted = 0;
        foreach (var child in partModel.GeometryRoot.Children)
        {
            var clone = CloneNode(child, root);
            ApplyPartBitmap(clone, partResRef, partType, meshPartTypes);
            root.Children.Add(clone);
            grafted++;
        }

        UnifiedLogger.LogApplication(LogLevel.INFO,
            $"MdlPartComposer: grafted full-body part '{partType}' ({partResRef}) — {grafted} subtree(s)");
    }

    /// <summary>Set the texture name + record the part type on every mesh in a grafted subtree.</summary>
    private static void ApplyPartBitmap(MdlNode node, string partResRef, string partType, Dictionary<string, string> meshPartTypes)
    {
        if (node is MdlTrimeshNode mesh && mesh.Vertices.Length > 0)
        {
            mesh.Bitmap = partResRef;
            meshPartTypes[mesh.Name] = partType;
        }
        foreach (var child in node.Children)
            ApplyPartBitmap(child, partResRef, partType, meshPartTypes);
    }

    /// <summary>
    /// Record the part type on every mesh in a grafted subtree WITHOUT touching its Bitmap (#1485).
    /// Wing/tail meshes carry correct authored texture names — unlike body parts, they must not be
    /// overridden with the part resref.
    /// </summary>
    private static void TagSubtreeMeshes(MdlNode node, string partType, Dictionary<string, string> meshPartTypes)
    {
        if (node is MdlTrimeshNode mesh && mesh.Vertices.Length > 0)
            meshPartTypes[mesh.Name] = partType;
        foreach (var child in node.Children)
            TagSubtreeMeshes(child, partType, meshPartTypes);
    }

    /// <summary>
    /// Graft a wing/tail supermodel (#1485) under the composite root, preserving its internal bone
    /// hierarchy + meshes, scaled by <paramref name="scale"/> (the appearance's WING_TAIL_SCALE).
    /// The supermodel's own animations (named to match the body — cwalkf, cidle, ...) are merged
    /// BY NAME into the composite so one active animation drives the body and the wing/tail bones
    /// together. The model is loaded WITH supermodel animations so inheritors (e.g. c_tail_brs →
    /// c_tail_sil, which ships 0 own animations) still flap.
    /// </summary>
    public void GraftSupermodel(
        MdlModel compositeModel,
        string resRef,
        string tag,
        float scale,
        Dictionary<string, string> meshPartTypes)
    {
        try
        {
            var partModel = _modelLoader(resRef, /* withSupermodelAnims */ true);
            if (partModel?.GeometryRoot == null)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN,
                    $"MdlPartComposer.GraftSupermodel: '{tag}' model '{resRef}' not loadable — skipped");
                return;
            }

            EnsureCompositeRoot(compositeModel);
            var root = compositeModel.GeometryRoot!;

            // Wings/tail are authored at local origin and attach to a NAMED bone on the body
            // skeleton ('wings' on the upper back, 'tail' on the pelvis) that carries the correct
            // world transform — exactly like body parts attach to torso_g/head_g (#1485). Parent
            // the grafted subtree under that bone so it sits at back/rear height; fall back to the
            // composite root only if the skeleton lacks the bone (grafting at root would otherwise
            // place the wings at the model origin — i.e. the creature's feet).
            var attachBone = FindBoneByName(root, tag);
            var attachParent = attachBone ?? root;
            if (attachBone == null)
                UnifiedLogger.LogApplication(LogLevel.WARN,
                    $"MdlPartComposer.GraftSupermodel: no '{tag}' attach bone on skeleton — grafting at root (may mis-place)");

            // Graft the supermodel root's CHILDREN (skip its own root dummy, whose translation
            // duplicates the skeleton root's), cloning each subtree with hierarchy intact — same
            // mechanism as the robe graft (#1989). Apply WING_TAIL_SCALE to each grafted child's
            // root so the whole wing/tail subtree scales as a unit.
            int grafted = 0;
            foreach (var child in partModel.GeometryRoot.Children)
            {
                var clone = CloneNode(child, attachParent);
                if (scale > 0f && scale != 1.0f)
                    clone.Scale *= scale;
                // Record the part type for seam handling but PRESERVE each mesh's authored Bitmap
                // (#1485). Unlike body parts (whose Bitmap is stale and gets the part-resref override),
                // wing/tail meshes carry their own correct texture (e.g. c_dmsucubus); overwriting it
                // with the model resref points the renderer at a non-existent texture → grey wings.
                TagSubtreeMeshes(clone, tag, meshPartTypes);
                attachParent.Children.Add(clone);
                grafted++;
            }

            MergeSupermodelAnimationsByName(compositeModel, partModel);

            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"MdlPartComposer.GraftSupermodel: '{tag}' ({resRef}) scale={scale:F2} — {grafted} subtree(s), {partModel.Animations.Count} anim(s)");
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN,
                $"MdlPartComposer.GraftSupermodel: failed to graft '{tag}' from '{resRef}': {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Merge a wing/tail model's animations into the composite (#1485). The renderer plays ONE
    /// active animation and builds a node-name → pose dictionary from its GeometryRoot, so the
    /// wing's keyframed bones must live in the SAME animation tree as the body's. For each source
    /// animation: if the composite already has an animation of that name, graft the source's
    /// GeometryRoot children into the existing animation's tree (so cwalkf drives body + wing bones
    /// from one playhead). Names the body lacks are appended as new selectable entries.
    ///
    /// This is deliberately different from the dedup-by-name supermodel merge used for skeletons,
    /// which would DROP the wing's cwalkf because the body already owns that name.
    /// </summary>
    private static void MergeSupermodelAnimationsByName(MdlModel compositeModel, MdlModel partModel)
    {
        if (partModel.Animations.Count == 0)
            return;

        var byName = new Dictionary<string, MdlAnimation>(StringComparer.OrdinalIgnoreCase);
        foreach (var anim in compositeModel.Animations)
            byName[anim.Name] = anim;

        foreach (var src in partModel.Animations)
        {
            if (src.GeometryRoot == null)
                continue;

            if (byName.TryGetValue(src.Name, out var existing) && existing.GeometryRoot != null)
            {
                // Graft the wing's keyed node subtrees into the body animation's tree so a single
                // pose lookup covers both. Clone so the cached part model is never mutated (#1735).
                foreach (var child in src.GeometryRoot.Children)
                {
                    var clone = CloneNode(child, existing.GeometryRoot);
                    existing.GeometryRoot.Children.Add(clone);
                }
            }
            else
            {
                // Wing-only animation (body lacks the name) — append so it stays selectable.
                var copy = new MdlAnimation
                {
                    Name = src.Name,
                    Length = src.Length,
                    TransitionTime = src.TransitionTime,
                    AnimRoot = src.AnimRoot,
                    GeometryRoot = CloneNode(src.GeometryRoot, null),
                };
                compositeModel.Animations.Add(copy);
                byName[copy.Name] = copy;
            }
        }
    }

    /// <summary>
    /// Recursively clone a node hierarchy (transform + structure), giving each clone its own
    /// Children list and Parent pointer. Trimesh geometry arrays are shared (immutable during
    /// composition); only the per-node transform/parent state is duplicated. Used to build a
    /// composite skeleton root without mutating the cached source model (#1735).
    /// </summary>
    private static MdlNode CloneNode(MdlNode source, MdlNode? parent)
    {
        var clone = source is MdlTrimeshNode mesh
            ? CloneTrimeshShallow(mesh)
            : new MdlNode
            {
                NodeType = source.NodeType,
                Name = source.Name,
                Position = source.Position,
                Orientation = source.Orientation,
                Scale = source.Scale,
                Wirecolor = source.Wirecolor,
                InheritColor = source.InheritColor,
                PositionTimes = source.PositionTimes,
                PositionValues = source.PositionValues,
                OrientationTimes = source.OrientationTimes,
                OrientationValues = source.OrientationValues,
                ScaleTimes = source.ScaleTimes,
                ScaleValues = source.ScaleValues,
            };

        clone.Parent = parent;
        foreach (var child in source.Children)
            clone.Children.Add(CloneNode(child, clone));

        return clone;
    }

    /// <summary>
    /// Shallow-clone a trimesh node: copies the per-node transform/material/flags but SHARES the
    /// large immutable geometry arrays (vertices, normals, faces, UVs, colors). Composition only
    /// mutates Position / Bitmap / Parent on the clone, so sharing geometry is safe and cheap.
    /// Children are NOT copied here (CloneNode handles hierarchy).
    /// </summary>
    private static MdlTrimeshNode CloneTrimeshShallow(MdlTrimeshNode s)
    {
        // Preserve the runtime type so a skin mesh stays an MdlSkinNode (#1989): the
        // tiny-trimesh skip heuristic and mesh-info counts key on `is MdlSkinNode`, and a
        // robe's coat/arms are skins. Bone arrays are shared (immutable during composition).
        MdlTrimeshNode clone = s is MdlSkinNode sk
            ? new MdlSkinNode
            {
                BoneWeights = sk.BoneWeights,
                BoneNodeNames = sk.BoneNodeNames,
                BoneQuaternions = sk.BoneQuaternions,
                BoneTranslations = sk.BoneTranslations,
                NodeToBoneMap = sk.NodeToBoneMap,
            }
            : new MdlTrimeshNode();

        clone.NodeType = s.NodeType;
        clone.Name = s.Name;
        clone.Position = s.Position;
        clone.Orientation = s.Orientation;
        clone.Scale = s.Scale;
        clone.Wirecolor = s.Wirecolor;
        clone.InheritColor = s.InheritColor;
        clone.PositionTimes = s.PositionTimes;
        clone.PositionValues = s.PositionValues;
        clone.OrientationTimes = s.OrientationTimes;
        clone.OrientationValues = s.OrientationValues;
        clone.ScaleTimes = s.ScaleTimes;
        clone.ScaleValues = s.ScaleValues;
        clone.Vertices = s.Vertices;
        clone.Normals = s.Normals;
        clone.TextureCoords = s.TextureCoords;
        clone.VertexColors = s.VertexColors;
        clone.Faces = s.Faces;
        clone.Bitmap = s.Bitmap;
        clone.Bitmap2 = s.Bitmap2;
        clone.MaterialName = s.MaterialName;
        clone.Ambient = s.Ambient;
        clone.Diffuse = s.Diffuse;
        clone.Specular = s.Specular;
        clone.Shininess = s.Shininess;
        clone.Alpha = s.Alpha;
        clone.SelfIllumColor = s.SelfIllumColor;
        clone.Render = s.Render;
        clone.Shadow = s.Shadow;
        clone.Beaming = s.Beaming;
        clone.RotateTexture = s.RotateTexture;
        clone.TransparencyHint = s.TransparencyHint;
        clone.Tilefade = s.Tilefade;
        clone.RenderOrder = s.RenderOrder;
        return clone;
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
    public static Matrix4x4 GetMeshWorldTransform(MdlNode mesh) => GetBoneWorldTransform(mesh);

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

        // Seam threshold scales with model height so tiny human-proportioned creatures (Brownie)
        // aren't over-nudged by the human-scale constant (#1735). Height = world-space Z extent
        // across all part meshes.
        float modelMinZ = float.MaxValue, modelMaxZ = float.MinValue;
        foreach (var mesh in meshes)
        {
            var t = GetMeshWorldTransform(mesh);
            foreach (var vertex in mesh.Vertices)
            {
                var wz = Vector3.Transform(vertex, t).Z;
                if (float.IsNaN(wz)) continue;
                modelMinZ = Math.Min(modelMinZ, wz);
                modelMaxZ = Math.Max(modelMaxZ, wz);
            }
        }
        float modelHeight = (modelMaxZ > modelMinZ) ? modelMaxZ - modelMinZ : 0f;
        float threshold = GetSeamThreshold(modelHeight);

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

            // Measure overlap in WORLD space. Body-part meshes are reparented under skeleton
            // bones with their own Position zeroed (#1735), so the world Z lives in the bone
            // chain — the mesh-local transform alone reads every part at the bone-local origin.
            float upperMinZ = float.MaxValue;
            foreach (var mesh in upperMeshes)
            {
                var transform = GetMeshWorldTransform(mesh);
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
                var transform = GetMeshWorldTransform(mesh);
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

            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                $"Seam[{upperPartType}/{lowerPartType}]: overlap={overlap:F3} " +
                $"threshold={threshold:F3} (modelH={modelHeight:F3}) " +
                $"{(overlap >= threshold ? "no-nudge" : "nudge")}");

            if (overlap >= threshold)
                continue;

            // Cap the nudge so parts that already overlap are never driven PAST each other:
            // the total move can't exceed the existing overlap. For an actual gap (overlap ≤ 0)
            // there's nothing to push through, so close the full deficit.
            float deficit = threshold - overlap;
            if (overlap > 0f)
                deficit = Math.Min(deficit, overlap);
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
            // Use the FULL world transform (parent bone chain × mesh-local). Parts reparented
            // under skeleton bones carry their world position in the chain, not in mesh.Position
            // (#1735) — the mesh-local transform alone collapses every part to the bone origin.
            var worldTransform = GetMeshWorldTransform(mesh);

            foreach (var vertex in mesh.Vertices)
            {
                var worldVert = Vector3.Transform(vertex, worldTransform);

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
