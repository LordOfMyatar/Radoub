// MDL (Model) file format structures for Neverwinter Nights
// Based on format documentation from NWN Wiki and Torlack's binary format specs
// License: BSD 3-Clause (compatible with nwnexplorer reference)

using System.Numerics;

namespace Radoub.Formats.Mdl;

/// <summary>
/// Node types in NWN MDL files.
/// </summary>
public enum MdlNodeType : uint
{
    Dummy = 0x00000001,
    Light = 0x00000003,
    Emitter = 0x00000005,
    Camera = 0x00000009,
    Reference = 0x00000011,
    Trimesh = 0x00000021,
    Skin = 0x00000061,
    Anim = 0x000000A1,
    Dangly = 0x00000121,
    Aabb = 0x00000221,
    Patch = 0x00000401  // Treated as dummy
}

/// <summary>
/// Classification flags for models.
/// </summary>
[Flags]
public enum MdlClassification : byte
{
    None = 0x00,
    Effect = 0x01,
    Tile = 0x02,
    Character = 0x04,
    Door = 0x08
}

/// <summary>
/// A single face (triangle) in a mesh.
/// </summary>
public struct MdlFace
{
    /// <summary>Normal vector for the face plane.</summary>
    public Vector3 PlaneNormal;

    /// <summary>Distance from origin for the face plane.</summary>
    public float PlaneDistance;

    /// <summary>Surface material ID.</summary>
    public int SurfaceId;

    /// <summary>Adjacent face indices (for mesh connectivity). -1 if no adjacent face.</summary>
    public int AdjacentFace0;
    public int AdjacentFace1;
    public int AdjacentFace2;

    /// <summary>Vertex indices for the three corners of the triangle.</summary>
    public int VertexIndex0;
    public int VertexIndex1;
    public int VertexIndex2;
}

/// <summary>
/// Base class for all MDL nodes.
/// </summary>
public class MdlNode
{
    /// <summary>Node type flags.</summary>
    public MdlNodeType NodeType { get; set; }

    /// <summary>Node name (max 32 chars in NWN).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Parent node reference (null for root).</summary>
    public MdlNode? Parent { get; set; }

    /// <summary>Child nodes.</summary>
    public List<MdlNode> Children { get; } = new();

    /// <summary>Local position relative to parent.</summary>
    public Vector3 Position { get; set; }

    /// <summary>Local rotation as quaternion.</summary>
    public Quaternion Orientation { get; set; } = Quaternion.Identity;

    /// <summary>Uniform scale factor.</summary>
    public float Scale { get; set; } = 1.0f;

    /// <summary>Wireframe display color (RGB, 0-1 range).</summary>
    public Vector3 Wirecolor { get; set; } = Vector3.One;

    /// <summary>Inherit color from parent.</summary>
    public bool InheritColor { get; set; }
}

/// <summary>
/// Trimesh node - basic triangle mesh with textures.
/// </summary>
public class MdlTrimeshNode : MdlNode
{
    /// <summary>Vertex positions.</summary>
    public Vector3[] Vertices { get; set; } = Array.Empty<Vector3>();

    /// <summary>Vertex normals.</summary>
    public Vector3[] Normals { get; set; } = Array.Empty<Vector3>();

    /// <summary>Texture coordinates (UV). Up to 4 sets supported.</summary>
    public Vector2[][] TextureCoords { get; set; } = Array.Empty<Vector2[]>();

    /// <summary>Vertex colors (RGBA as uint32).</summary>
    public uint[] VertexColors { get; set; } = Array.Empty<uint>();

    /// <summary>Triangle faces.</summary>
    public MdlFace[] Faces { get; set; } = Array.Empty<MdlFace>();

    /// <summary>Primary texture/bitmap name (16 char max).</summary>
    public string Bitmap { get; set; } = string.Empty;

    /// <summary>Secondary texture name.</summary>
    public string Bitmap2 { get; set; } = string.Empty;

    /// <summary>Material name.</summary>
    public string MaterialName { get; set; } = string.Empty;

    /// <summary>Ambient color.</summary>
    public Vector3 Ambient { get; set; }

    /// <summary>Diffuse color.</summary>
    public Vector3 Diffuse { get; set; } = Vector3.One;

    /// <summary>Specular color.</summary>
    public Vector3 Specular { get; set; }

    /// <summary>Shininess (specular power).</summary>
    public float Shininess { get; set; }

    /// <summary>Alpha/transparency (0-1).</summary>
    public float Alpha { get; set; } = 1.0f;

    /// <summary>Self-illumination color.</summary>
    public Vector3 SelfIllumColor { get; set; }

    /// <summary>Whether this mesh renders.</summary>
    public bool Render { get; set; } = true;

    /// <summary>Whether this mesh casts shadows.</summary>
    public bool Shadow { get; set; } = true;

    /// <summary>Whether lighting affects this mesh.</summary>
    public bool Beaming { get; set; }

    /// <summary>Rotate texture.</summary>
    public bool RotateTexture { get; set; }

    /// <summary>Transparency hint for rendering order.</summary>
    public int TransparencyHint { get; set; }

    /// <summary>Tilefade setting (for tiles).</summary>
    public int Tilefade { get; set; }

    /// <summary>Render order.</summary>
    public int RenderOrder { get; set; }
}

/// <summary>
/// Skin mesh node - trimesh with bone weights for skeletal animation.
/// </summary>
public class MdlSkinNode : MdlTrimeshNode
{
    /// <summary>Bone weight data per vertex (up to 4 bones per vertex).</summary>
    public MdlBoneWeight[] BoneWeights { get; set; } = Array.Empty<MdlBoneWeight>();

    /// <summary>Bone node name references.</summary>
    public string[] BoneNodeNames { get; set; } = Array.Empty<string>();

    /// <summary>Quaternion rotations for bones.</summary>
    public Quaternion[] BoneQuaternions { get; set; } = Array.Empty<Quaternion>();

    /// <summary>Translation offsets for bones.</summary>
    public Vector3[] BoneTranslations { get; set; } = Array.Empty<Vector3>();
}

/// <summary>
/// Bone weight for a single vertex.
/// </summary>
public struct MdlBoneWeight
{
    /// <summary>Bone indices (up to 4).</summary>
    public int Bone0;
    public int Bone1;
    public int Bone2;
    public int Bone3;

    /// <summary>Bone weights (should sum to 1.0).</summary>
    public float Weight0;
    public float Weight1;
    public float Weight2;
    public float Weight3;
}

/// <summary>
/// Dangly mesh node - physics-affected mesh (hair, cloth, etc.).
/// </summary>
public class MdlDanglyNode : MdlTrimeshNode
{
    /// <summary>Per-vertex constraint values (0 = free, 255 = fixed).</summary>
    public float[] Constraints { get; set; } = Array.Empty<float>();

    /// <summary>Displacement amount.</summary>
    public float Displacement { get; set; }

    /// <summary>Tightness (spring constant).</summary>
    public float Tightness { get; set; } = 1.0f;

    /// <summary>Period of oscillation.</summary>
    public float Period { get; set; } = 1.0f;
}

/// <summary>
/// Animated mesh node - vertex animation.
/// </summary>
public class MdlAnimNode : MdlTrimeshNode
{
    /// <summary>Sample period for animation frames.</summary>
    public float SamplePeriod { get; set; }

    /// <summary>Animated vertex positions per frame.</summary>
    public Vector3[][] AnimatedVertices { get; set; } = Array.Empty<Vector3[]>();

    /// <summary>Animated texture coordinates per frame.</summary>
    public Vector2[][] AnimatedTextureCoords { get; set; } = Array.Empty<Vector2[]>();
}

/// <summary>
/// AABB mesh node - axis-aligned bounding box for collision detection.
/// </summary>
public class MdlAabbNode : MdlTrimeshNode
{
    /// <summary>AABB tree for spatial partitioning.</summary>
    public MdlAabbEntry? RootAabb { get; set; }
}

/// <summary>
/// AABB tree entry for collision detection.
/// </summary>
public class MdlAabbEntry
{
    /// <summary>Bounding box minimum.</summary>
    public Vector3 BoundingMin { get; set; }

    /// <summary>Bounding box maximum.</summary>
    public Vector3 BoundingMax { get; set; }

    /// <summary>Face index if this is a leaf node, -1 otherwise.</summary>
    public int LeafFaceIndex { get; set; } = -1;

    /// <summary>Left child in AABB tree.</summary>
    public MdlAabbEntry? Left { get; set; }

    /// <summary>Right child in AABB tree.</summary>
    public MdlAabbEntry? Right { get; set; }
}

/// <summary>
/// Light node.
/// </summary>
public class MdlLightNode : MdlNode
{
    /// <summary>Light color.</summary>
    public Vector3 Color { get; set; } = Vector3.One;

    /// <summary>Light radius/range.</summary>
    public float Radius { get; set; } = 5.0f;

    /// <summary>Light multiplier.</summary>
    public float Multiplier { get; set; } = 1.0f;

    /// <summary>Whether this is a dynamic light.</summary>
    public bool IsDynamic { get; set; }

    /// <summary>Affects dynamic objects only.</summary>
    public bool AffectDynamic { get; set; } = true;

    /// <summary>Whether this light casts shadows.</summary>
    public bool Shadow { get; set; }

    /// <summary>Flare radius (for lens flare).</summary>
    public float FlareRadius { get; set; }

    /// <summary>Light priority (for performance culling).</summary>
    public int Priority { get; set; } = 5;

    /// <summary>Ambient-only lighting.</summary>
    public bool AmbientOnly { get; set; }

    /// <summary>Fading light.</summary>
    public bool Fading { get; set; }
}

/// <summary>
/// Emitter node - particle effects.
/// </summary>
public class MdlEmitterNode : MdlNode
{
    /// <summary>Emitter update method.</summary>
    public string Update { get; set; } = "Fountain";

    /// <summary>Emitter render method.</summary>
    public string RenderMethod { get; set; } = "Normal";

    /// <summary>Blend mode.</summary>
    public string Blend { get; set; } = "Normal";

    /// <summary>Texture name.</summary>
    public string Texture { get; set; } = string.Empty;

    /// <summary>Particle spawn type.</summary>
    public string SpawnType { get; set; } = "Normal";

    /// <summary>Grid dimensions X.</summary>
    public int XGrid { get; set; } = 1;

    /// <summary>Grid dimensions Y.</summary>
    public int YGrid { get; set; } = 1;

    /// <summary>Render order.</summary>
    public int RenderOrder { get; set; }

    /// <summary>Particles inherit velocity from parent.</summary>
    public bool Inherit { get; set; }

    /// <summary>Particles inherit local coordinates.</summary>
    public bool InheritLocal { get; set; }

    /// <summary>Particles inherit part (for character-attached effects).</summary>
    public bool InheritPart { get; set; }

    /// <summary>Particles affected by wind.</summary>
    public bool AffectedByWind { get; set; }

    /// <summary>Particles are splats.</summary>
    public bool IsSplat { get; set; }

    /// <summary>Particles bounce.</summary>
    public bool Bounce { get; set; }

    /// <summary>Particles generated randomly.</summary>
    public bool Random { get; set; }

    /// <summary>Particle loop.</summary>
    public bool Loop { get; set; } = true;

    /// <summary>P2P (point to point) enabled.</summary>
    public bool P2P { get; set; }

    /// <summary>P2P uses Bezier curves.</summary>
    public bool P2PBezier { get; set; }
}

/// <summary>
/// Reference node - references another model.
/// </summary>
public class MdlReferenceNode : MdlNode
{
    /// <summary>Referenced model name.</summary>
    public string RefModel { get; set; } = string.Empty;

    /// <summary>Whether the reference can be reattached.</summary>
    public bool Reattachable { get; set; }
}

/// <summary>
/// Animation event that occurs at a specific time.
/// </summary>
public struct MdlAnimationEvent
{
    /// <summary>Time in seconds when event fires.</summary>
    public float Time { get; set; }

    /// <summary>Event name/type.</summary>
    public string EventName { get; set; }
}

/// <summary>
/// Animation sequence.
/// </summary>
public class MdlAnimation
{
    /// <summary>Animation name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Animation length in seconds.</summary>
    public float Length { get; set; }

    /// <summary>Transition time to this animation.</summary>
    public float TransitionTime { get; set; }

    /// <summary>Root node name for animation.</summary>
    public string AnimRoot { get; set; } = string.Empty;

    /// <summary>Animation events.</summary>
    public List<MdlAnimationEvent> Events { get; } = new();

    /// <summary>Animated node data (position, rotation keys).</summary>
    public MdlNode? GeometryRoot { get; set; }
}

/// <summary>
/// Complete MDL model.
/// </summary>
public class MdlModel
{
    /// <summary>Model name (max 64 chars).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Model classification.</summary>
    public MdlClassification Classification { get; set; }

    /// <summary>Root geometry node.</summary>
    public MdlNode? GeometryRoot { get; set; }

    /// <summary>Animations.</summary>
    public List<MdlAnimation> Animations { get; } = new();

    /// <summary>Super model reference (for animation inheritance).</summary>
    public string SuperModel { get; set; } = string.Empty;

    /// <summary>Bounding box minimum.</summary>
    public Vector3 BoundingMin { get; set; }

    /// <summary>Bounding box maximum.</summary>
    public Vector3 BoundingMax { get; set; }

    /// <summary>Bounding sphere radius.</summary>
    public float Radius { get; set; }

    /// <summary>Animation scale.</summary>
    public float AnimationScale { get; set; } = 1.0f;

    /// <summary>File version.</summary>
    public int FileVersion { get; set; }

    /// <summary>Whether this was loaded from binary format.</summary>
    public bool IsBinary { get; set; }

    /// <summary>
    /// Recursively enumerate all nodes in the model (breadth-first order).
    /// </summary>
    public IEnumerable<MdlNode> EnumerateAllNodes()
    {
        if (GeometryRoot == null) yield break;

        var queue = new Queue<MdlNode>();
        queue.Enqueue(GeometryRoot);

        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            yield return node;

            foreach (var child in node.Children)
            {
                queue.Enqueue(child);
            }
        }
    }

    /// <summary>
    /// Find a node by name.
    /// </summary>
    public MdlNode? FindNode(string name)
    {
        return EnumerateAllNodes().FirstOrDefault(n =>
            string.Equals(n.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Get all mesh nodes.
    /// </summary>
    public IEnumerable<MdlTrimeshNode> GetMeshNodes()
    {
        return EnumerateAllNodes().OfType<MdlTrimeshNode>();
    }
}
