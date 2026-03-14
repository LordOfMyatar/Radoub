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
    /// Whether _pointerBase looks like a real memory address (large value)
    /// vs a small/zero value that indicates offsets are already buffer-relative.
    /// NWN compiled binary MDLs store memory addresses from the compilation step.
    /// Threshold: if base > buffer size, it's clearly a memory address.
    /// </summary>
    private bool HasMemoryPointers => _pointerBase > _modelData.Length;

    /// <summary>
    /// Convert a pointer/offset to a model data buffer offset.
    /// When _pointerBase is a large memory address, pointers need base subtraction.
    /// When _pointerBase is small/zero (some models), offsets are already buffer-relative.
    /// </summary>
    private uint PointerToModelOffset(uint pointer)
    {
        if (pointer == 0xFFFFFFFF)
            return uint.MaxValue; // Null sentinel
        if (pointer == 0)
            return uint.MaxValue; // Null — 0 is the model header, never a valid node/array offset

        if (HasMemoryPointers)
        {
            // Model uses memory pointers — subtract base first
            if (pointer >= _pointerBase)
            {
                var bufferOffset = pointer - _pointerBase;
                if (bufferOffset < _modelData.Length)
                    return bufferOffset;
            }
            // Fallback: maybe this particular offset is already buffer-relative
            if (pointer < _modelData.Length)
                return pointer;
        }
        else
        {
            // Model uses buffer-relative offsets directly
            if (pointer < _modelData.Length)
                return pointer;
        }

        return uint.MaxValue; // Out of bounds
    }

    /// <summary>
    /// Convert a pointer/offset to a raw data buffer offset.
    /// 0xFFFFFFFF means "not present". 0 IS a valid offset (start of raw data).
    /// </summary>
    private uint PointerToRawOffset(uint pointer)
    {
        if (pointer == 0xFFFFFFFF)
            return uint.MaxValue;

        if (_rawDataFileOffset == 0 || _rawData.Length == 0)
            return uint.MaxValue; // No raw data section

        if (HasMemoryPointers)
        {
            // Model uses memory pointers — subtract raw base first
            var rawBase = _pointerBase + (uint)_modelData.Length;
            if (pointer >= rawBase)
            {
                var bufferOffset = pointer - rawBase;
                if (bufferOffset < (uint)_rawData.Length)
                    return bufferOffset;
            }
            // Fallback: maybe this particular offset is already buffer-relative
            if (pointer < (uint)_rawData.Length)
                return pointer;
        }
        else
        {
            // Model uses buffer-relative offsets directly
            if (pointer < (uint)_rawData.Length)
                return pointer;
        }

        return uint.MaxValue; // Out of bounds
    }
}
