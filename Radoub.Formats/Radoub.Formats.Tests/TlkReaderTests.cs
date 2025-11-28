using System.Text;
using Radoub.Formats.Tlk;
using Xunit;

namespace Radoub.Formats.Tests;

public class TlkReaderTests
{
    static TlkReaderTests()
    {
        // Register code pages for Windows-1252 encoding support
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }
    [Fact]
    public void Read_MinimalTlkFile_ParsesCorrectly()
    {
        var buffer = CreateMinimalTlkFile();

        var result = TlkReader.Read(buffer);

        Assert.Equal("TLK ", result.FileType);
        Assert.Equal("V3.0", result.FileVersion);
        Assert.Empty(result.Entries);
    }

    [Fact]
    public void Read_TlkWithLanguageId_ParsesLanguage()
    {
        var buffer = CreateMinimalTlkFile(languageId: 2); // German

        var result = TlkReader.Read(buffer);

        Assert.Equal(2u, result.LanguageId);
    }

    [Fact]
    public void Read_TlkWithSingleEntry_ParsesText()
    {
        var buffer = CreateTlkWithEntry("Hello, world!");

        var result = TlkReader.Read(buffer);

        Assert.Single(result.Entries);
        Assert.Equal("Hello, world!", result.Entries[0].Text);
        Assert.True(result.Entries[0].HasText);
    }

    [Fact]
    public void Read_TlkWithMultipleEntries_ParsesAll()
    {
        var texts = new[] { "First", "Second", "Third" };
        var buffer = CreateTlkWithEntries(texts);

        var result = TlkReader.Read(buffer);

        Assert.Equal(3, result.Entries.Count);
        for (int i = 0; i < texts.Length; i++)
        {
            Assert.Equal(texts[i], result.Entries[i].Text);
        }
    }

    [Fact]
    public void Read_EntryWithSoundResRef_ParsesResRef()
    {
        var buffer = CreateTlkWithEntryAndSound("Test text", "vo_test01");

        var result = TlkReader.Read(buffer);

        Assert.Single(result.Entries);
        Assert.Equal("vo_test01", result.Entries[0].SoundResRef);
        Assert.True(result.Entries[0].HasSound);
    }

    [Fact]
    public void Read_EntryWithSoundLength_ParsesLength()
    {
        var buffer = CreateTlkWithEntryAndSoundLength("Test text", 2.5f);

        var result = TlkReader.Read(buffer);

        Assert.Single(result.Entries);
        Assert.Equal(2.5f, result.Entries[0].SoundLength, 0.001f);
        Assert.True(result.Entries[0].HasSoundLength);
    }

    [Fact]
    public void Read_EntryWithNegativeSoundLength_ClampedToZero()
    {
        var buffer = CreateTlkWithEntryAndSoundLength("Test text", -1.0f);

        var result = TlkReader.Read(buffer);

        Assert.Equal(0f, result.Entries[0].SoundLength);
    }

    [Fact]
    public void Read_InvalidSignature_ThrowsException()
    {
        var buffer = new byte[20];
        Encoding.ASCII.GetBytes("XXX V3.0").CopyTo(buffer, 0);

        var ex = Assert.Throws<InvalidDataException>(() => TlkReader.Read(buffer));
        Assert.Contains("Invalid TLK signature", ex.Message);
    }

    [Fact]
    public void Read_InvalidVersion_ThrowsException()
    {
        var buffer = new byte[20];
        Encoding.ASCII.GetBytes("TLK V2.0").CopyTo(buffer, 0);

        var ex = Assert.Throws<InvalidDataException>(() => TlkReader.Read(buffer));
        Assert.Contains("Invalid TLK signature", ex.Message);
    }

    [Fact]
    public void Read_FileTooSmall_ThrowsException()
    {
        var buffer = new byte[10]; // Too small for header (20 bytes)

        var ex = Assert.Throws<InvalidDataException>(() => TlkReader.Read(buffer));
        Assert.Contains("too small", ex.Message);
    }

    [Fact]
    public void Read_EntryTableBeyondFile_ThrowsException()
    {
        var buffer = new byte[20];
        Encoding.ASCII.GetBytes("TLK V3.0").CopyTo(buffer, 0);
        BitConverter.GetBytes(1000u).CopyTo(buffer, 12); // 1000 entries would require 40000+ bytes

        var ex = Assert.Throws<InvalidDataException>(() => TlkReader.Read(buffer));
        Assert.Contains("extends beyond file boundary", ex.Message);
    }

    [Fact]
    public void GetString_ValidStrRef_ReturnsText()
    {
        var tlk = new TlkFile();
        tlk.Entries.Add(new TlkEntry { Flags = 0x1, Text = "Hello" });
        tlk.Entries.Add(new TlkEntry { Flags = 0x1, Text = "World" });

        Assert.Equal("Hello", tlk.GetString(0));
        Assert.Equal("World", tlk.GetString(1));
    }

    [Fact]
    public void GetString_InvalidStrRef_ReturnsNull()
    {
        var tlk = new TlkFile();
        tlk.Entries.Add(new TlkEntry { Flags = 0x1, Text = "Hello" });

        Assert.Null(tlk.GetString(999));
    }

    [Fact]
    public void GetString_EntryWithoutText_ReturnsNull()
    {
        var tlk = new TlkFile();
        tlk.Entries.Add(new TlkEntry { Flags = 0x0, Text = "" }); // No HasText flag

        Assert.Null(tlk.GetString(0));
    }

    [Fact]
    public void GetEntry_ValidStrRef_ReturnsEntry()
    {
        var tlk = new TlkFile();
        var entry = new TlkEntry { Flags = 0x1, Text = "Test", SoundResRef = "sound01" };
        tlk.Entries.Add(entry);

        var result = tlk.GetEntry(0);

        Assert.NotNull(result);
        Assert.Equal("Test", result.Text);
        Assert.Equal("sound01", result.SoundResRef);
    }

    [Fact]
    public void GetEntry_InvalidStrRef_ReturnsNull()
    {
        var tlk = new TlkFile();

        Assert.Null(tlk.GetEntry(0));
    }

    [Fact]
    public void Count_ReturnsEntryCount()
    {
        var tlk = new TlkFile();
        tlk.Entries.Add(new TlkEntry());
        tlk.Entries.Add(new TlkEntry());
        tlk.Entries.Add(new TlkEntry());

        Assert.Equal(3, tlk.Count);
    }

    [Fact]
    public void EntryFlags_HasText_DetectedCorrectly()
    {
        var entry = new TlkEntry { Flags = 0x1 };
        Assert.True(entry.HasText);
        Assert.False(entry.HasSound);
        Assert.False(entry.HasSoundLength);
    }

    [Fact]
    public void EntryFlags_HasSound_DetectedCorrectly()
    {
        var entry = new TlkEntry { Flags = 0x2 };
        Assert.False(entry.HasText);
        Assert.True(entry.HasSound);
        Assert.False(entry.HasSoundLength);
    }

    [Fact]
    public void EntryFlags_HasSoundLength_DetectedCorrectly()
    {
        var entry = new TlkEntry { Flags = 0x4 };
        Assert.False(entry.HasText);
        Assert.False(entry.HasSound);
        Assert.True(entry.HasSoundLength);
    }

    [Fact]
    public void EntryFlags_AllFlags_DetectedCorrectly()
    {
        var entry = new TlkEntry { Flags = 0x7 }; // All three flags
        Assert.True(entry.HasText);
        Assert.True(entry.HasSound);
        Assert.True(entry.HasSoundLength);
    }

    [Fact]
    public void Read_SoundResRefWithLegacyArtifact_StripsArtifact()
    {
        var buffer = CreateTlkWithEntryAndSoundWithArtifact("Test", "sound\xC001");

        var result = TlkReader.Read(buffer);

        // Should strip the 0xC0 character
        Assert.Equal("sound01", result.Entries[0].SoundResRef);
    }

    [Fact]
    public void Read_EmptyEntry_ParsesCorrectly()
    {
        var buffer = CreateTlkWithEmptyEntry();

        var result = TlkReader.Read(buffer);

        Assert.Single(result.Entries);
        Assert.Equal(string.Empty, result.Entries[0].Text);
        Assert.False(result.Entries[0].HasText);
    }

    #region Test Helpers

    private static byte[] CreateMinimalTlkFile(uint languageId = 0)
    {
        var buffer = new byte[20]; // Header only
        Encoding.ASCII.GetBytes("TLK V3.0").CopyTo(buffer, 0);
        BitConverter.GetBytes(languageId).CopyTo(buffer, 8);
        BitConverter.GetBytes(0u).CopyTo(buffer, 12); // Entry count = 0
        BitConverter.GetBytes(20u).CopyTo(buffer, 16); // String data offset (at end of header)
        return buffer;
    }

    private static byte[] CreateTlkWithEntry(string text)
    {
        return CreateTlkWithEntries(new[] { text });
    }

    private static byte[] CreateTlkWithEntries(string[] texts)
    {
        var entryCount = texts.Length;
        var entryTableSize = entryCount * 40;
        var stringDataOffset = 20 + entryTableSize;

        // Calculate total string data size
        var stringOffsets = new int[entryCount];
        var currentStringOffset = 0;
        for (int i = 0; i < entryCount; i++)
        {
            stringOffsets[i] = currentStringOffset;
            currentStringOffset += Encoding.UTF8.GetByteCount(texts[i]);
        }
        var totalStringSize = currentStringOffset;

        var totalSize = stringDataOffset + totalStringSize;
        var buffer = new byte[totalSize];

        // Header
        Encoding.ASCII.GetBytes("TLK V3.0").CopyTo(buffer, 0);
        BitConverter.GetBytes(0u).CopyTo(buffer, 8); // Language ID
        BitConverter.GetBytes((uint)entryCount).CopyTo(buffer, 12);
        BitConverter.GetBytes((uint)stringDataOffset).CopyTo(buffer, 16);

        // Entries
        for (int i = 0; i < entryCount; i++)
        {
            var entryOffset = 20 + (i * 40);
            var textBytes = Encoding.UTF8.GetBytes(texts[i]);

            BitConverter.GetBytes(0x1u).CopyTo(buffer, entryOffset); // Flags = HasText
            // Bytes 4-19: SoundResRef (zeros)
            // Bytes 20-27: VolumeVariance, PitchVariance (zeros)
            BitConverter.GetBytes((uint)stringOffsets[i]).CopyTo(buffer, entryOffset + 28);
            BitConverter.GetBytes((uint)textBytes.Length).CopyTo(buffer, entryOffset + 32);
            // Bytes 36-39: SoundLength (zeros)

            // String data
            textBytes.CopyTo(buffer, stringDataOffset + stringOffsets[i]);
        }

        return buffer;
    }

    private static byte[] CreateTlkWithEmptyEntry()
    {
        var buffer = new byte[20 + 40]; // Header + 1 entry, no string data
        Encoding.ASCII.GetBytes("TLK V3.0").CopyTo(buffer, 0);
        BitConverter.GetBytes(0u).CopyTo(buffer, 8); // Language ID
        BitConverter.GetBytes(1u).CopyTo(buffer, 12); // Entry count = 1
        BitConverter.GetBytes(60u).CopyTo(buffer, 16); // String data offset

        // Entry with no flags set
        // All zeros = no text, no sound
        return buffer;
    }

    private static byte[] CreateTlkWithEntryAndSound(string text, string soundResRef)
    {
        var textBytes = Encoding.UTF8.GetBytes(text);
        var stringDataOffset = 20 + 40;
        var totalSize = stringDataOffset + textBytes.Length;
        var buffer = new byte[totalSize];

        // Header
        Encoding.ASCII.GetBytes("TLK V3.0").CopyTo(buffer, 0);
        BitConverter.GetBytes(0u).CopyTo(buffer, 8);
        BitConverter.GetBytes(1u).CopyTo(buffer, 12);
        BitConverter.GetBytes((uint)stringDataOffset).CopyTo(buffer, 16);

        // Entry
        var entryOffset = 20;
        BitConverter.GetBytes(0x3u).CopyTo(buffer, entryOffset); // Flags = HasText | HasSound
        Encoding.ASCII.GetBytes(soundResRef.PadRight(16, '\0')).CopyTo(buffer, entryOffset + 4);
        BitConverter.GetBytes(0u).CopyTo(buffer, entryOffset + 28); // String offset
        BitConverter.GetBytes((uint)textBytes.Length).CopyTo(buffer, entryOffset + 32);

        // String data
        textBytes.CopyTo(buffer, stringDataOffset);

        return buffer;
    }

    private static byte[] CreateTlkWithEntryAndSoundWithArtifact(string text, string soundResRefWithArtifact)
    {
        var textBytes = Encoding.UTF8.GetBytes(text);
        var stringDataOffset = 20 + 40;
        var totalSize = stringDataOffset + textBytes.Length;
        var buffer = new byte[totalSize];

        // Header
        Encoding.ASCII.GetBytes("TLK V3.0").CopyTo(buffer, 0);
        BitConverter.GetBytes(0u).CopyTo(buffer, 8);
        BitConverter.GetBytes(1u).CopyTo(buffer, 12);
        BitConverter.GetBytes((uint)stringDataOffset).CopyTo(buffer, 16);

        // Entry
        var entryOffset = 20;
        BitConverter.GetBytes(0x3u).CopyTo(buffer, entryOffset); // Flags = HasText | HasSound

        // Write sound resref bytes directly - manually construct with 0xC0 artifact
        // soundResRefWithArtifact is "sound\xC001" but we need to write raw bytes
        var paddedResRef = new byte[16];
        Encoding.ASCII.GetBytes("sound").CopyTo(paddedResRef, 0);
        paddedResRef[5] = 0xC0; // Legacy artifact byte
        Encoding.ASCII.GetBytes("01").CopyTo(paddedResRef, 6);
        paddedResRef.CopyTo(buffer, entryOffset + 4);

        BitConverter.GetBytes(0u).CopyTo(buffer, entryOffset + 28);
        BitConverter.GetBytes((uint)textBytes.Length).CopyTo(buffer, entryOffset + 32);

        // String data
        textBytes.CopyTo(buffer, stringDataOffset);

        return buffer;
    }

    private static byte[] CreateTlkWithEntryAndSoundLength(string text, float soundLength)
    {
        var textBytes = Encoding.UTF8.GetBytes(text);
        var stringDataOffset = 20 + 40;
        var totalSize = stringDataOffset + textBytes.Length;
        var buffer = new byte[totalSize];

        // Header
        Encoding.ASCII.GetBytes("TLK V3.0").CopyTo(buffer, 0);
        BitConverter.GetBytes(0u).CopyTo(buffer, 8);
        BitConverter.GetBytes(1u).CopyTo(buffer, 12);
        BitConverter.GetBytes((uint)stringDataOffset).CopyTo(buffer, 16);

        // Entry
        var entryOffset = 20;
        BitConverter.GetBytes(0x5u).CopyTo(buffer, entryOffset); // Flags = HasText | HasSoundLength
        BitConverter.GetBytes(0u).CopyTo(buffer, entryOffset + 28);
        BitConverter.GetBytes((uint)textBytes.Length).CopyTo(buffer, entryOffset + 32);
        BitConverter.GetBytes(soundLength).CopyTo(buffer, entryOffset + 36);

        // String data
        textBytes.CopyTo(buffer, stringDataOffset);

        return buffer;
    }

    #endregion
}
