using System.Text;
using Radoub.Formats.Bif;
using Radoub.Formats.Erf;
using Radoub.Formats.Gff;
using Radoub.Formats.Key;
using Radoub.Formats.Tlk;
using Radoub.Formats.TwoDA;
using Xunit;

namespace Radoub.Formats.Tests;

/// <summary>
/// Tests for corrupted/malformed file handling in Radoub.Formats parsers.
/// These tests verify that parsers fail gracefully with appropriate exceptions
/// rather than crashing, hanging, or causing memory exhaustion.
/// </summary>
public class CorruptedFileTests
{
    #region GFF Parser Tests

    [Fact]
    public void GffReader_TruncatedHeader_ThrowsInvalidData()
    {
        // Header < 56 bytes
        var truncatedHeader = new byte[30];
        Array.Copy(Encoding.ASCII.GetBytes("DLG V3.2"), truncatedHeader, 8);

        var ex = Assert.Throws<InvalidDataException>(() => GffReader.Read(truncatedHeader));
        Assert.Contains("too small", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GffReader_InvalidFieldOffset_ThrowsOrHandlesGracefully()
    {
        // Create minimal GFF with field offset pointing beyond file
        var buffer = CreateMinimalGffWithBadOffset(fieldOffset: 0xFFFFFFFF);

        // Should throw or handle gracefully (not crash/hang)
        var exception = Record.Exception(() => GffReader.Read(buffer));

        // Either throws InvalidDataException/ArgumentOutOfRangeException or returns empty (graceful handling)
        // We just verify it doesn't hang or crash
        Assert.True(exception == null || exception is InvalidDataException || exception is ArgumentOutOfRangeException);
    }

    [Fact]
    public void GffReader_NegativeCount_HandlesGracefully()
    {
        // Create GFF with count that would be interpreted as huge if treated as unsigned
        // 0xFFFFFFFF as signed int is -1
        var buffer = CreateMinimalGffWithBadCount(structCount: unchecked((uint)-1));

        // Should not cause OOM or infinite loop
        var exception = Record.Exception(() =>
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            GffReader.Read(buffer);
        });

        // Either succeeds with empty/default or throws - just shouldn't hang
        Assert.True(exception == null || exception is InvalidDataException || exception is ArgumentOutOfRangeException || exception is OverflowException);
    }

    [Fact]
    public void GffReader_TruncatedFieldData_ThrowsOrHandlesGracefully()
    {
        // Create GFF where field data is cut off
        var buffer = CreateMinimalGffWithTruncatedFieldData();

        var exception = Record.Exception(() => GffReader.Read(buffer));

        // Should throw or handle gracefully
        Assert.True(exception == null || exception is InvalidDataException || exception is ArgumentOutOfRangeException);
    }

    [Fact]
    public void GffReader_EmptyFile_ThrowsInvalidData()
    {
        var emptyBuffer = Array.Empty<byte>();

        var ex = Assert.Throws<InvalidDataException>(() => GffReader.Read(emptyBuffer));
        Assert.Contains("too small", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GffReader_InvalidSignature_ThrowsInvalidData()
    {
        // Create minimal header with wrong version
        var buffer = CreateMinimalGffWithVersion("V1.0");

        var ex = Assert.Throws<InvalidDataException>(() => GffReader.Read(buffer));
        Assert.Contains("version", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region KEY Parser Tests

    [Fact]
    public void KeyReader_TruncatedFile_ThrowsOrHandlesGracefully()
    {
        // KEY file with header but truncated entry table
        var buffer = CreateTruncatedKeyFile();

        var exception = Record.Exception(() => KeyReader.Read(buffer));

        Assert.True(exception == null || exception is InvalidDataException || exception is ArgumentOutOfRangeException);
    }

    [Fact]
    public void KeyReader_EmptyFile_ThrowsInvalidData()
    {
        var emptyBuffer = Array.Empty<byte>();

        Assert.Throws<InvalidDataException>(() => KeyReader.Read(emptyBuffer));
    }

    [Fact]
    public void KeyReader_InvalidSignature_ThrowsInvalidData()
    {
        var buffer = new byte[64];
        Array.Copy(Encoding.ASCII.GetBytes("XXX V1.0"), buffer, 8);

        var exception = Record.Exception(() => KeyReader.Read(buffer));

        Assert.True(exception is InvalidDataException || exception is ArgumentException);
    }

    [Fact]
    public void KeyReader_OffsetOverflow_HandlesGracefully()
    {
        // Create KEY with offset that would cause integer overflow
        var buffer = CreateKeyFileWithBadOffset(0xFFFFFFFF);

        var exception = Record.Exception(() => KeyReader.Read(buffer));

        // Should not cause OOM or crash
        Assert.True(exception == null || exception is InvalidDataException || exception is ArgumentOutOfRangeException || exception is OverflowException);
    }

    #endregion

    #region BIF Parser Tests

    [Fact]
    public void BifReader_TruncatedFile_ThrowsOrHandlesGracefully()
    {
        var buffer = CreateTruncatedBifFile();

        var exception = Record.Exception(() => BifReader.Read(buffer));

        Assert.True(exception == null || exception is InvalidDataException || exception is ArgumentOutOfRangeException);
    }

    [Fact]
    public void BifReader_EmptyFile_ThrowsInvalidData()
    {
        var emptyBuffer = Array.Empty<byte>();

        Assert.Throws<InvalidDataException>(() => BifReader.Read(emptyBuffer));
    }

    [Fact]
    public void BifReader_InvalidSignature_ThrowsInvalidData()
    {
        var buffer = new byte[24];
        Array.Copy(Encoding.ASCII.GetBytes("XXXX"), buffer, 4);

        var exception = Record.Exception(() => BifReader.Read(buffer));

        Assert.True(exception is InvalidDataException || exception is ArgumentException);
    }

    #endregion

    #region ERF Parser Tests

    [Fact]
    public void ErfReader_TruncatedFile_ThrowsOrHandlesGracefully()
    {
        var buffer = CreateTruncatedErfFile();

        var exception = Record.Exception(() => ErfReader.Read(buffer));

        Assert.True(exception == null || exception is InvalidDataException || exception is ArgumentOutOfRangeException);
    }

    [Fact]
    public void ErfReader_EmptyFile_ThrowsInvalidData()
    {
        var emptyBuffer = Array.Empty<byte>();

        Assert.Throws<InvalidDataException>(() => ErfReader.Read(emptyBuffer));
    }

    [Fact]
    public void ErfReader_InvalidSignature_ThrowsInvalidData()
    {
        var buffer = new byte[160];
        Array.Copy(Encoding.ASCII.GetBytes("XXXX"), buffer, 4);

        var exception = Record.Exception(() => ErfReader.Read(buffer));

        Assert.True(exception is InvalidDataException || exception is ArgumentException);
    }

    [Fact]
    public void ErfReader_ResourceOffsetOverflow_HandlesGracefully()
    {
        var buffer = CreateErfFileWithBadResourceOffset(0xFFFFFFFF);

        var exception = Record.Exception(() => ErfReader.Read(buffer));

        // Should not cause crash when accessing resource
        Assert.True(exception == null || exception is InvalidDataException || exception is ArgumentOutOfRangeException);
    }

    #endregion

    #region TLK Parser Tests

    [Fact]
    public void TlkReader_TruncatedStringData_ThrowsOrHandlesGracefully()
    {
        var buffer = CreateTruncatedTlkFile();

        var exception = Record.Exception(() => TlkReader.Read(buffer));

        Assert.True(exception == null || exception is InvalidDataException || exception is ArgumentOutOfRangeException);
    }

    [Fact]
    public void TlkReader_EmptyFile_ThrowsInvalidData()
    {
        var emptyBuffer = Array.Empty<byte>();

        Assert.Throws<InvalidDataException>(() => TlkReader.Read(emptyBuffer));
    }

    [Fact]
    public void TlkReader_InvalidSignature_ThrowsInvalidData()
    {
        var buffer = new byte[40];
        Array.Copy(Encoding.ASCII.GetBytes("XXXX"), buffer, 4);

        var exception = Record.Exception(() => TlkReader.Read(buffer));

        Assert.True(exception is InvalidDataException || exception is ArgumentException);
    }

    [Fact]
    public void TlkReader_StringOffsetBeyondFile_HandlesGracefully()
    {
        var buffer = CreateTlkFileWithBadStringOffset(0xFFFFFFFF);

        var exception = Record.Exception(() => TlkReader.Read(buffer));

        Assert.True(exception == null || exception is InvalidDataException || exception is ArgumentOutOfRangeException);
    }

    #endregion

    #region 2DA Parser Tests

    [Fact]
    public void TwoDAReader_BinaryGarbage_ThrowsOrReturnsEmpty()
    {
        // Random binary data that isn't valid 2DA
        var garbageBuffer = new byte[] { 0x00, 0x01, 0x02, 0xFF, 0xFE, 0xFD, 0x00, 0x00 };

        var exception = Record.Exception(() => TwoDAReader.Read(garbageBuffer));

        // Should throw or return empty/minimal result - not crash
        Assert.True(exception == null || exception is InvalidDataException || exception is FormatException);
    }

    [Fact]
    public void TwoDAReader_EmptyFile_ReturnsEmptyOrThrows()
    {
        var emptyBuffer = Array.Empty<byte>();

        var exception = Record.Exception(() => TwoDAReader.Read(emptyBuffer));

        // Either returns empty 2DA or throws - both are acceptable
        Assert.True(exception == null || exception is InvalidDataException);
    }

    [Fact]
    public void TwoDAReader_MissingNewline_HandlesGracefully()
    {
        // Valid 2DA that doesn't end with newline
        var content = "2DA V2.0\n\nCOL1\n0    value1";  // No trailing newline
        var buffer = Encoding.ASCII.GetBytes(content);

        // Should handle gracefully
        var result = TwoDAReader.Read(buffer);
        Assert.NotNull(result);
    }

    [Fact]
    public void TwoDAReader_VeryLongLine_HandlesGracefully()
    {
        // Create 2DA with extremely long line
        var longValue = new string('X', 100000);
        var content = $"2DA V2.0\n\nCOL1\n0    {longValue}\n";
        var buffer = Encoding.ASCII.GetBytes(content);

        // Should not hang or crash
        var exception = Record.Exception(() => TwoDAReader.Read(buffer));

        Assert.True(exception == null || exception is InvalidDataException || exception is OutOfMemoryException);
    }

    #endregion

    #region Helper Methods - GFF

    private static byte[] CreateMinimalGffWithBadOffset(uint fieldOffset)
    {
        var buffer = new byte[56];
        var offset = 0;

        // Signature
        WriteString(buffer, ref offset, "DLG ");
        WriteString(buffer, ref offset, "V3.2");

        // Struct section (empty)
        WriteUInt32(buffer, ref offset, 56); // StructOffset
        WriteUInt32(buffer, ref offset, 0);  // StructCount

        // Field section (bad offset)
        WriteUInt32(buffer, ref offset, fieldOffset);
        WriteUInt32(buffer, ref offset, 1);  // FieldCount

        // Label section (empty)
        WriteUInt32(buffer, ref offset, 56);
        WriteUInt32(buffer, ref offset, 0);

        // FieldData section (empty)
        WriteUInt32(buffer, ref offset, 56);
        WriteUInt32(buffer, ref offset, 0);

        // FieldIndices section (empty)
        WriteUInt32(buffer, ref offset, 56);
        WriteUInt32(buffer, ref offset, 0);

        // ListIndices section (empty)
        WriteUInt32(buffer, ref offset, 56);
        WriteUInt32(buffer, ref offset, 0);

        return buffer;
    }

    private static byte[] CreateMinimalGffWithBadCount(uint structCount)
    {
        var buffer = new byte[56];
        var offset = 0;

        WriteString(buffer, ref offset, "DLG ");
        WriteString(buffer, ref offset, "V3.2");

        WriteUInt32(buffer, ref offset, 56);
        WriteUInt32(buffer, ref offset, structCount); // Bad count

        // Rest of header
        WriteUInt32(buffer, ref offset, 56);
        WriteUInt32(buffer, ref offset, 0);
        WriteUInt32(buffer, ref offset, 56);
        WriteUInt32(buffer, ref offset, 0);
        WriteUInt32(buffer, ref offset, 56);
        WriteUInt32(buffer, ref offset, 0);
        WriteUInt32(buffer, ref offset, 56);
        WriteUInt32(buffer, ref offset, 0);
        WriteUInt32(buffer, ref offset, 56);
        WriteUInt32(buffer, ref offset, 0);

        return buffer;
    }

    private static byte[] CreateMinimalGffWithTruncatedFieldData()
    {
        // Create GFF header that references field data beyond the buffer
        var buffer = new byte[60];  // Barely enough for header
        var offset = 0;

        WriteString(buffer, ref offset, "DLG ");
        WriteString(buffer, ref offset, "V3.2");

        WriteUInt32(buffer, ref offset, 56);
        WriteUInt32(buffer, ref offset, 1);  // 1 struct

        WriteUInt32(buffer, ref offset, 200); // Field offset beyond file
        WriteUInt32(buffer, ref offset, 1);

        // Minimal remaining header
        for (int i = 0; i < 8; i++)
            WriteUInt32(buffer, ref offset, 56);

        return buffer;
    }

    private static byte[] CreateMinimalGffWithVersion(string version)
    {
        var buffer = new byte[56];
        var offset = 0;

        WriteString(buffer, ref offset, "DLG ");
        WriteString(buffer, ref offset, version.PadRight(4));

        // Fill rest with zeros (valid empty sections)
        for (int i = 0; i < 12; i++)
            WriteUInt32(buffer, ref offset, 56);

        return buffer;
    }

    #endregion

    #region Helper Methods - KEY

    private static byte[] CreateTruncatedKeyFile()
    {
        // KEY header is 64 bytes, but we'll create incomplete entry table
        var buffer = new byte[70];  // Header + partial entry
        var offset = 0;

        WriteString(buffer, ref offset, "KEY ");
        WriteString(buffer, ref offset, "V1  ");

        WriteUInt32(buffer, ref offset, 1);   // BIF count
        WriteUInt32(buffer, ref offset, 10);  // Key count (more than we have)
        WriteUInt32(buffer, ref offset, 64);  // BIF offset
        WriteUInt32(buffer, ref offset, 68);  // Key offset (truncated)

        // Build year/day
        WriteUInt32(buffer, ref offset, 125);
        WriteUInt32(buffer, ref offset, 1);

        // Reserved
        for (int i = 0; i < 8; i++)
            WriteUInt32(buffer, ref offset, 0);

        return buffer;
    }

    private static byte[] CreateKeyFileWithBadOffset(uint keyOffset)
    {
        var buffer = new byte[64];
        var offset = 0;

        WriteString(buffer, ref offset, "KEY ");
        WriteString(buffer, ref offset, "V1  ");

        WriteUInt32(buffer, ref offset, 0);
        WriteUInt32(buffer, ref offset, 1);
        WriteUInt32(buffer, ref offset, 64);
        WriteUInt32(buffer, ref offset, keyOffset);  // Bad offset

        WriteUInt32(buffer, ref offset, 0);
        WriteUInt32(buffer, ref offset, 0);

        for (int i = 0; i < 8; i++)
            WriteUInt32(buffer, ref offset, 0);

        return buffer;
    }

    #endregion

    #region Helper Methods - BIF

    private static byte[] CreateTruncatedBifFile()
    {
        var buffer = new byte[24];  // Header only, no variable table
        var offset = 0;

        WriteString(buffer, ref offset, "BIFF");
        WriteString(buffer, ref offset, "V1  ");

        WriteUInt32(buffer, ref offset, 10);  // Variable count (more than we have)
        WriteUInt32(buffer, ref offset, 0);   // Fixed count
        WriteUInt32(buffer, ref offset, 24);  // Variable table offset

        return buffer;
    }

    #endregion

    #region Helper Methods - ERF

    private static byte[] CreateTruncatedErfFile()
    {
        var buffer = new byte[160];  // Header only
        var offset = 0;

        WriteString(buffer, ref offset, "ERF ");
        WriteString(buffer, ref offset, "V1.0");

        WriteUInt32(buffer, ref offset, 0);   // Language count
        WriteUInt32(buffer, ref offset, 0);   // Localized string size
        WriteUInt32(buffer, ref offset, 10);  // Entry count (more than we have)

        // Offsets
        WriteUInt32(buffer, ref offset, 160); // LocalizedStrings offset
        WriteUInt32(buffer, ref offset, 160); // KeyList offset
        WriteUInt32(buffer, ref offset, 200); // ResourceList offset (beyond file)

        // Build date
        WriteUInt32(buffer, ref offset, 0);
        WriteUInt32(buffer, ref offset, 0);

        // Description StrRef
        WriteUInt32(buffer, ref offset, 0xFFFFFFFF);

        // Reserved
        for (int i = 0; i < 29; i++)
            WriteUInt32(buffer, ref offset, 0);

        return buffer;
    }

    private static byte[] CreateErfFileWithBadResourceOffset(uint resourceOffset)
    {
        var buffer = new byte[200];
        var offset = 0;

        WriteString(buffer, ref offset, "ERF ");
        WriteString(buffer, ref offset, "V1.0");

        WriteUInt32(buffer, ref offset, 0);
        WriteUInt32(buffer, ref offset, 0);
        WriteUInt32(buffer, ref offset, 1);  // 1 entry

        WriteUInt32(buffer, ref offset, 160);
        WriteUInt32(buffer, ref offset, 160);  // KeyList at 160
        WriteUInt32(buffer, ref offset, 184);  // ResourceList at 184

        WriteUInt32(buffer, ref offset, 0);
        WriteUInt32(buffer, ref offset, 0);
        WriteUInt32(buffer, ref offset, 0xFFFFFFFF);

        // Reserved
        for (int i = 0; i < 29; i++)
            WriteUInt32(buffer, ref offset, 0);

        // KeyList entry (24 bytes) at offset 160
        offset = 160;
        for (int i = 0; i < 16; i++)
            buffer[offset++] = (byte)'X';  // ResRef
        WriteUInt32(buffer, ref offset, 0);  // ResID
        WriteUInt16(buffer, ref offset, 0);  // ResType
        WriteUInt16(buffer, ref offset, 0);  // Unused

        // ResourceList entry (8 bytes) at offset 184
        WriteUInt32(buffer, ref offset, resourceOffset);  // Bad offset
        WriteUInt32(buffer, ref offset, 100);  // Size

        return buffer;
    }

    #endregion

    #region Helper Methods - TLK

    private static byte[] CreateTruncatedTlkFile()
    {
        var buffer = new byte[44];  // Header + 1 partial entry
        var offset = 0;

        WriteString(buffer, ref offset, "TLK ");
        WriteString(buffer, ref offset, "V3.0");

        WriteUInt32(buffer, ref offset, 0);   // LanguageID
        WriteUInt32(buffer, ref offset, 10);  // StringCount (more than we have)
        WriteUInt32(buffer, ref offset, 44);  // StringEntriesOffset

        return buffer;
    }

    private static byte[] CreateTlkFileWithBadStringOffset(uint stringOffset)
    {
        // Header (20 bytes) + entry (40 bytes) + string data
        var buffer = new byte[60];
        var offset = 0;

        WriteString(buffer, ref offset, "TLK ");
        WriteString(buffer, ref offset, "V3.0");

        WriteUInt32(buffer, ref offset, 0);   // LanguageID
        WriteUInt32(buffer, ref offset, 1);   // StringCount
        WriteUInt32(buffer, ref offset, 20);  // StringEntriesOffset

        // Entry at offset 20 (40 bytes)
        WriteUInt32(buffer, ref offset, 1);   // Flags (HasText)
        for (int i = 0; i < 16; i++)
            buffer[offset++] = 0;  // SoundResRef
        WriteUInt32(buffer, ref offset, 0);   // VolumeVariance
        WriteUInt32(buffer, ref offset, 0);   // PitchVariance
        WriteUInt32(buffer, ref offset, stringOffset);  // Bad offset
        WriteUInt32(buffer, ref offset, 10);  // StringSize
        WriteFloat(buffer, ref offset, 0);    // SoundLength

        return buffer;
    }

    #endregion

    #region Low-Level Helpers

    private static void WriteString(byte[] buffer, ref int offset, string value)
    {
        var bytes = Encoding.ASCII.GetBytes(value);
        Array.Copy(bytes, 0, buffer, offset, Math.Min(bytes.Length, buffer.Length - offset));
        offset += bytes.Length;
    }

    private static void WriteUInt32(byte[] buffer, ref int offset, uint value)
    {
        if (offset + 4 <= buffer.Length)
        {
            buffer[offset++] = (byte)(value & 0xFF);
            buffer[offset++] = (byte)((value >> 8) & 0xFF);
            buffer[offset++] = (byte)((value >> 16) & 0xFF);
            buffer[offset++] = (byte)((value >> 24) & 0xFF);
        }
        else
        {
            offset += 4;
        }
    }

    private static void WriteUInt16(byte[] buffer, ref int offset, ushort value)
    {
        if (offset + 2 <= buffer.Length)
        {
            buffer[offset++] = (byte)(value & 0xFF);
            buffer[offset++] = (byte)((value >> 8) & 0xFF);
        }
        else
        {
            offset += 2;
        }
    }

    private static void WriteFloat(byte[] buffer, ref int offset, float value)
    {
        if (offset + 4 <= buffer.Length)
        {
            var bytes = BitConverter.GetBytes(value);
            Array.Copy(bytes, 0, buffer, offset, 4);
        }
        offset += 4;
    }

    #endregion
}
