# Code Path Map - DialogParser

**Purpose**: Track active code paths for DLG read/write operations
**Last Updated**: 2025-10-28
**Note**: This information was discovered and written by Claude AI.
---

## ARCHITECTURE OVERVIEW (Post-Phase 3)

**DialogParser** (4143 lines): Main parser - delegates to support classes
**DialogBuilder** (637 lines): Builds Dialog models from GFF structs (Phase 2)
**DialogWriter** (2513 lines): Handles all write/serialization operations (Phase 3)
**GffIndexFixer** (~400 lines): Fixes field indices for Aurora compatibility (Phase 1)

---

## WRITE PATH (Save DLG File)

**Entry Point**: `MainWindow.axaml.cs` calls `MainViewModel.SaveDialogAsync()`

### Call Chain (Phase 3 Refactored)
```
MainViewModel.SaveDialogAsync (MainViewModel.cs:250-280)
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

**Entry Point**: `MainWindow.axaml.cs` calls `MainViewModel.OpenDialogAsync()`

### Call Chain (Phase 2 Refactored)
```
MainViewModel.OpenDialogAsync (MainViewModel.cs:170-200)
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

## USAGE GUIDELINES

**Before Adding New Write Logic**:
1. Check this map - which method in the chain needs changes?
2. Add logging to confirm code executes in the right place
3. Run regression tests before committing

**Before Adding New Read Logic**:
1. Check GffBinaryReader first - might be there instead of DialogParser
2. Verify structs vs fields - don't confuse the two
3. Test with multiple DLG files (simple and complex)

**When Lost**:
1. Add logging with unique emoji/prefix
2. Run test - if logging doesn't appear, check call chain
3. Update this map when you find the actual path

---

## REGRESSION TESTING

**Before Parser Changes**:
1. Run `TestingTools/Scripts/QuickRegressionCheck.ps1`
2. Verify struct count, field count, StartingList count
3. Test in Aurora Editor (tree structure, script names)





