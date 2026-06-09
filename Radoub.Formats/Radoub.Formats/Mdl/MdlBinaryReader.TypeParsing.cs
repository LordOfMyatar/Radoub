// MDL Binary Reader - Light, Emitter, Reference node parsing
// Partial class for non-mesh node type specific data

using System.Text;

namespace Radoub.Formats.Mdl;

public partial class MdlBinaryReader
{
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
        // Reader is positioned at node+0x70 (end of node header) on entry.
        reader.ReadSingle();                 // 0x70 deadSpace
        reader.ReadSingle();                 // 0x74 blastRadius
        reader.ReadSingle();                 // 0x78 blastLength
        emitter.XGrid = reader.ReadInt32();  // 0x7C
        emitter.YGrid = reader.ReadInt32();  // 0x80
        reader.ReadUInt32();                 // 0x84 spawnType
        emitter.Update = ReadFixedString(reader, 32);       // 0x88
        emitter.RenderMethod = ReadFixedString(reader, 32); // 0xA8
        emitter.Blend = ReadFixedString(reader, 32);        // 0xC8
        emitter.Texture = ReadFixedString(reader, 64);      // 0xE8 (64, not 32 — the bug)
        ReadFixedString(reader, 16);         // 0x128 chunkName (unused)
        reader.ReadUInt32();                 // 0x138 twoSidedTex
        emitter.Loop = reader.ReadUInt32() != 0;            // 0x13C
        emitter.RenderOrder = reader.ReadUInt16();          // 0x140
        reader.ReadUInt16();                 // 0x142 pad
        emitter.EmitterFlags = reader.ReadUInt32();         // 0x144
    }

    private void ParseReferenceNode(MdlReferenceNode reference, BinaryReader reader)
    {
        reference.RefModel = ReadFixedString(reader, 64);
        reference.Reattachable = reader.ReadUInt32() != 0;
    }
}
