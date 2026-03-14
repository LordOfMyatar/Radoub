// MDL Binary Reader - Offset conversion helpers
// Partial class for offset validation and conversion
//
// Per xoreos reference implementation, all offsets in binary MDL files are
// buffer-relative (not memory pointers). The uint32 at model data offset 0x00
// is a function pointer to be skipped, not a "pointer base" for subtraction.

namespace Radoub.Formats.Mdl;

public partial class MdlBinaryReader
{
    /// <summary>
    /// Validate a model data offset. Returns the offset if valid, uint.MaxValue if not.
    /// All offsets in MDL files are buffer-relative (per xoreos reference).
    /// </summary>
    private uint PointerToModelOffset(uint pointer)
    {
        if (pointer == 0xFFFFFFFF)
            return uint.MaxValue; // Null sentinel
        if (pointer == 0)
            return uint.MaxValue; // Null — 0 is the model header, never a valid node/array offset

        if (pointer < _modelData.Length)
            return pointer;

        // Value exceeds buffer — may be a memory pointer needing base subtraction
        if (_pointerBase > 0 && pointer >= _pointerBase)
        {
            var bufferOffset = pointer - _pointerBase;
            if (bufferOffset < _modelData.Length)
            {
                Logging.UnifiedLogger.LogApplication(Logging.LogLevel.DEBUG,
                    $"[MDL] PointerToModelOffset: 0x{pointer:X8} resolved via base subtraction (base=0x{_pointerBase:X8}) -> offset={bufferOffset}");
                return bufferOffset;
            }
        }

        Logging.UnifiedLogger.LogApplication(Logging.LogLevel.WARN,
            $"[MDL] PointerToModelOffset: 0x{pointer:X8} out of bounds (modelDataLen={_modelData.Length}, base=0x{_pointerBase:X8})");
        return uint.MaxValue; // Out of bounds
    }

    /// <summary>
    /// Validate a raw data offset. Returns the offset if valid, uint.MaxValue if not.
    /// 0xFFFFFFFF means "not present". 0 IS a valid offset (start of raw data).
    /// </summary>
    private uint PointerToRawOffset(uint pointer)
    {
        if (pointer == 0xFFFFFFFF)
            return uint.MaxValue;

        if (_rawDataFileOffset == 0 || _rawData.Length == 0)
            return uint.MaxValue; // No raw data section

        // 0 IS a valid raw data offset (start of raw data buffer)
        if (pointer < (uint)_rawData.Length)
            return pointer;

        // Value exceeds buffer — may be a memory pointer needing base subtraction
        var rawBase = _pointerBase + (uint)_modelData.Length;
        if (rawBase > 0 && pointer >= rawBase)
        {
            var bufferOffset = pointer - rawBase;
            if (bufferOffset < (uint)_rawData.Length)
            {
                Logging.UnifiedLogger.LogApplication(Logging.LogLevel.DEBUG,
                    $"[MDL] PointerToRawOffset: 0x{pointer:X8} resolved via base subtraction (rawBase=0x{rawBase:X8}) -> offset={bufferOffset}");
                return bufferOffset;
            }
        }

        Logging.UnifiedLogger.LogApplication(Logging.LogLevel.WARN,
            $"[MDL] PointerToRawOffset: 0x{pointer:X8} out of bounds (rawDataLen={_rawData.Length}, rawBase=0x{rawBase:X8})");
        return uint.MaxValue; // Out of bounds
    }
}
