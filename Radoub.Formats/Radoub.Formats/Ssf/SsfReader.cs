using System.Text;
using Radoub.Formats.Logging;

namespace Radoub.Formats.Ssf;

/// <summary>
/// Reads Sound Set Files (SSF) from binary data.
/// SSF files contain sound references for creature/character actions.
/// </summary>
public static class SsfReader
{
    /// <summary>
    /// Reads an SSF file from binary data.
    /// </summary>
    /// <param name="data">Raw SSF file bytes.</param>
    /// <returns>Parsed SsfFile, or null if parsing fails.</returns>
    public static SsfFile? Read(byte[] data)
    {
        if (data == null || data.Length < 40)
        {
            UnifiedLogger.LogParser(LogLevel.WARN, "SSF data is null or too short");
            return null;
        }

        try
        {
            using var stream = new MemoryStream(data);
            using var reader = new BinaryReader(stream, Encoding.ASCII);

            // Read header
            var fileType = new string(reader.ReadChars(4));
            if (fileType != "SSF ")
            {
                UnifiedLogger.LogParser(LogLevel.WARN, $"Invalid SSF file type: '{fileType}'");
                return null;
            }

            var version = new string(reader.ReadChars(4));
            var entryCount = reader.ReadUInt32();
            var tableOffset = reader.ReadUInt32();

            // Skip padding (24 bytes)
            reader.ReadBytes(24);

            var ssf = new SsfFile { Version = version };

            // Read entry offsets
            stream.Position = tableOffset;
            var entryOffsets = new uint[entryCount];
            for (int i = 0; i < entryCount; i++)
            {
                entryOffsets[i] = reader.ReadUInt32();
            }

            // Read each entry
            for (int i = 0; i < entryCount; i++)
            {
                stream.Position = entryOffsets[i];

                var entry = new SsfEntry
                {
                    // ResRef is 16 characters, null-padded
                    ResRef = new string(reader.ReadChars(16)).TrimEnd('\0'),
                    StringRef = reader.ReadUInt32()
                };

                ssf.Entries.Add(entry);
            }

            UnifiedLogger.LogParser(LogLevel.DEBUG, $"Read SSF with {ssf.Entries.Count} entries");
            return ssf;
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogParser(LogLevel.ERROR, $"Failed to parse SSF: {ex.Message}");
            return null;
        }
    }
}
