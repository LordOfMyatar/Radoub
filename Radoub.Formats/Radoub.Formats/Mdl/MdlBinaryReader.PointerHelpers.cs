// MDL Binary Reader - Pointer conversion helpers
// Partial class for memory pointer to buffer offset conversion

namespace Radoub.Formats.Mdl;

public partial class MdlBinaryReader
{
    /// <summary>
    /// Convert a memory pointer or offset to a model data buffer offset.
    /// Binary MDL uses a mix: function pointers are memory addresses,
    /// but internal structure offsets are often buffer-relative.
    /// </summary>
    private uint PointerToModelOffset(uint pointer)
    {
        if (pointer == 0xFFFFFFFF || pointer == 0)
            return pointer; // Pass through sentinel values

        // If pointer is already a valid buffer offset (less than buffer size and less than base),
        // use it directly - these are buffer-relative offsets
        if (pointer < _pointerBase && pointer < _modelData.Length)
            return pointer;

        // Otherwise, treat as a memory pointer and subtract base address
        if (pointer >= _pointerBase)
        {
            var bufferOffset = pointer - _pointerBase;
            if (bufferOffset < _modelData.Length)
                return bufferOffset;
        }

        return uint.MaxValue; // Invalid
    }

    /// <summary>
    /// Convert a memory pointer or offset to a raw data buffer offset.
    /// 0xFFFFFFFF means "not present" (null pointer).
    /// 0 is a valid offset meaning "start of raw data buffer".
    /// </summary>
    private uint PointerToRawOffset(uint pointer)
    {
        // 0xFFFFFFFF is the null/invalid sentinel - pass through
        if (pointer == 0xFFFFFFFF)
            return pointer;

        // 0 is a VALID offset meaning "start of raw data buffer"
        // (unlike model data pointers which may use 0 as null)

        if (_rawDataFileOffset == 0 || _rawData.Length == 0)
            return uint.MaxValue; // No raw data section

        // If pointer is already a valid raw buffer offset (small number),
        // use it directly
        if (pointer < (uint)_rawData.Length)
            return pointer;

        // Otherwise, treat as a memory pointer and subtract raw base address
        var rawBase = _pointerBase + (uint)_modelData.Length;
        if (pointer >= rawBase)
        {
            var bufferOffset = pointer - rawBase;
            if (bufferOffset < (uint)_rawData.Length)
                return bufferOffset;
        }

        return uint.MaxValue; // Invalid
    }
}
