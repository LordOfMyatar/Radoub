# Code Path Map - Parley Architecture

**Purpose**: Track active code paths for file operations and UI workflows
**Last Updated**: 2025-11-16
**Note**: This information was discovered and written by Claude AI.

---

## ARCHITECTURE OVERVIEW

### Parsers & Writers
**DialogParser** (~4143 lines): Main parser - delegates to support classes
**DialogBuilder** (637 lines): Builds Dialog models from GFF structs
**DialogWriter** (2513 lines): Handles all write/serialization operations
**GffBinaryReader** (~400 lines): Reads GFF binary format
**GffIndexFixer** (~400 lines): Fixes field indices for Aurora compatibility

### Services & Managers
**DialogFileService** (~200 lines): Facade for file operations (load/save)
**UndoManager** (~150 lines): Manages undo/redo state history
**DialogClipboardService** (~300 lines): Copy/paste operations
**ScrapManager** (~250 lines): Manages deleted/scrapped nodes
**DialogEditorService** (~200 lines): Node editing operations
**SettingsService** (~650 lines): Application settings persistence
**UnifiedLogger** (~385 lines): Session-based logging with path sanitization
**DebugLogger** (~180 lines): UI debug console with log level filtering

### ViewModels & UI
**MainViewModel** (~3,500 lines): **ACTIVELY REFACTORING - DO NOT ADD LOGIC HERE**
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





