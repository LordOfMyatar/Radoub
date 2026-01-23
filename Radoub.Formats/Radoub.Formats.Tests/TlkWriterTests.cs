using System.Text;
using Radoub.Formats.Tlk;
using Xunit;

namespace Radoub.Formats.Tests;

public class TlkWriterTests
{
    static TlkWriterTests()
    {
        // Register code pages for Windows-1252 encoding support
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    [Fact]
    public void Write_EmptyTlk_CreatesValidFile()
    {
        var tlk = new TlkFile();

        var bytes = TlkWriter.Write(tlk);

        Assert.NotNull(bytes);
        Assert.True(bytes.Length >= 20, "Should have at least header bytes");

        // Verify header
        var signature = Encoding.ASCII.GetString(bytes, 0, 8);
        Assert.Equal("TLK V3.0", signature);
    }

    [Fact]
    public void Write_TlkWithLanguageId_PreservesLanguage()
    {
        var tlk = new TlkFile { LanguageId = 2 }; // German

        var bytes = TlkWriter.Write(tlk);
        var result = TlkReader.Read(bytes);

        Assert.Equal(2u, result.LanguageId);
    }

    [Fact]
    public void Write_SingleEntry_RoundTrips()
    {
        var tlk = new TlkFile();
        tlk.Entries.Add(new TlkEntry
        {
            Flags = 0x1, // HasText
            Text = "Hello, world!"
        });

        var bytes = TlkWriter.Write(tlk);
        var result = TlkReader.Read(bytes);

        Assert.Single(result.Entries);
        Assert.Equal("Hello, world!", result.Entries[0].Text);
        Assert.True(result.Entries[0].HasText);
    }

    [Fact]
    public void Write_MultipleEntries_RoundTrips()
    {
        var tlk = new TlkFile();
        tlk.Entries.Add(new TlkEntry { Flags = 0x1, Text = "First entry" });
        tlk.Entries.Add(new TlkEntry { Flags = 0x1, Text = "Second entry" });
        tlk.Entries.Add(new TlkEntry { Flags = 0x1, Text = "Third entry" });

        var bytes = TlkWriter.Write(tlk);
        var result = TlkReader.Read(bytes);

        Assert.Equal(3, result.Entries.Count);
        Assert.Equal("First entry", result.Entries[0].Text);
        Assert.Equal("Second entry", result.Entries[1].Text);
        Assert.Equal("Third entry", result.Entries[2].Text);
    }

    [Fact]
    public void Write_EntryWithSound_RoundTrips()
    {
        var tlk = new TlkFile();
        tlk.Entries.Add(new TlkEntry
        {
            Flags = 0x3, // HasText | HasSound
            Text = "Voice line",
            SoundResRef = "vo_aribeth_01"
        });

        var bytes = TlkWriter.Write(tlk);
        var result = TlkReader.Read(bytes);

        Assert.Single(result.Entries);
        Assert.Equal("Voice line", result.Entries[0].Text);
        Assert.Equal("vo_aribeth_01", result.Entries[0].SoundResRef);
        Assert.True(result.Entries[0].HasText);
        Assert.True(result.Entries[0].HasSound);
    }

    [Fact]
    public void Write_EntryWithSoundLength_RoundTrips()
    {
        var tlk = new TlkFile();
        tlk.Entries.Add(new TlkEntry
        {
            Flags = 0x5, // HasText | HasSoundLength
            Text = "Spoken line",
            SoundLength = 2.5f
        });

        var bytes = TlkWriter.Write(tlk);
        var result = TlkReader.Read(bytes);

        Assert.Single(result.Entries);
        Assert.Equal(2.5f, result.Entries[0].SoundLength, 0.001f);
        Assert.True(result.Entries[0].HasSoundLength);
    }

    [Fact]
    public void Write_EntryWithAllProperties_RoundTrips()
    {
        var tlk = new TlkFile { LanguageId = 1 }; // French
        tlk.Entries.Add(new TlkEntry
        {
            Flags = 0x7, // HasText | HasSound | HasSoundLength
            Text = "Complete entry",
            SoundResRef = "sound_test",
            VolumeVariance = 10,
            PitchVariance = 5,
            SoundLength = 3.14159f
        });

        var bytes = TlkWriter.Write(tlk);
        var result = TlkReader.Read(bytes);

        Assert.Equal(1u, result.LanguageId);
        Assert.Single(result.Entries);
        var entry = result.Entries[0];
        Assert.Equal("Complete entry", entry.Text);
        Assert.Equal("sound_test", entry.SoundResRef);
        Assert.Equal(10u, entry.VolumeVariance);
        Assert.Equal(5u, entry.PitchVariance);
        Assert.Equal(3.14159f, entry.SoundLength, 0.00001f);
    }

    [Fact]
    public void Write_EmptyEntry_RoundTrips()
    {
        var tlk = new TlkFile();
        tlk.Entries.Add(new TlkEntry { Flags = 0x0 }); // No flags

        var bytes = TlkWriter.Write(tlk);
        var result = TlkReader.Read(bytes);

        Assert.Single(result.Entries);
        Assert.False(result.Entries[0].HasText);
        Assert.Equal(string.Empty, result.Entries[0].Text);
    }

    [Fact]
    public void Write_MixedEntries_RoundTrips()
    {
        var tlk = new TlkFile();
        tlk.Entries.Add(new TlkEntry { Flags = 0x1, Text = "Text only" });
        tlk.Entries.Add(new TlkEntry { Flags = 0x0 }); // Empty
        tlk.Entries.Add(new TlkEntry { Flags = 0x3, Text = "With sound", SoundResRef = "snd01" });
        tlk.Entries.Add(new TlkEntry { Flags = 0x1, Text = "Another text" });

        var bytes = TlkWriter.Write(tlk);
        var result = TlkReader.Read(bytes);

        Assert.Equal(4, result.Entries.Count);
        Assert.Equal("Text only", result.Entries[0].Text);
        Assert.False(result.Entries[1].HasText);
        Assert.Equal("With sound", result.Entries[2].Text);
        Assert.Equal("snd01", result.Entries[2].SoundResRef);
        Assert.Equal("Another text", result.Entries[3].Text);
    }

    [Fact]
    public void Write_UnicodeText_RoundTrips()
    {
        var tlk = new TlkFile();
        // Windows-1252 compatible characters
        tlk.Entries.Add(new TlkEntry { Flags = 0x1, Text = "Héllo Wörld! Çafé" });

        var bytes = TlkWriter.Write(tlk);
        var result = TlkReader.Read(bytes);

        Assert.Equal("Héllo Wörld! Çafé", result.Entries[0].Text);
    }

    [Fact]
    public void Write_LongSoundResRef_Truncates()
    {
        var tlk = new TlkFile();
        tlk.Entries.Add(new TlkEntry
        {
            Flags = 0x2,
            SoundResRef = "this_is_a_very_long_resref_that_exceeds_16_chars"
        });

        var bytes = TlkWriter.Write(tlk);
        var result = TlkReader.Read(bytes);

        // Should be truncated to 16 chars
        Assert.Equal(16, result.Entries[0].SoundResRef.Length);
        Assert.Equal("this_is_a_very_l", result.Entries[0].SoundResRef);
    }

    [Fact]
    public void Write_ToFile_CreatesReadableFile()
    {
        var tlk = new TlkFile();
        tlk.Entries.Add(new TlkEntry { Flags = 0x1, Text = "Test entry" });

        var tempPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.tlk");
        try
        {
            TlkWriter.Write(tlk, tempPath);
            Assert.True(File.Exists(tempPath));

            var result = TlkReader.Read(tempPath);
            Assert.Single(result.Entries);
            Assert.Equal("Test entry", result.Entries[0].Text);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    [Fact]
    public void Write_ToStream_CreatesReadableData()
    {
        var tlk = new TlkFile();
        tlk.Entries.Add(new TlkEntry { Flags = 0x1, Text = "Stream test" });

        using var stream = new MemoryStream();
        TlkWriter.Write(tlk, stream);

        stream.Position = 0;
        var result = TlkReader.Read(stream);

        Assert.Single(result.Entries);
        Assert.Equal("Stream test", result.Entries[0].Text);
    }

    [Fact]
    public void Write_LargeEntryCount_RoundTrips()
    {
        var tlk = new TlkFile();
        for (int i = 0; i < 100; i++)
        {
            tlk.Entries.Add(new TlkEntry
            {
                Flags = 0x1,
                Text = $"Entry number {i}"
            });
        }

        var bytes = TlkWriter.Write(tlk);
        var result = TlkReader.Read(bytes);

        Assert.Equal(100, result.Entries.Count);
        Assert.Equal("Entry number 0", result.Entries[0].Text);
        Assert.Equal("Entry number 99", result.Entries[99].Text);
    }
}
