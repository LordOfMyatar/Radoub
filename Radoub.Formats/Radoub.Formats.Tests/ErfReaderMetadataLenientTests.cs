using Radoub.Formats.Erf;
using Xunit;

namespace Radoub.Formats.Tests;

/// <summary>
/// Regression tests for #2501: ErfReader.ReadMetadataOnly() must tolerate a
/// localized-string entry whose declared StringSize exceeds the sliced
/// loc-string block (as seen in sloppily-authored community PRC HAKs), rather
/// than throwing InvalidDataException.
/// </summary>
public class ErfReaderMetadataLenientTests
{
    /// <summary>
    /// Builds a valid ERF (via ErfWriter) with one resource and one localized
    /// string, then inflates the loc-string entry's StringSize field so it
    /// declares more bytes than the loc-string block actually contains. This
    /// mirrors the PRC HAK failure: locCount=1, StringSize larger than the block.
    ///
    /// ERF layout (little-endian):
    ///   Header: 160 bytes.
    ///     offset 12: localizedStringSize (uint) — total loc-string block size.
    ///   Loc-string block starts at file offset 160 (HeaderSize):
    ///     +0 (file offset 160): LanguageId (uint)
    ///     +4 (file offset 164): StringSize (uint)  <-- field we inflate
    ///     +8:                    text bytes (StringSize bytes)
    /// We overwrite the StringSize at file offset 164 with a value larger than
    /// the block so the strict reader trips the "data extends beyond buffer
    /// boundary" check while the block size in the header stays honest.
    /// </summary>
    private static byte[] CreateMalformedErfWithOverlongLocString()
    {
        var erf = new ErfFile
        {
            FileType = "HAK ",
            FileVersion = "V1.0",
        };
        erf.LocalizedStrings.Add(new ErfLocalizedString { LanguageId = 0, Text = "PRC HAK" });
        erf.Resources.Add(new ErfResourceEntry
        {
            ResRef = "testres",
            ResourceType = 2029, // UTI
            ResId = 0,
        });

        var resourceData = new Dictionary<(string ResRef, ushort Type), byte[]>
        {
            [("testres", 2029)] = new byte[] { 0x01, 0x02, 0x03, 0x04 },
        };

        byte[] bytes;
        using (var ms = new MemoryStream())
        {
            ErfWriter.Write(erf, ms, resourceData);
            bytes = ms.ToArray();
        }

        // Loc-string entry StringSize field: file offset 164 (HeaderSize 160 + 4).
        const int stringSizeFieldOffset = 164;

        // Inflate to a value far larger than the loc-string block. The block is
        // 8 (entry header) + text bytes; 0xFFFF is well beyond it but small
        // enough to avoid the int.MaxValue overflow branch, forcing the strict
        // "data extends beyond buffer boundary" throw at ErfReader.cs:255-257.
        BitConverter.GetBytes((uint)0xFFFF).CopyTo(bytes, stringSizeFieldOffset);

        return bytes;
    }

    [Fact]
    public void ReadMetadataOnly_OverlongLocString_DoesNotThrow_AndKeepsResources()
    {
        var bytes = CreateMalformedErfWithOverlongLocString();
        var path = Path.Combine(Path.GetTempPath(), $"erf_meta_{Guid.NewGuid():N}.hak");

        try
        {
            File.WriteAllBytes(path, bytes);

            var erf = ErfReader.ReadMetadataOnly(path);

            Assert.NotNull(erf);
            Assert.Equal("HAK ", erf.FileType);
            Assert.Single(erf.Resources);
            Assert.Equal("testres", erf.Resources[0].ResRef);
            Assert.Equal((ushort)2029, erf.Resources[0].ResourceType);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
