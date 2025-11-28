using System.Text;

namespace Radoub.Formats.Tlk;

/// <summary>
/// Reads TLK (Talk Table) files from binary format.
/// Reference: neverwinter.nim tlk.nim
/// </summary>
public static class TlkReader
{
    private const int HeaderSize = 20;
    private const int EntrySize = 40;
    private const string ExpectedSignature = "TLK V3.0";

    static TlkReader()
    {
        // Register code pages for Windows-1252 encoding support
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    /// <summary>
    /// Read a TLK file from a file path.
    /// </summary>
    public static TlkFile Read(string filePath)
    {
        var buffer = File.ReadAllBytes(filePath);
        return Read(buffer);
    }

    /// <summary>
    /// Read a TLK file from a stream.
    /// </summary>
    public static TlkFile Read(Stream stream)
    {
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return Read(ms.ToArray());
    }

    /// <summary>
    /// Read a TLK file from a byte buffer.
    /// </summary>
    public static TlkFile Read(byte[] buffer)
    {
        if (buffer.Length < HeaderSize)
            throw new InvalidDataException($"TLK file too small: {buffer.Length} bytes, minimum {HeaderSize}");

        var tlk = new TlkFile();

        // Read header (20 bytes)
        var signature = Encoding.ASCII.GetString(buffer, 0, 8);
        if (signature != ExpectedSignature)
            throw new InvalidDataException($"Invalid TLK signature: '{signature}', expected '{ExpectedSignature}'");

        tlk.FileType = signature.Substring(0, 4);
        tlk.FileVersion = signature.Substring(4, 4);
        tlk.LanguageId = BitConverter.ToUInt32(buffer, 8);
        var entryCount = BitConverter.ToUInt32(buffer, 12);
        var stringDataOffset = BitConverter.ToUInt32(buffer, 16);

        // Validate entry table fits in file
        var expectedEntryTableEnd = HeaderSize + (entryCount * EntrySize);
        if (expectedEntryTableEnd > buffer.Length)
            throw new InvalidDataException($"TLK entry table extends beyond file boundary: {expectedEntryTableEnd} > {buffer.Length}");

        // Read entries
        for (uint i = 0; i < entryCount; i++)
        {
            var entryOffset = HeaderSize + (i * EntrySize);
            var entry = ReadEntry(buffer, (int)entryOffset, (int)stringDataOffset);
            tlk.Entries.Add(entry);
        }

        return tlk;
    }

    private static TlkEntry ReadEntry(byte[] buffer, int entryOffset, int stringDataOffset)
    {
        var entry = new TlkEntry();

        // Entry structure (40 bytes):
        // 0-3: Flags (uint32)
        // 4-19: SoundResRef (16 bytes, null-terminated)
        // 20-23: VolumeVariance (uint32)
        // 24-27: PitchVariance (uint32)
        // 28-31: StringOffset (uint32, relative to stringDataOffset)
        // 32-35: StringLength (uint32)
        // 36-39: SoundLength (float32)

        entry.Flags = BitConverter.ToUInt32(buffer, entryOffset);

        // Read sound ResRef - strip nulls and invalid legacy characters (0xC0)
        var soundResRefBytes = new byte[16];
        Array.Copy(buffer, entryOffset + 4, soundResRefBytes, 0, 16);
        entry.SoundResRef = CleanResRef(soundResRefBytes);

        entry.VolumeVariance = BitConverter.ToUInt32(buffer, entryOffset + 20);
        entry.PitchVariance = BitConverter.ToUInt32(buffer, entryOffset + 24);

        var stringOffset = BitConverter.ToUInt32(buffer, entryOffset + 28);
        var stringLength = BitConverter.ToUInt32(buffer, entryOffset + 32);
        entry.SoundLength = BitConverter.ToSingle(buffer, entryOffset + 36);

        // Ensure sound length is non-negative (per neverwinter.nim)
        if (entry.SoundLength < 0)
            entry.SoundLength = 0;

        // Read text if present
        if (entry.HasText && stringLength > 0)
        {
            var absoluteStringOffset = stringDataOffset + stringOffset;
            if (absoluteStringOffset + stringLength <= buffer.Length)
            {
                // Use Windows-1252 encoding for NWN text (common for legacy BioWare files)
                // Fall back to UTF-8 if invalid
                try
                {
                    var encoding = Encoding.GetEncoding(1252);
                    entry.Text = encoding.GetString(buffer, (int)absoluteStringOffset, (int)stringLength);
                }
                catch
                {
                    entry.Text = Encoding.UTF8.GetString(buffer, (int)absoluteStringOffset, (int)stringLength);
                }

                // Trim any trailing nulls
                entry.Text = entry.Text.TrimEnd('\0');
            }
        }

        return entry;
    }

    /// <summary>
    /// Clean a ResRef by removing null bytes and invalid legacy characters.
    /// Per neverwinter.nim: strips 0xC0 artifacts from old editors.
    /// </summary>
    private static string CleanResRef(byte[] bytes)
    {
        var sb = new StringBuilder();
        foreach (var b in bytes)
        {
            // Stop at null terminator
            if (b == 0)
                break;

            // Skip invalid legacy editor artifacts (0xC0)
            if (b == 0xC0)
                continue;

            // Skip whitespace
            if (char.IsWhiteSpace((char)b))
                continue;

            sb.Append((char)b);
        }

        return sb.ToString();
    }
}
