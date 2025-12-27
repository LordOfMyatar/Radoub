using System.Collections.Generic;
using DialogEditor.Models;
using DialogEditor.Services;
using Radoub.Formats.Gff;
using Radoub.Formats.Logging;
using DialogEditor.Utils;

namespace DialogEditor.Parsers
{
    /// <summary>
    /// Handles fixing GFF field indices for Aurora Engine compatibility.
    /// The Aurora Engine expects fields in a specific 4:1 pattern.
    /// Extracted from DialogParser.cs to improve maintainability.
    /// Phase 1 of parser refactoring - Oct 28, 2025
    /// </summary>
    public class GffIndexFixer
    {
        /// <summary>
        /// Fix field indices for compact pointer structs (optimized storage)
        /// </summary>
        public void FixCompactPointerStructFieldIndices(CompactPointerResult compactResult,
            List<GffStruct> allStructs, uint compactFieldStartIndex)
        {
            UnifiedLogger.LogParser(LogLevel.TRACE, $"?? FixCompactPointerStructFieldIndices: Updating {compactResult.UniquePointers.Count} compact pointer structs");

            // Calculate the base struct index where compact pointer structs start
            int compactStructStartIndex = allStructs.Count - compactResult.UniquePointers.Count;

            // Fix DataOrDataOffset for each unique pointer struct
            for (int i = 0; i < compactResult.UniquePointers.Count; i++)
            {
                int structIndex = compactStructStartIndex + i;
                var ptrStruct = allStructs[structIndex];

                // Each unique pointer gets 4 consecutive fields starting from compactFieldStartIndex
                uint fieldIndex = compactFieldStartIndex + (uint)(i * 4);
                ptrStruct.DataOrDataOffset = fieldIndex * 4; // Byte offset into field indices array

                UnifiedLogger.LogParser(LogLevel.DEBUG, $"Fixed UniquePtr[{i}] struct[{structIndex}]: DataOrDataOffset={ptrStruct.DataOrDataOffset} (field index {fieldIndex})");
            }

            UnifiedLogger.LogParser(LogLevel.TRACE, $"? Updated {compactResult.UniquePointers.Count} compact pointer struct field indices");
        }

        /// <summary>
        /// Fix field indices for direct pointer structs (standard storage)
        /// Fix DataOrDataOffset for pointer structs created with direct 1:1 export
        /// </summary>
        public void FixDirectPointerStructFieldIndices(List<GffStruct> allStructs,
            int structStartIndex, int structCount, uint fieldStartIndex)
        {
            UnifiedLogger.LogParser(LogLevel.TRACE, $"?? FixDirectPointerStructFieldIndices: Updating {structCount} pointer structs starting at struct[{structStartIndex}]");

            for (int i = 0; i < structCount; i++)
            {
                int structIndex = structStartIndex + i;
                if (structIndex < allStructs.Count)
                {
                    var ptrStruct = allStructs[structIndex];
                    uint fieldIndex = fieldStartIndex + (uint)(i * 4); // Each pointer has 4 fields
                    ptrStruct.DataOrDataOffset = fieldIndex * 4; // Byte offset into field indices array

                    UnifiedLogger.LogParser(LogLevel.DEBUG, $"Fixed Pointer struct[{structIndex}]: DataOrDataOffset={ptrStruct.DataOrDataOffset} (field index {fieldIndex})");
                }
            }

            UnifiedLogger.LogParser(LogLevel.TRACE, $"? Updated {structCount} pointer struct field indices");
        }

        /// <summary>
        /// Fix field indices for reply pointer structs
        /// </summary>
        public void FixReplyPointerStructFieldIndices(Dialog dialog, List<GffStruct> allStructs,
            List<List<int>> replyPointerStructIndices, uint replyPointerFieldStartIndex)
        {
            UnifiedLogger.LogParser(LogLevel.TRACE, $"?? FixReplyPointerStructFieldIndices: Updating struct DataOrDataOffset values (append-only method)");

            // With append-only field creation, each pointer struct gets 4 consecutive fields
            // starting from replyPointerFieldStartIndex
            uint currentFieldIndex = replyPointerFieldStartIndex;

            for (int replyIdx = 0; replyIdx < dialog.Replies.Count; replyIdx++)
            {
                var reply = dialog.Replies[replyIdx];
                if (reply.Pointers.Count > 0 && replyIdx < replyPointerStructIndices.Count)
                {
                    foreach (int structIndex in replyPointerStructIndices[replyIdx])
                    {
                        if (structIndex < allStructs.Count)
                        {
                            // Each pointer struct uses exactly 4 consecutive fields
                            uint byteOffset = currentFieldIndex * 4;
                            allStructs[structIndex].DataOrDataOffset = byteOffset;

                            // Move to next pointer struct's field range (4 fields per pointer)
                            currentFieldIndex += 4;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Fix field indices for start wrapper structs
        /// </summary>
        public void FixStartWrapperStructFieldIndices(Dialog dialog, List<GffStruct> allStructs,
            List<int> startStructIndices, uint startWrapperFieldStartIndex)
        {
            UnifiedLogger.LogParser(LogLevel.TRACE, $"?? FixStartWrapperStructFieldIndices: Updating struct DataOrDataOffset values for Start wrapper structs");

            // Each start wrapper struct gets 3 consecutive fields (Index, Active, ConditionParams)
            uint currentFieldIndex = startWrapperFieldStartIndex;

            for (int startIdx = 0; startIdx < dialog.Starts.Count; startIdx++)
            {
                if (startIdx < startStructIndices.Count)
                {
                    int structIndex = startStructIndices[startIdx];
                    if (structIndex < allStructs.Count)
                    {
                        // Each start wrapper struct uses exactly 3 consecutive fields (NO IsChild!)
                        uint byteOffset = currentFieldIndex * 4;
                        allStructs[structIndex].DataOrDataOffset = byteOffset;

                        // Move to next start wrapper struct's field range (3 fields per wrapper)
                        currentFieldIndex += 3;
                    }
                }
            }
        }

        /// <summary>
        /// Fix field indices for entry pointer structs
        /// </summary>
        public void FixEntryPointerStructFieldIndices(Dialog dialog, List<GffStruct> allStructs,
            List<List<int>> entryPointerStructIndices, System.Func<DialogNode, uint> calculateEntryFieldCount)
        {
            UnifiedLogger.LogParser(LogLevel.TRACE, $"?? FixEntryPointerStructFieldIndices: Updating struct DataOrDataOffset values");

            // Calculate where entry pointer fields start in the field sequence
            // Entry pointer fields are created after: root fields (9) + entry fields (variable)
            uint rootFieldCount = 9; // Known fixed count for root fields
            uint entryFieldsCount = 0;
            for (int i = 0; i < dialog.Entries.Count; i++)
            {
                entryFieldsCount += calculateEntryFieldCount(dialog.Entries[i]);
            }
            uint entryPointerFieldStartIndex = rootFieldCount + entryFieldsCount;

            // Track which entry has pointers and fix their struct indices
            int entryPointerIndex = 0;
            for (int entryIdx = 0; entryIdx < dialog.Entries.Count; entryIdx++)
            {
                var entry = dialog.Entries[entryIdx];
                if (entry.Pointers.Count > 0)
                {
                    // This entry has pointers, find its structs and update their DataOrDataOffset
                    if (entryIdx < entryPointerStructIndices.Count)
                    {
                        foreach (int structIndex in entryPointerStructIndices[entryIdx])
                        {
                            if (structIndex < allStructs.Count)
                            {
                                // Calculate the correct field index for this pointer struct
                                uint expectedFieldIndex = entryPointerFieldStartIndex + ((uint)entryPointerIndex * 4);
                                // Convert to byte offset pattern (fieldIndex * 4)
                                uint byteOffset = expectedFieldIndex * 4;
                                allStructs[structIndex].DataOrDataOffset = byteOffset;
                                entryPointerIndex++;
                            }
                        }
                    }
                }
            }
        }

    }
}
