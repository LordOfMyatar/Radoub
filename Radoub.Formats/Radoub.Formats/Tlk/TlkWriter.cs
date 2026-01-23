using System.Text;

namespace Radoub.Formats.Tlk;

/// <summary>
/// Writes TLK (Talk Table) files to binary format.
/// Reference: neverwinter.nim tlk.nim
/// </summary>
public static class TlkWriter
{
    private const int HeaderSize = 20;
    private const int EntrySize = 40;
    private const string Signature = "TLK V3.0";

    static TlkWriter()
    {
        // Register code pages for Windows-1252 encoding support
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    /// <summary>
    /// Write a TLK file to a file path.
    /// </summary>
    public static void Write(TlkFile tlk, string filePath)
    {
        var bytes = Write(tlk);
        File.WriteAllBytes(filePath, bytes);
    }

    /// <summary>
    /// Write a TLK file to a stream.
    /// </summary>
    public static void Write(TlkFile tlk, Stream stream)
    {
        var bytes = Write(tlk);
        stream.Write(bytes, 0, bytes.Length);
    }

    /// <summary>
    /// Write a TLK file to a byte array.
    /// </summary>
    public static byte[] Write(TlkFile tlk)
    {
        // Calculate sizes
        var entryCount = (uint)tlk.Entries.Count;
        var stringDataOffset = HeaderSize + (entryCount * EntrySize);

        // Build string data and calculate offsets
        var stringData = new List<byte>();
        var stringOffsets = new List<uint>();
        var stringLengths = new List<uint>();

        var encoding = Encoding.GetEncoding(1252);

        foreach (var entry in tlk.Entries)
        {
            if (entry.HasText && !string.IsNullOrEmpty(entry.Text))
            {
                stringOffsets.Add((uint)stringData.Count);
                var textBytes = encoding.GetBytes(entry.Text);
                stringLengths.Add((uint)textBytes.Length);
                stringData.AddRange(textBytes);
            }
            else
            {
                stringOffsets.Add(0);
                stringLengths.Add(0);
            }
        }

        // Allocate buffer
        var totalSize = (int)stringDataOffset + stringData.Count;
        var buffer = new byte[totalSize];

        // Write header (20 bytes)
        var signatureBytes = Encoding.ASCII.GetBytes(Signature);
        Array.Copy(signatureBytes, 0, buffer, 0, 8);
        BitConverter.GetBytes(tlk.LanguageId).CopyTo(buffer, 8);
        BitConverter.GetBytes(entryCount).CopyTo(buffer, 12);
        BitConverter.GetBytes((uint)stringDataOffset).CopyTo(buffer, 16);

        // Write entries
        for (int i = 0; i < tlk.Entries.Count; i++)
        {
            var entry = tlk.Entries[i];
            var entryOffset = HeaderSize + (i * EntrySize);

            // Entry structure (40 bytes):
            // 0-3: Flags (uint32)
            // 4-19: SoundResRef (16 bytes, null-terminated)
            // 20-23: VolumeVariance (uint32)
            // 24-27: PitchVariance (uint32)
            // 28-31: StringOffset (uint32)
            // 32-35: StringLength (uint32)
            // 36-39: SoundLength (float32)

            BitConverter.GetBytes(entry.Flags).CopyTo(buffer, entryOffset);

            // Write SoundResRef (16 bytes, null-padded)
            var resRefBytes = Encoding.ASCII.GetBytes(entry.SoundResRef ?? string.Empty);
            var resRefLength = Math.Min(resRefBytes.Length, 16);
            Array.Copy(resRefBytes, 0, buffer, entryOffset + 4, resRefLength);
            // Remaining bytes are already zero (null-padded)

            BitConverter.GetBytes(entry.VolumeVariance).CopyTo(buffer, entryOffset + 20);
            BitConverter.GetBytes(entry.PitchVariance).CopyTo(buffer, entryOffset + 24);
            BitConverter.GetBytes(stringOffsets[i]).CopyTo(buffer, entryOffset + 28);
            BitConverter.GetBytes(stringLengths[i]).CopyTo(buffer, entryOffset + 32);
            BitConverter.GetBytes(entry.SoundLength).CopyTo(buffer, entryOffset + 36);
        }

        // Write string data
        stringData.CopyTo(buffer, (int)stringDataOffset);

        return buffer;
    }
}
