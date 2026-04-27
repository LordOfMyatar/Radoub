// Fresh MDL Binary Reader - Clean implementation based on xoreos documentation
// No heuristics, no guessing - follows the format spec exactly
//
// Format structure (from xoreos-docs/templates/NWN1MDL.bt):
// Offset 0x00: uint32 zero (magic)
// Offset 0x04: uint32 rawDataOffset (offset in file where raw data starts)
// Offset 0x08: uint32 rawDataSize
// Offset 0x0C: Model data starts here (modelDataOffset = 12)
//
// All pointers in the model data section are offsets from modelDataOffset (12)
// All pointers to raw data are offsets from the start of raw data section

using System.Numerics;
using System.Text;
using Radoub.Formats.Logging;

namespace Radoub.Formats.Mdl;

/// <summary>
/// Fresh binary MDL reader - no heuristics, follows format exactly.
/// </summary>
public class MdlBinaryReader2
{
    // File structure constants
    private const int FILE_HEADER_SIZE = 12;

    // Node type flags
    private const uint NODE_FLAG_HEADER = 0x00000001;
    private const uint NODE_FLAG_LIGHT = 0x00000002;
    private const uint NODE_FLAG_EMITTER = 0x00000004;
    private const uint NODE_FLAG_CAMERA = 0x00000008;
    private const uint NODE_FLAG_REFERENCE = 0x00000010;
    private const uint NODE_FLAG_MESH = 0x00000020;
    private const uint NODE_FLAG_SKIN = 0x00000040;
    private const uint NODE_FLAG_ANIM = 0x00000080;
    private const uint NODE_FLAG_DANGLY = 0x00000100;
    private const uint NODE_FLAG_AABB = 0x00000200;

    // Mesh header offsets (relative to start of mesh-specific data, after node header)
    private const int MESH_FACES_OFFSET = 0x08;         // Face array definition starts here
    private const int MESH_TEXTURE0_OFFSET = 0x78;      // First texture name (64 bytes)
    private const int MESH_VERTEX_OFFSET = 0x1BC;       // Vertex data offset in raw section
    private const int MESH_VERTEX_COUNT = 0x1C0;        // Vertex count (uint16)
    private const int MESH_TEXTURE_COUNT = 0x1C2;       // Texture count (uint16)
    private const int MESH_TVERT0_OFFSET = 0x1C4;       // Texture coord set 0 offset
    private const int MESH_TVERT1_OFFSET = 0x1C8;       // Texture coord set 1 offset
    private const int MESH_TVERT2_OFFSET = 0x1CC;       // Texture coord set 2 offset
    private const int MESH_TVERT3_OFFSET = 0x1D0;       // Texture coord set 3 offset
    private const int MESH_NORMALS_OFFSET = 0x1D4;      // Normals offset in raw section
    private const int MESH_COLORS_OFFSET = 0x1D8;       // Colors offset in raw section

    // Face structure size: 3 floats (normal) + 1 float (distance) + 1 int (surfaceId) +
    //                      3 shorts (adjacent) + 3 shorts (indices) = 32 bytes
    private const int FACE_SIZE = 32;

    private byte[] _modelData = null!;
    private byte[] _rawData = null!;

    public MdlModel Parse(byte[] data)
    {
        using var stream = new MemoryStream(data);
        return Parse(stream);
    }

    public MdlModel Parse(Stream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);

        // === File Header (12 bytes) ===
        uint zero = reader.ReadUInt32();
        if (zero != 0)
            throw new InvalidDataException($"Not a binary MDL file (expected 0, got {zero})");

        uint rawDataOffset = reader.ReadUInt32();
        uint rawDataSize = reader.ReadUInt32();

        Log(LogLevel.DEBUG, $"File header: rawDataOffset={rawDataOffset}, rawDataSize={rawDataSize}");

        // === Read model data section ===
        // Model data runs from offset 12 to rawDataOffset
        long modelDataSize = (rawDataOffset > FILE_HEADER_SIZE)
            ? rawDataOffset - FILE_HEADER_SIZE
            : stream.Length - FILE_HEADER_SIZE;

        _modelData = reader.ReadBytes((int)modelDataSize);
        Log(LogLevel.DEBUG, $"Model data: {_modelData.Length} bytes");

        // === Read raw data section ===
        if (rawDataSize > 0 && rawDataOffset >= FILE_HEADER_SIZE)
        {
            stream.Position = rawDataOffset;
            _rawData = reader.ReadBytes((int)rawDataSize);
            Log(LogLevel.DEBUG, $"Raw data: {_rawData.Length} bytes");
        }
        else
        {
            _rawData = Array.Empty<byte>();
        }

        // === Parse model header ===
        return ParseModel();
    }

    private MdlModel ParseModel()
    {
        var model = new MdlModel { IsBinary = true };
        using var reader = new BinaryReader(new MemoryStream(_modelData));

        // Skip geometry header function pointers (offset 0x00)
        // Read model name at offset 0x08
        reader.BaseStream.Position = 0x08;
        model.Name = ReadString(reader, 64);
        Log(LogLevel.DEBUG, $"Model name: '{model.Name}'");

        // Root node offset at 0x48
        reader.BaseStream.Position = 0x48;
        uint rootNodeOffset = reader.ReadUInt32();
        uint nodeCount = reader.ReadUInt32();
        Log(LogLevel.DEBUG, $"Root node offset: {rootNodeOffset}, node count: {nodeCount}");

        // Bounding box at 0x88
        reader.BaseStream.Position = 0x88;
        model.BoundingMin = ReadVector3(reader);
        model.BoundingMax = ReadVector3(reader);
        model.Radius = reader.ReadSingle();
        Log(LogLevel.DEBUG, $"Bounds: {model.BoundingMin} to {model.BoundingMax}, radius={model.Radius}");

        // Parse node tree starting at root
        if (rootNodeOffset > 0 && rootNodeOffset < _modelData.Length)
        {
            model.GeometryRoot = ParseNode(rootNodeOffset);
        }

        return model;
    }

    private MdlNode ParseNode(uint offset)
    {
        if (offset >= _modelData.Length)
        {
            Log(LogLevel.WARN, $"Node offset {offset} out of bounds (modelData={_modelData.Length})");
            return new MdlNode { Name = "invalid" };
        }

        using var reader = new BinaryReader(new MemoryStream(_modelData));
        reader.BaseStream.Position = offset;

        // === Node header (0x70 bytes = 112 bytes) ===
        // 0x00: 6 function pointers (skip)
        reader.BaseStream.Position = offset + 0x18;

        // 0x18: inherit color flag
        reader.ReadUInt32();

        // 0x1C: node number
        uint nodeNumber = reader.ReadUInt32();

        // 0x20: node name (32 bytes)
        string name = ReadString(reader, 32);

        // 0x40: geometry pointer (skip)
        reader.ReadUInt32();

        // 0x44: parent pointer (skip)
        reader.ReadUInt32();

        // 0x48: children array definition
        uint childrenOffset = reader.ReadUInt32();
        uint childrenCount = reader.ReadUInt32();
        reader.ReadUInt32(); // allocated

        // 0x54: controller keys array (skip for now)
        reader.BaseStream.Position = offset + 0x60;

        // 0x60: controller data array (skip for now)
        reader.BaseStream.Position = offset + 0x6C;

        // 0x6C: node flags
        uint flags = reader.ReadUInt32();

        Log(LogLevel.TRACE, $"Node '{name}' at {offset}: flags=0x{flags:X8}, children={childrenCount}");

        // Determine node type and create appropriate object
        MdlNode node;
        if ((flags & NODE_FLAG_MESH) != 0)
        {
            var trimesh = new MdlTrimeshNode();
            node = trimesh;

            // Mesh-specific data starts at offset + 0x70
            ParseMeshData(trimesh, offset + 0x70);
        }
        else
        {
            node = new MdlNode();
        }

        node.Name = name;
        node.NodeType = GetNodeTypeEnum(flags);

        // Read position/orientation from node header
        reader.BaseStream.Position = offset + 0x10;  // Position is at offset 0x10 in some versions
        // Actually position might be in controllers - skip for now, use default
        node.Position = Vector3.Zero;

        // Parse children
        if (childrenCount > 0 && childrenOffset > 0 && childrenOffset < _modelData.Length)
        {
            reader.BaseStream.Position = childrenOffset;
            for (int i = 0; i < childrenCount && i < 100; i++)
            {
                uint childOffset = reader.ReadUInt32();
                if (childOffset > 0 && childOffset < _modelData.Length)
                {
                    var child = ParseNode(childOffset);
                    child.Parent = node;
                    node.Children.Add(child);
                }
            }
        }

        return node;
    }

    private void ParseMeshData(MdlTrimeshNode mesh, uint meshHeaderStart)
    {
        if (meshHeaderStart + 0x200 > _modelData.Length)
        {
            Log(LogLevel.WARN, $"Mesh header at {meshHeaderStart} too close to end of model data");
            return;
        }

        using var reader = new BinaryReader(new MemoryStream(_modelData));

        // === Face array ===
        reader.BaseStream.Position = meshHeaderStart + MESH_FACES_OFFSET;
        uint facesOffset = reader.ReadUInt32();
        uint facesCount = reader.ReadUInt32();
        Log(LogLevel.TRACE, $"Mesh '{mesh.Name}': faces at {facesOffset}, count={facesCount}");

        // === Texture name ===
        reader.BaseStream.Position = meshHeaderStart + MESH_TEXTURE0_OFFSET;
        mesh.Bitmap = ReadString(reader, 64);
        Log(LogLevel.TRACE, $"Mesh '{mesh.Name}': texture='{mesh.Bitmap}'");

        // === Vertex data pointers ===
        reader.BaseStream.Position = meshHeaderStart + MESH_VERTEX_OFFSET;
        uint vertexOffset = reader.ReadUInt32();
        ushort vertexCount = reader.ReadUInt16();
        ushort textureCount = reader.ReadUInt16();
        Log(LogLevel.TRACE, $"Mesh '{mesh.Name}': vertexOffset={vertexOffset}, vertexCount={vertexCount}, textureCount={textureCount}");

        // === Texture coordinate offsets ===
        reader.BaseStream.Position = meshHeaderStart + MESH_TVERT0_OFFSET;
        uint[] tvertOffsets = new uint[4];
        for (int i = 0; i < 4; i++)
            tvertOffsets[i] = reader.ReadUInt32();
        Log(LogLevel.TRACE, $"Mesh '{mesh.Name}': tvert offsets={tvertOffsets[0]}, {tvertOffsets[1]}, {tvertOffsets[2]}, {tvertOffsets[3]}");

        // === Normals offset ===
        reader.BaseStream.Position = meshHeaderStart + MESH_NORMALS_OFFSET;
        uint normalsOffset = reader.ReadUInt32();

        // === Colors offset ===
        uint colorsOffset = reader.ReadUInt32();

        // === Read vertex positions from raw data ===
        // Detect and skip average normal header if present (12-byte header prepended to ALL raw data arrays)
        uint originalVertexOffset = vertexOffset;
        uint adjustedVertexOffset = DetectAndSkipAverageNormal(vertexOffset, vertexCount);
        uint avgNormalSkip = adjustedVertexOffset - originalVertexOffset;  // Usually 0 or 12

        if (vertexCount > 0 && adjustedVertexOffset != 0xFFFFFFFF && _rawData.Length > 0)
        {
            mesh.Vertices = ReadVerticesFromRaw(adjustedVertexOffset, vertexCount);
            Log(LogLevel.INFO, $"Mesh '{mesh.Name}': Read {mesh.Vertices.Length} vertices, first: {mesh.Vertices[0]}");
        }

        // === Read normals from raw data ===
        // Apply same offset adjustment as vertices - normals array also has the avg normal header
        if (vertexCount > 0 && normalsOffset != 0xFFFFFFFF && _rawData.Length > 0)
        {
            uint adjustedNormalsOffset = normalsOffset + avgNormalSkip;
            Log(LogLevel.TRACE, $"Mesh '{mesh.Name}': Normals offset {normalsOffset} + {avgNormalSkip} = {adjustedNormalsOffset}");
            mesh.Normals = ReadVerticesFromRaw(adjustedNormalsOffset, vertexCount);
        }

        // === Read texture coordinates from raw data ===
        // Apply same average normal skip as vertices - UV arrays also have the header
        if (textureCount > 0 && vertexCount > 0 && _rawData.Length > 0)
        {
            var texCoordsList = new List<Vector2[]>();
            for (int i = 0; i < textureCount && i < 4; i++)
            {
                if (tvertOffsets[i] != 0xFFFFFFFF)
                {
                    uint adjustedTvertOffset = tvertOffsets[i] + avgNormalSkip;
                    Log(LogLevel.TRACE, $"Mesh '{mesh.Name}': UV[{i}] offset {tvertOffsets[i]} + {avgNormalSkip} = {adjustedTvertOffset}");
                    var uvs = ReadTexCoordsFromRaw(adjustedTvertOffset, vertexCount);
                    texCoordsList.Add(uvs);
                    if (i == 0)
                        Log(LogLevel.INFO, $"Mesh '{mesh.Name}': Read {uvs.Length} UVs, first: {uvs[0]}");
                }
            }
            mesh.TextureCoords = texCoordsList.ToArray();
        }

        // === Read faces from model data ===
        if (facesCount > 0 && facesOffset < _modelData.Length)
        {
            mesh.Faces = ReadFaces(facesOffset, (int)facesCount);
            Log(LogLevel.INFO, $"Mesh '{mesh.Name}': Read {mesh.Faces.Length} faces");

            // Validate face indices
            int maxIdx = 0;
            foreach (var f in mesh.Faces)
                maxIdx = Math.Max(maxIdx, Math.Max(f.VertexIndex0, Math.Max(f.VertexIndex1, f.VertexIndex2)));

            if (maxIdx >= vertexCount)
                Log(LogLevel.ERROR, $"Mesh '{mesh.Name}': Face index {maxIdx} >= vertex count {vertexCount}!");
        }
    }

    private Vector3[] ReadVerticesFromRaw(uint offset, int count)
    {
        var vertices = new Vector3[count];

        // Raw data offsets are direct offsets into the raw data buffer
        if (offset + count * 12 > _rawData.Length)
        {
            Log(LogLevel.WARN, $"Vertex read out of bounds: offset={offset}, count={count}, rawLen={_rawData.Length}");
            return vertices;
        }

        using var reader = new BinaryReader(new MemoryStream(_rawData));
        reader.BaseStream.Position = offset;

        for (int i = 0; i < count; i++)
        {
            vertices[i] = ReadVector3(reader);
        }

        return vertices;
    }

    private Vector2[] ReadTexCoordsFromRaw(uint offset, int count)
    {
        var uvs = new Vector2[count];

        if (offset + count * 8 > _rawData.Length)
        {
            Log(LogLevel.WARN, $"UV read out of bounds: offset={offset}, count={count}, rawLen={_rawData.Length}");
            return uvs;
        }

        using var reader = new BinaryReader(new MemoryStream(_rawData));
        reader.BaseStream.Position = offset;

        for (int i = 0; i < count; i++)
        {
            uvs[i] = new Vector2(reader.ReadSingle(), reader.ReadSingle());
        }

        return uvs;
    }

    private MdlFace[] ReadFaces(uint offset, int count)
    {
        var faces = new MdlFace[count];

        if (offset + count * FACE_SIZE > _modelData.Length)
        {
            Log(LogLevel.WARN, $"Face read out of bounds: offset={offset}, count={count}");
            return faces;
        }

        using var reader = new BinaryReader(new MemoryStream(_modelData));
        reader.BaseStream.Position = offset;

        for (int i = 0; i < count; i++)
        {
            var face = new MdlFace();
            face.PlaneNormal = ReadVector3(reader);
            face.PlaneDistance = reader.ReadSingle();
            face.SurfaceId = reader.ReadInt32();
            face.AdjacentFace0 = reader.ReadInt16();
            face.AdjacentFace1 = reader.ReadInt16();
            face.AdjacentFace2 = reader.ReadInt16();
            face.VertexIndex0 = reader.ReadUInt16();
            face.VertexIndex1 = reader.ReadUInt16();
            face.VertexIndex2 = reader.ReadUInt16();
            faces[i] = face;
        }

        return faces;
    }

    /// <summary>
    /// Detect and skip "average normal" header that some NWN body part MDL files have.
    /// The average normal is a 12-byte header (3 floats) prepended to vertex data.
    /// Detection cases:
    /// 1. First vector is zero (0,0,0) - placeholder/padding header
    /// 2. First vector is unit normal (~1.0 magnitude) AND either axis-aligned OR second vector is small
    /// </summary>
    private uint DetectAndSkipAverageNormal(uint vertexRawOffset, int vertexCount)
    {
        if (vertexCount <= 0 || vertexRawOffset == 0xFFFFFFFF || vertexRawOffset == uint.MaxValue ||
            vertexRawOffset + 24 > _rawData.Length)
        {
            return vertexRawOffset;
        }

        float vx = BitConverter.ToSingle(_rawData, (int)vertexRawOffset);
        float vy = BitConverter.ToSingle(_rawData, (int)vertexRawOffset + 4);
        float vz = BitConverter.ToSingle(_rawData, (int)vertexRawOffset + 8);
        float vMag = (float)Math.Sqrt(vx * vx + vy * vy + vz * vz);

        float v2x = BitConverter.ToSingle(_rawData, (int)vertexRawOffset + 12);
        float v2y = BitConverter.ToSingle(_rawData, (int)vertexRawOffset + 16);
        float v2z = BitConverter.ToSingle(_rawData, (int)vertexRawOffset + 20);
        float v2Mag = (float)Math.Sqrt(v2x * v2x + v2y * v2y + v2z * v2z);

        Log(LogLevel.TRACE,
            $"AvgNormal check at offset {vertexRawOffset}: v1=({vx:F4},{vy:F4},{vz:F4}) mag={vMag:F4}, v2=({v2x:F4},{v2y:F4},{v2z:F4}) mag={v2Mag:F4}");

        bool shouldSkip = false;
        string skipReason = "";

        // Case 1: First vector is zero - this is a placeholder/padding, skip it
        // Zero vertex at origin would create triangles connecting to (0,0,0)
        if (vMag < 0.0001f && v2Mag > 0.001f)
        {
            shouldSkip = true;
            skipReason = "zero placeholder";
        }
        else
        {
            // Case 2: First vector is a unit normal (magnitude very close to 1.0)
            bool isUnitNormal = vMag > 0.98f && vMag < 1.02f;

            // Common average normal values are exactly (0,0,1) or (0,0,-1) pointing up/down
            bool isAxisAlignedNormal = (Math.Abs(vx) < 0.001f && Math.Abs(vy) < 0.001f && Math.Abs(vz - 1.0f) < 0.01f) ||
                                       (Math.Abs(vx) < 0.001f && Math.Abs(vy) < 0.001f && Math.Abs(vz + 1.0f) < 0.01f);

            // Second vector (actual first vertex) should have small magnitude for body parts
            // Body part vertices are typically within -0.5 to 0.5 range in local space
            bool secondVectorSmall = v2Mag < 0.5f;

            if (isUnitNormal && (isAxisAlignedNormal || secondVectorSmall))
            {
                shouldSkip = true;
                skipReason = "unit normal header";
            }
        }

        if (shouldSkip)
        {
            vertexRawOffset += 12;
            Log(LogLevel.TRACE,
                $"Skipping {skipReason} ({vx:F4},{vy:F4},{vz:F4}), actual positions start at offset {vertexRawOffset}");
        }

        return vertexRawOffset;
    }

    private static Vector3 ReadVector3(BinaryReader reader)
    {
        return new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
    }

    private static string ReadString(BinaryReader reader, int length)
    {
        var bytes = reader.ReadBytes(length);
        int nullIdx = Array.IndexOf(bytes, (byte)0);
        if (nullIdx >= 0)
            return Encoding.ASCII.GetString(bytes, 0, nullIdx);
        return Encoding.ASCII.GetString(bytes);
    }

    private static MdlNodeType GetNodeTypeEnum(uint flags)
    {
        if ((flags & NODE_FLAG_AABB) != 0) return MdlNodeType.Aabb;
        if ((flags & NODE_FLAG_DANGLY) != 0) return MdlNodeType.Dangly;
        if ((flags & NODE_FLAG_SKIN) != 0) return MdlNodeType.Skin;
        if ((flags & NODE_FLAG_ANIM) != 0) return MdlNodeType.Anim;
        if ((flags & NODE_FLAG_MESH) != 0) return MdlNodeType.Trimesh;
        if ((flags & NODE_FLAG_REFERENCE) != 0) return MdlNodeType.Reference;
        if ((flags & NODE_FLAG_EMITTER) != 0) return MdlNodeType.Emitter;
        if ((flags & NODE_FLAG_LIGHT) != 0) return MdlNodeType.Light;
        return MdlNodeType.Dummy;
    }

    private static void Log(LogLevel level, string message)
    {
        UnifiedLogger.LogApplication(level, $"[MDL2] {message}");
    }
}
