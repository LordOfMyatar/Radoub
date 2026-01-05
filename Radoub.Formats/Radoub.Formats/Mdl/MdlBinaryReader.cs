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

    // Binary MDL files store memory pointers that need patching.
    // The base address is stored at offset 0 in the geometry header.
    private uint _pointerBase;
    private uint _rawDataFileOffset;   // Where raw data starts in file (needed to check if raw data exists)

    /// <summary>
    /// Convert a memory pointer or offset to a model data buffer offset.
    /// Binary MDL uses a mix: function pointers are memory addresses,
    /// but internal structure offsets are often buffer-relative.
    /// </summary>
    private uint PointerToModelOffset(uint pointer)
    {
        if (pointer == 0xFFFFFFFF || pointer == 0)
            return pointer; // Pass through sentinel values

        // If pointer is already a valid buffer offset (less than buffer size and less than base),
        // use it directly - these are buffer-relative offsets
        if (pointer < _pointerBase && pointer < _modelData.Length)
            return pointer;

        // Otherwise, treat as a memory pointer and subtract base address
        if (pointer >= _pointerBase)
        {
            var bufferOffset = pointer - _pointerBase;
            if (bufferOffset < _modelData.Length)
                return bufferOffset;
        }

        return uint.MaxValue; // Invalid
    }

    /// <summary>
    /// Convert a memory pointer or offset to a raw data buffer offset.
    /// </summary>
    private uint PointerToRawOffset(uint pointer)
    {
        if (pointer == 0xFFFFFFFF || pointer == 0)
            return pointer; // Pass through sentinel values

        if (_rawDataFileOffset == 0 || _rawData.Length == 0)
            return uint.MaxValue; // No raw data section

        // If pointer is already a valid raw buffer offset (small number),
        // use it directly
        if (pointer < (uint)_rawData.Length)
            return pointer;

        // Otherwise, treat as a memory pointer and subtract raw base address
        var rawBase = _pointerBase + (uint)_modelData.Length;
        if (pointer >= rawBase)
        {
            var bufferOffset = pointer - rawBase;
            if (bufferOffset < (uint)_rawData.Length)
                return bufferOffset;
        }

        return uint.MaxValue; // Invalid
    }

    /// <summary>
    /// Parse a binary MDL file from a stream.
    /// </summary>
    public MdlModel Parse(Stream stream)
    {
        Radoub.Formats.Logging.UnifiedLogger.LogApplication(
            Radoub.Formats.Logging.LogLevel.DEBUG,
            $"[MDL] Parse START: streamLen={stream.Length}");

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

        // Debug: log header values
        Radoub.Formats.Logging.UnifiedLogger.LogApplication(
            Radoub.Formats.Logging.LogLevel.DEBUG,
            $"[MDL] rawDataOffset={rawDataOffsetInFile}, rawDataSize={rawDataSize}, streamLen={stream.Length}");

        // Validate offsets - rawDataOffset may be 0 for models without raw data
        var streamLength = stream.Length;

        // rawDataOffset must be within file bounds or be 0 (no raw data)
        if (rawDataOffsetInFile > streamLength)
        {
            throw new InvalidDataException($"Raw data offset {rawDataOffsetInFile} exceeds file length {streamLength}");
        }

        // Determine model data size
        uint modelDataSize;
        if (rawDataOffsetInFile == 0 || rawDataOffsetInFile <= FileHeaderSize)
        {
            // No raw data section or invalid offset - model data extends to end of file
            modelDataSize = (uint)(streamLength - FileHeaderSize);
            rawDataOffsetInFile = 0; // Normalize to indicate no raw data
        }
        else
        {
            modelDataSize = rawDataOffsetInFile - FileHeaderSize;
        }

        if (modelDataSize < GeometryHeaderSize)
        {
            throw new InvalidDataException($"Model data too small: {modelDataSize} bytes (need at least {GeometryHeaderSize})");
        }

        // Store raw data offset to check if raw data section exists
        _rawDataFileOffset = rawDataOffsetInFile;

        Radoub.Formats.Logging.UnifiedLogger.LogApplication(
            Radoub.Formats.Logging.LogLevel.DEBUG,
            $"[MDL] modelDataSize={modelDataSize}, rawDataOffset={rawDataOffsetInFile}");

        _modelData = _reader.ReadBytes((int)modelDataSize);

        // Read raw data (vertex data, face data, etc.) - may be empty for simple models
        if (rawDataSize > 0 && rawDataOffsetInFile > 0 && stream.Position + rawDataSize <= streamLength)
        {
            _rawData = _reader.ReadBytes((int)rawDataSize);
            Radoub.Formats.Logging.UnifiedLogger.LogApplication(
                Radoub.Formats.Logging.LogLevel.DEBUG,
                $"[MDL] Read rawData: {_rawData.Length} bytes");

        }
        else
        {
            _rawData = Array.Empty<byte>();
            Radoub.Formats.Logging.UnifiedLogger.LogApplication(
                Radoub.Formats.Logging.LogLevel.DEBUG,
                $"[MDL] No raw data section");
        }

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

        // Verify we have enough model data for the headers
        if (_modelData.Length < ModelHeaderSize)
        {
            throw new InvalidDataException($"Model data too small for header: {_modelData.Length} bytes, need {ModelHeaderSize}");
        }

        // Read the first function pointer to establish the base address for pointer fixup
        // All pointers in the file are relative to this base
        _pointerBase = reader.ReadUInt32();

        Radoub.Formats.Logging.UnifiedLogger.LogApplication(
            Radoub.Formats.Logging.LogLevel.DEBUG,
            $"[MDL] Pointer base address: 0x{_pointerBase:X8}");

        // Skip second function pointer
        reader.BaseStream.Position = 8;

        // Read model name (64 bytes)
        model.Name = ReadFixedString(reader, 64);

        // Skip to node pointer and count (offset 0x48 in geometry header)
        reader.BaseStream.Position = 0x48;
        var rootNodeOffset = reader.ReadUInt32();
        var nodeCount = reader.ReadUInt32();

        // Note: After testing, it appears offsets in MDL files ARE relative to model data section,
        // not file-absolute. So rootNodeOffset can be used directly against _modelData buffer.
        // Keep this for diagnostic purposes.
        var rootNodeBufferOffset = rootNodeOffset; // Use directly, no conversion needed

        Radoub.Formats.Logging.UnifiedLogger.LogApplication(
            Radoub.Formats.Logging.LogLevel.DEBUG,
            $"[MDL] rootNodeOffset={rootNodeOffset}, nodeCount={nodeCount}, modelDataLen={_modelData.Length}");

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

        // Parse the root geometry node using buffer offset
        if (rootNodeBufferOffset != 0xFFFFFFFF && rootNodeBufferOffset != 0 && rootNodeBufferOffset != uint.MaxValue)
        {
            model.GeometryRoot = ParseNode(rootNodeBufferOffset);
        }

        // Parse animations (convert pointer to buffer offset)
        var animArrayBufferOffset = PointerToModelOffset(animArrayOffset);
        if (animArrayCount > 0 && animArrayBufferOffset != 0xFFFFFFFF && animArrayBufferOffset != uint.MaxValue)
        {
            ParseAnimations(model, animArrayBufferOffset, (int)animArrayCount);
        }

        return model;
    }

    private MdlNode ParseNode(uint nodeOffset)
    {
        // Bounds check
        if (nodeOffset + NodeHeaderSize > _modelData.Length)
        {
            Radoub.Formats.Logging.UnifiedLogger.LogApplication(
                Radoub.Formats.Logging.LogLevel.WARN,
                $"[MDL] ParseNode: offset {nodeOffset} + {NodeHeaderSize} > modelDataLen {_modelData.Length}");
            return new MdlNode { Name = "invalid", NodeType = MdlNodeType.Dummy };
        }

        using var nodeStream = new MemoryStream(_modelData);
        using var reader = new BinaryReader(nodeStream, Encoding.ASCII);
        reader.BaseStream.Position = nodeOffset;

        // Log first 16 bytes at this position for debugging
        var debugBytes = new byte[Math.Min(16, _modelData.Length - (int)nodeOffset)];
        Array.Copy(_modelData, nodeOffset, debugBytes, 0, debugBytes.Length);
        Radoub.Formats.Logging.UnifiedLogger.LogApplication(
            Radoub.Formats.Logging.LogLevel.DEBUG,
            $"[MDL] ParseNode at {nodeOffset}: first16bytes={BitConverter.ToString(debugBytes)}");

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

        // Convert pointer to buffer offset
        var childArrayBufferOffset = PointerToModelOffset(childArrayOffset);

        Radoub.Formats.Logging.UnifiedLogger.LogApplication(
            Radoub.Formats.Logging.LogLevel.DEBUG,
            $"[MDL] Node '{nodeName}': childArrayPtr=0x{childArrayOffset:X8} -> bufferOffset={childArrayBufferOffset}, childCount={childCount}");

        // Controller arrays (these are also pointers)
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

        Radoub.Formats.Logging.UnifiedLogger.LogApplication(
            Radoub.Formats.Logging.LogLevel.DEBUG,
            $"[MDL] ParseNode: name='{nodeName}', flags=0x{nodeFlags:X8}, type={node.NodeType}, childCount={childCount}");

        // Set common properties
        node.Name = nodeName;
        node.InheritColor = inheritColor != 0;

        // Parse node-type-specific data
        try
        {
            ParseNodeTypeData(node, nodeOffset, nodeFlags, reader);
        }
        catch (Exception ex)
        {
            Radoub.Formats.Logging.UnifiedLogger.LogApplication(
                Radoub.Formats.Logging.LogLevel.WARN,
                $"[MDL] ParseNodeTypeData failed for '{nodeName}' (flags=0x{nodeFlags:X8}): {ex.Message}");
        }

        // Parse controller data for position/rotation (convert pointer to buffer offset)
        var ctrlKeyBufferOffset = PointerToModelOffset(controllerKeyOffset);
        var ctrlDataBufferOffset = PointerToModelOffset(controllerDataOffset);
        if (controllerKeyCount > 0 && ctrlKeyBufferOffset != 0xFFFFFFFF && ctrlKeyBufferOffset != uint.MaxValue)
        {
            try
            {
                ParseControllers(node, ctrlKeyBufferOffset, (int)controllerKeyCount,
                    ctrlDataBufferOffset);
            }
            catch (Exception ex)
            {
                Radoub.Formats.Logging.UnifiedLogger.LogApplication(
                    Radoub.Formats.Logging.LogLevel.WARN,
                    $"[MDL] ParseControllers failed for '{nodeName}': {ex.Message}");
            }
        }

        // Parse children (pointer already converted above)
        if (childCount > 0 && childArrayBufferOffset != 0xFFFFFFFF && childArrayBufferOffset != uint.MaxValue)
        {
            ParseChildren(node, childArrayBufferOffset, (int)childCount);
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
        // Mesh header structure based on Torlack's NWN Binary Model Files spec
        // Mesh header starts at nodeOffset + 0x70 (after 112-byte node header)
        // Total mesh header size: 0x200 bytes (512 bytes)
        //
        // Offsets relative to mesh header start (absolute offset = nodeOffset + 0x70 + relative):
        // 0x000-0x007: Function pointers (8 bytes)
        // 0x008-0x013: Face array (12 bytes: offset, count, allocated)
        // 0x014-0x01F: BBox min (12 bytes)
        // 0x020-0x02B: BBox max (12 bytes)
        // 0x02C-0x02F: Mesh radius (4 bytes)
        // 0x030-0x03B: Average point (12 bytes)
        // 0x03C-0x047: Diffuse color (12 bytes)
        // 0x048-0x053: Ambient color (12 bytes)
        // 0x054-0x05F: Specular color (12 bytes)
        // 0x060-0x063: Shininess (4 bytes)
        // 0x064-0x067: Shadow flag (4 bytes)
        // 0x068-0x06B: Beaming flag (4 bytes)
        // 0x06C-0x06F: Render flag (4 bytes)
        // 0x070-0x073: Transparency hint (4 bytes)
        // 0x074-0x077: Unknown (4 bytes)
        // 0x078-0x0B7: Texture0/Bitmap (64 bytes)
        // 0x0B8-0x0F7: Texture1 (64 bytes)
        // 0x0F8-0x137: Texture2 (64 bytes)
        // 0x138-0x177: Texture3 (64 bytes)
        // 0x178-0x17B: Tilefade (4 bytes)
        // 0x17C-0x187: Vertex indices compile-only (12 bytes)
        // 0x188-0x193: Leftover faces compile-only (12 bytes)
        // 0x194-0x19F: Vertex indices count array (12 bytes)
        // 0x1A0-0x1AB: Vertex indices offset array (12 bytes)
        // 0x1AC-0x1AF: Unknown triangle strips (4 bytes)
        // 0x1B0-0x1B3: Unknown triangle strips (4 bytes)
        // 0x1B4: Triangle mode (1 byte)
        // 0x1B5-0x1B7: Padding (3 bytes)
        // 0x1B8-0x1BB: Compile-only pointer (4 bytes)
        // 0x1BC-0x1BF: Vertex data pointer (4 bytes) <-- KEY OFFSET
        // 0x1C0-0x1C1: Vertex count (2 bytes)
        // 0x1C2-0x1C3: Texture count (2 bytes)
        // 0x1C4-0x1D3: Texture vertex pointers 0-3 (16 bytes)
        // 0x1D4-0x1D7: Vertex normals pointer (4 bytes)
        // 0x1D8-0x1DB: Vertex colors pointer (4 bytes)
        // 0x1DC-0x1F3: Texture animation data (24 bytes)
        // 0x1F4: Light mapped flag (1 byte)
        // 0x1F5: Rotate texture flag (1 byte)
        // 0x1F6-0x1F7: Padding (2 bytes)
        // 0x1F8-0x1FF: Misc values (8 bytes)

        var meshHeaderStart = reader.BaseStream.Position;

        Radoub.Formats.Logging.UnifiedLogger.LogApplication(
            Radoub.Formats.Logging.LogLevel.DEBUG,
            $"[MDL] ParseMeshNode: meshHeaderStart=0x{meshHeaderStart:X4}");

        // 0x000: Skip mesh routines (2 uint32 = 8 bytes)
        reader.BaseStream.Position += 8;

        // 0x008: Faces array (12 bytes)
        var faceArrayOffset = reader.ReadUInt32();
        var faceCount = reader.ReadUInt32();
        reader.ReadUInt32(); // allocated

        // 0x014: Bounding box (40 bytes total)
        var meshBMin = ReadVector3(reader);
        var meshBMax = ReadVector3(reader);
        var meshRadius = reader.ReadSingle();
        var meshBAverage = ReadVector3(reader);

        // 0x03C: Material properties
        mesh.Diffuse = ReadVector3(reader);
        mesh.Ambient = ReadVector3(reader);
        mesh.Specular = ReadVector3(reader);
        mesh.Shininess = reader.ReadSingle();

        // 0x064: Flags
        mesh.Shadow = reader.ReadUInt32() != 0;
        mesh.Beaming = reader.ReadUInt32() != 0;
        mesh.Render = reader.ReadUInt32() != 0;
        mesh.TransparencyHint = reader.ReadInt32();

        // 0x074: Unknown value
        reader.ReadUInt32();

        // 0x078: Textures (4 x 64 bytes = 256 bytes)
        mesh.Bitmap = ReadFixedString(reader, 64);   // Texture0
        mesh.Bitmap2 = ReadFixedString(reader, 64);  // Texture1
        ReadFixedString(reader, 64);                  // Texture2
        ReadFixedString(reader, 64);                  // Texture3

        // 0x178: Tilefade (4 bytes)
        mesh.Tilefade = reader.ReadInt32();

        // 0x17C: Skip compile-only vertex index arrays (2 arrays x 12 bytes = 24 bytes)
        reader.BaseStream.Position += 24;

        // 0x194: Skip vertex indices count/offset arrays (2 arrays x 12 bytes = 24 bytes)
        reader.BaseStream.Position += 24;

        // 0x1AC: Skip triangle strip unknowns (8 bytes)
        reader.BaseStream.Position += 8;

        // 0x1B4: Triangle mode (1 byte) + padding (3 bytes)
        reader.ReadByte();
        reader.BaseStream.Position += 3;

        // 0x1B8: Skip compile-only pointer (4 bytes)
        reader.ReadUInt32();

        // 0x1BC: Vertex data pointer and count
        var vertexDataOffset = reader.ReadUInt32();
        var vertexCount = reader.ReadUInt16();
        var textureCount = reader.ReadUInt16();

        // 0x1C4: Texture coordinate pointers (4 sets x 4 bytes = 16 bytes)
        var tvertOffsets = new uint[4];
        for (int i = 0; i < 4; i++)
            tvertOffsets[i] = reader.ReadUInt32();

        // 0x1D4: Normals pointer
        var normalsOffset = reader.ReadUInt32();

        // 0x1D8: Colors pointer
        var colorsOffset = reader.ReadUInt32();


        // 0x1DC: Skip texture animation data (24 bytes)
        reader.BaseStream.Position += 24;

        // 0x1F4: Light mapped and rotate texture flags
        reader.ReadByte(); // light mapped
        mesh.RotateTexture = reader.ReadByte() != 0;
        reader.BaseStream.Position += 2; // padding

        // Read vertex data from raw data section (convert pointer to raw buffer offset)
        var vertexRawOffset = PointerToRawOffset(vertexDataOffset);
        var normalsRawOffset = PointerToRawOffset(normalsOffset);

        Radoub.Formats.Logging.UnifiedLogger.LogApplication(
            Radoub.Formats.Logging.LogLevel.DEBUG,
            $"[MDL] Mesh vertexDataPtr=0x{vertexDataOffset:X8} -> rawOffset={vertexRawOffset}, normalsPtr=0x{normalsOffset:X8} -> rawOffset={normalsRawOffset}, vertexCount={vertexCount}, faceCount={faceCount}");

        // Read vertex positions from raw data section
        // WORKAROUND: Some NWN body part MDL files have an "average normal" vector at the start of
        // the vertex data section. This unit-length vector precedes the actual vertex positions.
        // We detect this by checking if the first vector has unit magnitude and the second has small magnitude.
        if (vertexCount > 0 && vertexRawOffset != 0xFFFFFFFF && vertexRawOffset != uint.MaxValue &&
            vertexRawOffset + 24 <= _rawData.Length)
        {
            float vx = BitConverter.ToSingle(_rawData, (int)vertexRawOffset);
            float vy = BitConverter.ToSingle(_rawData, (int)vertexRawOffset + 4);
            float vz = BitConverter.ToSingle(_rawData, (int)vertexRawOffset + 8);
            float vMag = (float)Math.Sqrt(vx * vx + vy * vy + vz * vz);

            // If first vector looks like a unit normal (not at origin)
            if (vMag > 0.98f && vMag < 1.02f && !(vx == 0 && vy == 0 && vz == 0))
            {
                // Check if second vector has small magnitude (typical for local-space positions)
                float v2x = BitConverter.ToSingle(_rawData, (int)vertexRawOffset + 12);
                float v2y = BitConverter.ToSingle(_rawData, (int)vertexRawOffset + 16);
                float v2z = BitConverter.ToSingle(_rawData, (int)vertexRawOffset + 20);
                float v2Mag = (float)Math.Sqrt(v2x * v2x + v2y * v2y + v2z * v2z);

                if (v2Mag < 0.3f)
                {
                    // First vector is an "average normal" header - skip it
                    vertexRawOffset += 12;
                    Radoub.Formats.Logging.UnifiedLogger.LogApplication(
                        Radoub.Formats.Logging.LogLevel.DEBUG,
                        $"[MDL] Skipping average normal header ({vx:F2},{vy:F2},{vz:F2}), actual positions start at offset {vertexRawOffset}");
                }
            }
        }

        uint actualVertexOffset = vertexRawOffset;
        uint actualNormalsOffset = normalsRawOffset;

        if (vertexCount > 0 && actualVertexOffset != 0xFFFFFFFF && actualVertexOffset != uint.MaxValue)
        {
            mesh.Vertices = ReadVertices(actualVertexOffset, vertexCount);
        }

        // Read normals from raw data section
        if (vertexCount > 0 && actualNormalsOffset != 0xFFFFFFFF && actualNormalsOffset != uint.MaxValue)
        {
            mesh.Normals = ReadVertices(actualNormalsOffset, vertexCount);
        }

        // Read texture coordinates (convert pointers to raw buffer offsets)
        if (textureCount > 0 && vertexCount > 0)
        {
            var texCoordsList = new List<Vector2[]>();
            for (int i = 0; i < Math.Min((int)textureCount, 4); i++)
            {
                var tvertRawOffset = PointerToRawOffset(tvertOffsets[i]);
                if (tvertRawOffset != 0xFFFFFFFF && tvertRawOffset != uint.MaxValue)
                {
                    texCoordsList.Add(ReadTexCoords(tvertRawOffset, vertexCount));
                }
            }
            mesh.TextureCoords = texCoordsList.ToArray();
        }

        // Read vertex colors (convert pointer to raw buffer offset)
        var colorsRawOffset = PointerToRawOffset(colorsOffset);
        if (vertexCount > 0 && colorsRawOffset != 0xFFFFFFFF && colorsRawOffset != uint.MaxValue)
        {
            mesh.VertexColors = ReadColors(colorsRawOffset, vertexCount);
        }

        // Read faces from model data section (convert pointer to model buffer offset)
        var faceArrayBufferOffset = PointerToModelOffset(faceArrayOffset);
        if (faceCount > 0 && faceArrayBufferOffset != 0xFFFFFFFF && faceArrayBufferOffset != uint.MaxValue)
        {
            mesh.Faces = ReadFaces(faceArrayBufferOffset, (int)faceCount);
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
            // AABB tree pointer (convert pointer to model buffer offset)
            var aabbTreePointer = reader.ReadUInt32();
            var aabbTreeBufferOffset = PointerToModelOffset(aabbTreePointer);
            if (aabbTreeBufferOffset != 0xFFFFFFFF && aabbTreeBufferOffset != uint.MaxValue)
            {
                aabb.RootAabb = ParseAabbTree(aabbTreeBufferOffset);
            }
        }
    }

    private Vector3[] ReadVertices(uint offset, int count)
    {
        var vertices = new Vector3[count];
        if (_rawData.Length == 0 || offset + count * 12 > _rawData.Length)
        {
            // Not enough data - return empty array
            return vertices;
        }

        using var stream = new MemoryStream(_rawData);
        using var reader = new BinaryReader(stream);

        stream.Position = offset;
        for (int i = 0; i < count; i++)
        {
            vertices[i] = ReadVector3(reader);
        }

        // Debug: log first few vertices
        if (count > 0)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append($"[MDL] ReadVertices: offset={offset}, count={count}, first 3: ");
            for (int i = 0; i < Math.Min(3, count); i++)
            {
                sb.Append($"[{vertices[i].X:F3},{vertices[i].Y:F3},{vertices[i].Z:F3}] ");
            }
            Radoub.Formats.Logging.UnifiedLogger.LogApplication(
                Radoub.Formats.Logging.LogLevel.DEBUG, sb.ToString());
        }

        return vertices;
    }

    private Vector2[] ReadTexCoords(uint offset, int count)
    {
        var coords = new Vector2[count];
        if (_rawData.Length == 0 || offset + count * 8 > _rawData.Length)
        {
            return coords;
        }

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
        if (_rawData.Length == 0 || offset + count * 4 > _rawData.Length)
        {
            return colors;
        }

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
        if (_modelData.Length == 0 || offset + count * FaceSize > _modelData.Length)
        {
            return faces;
        }

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
        if (offset == 0xFFFFFFFF || offset >= _modelData.Length) return null;

        using var stream = new MemoryStream(_modelData);
        using var reader = new BinaryReader(stream);
        stream.Position = offset;

        var entry = new MdlAabbEntry();
        entry.BoundingMin = ReadVector3(reader);
        entry.BoundingMax = ReadVector3(reader);

        var leftPointer = reader.ReadUInt32();
        var rightPointer = reader.ReadUInt32();
        entry.LeafFaceIndex = reader.ReadInt32();
        reader.ReadUInt32(); // plane flags

        // Convert child pointers to buffer offsets
        var leftBufferOffset = PointerToModelOffset(leftPointer);
        var rightBufferOffset = PointerToModelOffset(rightPointer);

        if (leftBufferOffset != 0xFFFFFFFF && leftBufferOffset != uint.MaxValue)
            entry.Left = ParseAabbTree(leftBufferOffset);
        if (rightBufferOffset != 0xFFFFFFFF && rightBufferOffset != uint.MaxValue)
            entry.Right = ParseAabbTree(rightBufferOffset);

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
        Radoub.Formats.Logging.UnifiedLogger.LogApplication(
            Radoub.Formats.Logging.LogLevel.DEBUG,
            $"[MDL] ParseChildren START: parent='{parent.Name}', arrayOffset={arrayOffset}, count={count}, modelDataLen={_modelData.Length}");

        // Verify we can read the child array
        if (arrayOffset + count * 4 > _modelData.Length)
        {
            Radoub.Formats.Logging.UnifiedLogger.LogApplication(
                Radoub.Formats.Logging.LogLevel.WARN,
                $"[MDL] ParseChildren: Child array out of bounds! arrayOffset={arrayOffset}, count={count}, needed={arrayOffset + count * 4}, available={_modelData.Length}");
            return;
        }

        using var stream = new MemoryStream(_modelData);
        using var reader = new BinaryReader(stream);

        for (int i = 0; i < count; i++)
        {
            stream.Position = arrayOffset + i * 4;
            var childPointer = reader.ReadUInt32();

            // Convert pointer to buffer offset
            var childOffset = PointerToModelOffset(childPointer);

            Radoub.Formats.Logging.UnifiedLogger.LogApplication(
                Radoub.Formats.Logging.LogLevel.DEBUG,
                $"[MDL] ParseChildren: parent='{parent.Name}', i={i}, childPtr=0x{childPointer:X8} -> offset={childOffset}, valid={childOffset != 0xFFFFFFFF && childOffset != uint.MaxValue && childOffset < _modelData.Length}");

            if (childOffset != 0xFFFFFFFF && childOffset != uint.MaxValue && childOffset < _modelData.Length)
            {
                var child = ParseNode(childOffset);
                child.Parent = parent;
                parent.Children.Add(child);
                Radoub.Formats.Logging.UnifiedLogger.LogApplication(
                    Radoub.Formats.Logging.LogLevel.DEBUG,
                    $"[MDL] ParseChildren: Added child '{child.Name}' to '{parent.Name}', parent now has {parent.Children.Count} children");
            }
            else
            {
                Radoub.Formats.Logging.UnifiedLogger.LogApplication(
                    Radoub.Formats.Logging.LogLevel.WARN,
                    $"[MDL] ParseChildren: SKIPPED invalid child pointer for '{parent.Name}'[{i}]");
            }
        }

        Radoub.Formats.Logging.UnifiedLogger.LogApplication(
            Radoub.Formats.Logging.LogLevel.DEBUG,
            $"[MDL] ParseChildren END: parent='{parent.Name}' now has {parent.Children.Count} children");
    }

    private void ParseAnimations(MdlModel model, uint arrayOffset, int count)
    {
        using var stream = new MemoryStream(_modelData);
        using var reader = new BinaryReader(stream);

        for (int i = 0; i < count; i++)
        {
            stream.Position = arrayOffset + i * 4;
            var animPointer = reader.ReadUInt32();

            // Convert animation pointer to buffer offset
            var animBufferOffset = PointerToModelOffset(animPointer);
            if (animBufferOffset != 0xFFFFFFFF && animBufferOffset != uint.MaxValue)
            {
                var anim = ParseAnimation(animBufferOffset);
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
        var eventsPointer = reader.ReadUInt32();
        var eventsCount = reader.ReadUInt32();
        reader.ReadUInt32(); // allocated

        // Convert events pointer to buffer offset
        var eventsBufferOffset = PointerToModelOffset(eventsPointer);
        if (eventsCount > 0 && eventsBufferOffset != 0xFFFFFFFF && eventsBufferOffset != uint.MaxValue)
        {
            for (int i = 0; i < eventsCount; i++)
            {
                stream.Position = eventsBufferOffset + i * 36; // 4 + 32 bytes per event
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
