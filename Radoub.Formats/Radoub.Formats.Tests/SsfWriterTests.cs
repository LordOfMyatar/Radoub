using System.Text;
using Radoub.Formats.Ssf;
using Xunit;

namespace Radoub.Formats.Tests;

/// <summary>
/// Tests for SsfWriter round-trip support (issue #2270).
/// Layout mirrors SsfReader: 40-byte header, entry offset table, 20-byte entries
/// (16-byte null-padded ResRef + 4-byte StringRef), ASCII encoding.
/// </summary>
public class SsfWriterTests
{
    /// <summary>
    /// Reproduces SsfReaderTests.BuildValidSsf so the writer can be byte-compared
    /// against the same canonical layout the reader parses.
    /// </summary>
    private static byte[] BuildValidSsf(int entryCount = 2)
    {
        int tableOffset = 40;
        int entriesStart = tableOffset + entryCount * 4;
        var data = new byte[entriesStart + entryCount * 20];

        Encoding.ASCII.GetBytes("SSF ").CopyTo(data, 0);
        Encoding.ASCII.GetBytes("V1.0").CopyTo(data, 4);
        BitConverter.GetBytes((uint)entryCount).CopyTo(data, 8);
        BitConverter.GetBytes((uint)tableOffset).CopyTo(data, 12);

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
    public void Write_ProducesByteIdenticalCanonicalLayout()
    {
        var expected = BuildValidSsf(entryCount: 3);
        var ssf = SsfReader.Read(expected);
        Assert.NotNull(ssf);

        var actual = SsfWriter.Write(ssf!);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void RoundTrip_ReadWriteRead_PreservesContent()
    {
        var original = BuildValidSsf(entryCount: 5);

        var ssf = SsfReader.Read(original);
        Assert.NotNull(ssf);

        var written = SsfWriter.Write(ssf!);
        var reparsed = SsfReader.Read(written);

        Assert.NotNull(reparsed);
        Assert.Equal(ssf!.Version, reparsed!.Version);
        Assert.Equal(ssf.Entries.Count, reparsed.Entries.Count);
        for (int i = 0; i < ssf.Entries.Count; i++)
        {
            Assert.Equal(ssf.Entries[i].ResRef, reparsed.Entries[i].ResRef);
            Assert.Equal(ssf.Entries[i].StringRef, reparsed.Entries[i].StringRef);
        }
    }

    [Fact]
    public void Write_StandardSoundset_49Entries_RoundTrips()
    {
        var ssf = new SsfFile { Version = "V1.0" };
        for (int i = 0; i < 49; i++)
        {
            ssf.Entries.Add(new SsfEntry { ResRef = $"vs_x{i:D2}", StringRef = (uint)(2000 + i) });
        }

        var reparsed = SsfReader.Read(SsfWriter.Write(ssf));

        Assert.NotNull(reparsed);
        Assert.Equal(49, reparsed!.Entries.Count);
        Assert.Equal("vs_x00", reparsed.Entries[0].ResRef);
        Assert.Equal(2048u, reparsed.Entries[48].StringRef);
    }

    [Fact]
    public void Write_EmptyResRef_RoundTripsAsEmpty()
    {
        var ssf = new SsfFile { Version = "V1.0" };
        ssf.Entries.Add(new SsfEntry { ResRef = string.Empty, StringRef = uint.MaxValue });

        var reparsed = SsfReader.Read(SsfWriter.Write(ssf));

        Assert.NotNull(reparsed);
        Assert.Single(reparsed!.Entries);
        Assert.Equal(string.Empty, reparsed.Entries[0].ResRef);
        Assert.Equal(uint.MaxValue, reparsed.Entries[0].StringRef);
    }

    [Fact]
    public void Write_ResRefExactly16Chars_NotTruncated()
    {
        var ssf = new SsfFile { Version = "V1.0" };
        ssf.Entries.Add(new SsfEntry { ResRef = "abcdefghijklmnop", StringRef = 7u });

        var reparsed = SsfReader.Read(SsfWriter.Write(ssf));

        Assert.NotNull(reparsed);
        Assert.Equal("abcdefghijklmnop", reparsed!.Entries[0].ResRef);
    }

    [Fact]
    public void Write_NullFile_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => SsfWriter.Write(null!));
    }
}
