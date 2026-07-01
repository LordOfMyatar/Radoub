using System;
using System.Text;

namespace Radoub.Formats.Tests.Mdl;

/// <summary>
/// Builds a minimal, byte-valid binary NWN MDL whose geometry root is a single trimesh
/// node with zero geometry (no verts/faces). Used to verify texture/material-name capture
/// (#2496) without hand-building vertex/face raw data — the reader guards all geometry reads
/// on count &gt; 0, so a zero-geometry mesh still exercises the texture-slot reads.
///
/// Layout mirrors the binary reader's expectations:
///   File header (12 bytes): zeroField=0, modelDataSize, rawDataSize=0
///   [0x000] model/geometry header (0xE8) — pointerBase=0, root ptr @0x48, nodeCount @0x4C
///   [0x0E8] trimesh node = 0x70 node header + 0x200 mesh header (0x270 total)
///
/// Texture slots live at mesh-header-relative 0x078 (4 × 64 bytes). texture3 (slot 4) is the
/// NWN:EE material name — confirmed by rollnw (MdlBinaryParser.hpp: "This is material name in NWN:EE").
/// </summary>
public static class TrimeshMdlFixture
{
    private const int FileHeaderSize = 12;
    private const int ModelHeaderSize = 0xE8;
    private const int NodeHeaderSize = 0x70;
    private const int MeshHeaderSize = 0x200;
    private const int TrimeshNodeSize = NodeHeaderSize + MeshHeaderSize; // 0x270

    private const uint NodeFlagHasMesh = 0x00000020;

    private const uint TrimeshNodeOffset = ModelHeaderSize; // 0x0E8

    // Mesh-header-relative texture block: 0x078, then 4 × 64-byte slots.
    private const int MeshTextureBlockRel = 0x078;
    private const int TextureSlotSize = 64;

    public static byte[] BuildSingleTrimesh(string bitmap, string materialName)
    {
        int modelDataSize = (int)TrimeshNodeOffset + TrimeshNodeSize;
        var modelData = new byte[modelDataSize];

        // ---- Model / geometry header ----
        WriteUInt32(modelData, 0x00, 0);                  // pointerBase = 0
        WriteFixedString(modelData, 0x08, "mattest", 64); // model name
        WriteUInt32(modelData, 0x48, TrimeshNodeOffset);  // root node pointer
        WriteUInt32(modelData, 0x4C, 1);                  // node count

        // ---- Trimesh node header (0x70) ----
        int n = (int)TrimeshNodeOffset;
        WriteUInt32(modelData, n + 24, 0);                       // inheritColor
        WriteInt32(modelData, n + 28, 0);                        // partNumber
        WriteFixedString(modelData, n + 32, "mat_mesh", 32);     // node name
        WriteUInt32(modelData, n + 0x40, 0);                     // geometry header ptr
        WriteUInt32(modelData, n + 0x44, 0);                     // parent ptr
        WriteUInt32(modelData, n + 0x48, 0);                     // child array ptr (none)
        WriteUInt32(modelData, n + 0x4C, 0);                     // child count
        WriteUInt32(modelData, n + 0x50, 0);                     // child allocated
        WriteUInt32(modelData, n + 0x54, 0);                     // ctrl key ptr (none)
        WriteUInt32(modelData, n + 0x58, 0);                     // ctrl key count
        WriteUInt32(modelData, n + 0x5C, 0);                     // ctrl key alloc
        WriteUInt32(modelData, n + 0x60, 0);                     // ctrl data ptr
        WriteUInt32(modelData, n + 0x64, 0);                     // ctrl data count
        WriteUInt32(modelData, n + 0x68, 0);                     // ctrl data alloc
        WriteUInt32(modelData, n + 0x6C, NodeFlagHasMesh);       // node flags -> trimesh

        // ---- Mesh header (0x200) relative to node + 0x70 ----
        int m = n + NodeHeaderSize;
        // 0x000 mesh routines (8) — zero. 0x008 faces array ptr/count/alloc — zero (no faces).
        // 0x014 bounding box / radius / avg (40) — zero. 0x03C material colors (Diffuse..Shininess) — zero.
        // 0x064 flags (shadow/beaming/render/transparency) — leave render=0 is fine for parsing.
        // 0x074 unknown — zero.
        // 0x078 texture block: 4 × 64-byte slots.
        WriteFixedString(modelData, m + MeshTextureBlockRel + 0 * TextureSlotSize, bitmap, TextureSlotSize);       // texture0 = bitmap
        WriteFixedString(modelData, m + MeshTextureBlockRel + 1 * TextureSlotSize, string.Empty, TextureSlotSize); // texture1
        WriteFixedString(modelData, m + MeshTextureBlockRel + 2 * TextureSlotSize, string.Empty, TextureSlotSize); // texture2
        WriteFixedString(modelData, m + MeshTextureBlockRel + 3 * TextureSlotSize, materialName, TextureSlotSize); // texture3 = materialname (EE)
        // Remaining mesh-header fields (vertex/tvert/normal/color pointers, counts) stay zero;
        // vertexCount=0 and faceCount=0 short-circuit all geometry reads.

        // ---- Prepend file header (12 bytes) ----
        var file = new byte[FileHeaderSize + modelData.Length];
        WriteUInt32(file, 0, 0);                            // zeroField
        WriteUInt32(file, 4, (uint)modelData.Length);       // model data size
        WriteUInt32(file, 8, 0);                            // raw data size
        Array.Copy(modelData, 0, file, FileHeaderSize, modelData.Length);
        return file;
    }

    private const uint NodeFlagHasDangly = 0x00000100;
    // Danglymesh node: trimesh flags PLUS the dangly bit. Dangly extension block sits right after
    // the 0x200 mesh header (meshHeaderStart + 0x200), matching nwnexplorer CNwnMdlDanglyMeshNode.
    private const uint DanglyMeshFlags = NodeFlagHasMesh | NodeFlagHasDangly;
    private const int DanglyExtSize = 24; // CNwnArray<float> constraints (12) + displacement/tightness/period (12)

    /// <summary>
    /// Build a binary MDL whose root is a single danglymesh node carrying the given
    /// displacement/tightness/period and a constraints float array (#2619 regression). The
    /// constraints data is appended after the node and referenced by a model-relative pointer.
    /// </summary>
    public static byte[] BuildSingleDangly(float displacement, float tightness, float period, float[] constraints)
    {
        int danglyNodeSize = NodeHeaderSize + MeshHeaderSize + DanglyExtSize; // 0x288
        int constraintsBytes = constraints.Length * sizeof(float);
        // Constraints array placed immediately after the dangly node, inside model data.
        int constraintsOffset = (int)TrimeshNodeOffset + danglyNodeSize;
        int modelDataSize = constraintsOffset + constraintsBytes;
        var modelData = new byte[modelDataSize];

        // ---- Model / geometry header ----
        WriteUInt32(modelData, 0x00, 0);                  // pointerBase = 0
        WriteFixedString(modelData, 0x08, "danglytest", 64);
        WriteUInt32(modelData, 0x48, TrimeshNodeOffset);  // root node pointer
        WriteUInt32(modelData, 0x4C, 1);                  // node count

        // ---- Dangly node header (0x70) ----
        int n = (int)TrimeshNodeOffset;
        WriteFixedString(modelData, n + 32, "mane", 32);  // node name
        WriteUInt32(modelData, n + 0x6C, DanglyMeshFlags); // node flags -> dangly trimesh

        // ---- Dangly extension at meshHeaderStart + 0x200 ----
        int ext = n + NodeHeaderSize + MeshHeaderSize;
        // m_afConstraints CNwnArray { ptr, count, allocated }. Pointer is model-relative (pointerBase=0).
        WriteUInt32(modelData, ext + 0, (uint)constraintsOffset);
        WriteUInt32(modelData, ext + 4, (uint)constraints.Length);
        WriteUInt32(modelData, ext + 8, (uint)constraints.Length);
        WriteSingle(modelData, ext + 12, displacement);
        WriteSingle(modelData, ext + 16, tightness);
        WriteSingle(modelData, ext + 20, period);

        // ---- Constraints float data ----
        for (int i = 0; i < constraints.Length; i++)
            WriteSingle(modelData, constraintsOffset + i * sizeof(float), constraints[i]);

        // ---- Prepend file header (12 bytes) ----
        var file = new byte[FileHeaderSize + modelData.Length];
        WriteUInt32(file, 0, 0);
        WriteUInt32(file, 4, (uint)modelData.Length);
        WriteUInt32(file, 8, 0);
        Array.Copy(modelData, 0, file, FileHeaderSize, modelData.Length);
        return file;
    }

    private static void WriteSingle(byte[] buf, int offset, float value) =>
        BitConverter.GetBytes(value).CopyTo(buf, offset);

    private static void WriteUInt32(byte[] buf, int offset, uint value) =>
        BitConverter.GetBytes(value).CopyTo(buf, offset);

    private static void WriteInt32(byte[] buf, int offset, int value) =>
        BitConverter.GetBytes(value).CopyTo(buf, offset);

    private static void WriteFixedString(byte[] buf, int offset, string value, int length)
    {
        var bytes = Encoding.ASCII.GetBytes(value ?? string.Empty);
        int count = Math.Min(bytes.Length, length - 1); // leave room for null terminator
        Array.Copy(bytes, 0, buf, offset, count);
    }
}
