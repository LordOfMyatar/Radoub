using System.Text;

namespace Radoub.Formats.Erf;

/// <summary>
/// Reads ERF files (including HAK, MOD, SAV) from binary format.
/// Reference: BioWare Aurora ERF format spec, neverwinter.nim erf.nim
/// </summary>
public static class ErfReader
{
    private const int HeaderSize = 160;
    private const int KeyEntrySize = 24;
    private const int ResourceEntrySize = 8;

    private static readonly string[] ValidFileTypes = { "ERF ", "HAK ", "MOD ", "SAV ", "NWM " };

    /// <summary>
    /// Read an ERF file from a file path.
    /// </summary>
    public static ErfFile Read(string filePath)
    {
        var buffer = File.ReadAllBytes(filePath);
        return Read(buffer);
    }

    /// <summary>
    /// Read an ERF file from a stream.
    /// </summary>
    public static ErfFile Read(Stream stream)
    {
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return Read(ms.ToArray());
    }

    /// <summary>
    /// Read an ERF file from a byte buffer.
    /// </summary>
    public static ErfFile Read(byte[] buffer)
    {
        if (buffer.Length < HeaderSize)
            throw new InvalidDataException($"ERF file too small: {buffer.Length} bytes, minimum {HeaderSize}");

        var erf = new ErfFile();

        // Read header
        erf.FileType = Encoding.ASCII.GetString(buffer, 0, 4);
        erf.FileVersion = Encoding.ASCII.GetString(buffer, 4, 4);

        if (!ValidFileTypes.Contains(erf.FileType))
            throw new InvalidDataException($"Invalid ERF file type: '{erf.FileType}', expected one of: {string.Join(", ", ValidFileTypes)}");

        if (erf.FileVersion != "V1.0")
            throw new InvalidDataException($"Unsupported ERF version: '{erf.FileVersion}', expected 'V1.0'");

        var languageCount = BitConverter.ToUInt32(buffer, 8);
        var localizedStringSize = BitConverter.ToUInt32(buffer, 12);
        var entryCount = BitConverter.ToUInt32(buffer, 16);
        var offsetToLocalizedString = BitConverter.ToUInt32(buffer, 20);
        var offsetToKeyList = BitConverter.ToUInt32(buffer, 24);
        var offsetToResourceList = BitConverter.ToUInt32(buffer, 28);
        erf.BuildYear = BitConverter.ToUInt32(buffer, 32);
        erf.BuildDay = BitConverter.ToUInt32(buffer, 36);
        erf.DescriptionStrRef = BitConverter.ToUInt32(buffer, 40);
        // Bytes 44-159: Reserved (116 bytes)

        // Read localized strings
        ReadLocalizedStrings(buffer, erf, languageCount, offsetToLocalizedString, localizedStringSize);

        // Read key list and resource list together to build resource entries
        ReadResources(buffer, erf, entryCount, offsetToKeyList, offsetToResourceList);

        return erf;
    }

    private static void ReadLocalizedStrings(byte[] buffer, ErfFile erf, uint count, uint offset, uint totalSize)
    {
        var currentOffset = (int)offset;
        var endOffset = (int)(offset + totalSize);

        for (uint i = 0; i < count; i++)
        {
            if (currentOffset + 8 > buffer.Length || currentOffset >= endOffset)
                break;

            var languageId = BitConverter.ToUInt32(buffer, currentOffset);
            var stringSize = BitConverter.ToUInt32(buffer, currentOffset + 4);
            currentOffset += 8;

            var text = string.Empty;
            if (stringSize > 0 && currentOffset + stringSize <= buffer.Length)
            {
                text = Encoding.UTF8.GetString(buffer, currentOffset, (int)stringSize).TrimEnd('\0');
                currentOffset += (int)stringSize;
            }

            erf.LocalizedStrings.Add(new ErfLocalizedString
            {
                LanguageId = languageId,
                Text = text
            });
        }
    }

    private static void ReadResources(byte[] buffer, ErfFile erf, uint count, uint keyListOffset, uint resourceListOffset)
    {
        for (uint i = 0; i < count; i++)
        {
            var keyOffset = (int)(keyListOffset + (i * KeyEntrySize));
            var resOffset = (int)(resourceListOffset + (i * ResourceEntrySize));

            if (keyOffset + KeyEntrySize > buffer.Length)
                throw new InvalidDataException($"Key entry {i} extends beyond file boundary");

            if (resOffset + ResourceEntrySize > buffer.Length)
                throw new InvalidDataException($"Resource entry {i} extends beyond file boundary");

            // Read key entry - trim nulls and any garbage after first null
            var rawResRef = Encoding.ASCII.GetString(buffer, keyOffset, 16);
            var nullIndex = rawResRef.IndexOf('\0');
            var resRef = nullIndex >= 0 ? rawResRef.Substring(0, nullIndex) : rawResRef;
            var resId = BitConverter.ToUInt32(buffer, keyOffset + 16);
            var resourceType = BitConverter.ToUInt16(buffer, keyOffset + 20);
            // Bytes 22-23: Unused (2 bytes)

            // Read resource list entry
            var dataOffset = BitConverter.ToUInt32(buffer, resOffset);
            var dataSize = BitConverter.ToUInt32(buffer, resOffset + 4);

            // Skip invalid resource types (per neverwinter.nim)
            if (resourceType == 0xFFFF)
                continue;

            erf.Resources.Add(new ErfResourceEntry
            {
                ResRef = resRef,
                ResId = resId,
                ResourceType = resourceType,
                Offset = dataOffset,
                Size = dataSize
            });
        }
    }

    /// <summary>
    /// Extract resource data from an ERF file buffer.
    /// </summary>
    public static byte[] ExtractResource(byte[] erfBuffer, ErfResourceEntry entry)
    {
        if (entry.Offset + entry.Size > erfBuffer.Length)
            throw new InvalidDataException($"Resource '{entry.ResRef}' extends beyond file boundary");

        var data = new byte[entry.Size];
        Array.Copy(erfBuffer, entry.Offset, data, 0, entry.Size);
        return data;
    }

    /// <summary>
    /// Extract resource data from an ERF file on disk.
    /// Uses file stream to avoid loading entire ERF into memory.
    /// </summary>
    public static byte[] ExtractResource(string erfPath, ErfResourceEntry entry)
    {
        using var fs = File.OpenRead(erfPath);
        fs.Seek(entry.Offset, SeekOrigin.Begin);
        var data = new byte[entry.Size];
        var bytesRead = fs.Read(data, 0, (int)entry.Size);
        if (bytesRead != entry.Size)
            throw new InvalidDataException($"Failed to read resource '{entry.ResRef}': expected {entry.Size} bytes, got {bytesRead}");
        return data;
    }
}
