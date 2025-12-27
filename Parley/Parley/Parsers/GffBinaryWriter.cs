using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DialogEditor.Models;
using Radoub.Formats.Gff;
using Radoub.Formats.Logging;

namespace DialogEditor.Parsers
{
    /// <summary>
    /// Handles low-level GFF binary serialization.
    /// Extracted from DialogWriter.cs to improve maintainability.
    /// Refactoring: Dec 27, 2025 - Issue #533
    ///
    /// SCOPE: Binary writing operations including:
    /// - GFF header writing (signature, version, offsets)
    /// - Struct array serialization
    /// - Field array serialization
    /// - Label array serialization (16-byte fixed format)
    /// - Field data writing
    /// - Field indices section
    /// - List indices section (for DLG conversation flow)
    /// </summary>
    internal class GffBinaryWriter
    {
        /// <summary>
        /// Writes a complete GFF binary file to a byte array.
        /// </summary>
        /// <param name="allStructs">All GFF structs to write</param>
        /// <param name="allFields">All GFF fields to write</param>
        /// <param name="allLabels">All unique label strings</param>
        /// <param name="fieldData">Pre-built field data section</param>
        /// <param name="signature">4-char file signature (e.g., "DLG ")</param>
        /// <param name="version">4-char version string (e.g., "V3.28")</param>
        /// <param name="dialog">Optional dialog for DLG-specific list indices</param>
        /// <param name="entryStructIndices">Tracked entry struct indices</param>
        /// <param name="replyStructIndices">Tracked reply struct indices</param>
        /// <param name="startStructIndices">Tracked start struct indices</param>
        /// <param name="entryPointerStructIndices">Tracked entry pointer struct indices</param>
        /// <param name="replyPointerStructIndices">Tracked reply pointer struct indices</param>
        /// <param name="pointerConditionParamsMapping">Mapping of pointer condition param struct indices</param>
        /// <param name="nodeActionParamsMapping">Mapping of node action param struct indices</param>
        /// <returns>Complete GFF binary as byte array</returns>
        public byte[] Write(
            List<GffStruct> allStructs,
            List<GffField> allFields,
            List<string> allLabels,
            List<byte> fieldData,
            string signature,
            string version,
            Dialog? dialog = null,
            List<int>? entryStructIndices = null,
            List<int>? replyStructIndices = null,
            List<int>? startStructIndices = null,
            List<List<int>>? entryPointerStructIndices = null,
            List<List<int>>? replyPointerStructIndices = null,
            Dictionary<int, List<int>>? pointerConditionParamsMapping = null,
            Dictionary<int, List<int>>? nodeActionParamsMapping = null,
            Func<Dialog, Dictionary<int, List<int>>?, Dictionary<int, List<int>>?, uint>? calculateListDataSize = null)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);

            // Calculate offsets
            uint headerSize = 56;
            uint structOffset = headerSize;
            uint structSize = (uint)(allStructs.Count * 12); // 12 bytes per struct
            uint fieldOffset = structOffset + structSize;
            uint fieldSize = (uint)(allFields.Count * 12); // 12 bytes per field
            uint labelOffset = fieldOffset + fieldSize;
            uint labelSize = CalculateLabelSize(allLabels);
            uint fieldDataOffset = labelOffset + labelSize;

            // Write GFF header (56 bytes)
            WriteHeader(writer, signature, version);

            // Calculate remaining offsets for header
            uint fieldIndicesOffset = fieldDataOffset + (uint)fieldData.Count;
            uint fieldCount = CalculateActualFieldIndicesCount(allFields);
            uint fieldIndicesBytes = fieldCount * 4;
            uint listIndicesOffset = fieldIndicesOffset + fieldIndicesBytes;

            // Calculate list indices size
            uint listIndicesCount = 0;
            if (dialog != null && calculateListDataSize != null)
            {
                listIndicesCount = calculateListDataSize(dialog, pointerConditionParamsMapping, nodeActionParamsMapping);
            }

            // Log buffer size calculation
            uint listDataSize = listIndicesCount;
            uint totalBufferSize = fieldDataOffset + (uint)fieldData.Count + fieldIndicesBytes + listDataSize;
            UnifiedLogger.LogParser(LogLevel.TRACE, $"📦 Complete buffer size: fieldData={fieldData.Count}, fieldIndices={fieldIndicesBytes}, listData={listDataSize}, total={totalBufferSize} bytes");

            // Write header offset table (12 uint32 values)
            writer.Write(structOffset);
            writer.Write((uint)allStructs.Count);
            writer.Write(fieldOffset);
            writer.Write((uint)allFields.Count);
            writer.Write(labelOffset);
            writer.Write((uint)allLabels.Count);
            writer.Write(fieldDataOffset);
            writer.Write((uint)fieldData.Count);
            writer.Write(fieldIndicesOffset);
            writer.Write(fieldIndicesBytes);
            writer.Write(listIndicesOffset);
            writer.Write(listIndicesCount);

            // Write struct array
            WriteStructs(writer, allStructs);

            // Write field array
            WriteFields(writer, allFields);

            // Write label array
            WriteLabels(writer, allLabels);

            // Write field data
            WriteFieldData(writer, fieldData);

            // Write field indices
            WriteFieldIndices(writer, allFields);

            // Write list indices (DLG-specific)
            if (dialog != null && entryStructIndices != null && replyStructIndices != null &&
                startStructIndices != null && entryPointerStructIndices != null && replyPointerStructIndices != null)
            {
                WriteListIndices(writer, allStructs, allFields, dialog,
                    entryStructIndices, replyStructIndices, startStructIndices,
                    entryPointerStructIndices, replyPointerStructIndices,
                    listIndicesOffset,
                    pointerConditionParamsMapping ?? new Dictionary<int, List<int>>(),
                    nodeActionParamsMapping ?? new Dictionary<int, List<int>>());
            }

            UnifiedLogger.LogParser(LogLevel.TRACE, $"Generated GFF binary: {stream.Length} bytes");
            return stream.ToArray();
        }

        private void WriteHeader(BinaryWriter writer, string signature, string version)
        {
            // Fixed header format: proper 4+4 byte alignment for signature+version
            var signatureBytes = new byte[4];
            var signatureSrc = System.Text.Encoding.ASCII.GetBytes(signature);
            Array.Copy(signatureSrc, 0, signatureBytes, 0, Math.Min(4, signatureSrc.Length));
            writer.Write(signatureBytes);

            var versionBytes = new byte[4];
            var versionSrc = System.Text.Encoding.ASCII.GetBytes(version);
            Array.Copy(versionSrc, 0, versionBytes, 0, Math.Min(4, versionSrc.Length));
            writer.Write(versionBytes);
        }

        private void WriteStructs(BinaryWriter writer, List<GffStruct> allStructs)
        {
            UnifiedLogger.LogParser(LogLevel.DEBUG, $"📋 Writing {allStructs.Count} structs:");
            for (int i = 0; i < allStructs.Count; i++)
            {
                var gffStruct = allStructs[i];
                UnifiedLogger.LogParser(LogLevel.DEBUG, $"📋 Struct[{i}]: Type={gffStruct.Type}, DataOrDataOffset={gffStruct.DataOrDataOffset}, FieldCount={gffStruct.FieldCount}");
                writer.Write(gffStruct.Type);
                writer.Write(gffStruct.DataOrDataOffset);
                writer.Write(gffStruct.FieldCount);
            }
        }

        private void WriteFields(BinaryWriter writer, List<GffField> allFields)
        {
            UnifiedLogger.LogParser(LogLevel.TRACE, $"📝 Writing {allFields.Count} fields to binary:");
            for (int i = 0; i < allFields.Count; i++)
            {
                var field = allFields[i];
                if (i < 10)
                {
                    UnifiedLogger.LogParser(LogLevel.TRACE, $"📝 Field[{i}]: Label='{field.Label}', Type={field.Type}, LabelIndex={field.LabelIndex}, Offset={field.DataOrDataOffset}");
                }
                if (field.Label == "StartingList" || field.Label == "EntryList")
                {
                    UnifiedLogger.LogParser(LogLevel.TRACE, $"📝 WRITING {field.Label} field: LabelIndex={field.LabelIndex}, DataOrDataOffset={field.DataOrDataOffset} (0x{field.DataOrDataOffset:X8})");
                }
                if (field.Label == "Animation" && i < 20)
                {
                    UnifiedLogger.LogParser(LogLevel.TRACE, $"📝 WRITE Field[{i}]: Label='Animation', Type={field.Type} (DWORD={GffField.DWORD}, FLOAT={GffField.FLOAT}), DataOrDataOffset={field.DataOrDataOffset}");
                }
                writer.Write(field.Type);
                writer.Write(field.LabelIndex);
                writer.Write(field.DataOrDataOffset);

                if (field.Label == "StartingList" || field.Label == "EntryList")
                {
                    UnifiedLogger.LogParser(LogLevel.TRACE, $"📝 BINARY WRITE: Field[{i}] {field.Label} wrote LabelIndex={field.LabelIndex} to binary");
                }
            }
        }

        private void WriteLabels(BinaryWriter writer, List<string> allLabels)
        {
            UnifiedLogger.LogParser(LogLevel.TRACE, $"🏷️ Writing {allLabels.Count} labels:");
            for (int i = 0; i < allLabels.Count; i++)
            {
                var label = allLabels[i];
                if (label == "EntryList" || label == "StartingList")
                {
                    UnifiedLogger.LogParser(LogLevel.TRACE, $"🏷️ Label[{i}]: '{label}'");
                }
                var labelData = new byte[16]; // Always exactly 16 bytes
                var labelBytes = System.Text.Encoding.ASCII.GetBytes(label);
                Array.Copy(labelBytes, labelData, Math.Min(labelBytes.Length, 16));
                writer.Write(labelData);
            }
        }

        private void WriteFieldData(BinaryWriter writer, List<byte> fieldData)
        {
            UnifiedLogger.LogParser(LogLevel.TRACE, $"💾 Writing FieldData section: {fieldData.Count} bytes");
            if (fieldData.Count >= 4)
            {
                uint first4Bytes = BitConverter.ToUInt32(fieldData.ToArray(), 0);
                UnifiedLogger.LogParser(LogLevel.TRACE, $"💾 First 4 bytes of FieldData: 0x{first4Bytes:X8} ({first4Bytes})");
            }
            writer.Write(fieldData.ToArray());
        }

        private void WriteFieldIndices(BinaryWriter writer, List<GffField> allFields)
        {
            UnifiedLogger.LogParser(LogLevel.TRACE, "📇 Writing complex field index mapping pattern");

            uint fieldCount = (uint)allFields.Count;
            uint totalIndicesWritten = 0;

            UnifiedLogger.LogParser(LogLevel.TRACE, $"📇 Writing complex field mapping for {fieldCount} fields");

            // Write GFF's complex root struct field mapping
            WriteAuroraRootStructIndices(writer, allFields, ref totalIndicesWritten);

            // Write remaining field indices for other structs
            WriteRemainingStructIndices(writer, allFields, ref totalIndicesWritten);

            UnifiedLogger.LogParser(LogLevel.TRACE, $"📇 Field Indices: Wrote {totalIndicesWritten} field indices for {fieldCount} fields");
        }

        private void WriteAuroraRootStructIndices(BinaryWriter writer, List<GffField> allFields, ref uint totalIndicesWritten)
        {
            // Aurora pattern: Root struct field mapping follows specific pattern
            // [0,1,2,3,4,5,6, EntryListIndex, ReplyListIndex, 7,8,StartingListIndex]

            int entryListIndex = FindFieldIndex(allFields, "EntryList");
            int replyListIndex = FindFieldIndex(allFields, "ReplyList");
            int startingListIndex = FindFieldIndex(allFields, "StartingList");

            UnifiedLogger.LogParser(LogLevel.TRACE, $"📇 List field indices: EntryList={entryListIndex}, ReplyList={replyListIndex}, StartingList={startingListIndex}");

            // Write GFF's root struct pattern - SIMPLE SEQUENTIAL INDICES
            // Root fields: DelayEntry(0), DelayReply(1), EndConverAbort(2), EndConversation(3), EntryList(4), NumWords(5), PreventZoomIn(6), ReplyList(7), StartingList(8)
            writer.Write((uint)0);
            writer.Write((uint)1);
            writer.Write((uint)2);
            writer.Write((uint)3);
            writer.Write((uint)4);
            writer.Write((uint)5);
            writer.Write((uint)6);
            writer.Write((uint)7);
            writer.Write((uint)8);

            UnifiedLogger.LogParser(LogLevel.TRACE, $"📇 Wrote Root struct indices with simple sequential mapping [0,1,2,3,4,5,6,7,8]");

            totalIndicesWritten += 9;
        }

        private void WriteRemainingStructIndices(BinaryWriter writer, List<GffField> allFields, ref uint totalIndicesWritten)
        {
            uint fieldCount = (uint)allFields.Count;

            // Write sequential indices for all remaining fields (non-root structs)
            for (uint fieldIndex = 9; fieldIndex < fieldCount; fieldIndex++)
            {
                writer.Write(fieldIndex);
                totalIndicesWritten++;
            }

            UnifiedLogger.LogParser(LogLevel.TRACE,
                $"📇 Field Indices: Wrote {totalIndicesWritten} field indices for {fieldCount} fields (1:1 ratio - FIXED)");
        }

        private void WriteListIndices(
            BinaryWriter writer,
            List<GffStruct> allStructs,
            List<GffField> allFields,
            Dialog dialog,
            List<int> entryStructIndices,
            List<int> replyStructIndices,
            List<int> startStructIndices,
            List<List<int>> entryPointerStructIndices,
            List<List<int>> replyPointerStructIndices,
            uint listIndicesOffset,
            Dictionary<int, List<int>> pointerConditionParamsMapping,
            Dictionary<int, List<int>> nodeActionParamsMapping)
        {
            UnifiedLogger.LogParser(LogLevel.TRACE, "📜 Writing list indices pattern (conversation flow)");

            // Seek to the correct ListIndicesOffset before writing list data
            UnifiedLogger.LogParser(LogLevel.TRACE, $"📜 SEEKING to ListIndicesOffset {listIndicesOffset} (current position: {writer.BaseStream.Position})");
            writer.BaseStream.Seek(listIndicesOffset, SeekOrigin.Begin);
            UnifiedLogger.LogParser(LogLevel.TRACE, $"📜 POSITIONED at {writer.BaseStream.Position} for list data write");

            uint relativePosition = 0;

            int entryCount = dialog.Entries.Count;
            int replyCount = dialog.Replies.Count;
            int startCount = dialog.Starts.Count;

            UnifiedLogger.LogParser(LogLevel.TRACE, $"Using tracked reply struct indices: [{string.Join(", ", replyStructIndices)}]");
            UnifiedLogger.LogParser(LogLevel.TRACE, $"Using tracked start struct indices: [{string.Join(", ", startStructIndices)}]");

            // Write EntryList
            WriteEntryList(writer, dialog, entryStructIndices, ref relativePosition);

            // Write ReplyList
            WriteReplyList(writer, dialog, replyStructIndices, ref relativePosition);

            // Write StartingList
            WriteStartingList(writer, dialog, startStructIndices, ref relativePosition);

            // Write individual pointer lists for each dialog node
            UnifiedLogger.LogParser(LogLevel.DEBUG, "📜 Writing individual pointer lists for conversation flow");

            // Write RepliesList for each entry
            WriteEntryRepliesLists(writer, dialog, entryPointerStructIndices, ref relativePosition);

            // Write EntriesList for each reply
            WriteReplyEntriesLists(writer, dialog, replyPointerStructIndices, ref relativePosition);

            // Write ConditionParams for all pointers
            WriteConditionParams(writer, dialog, pointerConditionParamsMapping, ref relativePosition);

            // Write ActionParams for all nodes
            WriteActionParams(writer, dialog, entryStructIndices, replyStructIndices, nodeActionParamsMapping, ref relativePosition);

            UnifiedLogger.LogParser(LogLevel.TRACE, $"✅ ListIndices section complete at position {writer.BaseStream.Position}");
        }

        private void WriteEntryList(BinaryWriter writer, Dialog dialog, List<int> entryStructIndices, ref uint relativePosition)
        {
            int entryCount = dialog.Entries.Count;
            UnifiedLogger.LogParser(LogLevel.TRACE, $"📜 POSITION DEBUG: About to write EntryList count at position {writer.BaseStream.Position}");
            UnifiedLogger.LogParser(LogLevel.TRACE, $"📜 DIAGNOSTIC: EntryList write starting at relative offset {relativePosition}");
            writer.Write((uint)entryCount);
            UnifiedLogger.LogParser(LogLevel.TRACE, $"📜 Writing EntryList: count={entryCount} (TRACKED INDICES)");
            for (int i = 0; i < entryCount; i++)
            {
                var structIndex = entryStructIndices[i];
                writer.Write((uint)structIndex);
            }
            relativePosition += 4 + ((uint)entryCount * 4);
            UnifiedLogger.LogParser(LogLevel.TRACE, $"📜 DIAGNOSTIC: EntryList complete, relative position now {relativePosition}");
        }

        private void WriteReplyList(BinaryWriter writer, Dialog dialog, List<int> replyStructIndices, ref uint relativePosition)
        {
            int actualReplyContentCount = Math.Min(dialog.Replies.Count, replyStructIndices.Count);
            writer.Write((uint)actualReplyContentCount);
            UnifiedLogger.LogParser(LogLevel.TRACE, $"📜 Writing ReplyList: count={actualReplyContentCount} (CONTENT ONLY)");
            for (int i = 0; i < actualReplyContentCount; i++)
            {
                var structIndex = replyStructIndices[i];
                writer.Write((uint)structIndex);
                UnifiedLogger.LogParser(LogLevel.TRACE, $"📜 ReplyList[{i}] → struct[{structIndex}] (CONTENT: '{dialog.Replies[i].Text?.GetDefault() ?? ""}')");
            }
            relativePosition += 4 + ((uint)actualReplyContentCount * 4);
            UnifiedLogger.LogParser(LogLevel.TRACE, $"📜 DIAGNOSTIC: ReplyList complete, relative position now {relativePosition}");
        }

        private void WriteStartingList(BinaryWriter writer, Dialog dialog, List<int> startStructIndices, ref uint relativePosition)
        {
            int startCount = dialog.Starts.Count;
            UnifiedLogger.LogParser(LogLevel.TRACE, $"📜 CRITICAL DEBUG: dialog.Starts.Count={startCount}, startStructIndices.Count={startStructIndices.Count}");
            writer.Write((uint)startCount);
            UnifiedLogger.LogParser(LogLevel.DEBUG, $"📜 Writing StartingList: count={startCount}");
            for (int i = 0; i < startCount; i++)
            {
                if (i < startStructIndices.Count)
                {
                    var startWrapperStructIndex = startStructIndices[i];
                    writer.Write((uint)startWrapperStructIndex);
                    UnifiedLogger.LogParser(LogLevel.TRACE, $"📜 StartingList[{i}] → Start wrapper struct[{startWrapperStructIndex}] (points to Entry[{dialog.Starts[i].Index}])");
                }
                else
                {
                    UnifiedLogger.LogParser(LogLevel.ERROR, $"📜 StartingList[{i}] → MISSING start wrapper struct (tracking failed)");
                    writer.Write((uint)0);
                }
            }
            relativePosition += 4 + ((uint)startCount * 4);
            UnifiedLogger.LogParser(LogLevel.TRACE, $"📜 DIAGNOSTIC: StartingList complete, relative position now {relativePosition}");
        }

        private void WriteEntryRepliesLists(BinaryWriter writer, Dialog dialog, List<List<int>> entryPointerStructIndices, ref uint relativePosition)
        {
            UnifiedLogger.LogParser(LogLevel.TRACE, $"📜 DIAGNOSTIC: RepliesList write starting at relative offset {relativePosition}");
            for (int entryIdx = 0; entryIdx < dialog.Entries.Count; entryIdx++)
            {
                var entry = dialog.Entries[entryIdx];
                UnifiedLogger.LogParser(LogLevel.TRACE, $"📜 Entry[{entryIdx}] writing {entry.Pointers.Count} pointer list entries");

                if (entry.Pointers.Count > 0)
                {
                    writer.Write((uint)entry.Pointers.Count);
                    relativePosition += 4;
                    for (int ptrIdx = 0; ptrIdx < entry.Pointers.Count; ptrIdx++)
                    {
                        if (entryIdx < entryPointerStructIndices.Count && ptrIdx < entryPointerStructIndices[entryIdx].Count)
                        {
                            var pointerStructIndex = entryPointerStructIndices[entryIdx][ptrIdx];
                            writer.Write((uint)pointerStructIndex);
                            relativePosition += 4;
                            UnifiedLogger.LogParser(LogLevel.TRACE, $"→ Entry[{entryIdx}] RepliesList[{ptrIdx}] → pointer struct index {pointerStructIndex}");
                        }
                        else
                        {
                            var targetReplyIndex = entry.Pointers[ptrIdx].Index;
                            writer.Write((uint)targetReplyIndex);
                            relativePosition += 4;
                            UnifiedLogger.LogParser(LogLevel.WARN, $"→ Entry[{entryIdx}] RepliesList[{ptrIdx}] → FALLBACK direct reply index {targetReplyIndex}");
                        }
                    }
                }
                else
                {
                    writer.Write((uint)0);
                    relativePosition += 4;
                    UnifiedLogger.LogParser(LogLevel.DEBUG, $"Entry[{entryIdx}] has empty RepliesList (count=0)");
                }
            }
            UnifiedLogger.LogParser(LogLevel.TRACE, $"📜 DIAGNOSTIC: RepliesList complete, relative position now {relativePosition}");
        }

        private void WriteReplyEntriesLists(BinaryWriter writer, Dialog dialog, List<List<int>> replyPointerStructIndices, ref uint relativePosition)
        {
            UnifiedLogger.LogParser(LogLevel.TRACE, $"📜 DIAGNOSTIC: EntriesList write starting at relative offset {relativePosition}");
            for (int replyIdx = 0; replyIdx < dialog.Replies.Count; replyIdx++)
            {
                var reply = dialog.Replies[replyIdx];
                if (reply.Pointers.Count > 0)
                {
                    writer.Write((uint)reply.Pointers.Count);
                    relativePosition += 4;
                    for (int ptrIdx = 0; ptrIdx < reply.Pointers.Count; ptrIdx++)
                    {
                        if (replyIdx < replyPointerStructIndices.Count && ptrIdx < replyPointerStructIndices[replyIdx].Count)
                        {
                            var pointerStructIndex = replyPointerStructIndices[replyIdx][ptrIdx];
                            writer.Write((uint)pointerStructIndex);
                            relativePosition += 4;
                            UnifiedLogger.LogParser(LogLevel.TRACE, $"Reply[{replyIdx}] EntriesList[{ptrIdx}] → pointer struct index {pointerStructIndex}");
                        }
                        else
                        {
                            var targetEntryIndex = reply.Pointers[ptrIdx].Index;
                            writer.Write((uint)targetEntryIndex);
                            relativePosition += 4;
                            UnifiedLogger.LogParser(LogLevel.WARN, $"Reply[{replyIdx}] EntriesList[{ptrIdx}] → FALLBACK direct entry index {targetEntryIndex}");
                        }
                    }
                }
                else
                {
                    writer.Write((uint)0);
                    relativePosition += 4;
                    UnifiedLogger.LogParser(LogLevel.DEBUG, $"Reply[{replyIdx}] has empty EntriesList (count=0)");
                }
            }
            UnifiedLogger.LogParser(LogLevel.TRACE, $"📜 DIAGNOSTIC: EntriesList complete, relative position now {relativePosition}");
        }

        private void WriteConditionParams(BinaryWriter writer, Dialog dialog, Dictionary<int, List<int>> pointerConditionParamsMapping, ref uint relativePosition)
        {
            int globalPointerIndex = 0;
            var conditionParamsList = pointerConditionParamsMapping.OrderBy(kvp => kvp.Key).Select(kvp => kvp.Value).ToList();
            int conditionParamsIndex = 0;

            // Entry pointers
            UnifiedLogger.LogParser(LogLevel.TRACE, $"📜 Writing ConditionParams for entry pointers");
            UnifiedLogger.LogParser(LogLevel.TRACE, $"📜 DIAGNOSTIC: ConditionParams (Entry pointers) write starting at relative offset {relativePosition}");
            for (int entryIdx = 0; entryIdx < dialog.Entries.Count; entryIdx++)
            {
                foreach (var ptr in dialog.Entries[entryIdx].Pointers)
                {
                    int paramCount = ptr.ConditionParams?.Count ?? 0;
                    writer.Write((uint)paramCount);
                    relativePosition += 4;

                    if (paramCount > 0 && conditionParamsIndex < conditionParamsList.Count)
                    {
                        var structIndices = conditionParamsList[conditionParamsIndex];
                        conditionParamsIndex++;

                        foreach (var structIdx in structIndices)
                        {
                            writer.Write((uint)structIdx);
                            relativePosition += 4;
                        }
                        UnifiedLogger.LogParser(LogLevel.DEBUG, $"   Entry[{entryIdx}] Pointer[{globalPointerIndex}] ConditionParams: wrote {structIndices.Count} param struct indices");
                    }
                    else
                    {
                        UnifiedLogger.LogParser(LogLevel.DEBUG, $"   Entry[{entryIdx}] Pointer[{globalPointerIndex}] ConditionParams: count={paramCount} (empty)");
                    }
                    globalPointerIndex++;
                }
            }
            UnifiedLogger.LogParser(LogLevel.TRACE, $"📜 DIAGNOSTIC: ConditionParams (Entry pointers) complete, relative position now {relativePosition}");

            // Reply pointers
            UnifiedLogger.LogParser(LogLevel.TRACE, $"📜 Writing ConditionParams for reply pointers");
            for (int replyIdx = 0; replyIdx < dialog.Replies.Count; replyIdx++)
            {
                foreach (var ptr in dialog.Replies[replyIdx].Pointers)
                {
                    int paramCount = ptr.ConditionParams?.Count ?? 0;
                    writer.Write((uint)paramCount);
                    relativePosition += 4;

                    if (paramCount > 0 && conditionParamsIndex < conditionParamsList.Count)
                    {
                        var structIndices = conditionParamsList[conditionParamsIndex];
                        conditionParamsIndex++;

                        foreach (var structIdx in structIndices)
                        {
                            writer.Write((uint)structIdx);
                            relativePosition += 4;
                        }
                        UnifiedLogger.LogParser(LogLevel.DEBUG, $"   Reply[{replyIdx}] Pointer[{globalPointerIndex}] ConditionParams: wrote {structIndices.Count} param struct indices");
                    }
                    else
                    {
                        UnifiedLogger.LogParser(LogLevel.DEBUG, $"   Reply[{replyIdx}] Pointer[{globalPointerIndex}] ConditionParams: count={paramCount} (empty)");
                    }
                    globalPointerIndex++;
                }
            }
            UnifiedLogger.LogParser(LogLevel.TRACE, $"📜 DIAGNOSTIC: ConditionParams (Reply pointers) complete, relative position now {relativePosition}");

            // Start wrappers
            UnifiedLogger.LogParser(LogLevel.TRACE, $"📜 Writing ConditionParams for {dialog.Starts.Count} start wrappers");
            for (int startIdx = 0; startIdx < dialog.Starts.Count; startIdx++)
            {
                var startPtr = dialog.Starts[startIdx];
                int paramCount = startPtr.ConditionParams?.Count ?? 0;
                writer.Write((uint)paramCount);
                relativePosition += 4;

                if (paramCount > 0 && conditionParamsIndex < conditionParamsList.Count)
                {
                    var structIndices = conditionParamsList[conditionParamsIndex];
                    conditionParamsIndex++;

                    foreach (var structIdx in structIndices)
                    {
                        writer.Write((uint)structIdx);
                        relativePosition += 4;
                    }
                    UnifiedLogger.LogParser(LogLevel.DEBUG, $"   Start[{startIdx}] ConditionParams: wrote {structIndices.Count} param struct indices");
                }
                else
                {
                    UnifiedLogger.LogParser(LogLevel.DEBUG, $"   Start[{startIdx}] ConditionParams: count={paramCount} (empty)");
                }
            }
            UnifiedLogger.LogParser(LogLevel.TRACE, $"📜 DIAGNOSTIC: ConditionParams (Start pointers) complete, relative position now {relativePosition}");
        }

        private void WriteActionParams(
            BinaryWriter writer,
            Dialog dialog,
            List<int> entryStructIndices,
            List<int> replyStructIndices,
            Dictionary<int, List<int>> nodeActionParamsMapping,
            ref uint relativePosition)
        {
            // Entry ActionParams
            UnifiedLogger.LogParser(LogLevel.TRACE, $"📜 Writing ActionParams for {dialog.Entries.Count} entries");
            UnifiedLogger.LogParser(LogLevel.TRACE, $"📜 DIAGNOSTIC: ActionParams (Entries) write starting at relative offset {relativePosition}");
            for (int entryIdx = 0; entryIdx < dialog.Entries.Count; entryIdx++)
            {
                int paramCount = dialog.Entries[entryIdx].ActionParams?.Count ?? 0;
                uint writeStartPos = relativePosition;
                writer.Write((uint)paramCount);
                relativePosition += 4;

                int gffStructIdx = entryStructIndices[entryIdx];
                if (paramCount > 0 && nodeActionParamsMapping.TryGetValue(gffStructIdx, out var structIndices))
                {
                    foreach (var structIdx in structIndices)
                    {
                        writer.Write((uint)structIdx);
                        relativePosition += 4;
                    }
                    UnifiedLogger.LogParser(LogLevel.DEBUG, $"   Entry[{entryIdx}] (GFF Struct[{gffStructIdx}]) ActionParams: wrote {structIndices.Count} param struct indices at relative offset {writeStartPos}");
                }
                else
                {
                    UnifiedLogger.LogParser(LogLevel.DEBUG, $"   Entry[{entryIdx}] (GFF Struct[{gffStructIdx}]) ActionParams: count={paramCount} (empty) at relative offset {writeStartPos}");
                }
            }
            UnifiedLogger.LogParser(LogLevel.TRACE, $"📜 DIAGNOSTIC: ActionParams (Entries) complete, relative position now {relativePosition}");

            // Reply ActionParams
            UnifiedLogger.LogParser(LogLevel.TRACE, $"📜 Writing ActionParams for {dialog.Replies.Count} replies");
            UnifiedLogger.LogParser(LogLevel.TRACE, $"📜 DIAGNOSTIC: ActionParams (Replies) write starting at relative offset {relativePosition}");
            for (int replyIdx = 0; replyIdx < dialog.Replies.Count; replyIdx++)
            {
                int paramCount = dialog.Replies[replyIdx].ActionParams?.Count ?? 0;
                uint writeStartPos = relativePosition;
                writer.Write((uint)paramCount);
                relativePosition += 4;

                int gffStructIdx = replyStructIndices[replyIdx];
                if (paramCount > 0 && nodeActionParamsMapping.TryGetValue(gffStructIdx, out var structIndices))
                {
                    foreach (var structIdx in structIndices)
                    {
                        writer.Write((uint)structIdx);
                        relativePosition += 4;
                    }
                    UnifiedLogger.LogParser(LogLevel.DEBUG, $"   Reply[{replyIdx}] (GFF Struct[{gffStructIdx}]) ActionParams: wrote {structIndices.Count} param struct indices at relative offset {writeStartPos}");
                }
                else
                {
                    UnifiedLogger.LogParser(LogLevel.DEBUG, $"   Reply[{replyIdx}] (GFF Struct[{gffStructIdx}]) ActionParams: count={paramCount} (empty) at relative offset {writeStartPos}");
                }
            }
            UnifiedLogger.LogParser(LogLevel.TRACE, $"📜 DIAGNOSTIC: ActionParams (Replies) complete, relative position now {relativePosition}");
        }

        private int FindFieldIndex(List<GffField> allFields, string labelName)
        {
            for (int i = 0; i < allFields.Count; i++)
            {
                if (allFields[i].Label == labelName)
                {
                    return i;
                }
            }
            return -1;
        }

        private uint CalculateLabelSize(List<string> allLabels)
        {
            return (uint)(allLabels.Count * 16); // 16 bytes per label
        }

        private uint CalculateActualFieldIndicesCount(List<GffField> allFields)
        {
            // 1:1 ratio - one index per field
            return (uint)allFields.Count;
        }
    }
}
