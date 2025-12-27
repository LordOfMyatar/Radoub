using System;
using System.Collections.Generic;
using System.Text;
using DialogEditor.Models;
using Radoub.Formats.Gff;
using Radoub.Formats.Logging;

namespace DialogEditor.Parsers
{
    /// <summary>
    /// Factory for creating GFF field data and managing field/label collections.
    /// Extracted from DialogWriter.cs to improve maintainability.
    /// Refactoring: Dec 27, 2025 - Issue #534
    ///
    /// SCOPE: Field creation operations including:
    /// - Binary data builders (CExoLocString, CExoString, CResRef)
    /// - Label and field collection management
    /// - Field count calculations for Entry/Reply nodes
    /// </summary>
    internal class GffFieldFactory
    {
        /// <summary>
        /// Builds binary data for a CExoLocString field (localized string with StrRef).
        /// Format: TotalSize(4) + StrRef(4) + SubStringCount(4) + LangID(4) + TextLength(4) + Text + padding
        /// </summary>
        public byte[] BuildLocStringFieldData(string text)
        {
            var data = new List<byte>();

            // Write CExoLocString structure
            byte[] textBytes = Encoding.UTF8.GetBytes(text);
            // AURORA FIX: TotalSize = StringRef(4) + StringCount(4) + StringID(4) + StringLength(4) + Text
            // (NOT including TotalSize itself!)
            uint totalSize = (uint)(4 + 4 + 4 + 4 + textBytes.Length);

            UnifiedLogger.LogParser(LogLevel.TRACE, $"📝 BuildLocStringFieldData: text='{text.Substring(0, Math.Min(50, text.Length))}...', textBytes.Length={textBytes.Length}, totalSize={totalSize}");

            data.AddRange(BitConverter.GetBytes(totalSize)); // Total size (4 bytes)
            data.AddRange(BitConverter.GetBytes(0xFFFFFFFF)); // StrRef (4 bytes) - custom text
            data.AddRange(BitConverter.GetBytes((uint)1)); // SubString count (4 bytes)
            data.AddRange(BitConverter.GetBytes((uint)0)); // Language ID (4 bytes) - English
            data.AddRange(BitConverter.GetBytes((uint)textBytes.Length)); // Text length (4 bytes)
            data.AddRange(textBytes); // Text data

            // Pad to 4-byte boundary
            while (data.Count % 4 != 0)
                data.Add(0);

            UnifiedLogger.LogParser(LogLevel.TRACE, $"📝 BuildLocStringFieldData result: {data.Count} bytes total");

            return data.ToArray();
        }

        /// <summary>
        /// Builds binary data for a CExoString field (simple string).
        /// Format: Length(4) + Text
        /// </summary>
        public byte[] BuildCExoStringFieldData(string text)
        {
            var data = new List<byte>();

            // Write CExoString structure - just length + text
            byte[] textBytes = Encoding.UTF8.GetBytes(text);
            data.AddRange(BitConverter.GetBytes((uint)textBytes.Length)); // Length (4 bytes)
            data.AddRange(textBytes); // Text data

            return data.ToArray();
        }

        /// <summary>
        /// Builds binary data for a CResRef field (resource reference, max 16 chars).
        /// Format: LengthByte(1) + String
        /// </summary>
        public byte[] BuildCResRefFieldData(string resref)
        {
            // CResRef format matches reader expectations - length prefix + string data
            if (string.IsNullOrEmpty(resref))
            {
                return new byte[] { 0 }; // Zero length for empty CResRef
            }

            var resrefBytes = Encoding.ASCII.GetBytes(resref);
            var length = Math.Min(resrefBytes.Length, 16); // Max 16 characters

            var data = new byte[length + 1]; // Length prefix + string data
            data[0] = (byte)length; // Length prefix byte
            Array.Copy(resrefBytes, 0, data, 1, length); // String data

            return data;
        }

        /// <summary>
        /// Adds a label to the label collection (if not present) and creates a field.
        /// </summary>
        public void AddLabelAndField(List<GffField> allFields, List<string> allLabels, string label, uint type, uint value)
        {
            // Get or create label index
            int labelIndex = allLabels.IndexOf(label);
            if (labelIndex == -1)
            {
                labelIndex = allLabels.Count;
                allLabels.Add(label);
            }

            // Create field
            var field = new GffField
            {
                Type = type,
                LabelIndex = (uint)labelIndex,
                Label = label, // CRITICAL FIX: Set the Label property for FixListFieldOffsets
                DataOrDataOffset = value
            };
            allFields.Add(field);
        }

        /// <summary>
        /// Creates text data in fieldData and returns the offset.
        /// Each call creates new text data (no deduplication - per Aurora Engine design).
        /// </summary>
        public uint GetOrCreateTextOffset(string text, List<byte> fieldData)
        {
            // DISABLED: Text deduplication (2025-10-22)
            // Duplicated text is intentional author content, not a pattern to optimize
            // GFF only deduplicates ChildLink structures, not dialog text

            // Handle empty text
            if (string.IsNullOrEmpty(text))
            {
                text = ""; // Normalize null to empty string
            }

            // Create new text data
            uint newOffset = (uint)fieldData.Count;
            var locStringData = BuildLocStringFieldData(text);

            // DIAGNOSTIC: Check what we're about to add
            if (locStringData.Length >= 4)
            {
                uint first4 = BitConverter.ToUInt32(locStringData, 0);
                UnifiedLogger.LogParser(LogLevel.TRACE, $"📝 About to add locStringData: first 4 bytes = 0x{first4:X8} ({first4})");
            }

            fieldData.AddRange(locStringData);

            // DIAGNOSTIC: Check fieldData after adding (FIRST TEXT ONLY)
            if (newOffset == 0 && fieldData.Count >= 4)
            {
                uint first4AfterAdd = BitConverter.ToUInt32(fieldData.ToArray(), 0);
                UnifiedLogger.LogParser(LogLevel.TRACE, $"📝 CRITICAL: After adding FIRST text to fieldData: first 4 bytes = 0x{first4AfterAdd:X8} ({first4AfterAdd}), fieldData.Count={fieldData.Count}");
            }

            // Pad to 4-byte boundary
            while (fieldData.Count % 4 != 0)
            {
                fieldData.Add(0);
            }

            UnifiedLogger.LogParser(LogLevel.TRACE, $"📝 NEW TEXT: '{text}' → offset {newOffset}");

            return newOffset;
        }

        /// <summary>
        /// Calculates the number of fields for an Entry node.
        /// Base: 11 fields (Speaker, Animation, AnimLoop, Text, Script, ActionParams, Delay, Comment, Sound, Quest, RepliesList)
        /// QuestEntry is ONLY present when Quest is non-empty (per BioWare docs)
        /// </summary>
        public uint CalculateEntryFieldCount(DialogNode entry)
        {
            uint count = 11; // Base fields
            if (!string.IsNullOrEmpty(entry.Quest))
            {
                count++; // Add QuestEntry field
            }
            return count;
        }

        /// <summary>
        /// Calculates the number of fields for a Reply node.
        /// Base: 10 fields (Animation, AnimLoop, Text, Script, ActionParams, Delay, Comment, Sound, Quest, EntriesList)
        /// QuestEntry is ONLY present when Quest is non-empty (per BioWare docs)
        /// </summary>
        public uint CalculateReplyFieldCount(DialogNode reply)
        {
            uint count = 10; // Base fields
            if (!string.IsNullOrEmpty(reply.Quest))
            {
                count++; // Add QuestEntry field
            }
            return count;
        }

        /// <summary>
        /// Calculates the total size of the label section.
        /// GFF format uses exactly 16 bytes per label (fixed-width, null-padded).
        /// </summary>
        public uint CalculateLabelSize(List<string> allLabels)
        {
            return (uint)(allLabels.Count * 16); // GFF 16-byte fixed format
        }
    }
}
