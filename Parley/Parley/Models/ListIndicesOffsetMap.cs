using System.Collections.Generic;

namespace DialogEditor.Models
{
    /// <summary>
    /// Pre-calculated offsets for all lists in the ListIndices section.
    /// Used to set correct field offsets during field creation (no patching needed).
    /// See PARAMETER_PRESERVATION_ARCHITECTURE.md for design details.
    /// Extracted from DialogParser.cs - Phase 1 refactoring (Oct 28, 2025)
    /// </summary>
    public class ListIndicesOffsetMap
    {
        // Root lists (always present)
        public uint EntryListOffset { get; set; }
        public uint ReplyListOffset { get; set; }
        public uint StartingListOffset { get; set; }

        // Individual node lists (key = node index in Dialog.Entries/Replies)
        public Dictionary<int, uint> RepliesListOffsets { get; set; } = new Dictionary<int, uint>();
        public Dictionary<int, uint> EntriesListOffsets { get; set; } = new Dictionary<int, uint>();
        public Dictionary<int, uint> EntryActionParamsOffsets { get; set; } = new Dictionary<int, uint>();
        public Dictionary<int, uint> ReplyActionParamsOffsets { get; set; } = new Dictionary<int, uint>();

        // Pointer lists (key = global pointer index across all nodes)
        public Dictionary<int, uint> PointerConditionParamsOffsets { get; set; } = new Dictionary<int, uint>();

        // Start wrapper lists (key = start index in Dialog.Starts)
        public Dictionary<int, uint> StartConditionParamsOffsets { get; set; } = new Dictionary<int, uint>();
    }
}
