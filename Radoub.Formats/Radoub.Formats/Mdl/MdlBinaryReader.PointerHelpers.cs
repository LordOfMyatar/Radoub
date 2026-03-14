// MDL Binary Reader - Offset conversion helpers
// Partial class for offset validation and conversion
//
// NWN compiled binary MDLs may store memory addresses or buffer-relative offsets.
// Both strategies are attempted for every pointer — order depends on heuristic.

namespace Radoub.Formats.Mdl;

public partial class MdlBinaryReader
{
    /// <summary>
    /// Whether _pointerBase looks like a real memory address (large value)
    /// vs a small/zero value that indicates offsets are likely buffer-relative.
    /// Used to decide which resolution strategy to try FIRST (both are always tried).
    /// </summary>
    private bool HasMemoryPointers => _pointerBase > _modelData.Length;

    /// <summary>
    /// Convert a pointer/offset to a model data buffer offset.
    /// Always tries both strategies (direct and base-subtraction).
    /// HasMemoryPointers controls which is tried first.
    /// </summary>
    private uint PointerToModelOffset(uint pointer)
    {
        if (pointer == 0xFFFFFFFF)
            return uint.MaxValue; // Null sentinel
        if (pointer == 0)
            return uint.MaxValue; // Null — 0 is the model header, never a valid node/array offset

        if (HasMemoryPointers)
        {
            // Try base subtraction first, then direct
            var result = TrySubtractBase(pointer, _pointerBase, (uint)_modelData.Length);
            if (result != uint.MaxValue) return result;
            if (pointer < (uint)_modelData.Length) return pointer;
        }
        else
        {
            // Try direct first, then base subtraction as fallback
            if (pointer < (uint)_modelData.Length) return pointer;
            var result = TrySubtractBase(pointer, _pointerBase, (uint)_modelData.Length);
            if (result != uint.MaxValue) return result;
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

        var rawLen = (uint)_rawData.Length;

        if (HasMemoryPointers)
        {
            // Try base subtraction first, then direct
            var rawBase = _pointerBase + (uint)_modelData.Length;
            var result = TrySubtractBase(pointer, rawBase, rawLen);
            if (result != uint.MaxValue) return result;
            if (pointer < rawLen) return pointer;
        }
        else
        {
            // Try direct first, then base subtraction as fallback
            if (pointer < rawLen) return pointer;
            var rawBase = _pointerBase + (uint)_modelData.Length;
            var result = TrySubtractBase(pointer, rawBase, rawLen);
            if (result != uint.MaxValue) return result;
        }

        return uint.MaxValue; // Out of bounds
    }

    /// <summary>
    /// Attempt to subtract a base address from a pointer and validate the result.
    /// Returns uint.MaxValue if subtraction doesn't yield a valid offset.
    /// </summary>
    private static uint TrySubtractBase(uint pointer, uint baseAddr, uint bufferLength)
    {
        if (baseAddr == 0 || pointer < baseAddr)
            return uint.MaxValue;

        var offset = pointer - baseAddr;
        return offset < bufferLength ? offset : uint.MaxValue;
    }
}
