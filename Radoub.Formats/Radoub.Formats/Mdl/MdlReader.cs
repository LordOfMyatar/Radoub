// Unified MDL reader that auto-detects binary vs ASCII format

namespace Radoub.Formats.Mdl;

/// <summary>
/// Unified MDL reader that automatically detects binary vs ASCII format.
/// </summary>
public class MdlReader
{
    // MdlBinaryReader / MdlAsciiReader hold per-parse mutable state (BinaryReader, pointer base,
    // raw-data buffers). Reusing one instance across threads corrupts that state — #1485's
    // background model loader raced the main render's parse, truncating the node tree (#2510).
    // Construct a fresh reader per Parse so each call has its own state and Parse is thread-safe.

    /// <summary>
    /// Parse an MDL file from a stream, auto-detecting format.
    /// </summary>
    public MdlModel Parse(Stream stream)
    {
        if (MdlBinaryReader.IsBinaryMdl(stream))
        {
            return new MdlBinaryReader().Parse(stream);
        }
        else
        {
            return new MdlAsciiReader().Parse(stream);
        }
    }

    /// <summary>
    /// Parse an MDL file from bytes, auto-detecting format.
    /// </summary>
    public MdlModel Parse(byte[] data)
    {
        var isBinary = MdlBinaryReader.IsBinaryMdl(data);
        Radoub.Formats.Logging.UnifiedLogger.LogApplication(
            Radoub.Formats.Logging.LogLevel.DEBUG,
            $"[MDL] MdlReader.Parse: dataLen={data.Length}, isBinary={isBinary}, first4bytes=0x{(data.Length >= 4 ? BitConverter.ToUInt32(data, 0) : 0):X8}");

        if (isBinary)
        {
            return new MdlBinaryReader().Parse(data);
        }
        else
        {
            using var stream = new MemoryStream(data);
            return new MdlAsciiReader().Parse(stream);
        }
    }

    /// <summary>
    /// Parse an MDL file from a file path, auto-detecting format.
    /// </summary>
    public MdlModel ParseFile(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        return Parse(stream);
    }

    /// <summary>
    /// Check if data is binary MDL format.
    /// </summary>
    public static bool IsBinary(Stream stream) => MdlBinaryReader.IsBinaryMdl(stream);

    /// <summary>
    /// Check if data is binary MDL format.
    /// </summary>
    public static bool IsBinary(byte[] data) => MdlBinaryReader.IsBinaryMdl(data);
}
