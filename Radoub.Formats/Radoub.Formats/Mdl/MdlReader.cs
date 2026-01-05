// Unified MDL reader that auto-detects binary vs ASCII format

namespace Radoub.Formats.Mdl;

/// <summary>
/// Unified MDL reader that automatically detects binary vs ASCII format.
/// </summary>
public class MdlReader
{
    private readonly MdlAsciiReader _asciiReader = new();
    private readonly MdlBinaryReader _binaryReader = new();

    /// <summary>
    /// Parse an MDL file from a stream, auto-detecting format.
    /// </summary>
    public MdlModel Parse(Stream stream)
    {
        if (MdlBinaryReader.IsBinaryMdl(stream))
        {
            return _binaryReader.Parse(stream);
        }
        else
        {
            return _asciiReader.Parse(stream);
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
            return _binaryReader.Parse(data);
        }
        else
        {
            using var stream = new MemoryStream(data);
            return _asciiReader.Parse(stream);
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
