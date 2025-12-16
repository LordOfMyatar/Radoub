using System.Text;

namespace Radoub.Formats.Bif;

/// <summary>
/// Reads BIF archive files.
/// Reference: BioWare Aurora BIF format spec, neverwinter.nim bif.nim
/// </summary>
public static class BifReader
{
    private const int HeaderSize = 20;
    private const int VariableResourceEntrySize = 16;
    private const int FixedResourceEntrySize = 20;

    /// <summary>
    /// Read a BIF file from a file path.
    /// WARNING: This loads the entire file into memory. For large files,
    /// use ReadMetadataOnly() instead, then ExtractResource() for specific resources.
    /// </summary>
    /// <param name="filePath">Path to the BIF file.</param>
    /// <param name="keepBuffer">If true, keeps the raw buffer for resource extraction.</param>
    public static BifFile Read(string filePath, bool keepBuffer = true)
    {
        var buffer = File.ReadAllBytes(filePath);
        var bif = Read(buffer, keepBuffer);
        bif.SourcePath = filePath; // Store for on-demand extraction
        return bif;
    }

    /// <summary>
    /// Read only the metadata (header, resource tables) from a BIF file.
    /// Does NOT load resource data into memory - suitable for large BIF files.
    /// Use ExtractResource(string bifPath, BifVariableResource resource) to get resource data on demand.
    /// </summary>
    public static BifFile ReadMetadataOnly(string filePath)
    {
        using var fs = File.OpenRead(filePath);
        var bif = ReadMetadataOnly(fs);
        bif.SourcePath = filePath; // Store for on-demand extraction
        return bif;
    }

    /// <summary>
    /// Read only the metadata from a BIF stream.
    /// </summary>
    public static BifFile ReadMetadataOnly(Stream stream)
    {
        var bif = new BifFile();

        // Read header (20 bytes)
        var header = new byte[HeaderSize];
        var bytesRead = stream.Read(header, 0, HeaderSize);
        if (bytesRead < HeaderSize)
            throw new InvalidDataException($"BIF file too small: {bytesRead} bytes, minimum {HeaderSize}");

        bif.FileType = Encoding.ASCII.GetString(header, 0, 4);
        bif.FileVersion = Encoding.ASCII.GetString(header, 4, 4);

        if (bif.FileType != "BIFF")
            throw new InvalidDataException($"Invalid BIF file type: '{bif.FileType}', expected 'BIFF'");

        if (bif.FileVersion != "V1  ")
            throw new InvalidDataException($"Unsupported BIF version: '{bif.FileVersion}', expected 'V1  '");

        var variableResourceCount = BitConverter.ToUInt32(header, 8);
        var fixedResourceCount = BitConverter.ToUInt32(header, 12);
        var variableTableOffset = BitConverter.ToUInt32(header, 16);

        // Fixed table follows variable table
        var fixedTableOffset = variableTableOffset + (variableResourceCount * VariableResourceEntrySize);

        // Read variable resource table
        var variableTableSize = variableResourceCount * VariableResourceEntrySize;
        stream.Seek(variableTableOffset, SeekOrigin.Begin);
        var variableBuffer = new byte[variableTableSize];
        stream.ReadExactly(variableBuffer, 0, (int)variableTableSize);
        ReadVariableResourcesFromBuffer(variableBuffer, bif, variableResourceCount);

        // Read fixed resource table
        if (fixedResourceCount > 0)
        {
            var fixedTableSize = fixedResourceCount * FixedResourceEntrySize;
            stream.Seek(fixedTableOffset, SeekOrigin.Begin);
            var fixedBuffer = new byte[fixedTableSize];
            stream.ReadExactly(fixedBuffer, 0, (int)fixedTableSize);
            ReadFixedResourcesFromBuffer(fixedBuffer, bif, fixedResourceCount);
        }

        return bif;
    }

    private static void ReadVariableResourcesFromBuffer(byte[] buffer, BifFile bif, uint count)
    {
        for (uint i = 0; i < count; i++)
        {
            var entryOffset = (int)(i * VariableResourceEntrySize);

            if (entryOffset + VariableResourceEntrySize > buffer.Length)
                break;

            var entry = new BifVariableResource
            {
                Id = BitConverter.ToUInt32(buffer, entryOffset),
                Offset = BitConverter.ToUInt32(buffer, entryOffset + 4),
                FileSize = BitConverter.ToUInt32(buffer, entryOffset + 8),
                ResourceType = BitConverter.ToUInt32(buffer, entryOffset + 12)
            };

            bif.VariableResources.Add(entry);
        }
    }

    private static void ReadFixedResourcesFromBuffer(byte[] buffer, BifFile bif, uint count)
    {
        for (uint i = 0; i < count; i++)
        {
            var entryOffset = (int)(i * FixedResourceEntrySize);

            if (entryOffset + FixedResourceEntrySize > buffer.Length)
                break;

            var entry = new BifFixedResource
            {
                Id = BitConverter.ToUInt32(buffer, entryOffset),
                Offset = BitConverter.ToUInt32(buffer, entryOffset + 4),
                PartCount = BitConverter.ToUInt32(buffer, entryOffset + 8),
                PartSize = BitConverter.ToUInt32(buffer, entryOffset + 12),
                ResourceType = BitConverter.ToUInt32(buffer, entryOffset + 16)
            };

            bif.FixedResources.Add(entry);
        }
    }

    /// <summary>
    /// Extract a variable resource's data from a BIF file on disk.
    /// Uses file stream to avoid loading entire BIF into memory.
    /// </summary>
    public static byte[] ExtractResource(string bifPath, BifVariableResource resource)
    {
        if (resource.FileSize > int.MaxValue)
            throw new InvalidDataException($"Resource size {resource.FileSize} exceeds maximum supported value");

        using var fs = File.OpenRead(bifPath);
        fs.Seek(resource.Offset, SeekOrigin.Begin);
        var data = new byte[resource.FileSize];
        var bytesRead = fs.Read(data, 0, (int)resource.FileSize);
        if (bytesRead != resource.FileSize)
            throw new InvalidDataException($"Failed to read resource: expected {resource.FileSize} bytes, got {bytesRead}");
        return data;
    }

    /// <summary>
    /// Read a BIF file from a stream.
    /// </summary>
    /// <param name="stream">Stream containing BIF data.</param>
    /// <param name="keepBuffer">If true, keeps the raw buffer for resource extraction.</param>
    public static BifFile Read(Stream stream, bool keepBuffer = true)
    {
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return Read(ms.ToArray(), keepBuffer);
    }

    /// <summary>
    /// Read a BIF file from a byte buffer.
    /// </summary>
    /// <param name="buffer">Byte buffer containing BIF data.</param>
    /// <param name="keepBuffer">If true, keeps the raw buffer for resource extraction.</param>
    public static BifFile Read(byte[] buffer, bool keepBuffer = true)
    {
        if (buffer.Length < HeaderSize)
            throw new InvalidDataException($"BIF file too small: {buffer.Length} bytes, minimum {HeaderSize}");

        var bif = new BifFile();

        // Read header
        bif.FileType = Encoding.ASCII.GetString(buffer, 0, 4);
        bif.FileVersion = Encoding.ASCII.GetString(buffer, 4, 4);

        if (bif.FileType != "BIFF")
            throw new InvalidDataException($"Invalid BIF file type: '{bif.FileType}', expected 'BIFF'");

        if (bif.FileVersion != "V1  ")
            throw new InvalidDataException($"Unsupported BIF version: '{bif.FileVersion}', expected 'V1  '");

        var variableResourceCount = BitConverter.ToUInt32(buffer, 8);
        var fixedResourceCount = BitConverter.ToUInt32(buffer, 12);
        var variableTableOffset = BitConverter.ToUInt32(buffer, 16);

        // Fixed table follows variable table
        var fixedTableOffset = variableTableOffset + (variableResourceCount * VariableResourceEntrySize);

        // Read variable resource table
        ReadVariableResources(buffer, bif, variableResourceCount, variableTableOffset);

        // Read fixed resource table
        ReadFixedResources(buffer, bif, fixedResourceCount, fixedTableOffset);

        // Keep buffer for extraction if requested
        if (keepBuffer)
            bif.RawBuffer = buffer;

        return bif;
    }

    private static void ReadVariableResources(byte[] buffer, BifFile bif, uint count, uint offset)
    {
        for (uint i = 0; i < count; i++)
        {
            var entryOffset = (int)(offset + (i * VariableResourceEntrySize));

            if (entryOffset + VariableResourceEntrySize > buffer.Length)
                throw new InvalidDataException($"Variable resource entry {i} extends beyond file boundary");

            var entry = new BifVariableResource
            {
                Id = BitConverter.ToUInt32(buffer, entryOffset),
                Offset = BitConverter.ToUInt32(buffer, entryOffset + 4),
                FileSize = BitConverter.ToUInt32(buffer, entryOffset + 8),
                ResourceType = BitConverter.ToUInt32(buffer, entryOffset + 12)
            };

            bif.VariableResources.Add(entry);
        }
    }

    private static void ReadFixedResources(byte[] buffer, BifFile bif, uint count, uint offset)
    {
        for (uint i = 0; i < count; i++)
        {
            var entryOffset = (int)(offset + (i * FixedResourceEntrySize));

            if (entryOffset + FixedResourceEntrySize > buffer.Length)
                throw new InvalidDataException($"Fixed resource entry {i} extends beyond file boundary");

            var entry = new BifFixedResource
            {
                Id = BitConverter.ToUInt32(buffer, entryOffset),
                Offset = BitConverter.ToUInt32(buffer, entryOffset + 4),
                PartCount = BitConverter.ToUInt32(buffer, entryOffset + 8),
                PartSize = BitConverter.ToUInt32(buffer, entryOffset + 12),
                ResourceType = BitConverter.ToUInt32(buffer, entryOffset + 16)
            };

            bif.FixedResources.Add(entry);
        }
    }
}
