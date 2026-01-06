// MDL Binary Reader - Mesh node parsing
// Partial class for mesh/trimesh specific data parsing

using System.Numerics;
using System.Text;

namespace Radoub.Formats.Mdl;

public partial class MdlBinaryReader
{
    private void ParseMeshNode(MdlTrimeshNode mesh, BinaryReader reader, uint flags)
    {
        // Mesh header structure based on Torlack's NWN Binary Model Files spec
        // Mesh header starts at nodeOffset + 0x70 (after 112-byte node header)
        // Total mesh header size: 0x200 bytes (512 bytes)

        var meshHeaderStart = reader.BaseStream.Position;

        Logging.UnifiedLogger.LogApplication(Logging.LogLevel.DEBUG,
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

        Logging.UnifiedLogger.LogApplication(Logging.LogLevel.DEBUG,
            $"[MDL] Mesh vertexDataPtr=0x{vertexDataOffset:X8} -> rawOffset={vertexRawOffset}, normalsPtr=0x{normalsOffset:X8} -> rawOffset={normalsRawOffset}, vertexCount={vertexCount}, faceCount={faceCount}");

        // Detect and skip average normal header if present
        vertexRawOffset = DetectAndSkipAverageNormal(vertexRawOffset, vertexCount);

        uint actualVertexOffset = vertexRawOffset;
        uint actualNormalsOffset = normalsRawOffset;

        if (vertexCount > 0 && actualVertexOffset != 0xFFFFFFFF && actualVertexOffset != uint.MaxValue)
        {
            mesh.Vertices = ReadVertices(actualVertexOffset, vertexCount);
        }

        if (vertexCount > 0 && actualNormalsOffset != 0xFFFFFFFF && actualNormalsOffset != uint.MaxValue)
        {
            mesh.Normals = ReadVertices(actualNormalsOffset, vertexCount);
        }

        // Read texture coordinates
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

        // Read vertex colors
        var colorsRawOffset = PointerToRawOffset(colorsOffset);
        if (vertexCount > 0 && colorsRawOffset != 0xFFFFFFFF && colorsRawOffset != uint.MaxValue)
        {
            mesh.VertexColors = ReadColors(colorsRawOffset, vertexCount);
        }

        // Read faces from model data section
        var faceArrayBufferOffset = PointerToModelOffset(faceArrayOffset);
        if (faceCount > 0 && faceArrayBufferOffset != 0xFFFFFFFF && faceArrayBufferOffset != uint.MaxValue)
        {
            mesh.Faces = ReadFaces(faceArrayBufferOffset, (int)faceCount);
        }

        // Parse type-specific data
        if ((flags & NodeFlagHasDangly) != 0 && mesh is MdlDanglyNode dangly)
        {
            reader.BaseStream.Position += 12; // skip constraints array header
            dangly.Displacement = reader.ReadSingle();
            dangly.Tightness = reader.ReadSingle();
            dangly.Period = reader.ReadSingle();
        }

        if ((flags & NodeFlagHasAABB) != 0 && mesh is MdlAabbNode aabb)
        {
            var aabbTreePointer = reader.ReadUInt32();
            var aabbTreeBufferOffset = PointerToModelOffset(aabbTreePointer);
            if (aabbTreeBufferOffset != 0xFFFFFFFF && aabbTreeBufferOffset != uint.MaxValue)
            {
                aabb.RootAabb = ParseAabbTree(aabbTreeBufferOffset);
            }
        }
    }

    /// <summary>
    /// Detect and skip "average normal" header that some NWN body part MDL files have.
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
                Logging.UnifiedLogger.LogApplication(Logging.LogLevel.DEBUG,
                    $"[MDL] Skipping average normal header ({vx:F2},{vy:F2},{vz:F2}), actual positions start at offset {vertexRawOffset}");
            }
        }

        return vertexRawOffset;
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

        var leftBufferOffset = PointerToModelOffset(leftPointer);
        var rightBufferOffset = PointerToModelOffset(rightPointer);

        if (leftBufferOffset != 0xFFFFFFFF && leftBufferOffset != uint.MaxValue)
            entry.Left = ParseAabbTree(leftBufferOffset);
        if (rightBufferOffset != 0xFFFFFFFF && rightBufferOffset != uint.MaxValue)
            entry.Right = ParseAabbTree(rightBufferOffset);

        return entry;
    }
}
