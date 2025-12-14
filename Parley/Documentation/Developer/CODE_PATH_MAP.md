# Code Path Map - Parley Architecture

**Purpose**: Track active code paths for file operations and UI workflows
**Last Updated**: 2025-12-13 (Epic #368: Menu Reorganization)
**Note**: This information was discovered and written by Claude AI.

---

## ARCHITECTURE OVERVIEW

### Parsers & Writers

**DialogParser** (~4143 lines): Main parser - delegates to support classes
**DialogBuilder** (637 lines): Builds Dialog models from GFF structs
**DialogWriter** (2513 lines): Handles all write/serialization operations
**GffBinaryReader** (~400 lines): Reads GFF binary format
**GffIndexFixer** (~400 lines): Fixes field indices for Aurora compatibility

### Services & Managers (Epic #99 + #163 Refactoring)

**DialogFileService** (~200 lines): Facade for file operations (load/save)
**UndoManager** (~150 lines): Manages undo/redo state history
**DialogClipboardService** (~300 lines): Copy/paste operations
**ScrapManager** (~420 lines): Manages deleted/scrapped nodes + scrap restoration
  - 2025-11-19: Added RestoreFromScrap
  - 2025-12-13: Added `RemoveRestoredNodes()` for undo scrap cleanup (#356)
  - 2025-12-13: Fixed file-based filtering in `AddToScrap`, `RemoveFromScrap`, `ClearScrapForFile` (#352)
**OrphanNodeManager** (~250 lines): Orphan detection and cleanup (2025-11-18: Fixed to skip child links)
**NodeOperationsManager** (~530 lines): Node add/delete/move operations (Phase 6, PR #137)
  - 2025-12-13: Fixed `CheckNodeForLinks` to skip link children (bookmarks) - prevents stack overflow on complex dialogs (#368)
**TreeNavigationManager** (~280 lines): Tree traversal, expansion state, node finding (Phase 7, PR #133)
**DialogEditorService** (~200 lines): Node editing operations
**PropertyPanelPopulator** (~460 lines): Properties panel population (Phase 5, PR #135)
**IndexManager** (~280 lines): Pointer index management and validation (Phase 7, 2025-11-19)
**NodeCloningService** (~140 lines): Deep node cloning with circular reference detection (Phase 7, 2025-11-19)
**ReferenceManager** (~135 lines): Reference counting and pointer detachment (Phase 7, 2025-11-19)
**PasteOperationsManager** (~220 lines): Paste as duplicate with type validation (Phase 7, 2025-11-19)
**SettingsService** (~650 lines): Application settings persistence
**UnifiedLogger** (~385 lines): Session-based logging with path sanitization
**DebugLogger** (~180 lines): UI debug console with log level filtering

### MainWindow Services (Epic #163 - PR #164)

**PropertyAutoSaveService** (245 lines): Auto-save property changes with strategy pattern (2025-11-22)
**ScriptParameterUIManager** (~380 lines): Parameter UI management for conditions/actions
  - 2025-11-22: Initial extraction from MainWindow
  - 2025-12-13: Added `HasAnyDuplicateKeys()` for save validation (#289)
**NodeCreationHelper** (229 lines): Smart node creation with debouncing and tree navigation (2025-11-22)
**ResourceBrowserManager** (~230 lines): Sound/Creature browser dialogs and recent tags
  - 2025-11-22: Initial extraction from MainWindow
  - 2025-12-13: Added lazy loading callback for creatures (#5 startup performance)
**KeyboardShortcutManager** (215 lines): Data-driven keyboard shortcuts with 20+ mappings (2025-11-22)
**DebugAndLoggingHandler** (311 lines): Log export, scrap operations, debug console (2025-11-22)
**WindowPersistenceManager** (252 lines): Window/panel persistence and screen validation (2025-11-22)

### Plugin System (Epic 40 - PR #244)

**DialogContextService** (~237 lines): Singleton providing dialog state to plugins (2025-11-29)
**PluginGrpcServer** (~386 lines): gRPC server hosting plugin services (2025-11-29)
**PluginPanelManager** (~200 lines): Panel registration and lifecycle management (2025-11-29)
**PluginUIService** (~150 lines): UI operations for plugins (notifications, dialogs) (2025-11-29)

### TreeView Models (Models/TreeViewSafeNode.cs)

**TreeViewSafeNode** (~470 lines): Wrapper for DialogNode providing circular reference protection
  - Lazy loading via `IsExpanded` setter and `PopulateChildren()`
  - Terminal node detection: Empty nodes show `[END DIALOG]` vs `[CONTINUE]` (#353, 2025-12-13)
  - Depth limit: 250 levels max (increased from 50, #32 2025-12-13)

**TreeViewSafeLinkNode**: Link node for IsLink=true pointers (terminal, no children)
**TreeViewRootNode**: ROOT node representing the dialog file
**TreeViewPlaceholderNode**: Placeholder for lazy loading ("Loading...")

### ViewModels & UI

**MainViewModel** (~1,258 lines as of Phase 7): **REFACTORING COMPLETE - GOAL EXCEEDED ✅**

- Down from ~2,956 lines (Phase 7: -1,698 lines, 57% reduction)
- **Target Exceeded**: Now 258 lines BELOW 1,000 line goal
- Phase 7 Extractions: IndexManager, NodeCloningService, ReferenceManager, PasteOperationsManager, RestoreFromScrap

**MainWindow.axaml.cs** (2,603 lines): **EPIC #163 REFACTORING COMPLETE ✅**

- Down from 4,126 lines (Epic #163: -1,523 lines, 37% reduction)
- **7 Services Extracted**: PropertyAutoSave, ScriptParameterUI, NodeCreation, ResourceBrowser, KeyboardShortcuts, DebugLogging, WindowPersistence
- Implements IKeyboardShortcutHandler interface
- All build warnings fixed (5 → 0)
- All 231 tests passing

---

## WRITE PATH (Save DLG File)

**Entry Point**: User clicks Save → MainWindow → FileOperationsHandler

### Pre-Save Validation (2025-12-13)

```
MainWindow.OnSaveClick (MainWindow.axaml.cs)
  ↓ Check for duplicate parameter keys (#289)
ScriptParameterUIManager.HasAnyDuplicateKeys()
  ↓ If duplicates found → show warning dialog, abort save
  ↓ If clean → proceed with save
```

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

## PASTE PATH (Copy/Paste Operations)

**Entry Point**: User right-clicks → Paste as Duplicate → MainViewModel.PasteAsDuplicate

### Call Chain (Phase 7 Architecture - 2025-11-19)

```
MainViewModel.PasteAsDuplicate (MainViewModel.cs:1280)
  ↓ Save undo state
  ↓ Delegate to PasteOperationsManager
PasteOperationsManager.PasteAsDuplicate (PasteOperationsManager.cs:48)
  ↓
  ↓ Validate parent is selected
  ↓ Check clipboard has content
  ↓
  ↓ Route based on parent type
  ↓
  ├→ PasteToRoot (PasteOperationsManager.cs:73)
  │   ↓ Validate PC Reply not pasted to ROOT
  │   ↓ Clone or reuse node (cut vs copy)
  │   ↓ Convert NPC Reply → Entry at ROOT (GFF requirement)
  │   ↓ Add to dialog.Entries list
  │   ↓ Create start pointer (IsStart=true)
  │   ↓ Register with LinkRegistry
  │   ↓ Recalculate indices
  │
  └→ PasteToParent (PasteOperationsManager.cs:155)
      ↓ Validate type alternation (Entry→Reply→Entry)
      ↓ Clone or reuse node (cut vs copy)
      ↓ Add to appropriate list (Entries/Replies)
      ↓ Create pointer from parent
      ↓ Register with LinkRegistry
      ↓ Recalculate indices
  ↓
  ↓ Return PasteResult
  ↓
MainViewModel updates StatusMessage and refreshes tree
```

### Key Components

**Result Pattern**:

- `PasteResult` - Success flag, status message, pasted node reference
- Clean separation: Service handles logic, ViewModel handles UI

**Type Validation**:

- ROOT only accepts Entry nodes (NPC speech)
- Entry→Reply alternation enforced (conversation structure)
- PC Reply auto-converted to Entry at ROOT

**Index Management**:

- Uses `IndexManager.RecalculatePointerIndices` after paste
- Ensures pointer.Index matches list position

---

## SCRAP RESTORATION PATH (Restore from Scrap)

**Entry Point**: User selects scrap entry + tree parent → Restore → MainViewModel.RestoreFromScrap

### Call Chain (Phase 7 Architecture - 2025-11-19)

```
MainViewModel.RestoreFromScrap (MainViewModel.cs:1441)
  ↓ Save undo state
  ↓ Delegate to ScrapManager
ScrapManager.RestoreFromScrap (ScrapManager.cs:228)
  ↓
  ↓ Validate dialog loaded
  ↓ Validate parent selected
  ↓ Retrieve node from scrap (GetNodeFromScrap)
  ↓
  ↓ Validate restoration target
  ↓   - ROOT: Only Entry nodes allowed
  ↓   - Parent type: Enforce Entry→Reply alternation
  ↓
  ↓ Add restored node to Entries/Replies list
  ↓ Get node index from list position
  ↓
  ↓ Create pointer to restored node
  ↓
  ├→ Restore to ROOT
  │   ↓ Set ptr.IsStart = true
  │   ↓ Add to dialog.Starts list
  │
  └→ Restore to Parent
      ↓ Add to parent.Pointers
      ↓ Set parent.IsExpanded = true
  ↓
  ↓ Register pointer with LinkRegistry
  ↓ IndexManager.RecalculatePointerIndices
  ↓ Remove from scrap (only after successful restoration)
  ↓
  ↓ Return RestoreResult
  ↓
MainViewModel updates StatusMessage and refreshes tree
```

### Key Components

**Result Pattern**:

- `RestoreResult` - Success flag, status message, restored node reference
- Clean separation: Service handles logic, ViewModel handles UI

**Validation Rules**:

- Same as paste: Entry→Reply alternation enforced
- ROOT validation: Only Entry nodes allowed
- No type conversion (unlike paste)

**Transaction Safety**:

- Node only removed from scrap AFTER successful restoration
- Validation runs BEFORE making any changes
- If validation fails, scrap entry preserved

**Index Management**:

- Uses `IndexManager.RecalculatePointerIndices` after restoration
- Ensures all pointers stay synchronized with list positions

---

## UNDO/REDO PATH

**Entry Point**: User presses Ctrl+Z → MainViewModel.Undo

### Call Chain (2025-12-13: Added scrap cleanup #356)

```
MainViewModel.Undo (MainViewModel.cs)
  ↓ Pop previous state from UndoManager
UndoManager.PopUndo
  ↓ Restore dialog JSON
  ↓
MainViewModel.RebuildDialogFromJson
  ↓ Reconstruct Dialog object from stored JSON
  ↓
  ↓ NEW (2025-12-13): Clean up scrap for restored nodes
ScrapManager.RemoveRestoredNodes (ScrapManager.cs)
  ↓ Compare scrap entries against current dialog
  ↓ Remove entries matching restored Entry/Reply nodes
  ↓ Save updated scrap data
  ↓ Update filtered ScrapEntries for current file
  ↓
MainViewModel refreshes tree and UI
```

### Key Components

**Scrap Cleanup Logic**:

- After undo restores deleted nodes, those nodes should no longer appear in scrap
- `RemoveRestoredNodes()` matches by node text and type (Entry/Reply)
- Only removes entries for the current file (file-based filtering)

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

---

## NATIVE FLOWCHART PATH (Epic #325)

**Entry Point**: UI loads/updates dialog → DialogToFlowchartConverter → GraphPanel

### Data Model (Sprint 1 - Foundation)

**FlowchartGraph** (~240 lines): Container for flowchart visualization data (2025-12-10)
- `FlowchartNode` - Node view model (id, type, text, speaker, conditions, actions)
- `FlowchartEdge` - Edge between nodes (source, target, conditional, link)
- `FlowchartGraph` - Graph container (nodes dictionary, edges list, root node IDs)

**DialogToFlowchartConverter** (~195 lines): Dialog → FlowchartGraph transformation (2025-12-10)
- Handles circular references gracefully (visited node tracking)
- Creates link nodes for IsLink=true pointers (visual representation)
- Preserves OriginalNode/OriginalPointer references for selection sync

### Conversion Flow (Sprint 1)

```
Dialog (DialogStructures.cs)
  ↓
DialogToFlowchartConverter.Convert() (DialogToFlowchartConverter.cs)
  ↓ AssignNodeIds - creates E0, E1, R0, R1 IDs
  ↓ ProcessNode - recursive traversal from Starts
  ↓ CreateFlowchartNode - maps DialogNode → FlowchartNode
  ↓ Handle links - creates L0, L1 link nodes
  ↓
FlowchartGraph (FlowchartGraph.cs)
  ↓ [Sprint 2: Convert to AvaloniaGraphControl.Graph]
  ↓
GraphPanel (AvaloniaGraphControl - NuGet package)
```

### Key Components

**Node Types**:
- `FlowchartNodeType.Entry` - NPC dialog (orange in UI)
- `FlowchartNodeType.Reply` - PC response (blue in UI)
- `FlowchartNodeType.Link` - Visual link to existing node (dashed)

**Circular Reference Handling**:
- `_visitedNodes` HashSet prevents infinite recursion
- Link nodes created for back-references (pointer.IsLink=true)
- Target node ID preserved in LinkTargetId property

**Original Data Preservation**:
- FlowchartNode.OriginalNode → DialogNode (for selection sync)
- FlowchartNode.OriginalPointer → DialogPtr (for condition display)

### Planned Sprints

- **Sprint 2** (#327): Basic rendering with AvaloniaGraphControl.GraphPanel
- **Sprint 3** (#328): Visual polish, node click → tree selection
- **Sprint 4** (#329): Layout options, export to image

---

## PLUGIN SYSTEM PATH (Epic 40)

**Entry Point**: Plugin process connects via gRPC on startup

### Plugin Initialization Flow

```
Python Plugin (flowchart_plugin.py)
  ↓ Read PARLEY_GRPC_PORT env var
  ↓
ParleyClient.__init__ (client.py)
  ↓ Connect to localhost:{port}
  ↓ Create service stubs (AudioService, UIService, DialogService)
  ↓
Plugin.initialize()
  ↓ register_panel() → gRPC → PluginGrpcServer
  ↓
UIServiceImpl.RegisterPanel (PluginGrpcServer.cs)
  ↓ Dispatcher.UIThread.InvokeAsync
  ↓
PluginPanelManager.RegisterPanel (PluginPanelManager.cs)
  ↓ Create PluginPanelWindow
  ↓ Store in _panels dictionary
  ↓ Return panel_id to plugin
```

### Dialog Data Query Flow

```
Plugin requests dialog structure
  ↓
ParleyClient.get_dialog_structure() (client.py)
  ↓ gRPC call
  ↓
DialogServiceImpl.GetDialogStructure (PluginGrpcServer.cs)
  ↓
DialogContextService.Instance.GetDialogStructure (DialogContextService.cs)
  ↓ Traverse dialog tree (Starts → Entries → Replies)
  ↓ Build nodes[] and links[] arrays
  ↓ Return to plugin as protobuf
  ↓
Plugin renders D3.js flowchart (flowchart_plugin.py)
```

### Panel Content Update Flow

```
Plugin.update_panel_content() (client.py)
  ↓ gRPC: UpdatePanelContentRequest
  ↓
UIServiceImpl.UpdatePanelContent (PluginGrpcServer.cs)
  ↓ Dispatcher.UIThread.InvokeAsync
  ↓
PluginPanelManager.UpdatePanelContent (PluginPanelManager.cs)
  ↓ Lookup panel by ID
  ↓
PluginPanelWindow.UpdateContent (PluginPanelWindow.axaml.cs)
  ↓ content_type == "html" → WebView.LoadHtml()
  ↓ content_type == "url" → WebView.LoadUrl()
```

### State Synchronization

```
MainViewModel property change (MainViewModel.cs)
  ↓ CurrentDialog setter
  ↓
DialogContextService.Instance.CurrentDialog = value
  ↓ Fires DialogChanged event
  ↓
[Plugins poll or subscribe for changes]
```

### Key Components

**Proto Definitions** (plugin.proto):

- `DialogService`: GetCurrentDialog, GetSelectedNode, GetDialogStructure
- `UIService`: ShowNotification, ShowDialog, RegisterPanel, UpdatePanelContent
- `AudioService`: PlayAudio, StopAudio

**State Provider**:

- `DialogContextService` singleton - decouples plugins from MainViewModel
- Provides: CurrentDialog, CurrentFileName, SelectedNodeId
- Methods: GetDialogStructure() returns (nodes, links) for D3.js

**Panel Management**:

- `PluginPanelManager` - registry of active panels
- `PluginPanelWindow` - WebView-based panel window
- Supports: docking positions, floating, close behavior

---
