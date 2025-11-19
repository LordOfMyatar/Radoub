# Code Path Map - Parley Architecture

**Purpose**: Track active code paths for file operations and UI workflows
**Last Updated**: 2025-11-18
**Note**: This information was discovered and written by Claude AI.

---

## ARCHITECTURE OVERVIEW

### Parsers & Writers
**DialogParser** (~4143 lines): Main parser - delegates to support classes
**DialogBuilder** (637 lines): Builds Dialog models from GFF structs
**DialogWriter** (2513 lines): Handles all write/serialization operations
**GffBinaryReader** (~400 lines): Reads GFF binary format
**GffIndexFixer** (~400 lines): Fixes field indices for Aurora compatibility

### Services & Managers (Epic #99 Refactoring)
**DialogFileService** (~200 lines): Facade for file operations (load/save)
**UndoManager** (~150 lines): Manages undo/redo state history
**DialogClipboardService** (~300 lines): Copy/paste operations
**ScrapManager** (~250 lines): Manages deleted/scrapped nodes
**OrphanNodeManager** (~250 lines): Orphan detection and cleanup (2025-11-18: Fixed to skip child links)
**NodeOperationsManager** (~530 lines): Node add/delete/move operations (Phase 6, PR #137)
**TreeNavigationManager** (~280 lines): Tree traversal, expansion state, node finding (Phase 7, PR #133)
**DialogEditorService** (~200 lines): Node editing operations
**PropertyPanelPopulator** (~460 lines): Properties panel population (Phase 5, PR #135)
**SettingsService** (~650 lines): Application settings persistence
**UnifiedLogger** (~385 lines): Session-based logging with path sanitization
**DebugLogger** (~180 lines): UI debug console with log level filtering

### ViewModels & UI
**MainViewModel** (~2,933 lines as of Phase 6): **ACTIVELY REFACTORING - DO NOT ADD LOGIC HERE**
- Down from ~3,500 lines (Phase 6 NodeOperationsManager: -332 lines)
- Target: < 2,000 lines by end of Epic #99
**MainWindow.axaml.cs** (~370 lines): Thin coordinator - delegates to handlers

### Handlers (UI Logic Extracted from MainWindow)
**FileOperationsHandler** (~200 lines): Open, save, recent files
**ThemeAndSettingsHandler** (~150 lines): Theme, font size, game dirs
**TreeViewHandler** (~300 lines): Tree operations, selection, expand/collapse
**PropertiesPanelHandler** (~400 lines): Properties panel, script preview

---

## WRITE PATH (Save DLG File)

**Entry Point**: User clicks Save → MainWindow → FileOperationsHandler

### Call Chain (Current Architecture)
```
FileOperationsHandler.OnSaveClick (FileOperationsHandler.cs)
  ↓
MainViewModel.SaveDialogAsync (MainViewModel.cs)
  ↓
DialogFileService.SaveToFileAsync (DialogFileService.cs)
  ↓
DialogParser.WriteToFileAsync (DialogParser.cs:110)
  ↓ DELEGATES TO DialogWriter
DialogWriter.CreateDlgBuffer (DialogWriter.cs:2931)
  ↓
DialogWriter.CreateFullDlgBufferManual (DialogWriter.cs:2944)
  ↓
DialogWriter.ConvertDialogToGff (DialogWriter.cs:2956)
  ↓ Creates GFF structures
DialogWriter.CreateAuroraCompatibleGffStructures (DialogWriter.cs:561)
  ↓ Builds all fields + structs
  ↓
DialogWriter.WriteBinaryGff (DialogWriter.cs:1684)
  ↓ Writes binary GFF format
  ↓
DialogWriter.WriteFieldIndices (DialogWriter.cs:2101)
DialogWriter.WriteListIndices (DialogWriter.cs:2186)
```

### Key Methods in DialogWriter

**Field Creation Methods**:
- CreateRootFields (DialogWriter.cs:767) - 9 BioWare root fields
- CreateSingleEntryFields (DialogWriter.cs:896) - 12 fields per entry
- CreateSingleReplyFields (DialogWriter.cs:1013) - 11 fields per reply
- CreatePointerFields (DialogWriter.cs:687) - 4 fields per pointer
- CreateStartingFields (DialogWriter.cs:1114) - 3 fields per start

**Struct Creation Methods**:
- CreateInterleavedStructs (DialogWriter.cs:3361) - Conversation-flow order
- CreateDynamicParameterStructs (DialogWriter.cs:3847) - ActionParams/ConditionParams

**Calculation Methods**:
- CalculateListIndicesOffsets (DialogWriter.cs:54) - Pre-calculates list offsets
- CalculateEntryFieldCount (DialogWriter.cs:2244) - Count fields per entry
- CalculateReplyFieldCount (DialogWriter.cs:2254) - Count fields per reply

**Binary Writing**:
- WriteBinaryGff (DialogWriter.cs:1684) - Main binary serialization
- WriteFieldIndices (DialogWriter.cs:2101) - Aurora field index format
- WriteListIndices (DialogWriter.cs:2186) - ListIndices section

---

## READ PATH (Load DLG File)

**Entry Point**: User clicks Open → MainWindow → FileOperationsHandler

### Call Chain (Current Architecture)
```
FileOperationsHandler.OnOpenClick (FileOperationsHandler.cs)
  ↓
MainViewModel.LoadDialogAsync (MainViewModel.cs)
  ↓
DialogFileService.LoadFromFileAsync (DialogFileService.cs)
  ↓
DialogParser.ParseFromFileAsync (DialogParser.cs:26)
  ↓
DialogParser.ParseFromBufferAsync (DialogParser.cs:68)
  ↓
GffBinaryReader.ParseGffHeader (GffBinaryReader.cs:~30)
  ↓ Reads header.FieldCount from file
GffBinaryReader.ParseFields (GffBinaryReader.cs:79)
  ↓ Creates GffField[] array
  ↓
GffBinaryReader.ParseStructs (GffBinaryReader.cs:~45)
GffBinaryReader.ParseLabels (GffBinaryReader.cs:~110)
  ↓
DialogParser.AssignFieldsToStructs (DialogParser.cs:239)
  ↓ DELEGATES TO DialogBuilder
DialogBuilder.BuildDialogFromGffStruct (DialogBuilder.cs:16)
  ↓
DialogBuilder.BuildDialogNodeFromStruct (DialogBuilder.cs:39)
DialogBuilder.BuildDialogPtrFromStruct (DialogBuilder.cs:185)
```

---

## DELETE PATH (Node Deletion)

**Entry Point**: User right-clicks node → Delete → MainViewModel.DeleteNode

### Call Chain (Current Architecture - Phase 6)
```
MainViewModel.DeleteNode (MainViewModel.cs:926)
  ↓ Save undo state
  ↓ Delegate to NodeOperationsManager
NodeOperationsManager.DeleteNode (NodeOperationsManager.cs:67)
  ↓
  ↓ Check for incoming links to deleted node
  CheckForIncomingLinks (NodeOperationsManager.cs:320)
  ↓
  ↓ Collect node + all children for deletion
  CollectNodeAndChildren (NodeOperationsManager.cs:415)
  ↓
  ↓ Add to scrap BEFORE deleting
  ScrapManager.AddToScrap (ScrapManager.cs)
  ↓
  ↓ CRITICAL: Identify orphaned link children (PR #132 "evil twin" fix)
  OrphanNodeManager.IdentifyOrphanedLinkChildren (OrphanNodeManager.cs:193)
    ↓ Check children of nodes being deleted
    ↓ Find nodes with ONLY child link (IsLink=true) references
    ↓ These will become orphaned after deletion
  ↓
  ↓ Remove orphaned link children from Entries/Replies lists
  OrphanNodeManager.RemoveOrphanedLinkChildrenFromLists (OrphanNodeManager.cs:238)
  ↓
  ↓ Recursively delete node + children from dialog
  DeleteNodeRecursive (NodeOperationsManager.cs:390)
  ↓
  ↓ Recalculate pointer indices
  RecalculatePointerIndices (NodeOperationsManager.cs:490)
  ↓
  ↓ Remove orphaned pointers (dangling references)
  OrphanNodeManager.RemoveOrphanedPointers (OrphanNodeManager.cs:24)
  ↓
  ↓ CRITICAL: Remove orphaned nodes immediately (2025-11-18 fix)
  OrphanNodeManager.RemoveOrphanedNodes (OrphanNodeManager.cs:108)
    ↓ CollectReachableNodes from START (skips IsLink=true pointers)
    ↓ Remove unreachable nodes from Entries/Replies
    ↓ Add orphaned nodes to scrap
  ↓
  ↓ Return to MainViewModel
  ↓
MainViewModel.RefreshTreeView (MainViewModel.cs:1242)
  ↓ Tree view updates to reflect deletion
```

### Key Components

**Orphan Detection (2025-11-18 Fix)**:
- `CollectReachableNodes` (OrphanNodeManager.cs:170) - **ONLY traverses regular pointers (IsLink=false)**
- Nodes with only child link (IsLink=true) references are considered unreachable → orphaned
- Example: Chef reply with only child link from "I go through that sometimes..." becomes orphaned

**Scrap Integration**:
- Deleted nodes added to scrap with reason "deleted"
- Orphaned link children added with reason "orphaned link child"
- Orphaned nodes added with reason "orphaned after deletion"
- All recoverable from Scrap tab

**Link Handling**:
- Nodes with incoming links get warning logged
- Links are NOT followed during deletion (Aurora behavior: delete parent = delete subtree)
- Child links (IsLink=true) do NOT prevent orphaning (2025-11-18 fix)

---

## LOGGING & DEBUG CONSOLE PATH

**Entry Point**: `UnifiedLogger.Log*()` methods called throughout codebase

### Call Chain (Logging to UI)
```
UnifiedLogger.LogApplication/LogUI/LogParser/etc. (UnifiedLogger.cs)
  ↓ Automatic path sanitization
  ↓ Write to session log file
  ↓ Invoke debug console callback
  ↓
DebugLogger callback (DebugLogger.cs:20-60)
  ↓ Parse log level from message
  ↓ Store message with level (keeps last 1000)
  ↓ Apply filter (ERROR/WARN/INFO/DEBUG/TRACE)
  ↓ If passes filter:
    ↓
  MainViewModel.AddDebugMessage (MainViewModel.cs)
    ↓ Dispatcher.UIThread.Post
    ↓
  DebugMessages ObservableCollection
    ↓
  UI ListBox (Debug tab in MainWindow.axaml)
```

### Log Level Filtering
**Filter UI**: Debug tab → ComboBox (Error/Warning/Info/Debug/Trace)
**Filter Logic**: DebugLogger.SetLogLevelFilter() → RefreshDisplay()
**Default Level**: INFO (shows ERROR, WARN, INFO only)

**IMPORTANT**: Filtering logic is in **DebugLogger**, NOT MainViewModel
- MainViewModel just displays messages it receives
- DebugLogger handles all parsing, storage, and filtering

---

## USAGE GUIDELINES

**Before Adding New Logic**:
1. **Check CLAUDE.md** - Is MainViewModel actively being refactored?
2. **DO NOT add to MainViewModel** - Create new service/manager instead
3. **Check this map** - Which component in the chain needs changes?
4. **Add logging** to confirm code executes in the right place
5. **Run regression tests** before committing

**Before Adding New Write Logic**:
1. Check DialogWriter first - most write logic is there
2. Verify field counts match Aurora spec
3. Test round-trip (save → load → verify)

**Before Adding New Read Logic**:
1. Check GffBinaryReader first - might be there instead of DialogParser
2. Check DialogBuilder - model construction happens here
3. Verify structs vs fields - don't confuse the two
4. Test with multiple DLG files (simple and complex)

**Before Adding New UI Logic**:
1. Check if handler class already exists for this area
2. If no handler, consider creating one instead of adding to MainWindow
3. MainViewModel should only coordinate state, not contain business logic

**When Lost**:
1. Add logging with unique emoji/prefix
2. Run test - if logging doesn't appear, check call chain
3. Update this map when you find the actual path

**MainViewModel Refactoring Note**:
- MainViewModel is intentionally not mapped in detail here (too large, actively changing)
- Focus is on stable service/parser paths
- As MainViewModel gets refactored, those services will be added to this map
- See CLAUDE.md for MainViewModel refactoring status

---

## REGRESSION TESTING

**Before Parser Changes**:
1. Run `TestingTools/Scripts/QuickRegressionCheck.ps1`
2. Verify struct count, field count, StartingList count
3. Test in Aurora Editor (tree structure, script names)





