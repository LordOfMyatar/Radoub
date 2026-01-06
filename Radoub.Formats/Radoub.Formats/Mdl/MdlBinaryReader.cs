// MDL Binary file format reader for Neverwinter Nights
// Based on format documentation from nwnexplorer (BSD 3-Clause) and xoreos (GPL)
// Reference implementations used for format understanding only

using System.Numerics;
using System.Text;

namespace Radoub.Formats.Mdl;

/// <summary>
/// Reads binary MDL model files.
/// Binary MDL is a compiled format used by the game engine for faster loading.
/// Split into partial classes for maintainability.
/// </summary>
public partial class MdlBinaryReader
{
    // Node type flags (from binary format)
    private const uint NodeFlagHasHeader = 0x00000001;
    private const uint NodeFlagHasLight = 0x00000002;
    private const uint NodeFlagHasEmitter = 0x00000004;
    private const uint NodeFlagHasReference = 0x00000010;
    private const uint NodeFlagHasMesh = 0x00000020;
    private const uint NodeFlagHasSkin = 0x00000040;
    private const uint NodeFlagHasAnim = 0x00000080;
    private const uint NodeFlagHasDangly = 0x00000100;
    private const uint NodeFlagHasAABB = 0x00000200;

    // Structure sizes
    private const int FileHeaderSize = 12;
    private const int GeometryHeaderSize = 0x70;
    private const int ModelHeaderSize = 0xE8;
    private const int NodeHeaderSize = 0x70;
    private const int MeshHeaderSize = 0x270;
    private const int FaceSize = 0x20;
    private const int ControllerKeySize = 0x0C;

    private BinaryReader _reader = null!;
    private byte[] _modelData = null!;
    private byte[] _rawData = null!;

    // Binary MDL files store memory pointers that need patching.
    private uint _pointerBase;
    private uint _rawDataFileOffset;

    /// <summary>
    /// Parse a binary MDL file from a stream.
    /// </summary>
    public MdlModel Parse(Stream stream)
    {
        Logging.UnifiedLogger.LogApplication(Logging.LogLevel.DEBUG, $"[MDL] Parse START: streamLen={stream.Length}");

        _reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);

        // Read file header (12 bytes)
        var zeroField = _reader.ReadUInt32();
        var rawDataOffsetInFile = _reader.ReadUInt32();
        var rawDataSize = _reader.ReadUInt32();

        if (zeroField != 0)
        {
            throw new InvalidDataException("Not a binary MDL file (first 4 bytes should be 0)");
        }

        Logging.UnifiedLogger.LogApplication(Logging.LogLevel.DEBUG,
            $"[MDL] rawDataOffset={rawDataOffsetInFile}, rawDataSize={rawDataSize}, streamLen={stream.Length}");

        var streamLength = stream.Length;

        if (rawDataOffsetInFile > streamLength)
        {
            throw new InvalidDataException($"Raw data offset {rawDataOffsetInFile} exceeds file length {streamLength}");
        }

        uint modelDataSize;
        if (rawDataOffsetInFile == 0 || rawDataOffsetInFile <= FileHeaderSize)
        {
            modelDataSize = (uint)(streamLength - FileHeaderSize);
            rawDataOffsetInFile = 0;
        }
        else
        {
            modelDataSize = rawDataOffsetInFile - FileHeaderSize;
        }

        if (modelDataSize < GeometryHeaderSize)
        {
            throw new InvalidDataException($"Model data too small: {modelDataSize} bytes (need at least {GeometryHeaderSize})");
        }

        _rawDataFileOffset = rawDataOffsetInFile;

        Logging.UnifiedLogger.LogApplication(Logging.LogLevel.DEBUG,
            $"[MDL] modelDataSize={modelDataSize}, rawDataOffset={rawDataOffsetInFile}");

        _modelData = _reader.ReadBytes((int)modelDataSize);

        if (rawDataSize > 0 && rawDataOffsetInFile > 0 && stream.Position + rawDataSize <= streamLength)
        {
            _rawData = _reader.ReadBytes((int)rawDataSize);
            Logging.UnifiedLogger.LogApplication(Logging.LogLevel.DEBUG, $"[MDL] Read rawData: {_rawData.Length} bytes");
        }
        else
        {
            _rawData = Array.Empty<byte>();
            Logging.UnifiedLogger.LogApplication(Logging.LogLevel.DEBUG, "[MDL] No raw data section");
        }

        return ParseModel();
    }

    /// <summary>
    /// Parse a binary MDL file from bytes.
    /// </summary>
    public MdlModel Parse(byte[] data)
    {
        using var stream = new MemoryStream(data);
        return Parse(stream);
    }

    /// <summary>
    /// Check if data appears to be binary MDL format.
    /// </summary>
    public static bool IsBinaryMdl(Stream stream)
    {
        var position = stream.Position;
        try
        {
            if (stream.Length < 12) return false;
            using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);
            var firstField = reader.ReadUInt32();
            return firstField == 0;
        }
        finally
        {
            stream.Position = position;
        }
    }

    /// <summary>
    /// Check if data appears to be binary MDL format.
    /// </summary>
    public static bool IsBinaryMdl(byte[] data)
    {
        if (data.Length < 12) return false;
        return BitConverter.ToUInt32(data, 0) == 0;
    }

    private MdlModel ParseModel()
    {
        var model = new MdlModel { IsBinary = true };

        using var modelStream = new MemoryStream(_modelData);
        using var reader = new BinaryReader(modelStream, Encoding.ASCII);

        if (_modelData.Length < ModelHeaderSize)
        {
            throw new InvalidDataException($"Model data too small for header: {_modelData.Length} bytes, need {ModelHeaderSize}");
        }

        _pointerBase = reader.ReadUInt32();

        Logging.UnifiedLogger.LogApplication(Logging.LogLevel.DEBUG, $"[MDL] Pointer base address: 0x{_pointerBase:X8}");

        reader.BaseStream.Position = 8;
        model.Name = ReadFixedString(reader, 64);

        reader.BaseStream.Position = 0x48;
        var rootNodeOffset = reader.ReadUInt32();
        var nodeCount = reader.ReadUInt32();

        var rootNodeBufferOffset = rootNodeOffset;

        Logging.UnifiedLogger.LogApplication(Logging.LogLevel.DEBUG,
            $"[MDL] rootNodeOffset={rootNodeOffset}, nodeCount={nodeCount}, modelDataLen={_modelData.Length}");

        reader.BaseStream.Position = 0x70;

        var flags = reader.ReadUInt16();
        model.Classification = (MdlClassification)reader.ReadByte();
        reader.ReadByte(); // fog
        reader.ReadUInt32(); // refcount

        var animArrayOffset = reader.ReadUInt32();
        var animArrayCount = reader.ReadUInt32();
        reader.ReadUInt32(); // anim array allocated
        reader.ReadUInt32(); // supermodel pointer

        model.BoundingMin = ReadVector3(reader);
        model.BoundingMax = ReadVector3(reader);
        model.Radius = reader.ReadSingle();
        model.AnimationScale = reader.ReadSingle();
        model.SuperModel = ReadFixedString(reader, 64);

        if (rootNodeBufferOffset != 0xFFFFFFFF && rootNodeBufferOffset != 0 && rootNodeBufferOffset != uint.MaxValue)
        {
            model.GeometryRoot = ParseNode(rootNodeBufferOffset);
        }

        var animArrayBufferOffset = PointerToModelOffset(animArrayOffset);
        if (animArrayCount > 0 && animArrayBufferOffset != 0xFFFFFFFF && animArrayBufferOffset != uint.MaxValue)
        {
            ParseAnimations(model, animArrayBufferOffset, (int)animArrayCount);
        }

        return model;
    }
}
