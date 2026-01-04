// MDL Binary file format reader for Neverwinter Nights
// Based on format documentation from nwnexplorer (BSD 3-Clause) and xoreos (GPL)
// Reference implementations used for format understanding only

using System.Numerics;
using System.Text;

namespace Radoub.Formats.Mdl;

/// <summary>
/// Reads binary MDL model files.
/// Binary MDL is a compiled format used by the game engine for faster loading.
/// </summary>
public class MdlBinaryReader
{
    // Node type flags (from binary format)
    private const uint NodeFlagHasHeader = 0x00000001;
    private const uint NodeFlagHasLight = 0x00000002;
    private const uint NodeFlagHasEmitter = 0x00000004;
    private const uint NodeFlagHasReference = 0x00000010;
    private const uint NodeFlagHasMesh = 0x00000020;
    private const uint NodeFlagHasSkin = 0x00000040;
    private const uint NodeFlagHasAnim = 0x00000080;
    private const uint NodeFlagHasDangly = 0x00000100;
    private const uint NodeFlagHasAABB = 0x00000200;

    // Structure sizes
    private const int FileHeaderSize = 12;
    private const int GeometryHeaderSize = 0x70;
    private const int ModelHeaderSize = 0xE8;
    private const int NodeHeaderSize = 0x70;
    private const int MeshHeaderSize = 0x270;
    private const int FaceSize = 0x20;
    private const int ControllerKeySize = 0x0C;

    private BinaryReader _reader = null!;
    private byte[] _modelData = null!;
    private byte[] _rawData = null!;

    /// <summary>
    /// Parse a binary MDL file from a stream.
    /// </summary>
    public MdlModel Parse(Stream stream)
    {
        _reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);

        // Read file header (12 bytes)
        var zeroField = _reader.ReadUInt32();
        var rawDataOffsetInFile = _reader.ReadUInt32();
        var rawDataSize = _reader.ReadUInt32();

        // Check if this is actually binary (first field should be 0)
        if (zeroField != 0)
        {
            throw new InvalidDataException("Not a binary MDL file (first 4 bytes should be 0)");
        }

        // Read model data (everything from offset 12 to rawDataOffset)
        var modelDataSize = rawDataOffsetInFile - FileHeaderSize;
        _modelData = _reader.ReadBytes((int)modelDataSize);

        // Read raw data (vertex data, face data, etc.)
        _rawData = _reader.ReadBytes((int)rawDataSize);

        // Parse the model structure
        return ParseModel();
    }

    /// <summary>
    /// Parse a binary MDL file from bytes.
    /// </summary>
    public MdlModel Parse(byte[] data)
    {
        using var stream = new MemoryStream(data);
        return Parse(stream);
    }

    /// <summary>
    /// Check if data appears to be binary MDL format.
    /// </summary>
    public static bool IsBinaryMdl(Stream stream)
    {
        var position = stream.Position;
        try
        {
            if (stream.Length < 12) return false;
            using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);
            var firstField = reader.ReadUInt32();
            return firstField == 0;
        }
        finally
        {
            stream.Position = position;
        }
    }

    /// <summary>
    /// Check if data appears to be binary MDL format.
    /// </summary>
    public static bool IsBinaryMdl(byte[] data)
    {
        if (data.Length < 12) return false;
        return BitConverter.ToUInt32(data, 0) == 0;
    }

    private MdlModel ParseModel()
    {
        var model = new MdlModel { IsBinary = true };

        // Model starts with geometry header
        using var modelStream = new MemoryStream(_modelData);
        using var reader = new BinaryReader(modelStream, Encoding.ASCII);

        // Skip function pointers (2 uint32)
        reader.BaseStream.Position = 8;

        // Read model name (64 bytes)
        model.Name = ReadFixedString(reader, 64);

        // Skip to node pointer and count (offset 0x48 in geometry header)
        reader.BaseStream.Position = 0x48;
        var rootNodeOffset = reader.ReadUInt32();
        var nodeCount = reader.ReadUInt32();

        // Skip to model-specific fields (after geometry header at 0x70)
        reader.BaseStream.Position = 0x70;

        // Read model flags
        var flags = reader.ReadUInt16();
        model.Classification = (MdlClassification)reader.ReadByte();
        reader.ReadByte(); // fog

        // Skip refcount
        reader.ReadUInt32();

        // Animation array offset and count
        var animArrayOffset = reader.ReadUInt32();
        var animArrayCount = reader.ReadUInt32();
        reader.ReadUInt32(); // anim array allocated

        // Skip supermodel pointer
        reader.ReadUInt32();

        // Bounding box
        model.BoundingMin = ReadVector3(reader);
        model.BoundingMax = ReadVector3(reader);
        model.Radius = reader.ReadSingle();
        model.AnimationScale = reader.ReadSingle();

        // Supermodel name (64 bytes)
        model.SuperModel = ReadFixedString(reader, 64);

        // Parse the root geometry node
        if (rootNodeOffset != 0xFFFFFFFF && rootNodeOffset != 0)
        {
            model.GeometryRoot = ParseNode(rootNodeOffset);
        }

        // Parse animations
        if (animArrayCount > 0 && animArrayOffset != 0xFFFFFFFF)
        {
            ParseAnimations(model, animArrayOffset, (int)animArrayCount);
        }

        return model;
    }

    private MdlNode ParseNode(uint nodeOffset)
    {
        using var nodeStream = new MemoryStream(_modelData);
        using var reader = new BinaryReader(nodeStream, Encoding.ASCII);
        reader.BaseStream.Position = nodeOffset;

        // Read node header
        // Skip function pointers (6 uint32 = 24 bytes)
        reader.BaseStream.Position = nodeOffset + 24;

        var inheritColor = reader.ReadUInt32();
        var partNumber = reader.ReadInt32();

        // Node name (32 bytes)
        var nodeName = ReadFixedString(reader, 32);

        // Skip geometry header pointer and parent pointer
        reader.ReadUInt32(); // geometry header
        reader.ReadUInt32(); // parent node

        // Children array
        var childArrayOffset = reader.ReadUInt32();
        var childCount = reader.ReadUInt32();
        reader.ReadUInt32(); // allocated

        // Controller arrays
        var controllerKeyOffset = reader.ReadUInt32();
        var controllerKeyCount = reader.ReadUInt32();
        reader.ReadUInt32(); // allocated
        var controllerDataOffset = reader.ReadUInt32();
        var controllerDataCount = reader.ReadUInt32();
        reader.ReadUInt32(); // allocated

        // Node flags
        var nodeFlags = reader.ReadUInt32();

        // Determine node type and create appropriate object
        MdlNode node = CreateNodeFromFlags(nodeFlags, nodeName);

        // Set common properties
        node.Name = nodeName;
        node.InheritColor = inheritColor != 0;

        // Parse node-type-specific data
        ParseNodeTypeData(node, nodeOffset, nodeFlags, reader);

        // Parse controller data for position/rotation
        if (controllerKeyCount > 0 && controllerKeyOffset != 0xFFFFFFFF)
        {
            ParseControllers(node, controllerKeyOffset, (int)controllerKeyCount,
                controllerDataOffset);
        }

        // Parse children
        if (childCount > 0 && childArrayOffset != 0xFFFFFFFF)
        {
            ParseChildren(node, childArrayOffset, (int)childCount);
        }

        return node;
    }

    private MdlNode CreateNodeFromFlags(uint flags, string name)
    {
        // Determine most specific node type
        if ((flags & NodeFlagHasAABB) != 0)
            return new MdlAabbNode { NodeType = MdlNodeType.Aabb };
        if ((flags & NodeFlagHasDangly) != 0)
            return new MdlDanglyNode { NodeType = MdlNodeType.Dangly };
        if ((flags & NodeFlagHasAnim) != 0)
            return new MdlAnimNode { NodeType = MdlNodeType.Anim };
        if ((flags & NodeFlagHasSkin) != 0)
            return new MdlSkinNode { NodeType = MdlNodeType.Skin };
        if ((flags & NodeFlagHasMesh) != 0)
            return new MdlTrimeshNode { NodeType = MdlNodeType.Trimesh };
        if ((flags & NodeFlagHasLight) != 0)
            return new MdlLightNode { NodeType = MdlNodeType.Light };
        if ((flags & NodeFlagHasEmitter) != 0)
            return new MdlEmitterNode { NodeType = MdlNodeType.Emitter };
        if ((flags & NodeFlagHasReference) != 0)
            return new MdlReferenceNode { NodeType = MdlNodeType.Reference };

        return new MdlNode { NodeType = MdlNodeType.Dummy };
    }

    private void ParseNodeTypeData(MdlNode node, uint nodeOffset, uint flags, BinaryReader reader)
    {
        // Light node data starts at offset 0x70
        if ((flags & NodeFlagHasLight) != 0 && node is MdlLightNode light)
        {
            reader.BaseStream.Position = nodeOffset + NodeHeaderSize;
            ParseLightNode(light, reader);
        }

        // Emitter node data starts at offset 0x70
        if ((flags & NodeFlagHasEmitter) != 0 && node is MdlEmitterNode emitter)
        {
            reader.BaseStream.Position = nodeOffset + NodeHeaderSize;
            ParseEmitterNode(emitter, reader);
        }

        // Reference node data starts at offset 0x70
        if ((flags & NodeFlagHasReference) != 0 && node is MdlReferenceNode reference)
        {
            reader.BaseStream.Position = nodeOffset + NodeHeaderSize;
            ParseReferenceNode(reference, reader);
        }

        // Mesh data starts at offset 0x70
        if ((flags & NodeFlagHasMesh) != 0 && node is MdlTrimeshNode mesh)
        {
            reader.BaseStream.Position = nodeOffset + NodeHeaderSize;
            ParseMeshNode(mesh, reader, flags);
        }
    }

    private void ParseLightNode(MdlLightNode light, BinaryReader reader)
    {
        light.FlareRadius = reader.ReadSingle();
        // Skip flare arrays and textures (complex structure, not needed for preview)
        reader.BaseStream.Position += 0x40;

        light.Priority = reader.ReadInt32();
        light.AmbientOnly = reader.ReadUInt32() != 0;
        light.IsDynamic = reader.ReadUInt32() != 0;
        light.AffectDynamic = reader.ReadUInt32() != 0;
        light.Shadow = reader.ReadUInt32() != 0;
        // Skip flare flag
        reader.ReadUInt32();
        light.Fading = reader.ReadUInt32() != 0;
    }

    private void ParseEmitterNode(MdlEmitterNode emitter, BinaryReader reader)
    {
        // Skip dead space, blast radius/length
        reader.BaseStream.Position += 12;

        emitter.XGrid = reader.ReadInt32();
        emitter.YGrid = reader.ReadInt32();

        // Skip spawn type uint
        reader.ReadUInt32();

        emitter.Update = ReadFixedString(reader, 32);
        emitter.RenderMethod = ReadFixedString(reader, 32);
        emitter.Blend = ReadFixedString(reader, 32);
        emitter.Texture = ReadFixedString(reader, 32);
        // Skip chunk name
        ReadFixedString(reader, 16);

        // Skip to flags
        reader.BaseStream.Position += 8;
        // Read flags as needed
    }

    private void ParseReferenceNode(MdlReferenceNode reference, BinaryReader reader)
    {
        reference.RefModel = ReadFixedString(reader, 64);
        reference.Reattachable = reader.ReadUInt32() != 0;
    }

    private void ParseMeshNode(MdlTrimeshNode mesh, BinaryReader reader, uint flags)
    {
        // Skip mesh routines (2 uint32)
        reader.BaseStream.Position += 8;

        // Faces array
        var faceArrayOffset = reader.ReadUInt32();
        var faceCount = reader.ReadUInt32();
        reader.ReadUInt32(); // allocated

        // Bounding box
        var meshBMin = ReadVector3(reader);
        var meshBMax = ReadVector3(reader);
        var meshRadius = reader.ReadSingle();
        var meshBAverage = ReadVector3(reader);

        // Material properties
        mesh.Diffuse = ReadVector3(reader);
        mesh.Ambient = ReadVector3(reader);
        mesh.Specular = ReadVector3(reader);
        mesh.Shininess = reader.ReadSingle();

        mesh.Shadow = reader.ReadUInt32() != 0;
        mesh.Beaming = reader.ReadUInt32() != 0;
        mesh.Render = reader.ReadUInt32() != 0;
        mesh.TransparencyHint = reader.ReadInt32();
        // Skip renderHint
        reader.ReadUInt32();

        // Textures (3 x 64 bytes)
        mesh.Bitmap = ReadFixedString(reader, 64);
        mesh.Bitmap2 = ReadFixedString(reader, 64);
        ReadFixedString(reader, 64); // texture3

        // Material name
        mesh.MaterialName = ReadFixedString(reader, 64);

        // Tilefade
        mesh.Tilefade = reader.ReadInt32();

        // Skip vertex index arrays
        reader.BaseStream.Position += 12 * 4; // 4 arrays x 3 fields each

        // Skip something3 offset/count
        reader.ReadUInt32();
        reader.ReadUInt32();

        // Triangle mode and padding
        reader.ReadByte();
        reader.BaseStream.Position += 3;

        // Skip temp mesh data pointer
        reader.ReadUInt32();

        // Vertex data pointer and count
        var vertexDataOffset = reader.ReadUInt32();
        var vertexCount = reader.ReadUInt16();
        var textureCount = reader.ReadUInt16();

        // Texture coordinate pointers (4 sets)
        var tvertOffsets = new uint[4];
        for (int i = 0; i < 4; i++)
            tvertOffsets[i] = reader.ReadUInt32();

        // Normals pointer
        var normalsOffset = reader.ReadUInt32();

        // Colors pointer
        var colorsOffset = reader.ReadUInt32();

        // Skip bumpmap animation pointers
        reader.BaseStream.Position += 24;

        // Light mapped and rotate texture
        mesh.RotateTexture = reader.ReadByte() != 0;
        reader.BaseStream.Position += 3;

        // Read vertex data from raw data section
        if (vertexCount > 0 && vertexDataOffset != 0xFFFFFFFF)
        {
            mesh.Vertices = ReadVertices(vertexDataOffset, vertexCount);
        }

        // Read normals from raw data section
        if (vertexCount > 0 && normalsOffset != 0xFFFFFFFF)
        {
            mesh.Normals = ReadVertices(normalsOffset, vertexCount);
        }

        // Read texture coordinates
        if (textureCount > 0 && vertexCount > 0)
        {
            var texCoordsList = new List<Vector2[]>();
            for (int i = 0; i < Math.Min((int)textureCount, 4); i++)
            {
                if (tvertOffsets[i] != 0xFFFFFFFF)
                {
                    texCoordsList.Add(ReadTexCoords(tvertOffsets[i], vertexCount));
                }
            }
            mesh.TextureCoords = texCoordsList.ToArray();
        }

        // Read vertex colors
        if (vertexCount > 0 && colorsOffset != 0xFFFFFFFF)
        {
            mesh.VertexColors = ReadColors(colorsOffset, vertexCount);
        }

        // Read faces from model data section
        if (faceCount > 0 && faceArrayOffset != 0xFFFFFFFF)
        {
            mesh.Faces = ReadFaces(faceArrayOffset, (int)faceCount);
        }

        // Parse type-specific data
        if ((flags & NodeFlagHasDangly) != 0 && mesh is MdlDanglyNode dangly)
        {
            // Dangly data follows mesh header
            reader.BaseStream.Position += 12; // skip constraints array header
            dangly.Displacement = reader.ReadSingle();
            dangly.Tightness = reader.ReadSingle();
            dangly.Period = reader.ReadSingle();
        }

        if ((flags & NodeFlagHasSkin) != 0 && mesh is MdlSkinNode skin)
        {
            // Skin data follows mesh header - complex bone weight data
            // Skip for now, not needed for basic preview
        }

        if ((flags & NodeFlagHasAABB) != 0 && mesh is MdlAabbNode aabb)
        {
            // AABB tree pointer
            var aabbTreeOffset = reader.ReadUInt32();
            if (aabbTreeOffset != 0xFFFFFFFF)
            {
                aabb.RootAabb = ParseAabbTree(aabbTreeOffset);
            }
        }
    }

    private Vector3[] ReadVertices(uint offset, int count)
    {
        var vertices = new Vector3[count];
        using var stream = new MemoryStream(_rawData);
        using var reader = new BinaryReader(stream);

        stream.Position = offset;
        for (int i = 0; i < count; i++)
        {
            vertices[i] = ReadVector3(reader);
        }
        return vertices;
    }

    private Vector2[] ReadTexCoords(uint offset, int count)
    {
        var coords = new Vector2[count];
        using var stream = new MemoryStream(_rawData);
        using var reader = new BinaryReader(stream);

        stream.Position = offset;
        for (int i = 0; i < count; i++)
        {
            coords[i] = new Vector2(reader.ReadSingle(), reader.ReadSingle());
        }
        return coords;
    }

    private uint[] ReadColors(uint offset, int count)
    {
        var colors = new uint[count];
        using var stream = new MemoryStream(_rawData);
        using var reader = new BinaryReader(stream);

        stream.Position = offset;
        for (int i = 0; i < count; i++)
        {
            colors[i] = reader.ReadUInt32();
        }
        return colors;
    }

    private MdlFace[] ReadFaces(uint offset, int count)
    {
        var faces = new MdlFace[count];
        using var stream = new MemoryStream(_modelData);
        using var reader = new BinaryReader(stream);

        stream.Position = offset;
        for (int i = 0; i < count; i++)
        {
            var face = new MdlFace();
            face.PlaneNormal = ReadVector3(reader);
            face.PlaneDistance = reader.ReadSingle();
            face.SurfaceId = reader.ReadInt32();

            // Adjacent faces (3 x int16)
            face.AdjacentFace0 = reader.ReadInt16();
            face.AdjacentFace1 = reader.ReadInt16();
            face.AdjacentFace2 = reader.ReadInt16();

            // Vertex indices (3 x uint16)
            face.VertexIndex0 = reader.ReadUInt16();
            face.VertexIndex1 = reader.ReadUInt16();
            face.VertexIndex2 = reader.ReadUInt16();

            faces[i] = face;
        }
        return faces;
    }

    private MdlAabbEntry? ParseAabbTree(uint offset)
    {
        if (offset == 0xFFFFFFFF) return null;

        using var stream = new MemoryStream(_modelData);
        using var reader = new BinaryReader(stream);
        stream.Position = offset;

        var entry = new MdlAabbEntry();
        entry.BoundingMin = ReadVector3(reader);
        entry.BoundingMax = ReadVector3(reader);

        var leftOffset = reader.ReadUInt32();
        var rightOffset = reader.ReadUInt32();
        entry.LeafFaceIndex = reader.ReadInt32();
        reader.ReadUInt32(); // plane flags

        if (leftOffset != 0xFFFFFFFF)
            entry.Left = ParseAabbTree(leftOffset);
        if (rightOffset != 0xFFFFFFFF)
            entry.Right = ParseAabbTree(rightOffset);

        return entry;
    }

    private void ParseControllers(MdlNode node, uint keyOffset, int keyCount, uint dataOffset)
    {
        using var stream = new MemoryStream(_modelData);
        using var reader = new BinaryReader(stream);

        for (int i = 0; i < keyCount; i++)
        {
            stream.Position = keyOffset + i * ControllerKeySize;

            var type = reader.ReadInt32();
            var rows = reader.ReadInt16();
            var keyDataOffset = reader.ReadInt16();
            var valueDataOffset = reader.ReadInt16();
            var columns = reader.ReadByte();
            reader.ReadByte(); // pad

            // Read controller data based on type
            // Type 8 = Position, Type 20 = Orientation
            if (type == 8 && rows > 0) // Position
            {
                stream.Position = dataOffset + valueDataOffset * 4;
                node.Position = ReadVector3(reader);
            }
            else if (type == 20 && rows > 0) // Orientation
            {
                stream.Position = dataOffset + valueDataOffset * 4;
                var x = reader.ReadSingle();
                var y = reader.ReadSingle();
                var z = reader.ReadSingle();
                var w = reader.ReadSingle();
                node.Orientation = new Quaternion(x, y, z, w);
            }
            else if (type == 36 && rows > 0) // Scale
            {
                stream.Position = dataOffset + valueDataOffset * 4;
                node.Scale = reader.ReadSingle();
            }
            else if (type == 76 && rows > 0 && node is MdlLightNode light) // Light Color
            {
                stream.Position = dataOffset + valueDataOffset * 4;
                light.Color = ReadVector3(reader);
            }
            else if (type == 88 && rows > 0 && node is MdlLightNode light2) // Light Radius
            {
                stream.Position = dataOffset + valueDataOffset * 4;
                light2.Radius = reader.ReadSingle();
            }
        }
    }

    private void ParseChildren(MdlNode parent, uint arrayOffset, int count)
    {
        using var stream = new MemoryStream(_modelData);
        using var reader = new BinaryReader(stream);

        for (int i = 0; i < count; i++)
        {
            stream.Position = arrayOffset + i * 4;
            var childOffset = reader.ReadUInt32();

            if (childOffset != 0xFFFFFFFF)
            {
                var child = ParseNode(childOffset);
                child.Parent = parent;
                parent.Children.Add(child);
            }
        }
    }

    private void ParseAnimations(MdlModel model, uint arrayOffset, int count)
    {
        using var stream = new MemoryStream(_modelData);
        using var reader = new BinaryReader(stream);

        for (int i = 0; i < count; i++)
        {
            stream.Position = arrayOffset + i * 4;
            var animOffset = reader.ReadUInt32();

            if (animOffset != 0xFFFFFFFF)
            {
                var anim = ParseAnimation(animOffset);
                model.Animations.Add(anim);
            }
        }
    }

    private MdlAnimation ParseAnimation(uint offset)
    {
        using var stream = new MemoryStream(_modelData);
        using var reader = new BinaryReader(stream);
        stream.Position = offset;

        var anim = new MdlAnimation();

        // Skip function pointers (2 uint32)
        stream.Position += 8;

        // Animation name (64 bytes) - from geometry header portion
        anim.Name = ReadFixedString(reader, 64);

        // Skip to animation-specific fields at offset 0x70
        stream.Position = offset + GeometryHeaderSize;

        anim.Length = reader.ReadSingle();
        anim.TransitionTime = reader.ReadSingle();
        anim.AnimRoot = ReadFixedString(reader, 64);

        // Events array
        var eventsOffset = reader.ReadUInt32();
        var eventsCount = reader.ReadUInt32();
        reader.ReadUInt32(); // allocated

        if (eventsCount > 0 && eventsOffset != 0xFFFFFFFF)
        {
            for (int i = 0; i < eventsCount; i++)
            {
                stream.Position = eventsOffset + i * 36; // 4 + 32 bytes per event
                var time = reader.ReadSingle();
                var eventName = ReadFixedString(reader, 32);
                anim.Events.Add(new MdlAnimationEvent { Time = time, EventName = eventName });
            }
        }

        return anim;
    }

    private static Vector3 ReadVector3(BinaryReader reader)
    {
        return new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
    }

    private static string ReadFixedString(BinaryReader reader, int length)
    {
        var bytes = reader.ReadBytes(length);
        var nullIndex = Array.IndexOf(bytes, (byte)0);
        if (nullIndex >= 0)
            return Encoding.ASCII.GetString(bytes, 0, nullIndex);
        return Encoding.ASCII.GetString(bytes).TrimEnd('\0');
    }
}
