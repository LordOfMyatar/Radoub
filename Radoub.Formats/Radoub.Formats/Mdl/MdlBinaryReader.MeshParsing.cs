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
            $"[MDL] ParseMeshNode '{mesh.Name}': meshHeaderStart=0x{meshHeaderStart:X4}");

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
        var textureOffset = reader.BaseStream.Position;
        mesh.Bitmap = ReadFixedString(reader, 64);   // Texture0
        Logging.UnifiedLogger.LogApplication(Logging.LogLevel.DEBUG,
            $"[MDL] Mesh texture0 at 0x{textureOffset:X4} (relative 0x{textureOffset - meshHeaderStart:X4}): '{mesh.Bitmap}'");
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
            $"[MDL] Mesh '{mesh.Name}': vertexDataPtr=0x{vertexDataOffset:X8} -> rawOffset={vertexRawOffset}, normalsPtr=0x{normalsOffset:X8} -> rawOffset={normalsRawOffset}, vertexCount={vertexCount}, faceCount={faceCount}, rawDataLen={_rawData.Length}");

        // Log raw bytes at vertex offset for debugging
        if (vertexRawOffset != 0xFFFFFFFF && vertexRawOffset != uint.MaxValue && vertexRawOffset + 24 <= _rawData.Length)
        {
            var rawBytes = new byte[24];
            Array.Copy(_rawData, (int)vertexRawOffset, rawBytes, 0, 24);
            Logging.UnifiedLogger.LogApplication(Logging.LogLevel.DEBUG,
                $"[MDL] Mesh '{mesh.Name}': raw bytes at vertexOffset: {BitConverter.ToString(rawBytes)}");
        }

        // Detect and skip average normal header if present
        // This header is prepended to ALL raw data arrays (vertices, UVs, normals)
        var originalVertexRawOffset = vertexRawOffset;
        vertexRawOffset = DetectAndSkipAverageNormal(vertexRawOffset, vertexCount);
        uint avgNormalSkip = vertexRawOffset - originalVertexRawOffset;  // Usually 0 or 12

        uint actualVertexOffset = vertexRawOffset;
        uint actualNormalsOffset = normalsRawOffset;

        if (vertexCount > 0 && actualVertexOffset != 0xFFFFFFFF && actualVertexOffset != uint.MaxValue)
        {
            mesh.Vertices = ReadVertices(actualVertexOffset, vertexCount);
            Logging.UnifiedLogger.LogApplication(Logging.LogLevel.DEBUG,
                $"[MDL] Mesh '{mesh.Name}': Read {mesh.Vertices?.Length ?? 0} vertices");
        }

        if (vertexCount > 0 && actualNormalsOffset != 0xFFFFFFFF && actualNormalsOffset != uint.MaxValue)
        {
            // Apply same offset adjustment as vertices - normals array also has the avg normal header
            var adjustedNormalsOffset = actualNormalsOffset + avgNormalSkip;
            Logging.UnifiedLogger.LogApplication(Logging.LogLevel.DEBUG,
                $"[MDL] Mesh '{mesh.Name}': Normals offset {actualNormalsOffset} + {avgNormalSkip} = {adjustedNormalsOffset}");
            mesh.Normals = ReadVertices(adjustedNormalsOffset, vertexCount);
        }

        // Read texture coordinates - apply same average normal skip as vertices
        // The UV offset in MDL header is relative to the same base that vertex offset uses
        if (textureCount > 0 && vertexCount > 0)
        {
            var texCoordsList = new List<Vector2[]>();
            for (int i = 0; i < Math.Min((int)textureCount, 4); i++)
            {
                var tvertRawOffset = PointerToRawOffset(tvertOffsets[i]);
                if (tvertRawOffset != 0xFFFFFFFF && tvertRawOffset != uint.MaxValue)
                {
                    // Apply same offset adjustment as vertices
                    var adjustedTvertOffset = tvertRawOffset + avgNormalSkip;
                    Logging.UnifiedLogger.LogApplication(Logging.LogLevel.DEBUG,
                        $"[MDL] Mesh '{mesh.Name}': UV[{i}] offset {tvertRawOffset} + {avgNormalSkip} = {adjustedTvertOffset}");
                    texCoordsList.Add(ReadTexCoords(adjustedTvertOffset, vertexCount));
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

            // Validate face indices against vertex count — reject out-of-bounds faces
            if (mesh.Faces != null && mesh.Vertices != null)
            {
                int maxIdx = 0;
                int minIdx = int.MaxValue;
                int invalidFaceCount = 0;
                var validFaces = new List<MdlFace>(mesh.Faces.Length);

                foreach (var face in mesh.Faces)
                {
                    if (face.VertexIndex0 < 0 || face.VertexIndex0 >= mesh.Vertices.Length ||
                        face.VertexIndex1 < 0 || face.VertexIndex1 >= mesh.Vertices.Length ||
                        face.VertexIndex2 < 0 || face.VertexIndex2 >= mesh.Vertices.Length)
                    {
                        invalidFaceCount++;
                        continue;
                    }
                    validFaces.Add(face);
                    maxIdx = Math.Max(maxIdx, Math.Max(face.VertexIndex0, Math.Max(face.VertexIndex1, face.VertexIndex2)));
                    minIdx = Math.Min(minIdx, Math.Min(face.VertexIndex0, Math.Min(face.VertexIndex1, face.VertexIndex2)));
                }

                if (invalidFaceCount > 0)
                {
                    Logging.UnifiedLogger.LogApplication(Logging.LogLevel.WARN,
                        $"[MDL] Mesh '{mesh.Name}': Rejected {invalidFaceCount}/{mesh.Faces.Length} faces with out-of-bounds indices (vertexCount={mesh.Vertices.Length})");
                    mesh.Faces = validFaces.ToArray();
                }

                if (validFaces.Count > 0)
                {
                    var f0 = validFaces[0];
                    var v0 = mesh.Vertices[f0.VertexIndex0];
                    Logging.UnifiedLogger.LogApplication(Logging.LogLevel.DEBUG,
                        $"[MDL] Mesh '{mesh.Name}': {validFaces.Count} valid faces, idx range [{minIdx},{maxIdx}], vtxCount={mesh.Vertices.Length}. Face0: [{f0.VertexIndex0},{f0.VertexIndex1},{f0.VertexIndex2}] -> v0=({v0.X:F3},{v0.Y:F3},{v0.Z:F3})");
                }
            }
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

        // Skin node extension data (0x64 bytes after mesh header, at nodeOffset + 0x270)
        if ((flags & NodeFlagHasSkin) != 0 && mesh is MdlSkinNode skin)
        {
            ParseSkinNode(skin, reader, meshHeaderStart, vertexCount);
        }
    }

    /// <summary>
    /// Parse skin mesh extension data: bone weights, bone refs, and inverse bind-pose transforms.
    /// The skin extension is 0x64 bytes starting at mesh header + 0x200 (i.e., nodeOffset + 0x270).
    /// Layout based on nwnexplorer CNwnMdlSkinMeshNode structure.
    /// </summary>
    private void ParseSkinNode(MdlSkinNode skin, BinaryReader reader, long meshHeaderStart, int vertexCount)
    {
        // Skin extension starts at meshHeaderStart + 0x200
        var skinExtStart = meshHeaderStart + 0x200;
        reader.BaseStream.Position = skinExtStart;

        Logging.UnifiedLogger.LogApplication(Logging.LogLevel.DEBUG,
            $"[MDL] ParseSkinNode '{skin.Name}': skinExtStart=0x{skinExtStart:X4}, vertexCount={vertexCount}");

        // 0x000: m_aWeights array header (12 bytes) — string-based weight data in model data, skip
        reader.BaseStream.Position += 12;

        // 0x00C: m_pafSkinWeights — pointer to raw data: 4 floats per vertex (bone weights)
        var skinWeightsPtr = reader.ReadUInt32();

        // 0x010: m_pasSkinBoneRefs — pointer to raw data: 4 int16s per vertex (bone indices)
        var skinBoneRefsPtr = reader.ReadUInt32();

        // 0x014: m_pasNodeToBoneMap — pointer to model data (int16 array), skip
        reader.ReadUInt32();
        // 0x018: m_ulNodeToBoneCount
        reader.ReadUInt32();

        // 0x01C: m_aQBoneRefInv — array of inverse bind-pose quaternions (CNwnArray: offset, count, allocated)
        var qBoneArrayOffset = reader.ReadUInt32();
        var qBoneCount = reader.ReadUInt32();
        reader.ReadUInt32(); // allocated

        // 0x028: m_aTBoneRefInv — array of inverse bind-pose translations (CNwnArray: offset, count, allocated)
        var tBoneArrayOffset = reader.ReadUInt32();
        var tBoneCount = reader.ReadUInt32();
        reader.ReadUInt32(); // allocated

        Logging.UnifiedLogger.LogApplication(Logging.LogLevel.DEBUG,
            $"[MDL] Skin '{skin.Name}': weightsPtr=0x{skinWeightsPtr:X8}, boneRefsPtr=0x{skinBoneRefsPtr:X8}, " +
            $"qBonePtr=0x{qBoneArrayOffset:X8} count={qBoneCount}, tBonePtr=0x{tBoneArrayOffset:X8} count={tBoneCount}");

        // Read per-vertex bone weights from raw data (4 floats per vertex)
        var weightsRawOffset = PointerToRawOffset(skinWeightsPtr);
        if (vertexCount > 0 && weightsRawOffset != uint.MaxValue)
        {
            var requiredBytes = (uint)(vertexCount * 4 * sizeof(float));
            if (weightsRawOffset + requiredBytes <= _rawData.Length)
            {
                var weights = new MdlBoneWeight[vertexCount];

                // Read per-vertex bone refs from raw data (4 int16s per vertex)
                var refsRawOffset = PointerToRawOffset(skinBoneRefsPtr);
                short[]? boneRefs = null;
                if (refsRawOffset != uint.MaxValue)
                {
                    var refsRequired = (uint)(vertexCount * 4 * sizeof(short));
                    if (refsRawOffset + refsRequired <= _rawData.Length)
                    {
                        boneRefs = new short[vertexCount * 4];
                        Buffer.BlockCopy(_rawData, (int)refsRawOffset, boneRefs, 0, (int)refsRequired);
                    }
                }

                for (int i = 0; i < vertexCount; i++)
                {
                    var baseOff = (int)weightsRawOffset + i * 16; // 4 floats = 16 bytes
                    weights[i] = new MdlBoneWeight
                    {
                        Weight0 = BitConverter.ToSingle(_rawData, baseOff),
                        Weight1 = BitConverter.ToSingle(_rawData, baseOff + 4),
                        Weight2 = BitConverter.ToSingle(_rawData, baseOff + 8),
                        Weight3 = BitConverter.ToSingle(_rawData, baseOff + 12),
                        Bone0 = boneRefs != null ? boneRefs[i * 4] : -1,
                        Bone1 = boneRefs != null ? boneRefs[i * 4 + 1] : -1,
                        Bone2 = boneRefs != null ? boneRefs[i * 4 + 2] : -1,
                        Bone3 = boneRefs != null ? boneRefs[i * 4 + 3] : -1,
                    };
                }

                skin.BoneWeights = weights;
                Logging.UnifiedLogger.LogApplication(Logging.LogLevel.DEBUG,
                    $"[MDL] Skin '{skin.Name}': Read {vertexCount} bone weight entries");
            }
            else
            {
                Logging.UnifiedLogger.LogApplication(Logging.LogLevel.WARN,
                    $"[MDL] Skin '{skin.Name}': Bone weights would read past raw data (offset={weightsRawOffset}, need={requiredBytes}, have={_rawData.Length})");
            }
        }

        // Read inverse bind-pose quaternions from model data
        var qBoneBufferOffset = PointerToModelOffset(qBoneArrayOffset);
        if (qBoneCount > 0 && qBoneBufferOffset != uint.MaxValue)
        {
            var requiredBytes = qBoneCount * 16; // 4 floats per quaternion
            if (qBoneBufferOffset + requiredBytes <= _modelData.Length)
            {
                skin.BoneQuaternions = new Quaternion[qBoneCount];
                using var qStream = new MemoryStream(_modelData);
                using var qReader = new BinaryReader(qStream);
                qStream.Position = qBoneBufferOffset;

                for (int i = 0; i < (int)qBoneCount; i++)
                {
                    // On-disk order after nwnexplorer's reorder step: (W, X, Y, Z)
                    // But the reorder swaps from CQuaternion's (X,Y,Z,W) to disk (W,X,Y,Z)
                    // System.Numerics.Quaternion constructor: (X, Y, Z, W)
                    // Reading raw floats and trying both interpretations
                    var f0 = qReader.ReadSingle();
                    var f1 = qReader.ReadSingle();
                    var f2 = qReader.ReadSingle();
                    var f3 = qReader.ReadSingle();
                    // Disk format (W,X,Y,Z) -> Quaternion(X,Y,Z,W) = Quaternion(f1,f2,f3,f0)
                    skin.BoneQuaternions[i] = new Quaternion(f1, f2, f3, f0);
                }

                Logging.UnifiedLogger.LogApplication(Logging.LogLevel.DEBUG,
                    $"[MDL] Skin '{skin.Name}': Read {qBoneCount} inverse bind-pose quaternions");
            }
        }

        // Read inverse bind-pose translations from model data
        var tBoneBufferOffset = PointerToModelOffset(tBoneArrayOffset);
        if (tBoneCount > 0 && tBoneBufferOffset != uint.MaxValue)
        {
            var requiredBytes = tBoneCount * 12; // 3 floats per Vector3
            if (tBoneBufferOffset + requiredBytes <= _modelData.Length)
            {
                skin.BoneTranslations = new Vector3[tBoneCount];
                using var tStream = new MemoryStream(_modelData);
                using var tReader = new BinaryReader(tStream);
                tStream.Position = tBoneBufferOffset;

                for (int i = 0; i < (int)tBoneCount; i++)
                {
                    skin.BoneTranslations[i] = ReadVector3(tReader);
                }

                Logging.UnifiedLogger.LogApplication(Logging.LogLevel.DEBUG,
                    $"[MDL] Skin '{skin.Name}': Read {tBoneCount} inverse bind-pose translations");
            }
        }
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

        Logging.UnifiedLogger.LogApplication(Logging.LogLevel.DEBUG,
            $"[MDL] AvgNormal check at offset {vertexRawOffset}: v1=({vx:F4},{vy:F4},{vz:F4}) mag={vMag:F4}, v2=({v2x:F4},{v2y:F4},{v2z:F4}) mag={v2Mag:F4}");

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
            Logging.UnifiedLogger.LogApplication(Logging.LogLevel.DEBUG,
                $"[MDL] Skipping {skipReason} ({vx:F4},{vy:F4},{vz:F4}), actual positions start at offset {vertexRawOffset}");
        }

        return vertexRawOffset;
    }

    // Maximum AABB tree depth to prevent stack overflow from circular or pathologically deep trees.
    // Real NWN models rarely exceed depth 20; 64 is generous.
    private const int MaxAabbDepth = 64;

    private MdlAabbEntry? ParseAabbTree(uint offset)
    {
        var visitedOffsets = new HashSet<uint>();
        return ParseAabbTreeRecursive(offset, 0, visitedOffsets);
    }

    private MdlAabbEntry? ParseAabbTreeRecursive(uint offset, int depth, HashSet<uint> visitedOffsets)
    {
        if (offset == 0xFFFFFFFF || offset >= _modelData.Length) return null;

        if (depth >= MaxAabbDepth)
        {
            Logging.UnifiedLogger.LogApplication(Logging.LogLevel.WARN,
                $"[MDL] AABB tree exceeded max depth {MaxAabbDepth} at offset 0x{offset:X8} — truncating");
            return null;
        }

        if (!visitedOffsets.Add(offset))
        {
            Logging.UnifiedLogger.LogApplication(Logging.LogLevel.WARN,
                $"[MDL] AABB tree circular reference detected at offset 0x{offset:X8} — truncating");
            return null;
        }

        // Ensure enough data to read an AABB entry (6 floats + 4 uint32 = 40 bytes)
        if (offset + 40 > _modelData.Length)
        {
            Logging.UnifiedLogger.LogApplication(Logging.LogLevel.WARN,
                $"[MDL] AABB tree entry at offset 0x{offset:X8} would read past buffer end — truncating");
            return null;
        }

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
            entry.Left = ParseAabbTreeRecursive(leftBufferOffset, depth + 1, visitedOffsets);
        if (rightBufferOffset != 0xFFFFFFFF && rightBufferOffset != uint.MaxValue)
            entry.Right = ParseAabbTreeRecursive(rightBufferOffset, depth + 1, visitedOffsets);

        return entry;
    }
}
