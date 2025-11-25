using System;
using System.Collections.Generic;
using System.Linq;
using DialogEditor.Models;
using DialogEditor.Services;

namespace DialogEditor.Parsers
{
    /// <summary>
    /// Builds Dialog domain models from GFF structures.
    /// Extracted from DialogParser.cs to improve maintainability.
    /// Phase 2 of parser refactoring - Oct 28, 2025
    /// </summary>
    // Phase 4 Refactoring: Made internal - use DialogFileService for public API
    internal class DialogBuilder
    {
        public Dialog BuildDialogFromGffStruct(GffStruct rootStruct)
        {
            var dialog = new Dialog();

            try
            {
                // 2025-10-21: Preserve root GFF struct for round-trip type preservation
                dialog.OriginalRootGffStruct = rootStruct;

                // Parse dialog-level properties
                dialog.DelayEntry = rootStruct.GetFieldValue<uint>("DelayEntry", 0);
                dialog.DelayReply = rootStruct.GetFieldValue<uint>("DelayReply", 0);
                dialog.NumWords = rootStruct.GetFieldValue<uint>("NumWords", 0); // Bioware spec: word count
                dialog.ScriptAbort = rootStruct.GetFieldValue<string>("EndConverAbort", string.Empty);
                dialog.ScriptEnd = rootStruct.GetFieldValue<string>("EndConversation", string.Empty);
                dialog.PreventZoom = rootStruct.GetFieldValue<byte>("PreventZoomIn", 0) != 0;

                // Parse entry list
                var entriesField = rootStruct.GetField("EntryList");
                if (entriesField?.Value is GffList entriesList)
                {
                    foreach (var entryStruct in entriesList.Elements)
                    {
                        var dialogNode = BuildDialogNodeFromStruct(entryStruct, DialogNodeType.Entry, dialog);
                        if (dialogNode != null)
                        {
                            dialogNode.Parent = dialog;
                            dialog.Entries.Add(dialogNode);
                        }
                    }
                }
                
                // Parse reply list
                var repliesField = rootStruct.GetField("ReplyList");
                UnifiedLogger.LogParser(LogLevel.TRACE, $"🔍 DEBUG: Looking for ReplyList field... {(repliesField != null ? "FOUND" : "NOT FOUND")}");
                if (repliesField?.Value is GffList repliesList)
                {
                    UnifiedLogger.LogParser(LogLevel.TRACE, $"ReplyList contains {repliesList.Elements.Count} structs");
                    int successfulReplies = 0;
                    for (int i = 0; i < repliesList.Elements.Count; i++)
                    {
                        var replyStruct = repliesList.Elements[i];
                        UnifiedLogger.LogParser(LogLevel.TRACE, $"ReplyList[{i}]: Struct Type={replyStruct.Type}, Fields={replyStruct.Fields.Count}");
                        var dialogNode = BuildDialogNodeFromStruct(replyStruct, DialogNodeType.Reply, dialog);
                        if (dialogNode != null)
                        {
                            dialogNode.Parent = dialog;
                            dialog.Replies.Add(dialogNode);
                            successfulReplies++;
                        }
                        else
                        {
                            UnifiedLogger.LogParser(LogLevel.WARN, $"❌ Failed to create dialog node for ReplyList[{i}] (returned null)");
                        }
                    }
                    UnifiedLogger.LogParser(LogLevel.TRACE, $"🔍 REPLY PARSING SUMMARY: {successfulReplies}/{repliesList.Elements.Count} replies successfully created");
                }
                else
                {
                    UnifiedLogger.LogParser(LogLevel.WARN, $"❌ ReplyList field not found or not a GffList. Field exists: {repliesField != null}, Type: {repliesField?.Value?.GetType().Name ?? "NULL"}");
                }
                
                // Parse starting links
                var startingListField = rootStruct.GetField("StartingList");
                if (startingListField?.Value is GffList startingList)
                {
                    UnifiedLogger.LogParser(LogLevel.TRACE, $"🔍 STARTINGLIST FIELD: DataOrDataOffset={startingListField.DataOrDataOffset} (0x{startingListField.DataOrDataOffset:X8})");
                    UnifiedLogger.LogParser(LogLevel.TRACE, $"🔍 START LIST: Found {startingList.Elements.Count} start structs");
                    for (int i = 0; i < startingList.Elements.Count; i++)
                    {
                        var startStruct = startingList.Elements[i];
                        UnifiedLogger.LogParser(LogLevel.TRACE, $"🔍 START LIST: Processing start struct {i}, Type={startStruct.Type}, Fields={startStruct.Fields.Count}");

                        var startPtr = BuildDialogPtrFromStruct(startStruct, dialog, DialogNodeType.Entry);
                        if (startPtr != null)
                        {
                            UnifiedLogger.LogParser(LogLevel.TRACE, $"🔍 START LIST: Built start pointer with Index={startPtr.Index}");

                            // Note: Original file already has correct Start[0]: Index=0 for lista.dlg - no correction needed

                            startPtr.IsStart = true;
                            dialog.Starts.Add(startPtr);
                        }
                        else
                        {
                            UnifiedLogger.LogParser(LogLevel.ERROR, $"🔍 START LIST: Failed to build start pointer from struct {i}");
                        }
                    }
                }
                else
                {
                    UnifiedLogger.LogParser(LogLevel.WARN, $"🔍 STARTINGLIST FIELD: Not found or invalid in root struct");
                    UnifiedLogger.LogParser(LogLevel.WARN, $"🔍 Available root fields: {string.Join(", ", rootStruct.Fields.Select(f => f.Label))}");
                    UnifiedLogger.LogParser(LogLevel.WARN, $"🔍 This dialog file lacks starting conversation entries");

                    // Fallback: Create default starting entry pointing to Entry[0] if entries exist
                    if (dialog.Entries.Count > 0)
                    {
                        UnifiedLogger.LogParser(LogLevel.TRACE, $"🔧 FALLBACK: Creating default start pointer to Entry[0]");
                        var fallbackStart = new DialogPtr
                        {
                            Parent = dialog,
                            Type = DialogNodeType.Entry,
                            Index = 0,
                            Node = dialog.Entries[0],
                            IsStart = true,
                            ScriptAppears = "",
                            ConditionParams = new Dictionary<string, string>()
                        };

                        dialog.Starts.Add(fallbackStart);
                        UnifiedLogger.LogParser(LogLevel.TRACE, $"✅ FALLBACK: Added default start → Entry[0]: \"{dialog.Entries[0].DisplayText}\"");
                    }
                    else
                    {
                        UnifiedLogger.LogParser(LogLevel.ERROR, $"❌ FALLBACK: No entries available to create default start");
                    }
                }
                
                // CRITICAL: Resolve all pointer indices to actual dialog nodes
                ResolveDialogPointers(dialog);

                UnifiedLogger.LogParser(LogLevel.TRACE,
                    $"Built dialog: {dialog.Entries.Count} entries, {dialog.Replies.Count} replies, {dialog.Starts.Count} starts");


                return dialog;
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogParser(LogLevel.ERROR, $"Failed to build dialog from GFF struct: {ex.Message}");
                throw;
            }
        }

        public void ResolveDialogPointers(Dialog dialog)
        {
            UnifiedLogger.LogParser(LogLevel.TRACE, "🔗 POINTER RESOLUTION: Starting pointer-to-node linking");

            int resolvedCount = 0;
            int failedCount = 0;

            // Resolve entry pointers (point to replies)
            foreach (var entry in dialog.Entries)
            {
                foreach (var pointer in entry.Pointers)
                {
                    if (pointer.Index != uint.MaxValue && pointer.Index < dialog.Replies.Count)
                    {
                        pointer.Node = dialog.Replies[(int)pointer.Index];
                        pointer.Type = DialogNodeType.Reply;
                        resolvedCount++;
                        UnifiedLogger.LogParser(LogLevel.DEBUG, $"🔗 Resolved Entry pointer Index={pointer.Index} → Reply '{pointer.Node.Text?.GetDefault() ?? "empty"}'");
                    }
                    else
                    {
                        failedCount++;
                        UnifiedLogger.LogParser(LogLevel.WARN, $"🔗 Failed to resolve Entry pointer: Index={pointer.Index}, Max={dialog.Replies.Count - 1}");
                    }
                }
            }

            // Resolve reply pointers (point to entries)
            foreach (var reply in dialog.Replies)
            {
                foreach (var pointer in reply.Pointers)
                {
                    if (pointer.Index != uint.MaxValue && pointer.Index < dialog.Entries.Count)
                    {
                        pointer.Node = dialog.Entries[(int)pointer.Index];
                        pointer.Type = DialogNodeType.Entry;
                        resolvedCount++;
                        UnifiedLogger.LogParser(LogLevel.DEBUG, $"🔗 Resolved Reply pointer Index={pointer.Index} → Entry '{pointer.Node.Text?.GetDefault() ?? "empty"}'");
                    }
                    else
                    {
                        failedCount++;
                        UnifiedLogger.LogParser(LogLevel.WARN, $"🔗 Failed to resolve Reply pointer: Index={pointer.Index}, Max={dialog.Entries.Count - 1}");
                    }
                }
            }

            // Resolve start pointers (point to entries)
            foreach (var start in dialog.Starts)
            {
                if (start.Index != uint.MaxValue && start.Index < dialog.Entries.Count)
                {
                    start.Node = dialog.Entries[(int)start.Index];
                    start.Type = DialogNodeType.Entry;
                    resolvedCount++;
                    UnifiedLogger.LogParser(LogLevel.DEBUG, $"🔗 Resolved Start pointer Index={start.Index} → Entry '{start.Node.Text?.GetDefault() ?? "empty"}'");
                }
                else
                {
                    failedCount++;
                    UnifiedLogger.LogParser(LogLevel.WARN, $"🔗 Failed to resolve Start pointer: Index={start.Index}, Max={dialog.Entries.Count - 1}");
                }
            }

            UnifiedLogger.LogParser(LogLevel.TRACE, $"🔗 POINTER RESOLUTION: {resolvedCount} resolved, {failedCount} failed");

            // Removed parser workaround code 2025-09-29 - was corrupting valid GFF files by modifying pointer targets
        }

        public DialogNode? BuildDialogNodeFromStruct(GffStruct nodeStruct, DialogNodeType nodeType, Dialog? currentDialog = null)
        {
            try
            {
                var node = new DialogNode
                {
                    Type = nodeType,
                    // 2025-10-21: Preserve original GFF struct for round-trip type preservation
                    OriginalGffStruct = nodeStruct
                };
                
                // Debug: Log all available fields in this struct
                UnifiedLogger.LogParser(LogLevel.TRACE, $"BuildDialogNodeFromStruct [{nodeType}] (Struct Type={nodeStruct.Type}): Available fields: {string.Join(", ", nodeStruct.Fields.Select(f => f.Label))}");
                if (nodeStruct.Fields.Count == 0)
                {
                    UnifiedLogger.LogParser(LogLevel.WARN, $"BuildDialogNodeFromStruct [{nodeType}] (Struct Type={nodeStruct.Type}): Struct has NO FIELDS!");
                }
                
                // Parse basic properties
                node.Comment = nodeStruct.GetFieldValue<string>("Comment", string.Empty);
                node.Speaker = nodeStruct.GetFieldValue<string>("Speaker", string.Empty);
                node.Quest = nodeStruct.GetFieldValue<string>("Quest", string.Empty);
                node.QuestEntry = nodeStruct.GetFieldValue<uint>("QuestEntry", uint.MaxValue);
                node.ScriptAction = nodeStruct.GetFieldValue<string>("Script", string.Empty);
                node.Sound = nodeStruct.GetFieldValue<string>("Sound", string.Empty);
                node.Delay = nodeStruct.GetFieldValue<uint>("Delay", uint.MaxValue);
                
                // Debug: Log script action if present
                if (!string.IsNullOrEmpty(node.ScriptAction))
                {
                    UnifiedLogger.LogParser(LogLevel.DEBUG, $"Found {nodeType} script action: '{node.ScriptAction}'");
                }
                // Read AnimationLoop
                var animLoopField = nodeStruct.GetField("AnimLoop");
                var animLoopValue = nodeStruct.GetFieldValue<byte>("AnimLoop", 0);
                node.AnimationLoop = animLoopValue != 0;
                if (animLoopField != null)
                {
                    UnifiedLogger.LogParser(LogLevel.TRACE, $"Read {nodeType} AnimLoop: field.Value={animLoopField.Value}, field.Type={animLoopField.Type}, field.DataOrDataOffset={animLoopField.DataOrDataOffset}, converted={animLoopValue}");
                }
                else
                {
                    UnifiedLogger.LogParser(LogLevel.WARN, $"Read {nodeType} AnimLoop: field not found!");
                }

                // Check for conditional script on the node itself (starting conditional script)
                var nodeConditionalScript = nodeStruct.GetFieldValue<string>("Active", string.Empty);
                if (!string.IsNullOrEmpty(nodeConditionalScript))
                {
                    UnifiedLogger.LogParser(LogLevel.DEBUG, $"Found node-level conditional script: '{nodeConditionalScript}' for {nodeType}");
                    // Store this in a way that can be displayed in the GUI
                    // For now, we'll add it as a comment prefix to make it visible
                    if (!string.IsNullOrEmpty(node.Comment))
                        node.Comment = $"[Script:{nodeConditionalScript}] {node.Comment}";
                    else
                        node.Comment = $"[Script:{nodeConditionalScript}]";
                }
                
                // Check for conditional parameters on the node itself
                var conditionParams = nodeStruct.GetField("ConditionParams");
                if (conditionParams?.Value is Dictionary<string, object> nodeConditions)
                {
                    UnifiedLogger.LogParser(LogLevel.DEBUG, $"Found node-level condition parameters for {nodeType}: {nodeConditions.Count} params");
                    foreach (var param in nodeConditions)
                    {
                        UnifiedLogger.LogParser(LogLevel.DEBUG, $"Node condition parameter: {param.Key} = {param.Value}");
                        // For now, add to comment to make visible
                        var paramText = $"{param.Key}={param.Value}";
                        if (!string.IsNullOrEmpty(node.Comment))
                            node.Comment += $" [{paramText}]";
                        else
                            node.Comment = $"[{paramText}]";
                    }
                }
                
                // Parse animation
                var animField = nodeStruct.GetField("Animation");
                var animValue = nodeStruct.GetFieldValue<uint>("Animation", 0);
                if (animField != null)
                {
                    UnifiedLogger.LogParser(LogLevel.TRACE, $"Read {nodeType} Animation: field.Type={animField.Type}, field.DataOrDataOffset={animField.DataOrDataOffset}, field.Value={animField.Value}, GetFieldValue={animValue}");
                }
                else
                {
                    UnifiedLogger.LogParser(LogLevel.WARN, $"Read {nodeType} Animation: field not found!");
                }

                if (Enum.IsDefined(typeof(DialogAnimation), animValue))
                {
                    node.Animation = (DialogAnimation)animValue;
                    UnifiedLogger.LogParser(LogLevel.TRACE, $"Read {nodeType} Animation: converted to {node.Animation}");
                }
                else
                {
                    UnifiedLogger.LogParser(LogLevel.WARN, $"Read {nodeType} Animation: value {animValue} not a valid DialogAnimation enum, using default");
                    node.Animation = DialogAnimation.Default;
                }
                
                // Parse localized text
                var textField = nodeStruct.GetField("Text");
                UnifiedLogger.LogParser(LogLevel.TRACE, $"BuildDialogNodeFromStruct [{nodeType}]: textField={textField?.Label ?? "NULL"}, Value={textField?.Value?.GetType().Name ?? "NULL"}");
                if (textField?.Value is CExoLocString locString)
                {
                    UnifiedLogger.LogParser(LogLevel.TRACE, $"Converting CExoLocString with {locString.LocalizedStrings?.Count ?? 0} strings, StrRef={locString.StrRef}");
                    if (locString.LocalizedStrings != null && locString.LocalizedStrings.Count > 0)
                    {
                        // Convert CExoLocString to our LocString format
                        foreach (var kvp in locString.LocalizedStrings)
                        {
                            UnifiedLogger.LogParser(LogLevel.TRACE, $"Adding to node.Text: Lang {kvp.Key} = '{kvp.Value}'");
                            node.Text.Add((int)kvp.Key, kvp.Value);
                        }
                        UnifiedLogger.LogParser(LogLevel.TRACE, $"After conversion, node.Text.GetDefault() = '{node.Text.GetDefault()}'");
                    }
                    else if (locString.StrRef != 0xFFFFFFFF)
                    {
                        // Text is in TLK file - not yet supported
                        UnifiedLogger.LogParser(LogLevel.WARN, $"⚠️ TLK REFERENCE: StrRef={locString.StrRef} - TLK file support not implemented");
                        node.Text.Add(0, $"<StrRef:{locString.StrRef}>");
                    }
                    else
                    {
                        // Empty text is valid - this is a "[CONTINUE]" node in NWN dialogs
                        UnifiedLogger.LogParser(LogLevel.DEBUG, $"CExoLocString has no LocalizedStrings and no StrRef (empty/continue node)");
                    }
                }
                else
                {
                    UnifiedLogger.LogParser(LogLevel.DEBUG, $"Text field is not CExoLocString: {textField?.Value?.GetType().Name ?? "null"}");
                }

                // Parse ActionParams if present
                var actionParamsField = nodeStruct.GetField("ActionParams");
                if (actionParamsField?.Value is GffList actionParamsList)
                {
                    foreach (var paramStruct in actionParamsList.Elements)
                    {
                        var key = paramStruct.GetFieldValue<string>("Key", string.Empty);
                        var value = paramStruct.GetFieldValue<string>("Value", string.Empty);
                        if (!string.IsNullOrEmpty(key))
                        {
                            node.ActionParams[key] = value;
                            UnifiedLogger.LogParser(LogLevel.DEBUG, $"Added action parameter: {key} = {value}");
                        }
                    }
                }
                
                // Parse connections based on node type per Bioware spec
                // Entries have RepliesList -> Player replies
                // NOTE: Reply EntriesList processing is handled separately below to avoid duplicates
                if (nodeType == DialogNodeType.Entry)
                {
                    string connectionFieldName = "RepliesList";
                    var connectionField = nodeStruct.GetField(connectionFieldName);
                    if (connectionField?.Value is GffList connectionList)
                    {
                        UnifiedLogger.LogParser(LogLevel.TRACE,
                            $"🔍 ENTRY PARSE DEBUG: Entry {connectionFieldName} has {connectionList.Elements.Count} connections");
                        UnifiedLogger.LogParser(LogLevel.TRACE,
                            $"🔍 GFF FIELD DEBUG: Field Type={connectionField.Type}, DataOrDataOffset={connectionField.DataOrDataOffset}");
                        UnifiedLogger.LogParser(LogLevel.TRACE,
                            $"🔍 GFF LIST DEBUG: Elements.Count={connectionList.Elements.Count}, Type={connectionList.GetType().Name}");

                        int connectionIdx = 0;
                        foreach (var connectionStruct in connectionList.Elements)
                        {
                            UnifiedLogger.LogParser(LogLevel.TRACE, $"🔍 ENTRY PARSE DEBUG: Processing connectionStruct[{connectionIdx}] Type={connectionStruct.Type}, Fields={connectionStruct.Fields.Count}");
                            var connectionPtr = BuildDialogPtrFromStruct(connectionStruct, currentDialog);
                            if (connectionPtr != null)
                            {
                                connectionPtr.Type = DialogNodeType.Reply;
                                node.Pointers.Add(connectionPtr);

                                UnifiedLogger.LogParser(LogLevel.TRACE,
                                    $"🔍 ENTRY PARSE DEBUG: Added Reply pointer Index={connectionPtr.Index} to Entry, total pointers: {node.Pointers.Count}");
                            }
                            else
                            {
                                UnifiedLogger.LogParser(LogLevel.ERROR, $"🔍 ENTRY PARSE DEBUG: Failed to build pointer from connectionStruct[{connectionIdx}]");
                            }
                            connectionIdx++;
                        }
                    }
                }
                
                else if (nodeType == DialogNodeType.Reply)
                {
                    // Reply nodes have EntriesList containing Sync Structs (pointers to Entry nodes)
                    var entriesListField = nodeStruct.GetField("EntriesList");
                    if (entriesListField?.Value is GffList entriesList)
                    {
                        UnifiedLogger.LogParser(LogLevel.TRACE, $"Reply EntriesList contains {entriesList.Elements.Count} Sync Structs");
                        UnifiedLogger.LogParser(LogLevel.TRACE, $"🔍 REPLY PARSE DEBUG: Reply EntriesList has {entriesList.Elements.Count} sync structs");
                        int structIdx = 0;
                        foreach (var syncStruct in entriesList.Elements)
                        {
                            UnifiedLogger.LogParser(LogLevel.TRACE, $"🔍 REPLY PARSE DEBUG: Processing syncStruct[{structIdx}] Type={syncStruct.Type}");
                            // SAFETY CHECK: Ensure this is actually a pointer struct, not content
                            if (IsPointerStruct(syncStruct))
                            {
                                var pointer = BuildDialogPtrFromStruct(syncStruct, currentDialog, DialogNodeType.Entry);
                                if (pointer != null)
                                {
                                    pointer.Type = DialogNodeType.Entry;
                                    node.Pointers.Add(pointer);
                                    UnifiedLogger.LogParser(LogLevel.TRACE, $"🔍 REPLY PARSE DEBUG: Added Reply->Entry pointer: Index={pointer.Index}, now have {node.Pointers.Count} total pointers");
                                }
                            }
                            else
                            {
                                UnifiedLogger.LogParser(LogLevel.WARN, $"Skipping non-pointer struct in EntriesList: Type={syncStruct.Type}, Fields={string.Join(",", syncStruct.Fields.Select(f => f.Label))}");
                            }
                            structIdx++;
                        }
                    }
                }

                UnifiedLogger.LogParser(LogLevel.TRACE, $"BuildDialogNodeFromStruct [{nodeType}] FINAL: node.Text.GetDefault() = '{node.Text.GetDefault()}'");

                return node;
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogParser(LogLevel.ERROR, $"Failed to build dialog node: {ex.Message}");
                return null;
            }
        }

        public bool IsPointerStruct(GffStruct gffStruct)
        {
            // Bioware Sync Structs (pointers) have specific field signatures:
            // - Index (DWORD) - always present
            // - Active (CResRef) - always present
            // - IsChild (BYTE) - always present
            // - LinkComment (CExoString) - optional
            // - ConditionParams - may be present for conditional dialogue

            var fieldLabels = gffStruct.Fields.Select(f => f.Label).ToHashSet();

            // Must have Index field to be a pointer struct (most reliable indicator)
            bool hasIndex = fieldLabels.Contains("Index");

            // Content structs have dialogue-specific fields that pointers never have
            bool hasDialogueContent = fieldLabels.Contains("Text") ||
                                     fieldLabels.Contains("Speaker") ||
                                     fieldLabels.Contains("Animation");

            // Log the decision for debugging
            if (hasIndex && !hasDialogueContent)
            {
                UnifiedLogger.LogParser(LogLevel.TRACE, $"✅ POINTER ACCEPTED: Type={gffStruct.Type}, Fields={string.Join(",", fieldLabels)}");
                return true;
            }
            else
            {
                UnifiedLogger.LogParser(LogLevel.WARN, $"❌ POINTER REJECTED: Type={gffStruct.Type}, Fields={string.Join(",", fieldLabels)} - HasIndex:{hasIndex}, HasDialogue:{hasDialogueContent}");
                return false;
            }
        }

        public uint ConvertGlobalToLocalIndex(uint globalIndex, DialogNodeType? expectedTargetType, Dialog? dialog)
        {
            // If no dialog context or no target type, assume it's already a local index
            if (dialog == null || expectedTargetType == null)
            {
                return globalIndex;
            }

            try
            {
                // Calculate the base struct indices based on GFF struct layout:
                // Root(0) → All Entries(1+) → All Replies(E+1+) → All Starts(E+R+1+) → All Pointers(E+R+S+1+)

                uint entryBaseIndex = 1; // Entries start at struct 1
                uint replyBaseIndex = 1 + (uint)dialog.Entries.Count; // Replies start after entries
                uint startBaseIndex = 1 + (uint)dialog.Entries.Count + (uint)dialog.Replies.Count; // Starts after replies

                switch (expectedTargetType)
                {
                    case DialogNodeType.Entry:
                        // Check if this is a global struct index first (in the expected Entry global range)
                        if (globalIndex >= entryBaseIndex && globalIndex < replyBaseIndex)
                        {
                            uint localIndex = globalIndex - entryBaseIndex;
                            UnifiedLogger.LogParser(LogLevel.TRACE, $"🔄 Entry conversion: global {globalIndex} → local {localIndex} (base: {entryBaseIndex})");
                            return localIndex;
                        }

                        // Special case: If globalIndex is within the Entry count, it might be a local index already
                        if (globalIndex < dialog.Entries.Count)
                        {
                            UnifiedLogger.LogParser(LogLevel.TRACE, $"🔄 Entry index {globalIndex} appears to be local (< {dialog.Entries.Count}), using as-is");
                            return globalIndex;
                        }
                        break;

                    case DialogNodeType.Reply:
                        // Check if this is a global struct index first (in the expected Reply global range)
                        if (globalIndex >= replyBaseIndex && globalIndex < startBaseIndex)
                        {
                            uint localIndex = globalIndex - replyBaseIndex;
                            UnifiedLogger.LogParser(LogLevel.TRACE, $"🔄 Reply conversion: global {globalIndex} → local {localIndex} (base: {replyBaseIndex})");
                            return localIndex;
                        }

                        // Special case: If globalIndex is within the Reply count, it might be a local index already
                        if (globalIndex < dialog.Replies.Count)
                        {
                            UnifiedLogger.LogParser(LogLevel.TRACE, $"🔄 Reply index {globalIndex} appears to be local (< {dialog.Replies.Count}), using as-is");
                            return globalIndex;
                        }
                        break;
                }

                // If conversion doesn't apply or is out of range, log a warning and return as-is
                UnifiedLogger.LogParser(LogLevel.WARN, $"🔄 Index conversion failed: global {globalIndex} for {expectedTargetType} (Entry base: {entryBaseIndex}, Reply base: {replyBaseIndex})");
                return globalIndex;
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogParser(LogLevel.ERROR, $"🔄 Index conversion error: {ex.Message}");
                return globalIndex;
            }
        }

        public DialogPtr? BuildDialogPtrFromStruct(GffStruct ptrStruct, Dialog? parentDialog, DialogNodeType? expectedTargetType = null)
        {
            try
            {
                var ptr = new DialogPtr
                {
                    Parent = parentDialog,
                    // 2025-10-21: Preserve original GFF struct for round-trip type preservation
                    OriginalGffStruct = ptrStruct
                };
                
                // Debug: Log available fields in the pointer struct
                var fieldNames = string.Join(", ", ptrStruct.Fields.Select(f => f.Label));
                UnifiedLogger.LogParser(LogLevel.TRACE, $"🔍 POINTER STRUCT DEBUG: Type={ptrStruct.Type}, Fields: {fieldNames}");
                
                // Parse pointer properties
                // CRITICAL: For BYTE fields, the value is stored in DataOrDataOffset, not in field.Value!
                var isChildField = ptrStruct.GetField("IsChild");
                byte isChildValue = 0;
                if (isChildField != null && isChildField.Type == GffField.BYTE)
                {
                    isChildValue = (byte)(isChildField.DataOrDataOffset & 0xFF);
                }
                ptr.IsLink = isChildValue != 0;
                ptr.ScriptAppears = ptrStruct.GetFieldValue<string>("Active", string.Empty);

                UnifiedLogger.LogParser(LogLevel.TRACE, $"🔗 Pointer IsChild={isChildValue}, IsLink={ptr.IsLink}");

                // Handle LinkComment per Bioware spec
                // IsChild=1 (IsLink=true): LinkComment field exists
                // IsChild=0 (IsLink=false): No LinkComment field (it's in the node's Comment)
                if (ptr.IsLink)
                {
                    ptr.LinkComment = ptrStruct.GetFieldValue<string>("LinkComment", string.Empty);
                    UnifiedLogger.LogParser(LogLevel.TRACE, $"🔗 LINK DETECTED: LinkComment='{ptr.LinkComment}'");
                }
                
                // Parse the index reference - FIXED: Read directly from raw data for DWORD fields
                var indexField = ptrStruct.GetField("Index");
                if (indexField != null)
                {
                    if (indexField.Type == GffField.DWORD)
                    {
                        // For DWORD fields, use the raw DataOrDataOffset directly to avoid float conversion bugs
                        // NOTE: Indices in RepliesList/EntriesList are already local array indices, not global struct indices
                        ptr.Index = indexField.DataOrDataOffset;
                        UnifiedLogger.LogParser(LogLevel.TRACE, $"🔍 POINTER INDEX: Read Index={ptr.Index} as local array index");
                    }
                    else if (indexField?.Value is float floatValue)
                    {
                        ptr.Index = (uint)floatValue;
                        UnifiedLogger.LogParser(LogLevel.TRACE, $"🔍 START DEBUG: Converted float Index {floatValue} to uint {ptr.Index}");
                    }
                    else
                    {
                        ptr.Index = ptrStruct.GetFieldValue<uint>("Index", uint.MaxValue);
                        UnifiedLogger.LogParser(LogLevel.TRACE, $"🔍 START DEBUG: Used direct Index parsing: {ptr.Index}");
                    }
                }
                else
                {
                    ptr.Index = uint.MaxValue;
                    UnifiedLogger.LogParser(LogLevel.ERROR, $"🔍 ERROR: No Index field found in pointer struct");
                }

                // Parse ConditionParams if present
                var conditionParamsField = ptrStruct.GetField("ConditionParams");
                if (conditionParamsField?.Value is GffList conditionParamsList)
                {
                    foreach (var paramStruct in conditionParamsList.Elements)
                    {
                        var key = paramStruct.GetFieldValue<string>("Key", string.Empty);
                        var value = paramStruct.GetFieldValue<string>("Value", string.Empty);
                        if (!string.IsNullOrEmpty(key))
                        {
                            ptr.ConditionParams[key] = value;
                            UnifiedLogger.LogParser(LogLevel.TRACE, $"Added condition parameter: {key} = {value}");
                        }
                    }
                }
                
                // Determine target type based on context or field presence
                // This will need to be resolved later when we link nodes
                
                return ptr;
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogParser(LogLevel.ERROR, $"Failed to build dialog pointer: {ex.Message}");
                return null;
            }
        }
    }
}
