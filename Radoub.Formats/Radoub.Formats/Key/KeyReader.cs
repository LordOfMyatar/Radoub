using System.Text;

namespace Radoub.Formats.Key;

/// <summary>
/// Reads KEY files from binary format.
/// Reference: BioWare Aurora KEY format spec, neverwinter.nim key.nim
/// </summary>
public static class KeyReader
{
    private const int HeaderSize = 64;
    private const int BifEntrySize = 12;
    private const int KeyEntrySize = 22;

    /// <summary>
    /// Read a KEY file from a file path.
    /// </summary>
    public static KeyFile Read(string filePath)
    {
        var buffer = File.ReadAllBytes(filePath);
        return Read(buffer);
    }

    /// <summary>
    /// Read a KEY file from a stream.
    /// </summary>
    public static KeyFile Read(Stream stream)
    {
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return Read(ms.ToArray());
    }

    /// <summary>
    /// Read a KEY file from a byte buffer.
    /// </summary>
    public static KeyFile Read(byte[] buffer)
    {
        if (buffer.Length < HeaderSize)
            throw new InvalidDataException($"KEY file too small: {buffer.Length} bytes, minimum {HeaderSize}");

        var key = new KeyFile();

        // Read header
        key.FileType = Encoding.ASCII.GetString(buffer, 0, 4);
        key.FileVersion = Encoding.ASCII.GetString(buffer, 4, 4);

        if (key.FileType != "KEY ")
            throw new InvalidDataException($"Invalid KEY file type: '{key.FileType}', expected 'KEY '");

        if (key.FileVersion != "V1  ")
            throw new InvalidDataException($"Unsupported KEY version: '{key.FileVersion}', expected 'V1  '");

        var bifCount = BitConverter.ToUInt32(buffer, 8);
        var keyCount = BitConverter.ToUInt32(buffer, 12);
        var fileTableOffset = BitConverter.ToUInt32(buffer, 16);
        var keyTableOffset = BitConverter.ToUInt32(buffer, 20);
        // Bytes 24-31: Build year/day (ignored)
        // Bytes 32-63: Reserved

        // Read BIF file table
        ReadBifEntries(buffer, key, bifCount, fileTableOffset);

        // Read key (resource) entries
        ReadKeyEntries(buffer, key, keyCount, keyTableOffset);

        return key;
    }

    private static void ReadBifEntries(byte[] buffer, KeyFile key, uint count, uint offset)
    {
        // Calculate filename table offset (after BIF entries)
        var filenameTableOffset = offset + (count * BifEntrySize);

        for (uint i = 0; i < count; i++)
        {
            var entryOffset = (int)(offset + (i * BifEntrySize));

            if (entryOffset + BifEntrySize > buffer.Length)
                throw new InvalidDataException($"BIF entry {i} extends beyond file boundary");

            var entry = new KeyBifEntry
            {
                FileSize = BitConverter.ToUInt32(buffer, entryOffset),
                FilenameOffset = BitConverter.ToUInt32(buffer, entryOffset + 4),
                FilenameLength = BitConverter.ToUInt16(buffer, entryOffset + 8),
                Drives = BitConverter.ToUInt16(buffer, entryOffset + 10)
            };

            // Read filename from filename table
            // FilenameOffset is relative to start of file
            var nameOffset = (int)entry.FilenameOffset;
            var nameLength = entry.FilenameLength;

            if (nameOffset + nameLength <= buffer.Length)
            {
                entry.Filename = Encoding.ASCII.GetString(buffer, nameOffset, nameLength).TrimEnd('\0');
                // Normalize path separators
                entry.Filename = entry.Filename.Replace('\\', Path.DirectorySeparatorChar);
            }

            key.BifEntries.Add(entry);
        }
    }

    private static void ReadKeyEntries(byte[] buffer, KeyFile key, uint count, uint offset)
    {
        for (uint i = 0; i < count; i++)
        {
            var entryOffset = (int)(offset + (i * KeyEntrySize));

            if (entryOffset + KeyEntrySize > buffer.Length)
                throw new InvalidDataException($"Key entry {i} extends beyond file boundary");

            var entry = new KeyResourceEntry
            {
                // ResRef is 16 bytes, null-padded
                ResRef = Encoding.ASCII.GetString(buffer, entryOffset, 16).TrimEnd('\0'),
                ResourceType = BitConverter.ToUInt16(buffer, entryOffset + 16),
                ResId = BitConverter.ToUInt32(buffer, entryOffset + 18)
            };

            key.ResourceEntries.Add(entry);
        }
    }
}
