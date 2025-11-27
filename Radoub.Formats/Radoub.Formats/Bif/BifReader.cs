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
    /// </summary>
    /// <param name="filePath">Path to the BIF file.</param>
    /// <param name="keepBuffer">If true, keeps the raw buffer for resource extraction.</param>
    public static BifFile Read(string filePath, bool keepBuffer = true)
    {
        var buffer = File.ReadAllBytes(filePath);
        return Read(buffer, keepBuffer);
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
