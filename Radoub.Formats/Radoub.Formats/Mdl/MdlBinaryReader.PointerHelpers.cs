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

        if (pointer < (uint)_rawData.Length)
            return pointer;

        return uint.MaxValue; // Out of bounds
    }
}
