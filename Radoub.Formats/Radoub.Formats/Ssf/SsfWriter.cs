using System.Text;
using Radoub.Formats.Logging;

namespace Radoub.Formats.Ssf;

/// <summary>
/// Writes Sound Set Files (SSF) to binary data.
/// Mirrors <see cref="SsfReader"/> semantics: 40-byte header, an entry offset
/// table, then 20-byte entries (16-byte null-padded ResRef + 4-byte StringRef),
/// all ASCII-encoded. See BioWare Aurora SSF Format documentation for details.
/// </summary>
public static class SsfWriter
{
    private const int HeaderSize = 40;
    private const int OffsetEntrySize = 4;   // uint32 per entry in the offset table
    private const int ResRefSize = 16;        // null-padded ResRef
    private const int EntrySize = ResRefSize + 4; // ResRef + 4-byte StringRef

    /// <summary>
    /// Write an SSF file to a byte array.
    /// </summary>
    /// <param name="ssf">The sound set file to serialize.</param>
    /// <returns>Raw SSF file bytes.</returns>
    public static byte[] Write(SsfFile ssf)
    {
        ArgumentNullException.ThrowIfNull(ssf);

        int entryCount = ssf.Entries.Count;
        int tableOffset = HeaderSize;
        int entriesStart = tableOffset + entryCount * OffsetEntrySize;

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.ASCII);

        // Header
        writer.Write("SSF ".ToCharArray());
        writer.Write(NormalizeVersion(ssf.Version).ToCharArray());
        writer.Write((uint)entryCount);
        writer.Write((uint)tableOffset);
        writer.Write(new byte[24]); // padding

        // Entry offset table
        for (int i = 0; i < entryCount; i++)
        {
            writer.Write((uint)(entriesStart + i * EntrySize));
        }

        // Entries: 16-byte null-padded ResRef + 4-byte StringRef
        foreach (var entry in ssf.Entries)
        {
            WriteResRef(writer, entry.ResRef);
            writer.Write(entry.StringRef);
        }

        writer.Flush();
        UnifiedLogger.LogParser(LogLevel.DEBUG, $"Wrote SSF with {entryCount} entries");
        return stream.ToArray();
    }

    /// <summary>
    /// Write an SSF file to a stream.
    /// </summary>
    public static void Write(SsfFile ssf, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var bytes = Write(ssf);
        stream.Write(bytes, 0, bytes.Length);
    }

    /// <summary>
    /// Write an SSF file to a file path.
    /// </summary>
    public static void Write(SsfFile ssf, string filePath)
    {
        var bytes = Write(ssf);
        File.WriteAllBytes(filePath, bytes);
    }

    /// <summary>
    /// Writes a ResRef as 16 ASCII bytes, null-padded. Values longer than
    /// 16 characters are truncated to match the fixed-width slot the reader expects.
    /// </summary>
    private static void WriteResRef(BinaryWriter writer, string resRef)
    {
        var buffer = new byte[ResRefSize];
        if (!string.IsNullOrEmpty(resRef))
        {
            var encoded = Encoding.ASCII.GetBytes(resRef);
            int length = Math.Min(encoded.Length, ResRefSize);
            Array.Copy(encoded, buffer, length);
        }
        writer.Write(buffer);
    }

    /// <summary>
    /// The version field is a fixed 4-char slot in the header. Pad or truncate to
    /// keep the header at exactly 40 bytes if a caller supplies an off-spec value.
    /// </summary>
    private static string NormalizeVersion(string version)
    {
        version ??= "V1.0";
        if (version.Length == 4) return version;
        if (version.Length > 4) return version.Substring(0, 4);
        return version.PadRight(4, '\0');
    }
}
