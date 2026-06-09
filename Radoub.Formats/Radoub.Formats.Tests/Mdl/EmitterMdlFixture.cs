using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;

namespace Radoub.Formats.Tests.Mdl;

/// <summary>
/// Builds a minimal, byte-valid binary NWN MDL file in memory whose geometry root
/// contains exactly one emitter child node. Used by emitter-parser unit tests (#2395).
///
/// The fixture writes the AUTHORITATIVE emitter struct layout (texture = 64 bytes),
/// not the current parser's (buggy) 32-byte texture read. A later task fixes the
/// parser; this fixture is the ground truth it must read correctly.
///
/// Layout (pointerBase = 0, so written pointers equal model-data offsets):
///   [0x000] model/geometry header (0xE8 bytes)
///   [0x0E8] emitter node      = 0x70 node header + 0xD8 emitter struct (0x148 total)
///   [....]  controller key block (12 bytes / record)
///   [....]  controller data block (packed floats)
/// File header (12 bytes) is prepended: uint zeroField=0, uint modelDataSize, uint rawDataSize=0.
/// </summary>
public static class EmitterMdlFixture
{
    // File header
    private const int FileHeaderSize = 12;

    // Region sizes (must match MdlBinaryReader constants)
    private const int ModelHeaderSize = 0xE8;   // reserved before first node
    private const int NodeHeaderSize = 0x70;
    private const int EmitterStructSize = 0xD8;  // 0x148 - 0x70
    private const int EmitterNodeSize = NodeHeaderSize + EmitterStructSize; // 0x148
    private const int ControllerKeySize = 0x0C;

    private const uint NodeFlagHasEmitter = 0x00000004;

    // Model-data offsets
    private const uint GeometryHeaderOffset = 0x000;
    private const uint EmitterNodeOffset = ModelHeaderSize;                 // 0x0E8
    private static uint ControllerKeyOffset => EmitterNodeOffset + EmitterNodeSize; // after emitter node

    // Controller integer IDs (column counts: scalar = 1, color = 3).
    private const int CtrlAlphaEnd = 80;
    private const int CtrlAlphaStart = 84;
    private const int CtrlBirthrate = 88;
    private const int CtrlColorEnd = 96;
    private const int CtrlColorStart = 108;
    private const int CtrlDrag = 124;
    private const int CtrlGrav = 140;
    private const int CtrlLifeExp = 144;
    private const int CtrlMass = 148;
    private const int CtrlRandVel = 164;
    private const int CtrlSizeStart = 168;
    private const int CtrlSizeEnd = 172;
    private const int CtrlSpread = 184;
    private const int CtrlVelocity = 192;
    private const int CtrlAlphaMid = 464;
    private const int CtrlColorMid = 468;
    private const int CtrlPercentStart = 480;
    private const int CtrlPercentMid = 481;
    private const int CtrlPercentEnd = 482;
    private const int CtrlSizeMid = 484;

    private sealed record ControllerRecord(int Type, byte Columns, float[] Values);

    /// <summary>
    /// Build a binary MDL containing a single emitter node. All parameters are optional;
    /// a controller record is emitted only for parameters the caller actually sets.
    /// </summary>
    public static byte[] BuildSingleEmitter(
        // scalar controllers
        float? birthrate = null,
        float? lifeExp = null,
        float? velocity = null,
        float? randvel = null,
        float? spread = null,
        float? mass = null,
        float? grav = null,
        float? drag = null,
        float? sizeStart = null,
        float? sizeMid = null,
        float? sizeEnd = null,
        float? alphaStart = null,
        float? alphaMid = null,
        float? alphaEnd = null,
        float? percentStart = null,
        float? percentMid = null,
        float? percentEnd = null,
        // color controllers (RGB)
        Vector3? colorStart = null,
        Vector3? colorMid = null,
        Vector3? colorEnd = null,
        // string / grid fields
        string update = "Fountain",
        string render = "Normal",
        string blend = "Normal",
        string texture = "fx_tex",
        int xgrid = 1,
        int ygrid = 1,
        // post-texture fields (guard against texture-width misalignment)
        bool loop = false,
        int renderOrder = 0)
    {
        var controllers = new List<ControllerRecord>();

        void AddScalar(float? value, int type)
        {
            if (value.HasValue)
                controllers.Add(new ControllerRecord(type, 1, new[] { value.Value }));
        }

        void AddColor(Vector3? value, int type)
        {
            if (value.HasValue)
                controllers.Add(new ControllerRecord(type, 3,
                    new[] { value.Value.X, value.Value.Y, value.Value.Z }));
        }

        AddScalar(birthrate, CtrlBirthrate);
        AddScalar(lifeExp, CtrlLifeExp);
        AddScalar(velocity, CtrlVelocity);
        AddScalar(randvel, CtrlRandVel);
        AddScalar(spread, CtrlSpread);
        AddScalar(mass, CtrlMass);
        AddScalar(grav, CtrlGrav);
        AddScalar(drag, CtrlDrag);
        AddScalar(sizeStart, CtrlSizeStart);
        AddScalar(sizeMid, CtrlSizeMid);
        AddScalar(sizeEnd, CtrlSizeEnd);
        AddScalar(alphaStart, CtrlAlphaStart);
        AddScalar(alphaMid, CtrlAlphaMid);
        AddScalar(alphaEnd, CtrlAlphaEnd);
        AddScalar(percentStart, CtrlPercentStart);
        AddScalar(percentMid, CtrlPercentMid);
        AddScalar(percentEnd, CtrlPercentEnd);
        AddColor(colorStart, CtrlColorStart);
        AddColor(colorMid, CtrlColorMid);
        AddColor(colorEnd, CtrlColorEnd);

        // Controller data block holds: a shared single-row time stream (1 float = 0.0)
        // followed by each record's packed values. keyDataOffset points all records at
        // the shared time float (index 0); valueDataOffset is per-record.
        var dataFloats = new List<float> { 0.0f }; // time[0]
        var valueFloatIndex = new int[controllers.Count];
        for (int i = 0; i < controllers.Count; i++)
        {
            valueFloatIndex[i] = dataFloats.Count;
            dataFloats.AddRange(controllers[i].Values);
        }

        uint controllerDataOffset = ControllerKeyOffset + (uint)(controllers.Count * ControllerKeySize);
        int modelDataSize = (int)controllerDataOffset + dataFloats.Count * 4;

        var modelData = new byte[modelDataSize];

        // ---- Model / geometry header ----
        WriteUInt32(modelData, 0x00, 0);                       // pointerBase = 0
        WriteFixedString(modelData, 0x08, "fxtest", 64);       // model name
        WriteUInt32(modelData, 0x48, EmitterNodeOffset);       // root node pointer
        WriteUInt32(modelData, 0x4C, 1);                       // node count
        // 0x70: flags(u16)=0, class(byte)=0, fog(byte)=0, refcount(u32)=0 — already zero.
        // anim arrays / supermodel pointer / bounding / radius / scale / supermodel string
        // all left zero; the reader tolerates zero anim count and empty supermodel.

        // ---- Emitter node header (0x70) ----
        int n = (int)EmitterNodeOffset;
        // +0..+24 function pointers = 0
        WriteUInt32(modelData, n + 24, 0);                     // inheritColor
        WriteInt32(modelData, n + 28, 0);                      // partNumber
        WriteFixedString(modelData, n + 32, "emitter01", 32);  // node name
        WriteUInt32(modelData, n + 0x40, 0);                   // geometry header ptr
        WriteUInt32(modelData, n + 0x44, 0);                   // parent ptr
        WriteUInt32(modelData, n + 0x48, 0);                   // child array ptr (none)
        WriteUInt32(modelData, n + 0x4C, 0);                   // child count
        WriteUInt32(modelData, n + 0x50, 0);                   // child allocated
        WriteUInt32(modelData, n + 0x54, controllers.Count > 0 ? ControllerKeyOffset : 0); // ctrl key ptr
        WriteUInt32(modelData, n + 0x58, (uint)controllers.Count);                          // ctrl key count
        WriteUInt32(modelData, n + 0x5C, (uint)controllers.Count);                          // ctrl key alloc
        WriteUInt32(modelData, n + 0x60, controllers.Count > 0 ? controllerDataOffset : 0); // ctrl data ptr
        WriteUInt32(modelData, n + 0x64, (uint)dataFloats.Count);                           // ctrl data count
        WriteUInt32(modelData, n + 0x68, (uint)dataFloats.Count);                           // ctrl data alloc
        WriteUInt32(modelData, n + 0x6C, NodeFlagHasEmitter);  // node flags

        // ---- Emitter struct (offsets relative to node start) ----
        WriteSingle(modelData, n + 0x70, 0.0f);                // deadSpace
        WriteSingle(modelData, n + 0x74, 0.0f);                // blastRadius
        WriteSingle(modelData, n + 0x78, 0.0f);                // blastLength
        WriteUInt32(modelData, n + 0x7C, (uint)xgrid);         // xGrid
        WriteUInt32(modelData, n + 0x80, (uint)ygrid);         // yGrid
        WriteUInt32(modelData, n + 0x84, 0);                   // spawnType
        WriteFixedString(modelData, n + 0x88, update, 32);     // update[32]
        WriteFixedString(modelData, n + 0xA8, render, 32);     // render[32]
        WriteFixedString(modelData, n + 0xC8, blend, 32);      // blend[32]
        WriteFixedString(modelData, n + 0xE8, texture, 64);    // texture[64]  (AUTHORITATIVE width)
        WriteFixedString(modelData, n + 0x128, string.Empty, 16); // chunkName[16]
        WriteUInt32(modelData, n + 0x138, 0);                  // twoSidedTex
        WriteUInt32(modelData, n + 0x13C, loop ? 1u : 0u);     // loop
        WriteUInt16(modelData, n + 0x140, (ushort)renderOrder);// renderOrder
        WriteUInt16(modelData, n + 0x142, 0);                  // pad
        WriteUInt32(modelData, n + 0x144, 0);                  // emitterFlags
        // ends at n + 0x148

        // ---- Controller key block ----
        for (int i = 0; i < controllers.Count; i++)
        {
            int keyPos = (int)ControllerKeyOffset + i * ControllerKeySize;
            var c = controllers[i];
            WriteInt32(modelData, keyPos + 0, c.Type);          // type
            WriteInt16(modelData, keyPos + 4, 1);               // rows
            WriteInt16(modelData, keyPos + 6, 0);               // keyDataOffset (-> shared time float)
            WriteInt16(modelData, keyPos + 8, (short)valueFloatIndex[i]); // valueDataOffset
            modelData[keyPos + 10] = c.Columns;                 // columns
            modelData[keyPos + 11] = 0;                         // pad
        }

        // ---- Controller data block ----
        for (int i = 0; i < dataFloats.Count; i++)
        {
            WriteSingle(modelData, (int)controllerDataOffset + i * 4, dataFloats[i]);
        }

        // ---- Prepend file header (12 bytes) ----
        var file = new byte[FileHeaderSize + modelData.Length];
        WriteUInt32(file, 0, 0);                                // zeroField
        WriteUInt32(file, 4, (uint)modelData.Length);           // model data size
        WriteUInt32(file, 8, 0);                                // raw data size
        Array.Copy(modelData, 0, file, FileHeaderSize, modelData.Length);

        return file;
    }

    private static void WriteUInt32(byte[] buf, int offset, uint value) =>
        BitConverter.GetBytes(value).CopyTo(buf, offset);

    private static void WriteInt32(byte[] buf, int offset, int value) =>
        BitConverter.GetBytes(value).CopyTo(buf, offset);

    private static void WriteUInt16(byte[] buf, int offset, ushort value) =>
        BitConverter.GetBytes(value).CopyTo(buf, offset);

    private static void WriteInt16(byte[] buf, int offset, short value) =>
        BitConverter.GetBytes(value).CopyTo(buf, offset);

    private static void WriteSingle(byte[] buf, int offset, float value) =>
        BitConverter.GetBytes(value).CopyTo(buf, offset);

    private static void WriteFixedString(byte[] buf, int offset, string value, int length)
    {
        var bytes = Encoding.ASCII.GetBytes(value ?? string.Empty);
        int count = Math.Min(bytes.Length, length - 1); // leave room for null terminator
        Array.Copy(bytes, 0, buf, offset, count);
        // remaining bytes stay zero (null-padded)
    }
}
