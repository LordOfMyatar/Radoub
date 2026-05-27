using System.Text;
using Radoub.Formats.Bif;
using Radoub.Formats.Erf;
using Radoub.Formats.Gff;
using Radoub.Formats.Key;
using Radoub.Formats.Tlk;
using Xunit;

namespace Radoub.Formats.Tests;

/// <summary>
/// Red-phase tests for Radoub.Formats parser hardening (issue #2244).
/// Each finding has one test asserting the post-fix behavior. All currently FAIL.
/// Source: NonPublic/Radoub/Reviews/2026-05-25/radoub-formats.md (High).
/// </summary>
public class ParserHardeningTests
{
    // ---------------------------------------------------------------------
    // Finding 1: ErfReader.ExtractResource uint overflow before bounds check
    // (ErfReader.cs:321) — Offset + Size wraps in uint, allowing OOB read.
    // ---------------------------------------------------------------------
    [Fact]
    public void ExtractResource_OffsetPlusSizeWrapsUint_Throws()
    {
        var erfBuffer = new byte[256];
        var entry = new ErfResourceEntry
        {
            ResRef = "evil",
            ResourceType = 2029,
            Offset = 0xFFFFFFF0u, // near uint max
            Size = 0x20u          // Offset+Size = 0x10 in uint (wraps), but 0x100000010 in long
        };

        // Pre-fix: (uint)Offset + (uint)Size = 0x10, passes "<= erfBuffer.Length" → throws elsewhere (Array.Copy OOB) OR succeeds with garbage.
        // Post-fix: long-promoted compare detects overflow → InvalidDataException.
        Assert.Throws<InvalidDataException>(() => ErfReader.ExtractResource(erfBuffer, entry));
    }

    // ---------------------------------------------------------------------
    // Finding 2a: KeyReader.ReadBifEntries computes offset+(i*BifEntrySize)
    // in uint before casting to int (KeyReader.cs:73, 77). Must match the
    // long-arithmetic guard in ErfReader.ReadResources.
    // ---------------------------------------------------------------------
    [Fact]
    public void KeyReader_BifEntryOffsetOverflow_Throws()
    {
        // Build a 64-byte header claiming BIF table at uint offset 0xFFFFFFF0 with 4 entries.
        var buffer = new byte[64];
        Encoding.ASCII.GetBytes("KEY ").CopyTo(buffer, 0);
        Encoding.ASCII.GetBytes("V1  ").CopyTo(buffer, 4);
        BitConverter.GetBytes(4u).CopyTo(buffer, 8);          // bifCount = 4
        BitConverter.GetBytes(0u).CopyTo(buffer, 12);         // keyCount = 0
        BitConverter.GetBytes(0xFFFFFFF0u).CopyTo(buffer, 16); // fileTableOffset wraps when + entry stride
        BitConverter.GetBytes(0u).CopyTo(buffer, 20);

        // Pre-fix: (int)(uint + uint) can wrap to a small positive int that passes bounds check.
        // Post-fix: long arithmetic detects overflow → InvalidDataException.
        Assert.Throws<InvalidDataException>(() => KeyReader.Read(buffer));
    }

    // ---------------------------------------------------------------------
    // Finding 2b: KeyReader.ReadKeyEntries same issue (KeyReader.cs:110).
    // ---------------------------------------------------------------------
    [Fact]
    public void KeyReader_KeyEntryOffsetOverflow_Throws()
    {
        var buffer = new byte[64];
        Encoding.ASCII.GetBytes("KEY ").CopyTo(buffer, 0);
        Encoding.ASCII.GetBytes("V1  ").CopyTo(buffer, 4);
        BitConverter.GetBytes(0u).CopyTo(buffer, 8);
        BitConverter.GetBytes(4u).CopyTo(buffer, 12);          // keyCount = 4
        BitConverter.GetBytes(0u).CopyTo(buffer, 16);
        BitConverter.GetBytes(0xFFFFFFF0u).CopyTo(buffer, 20); // keyTableOffset wraps

        Assert.Throws<InvalidDataException>(() => KeyReader.Read(buffer));
    }

    // ---------------------------------------------------------------------
    // Finding 2c: BifFixedResource.TotalSize uses uint * uint silent wrap
    // (BifFile.cs:152). Must promote to ulong.
    // ---------------------------------------------------------------------
    [Fact]
    public void BifFixedResource_TotalSize_DoesNotWrap()
    {
        var fixedRes = new BifFixedResource
        {
            PartCount = 0x10000u, // 65536
            PartSize = 0x10000u   // 65536  → 2^32, wraps uint to 0
        };

        // Pre-fix: uint * uint = 0 (wraps)
        // Post-fix: promoted to ulong, expected = 4294967296
        Assert.Equal(0x100000000UL, fixedRes.TotalSize);
    }

    // ---------------------------------------------------------------------
    // Finding 3: ErfWriter.UpdateResource non-atomic rename
    // (ErfWriter.cs:210-219) — File.Delete then File.Move leaves a window
    // where the original ERF is gone. Post-fix: use File.Replace /
    // File.Move(overwrite:true) so the original is replaced atomically.
    //
    // The race window cannot be observed from a synchronous test without a
    // process-kill harness. This test pins the post-condition contract
    // (success path produces the new content, no temp file lingers) so
    // future refactors of the atomic write path stay correct end-to-end.
    // It will green even pre-fix — the actual atomicity fix is verified by
    // code inspection (Delete+Move → Move(overwrite:true)).
    // ---------------------------------------------------------------------
    [Fact]
    public void UpdateResource_PostConditionContract_HoldsAfterSuccess()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "RadoubHardening_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var erfPath = Path.Combine(tempDir, "test.mod");

            // Seed a minimal ERF with one resource so we have something to update.
            var erf = new ErfFile { FileType = "MOD ", FileVersion = "V1.0" };
            erf.Resources.Add(new ErfResourceEntry { ResRef = "seed", ResourceType = 2029, ResId = 0 });
            var seedData = new Dictionary<(string, ushort), byte[]>
            {
                { ("seed", 2029), new byte[] { 0xAA, 0xBB } }
            };
            ErfWriter.Write(erf, erfPath, seedData);

            // Update — must complete without leaving a .tmp file behind.
            ErfWriter.UpdateResource(erfPath, "seed", 2029, new byte[] { 0xCC, 0xDD }, createBackup: false);

            // Post-fix invariant: no leftover .tmp file (atomic Replace/Move-overwrite).
            Assert.False(File.Exists(erfPath + ".tmp"), "Temp file must not linger after atomic update.");

            // Original must still exist with new content.
            Assert.True(File.Exists(erfPath));
            var roundtripped = ErfReader.Read(erfPath);
            var newBytes = ErfReader.ExtractResource(erfPath, roundtripped.Resources[0]);
            Assert.Equal(new byte[] { 0xCC, 0xDD }, newBytes);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }

    // ---------------------------------------------------------------------
    // Finding 4: ErfWriter renumbers ResId to sequential index (ErfWriter.cs:98)
    // — third-party ERFs with non-sequential ResIds are silently rewritten.
    // ---------------------------------------------------------------------
    [Fact]
    public void Writer_PreservesNonSequentialResId_OnRoundTrip()
    {
        var erf = new ErfFile { FileType = "ERF ", FileVersion = "V1.0" };
        erf.Resources.Add(new ErfResourceEntry { ResRef = "alpha", ResourceType = 2029, ResId = 100 });
        erf.Resources.Add(new ErfResourceEntry { ResRef = "beta",  ResourceType = 2029, ResId = 7 });
        erf.Resources.Add(new ErfResourceEntry { ResRef = "gamma", ResourceType = 2029, ResId = 42 });
        var data = new Dictionary<(string, ushort), byte[]>
        {
            { ("alpha", 2029), new byte[] { 0x01 } },
            { ("beta",  2029), new byte[] { 0x02 } },
            { ("gamma", 2029), new byte[] { 0x03 } },
        };

        using var ms = new MemoryStream();
        ErfWriter.Write(erf, ms, data);
        var roundtripped = ErfReader.Read(ms.ToArray());

        // Pre-fix: ResIds become 0, 1, 2 (index).
        // Post-fix: preserved as 100, 7, 42.
        Assert.Equal(100u, roundtripped.Resources[0].ResId);
        Assert.Equal(7u,   roundtripped.Resources[1].ResId);
        Assert.Equal(42u,  roundtripped.Resources[2].ResId);
    }

    // ---------------------------------------------------------------------
    // Finding 5: GffReader.ReadCExoLocString abandons remaining substrings
    // on a single oversized entry (GffReader.cs:447-450). Must skip the
    // bad entry only, not the entire string table.
    // ---------------------------------------------------------------------
    [Fact]
    public void ReadCExoLocString_OversizedEntry_DoesNotAbandonRemaining()
    {
        // Build a CExoLocString with [normal, oversized(>=65535), normal].
        // Pre-fix: hitting the oversized entry triggers `break` and abandons
        // entry 3. Post-fix: cap is raised; entry 2 is read normally and
        // entry 3 is still parsed.
        var goodText = Encoding.UTF8.GetBytes("ok");
        const int oversizedLen = 70000;
        var oversizedText = new byte[oversizedLen];
        for (int i = 0; i < oversizedLen; i++) oversizedText[i] = (byte)'A';
        var goodText3 = Encoding.UTF8.GetBytes("yes");

        var locBytes = new List<byte>();
        // TotalSize (placeholder, ignored by reader), StrRef = 0xFFFFFFFF, SubStringCount = 3
        locBytes.AddRange(BitConverter.GetBytes(0u));
        locBytes.AddRange(BitConverter.GetBytes(0xFFFFFFFFu));
        locBytes.AddRange(BitConverter.GetBytes(3u));

        // Substring 1
        locBytes.AddRange(BitConverter.GetBytes(0u));
        locBytes.AddRange(BitConverter.GetBytes((uint)goodText.Length));
        locBytes.AddRange(goodText);

        // Substring 2 — oversized but bytes present in buffer
        locBytes.AddRange(BitConverter.GetBytes(1u));
        locBytes.AddRange(BitConverter.GetBytes((uint)oversizedLen));
        locBytes.AddRange(oversizedText);

        // Substring 3
        locBytes.AddRange(BitConverter.GetBytes(2u));
        locBytes.AddRange(BitConverter.GetBytes((uint)goodText3.Length));
        locBytes.AddRange(goodText3);

        var locStringBytes = locBytes.ToArray();

        var gffBytes = BuildMinimalGffWithLocStringFieldData(locStringBytes);
        var gff = GffReader.Read(gffBytes);
        var field = gff.RootStruct.GetField("Name");
        Assert.NotNull(field);
        var locString = Assert.IsType<CExoLocString>(field.Value!);

        // Pre-fix: only languageId 0 present (entry 2 oversized → break).
        // Post-fix: all three present.
        Assert.True(locString.LocalizedStrings.ContainsKey(0u), "language 0 missing");
        Assert.True(locString.LocalizedStrings.ContainsKey(2u), "language 2 missing — oversized middle entry abandoned rest");
        Assert.Equal("ok", locString.LocalizedStrings[0u]);
        Assert.Equal("yes", locString.LocalizedStrings[2u]);
    }

    // ---------------------------------------------------------------------
    // Finding 6: GffFile.GetFieldValue<T> bare catch swallows everything
    // (GffFile.cs:114-149). Must narrow to OverflowException/InvalidCastException
    // (and log at WARN). We can't directly observe a log call from here without
    // wiring; instead, assert that an unrelated exception type (e.g. NullReferenceException
    // from a custom IConvertible) is NOT swallowed.
    // ---------------------------------------------------------------------
    [Fact]
    public void GetFieldValue_DoesNotSwallowUnrelatedExceptions()
    {
        var s = new GffStruct();
        s.Fields.Add(new GffField
        {
            Label = "Bad",
            Type = GffField.DWORD,
            Value = new ThrowingConvertible() // throws ApplicationException on any Convert.ToXxx
        });

        // Pre-fix: bare catch{} swallows the ApplicationException → returns default(int) = 0.
        // Post-fix: only OverflowException / InvalidCastException are caught; this throws.
        Assert.Throws<ApplicationException>(() => s.GetFieldValue<int>("Bad"));
    }

    // ---------------------------------------------------------------------
    // Finding 7: TlkReader.CleanResRef strips mid-string whitespace
    // (TlkReader.cs:138-158). neverwinter.nim stops at first null and keeps
    // the remainder verbatim. Whitespace inside the name should be preserved
    // up to the first null (asymmetric with TlkWriter, which only null-pads).
    // ---------------------------------------------------------------------
    [Fact]
    public void TlkReader_SoundResRef_DoesNotStripInternalWhitespace()
    {
        // Build a single-entry TLK with SoundResRef = "a b\0\0\0..." (space, then null pad).
        // Pre-fix: stripped to "ab".
        // Post-fix: preserved as "a b" (read to first null).
        var resRef = "a b";
        var buffer = BuildMinimalTlkWithSoundResRef(resRef);

        var tlk = TlkReader.Read(buffer);
        Assert.Single(tlk.Entries);
        Assert.Equal("a b", tlk.Entries[0].SoundResRef);
    }

    // ---------------------------------------------------------------------
    // Finding 8: TlkReader unreachable UTF-8 fallback (TlkReader.cs:114-128).
    // Encoding.GetEncoding(1252).GetString cannot throw for valid byte ranges;
    // the catch is dead. We test the *positive* behavior — CP-1252 bytes decode
    // correctly — and document via comment that the try/catch is dead. The
    // post-fix removes the try/catch but preserves behavior.
    // ---------------------------------------------------------------------
    [Fact]
    public void TlkReader_HighBytes_DecodeAsWindows1252()
    {
        // CP-1252 byte 0xE9 = 'é'; UTF-8 would mojibake. Confirm CP-1252 decoding works
        // and that removing the unreachable fallback doesn't regress.
        var textBytes = new byte[] { 0xE9, 0x6C, 0xE8, 0x76, 0x65 }; // "élève"
        var buffer = BuildMinimalTlkWithText(textBytes);

        var tlk = TlkReader.Read(buffer);
        Assert.Single(tlk.Entries);
        Assert.Equal("élève", tlk.Entries[0].Text);
    }

    // =====================================================================
    // Helpers
    // =====================================================================

    private class ThrowingConvertible : IConvertible
    {
        public TypeCode GetTypeCode() => TypeCode.Object;
        public bool ToBoolean(IFormatProvider? p) => throw new ApplicationException();
        public byte ToByte(IFormatProvider? p) => throw new ApplicationException();
        public char ToChar(IFormatProvider? p) => throw new ApplicationException();
        public DateTime ToDateTime(IFormatProvider? p) => throw new ApplicationException();
        public decimal ToDecimal(IFormatProvider? p) => throw new ApplicationException();
        public double ToDouble(IFormatProvider? p) => throw new ApplicationException();
        public short ToInt16(IFormatProvider? p) => throw new ApplicationException();
        public int ToInt32(IFormatProvider? p) => throw new ApplicationException();
        public long ToInt64(IFormatProvider? p) => throw new ApplicationException();
        public sbyte ToSByte(IFormatProvider? p) => throw new ApplicationException();
        public float ToSingle(IFormatProvider? p) => throw new ApplicationException();
        public string ToString(IFormatProvider? p) => throw new ApplicationException();
        public object ToType(Type t, IFormatProvider? p) => throw new ApplicationException();
        public ushort ToUInt16(IFormatProvider? p) => throw new ApplicationException();
        public uint ToUInt32(IFormatProvider? p) => throw new ApplicationException();
        public ulong ToUInt64(IFormatProvider? p) => throw new ApplicationException();
    }

    /// <summary>
    /// Build a minimal GFF (V3.2) file with one root struct holding a single
    /// CExoLocString field named "Name" whose FieldData is the supplied bytes.
    /// </summary>
    private static byte[] BuildMinimalGffWithLocStringFieldData(byte[] locStringFieldData)
    {
        // Sections: Header (56) | Structs | Fields | Labels | FieldData | FieldIndices | ListIndices
        // 1 struct, 1 field, 1 label, locStringFieldData.Length bytes of FieldData, 0 indices.
        const int headerSize = 56;
        const int structSize = 12;
        const int fieldSize = 12;
        const int labelSize = 16;

        int structOffset = headerSize;
        int fieldOffset = structOffset + structSize;
        int labelOffset = fieldOffset + fieldSize;
        int fieldDataOffset = labelOffset + labelSize;
        int fieldIndicesOffset = fieldDataOffset + locStringFieldData.Length;
        int listIndicesOffset = fieldIndicesOffset;
        int totalSize = listIndicesOffset;

        var buffer = new byte[totalSize];

        // Header
        Encoding.ASCII.GetBytes("TST ").CopyTo(buffer, 0);
        Encoding.ASCII.GetBytes("V3.2").CopyTo(buffer, 4);
        BitConverter.GetBytes((uint)structOffset).CopyTo(buffer, 8);
        BitConverter.GetBytes(1u).CopyTo(buffer, 12);
        BitConverter.GetBytes((uint)fieldOffset).CopyTo(buffer, 16);
        BitConverter.GetBytes(1u).CopyTo(buffer, 20);
        BitConverter.GetBytes((uint)labelOffset).CopyTo(buffer, 24);
        BitConverter.GetBytes(1u).CopyTo(buffer, 28);
        BitConverter.GetBytes((uint)fieldDataOffset).CopyTo(buffer, 32);
        BitConverter.GetBytes((uint)locStringFieldData.Length).CopyTo(buffer, 36);
        BitConverter.GetBytes((uint)fieldIndicesOffset).CopyTo(buffer, 40);
        BitConverter.GetBytes(0u).CopyTo(buffer, 44);
        BitConverter.GetBytes((uint)listIndicesOffset).CopyTo(buffer, 48);
        BitConverter.GetBytes(0u).CopyTo(buffer, 52);

        // Struct: Type=0xFFFFFFFF (root), DataOrDataOffset=0 (direct field index since FieldCount=1), FieldCount=1
        BitConverter.GetBytes(0xFFFFFFFFu).CopyTo(buffer, structOffset);
        BitConverter.GetBytes(0u).CopyTo(buffer, structOffset + 4);
        BitConverter.GetBytes(1u).CopyTo(buffer, structOffset + 8);

        // Field: Type=CExoLocString (12), LabelIndex=0, DataOrDataOffset=0 (offset into FieldData)
        BitConverter.GetBytes((uint)GffField.CExoLocString).CopyTo(buffer, fieldOffset);
        BitConverter.GetBytes(0u).CopyTo(buffer, fieldOffset + 4);
        BitConverter.GetBytes(0u).CopyTo(buffer, fieldOffset + 8);

        // Label: "Name" padded to 16 bytes
        var labelBytes = new byte[16];
        Encoding.ASCII.GetBytes("Name").CopyTo(labelBytes, 0);
        labelBytes.CopyTo(buffer, labelOffset);

        // FieldData: the supplied locString bytes
        locStringFieldData.CopyTo(buffer, fieldDataOffset);

        return buffer;
    }

    private static byte[] BuildMinimalTlkWithSoundResRef(string resRef)
    {
        // Header(20) + 1 Entry(40) + string data block (empty).
        const int headerSize = 20;
        const int entrySize = 40;
        int totalSize = headerSize + entrySize;
        var buffer = new byte[totalSize];

        Encoding.ASCII.GetBytes("TLK V3.0").CopyTo(buffer, 0);
        BitConverter.GetBytes(0u).CopyTo(buffer, 8);            // languageId
        BitConverter.GetBytes(1u).CopyTo(buffer, 12);           // entryCount
        BitConverter.GetBytes((uint)totalSize).CopyTo(buffer, 16); // stringDataOffset (no text)

        // Entry: Flags = 2 (TEXT_PRESENT off intentionally — we're only testing SoundResRef.
        // Use 0 so no text branch runs.)
        BitConverter.GetBytes(0u).CopyTo(buffer, headerSize + 0);
        var resRefBytes = new byte[16];
        Encoding.ASCII.GetBytes(resRef).CopyTo(resRefBytes, 0);
        // Remaining bytes already null
        resRefBytes.CopyTo(buffer, headerSize + 4);
        // VolumeVariance, PitchVariance, StringOffset, StringLength, SoundLength: zero
        return buffer;
    }

    private static byte[] BuildMinimalTlkWithText(byte[] textBytes)
    {
        // Header(20) + 1 Entry(40) + textBytes.
        const int headerSize = 20;
        const int entrySize = 40;
        int stringDataOffset = headerSize + entrySize;
        int totalSize = stringDataOffset + textBytes.Length;
        var buffer = new byte[totalSize];

        Encoding.ASCII.GetBytes("TLK V3.0").CopyTo(buffer, 0);
        BitConverter.GetBytes(0u).CopyTo(buffer, 8);
        BitConverter.GetBytes(1u).CopyTo(buffer, 12);
        BitConverter.GetBytes((uint)stringDataOffset).CopyTo(buffer, 16);

        // Entry: Flags = 1 (TEXT_PRESENT) so HasText is true.
        BitConverter.GetBytes(1u).CopyTo(buffer, headerSize + 0);
        // SoundResRef zero
        BitConverter.GetBytes(0u).CopyTo(buffer, headerSize + 20); // VolumeVariance
        BitConverter.GetBytes(0u).CopyTo(buffer, headerSize + 24); // PitchVariance
        BitConverter.GetBytes(0u).CopyTo(buffer, headerSize + 28); // StringOffset
        BitConverter.GetBytes((uint)textBytes.Length).CopyTo(buffer, headerSize + 32);
        BitConverter.GetBytes(0f).CopyTo(buffer, headerSize + 36); // SoundLength

        textBytes.CopyTo(buffer, stringDataOffset);
        return buffer;
    }
}
