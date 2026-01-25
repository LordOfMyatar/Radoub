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
    /// WARNING: This loads the entire file into memory. For large files (HAKs),
    /// use ReadMetadataOnly() instead, then ExtractResource() for specific resources.
    /// </summary>
    public static ErfFile Read(string filePath)
    {
        var buffer = File.ReadAllBytes(filePath);
        return Read(buffer);
    }

    /// <summary>
    /// Read only the metadata (header, localized strings, resource list) from an ERF file.
    /// Does NOT load resource data into memory - suitable for large HAK files.
    /// Use ExtractResource(string erfPath, ErfResourceEntry entry) to get resource data on demand.
    /// </summary>
    public static ErfFile ReadMetadataOnly(string filePath)
    {
        using var fs = File.OpenRead(filePath);
        return ReadMetadataOnly(fs);
    }

    /// <summary>
    /// Read only the metadata from an ERF stream.
    /// </summary>
    public static ErfFile ReadMetadataOnly(Stream stream)
    {
        var erf = new ErfFile();

        // Read header (160 bytes)
        var header = new byte[HeaderSize];
        var bytesRead = stream.Read(header, 0, HeaderSize);
        if (bytesRead < HeaderSize)
            throw new InvalidDataException($"ERF file too small: {bytesRead} bytes, minimum {HeaderSize}");

        erf.FileType = Encoding.ASCII.GetString(header, 0, 4);
        erf.FileVersion = Encoding.ASCII.GetString(header, 4, 4);

        if (!ValidFileTypes.Contains(erf.FileType))
            throw new InvalidDataException($"Invalid ERF file type: '{erf.FileType}', expected one of: {string.Join(", ", ValidFileTypes)}");

        if (erf.FileVersion != "V1.0")
            throw new InvalidDataException($"Unsupported ERF version: '{erf.FileVersion}', expected 'V1.0'");

        var languageCount = BitConverter.ToUInt32(header, 8);
        var localizedStringSize = BitConverter.ToUInt32(header, 12);
        var entryCount = BitConverter.ToUInt32(header, 16);
        var offsetToLocalizedString = BitConverter.ToUInt32(header, 20);
        var offsetToKeyList = BitConverter.ToUInt32(header, 24);
        var offsetToResourceList = BitConverter.ToUInt32(header, 28);
        erf.BuildYear = BitConverter.ToUInt32(header, 32);
        erf.BuildDay = BitConverter.ToUInt32(header, 36);
        erf.DescriptionStrRef = BitConverter.ToUInt32(header, 40);

        // Read localized strings (if any)
        if (languageCount > 0 && localizedStringSize > 0)
        {
            stream.Seek(offsetToLocalizedString, SeekOrigin.Begin);
            var localizedBuffer = new byte[localizedStringSize];
            stream.ReadExactly(localizedBuffer, 0, (int)Math.Min(localizedStringSize, int.MaxValue));
            ReadLocalizedStringsFromBuffer(localizedBuffer, erf, languageCount);
        }

        // Calculate size needed for key list and resource list
        var keyListSize = entryCount * KeyEntrySize;
        var resourceListSize = entryCount * ResourceEntrySize;

        // Read key list
        stream.Seek(offsetToKeyList, SeekOrigin.Begin);
        var keyListBuffer = new byte[keyListSize];
        stream.ReadExactly(keyListBuffer, 0, (int)keyListSize);

        // Read resource list
        stream.Seek(offsetToResourceList, SeekOrigin.Begin);
        var resourceListBuffer = new byte[resourceListSize];
        stream.ReadExactly(resourceListBuffer, 0, (int)resourceListSize);

        // Parse resources from the buffers
        ReadResourcesFromBuffers(keyListBuffer, resourceListBuffer, erf, entryCount);

        return erf;
    }

    private static void ReadLocalizedStringsFromBuffer(byte[] buffer, ErfFile erf, uint count)
    {
        var currentOffset = 0;

        for (uint i = 0; i < count; i++)
        {
            if (currentOffset + 8 > buffer.Length)
                break;

            var languageId = BitConverter.ToUInt32(buffer, currentOffset);
            var stringSize = BitConverter.ToUInt32(buffer, currentOffset + 4);
            currentOffset += 8;

            var text = string.Empty;
            if (stringSize > 0 && currentOffset + (int)stringSize <= buffer.Length)
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

    private static void ReadResourcesFromBuffers(byte[] keyListBuffer, byte[] resourceListBuffer, ErfFile erf, uint count)
    {
        for (uint i = 0; i < count; i++)
        {
            var keyOffset = (int)(i * KeyEntrySize);
            var resOffset = (int)(i * ResourceEntrySize);

            if (keyOffset + KeyEntrySize > keyListBuffer.Length)
                break;
            if (resOffset + ResourceEntrySize > resourceListBuffer.Length)
                break;

            // Read key entry - trim nulls and spaces (some tools use space-padding)
            var rawResRef = Encoding.ASCII.GetString(keyListBuffer, keyOffset, 16);
            var nullIndex = rawResRef.IndexOf('\0');
            var resRef = nullIndex >= 0 ? rawResRef.Substring(0, nullIndex) : rawResRef.TrimEnd();
            var resId = BitConverter.ToUInt32(keyListBuffer, keyOffset + 16);
            var resourceType = BitConverter.ToUInt16(keyListBuffer, keyOffset + 20);

            // Read resource list entry
            var dataOffset = BitConverter.ToUInt32(resourceListBuffer, resOffset);
            var dataSize = BitConverter.ToUInt32(resourceListBuffer, resOffset + 4);

            // Skip invalid resource types
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
        // Validate offsets before casting to avoid integer overflow
        if (offset > int.MaxValue)
            throw new InvalidDataException($"Localized string offset {offset} exceeds maximum supported value");

        long endOffsetLong = (long)offset + totalSize;
        if (endOffsetLong > buffer.Length)
            throw new InvalidDataException($"Localized strings extend beyond file boundary");

        var currentOffset = (int)offset;
        var endOffset = (int)Math.Min(endOffsetLong, int.MaxValue);

        for (uint i = 0; i < count; i++)
        {
            if (currentOffset + 8 > buffer.Length || currentOffset >= endOffset)
                break;

            var languageId = BitConverter.ToUInt32(buffer, currentOffset);
            var stringSize = BitConverter.ToUInt32(buffer, currentOffset + 4);
            currentOffset += 8;

            var text = string.Empty;
            if (stringSize > 0 && stringSize <= int.MaxValue && currentOffset + (int)stringSize <= buffer.Length)
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
        // Validate base offsets before loop to detect overflow early
        if (keyListOffset > int.MaxValue)
            throw new InvalidDataException($"Key list offset {keyListOffset} exceeds maximum supported value");
        if (resourceListOffset > int.MaxValue)
            throw new InvalidDataException($"Resource list offset {resourceListOffset} exceeds maximum supported value");

        for (uint i = 0; i < count; i++)
        {
            // Use long arithmetic to detect overflow before casting to int
            long keyOffsetLong = (long)keyListOffset + ((long)i * KeyEntrySize);
            long resOffsetLong = (long)resourceListOffset + ((long)i * ResourceEntrySize);

            if (keyOffsetLong > int.MaxValue)
                throw new InvalidDataException($"Key entry {i} offset {keyOffsetLong} exceeds maximum supported value");
            if (resOffsetLong > int.MaxValue)
                throw new InvalidDataException($"Resource entry {i} offset {resOffsetLong} exceeds maximum supported value");

            var keyOffset = (int)keyOffsetLong;
            var resOffset = (int)resOffsetLong;

            if (keyOffset + KeyEntrySize > buffer.Length)
                throw new InvalidDataException($"Key entry {i} extends beyond file boundary");

            if (resOffset + ResourceEntrySize > buffer.Length)
                throw new InvalidDataException($"Resource entry {i} extends beyond file boundary");

            // Read key entry - trim nulls and spaces (some tools use space-padding)
            var rawResRef = Encoding.ASCII.GetString(buffer, keyOffset, 16);
            var nullIndex = rawResRef.IndexOf('\0');
            var resRef = nullIndex >= 0 ? rawResRef.Substring(0, nullIndex) : rawResRef.TrimEnd();
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
        // Validate size before allocation to prevent overflow
        if (entry.Size > int.MaxValue)
            throw new InvalidDataException($"Resource '{entry.ResRef}' size {entry.Size} exceeds maximum supported value");

        using var fs = File.OpenRead(erfPath);
        fs.Seek(entry.Offset, SeekOrigin.Begin);
        var data = new byte[entry.Size];
        var bytesRead = fs.Read(data, 0, (int)entry.Size);
        if (bytesRead != entry.Size)
            throw new InvalidDataException($"Failed to read resource '{entry.ResRef}': expected {entry.Size} bytes, got {bytesRead}");
        return data;
    }
}
