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

    // EmitterFlags bits (node header field 0x144). Authoritative values from rollnw
    // EmitterFlag (lib/nw/model/Mdl.hpp). The ASCII reader populates these same bools from
    // named properties; decoding them here keeps binary (compiled) models symmetric with
    // ASCII so any flag-driven render gating behaves identically on both paths (#2544).
    private const uint EmitterFlagP2P = 0x0001;
    private const uint EmitterFlagP2PSel = 0x0002;
    private const uint EmitterFlagAffectedByWind = 0x0004;
    // 0x0008 IsTinted and 0x0080 InheritVel are consumed by the UI compiler, not the node bools.
    private const uint EmitterFlagBounce = 0x0010;
    private const uint EmitterFlagRandom = 0x0020;
    private const uint EmitterFlagInherit = 0x0040;
    private const uint EmitterFlagInheritLocal = 0x0100;
    private const uint EmitterFlagSplat = 0x0200;
    private const uint EmitterFlagInheritPart = 0x0400;

    private void ParseEmitterNode(MdlEmitterNode emitter, BinaryReader reader)
    {
        // Reader is positioned at node+0x70 (end of node header) on entry.
        reader.ReadSingle();                 // 0x70 deadSpace
        reader.ReadSingle();                 // 0x74 blastRadius
        reader.ReadSingle();                 // 0x78 blastLength
        emitter.XGrid = reader.ReadInt32();  // 0x7C
        emitter.YGrid = reader.ReadInt32();  // 0x80
        // 0x84 spawnType: a uint here; the ASCII reader keeps the numeric token verbatim
        // (real models author "spawntype 0"), so stringify for symmetry with that path.
        emitter.SpawnType = reader.ReadUInt32().ToString();
        emitter.Update = ReadFixedString(reader, 32);       // 0x88
        emitter.RenderMethod = ReadFixedString(reader, 32); // 0xA8
        emitter.Blend = ReadFixedString(reader, 32);        // 0xC8
        emitter.Texture = ReadFixedString(reader, 64);      // 0xE8 (64, not 32 — the bug)
        ReadFixedString(reader, 16);         // 0x128 chunkName (unused)
        reader.ReadUInt32();                 // 0x138 twoSidedTex
        emitter.Loop = reader.ReadUInt32() != 0;            // 0x13C
        emitter.RenderOrder = reader.ReadUInt16();          // 0x140
        reader.ReadUInt16();                 // 0x142 pad
        uint flags = reader.ReadUInt32();                   // 0x144
        emitter.EmitterFlags = flags;

        // Decode the individual bools so binary models match ASCII (#2544).
        emitter.P2P = (flags & EmitterFlagP2P) != 0;
        emitter.P2PBezier = (flags & EmitterFlagP2PSel) != 0;
        emitter.AffectedByWind = (flags & EmitterFlagAffectedByWind) != 0;
        emitter.Bounce = (flags & EmitterFlagBounce) != 0;
        emitter.Random = (flags & EmitterFlagRandom) != 0;
        emitter.Inherit = (flags & EmitterFlagInherit) != 0;
        emitter.InheritLocal = (flags & EmitterFlagInheritLocal) != 0;
        emitter.IsSplat = (flags & EmitterFlagSplat) != 0;
        emitter.InheritPart = (flags & EmitterFlagInheritPart) != 0;
    }

    private void ParseReferenceNode(MdlReferenceNode reference, BinaryReader reader)
    {
        reference.RefModel = ReadFixedString(reader, 64);
        reference.Reattachable = reader.ReadUInt32() != 0;
    }
}
