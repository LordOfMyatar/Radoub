using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DialogEditor.Models;
using DialogEditor.Services;
using Radoub.Formats.Logging;
using DialogEditor.Utils;
using System.IO;

namespace DialogEditor.Parsers
{
    /// <summary>
    /// Handles Dialog-to-binary write operations for GFF format.
    /// Extracted from DialogParser.cs to improve maintainability.
    /// Phase 3 of parser refactoring - Oct 28, 2025
    ///
    /// SCOPE: Complete write infrastructure including:
    /// - GFF buffer creation (CreateDlgBuffer, CreateFullDlgBufferManual, ConvertDialogToGff)
    /// - Struct/field creation (CreateAuroraCompatibleGffStructures, CreateInterleavedStructs)
    /// - Field creation helpers (CreateRootFields, CreateSingleEntryFields, CreatePointerFields, etc.)
    /// - Binary serialization (WriteBinaryGff)
    /// - Calculation helpers (CalculateListIndicesOffsets, CalculateEntryFieldCount, etc.)
    /// - Parameter struct creation (CreateDynamicParameterStructs, etc.)
    /// </summary>
    // Phase 4 Refactoring: Made internal - use DialogFileService for public API
    internal class DialogWriter
    {
        private readonly GffIndexFixer _indexFixer = new();
        private readonly GffBinaryWriter _binaryWriter = new();
        private readonly GffFieldFactory _fieldFactory = new();

        /// <summary>
        /// Traversal state for interleaved struct creation.
        /// Tracks visited nodes and mappings to prevent infinite loops and duplicates.
        /// </summary>
        private class InterleavedTraversalState
        {
            public HashSet<uint> VisitedEntries { get; set; } = new HashSet<uint>();
            public HashSet<uint> VisitedReplies { get; set; } = new HashSet<uint>();
            public Dictionary<uint, int> EntryIndexToStructIndex { get; set; } = new Dictionary<uint, int>();
            public Dictionary<uint, int> ReplyIndexToStructIndex { get; set; } = new Dictionary<uint, int>();
            public List<int> EntryStructIndices { get; set; } = new List<int>();
            public List<int> ReplyStructIndices { get; set; } = new List<int>();
            public List<int> StartStructIndices { get; set; } = new List<int>();
            public List<List<int>> EntryPointerStructIndices { get; set; } = new List<List<int>>();
            public List<List<int>> ReplyPointerStructIndices { get; set; } = new List<List<int>>();
            public uint CurrentFieldIndex { get; set; }
        }

        /// <summary>
        /// Pre-calculates all ListIndices offsets BEFORE creating fields.
        /// This ensures fields are created with correct offsets from the start (no patching needed).
        /// See PARAMETER_PRESERVATION_ARCHITECTURE.md for design rationale.
        /// </summary>
        private ListIndicesOffsetMap CalculateListIndicesOffsets(Dialog dialog)
        {
            var map = new ListIndicesOffsetMap();
            uint currentOffset = 0;

            UnifiedLogger.LogParser(LogLevel.TRACE, "?? Pre-calculating ListIndices offsets for all lists");

            // 1. EntryList (always first in ListIndices section)
            map.EntryListOffset = currentOffset;
            currentOffset += 4 + ((uint)dialog.Entries.Count * 4); // count + struct indices
            UnifiedLogger.LogParser(LogLevel.DEBUG, $"   EntryList: offset={map.EntryListOffset}, size={currentOffset - map.EntryListOffset}");

            // 2. ReplyList
            map.ReplyListOffset = currentOffset;
            currentOffset += 4 + ((uint)dialog.Replies.Count * 4);
            UnifiedLogger.LogParser(LogLevel.DEBUG, $"   ReplyList: offset={map.ReplyListOffset}, size={currentOffset - map.ReplyListOffset}");

            // 3. StartingList
            map.StartingListOffset = currentOffset;
            currentOffset += 4 + ((uint)dialog.Starts.Count * 4);
            UnifiedLogger.LogParser(LogLevel.DEBUG, $"   StartingList: offset={map.StartingListOffset}, size={currentOffset - map.StartingListOffset}");

            // 4. Individual RepliesList for each entry
            for (int i = 0; i < dialog.Entries.Count; i++)
            {
                map.RepliesListOffsets[i] = currentOffset;
                int repliesCount = dialog.Entries[i].Pointers.Count;
                currentOffset += 4 + ((uint)repliesCount * 4);
            }
            UnifiedLogger.LogParser(LogLevel.DEBUG, $"   RepliesList: {dialog.Entries.Count} lists calculated");

            // 5. Individual EntriesList for each reply
            for (int i = 0; i < dialog.Replies.Count; i++)
            {
                map.EntriesListOffsets[i] = currentOffset;
                int entriesCount = dialog.Replies[i].Pointers.Count;
                currentOffset += 4 + ((uint)entriesCount * 4);
            }
            UnifiedLogger.LogParser(LogLevel.DEBUG, $"   EntriesList: {dialog.Replies.Count} lists calculated");

            // 6. ConditionParams for all pointers (entries + replies + starts)
            int globalPointerIndex = 0;

            // Entry pointers
            foreach (var entry in dialog.Entries)
            {
                foreach (var ptr in entry.Pointers)
                {
                    map.PointerConditionParamsOffsets[globalPointerIndex] = currentOffset;
                    int paramCount = ptr.ConditionParams?.Count ?? 0;
                    currentOffset += 4 + ((uint)paramCount * 4); // count + param struct indices
                    globalPointerIndex++;
                }
            }

            // Reply pointers
            foreach (var reply in dialog.Replies)
            {
                foreach (var ptr in reply.Pointers)
                {
                    map.PointerConditionParamsOffsets[globalPointerIndex] = currentOffset;
                    int paramCount = ptr.ConditionParams?.Count ?? 0;
                    currentOffset += 4 + ((uint)paramCount * 4);
                    globalPointerIndex++;
                }
            }

            // Start wrappers (each start IS a pointer with ConditionParams)
            for (int i = 0; i < dialog.Starts.Count; i++)
            {
                var startPtr = dialog.Starts[i];
                map.StartConditionParamsOffsets[i] = currentOffset;
                int paramCount = startPtr.ConditionParams?.Count ?? 0;
                currentOffset += 4 + ((uint)paramCount * 4);
            }
            UnifiedLogger.LogParser(LogLevel.DEBUG, $"   ConditionParams: {globalPointerIndex + dialog.Starts.Count} lists calculated");

            // 7. ActionParams for all nodes (entries + replies)
            for (int i = 0; i < dialog.Entries.Count; i++)
            {
                map.EntryActionParamsOffsets[i] = currentOffset;
                int paramCount = dialog.Entries[i].ActionParams?.Count ?? 0;
                UnifiedLogger.LogParser(LogLevel.DEBUG, $"   Entry[{i}] ActionParams: PRE-CALC offset={currentOffset}, paramCount={paramCount}");
                currentOffset += 4 + ((uint)paramCount * 4);
            }

            for (int i = 0; i < dialog.Replies.Count; i++)
            {
                map.ReplyActionParamsOffsets[i] = currentOffset;
                int paramCount = dialog.Replies[i].ActionParams?.Count ?? 0;
                currentOffset += 4 + ((uint)paramCount * 4);
            }
            UnifiedLogger.LogParser(LogLevel.DEBUG, $"   ActionParams: {dialog.Entries.Count + dialog.Replies.Count} lists calculated");

            UnifiedLogger.LogParser(LogLevel.TRACE, $"? ListIndices layout calculated: total size = {currentOffset} bytes");

            return map;
        }
        // ===== Field Creation Helpers (delegated to GffFieldFactory) =====
        private void AddLabelAndField(List<GffField> allFields, List<string> allLabels, string label, uint type, uint value)
            => _fieldFactory.AddLabelAndField(allFields, allLabels, label, type, value);

        private byte[] BuildCExoStringFieldData(string text)
            => _fieldFactory.BuildCExoStringFieldData(text);

        private byte[] BuildCResRefFieldData(string resref)
            => _fieldFactory.BuildCResRefFieldData(resref);

        private uint GetOrCreateTextOffset(string text, List<byte> fieldData)
            => _fieldFactory.GetOrCreateTextOffset(text, fieldData);

        private uint CalculateEntryFieldCount(DialogNode entry)
            => _fieldFactory.CalculateEntryFieldCount(entry);

        private uint CalculateReplyFieldCount(DialogNode reply)
            => _fieldFactory.CalculateReplyFieldCount(reply);

        private uint CalculateLabelSize(List<string> allLabels)
            => _fieldFactory.CalculateLabelSize(allLabels);

        // ===== Field Creation Methods =====
        private void CreatePointerFields(DialogPtr pointer, List<GffField> allFields, List<string> allLabels, List<byte> fieldData, Dialog dialog, ListIndicesOffsetMap offsetMap, int globalPointerIndex)
        {
            // Field 1: Index (DWORD, points to target Entry/Reply index)
            AddLabelAndField(allFields, allLabels, "Index", GffField.DWORD, pointer.Index);

            // Field 2: Active (CResRef, script name or empty)
            string scriptName = pointer.ScriptAppears ?? "";
            if (!string.IsNullOrEmpty(scriptName))
            {
                UnifiedLogger.LogParser(LogLevel.TRACE, $"?? EXPORT SCRIPT: Writing script '{scriptName}' for pointer Index={pointer.Index}");
            }
            uint activeOffset = (uint)fieldData.Count;
            // ?? CRITICAL FIX: Use BuildCResRefFieldData helper for consistent format (length prefix + string)
            if (!string.IsNullOrEmpty(scriptName))
            {
                var resRefData = BuildCResRefFieldData(scriptName);
                fieldData.AddRange(resRefData);
                while (fieldData.Count % 4 != 0) fieldData.Add(0); // 4-byte alignment padding
            }
            else
            {
                // ?? PADDING FIX (2025-10-24): Empty CResRef needs 4 bytes (uint32 length=0) with padding
                fieldData.AddRange(BitConverter.GetBytes((uint)0)); // 4 bytes: length = 0
            }
            AddLabelAndField(allFields, allLabels, "Active", GffField.CResRef, activeOffset);

            // Field 3: ConditionParams (List, points to parameter structs)
            // ?? ARCHITECTURE FIX: Use pre-calculated offset (no placeholder, no patching)
            uint conditionParamsOffset = offsetMap.PointerConditionParamsOffsets[globalPointerIndex];
            AddLabelAndField(allFields, allLabels, "ConditionParams", GffField.List, conditionParamsOffset);
            UnifiedLogger.LogParser(LogLevel.DEBUG, $"   Pointer[{globalPointerIndex}] ConditionParams offset: {conditionParamsOffset}");

            // Field 4: IsChild (BYTE, controls traversal recursion)
            AddLabelAndField(allFields, allLabels, "IsChild", GffField.BYTE, (uint)(pointer.IsLink ? 1 : 0));

            // Field 5 (conditional): LinkComment (CExoString) - ONLY present when IsLink=true
            if (pointer.IsLink)
            {
                if (!string.IsNullOrEmpty(pointer.LinkComment))
                {
                    uint linkCommentOffset = (uint)fieldData.Count;
                    var linkCommentData = BuildCExoStringFieldData(pointer.LinkComment);
                    fieldData.AddRange(linkCommentData);
                    while (fieldData.Count % 4 != 0) fieldData.Add(0);
                    AddLabelAndField(allFields, allLabels, "LinkComment", GffField.CExoString, linkCommentOffset);
                }
                else
                {
                    AddLabelAndField(allFields, allLabels, "LinkComment", GffField.CExoString, 0); // Empty LinkComment
                }
            }
        }

        /// <summary>
        /// Fix DataOrDataOffset for pointer structs created with direct 1:1 export
        /// </summary>
        private void FixDirectPointerStructFieldIndices(List<GffStruct> allStructs, int structStartIndex, int structCount, uint fieldStartIndex)
        {
            // Phase 1 Refactoring: Delegate to GffIndexFixer
            _indexFixer.FixDirectPointerStructFieldIndices(allStructs, structStartIndex, structCount, fieldStartIndex);
        }

        private void FixReplyPointerStructFieldIndices(Dialog dialog, List<GffStruct> allStructs, List<List<int>> replyPointerStructIndices, uint replyPointerFieldStartIndex)
        {
            // Phase 1 Refactoring: Delegate to GffIndexFixer
            _indexFixer.FixReplyPointerStructFieldIndices(dialog, allStructs, replyPointerStructIndices, replyPointerFieldStartIndex);
        }

        private void FixStartWrapperStructFieldIndices(Dialog dialog, List<GffStruct> allStructs, List<int> startStructIndices, uint startWrapperFieldStartIndex)
        {
            // Phase 1 Refactoring: Delegate to GffIndexFixer
            _indexFixer.FixStartWrapperStructFieldIndices(dialog, allStructs, startStructIndices, startWrapperFieldStartIndex);
        }

        private void FixEntryPointerStructFieldIndices(Dialog dialog, List<GffStruct> allStructs, List<List<int>> entryPointerStructIndices)
        {
            // Phase 1 Refactoring: Delegate to GffIndexFixer (pass CalculateEntryFieldCount as delegate)
            _indexFixer.FixEntryPointerStructFieldIndices(dialog, allStructs, entryPointerStructIndices, CalculateEntryFieldCount);
        }

        private void CreateRootFields(Dialog dialog, List<GffField> allFields, List<string> allLabels, List<byte> fieldData, ListIndicesOffsetMap offsetMap)
        {
            UnifiedLogger.LogParser(LogLevel.TRACE, $"?? CreateRootFields: Using pre-calculated offsets (fields {allFields.Count} onward)");

            // Standard DLG root fields
            // ?? CRITICAL: Order must match original GFF files exactly!
            // Correct order: DelayEntry, DelayReply, NumWords, EndConversation, EndConverAbort, PreventZoomIn, EntryList, ReplyList, StartingList

            // Field 0: DelayEntry
            AddLabelAndField(allFields, allLabels, "DelayEntry", GffField.DWORD, dialog.DelayEntry);

            // Field 1: DelayReply
            AddLabelAndField(allFields, allLabels, "DelayReply", GffField.DWORD, dialog.DelayReply);

            // Field 2: NumWords
            AddLabelAndField(allFields, allLabels, "NumWords", GffField.DWORD, dialog.NumWords);

            // ?? CRITICAL: GFF spec reserves offset 0 as "no data" for optional fields
            // Write 4-byte null zone at start of fieldData to prevent offset 0 collisions
            fieldData.AddRange(BitConverter.GetBytes((uint)0));

            // Field 3: EndConversation (CResRef - script reference)
            // Field 4: EndConverAbort (CResRef - script reference)
            // Default: nw_walk_wp (set in Dialog model)
            if (!string.IsNullOrEmpty(dialog.ScriptEnd))
            {
                uint endOffset = (uint)fieldData.Count;
                var endData = BuildCResRefFieldData(dialog.ScriptEnd);
                fieldData.AddRange(endData);
                while (fieldData.Count % 4 != 0) fieldData.Add(0); // 4-byte alignment
                AddLabelAndField(allFields, allLabels, "EndConversation", GffField.CResRef, endOffset);
            }
            else
            {
                AddLabelAndField(allFields, allLabels, "EndConversation", GffField.CResRef, 0);
            }

            if (!string.IsNullOrEmpty(dialog.ScriptAbort))
            {
                uint abortOffset = (uint)fieldData.Count;
                var abortData = BuildCResRefFieldData(dialog.ScriptAbort);
                fieldData.AddRange(abortData);
                while (fieldData.Count % 4 != 0) fieldData.Add(0); // 4-byte alignment
                AddLabelAndField(allFields, allLabels, "EndConverAbort", GffField.CResRef, abortOffset);
            }
            else
            {
                AddLabelAndField(allFields, allLabels, "EndConverAbort", GffField.CResRef, 0);
            }

            // Field 5: PreventZoomIn
            AddLabelAndField(allFields, allLabels, "PreventZoomIn", GffField.BYTE, dialog.PreventZoom ? (byte)1 : (byte)0);

            // ?? ARCHITECTURE FIX: Use pre-calculated offsets (no placeholders, no patching)
            // List fields now have correct offsets from creation
            // GFF List format: count (4 bytes) + indices (count * 4 bytes)

            // Field 6: EntryList
            AddLabelAndField(allFields, allLabels, "EntryList", GffField.List, offsetMap.EntryListOffset);
            UnifiedLogger.LogParser(LogLevel.DEBUG, $"   EntryList offset: {offsetMap.EntryListOffset}");

            // Field 7: ReplyList
            AddLabelAndField(allFields, allLabels, "ReplyList", GffField.List, offsetMap.ReplyListOffset);
            UnifiedLogger.LogParser(LogLevel.DEBUG, $"   ReplyList offset: {offsetMap.ReplyListOffset}");

            // Field 8: StartingList
            AddLabelAndField(allFields, allLabels, "StartingList", GffField.List, offsetMap.StartingListOffset);
            UnifiedLogger.LogParser(LogLevel.DEBUG, $"   StartingList offset: {offsetMap.StartingListOffset}");
        }
        
        

        /// <summary>
        /// Creates fields for a single Entry node (Entry-First batched processing).
        /// </summary>
        private void CreateSingleEntryFields(DialogNode entry, List<GffField> allFields, List<string> allLabels, List<byte> fieldData, ListIndicesOffsetMap offsetMap, int entryIndex)
        {
            // Create ALL Entry fields in exact order: Speaker, Animation, AnimLoop, Text, Script, ActionParams, Delay, Comment, Sound, Quest, RepliesList

            // 1. Speaker (CExoString)
            if (!string.IsNullOrEmpty(entry.Speaker))
            {
                uint speakerOffset = (uint)fieldData.Count;
                var speakerData = BuildCExoStringFieldData(entry.Speaker);
                fieldData.AddRange(speakerData);
                while (fieldData.Count % 4 != 0) fieldData.Add(0);
                AddLabelAndField(allFields, allLabels, "Speaker", GffField.CExoString, speakerOffset);
            }
            else
            {
                AddLabelAndField(allFields, allLabels, "Speaker", GffField.CExoString, 0);
            }

            // 2. Animation (DWORD)
            AddLabelAndField(allFields, allLabels, "Animation", GffField.DWORD, (uint)entry.Animation);

            // 3. AnimLoop (BYTE)
            AddLabelAndField(allFields, allLabels, "AnimLoop", GffField.BYTE, entry.AnimationLoop ? 1u : 0u);

            // 4. Text (CExoLocString) - WITH DEDUPLICATION
            uint textOffset = GetOrCreateTextOffset(entry.Text?.GetDefault() ?? "", fieldData);
            AddLabelAndField(allFields, allLabels, "Text", GffField.CExoLocString, textOffset);

            // 5. Script (CResRef)
            // ?? BUG FIX (Oct 25): Always write CResRef data, never use offset 0 (causes cross-contamination)
            uint scriptOffset = (uint)fieldData.Count;
            if (!string.IsNullOrEmpty(entry.ScriptAction))
            {
                var scriptData = BuildCResRefFieldData(entry.ScriptAction);
                fieldData.AddRange(scriptData);
                while (fieldData.Count % 4 != 0) fieldData.Add(0);
            }
            else
            {
                // Empty CResRef: write 4 bytes (length=0)
                fieldData.AddRange(BitConverter.GetBytes((uint)0));
            }
            AddLabelAndField(allFields, allLabels, "Script", GffField.CResRef, scriptOffset);

            // 6. ActionParams (List) - Each node needs its OWN empty list (not shared!)
            // ?? ARCHITECTURE FIX: Use pre-calculated offset (no placeholder, no patching)
            uint actionParamsOffset = offsetMap.EntryActionParamsOffsets[entryIndex];
            AddLabelAndField(allFields, allLabels, "ActionParams", GffField.List, actionParamsOffset);
            UnifiedLogger.LogParser(LogLevel.DEBUG, $"   Entry[{entryIndex}] ActionParams offset: {actionParamsOffset}");

            // 7. Delay (DWORD)
            AddLabelAndField(allFields, allLabels, "Delay", GffField.DWORD, entry.Delay != uint.MaxValue ? entry.Delay : 0u);

            // 8. Comment (CExoString)
            if (!string.IsNullOrEmpty(entry.Comment))
            {
                uint commentOffset = (uint)fieldData.Count;
                var commentData = BuildCExoStringFieldData(entry.Comment);
                fieldData.AddRange(commentData);
                while (fieldData.Count % 4 != 0) fieldData.Add(0);
                AddLabelAndField(allFields, allLabels, "Comment", GffField.CExoString, commentOffset);
            }
            else
            {
                AddLabelAndField(allFields, allLabels, "Comment", GffField.CExoString, 0);
            }

            // 9. Sound (CResRef)
            // ?? BUG FIX (Oct 25): Always write CResRef data, never use offset 0 (causes cross-contamination)
            uint soundOffset = (uint)fieldData.Count;
            if (!string.IsNullOrEmpty(entry.Sound))
            {
                var soundData = BuildCResRefFieldData(entry.Sound);
                fieldData.AddRange(soundData);
                while (fieldData.Count % 4 != 0) fieldData.Add(0);
            }
            else
            {
                // Empty CResRef: write 4 bytes (length=0)
                fieldData.AddRange(BitConverter.GetBytes((uint)0));
            }
            AddLabelAndField(allFields, allLabels, "Sound", GffField.CResRef, soundOffset);

            // 10. Quest (CExoString)
            if (!string.IsNullOrEmpty(entry.Quest))
            {
                uint questOffset = (uint)fieldData.Count;
                var questData = BuildCExoStringFieldData(entry.Quest);
                fieldData.AddRange(questData);
                while (fieldData.Count % 4 != 0) fieldData.Add(0);
                AddLabelAndField(allFields, allLabels, "Quest", GffField.CExoString, questOffset);

                // 11. QuestEntry (DWORD) - ONLY present when Quest is non-empty
                if (entry.QuestEntry != uint.MaxValue)
                {
                    AddLabelAndField(allFields, allLabels, "QuestEntry", GffField.DWORD, entry.QuestEntry);
                }
                else
                {
                    AddLabelAndField(allFields, allLabels, "QuestEntry", GffField.DWORD, 0u);
                }
            }
            else
            {
                AddLabelAndField(allFields, allLabels, "Quest", GffField.CExoString, 0);
                // QuestEntry is NOT included when Quest is empty
            }

            // 12. RepliesList (List) - ?? ARCHITECTURE FIX: Use pre-calculated offset
            uint repliesListOffset = offsetMap.RepliesListOffsets[entryIndex];
            AddLabelAndField(allFields, allLabels, "RepliesList", GffField.List, repliesListOffset);
            UnifiedLogger.LogParser(LogLevel.DEBUG, $"   Entry[{entryIndex}] RepliesList offset: {repliesListOffset}");
        }

        /// <summary>
        /// Creates fields for a single Reply node (Entry-First batched processing).
        /// </summary>
        private void CreateSingleReplyFields(DialogNode reply, List<GffField> allFields, List<string> allLabels, List<byte> fieldData, ListIndicesOffsetMap offsetMap, int replyIndex)
        {
            // Create ALL Reply fields in exact order: Animation, AnimLoop, Text, Script, ActionParams, Delay, Comment, Sound, Quest, EntriesList

            // 1. Animation (DWORD)
            AddLabelAndField(allFields, allLabels, "Animation", GffField.DWORD, (uint)reply.Animation);

            // 2. AnimLoop (BYTE)
            AddLabelAndField(allFields, allLabels, "AnimLoop", GffField.BYTE, reply.AnimationLoop ? 1u : 0u);

            // 3. Text (CExoLocString) - WITH DEDUPLICATION
            uint textOffset = GetOrCreateTextOffset(reply.Text?.GetDefault() ?? "", fieldData);
            AddLabelAndField(allFields, allLabels, "Text", GffField.CExoLocString, textOffset);

            // 4. Script (CResRef)
            // ?? BUG FIX (Oct 25): Always write CResRef data, never use offset 0 (causes cross-contamination)
            uint scriptOffset = (uint)fieldData.Count;
            if (!string.IsNullOrEmpty(reply.ScriptAction))
            {
                var scriptData = BuildCResRefFieldData(reply.ScriptAction);
                fieldData.AddRange(scriptData);
                while (fieldData.Count % 4 != 0) fieldData.Add(0);
            }
            else
            {
                // Empty CResRef: write 4 bytes (length=0)
                fieldData.AddRange(BitConverter.GetBytes((uint)0));
            }
            AddLabelAndField(allFields, allLabels, "Script", GffField.CResRef, scriptOffset);

            // 5. ActionParams (List) - Each node needs its OWN empty list (not shared!)
            // ?? ARCHITECTURE FIX: Use pre-calculated offset (no placeholder, no patching)
            uint actionParamsOffset = offsetMap.ReplyActionParamsOffsets[replyIndex];
            AddLabelAndField(allFields, allLabels, "ActionParams", GffField.List, actionParamsOffset);
            UnifiedLogger.LogParser(LogLevel.DEBUG, $"   Reply[{replyIndex}] ActionParams offset: {actionParamsOffset}");

            // 6. Delay (DWORD)
            AddLabelAndField(allFields, allLabels, "Delay", GffField.DWORD, reply.Delay != uint.MaxValue ? reply.Delay : 0u);

            // 7. Comment (CExoString)
            if (!string.IsNullOrEmpty(reply.Comment))
            {
                uint commentOffset = (uint)fieldData.Count;
                var commentData = BuildCExoStringFieldData(reply.Comment);
                fieldData.AddRange(commentData);
                while (fieldData.Count % 4 != 0) fieldData.Add(0);
                AddLabelAndField(allFields, allLabels, "Comment", GffField.CExoString, commentOffset);
            }
            else
            {
                AddLabelAndField(allFields, allLabels, "Comment", GffField.CExoString, 0);
            }

            // 8. Sound (CResRef)
            // ?? BUG FIX (Oct 25): Always write CResRef data, never use offset 0 (causes cross-contamination)
            uint soundOffset = (uint)fieldData.Count;
            if (!string.IsNullOrEmpty(reply.Sound))
            {
                var soundData = BuildCResRefFieldData(reply.Sound);
                fieldData.AddRange(soundData);
                while (fieldData.Count % 4 != 0) fieldData.Add(0);
            }
            else
            {
                // Empty CResRef: write 4 bytes (length=0)
                fieldData.AddRange(BitConverter.GetBytes((uint)0));
            }
            AddLabelAndField(allFields, allLabels, "Sound", GffField.CResRef, soundOffset);

            // 9. Quest (CExoString)
            if (!string.IsNullOrEmpty(reply.Quest))
            {
                uint questOffset = (uint)fieldData.Count;
                var questData = BuildCExoStringFieldData(reply.Quest);
                fieldData.AddRange(questData);
                while (fieldData.Count % 4 != 0) fieldData.Add(0);
                AddLabelAndField(allFields, allLabels, "Quest", GffField.CExoString, questOffset);

                // 10. QuestEntry (DWORD) - ONLY present when Quest is non-empty
                if (reply.QuestEntry != uint.MaxValue)
                {
                    AddLabelAndField(allFields, allLabels, "QuestEntry", GffField.DWORD, reply.QuestEntry);
                }
                else
                {
                    AddLabelAndField(allFields, allLabels, "QuestEntry", GffField.DWORD, 0u);
                }
            }
            else
            {
                AddLabelAndField(allFields, allLabels, "Quest", GffField.CExoString, 0);
                // QuestEntry is NOT included when Quest is empty
            }

            // 11. EntriesList (List) - ?? ARCHITECTURE FIX: Use pre-calculated offset
            uint entriesListOffset = offsetMap.EntriesListOffsets[replyIndex];
            AddLabelAndField(allFields, allLabels, "EntriesList", GffField.List, entriesListOffset);
            UnifiedLogger.LogParser(LogLevel.DEBUG, $"   Reply[{replyIndex}] EntriesList offset: {entriesListOffset}");
        }

        
        private void CreateStartingFields(Dialog dialog, List<GffField> allFields, List<string> allLabels, List<byte> fieldData, ListIndicesOffsetMap offsetMap)
        {
            for (int i = 0; i < dialog.Starts.Count; i++)
            {
                var start = dialog.Starts[i];
                // Create all 3 fields required by Start wrapper struct (Index, Active, ConditionParams)
                AddLabelAndField(allFields, allLabels, "Index", GffField.DWORD, start.Index);

                // Active field contains the starting conditional script name (ScriptAppears)
                // ?? CRITICAL FIX: Use BuildCResRefFieldData for GFF spec compliance (size-prefix format)
                var activeScriptOffset = (uint)fieldData.Count;
                if (!string.IsNullOrEmpty(start.ScriptAppears))
                {
                    var resRefData = BuildCResRefFieldData(start.ScriptAppears);
                    fieldData.AddRange(resRefData);
                    while (fieldData.Count % 4 != 0) fieldData.Add(0); // 4-byte alignment padding
                    UnifiedLogger.LogParser(LogLevel.DEBUG, $"Start[{i}] Active field: '{start.ScriptAppears}'");
                }
                else
                {
                    fieldData.Add(0); // Empty CResRef: size = 0
                }
                AddLabelAndField(allFields, allLabels, "Active", GffField.CResRef, activeScriptOffset);

                // ?? ARCHITECTURE FIX: Use pre-calculated offset (no placeholder, no patching)
                uint conditionParamsOffset = offsetMap.StartConditionParamsOffsets[i];
                AddLabelAndField(allFields, allLabels, "ConditionParams", GffField.List, conditionParamsOffset);
                UnifiedLogger.LogParser(LogLevel.DEBUG, $"   Start[{i}] ConditionParams offset: {conditionParamsOffset}");
            }
        }
        
        private void CreateEntryPointerFields(Dialog dialog, List<GffField> allFields, List<string> allLabels, List<byte> fieldData)
        {
            // Create fields for entry pointer structs
            for (int entryIdx = 0; entryIdx < dialog.Entries.Count; entryIdx++)
            {
                var entry = dialog.Entries[entryIdx];

                // Write ALL pointers - Entry may point to same Reply multiple times with different conditions
                foreach (var pointer in entry.Pointers)
                {
                    // Entry.RepliesList uses LOCAL Reply array indices, not global struct indices
                    // This is different from compact pointer structs which use global indices
                    AddLabelAndField(allFields, allLabels, "Index", GffField.DWORD, pointer.Index);

                    // Active field (default to 1/true for active pointers)
                    AddLabelAndField(allFields, allLabels, "Active", GffField.BYTE, 1);

                    // ConditionParams field (empty for most pointers)
                    uint conditionParamsOffset = (uint)fieldData.Count;
                    fieldData.AddRange(new byte[16]); // Empty CResRef
                    AddLabelAndField(allFields, allLabels, "ConditionParams", GffField.CResRef, conditionParamsOffset);

                    // IsChild field (use IsLink property)
                    AddLabelAndField(allFields, allLabels, "IsChild", GffField.BYTE, (uint)(pointer.IsLink ? 1 : 0));
                }
            }
        }
        
        private void CreateCompactPointerFields(List<DialogEditor.Utils.UniquePointer> uniquePointers, List<GffField> allFields, List<string> allLabels, List<byte> fieldData, DialogEditor.Utils.FieldIndexTracker fieldTracker, Dialog dialog)
        {
            // ?? COMPACT POINTER FIELDS: Create fields only for unique pointer structs identified by compact algorithm

            uint startingFieldIndex = (uint)allFields.Count;
            UnifiedLogger.LogParser(LogLevel.TRACE, $"?? CreateCompactPointerFields: Starting at field index {startingFieldIndex} (compact approach)");

            // Validate field index alignment before creating fields
            fieldTracker.ValidateFieldIndex(startingFieldIndex, "Compact Pointer Field Start");

            // Create fields for each unique pointer struct
            for (int i = 0; i < uniquePointers.Count; i++)
            {
                var uniquePtr = uniquePointers[i];

                // Compact pointer structs also use LOCAL array indices, not global struct indices
                // All pointer Index fields in DLG files are local indices
                uint indexFieldIndex = (uint)allFields.Count;
                fieldTracker.ValidateFieldIndex(indexFieldIndex, $"UniquePtr[{i}] Index");
                AddLabelAndField(allFields, allLabels, "Index", GffField.DWORD, uniquePtr.Index);

                // Active field (default to 1/true for active pointers)
                uint activeFieldIndex = (uint)allFields.Count;
                fieldTracker.ValidateFieldIndex(activeFieldIndex, $"UniquePtr[{i}] Active");
                AddLabelAndField(allFields, allLabels, "Active", GffField.BYTE, 1);

                // ConditionParams field
                uint condParamsFieldIndex = (uint)allFields.Count;
                fieldTracker.ValidateFieldIndex(condParamsFieldIndex, $"UniquePtr[{i}] ConditionParams");
                uint conditionParamsOffset = (uint)fieldData.Count;
                fieldData.AddRange(new byte[16]); // Empty CResRef
                AddLabelAndField(allFields, allLabels, "ConditionParams", GffField.CResRef, conditionParamsOffset);

                // IsChild field (use IsLink property)
                uint isChildFieldIndex = (uint)allFields.Count;
                fieldTracker.ValidateFieldIndex(isChildFieldIndex, $"UniquePtr[{i}] IsChild");
                AddLabelAndField(allFields, allLabels, "IsChild", GffField.BYTE, (uint)(uniquePtr.IsLink ? 1 : 0));
            }

            uint endingFieldIndex = (uint)allFields.Count;
            UnifiedLogger.LogParser(LogLevel.TRACE, $"?? CreateCompactPointerFields: Created {endingFieldIndex - startingFieldIndex} fields for {uniquePointers.Count} unique pointers (index {startingFieldIndex}-{endingFieldIndex - 1})");
        }

        // Removed deprecated CreateReplyPointerFields method - replaced by CreateCompactPointerFields 2025-09-29

        private void CreateStartPointerFields(Dialog dialog, List<GffField> allFields, List<string> allLabels, List<byte> fieldData)
        {
            uint startingFieldIndex = (uint)allFields.Count;
            UnifiedLogger.LogParser(LogLevel.TRACE, $"?? CreateStartPointerFields: Starting at field index {startingFieldIndex} (append-only approach)");
            UnifiedLogger.LogParser(LogLevel.TRACE, $"?? DEBUG: CreateStartPointerFields called with {dialog.Starts.Count} starts");

            // Create fields for start pointer structs using append-only approach
            for (int startIdx = 0; startIdx < dialog.Starts.Count; startIdx++)
            {
                var start = dialog.Starts[startIdx];

                // StartingList uses LOCAL Entry array indices, not global struct indices
                AddLabelAndField(allFields, allLabels, "Index", GffField.DWORD, start.Index);

                // Active field (default to 1/true for active pointers)
                AddLabelAndField(allFields, allLabels, "Active", GffField.BYTE, 1);

                // ConditionParams field
                uint conditionParamsOffset = (uint)fieldData.Count;
                fieldData.AddRange(new byte[16]); // Empty CResRef
                AddLabelAndField(allFields, allLabels, "ConditionParams", GffField.CResRef, conditionParamsOffset);

                // IsChild field (always 0 for starts)
                AddLabelAndField(allFields, allLabels, "IsChild", GffField.BYTE, 0);
            }

            uint endingFieldIndex = (uint)allFields.Count;
            UnifiedLogger.LogParser(LogLevel.TRACE, $"?? CreateStartPointerFields: Created {endingFieldIndex - startingFieldIndex} fields (index {startingFieldIndex}-{endingFieldIndex - 1})");
        }

        private void InsertFieldAtIndex(List<GffField> allFields, List<string> allLabels, int fieldIndex, string label, uint type, uint value)
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
                Label = label,
                DataOrDataOffset = value
            };

            // Ensure the list is large enough
            while (allFields.Count <= fieldIndex)
            {
                allFields.Add(null!);
                UnifiedLogger.LogParser(LogLevel.DEBUG, $"?? Added null padding at index {allFields.Count - 1}");
            }

            allFields[fieldIndex] = field;
            UnifiedLogger.LogParser(LogLevel.DEBUG, $"?? Inserted field '{label}' at index {fieldIndex} with value {value}");
        }


        // ===== Struct Creation Methods =====
        private void CreateInterleavedStructs(
            Dialog dialog,
            List<GffStruct> allStructs,
            InterleavedTraversalState state)
        {
            UnifiedLogger.LogParser(LogLevel.TRACE, "?? Creating structs in ENTRY-FIRST BATCHED order (2025-10-22 discovery)");

            // Initialize pointer tracking lists
            for (int i = 0; i < dialog.Entries.Count; i++)
            {
                state.EntryPointerStructIndices.Add(new List<int>());
            }
            for (int i = 0; i < dialog.Replies.Count; i++)
            {
                state.ReplyPointerStructIndices.Add(new List<int>());
            }

            // ?? CRITICAL DISCOVERY (2025-10-22): GFF uses BATCHED Entry-First traversal
            // NOT conversation-flow, NOT depth-first, NOT breadth-first
            // Algorithm:
            //   1. Process ALL Entries (in array order) with their pointers
            //   2. Then process ALL Replies (in array order) with their pointers
            //   3. Finally add Start structs at end

            // Phase 1: Process ALL Entries (in array order)
            UnifiedLogger.LogParser(LogLevel.TRACE, "?? Phase 1: Creating ALL Entry structs + pointers");
            for (uint i = 0; i < dialog.Entries.Count; i++)
            {
                CreateEntryStruct(dialog, i, allStructs, state);
            }

            // Phase 2: Process ALL Replies (in array order)
            UnifiedLogger.LogParser(LogLevel.TRACE, "?? Phase 2: Creating ALL Reply structs + pointers");
            for (uint i = 0; i < dialog.Replies.Count; i++)
            {
                CreateReplyStruct(dialog, i, allStructs, state);
            }

            // Phase 3: Create Start wrapper structs at the END
            UnifiedLogger.LogParser(LogLevel.TRACE, "?? Phase 3: Creating Start structs at end (structural pattern)");
            foreach (var start in dialog.Starts)
            {
                var startStruct = new GffStruct
                {
                    Type = start.OriginalGffStruct?.Type ?? 0,
                    DataOrDataOffset = state.CurrentFieldIndex * 4,
                    FieldCount = 3 // Index, Active, ConditionParams
                };
                allStructs.Add(startStruct);
                state.StartStructIndices.Add(allStructs.Count - 1);
                state.CurrentFieldIndex += 3;

                UnifiedLogger.LogParser(LogLevel.DEBUG, $"  Start[{state.StartStructIndices.Count - 1}] ? Struct[{allStructs.Count - 1}] ? Entry[{start.Index}]");
            }

            UnifiedLogger.LogParser(LogLevel.TRACE, $"? Entry-First batched struct creation complete: {allStructs.Count} total structs");
        }

        /// <summary>
        /// Creates Entry struct + all its Pointer structs (NO recursion - batched processing).
        /// </summary>
        private void CreateEntryStruct(
            Dialog dialog,
            uint entryIndex,
            List<GffStruct> allStructs,
            InterleavedTraversalState state)
        {
            if (entryIndex >= dialog.Entries.Count)
            {
                UnifiedLogger.LogParser(LogLevel.WARN, $"?? Invalid entry index: {entryIndex}");
                return;
            }

            var entry = dialog.Entries[(int)entryIndex];

            // Create Entry struct
            uint fieldCount = CalculateEntryFieldCount(entry);
            var entryStruct = new GffStruct
            {
                Type = entry.OriginalGffStruct?.Type ?? 0,
                DataOrDataOffset = state.CurrentFieldIndex * 4,
                FieldCount = fieldCount
            };
            allStructs.Add(entryStruct);
            int entryStructIndex = allStructs.Count - 1;
            state.EntryStructIndices.Add(entryStructIndex);
            state.EntryIndexToStructIndex[entryIndex] = entryStructIndex;
            state.CurrentFieldIndex += fieldCount;

            UnifiedLogger.LogParser(LogLevel.DEBUG, $"  Entry[{entryIndex}] ? Struct[{entryStructIndex}] ({fieldCount} fields)");

            // Create ALL pointer structs for this entry (pointers-first within node)
            foreach (var pointer in entry.Pointers)
            {
                // Field count: 4 base fields + 1 conditional LinkComment field
                uint pointerFieldCount = pointer.IsLink ? 5u : 4u;

                var ptrStruct = new GffStruct
                {
                    Type = pointer.OriginalGffStruct?.Type ?? 0,
                    DataOrDataOffset = state.CurrentFieldIndex * 4,
                    FieldCount = pointerFieldCount
                };
                allStructs.Add(ptrStruct);
                state.EntryPointerStructIndices[(int)entryIndex].Add(allStructs.Count - 1);
                state.CurrentFieldIndex += pointerFieldCount;

                UnifiedLogger.LogParser(LogLevel.DEBUG, $"    Entry[{entryIndex}].Pointer ? Struct[{allStructs.Count - 1}] ? Reply[{pointer.Index}] ({pointerFieldCount} fields, IsLink={pointer.IsLink})");
            }
        }

        /// <summary>
        /// Creates Reply struct + all its Pointer structs (NO recursion - batched processing).
        /// </summary>
        private void CreateReplyStruct(
            Dialog dialog,
            uint replyIndex,
            List<GffStruct> allStructs,
            InterleavedTraversalState state)
        {
            if (replyIndex >= dialog.Replies.Count)
            {
                UnifiedLogger.LogParser(LogLevel.WARN, $"?? Invalid reply index: {replyIndex}");
                return;
            }

            var reply = dialog.Replies[(int)replyIndex];

            // Create Reply struct
            uint fieldCount = CalculateReplyFieldCount(reply);
            var replyStruct = new GffStruct
            {
                Type = reply.OriginalGffStruct?.Type ?? 0,
                DataOrDataOffset = state.CurrentFieldIndex * 4,
                FieldCount = fieldCount
            };
            allStructs.Add(replyStruct);
            int replyStructIndex = allStructs.Count - 1;
            state.ReplyStructIndices.Add(replyStructIndex);
            state.ReplyIndexToStructIndex[replyIndex] = replyStructIndex;
            state.CurrentFieldIndex += fieldCount;

            UnifiedLogger.LogParser(LogLevel.DEBUG, $"  Reply[{replyIndex}] ? Struct[{replyStructIndex}] ({fieldCount} fields)");

            // Create ALL pointer structs for this reply (pointers-first within node)
            foreach (var pointer in reply.Pointers)
            {
                // Field count: 4 base fields + 1 conditional LinkComment field
                uint pointerFieldCount = pointer.IsLink ? 5u : 4u;

                var ptrStruct = new GffStruct
                {
                    Type = pointer.OriginalGffStruct?.Type ?? 0,
                    DataOrDataOffset = state.CurrentFieldIndex * 4,
                    FieldCount = pointerFieldCount
                };
                allStructs.Add(ptrStruct);
                state.ReplyPointerStructIndices[(int)replyIndex].Add(allStructs.Count - 1);
                state.CurrentFieldIndex += pointerFieldCount;

                UnifiedLogger.LogParser(LogLevel.DEBUG, $"    Reply[{replyIndex}].Pointer ? Struct[{allStructs.Count - 1}] ? Entry[{pointer.Index}] ({pointerFieldCount} fields, IsLink={pointer.IsLink})");
            }
        }


        // ===== Parameter Struct Creation =====
        private void CreateDynamicParameterStructs(Dialog dialog, List<GffStruct> allStructs, List<GffField> allFields, List<string> allLabels, List<byte> fieldData)
        {
            UnifiedLogger.LogParser(LogLevel.TRACE, "?? DYNAMIC PARAMETER CREATION: Collecting all ConditionParams from parsed dialog");

            // Collect all unique ConditionParams from the entire dialog
            var uniqueConditionParams = new Dictionary<string, string>();
            var allConditionParamsLists = new List<Dictionary<string, string>>();
            int totalConditionParamsCount = 0;

            // Collect from Entry pointers
            foreach (var entry in dialog.Entries)
            {
                foreach (var pointer in entry.Pointers)
                {
                    foreach (var kvp in pointer.ConditionParams)
                    {
                        totalConditionParamsCount++;
                        if (!uniqueConditionParams.ContainsKey(kvp.Key) || uniqueConditionParams[kvp.Key] != kvp.Value)
                        {
                            string uniqueKey = $"{kvp.Key}={kvp.Value}";
                            if (!uniqueConditionParams.ContainsKey(uniqueKey))
                            {
                                uniqueConditionParams[uniqueKey] = kvp.Value;
                                UnifiedLogger.LogParser(LogLevel.TRACE, $"   Found Entry ConditionParam: {kvp.Key} = {kvp.Value}");
                            }
                        }
                    }
                }
            }

            // Collect from Reply pointers
            foreach (var reply in dialog.Replies)
            {
                foreach (var pointer in reply.Pointers)
                {
                    foreach (var kvp in pointer.ConditionParams)
                    {
                        totalConditionParamsCount++;
                        if (!uniqueConditionParams.ContainsKey(kvp.Key) || uniqueConditionParams[kvp.Key] != kvp.Value)
                        {
                            string uniqueKey = $"{kvp.Key}={kvp.Value}";
                            if (!uniqueConditionParams.ContainsKey(uniqueKey))
                            {
                                uniqueConditionParams[uniqueKey] = kvp.Value;
                                UnifiedLogger.LogParser(LogLevel.TRACE, $"   Found Reply ConditionParam: {kvp.Key} = {kvp.Value}");
                            }
                        }
                    }
                }
            }

            // Collect from Start pointers
            foreach (var start in dialog.Starts)
            {
                foreach (var kvp in start.ConditionParams)
                {
                    totalConditionParamsCount++;
                    if (!uniqueConditionParams.ContainsKey(kvp.Key) || uniqueConditionParams[kvp.Key] != kvp.Value)
                    {
                        string uniqueKey = $"{kvp.Key}={kvp.Value}";
                        if (!uniqueConditionParams.ContainsKey(uniqueKey))
                        {
                            uniqueConditionParams[uniqueKey] = kvp.Value;
                            UnifiedLogger.LogParser(LogLevel.TRACE, $"   Found Start ConditionParam: {kvp.Key} = {kvp.Value}");
                        }
                    }
                }
            }

            UnifiedLogger.LogParser(LogLevel.TRACE, $"?? CONDITION PARAMS ANALYSIS:");
            UnifiedLogger.LogParser(LogLevel.TRACE, $"   Total ConditionParams references: {totalConditionParamsCount}");
            UnifiedLogger.LogParser(LogLevel.TRACE, $"   Unique ConditionParams found: {uniqueConditionParams.Count}");

            // Create parameter structs for each unique ConditionParam
            uint currentFieldIndex = (uint)allFields.Count;
            uint structTypeCounter = 0;

            foreach (var kvp in uniqueConditionParams)
            {
                // Parse the key back from "key=value" format
                string[] parts = kvp.Key.Split('=');
                string key = parts[0];
                string value = kvp.Value;

                var paramStruct = new GffStruct
                {
                    Type = 0, // ?? AURORA FIX: All structs use Type 0 (except root)
                    DataOrDataOffset = currentFieldIndex * 4, // Point to where parameter fields will be created
                    FieldCount = 2 // Key + Value fields
                };
                allStructs.Add(paramStruct);

                // Create the Key and Value fields
                AddParameterKeyValueFields(allFields, allLabels, fieldData, key, value);
                currentFieldIndex += 2; // Move to next field index (2 fields per parameter)

                UnifiedLogger.LogParser(LogLevel.TRACE, $"   Created parameter struct[{allStructs.Count - 1}] Type={structTypeCounter - 1} for {key}={value}");
            }

            UnifiedLogger.LogParser(LogLevel.TRACE, $"?? DYNAMIC PARAMETER CREATION: Created {uniqueConditionParams.Count} parameter structs, total structs now: {allStructs.Count}");
        }

        /// <summary>
        /// Create parameter structs for each pointer's ConditionParams and track the mapping
        /// This enables list indices to properly link ConditionParams Lists to parameter structs
        /// </summary>
        private void CreatePointerParameterStructs(
            Dialog dialog,
            List<GffStruct> allStructs,
            List<GffField> allFields,
            List<string> allLabels,
            List<byte> fieldData,
            uint entryPointerFieldStartIndex,
            uint replyPointerFieldStartIndex,
            uint startWrapperFieldStartIndex,
            Dictionary<int, List<int>> pointerConditionParamsMapping)
        {
            UnifiedLogger.LogParser(LogLevel.TRACE, $"?? POINTER PARAMETER CREATION: Creating parameter structs for pointer ConditionParams");

            uint structTypeCounter = (uint)allStructs.Count; // Continue from existing struct types
            uint currentFieldIndex = (uint)allFields.Count;

            // Create parameters for starting pointers
            // Start fields: Index, Active, ConditionParams
            // ConditionParams is at offset +2
            int startFieldIndex = (int)startWrapperFieldStartIndex;
            for (int startIdx = 0; startIdx < dialog.Starts.Count; startIdx++)
            {
                var start = dialog.Starts[startIdx];
                int conditionParamsFieldIndex = startFieldIndex + 2; // ConditionParams is 3rd field (0-indexed = 2)

                if (start.ConditionParams != null && start.ConditionParams.Count > 0)
                {
                    var paramStructIndices = new List<int>();

                    foreach (var kvp in start.ConditionParams)
                    {
                        // Create parameter struct
                        var paramStruct = new GffStruct
                        {
                            Type = 0, // ?? AURORA FIX: All structs use Type 0 (except root)
                            DataOrDataOffset = currentFieldIndex * 4,
                            FieldCount = 2 // Key + Value
                        };
                        allStructs.Add(paramStruct);
                        paramStructIndices.Add(allStructs.Count - 1);

                        // Create Key and Value fields
                        AddParameterKeyValueFields(allFields, allLabels, fieldData, kvp.Key, kvp.Value);
                        currentFieldIndex += 2;

                        UnifiedLogger.LogParser(LogLevel.DEBUG, $"Start[{startIdx}] ConditionParams: Created param struct[{allStructs.Count - 1}] for {kvp.Key}={kvp.Value}");
                    }

                    pointerConditionParamsMapping[conditionParamsFieldIndex] = paramStructIndices;
                    UnifiedLogger.LogParser(LogLevel.TRACE, $"Start[{startIdx}] ConditionParams: Mapped {paramStructIndices.Count} param structs to field {conditionParamsFieldIndex}");
                }

                startFieldIndex += 3; // Move to next start's fields (3 fields per start)
            }

            // Create parameters for entry pointers
            int entryPointerFieldIndex = (int)entryPointerFieldStartIndex;
            for (int entryIdx = 0; entryIdx < dialog.Entries.Count; entryIdx++)
            {
                var entry = dialog.Entries[entryIdx];
                foreach (var pointer in entry.Pointers)
                {
                    // ConditionParams field is at offset +2 (Index, Active, ConditionParams, IsChild)
                    int conditionParamsFieldIndex = entryPointerFieldIndex + 2;

                    if (pointer.ConditionParams != null && pointer.ConditionParams.Count > 0)
                    {
                        var paramStructIndices = new List<int>();

                        foreach (var kvp in pointer.ConditionParams)
                        {
                            // Create parameter struct
                            var paramStruct = new GffStruct
                            {
                                Type = 0, // ?? AURORA FIX: All structs use Type 0 (except root)
                                DataOrDataOffset = currentFieldIndex * 4,
                                FieldCount = 2 // Key + Value
                            };
                            allStructs.Add(paramStruct);
                            paramStructIndices.Add(allStructs.Count - 1);

                            // Create Key and Value fields
                            AddParameterKeyValueFields(allFields, allLabels, fieldData, kvp.Key, kvp.Value);
                            currentFieldIndex += 2;

                            UnifiedLogger.LogParser(LogLevel.DEBUG, $"Entry[{entryIdx}] Ptr: Created param struct[{allStructs.Count - 1}] for {kvp.Key}={kvp.Value}");
                        }

                        pointerConditionParamsMapping[conditionParamsFieldIndex] = paramStructIndices;
                        UnifiedLogger.LogParser(LogLevel.TRACE, $"Entry[{entryIdx}] Ptr: Mapped {paramStructIndices.Count} param structs to field {conditionParamsFieldIndex}");
                    }

                    entryPointerFieldIndex += 4; // Move to next pointer's fields
                }
            }

            // Create parameters for reply pointers
            int replyPointerFieldIndex = (int)replyPointerFieldStartIndex;
            for (int replyIdx = 0; replyIdx < dialog.Replies.Count; replyIdx++)
            {
                var reply = dialog.Replies[replyIdx];
                foreach (var pointer in reply.Pointers)
                {
                    // ConditionParams field is at offset +2
                    int conditionParamsFieldIndex = replyPointerFieldIndex + 2;

                    if (pointer.ConditionParams != null && pointer.ConditionParams.Count > 0)
                    {
                        var paramStructIndices = new List<int>();

                        foreach (var kvp in pointer.ConditionParams)
                        {
                            // Create parameter struct
                            var paramStruct = new GffStruct
                            {
                                Type = 0, // ?? AURORA FIX: All structs use Type 0 (except root)
                                DataOrDataOffset = currentFieldIndex * 4,
                                FieldCount = 2 // Key + Value
                            };
                            allStructs.Add(paramStruct);
                            paramStructIndices.Add(allStructs.Count - 1);

                            // Create Key and Value fields
                            AddParameterKeyValueFields(allFields, allLabels, fieldData, kvp.Key, kvp.Value);
                            currentFieldIndex += 2;

                            UnifiedLogger.LogParser(LogLevel.DEBUG, $"Reply[{replyIdx}] Ptr: Created param struct[{allStructs.Count - 1}] for {kvp.Key}={kvp.Value}");
                        }

                        pointerConditionParamsMapping[conditionParamsFieldIndex] = paramStructIndices;
                        UnifiedLogger.LogParser(LogLevel.TRACE, $"Reply[{replyIdx}] Ptr: Mapped {paramStructIndices.Count} param structs to field {conditionParamsFieldIndex}");
                    }

                    replyPointerFieldIndex += 4; // Move to next pointer's fields
                }
            }

            UnifiedLogger.LogParser(LogLevel.TRACE, $"?? POINTER PARAMETER CREATION: Created parameter structs for {pointerConditionParamsMapping.Count} pointers, total structs now: {allStructs.Count}");
        }

        /// <summary>
        /// Create parameter structs for each node's ActionParams and track the mapping
        /// This enables list indices to properly link ActionParams Lists to parameter structs
        /// </summary>
        private void CreateNodeActionParameterStructs(
            Dialog dialog,
            List<GffStruct> allStructs,
            List<GffField> allFields,
            List<string> allLabels,
            List<byte> fieldData,
            uint entryFieldStartIndex,
            uint replyFieldStartIndex,
            List<int> entryStructIndices,
            List<int> replyStructIndices,
            Dictionary<int, List<int>> nodeActionParamsMapping)
        {
            UnifiedLogger.LogParser(LogLevel.TRACE, $"?? NODE PARAMETER CREATION: Creating parameter structs for node ActionParams");

            uint structTypeCounter = (uint)allStructs.Count; // Continue from existing struct types
            uint currentFieldIndex = (uint)allFields.Count;

            // Create parameters for entry nodes
            // Entry fields: Speaker, Animation, AnimLoop, Text, Script, ActionParams, Delay, Comment, Sound, Quest, RepliesList
            // ActionParams is at offset +5 from entry field start
            int entryFieldIndex = (int)entryFieldStartIndex;
            for (int entryIdx = 0; entryIdx < dialog.Entries.Count; entryIdx++)
            {
                var entry = dialog.Entries[entryIdx];
                int actionParamsFieldIndex = entryFieldIndex + 5; // ActionParams is 6th field (0-indexed = 5)

                if (entry.ActionParams != null && entry.ActionParams.Count > 0)
                {
                    var paramStructIndices = new List<int>();

                    foreach (var kvp in entry.ActionParams)
                    {
                        // Create parameter struct
                        var paramStruct = new GffStruct
                        {
                            Type = 0, // ?? AURORA FIX: All structs use Type 0 (except root)
                            DataOrDataOffset = currentFieldIndex * 4,
                            FieldCount = 2 // Key + Value
                        };
                        allStructs.Add(paramStruct);
                        paramStructIndices.Add(allStructs.Count - 1);

                        // Create Key and Value fields
                        AddParameterKeyValueFields(allFields, allLabels, fieldData, kvp.Key, kvp.Value);
                        currentFieldIndex += 2;

                        UnifiedLogger.LogParser(LogLevel.DEBUG, $"Entry[{entryIdx}] ActionParams: Created param struct[{allStructs.Count - 1}] for {kvp.Key}={kvp.Value}");
                    }

                    // ?? KEY FIX: Use GFF struct index, not Dialog node index
                    // Write loop iterates by GFF struct order, so use struct index from tracking list
                    int structIdx = entryStructIndices[entryIdx];
                    nodeActionParamsMapping[structIdx] = paramStructIndices;
                    UnifiedLogger.LogParser(LogLevel.TRACE, $"Entry[{entryIdx}] ActionParams: Mapped {paramStructIndices.Count} param structs to GFF struct[{structIdx}]");
                }

                entryFieldIndex += 11; // Move to next entry's fields (11 fields per entry)
            }

            // Create parameters for reply nodes
            // Reply fields: Animation, AnimLoop, Text, Script, ActionParams, Delay, Comment, Sound, Quest, EntriesList
            // ActionParams is at offset +4 from reply field start
            int replyFieldIndex = (int)replyFieldStartIndex;
            for (int replyIdx = 0; replyIdx < dialog.Replies.Count; replyIdx++)
            {
                var reply = dialog.Replies[replyIdx];
                int actionParamsFieldIndex = replyFieldIndex + 4; // ActionParams is 5th field (0-indexed = 4)

                if (reply.ActionParams != null && reply.ActionParams.Count > 0)
                {
                    var paramStructIndices = new List<int>();

                    foreach (var kvp in reply.ActionParams)
                    {
                        // Create parameter struct
                        var paramStruct = new GffStruct
                        {
                            Type = 0, // ?? AURORA FIX: All structs use Type 0 (except root)
                            DataOrDataOffset = currentFieldIndex * 4,
                            FieldCount = 2 // Key + Value
                        };
                        allStructs.Add(paramStruct);
                        paramStructIndices.Add(allStructs.Count - 1);

                        // Create Key and Value fields
                        AddParameterKeyValueFields(allFields, allLabels, fieldData, kvp.Key, kvp.Value);
                        currentFieldIndex += 2;

                        UnifiedLogger.LogParser(LogLevel.DEBUG, $"Reply[{replyIdx}] ActionParams: Created param struct[{allStructs.Count - 1}] for {kvp.Key}={kvp.Value}");
                    }

                    // ?? KEY FIX: Use GFF struct index, not Dialog node index
                    // Write loop iterates by GFF struct order, so use struct index from tracking list
                    int structIdx = replyStructIndices[replyIdx];
                    nodeActionParamsMapping[structIdx] = paramStructIndices;
                    UnifiedLogger.LogParser(LogLevel.TRACE, $"Reply[{replyIdx}] ActionParams: Mapped {paramStructIndices.Count} param structs to GFF struct[{structIdx}]");
                }

                replyFieldIndex += 10; // Move to next reply's fields (10 fields per reply)
            }

            UnifiedLogger.LogParser(LogLevel.TRACE, $"?? NODE PARAMETER CREATION: Created parameter structs for {nodeActionParamsMapping.Count} nodes, total structs now: {allStructs.Count}");
        }

        private void CreateParameterStructs(List<GffStruct> allStructs, List<GffField> allFields, List<string> allLabels, List<byte> fieldData)
        {
            UnifiedLogger.LogParser(LogLevel.TRACE, $"?? AURORA FIX: Creating parameter STRUCTS to reach 29 total structs (current: {allStructs.Count})");

            // ?? Create 6 additional parameter-related structs to match GFF's 29 total
            // Based on GFF analysis, these are likely condition/parameter evaluation structs

            // Parameter struct set 1: she_gone conditions
            CreateParameterStruct(allStructs, allFields, allLabels, fieldData, "she_gone", "GONE", 5);
            CreateParameterStruct(allStructs, allFields, allLabels, fieldData, "she_gone_eval", "TRUE", 6);

            // Parameter struct set 2: is_another_bb conditions
            CreateParameterStruct(allStructs, allFields, allLabels, fieldData, "is_another_bb", "BABY", 7);
            CreateParameterStruct(allStructs, allFields, allLabels, fieldData, "is_another_bb_eval", "FALSE", 8);

            // Parameter struct set 3: starting_cond conditions
            CreateParameterStruct(allStructs, allFields, allLabels, fieldData, "starting_cond", "FALSE", 9);
            CreateParameterStruct(allStructs, allFields, allLabels, fieldData, "starting_cond_eval", "TRUE", 10);

            UnifiedLogger.LogParser(LogLevel.TRACE, $"?? AURORA COMPATIBILITY: Added 6 parameter structs, now have {allStructs.Count} total structs (target: 29)");
        }

        private void CreateParameterStruct(List<GffStruct> allStructs, List<GffField> allFields, List<string> allLabels, List<byte> fieldData, string key, string value, uint structType)
        {
            // Create parameter struct
            var paramStruct = new GffStruct
            {
                Type = structType,
                DataOrDataOffset = (uint)(allFields.Count * 4), // Point to where parameter fields will be created
                FieldCount = 2 // Key + Value fields
            };
            allStructs.Add(paramStruct);

            // Create parameter fields
            AddParameterKeyValueFields(allFields, allLabels, fieldData, key, value);

            UnifiedLogger.LogParser(LogLevel.TRACE, $"?? Created parameter struct Type={structType} for {key}={value}, struct index {allStructs.Count - 1}");
        }

        private void CreateStartStructsAtEnd(Dialog dialog, List<GffStruct> allStructs, List<int> startStructIndices)
        {
            // ?? AURORA FIX: Create start structs at END to match GFF indices (26,27,28 out of 29 total)
            // This ensures starts get the proper high indices Expected

            UnifiedLogger.LogParser(LogLevel.TRACE, $"?? Creating start structs at end for index compatibility (current count: {allStructs.Count})");

            uint startTypeCounter = 0;

            for (int i = 0; i < dialog.Starts.Count; i++)
            {
                var startStruct = new GffStruct
                {
                    Type = startTypeCounter++, // Start struct types: 0, 1, 2
                    DataOrDataOffset = 0, // Will be fixed when start fields are created
                    FieldCount = 3 // Index, Active, ConditionParams fields
                };

                startStructIndices.Add(allStructs.Count); // Track position BEFORE adding
                allStructs.Add(startStruct);

                UnifiedLogger.LogParser(LogLevel.TRACE, $"?? Created start struct[{i}] at index {allStructs.Count - 1} (type {startStruct.Type})");
            }

            UnifiedLogger.LogParser(LogLevel.TRACE, $"?? Start structs created at indices: [{string.Join(", ", startStructIndices)}] (Expected 26,27,28)");
        }
        
        private void AddParameterKeyValueFields(List<GffField> allFields, List<string> allLabels, List<byte> fieldData, string key, string value)
        {
            int beforeSize = fieldData.Count;

            // Key field (CExoString)
            var keyOffset = (uint)fieldData.Count;
            var keyBytes = System.Text.Encoding.UTF8.GetBytes(key);
            fieldData.AddRange(BitConverter.GetBytes((uint)keyBytes.Length)); // Length prefix
            fieldData.AddRange(keyBytes); // String data
            // ?? PADDING FIX (2025-10-24): Add 4-byte alignment padding like all other CExoString fields
            while (fieldData.Count % 4 != 0) fieldData.Add(0);
            AddLabelAndField(allFields, allLabels, "Key", GffField.CExoString, keyOffset);

            // Value field (CExoString)
            var valueOffset = (uint)fieldData.Count;
            var valueBytes = System.Text.Encoding.UTF8.GetBytes(value);
            fieldData.AddRange(BitConverter.GetBytes((uint)valueBytes.Length)); // Length prefix
            fieldData.AddRange(valueBytes); // String data
            // ?? PADDING FIX (2025-10-24): Add 4-byte alignment padding like all other CExoString fields
            while (fieldData.Count % 4 != 0) fieldData.Add(0);
            AddLabelAndField(allFields, allLabels, "Value", GffField.CExoString, valueOffset);

            int bytesAdded = fieldData.Count - beforeSize;
            UnifiedLogger.LogParser(LogLevel.DEBUG, $"?? AddParameterKeyValueFields: '{key}'='{value}' added {bytesAdded} bytes (before={beforeSize}, after={fieldData.Count})");
        }

        private uint CalculateFieldDataOffset(List<GffStruct> allStructs, List<GffField> allFields, List<string> allLabels)
        {
            // Replicate the offset calculation from WriteBinaryGff
            uint headerSize = 56;
            uint structOffset = headerSize;
            uint structSize = (uint)(allStructs.Count * 12);
            uint fieldOffset = structOffset + structSize;
            uint fieldSize = (uint)(allFields.Count * 12);
            uint labelOffset = fieldOffset + fieldSize;
            uint labelSize = CalculateLabelSize(allLabels);
            uint fieldDataOffset = labelOffset + labelSize;
            return fieldDataOffset;
        }

        // ===== Binary Writing (Extracted to GffBinaryWriter.cs - Dec 2025) =====
        // WriteBinaryGff and related methods moved to GffBinaryWriter class for maintainability.
        // See GffBinaryWriter.cs for: WriteHeader, WriteStructs, WriteFields, WriteLabels,
        // WriteFieldData, WriteFieldIndices, WriteListIndices, and helper methods.

        // ===== Size Calculation Methods =====
        private uint CalculateListDataSize(Dialog dialog, Dictionary<int, List<int>>? pointerConditionParamsMapping = null, Dictionary<int, List<int>>? nodeActionParamsMapping = null)
        {
            // Calculate the actual size of list data (count + indices) written to buffer
            uint totalDataSize = 0;

            // EntryList: count + entry indices
            uint entryCount = (uint)dialog.Entries.Count;
            totalDataSize += 4 + (entryCount * 4); // 4 bytes for count + 4 bytes per entry

            // ReplyList: count + reply indices
            uint replyCount = (uint)dialog.Replies.Count;
            totalDataSize += 4 + (replyCount * 4); // 4 bytes for count + 4 bytes per reply

            // StartingList: count + start indices
            uint startCount = (uint)dialog.Starts.Count;
            totalDataSize += 4 + (startCount * 4); // 4 bytes for count + 4 bytes per start

            // Individual pointer lists for each entry/reply - ALWAYS write count field (GFF GFF requirement)
            // ?? CRITICAL FIX: Use deduplication logic matching WriteListIndices
            foreach (var entry in dialog.Entries)
            {
                if (entry.Pointers.Count > 0)
                {
                    // Apply same deduplication as WriteListIndices
                    var uniquePointers = entry.Pointers.GroupBy(p => p.Index).Select(g => g.First()).ToList();
                    totalDataSize += 4 + ((uint)uniquePointers.Count * 4); // count + deduplicated indices
                }
                else
                {
                    totalDataSize += 4; // Just count field for empty list
                }
            }

            foreach (var reply in dialog.Replies)
            {
                // Note: Reply pointers don't use deduplication in WriteListIndices
                totalDataSize += 4 + ((uint)reply.Pointers.Count * 4); // count + indices
            }

            // ?? PARAMETER FIX (2025-10-24): Calculate ACTUAL ActionParams list sizes using nodeActionParamsMapping
            // If mapping provided, use actual parameter counts; otherwise assume empty (backward compat)
            if (nodeActionParamsMapping != null)
            {
                uint actionParamsBytes = 0;
                // The mapping uses GFF struct indices as keys
                // We need to count list data for ALL nodes (with or without params)
                // Nodes WITH params: count + indices (4 + count*4 bytes)
                // Nodes WITHOUT params: just count=0 (4 bytes)

                // Count entries with params
                for (int i = 0; i < dialog.Entries.Count; i++)
                {
                    // Check if this entry has params in mapping (mapping key is GFF struct index, not node index)
                    // But we don't have entryStructIndices here, so we'll count based on Dialog object
                    if (dialog.Entries[i].ActionParams != null && dialog.Entries[i].ActionParams.Count > 0)
                    {
                        actionParamsBytes += 4 + ((uint)dialog.Entries[i].ActionParams.Count * 4); // count + indices
                    }
                    else
                    {
                        actionParamsBytes += 4; // Empty list (count=0)
                    }
                }

                // Count replies with params
                for (int i = 0; i < dialog.Replies.Count; i++)
                {
                    if (dialog.Replies[i].ActionParams != null && dialog.Replies[i].ActionParams.Count > 0)
                    {
                        actionParamsBytes += 4 + ((uint)dialog.Replies[i].ActionParams.Count * 4); // count + indices
                    }
                    else
                    {
                        actionParamsBytes += 4; // Empty list (count=0)
                    }
                }

                totalDataSize += actionParamsBytes;
                UnifiedLogger.LogParser(LogLevel.TRACE, $"?? ActionParams lists: {actionParamsBytes} bytes (with actual parameters)");
            }
            else
            {
                // Fallback: assume all empty
                uint actionParamsLists = entryCount + replyCount;
                totalDataSize += actionParamsLists * 4;
                UnifiedLogger.LogParser(LogLevel.TRACE, $"?? ActionParams lists: {actionParamsLists} nodes � 4 bytes = {actionParamsLists * 4} bytes");
            }

            // ?? PARAMETER FIX (2025-10-24): Calculate ACTUAL ConditionParams list sizes
            if (pointerConditionParamsMapping != null)
            {
                uint conditionParamsBytes = 0;

                // Count ConditionParams for all pointers in entries
                foreach (var entry in dialog.Entries)
                {
                    foreach (var ptr in entry.Pointers)
                    {
                        if (ptr.ConditionParams != null && ptr.ConditionParams.Count > 0)
                        {
                            conditionParamsBytes += 4 + ((uint)ptr.ConditionParams.Count * 4); // count + indices
                        }
                        else
                        {
                            conditionParamsBytes += 4; // Empty list (count=0)
                        }
                    }
                }

                // Count ConditionParams for all pointers in replies
                foreach (var reply in dialog.Replies)
                {
                    foreach (var ptr in reply.Pointers)
                    {
                        if (ptr.ConditionParams != null && ptr.ConditionParams.Count > 0)
                        {
                            conditionParamsBytes += 4 + ((uint)ptr.ConditionParams.Count * 4); // count + indices
                        }
                        else
                        {
                            conditionParamsBytes += 4; // Empty list (count=0)
                        }
                    }
                }

                // Count ConditionParams for all starts
                foreach (var start in dialog.Starts)
                {
                    if (start.ConditionParams != null && start.ConditionParams.Count > 0)
                    {
                        conditionParamsBytes += 4 + ((uint)start.ConditionParams.Count * 4); // count + indices
                    }
                    else
                    {
                        conditionParamsBytes += 4; // Empty list (count=0)
                    }
                }

                totalDataSize += conditionParamsBytes;
                UnifiedLogger.LogParser(LogLevel.TRACE, $"?? ConditionParams lists: {conditionParamsBytes} bytes (with actual parameters)");
            }
            else
            {
                // Fallback: just start wrappers with empty ConditionParams
                totalDataSize += startCount * 4;
                UnifiedLogger.LogParser(LogLevel.TRACE, $"?? ConditionParams lists (Starts): {startCount} � 4 bytes = {startCount * 4} bytes");
            }

            return totalDataSize;
        }

        // ===== Removed Methods (Extracted to GffBinaryWriter.cs - Dec 2025) =====
        // The following methods were moved to GffBinaryWriter class:
        // - CalculateActualFieldIndicesCount
        // - WriteFieldIndices, WriteAuroraRootStructIndices, WriteRemainingStructIndices
        // - FindFieldIndex
        // - WriteListIndices (and all sub-methods)
        // - CountEntryStructs, CountReplyStructs, CountStartStructs

        public byte[] CreateDlgBuffer(Dialog dialog)
        {
            UnifiedLogger.LogParser(LogLevel.TRACE, "Creating complete DLG buffer");
            UnifiedLogger.LogParser(LogLevel.TRACE, "ENTERING CreateDlgBuffer method");
            
            try
            {
                // Use the complete manual GFF writer that includes all dialog content and proper structure
                var buffer = CreateFullDlgBufferManual(dialog);
                
                UnifiedLogger.LogParser(LogLevel.TRACE, $"Full manual DLG buffer created successfully, size: {buffer.Length} bytes");
                return buffer;
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogParser(LogLevel.ERROR, $"Failed to create DLG buffer: {ex.Message}");
                throw;
            }
        }
        
        public byte[] CreateFullDlgBufferManual(Dialog dialog)
        {
            UnifiedLogger.LogParser(LogLevel.TRACE, "?? GFF WRITER: Building DLG buffer with reverse-engineered patterns");
            UnifiedLogger.LogParser(LogLevel.TRACE, "ENTERING CreateFullDlgBufferManual method");
            
            // Convert dialog to GFF structures and use GFF writer
            UnifiedLogger.LogParser(LogLevel.TRACE, "?? ABOUT TO CALL ConvertDialogToGff");
            (List<GffStruct> allStructs, List<GffField> allFields, List<string> allLabels, List<byte> fieldData, List<int> entryStructIndices, List<int> replyStructIndices, List<int> startStructIndices, List<List<int>> entryPointerStructIndices, List<List<int>> replyPointerStructIndices, Dictionary<int, List<int>> pointerConditionParamsMapping, Dictionary<int, List<int>> nodeActionParamsMapping) result;
            try
            {
                result = ConvertDialogToGff(dialog);
                UnifiedLogger.LogParser(LogLevel.TRACE, "?? ConvertDialogToGff COMPLETED SUCCESSFULLY");
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogParser(LogLevel.ERROR, $"?? ConvertDialogToGff FAILED: {ex.Message}");
                UnifiedLogger.LogParser(LogLevel.ERROR, $"?? Exception Type: {ex.GetType().Name}");
                UnifiedLogger.LogParser(LogLevel.ERROR, $"?? Stack trace: {ex.StackTrace}");
                throw; // Re-throw to see the full error chain
            }
            var (allStructs, allFields, allLabels, fieldData, entryStructIndices, replyStructIndices, startStructIndices, entryPointerStructIndices, replyPointerStructIndices, pointerConditionParamsMapping, nodeActionParamsMapping) = result;
            
            UnifiedLogger.LogParser(LogLevel.TRACE, $"Generated GFF components: {allStructs.Count} structs, {allFields.Count} fields, {allLabels.Count} labels, {fieldData.Count} bytes field data");

            // Use extracted GFF binary writer
            return _binaryWriter.Write(
                allStructs, allFields, allLabels, fieldData,
                "DLG ", "V3.28",
                dialog, entryStructIndices, replyStructIndices, startStructIndices,
                entryPointerStructIndices, replyPointerStructIndices,
                pointerConditionParamsMapping, nodeActionParamsMapping,
                CalculateListDataSize);
        }
        
        public (List<GffStruct>, List<GffField>, List<string>, List<byte>, List<int>, List<int>, List<int>, List<List<int>>, List<List<int>>, Dictionary<int, List<int>>, Dictionary<int, List<int>>) ConvertDialogToGff(Dialog dialog)
        {
            UnifiedLogger.LogParser(LogLevel.TRACE, "?? ENTERING ConvertDialogToGff - This should show up in logs!");

            // Initialize FieldIndexTracker for robust field index management in complex files
            var mockLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
            var fieldTracker = new DialogEditor.Utils.FieldIndexTracker(0, mockLogger);
            UnifiedLogger.LogParser(LogLevel.TRACE, "?? FIELD INDEX TRACKER: Initialized for complex file field management");

            // Pre-calculate field counts needed for both struct creation and field creation
            uint totalEntryFields = 0;
            foreach (var entry in dialog.Entries)
            {
                totalEntryFields += CalculateEntryFieldCount(entry);
            }
            uint entryPointerFieldsCount = 0;
            foreach (var entry in dialog.Entries)
            {
                entryPointerFieldsCount += (uint)(entry.Pointers.Count * 4); // 4 fields per pointer
            }

            // ?? CORRUPTION PREVENTION: Clean dialog.Starts FIRST before any struct creation
            uint entryCount = (uint)dialog.Entries.Count;
            uint replyCount = (uint)dialog.Replies.Count;
            
            var originalStarts = new List<DialogPtr>();
            var seenIndices = new HashSet<uint>();
            
            foreach (var start in dialog.Starts)
            {
                // Only keep entries that are valid AND haven't been seen before (prevent duplicates)
                if (start.Index != uint.MaxValue && start.Index < entryCount && !seenIndices.Contains(start.Index))
                {
                    originalStarts.Add(start);
                    seenIndices.Add(start.Index);
                    UnifiedLogger.LogParser(LogLevel.TRACE, $"?? ORIGINAL START: Preserving unique start with Index={start.Index}");
                }
                else if (start.Index == uint.MaxValue)
                {
                    UnifiedLogger.LogParser(LogLevel.ERROR, $"?? CORRUPTION CLEANUP: Removing invalid start with Index={start.Index} (MaxValue)");
                }
                else if (start.Index >= entryCount)
                {
                    UnifiedLogger.LogParser(LogLevel.ERROR, $"?? CORRUPTION CLEANUP: Removing out-of-range start with Index={start.Index} (>= {entryCount})");
                }
                else if (seenIndices.Contains(start.Index))
                {
                    UnifiedLogger.LogParser(LogLevel.ERROR, $"?? DUPLICATE CLEANUP: Removing duplicate start with Index={start.Index}");
                }
            }
            
            // Replace dialog.Starts with cleaned, deduplicated list BEFORE struct creation
            dialog.Starts.Clear();
            foreach (var originalStart in originalStarts)
            {
                dialog.Starts.Add(originalStart);
            }
            
            uint startCount = (uint)dialog.Starts.Count;
            UnifiedLogger.LogParser(LogLevel.TRACE, $"?? EXPORT READY: Cleaned starts collection - now has {startCount} valid entries BEFORE struct creation");
            
            var allStructs = new List<GffStruct>();
            var allFields = new List<GffField>();
            var allLabels = new List<string>();
            var fieldData = new List<byte>();

            // ?? PRE-CALCULATE: All ListIndices offsets BEFORE creating fields
            // This ensures fields are created with correct offsets from the start (no patching needed)
            var offsetMap = CalculateListIndicesOffsets(dialog);

            // Build GFF structures
            var (entryStructIndices, replyStructIndices, startStructIndices, entryPointerStructIndices, replyPointerStructIndices, pointerConditionParamsMapping, nodeActionParamsMapping) = CreateAuroraCompatibleGffStructures(dialog, allStructs, allFields, allLabels, fieldData, fieldTracker, offsetMap);

            // Generate field allocation audit trail for debugging complex files
            fieldTracker.LogAllocationSummary();
            var conflicts = fieldTracker.DetectConflicts();
            if (conflicts.Count > 0)
            {
                UnifiedLogger.LogParser(LogLevel.WARN, $"?? FIELD ALLOCATION CONFLICTS DETECTED: {conflicts.Count} conflicts");
                foreach (var conflict in conflicts)
                {
                    UnifiedLogger.LogParser(LogLevel.WARN, $"   {conflict}");
                }
            }
            else
            {
                UnifiedLogger.LogParser(LogLevel.TRACE, $"? FIELD ALLOCATION: No conflicts detected - all field ranges properly allocated");
            }

            return (allStructs, allFields, allLabels, fieldData, entryStructIndices, replyStructIndices, startStructIndices, entryPointerStructIndices, replyPointerStructIndices, pointerConditionParamsMapping, nodeActionParamsMapping);
        }

        private (List<int>, List<int>, List<int>, List<List<int>>, List<List<int>>, Dictionary<int, List<int>>, Dictionary<int, List<int>>) CreateAuroraCompatibleGffStructures(Dialog dialog, List<GffStruct> allStructs, List<GffField> allFields, List<string> allLabels, List<byte> fieldData, DialogEditor.Utils.FieldIndexTracker fieldTracker, ListIndicesOffsetMap offsetMap)
        {
            UnifiedLogger.LogParser(LogLevel.DEBUG, "Creating GFF structures with pre-calculated offsets");
            
            // 2025-10-21: INTERLEAVED STRUCT CREATION (conversation-flow order)
            // Initialize traversal state
            var state = new InterleavedTraversalState
            {
                CurrentFieldIndex = 9 // Start after root fields (0-8)
            };

            // 1. Create root struct (conversation metadata)
            var rootStruct = new GffStruct
            {
                // 2025-10-21: Preserve original root type (always 0xFFFFFFFF per GFF spec)
                Type = dialog.OriginalRootGffStruct?.Type ?? 0xFFFFFFFF,
                DataOrDataOffset = 0, // ?? Root struct starts at field index 0
                FieldCount = 9 // ?? AURORA FIX: All 9 BioWare-required fields per spec
            };
            allStructs.Add(rootStruct);

            // 2. Create structs in interleaved order (depth-first traversal)
            CreateInterleavedStructs(dialog, allStructs, state);

            // Extract tracking indices from state
            var entryStructIndices = state.EntryStructIndices;
            var replyStructIndices = state.ReplyStructIndices;
            var startStructIndices = state.StartStructIndices;
            var entryPointerStructIndices = state.EntryPointerStructIndices;
            var replyPointerStructIndices = state.ReplyPointerStructIndices;

            UnifiedLogger.LogParser(LogLevel.TRACE, $"?? Interleaved struct stats: {allStructs.Count} structs, {entryStructIndices.Count} entries, {replyStructIndices.Count} replies, {startStructIndices.Count} starts");

            // ?? CRITICAL (2025-10-22): Fields MUST be created in SAME ORDER as structs (Entry-First batched)
            // Expected field indices to match struct DataOrDataOffset values exactly

            // 1. Root fields FIRST (fields 0-8) - use pre-calculated offsets
            CreateRootFields(dialog, allFields, allLabels, fieldData, offsetMap);

            // 2. Entry-First batched field creation (matches struct order)
            UnifiedLogger.LogParser(LogLevel.TRACE, "?? Creating Entry fields (batched with pointers)");
            uint entryPointerFieldStartIndex = (uint)allFields.Count; // Track for param mapping

            int globalPointerIndex = 0; // Track global pointer index across all nodes

            for (int entryIdx = 0; entryIdx < dialog.Entries.Count; entryIdx++)
            {
                var entry = dialog.Entries[entryIdx];

                // Create entry's content fields
                CreateSingleEntryFields(entry, allFields, allLabels, fieldData, offsetMap, entryIdx);

                // Immediately create this entry's pointer fields (pointers-first within node)
                foreach (var pointer in entry.Pointers)
                {
                    CreatePointerFields(pointer, allFields, allLabels, fieldData, dialog, offsetMap, globalPointerIndex);
                    globalPointerIndex++; // Increment for each pointer
                }
            }

            // 3. Reply-batched field creation (matches struct order)
            UnifiedLogger.LogParser(LogLevel.TRACE, "?? Creating Reply fields (batched with pointers)");
            uint replyPointerFieldStartIndex = (uint)allFields.Count; // Track for param mapping

            for (int replyIdx = 0; replyIdx < dialog.Replies.Count; replyIdx++)
            {
                var reply = dialog.Replies[replyIdx];

                // Create reply's content fields
                CreateSingleReplyFields(reply, allFields, allLabels, fieldData, offsetMap, replyIdx);

                // Immediately create this reply's pointer fields (pointers-first within node)
                foreach (var pointer in reply.Pointers)
                {
                    CreatePointerFields(pointer, allFields, allLabels, fieldData, dialog, offsetMap, globalPointerIndex);
                    globalPointerIndex++; // Increment for each pointer
                }
            }

            // 4. Start fields at END (matches struct order)
            uint startWrapperFieldStartIndex = (uint)allFields.Count;
            CreateStartingFields(dialog, allFields, allLabels, fieldData, offsetMap);

            // Create parameter structs for each pointer's ConditionParams and track mappings
            int fieldDataBeforeCondParams = fieldData.Count;
            var pointerConditionParamsMapping = new Dictionary<int, List<int>>();
            CreatePointerParameterStructs(dialog, allStructs, allFields, allLabels, fieldData, entryPointerFieldStartIndex, replyPointerFieldStartIndex, startWrapperFieldStartIndex, pointerConditionParamsMapping);
            UnifiedLogger.LogParser(LogLevel.TRACE, $"?? ConditionParams added {fieldData.Count - fieldDataBeforeCondParams} bytes to fieldData (before={fieldDataBeforeCondParams}, after={fieldData.Count})");

            // Create parameter structs for each node's ActionParams and track mappings
            int fieldDataBeforeActionParams = fieldData.Count;
            var nodeActionParamsMapping = new Dictionary<int, List<int>>();
            // NOTE: Simplified - we'll use field index 9 for entries, current count for replies
            uint entryFieldStartIndex = 9; // Entries start at field 9 (after root fields 0-8)
            uint replyFieldStartIndex = entryPointerFieldStartIndex; // Replies start after all entry/pointer fields
            CreateNodeActionParameterStructs(dialog, allStructs, allFields, allLabels, fieldData, entryFieldStartIndex, replyFieldStartIndex, entryStructIndices, replyStructIndices, nodeActionParamsMapping);
            UnifiedLogger.LogParser(LogLevel.TRACE, $"?? ActionParams added {fieldData.Count - fieldDataBeforeActionParams} bytes to fieldData (before={fieldDataBeforeActionParams}, after={fieldData.Count})");

            // ?? STRUCTURAL FIX: Keep parameter structs disabled to avoid duplicates with manual creation above
            // CreateParameterStructs(allStructs, allFields, allLabels, fieldData);

            // ?? STRUCTURAL FIX: Disable start struct creation at end - causes structure mismatch
            // CreateStartStructsAtEnd(dialog, allStructs, startStructIndices);

            // 12. Reply pointer fields now created as Bioware-spec Sync Structs (above)

            // 13. Reply pointer struct DataOrDataOffset values are already set correctly during struct creation

            UnifiedLogger.LogParser(LogLevel.DEBUG, $"Created GFF structure: {allStructs.Count} structs, {allFields.Count} fields, {allLabels.Count} labels, {fieldData.Count} bytes field data");

            return (entryStructIndices, replyStructIndices, startStructIndices, entryPointerStructIndices, replyPointerStructIndices, pointerConditionParamsMapping, nodeActionParamsMapping); // Return tracked struct positions and param mappings
        }

    }
}
