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
}
