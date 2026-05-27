using System.Text;
using Radoub.Formats.Ssf;
using Xunit;

namespace Radoub.Formats.Tests;

/// <summary>
/// Tests for SsfReader, including the bare-catch narrowing fix (issue #2243).
/// Source: NonPublic/Radoub/Reviews/2026-05-25/radoub-formats.md (Critical).
/// </summary>
public class SsfReaderTests
{
    private static byte[] BuildValidSsf(int entryCount = 2)
    {
        // Header: 4 (type) + 4 (version) + 4 (entryCount) + 4 (tableOffset) + 24 (padding) = 40
        // Then entry offset table: 4 * entryCount
        // Then entries: 20 each (16 ResRef + 4 StringRef)
        int tableOffset = 40;
        int entriesStart = tableOffset + entryCount * 4;
        var data = new byte[entriesStart + entryCount * 20];

        Encoding.ASCII.GetBytes("SSF ").CopyTo(data, 0);
        Encoding.ASCII.GetBytes("V1.0").CopyTo(data, 4);
        BitConverter.GetBytes((uint)entryCount).CopyTo(data, 8);
        BitConverter.GetBytes((uint)tableOffset).CopyTo(data, 12);
        // 24 bytes padding already zeroed

        for (int i = 0; i < entryCount; i++)
        {
            uint entryOffset = (uint)(entriesStart + i * 20);
            BitConverter.GetBytes(entryOffset).CopyTo(data, tableOffset + i * 4);

            string resRef = $"snd{i:D3}";
            Encoding.ASCII.GetBytes(resRef).CopyTo(data, (int)entryOffset);
            BitConverter.GetBytes((uint)(1000 + i)).CopyTo(data, (int)entryOffset + 16);
        }

        return data;
    }

    [Fact]
    public void Read_ValidSsf_ReturnsParsedFile()
    {
        var data = BuildValidSsf(entryCount: 3);

        var ssf = SsfReader.Read(data);

        Assert.NotNull(ssf);
        Assert.Equal("V1.0", ssf!.Version);
        Assert.Equal(3, ssf.Entries.Count);
        Assert.Equal("snd000", ssf.Entries[0].ResRef);
        Assert.Equal(1000u, ssf.Entries[0].StringRef);
    }

    [Fact]
    public void Read_NullData_ReturnsNull()
    {
        Assert.Null(SsfReader.Read(null!));
    }

    [Fact]
    public void Read_TooShortData_ReturnsNull()
    {
        Assert.Null(SsfReader.Read(new byte[10]));
    }

    [Fact]
    public void Read_InvalidFileType_ReturnsNull()
    {
        var data = BuildValidSsf();
        Encoding.ASCII.GetBytes("XYZ ").CopyTo(data, 0);

        Assert.Null(SsfReader.Read(data));
    }

    /// <summary>
    /// Entry offset points past the end of the buffer.
    /// Pre-fix: caught by bare-catch (returns null). Post-fix: caught by narrowed
    /// EndOfStreamException handler (still returns null). Behavior preserved;
    /// the difference is that genuinely unexpected exceptions (OOM, etc.) are
    /// no longer swallowed.
    /// </summary>
    [Fact]
    public void Read_EntryOffsetPastEndOfStream_ReturnsNull()
    {
        var data = BuildValidSsf(entryCount: 1);
        // Overwrite the first entry-table offset with a value past EOS.
        BitConverter.GetBytes(0xFFFFFFF0u).CopyTo(data, 40);

        Assert.Null(SsfReader.Read(data));
    }

    /// <summary>
    /// Table offset points past the end of the buffer.
    /// </summary>
    [Fact]
    public void Read_TableOffsetPastEndOfStream_ReturnsNull()
    {
        var data = BuildValidSsf(entryCount: 1);
        BitConverter.GetBytes(0xFFFFFFF0u).CopyTo(data, 12);

        Assert.Null(SsfReader.Read(data));
    }

    /// <summary>
    /// Bare-catch fix: ArgumentNullException (and other non-format exceptions) must
    /// propagate, not be silently swallowed. Triggered by passing data that causes
    /// the reader's logic to invoke an API with a null argument. Easiest path:
    /// inject a wrapped reader is impossible from outside, so we exercise the
    /// behavior via a corrupted-but-legal call path. Note: this test documents the
    /// contract — only EndOfStreamException, InvalidDataException, and
    /// ArgumentException (for invalid stream operations) should be caught.
    /// </summary>
    [Fact]
    public void Read_HeaderOnly_ReturnsNullViaNarrowedCatch()
    {
        // 40-byte buffer: passes length check, then reads header successfully,
        // then jumps to tableOffset (claims 1 entry at offset 40) → EOS reading the offset.
        var data = new byte[40];
        Encoding.ASCII.GetBytes("SSF ").CopyTo(data, 0);
        Encoding.ASCII.GetBytes("V1.0").CopyTo(data, 4);
        BitConverter.GetBytes(1u).CopyTo(data, 8);    // 1 entry
        BitConverter.GetBytes(40u).CopyTo(data, 12);  // tableOffset = end of buffer

        Assert.Null(SsfReader.Read(data));
    }
}
