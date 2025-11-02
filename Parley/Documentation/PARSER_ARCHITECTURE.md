# Parser Architecture

This document describes the current architecture of the DLG parser system after refactoring (October 2025).

## Overview

The parser system reads and writes Neverwinter Nights DLG files in Aurora Engine GFF binary format. The architecture separates concerns into specialized classes for reading, writing, and validation.

## Core Components

### DialogFileService (Public API)
**Location**: `Parley/Services/DialogFileService.cs` (151 lines)
**Access**: Public
**Purpose**: Primary interface for all dialog file I/O operations.

**Methods**:
- `LoadFromFileAsync(string filePath)` - Load DLG from file path
- `LoadFromStreamAsync(Stream stream)` - Load DLG from stream
- `LoadFromBufferAsync(byte[] buffer)` - Load DLG from byte array
- `LoadFromJsonAsync(string json)` - Load DLG from JSON
- `SaveToFileAsync(Dialog dialog, string filePath)` - Save DLG to file
- `SaveToStreamAsync(Dialog dialog, Stream stream)` - Save DLG to stream
- `ConvertToJsonAsync(Dialog dialog)` - Export DLG as JSON
- `IsValidDlgFile(string filePath)` - Validate file format
- `ValidateStructure(Dialog dialog)` - Validate dialog structure

**Usage**:
```csharp
var service = new DialogFileService();
var dialog = await service.LoadFromFileAsync("conversation.dlg");
await service.SaveToFileAsync(dialog, "output.dlg");
```

### DialogParser (Internal Implementation)
**Location**: `Parley/Parsers/DialogParser.cs` (3,945 lines)
**Access**: Internal (used by DialogFileService)
**Purpose**: GFF binary format parsing and Dialog object construction.

**Inheritance**: Extends `GffParser` (base GFF format handling)
**Implements**: `IDialogParser` interface

**Dependencies**:
- `GffIndexFixer` - Field index calculation and validation
- `DialogBuilder` - Converts GFF structs to Dialog objects
- `DialogWriter` - Converts Dialog objects to GFF binary

**Key Methods**:
- `ParseFromFileAsync()` - Entry point for file reading
- `ParseFromBufferAsync()` - Parse GFF binary buffer
- `WriteToFileAsync()` - Entry point for file writing
- `CreateDlgBuffer()` - Generate Aurora-compatible binary

### DialogBuilder (Model Construction)
**Location**: `Parley/Parsers/DialogBuilder.cs` (638 lines)
**Access**: Internal
**Purpose**: Converts GFF structs into Dialog/DialogNode/DialogPtr objects.

**Key Methods**:
- `BuildDialogFromGff()` - Main conversion entry point
- `BuildDialogNodeFromStruct()` - Create DialogNode from GFF struct
- `BuildStartPointer()` - Create start point pointers
- `ResolvePointers()` - Link pointers to target nodes

**Process**:
1. Read GFF structs from binary
2. Create DialogNode objects for entries and replies
3. Build DialogPtr objects for connections
4. Resolve pointer references to actual nodes
5. Return complete Dialog object

### DialogWriter (Binary Generation)
**Location**: `Parley/Parsers/DialogWriter.cs` (2,592 lines)
**Access**: Internal
**Purpose**: Converts Dialog objects into Aurora Engine GFF binary format.

**Key Methods**:
- `CreateDlgBuffer()` - Main binary generation entry point
- `ConvertDialogToGff()` - Convert Dialog to GFF components
- `WriteListIndices()` - Write connection lists (EntryList, ReplyList, StartingList, etc.)
- `CreateNodeStructs()` - Generate GFF structs for entries/replies
- `CreatePointerStructs()` - Generate GFF structs for connections

**Critical Sections**:
The `WriteListIndices()` method writes 7 distinct sections:
1. EntryList (root-level entry indices)
2. ReplyList (root-level reply indices)
3. StartingList (conversation start points)
4. Individual RepliesList for each entry
5. Individual EntriesList for each reply
6. ConditionParams for all pointers
7. ActionParams for all nodes

All 7 sections must be written for Aurora Engine compatibility.

### GffIndexFixer (Index Calculation)
**Location**: `Parley/Parsers/GffIndexFixer.cs` (176 lines)
**Access**: Internal
**Purpose**: Calculates field indices for GFF binary format.

**Key Methods**:
- `CalculateFieldIndices()` - Generate field index array
- `RecalculateOffsets()` - Update field data offsets

**Purpose**: GFF format requires a field indices section mapping fields to their locations. This class handles the complex 4:1 ratio requirement (4 bytes per field index).

### DialogValidator (Validation)
**Location**: `Parley/Services/DialogValidator.cs` (214 lines)
**Access**: Public
**Purpose**: Validates dialog structure and pointer integrity.

**Key Methods**:
- `ValidateStructure(Dialog dialog)` - Full structure validation
- `ValidatePointerIntegrity(Dialog dialog)` - Check pointer references
- `HasCircularReferences(Dialog dialog)` - Detect circular links
- `GetDialogStatistics(Dialog dialog)` - Generate stats

**Returns**: `ParserResult` object with success flag and warning list.

**Validation Checks**:
- All pointers reference valid nodes
- No circular references
- Node indices are valid
- Required fields are present

## Data Flow

### Reading DLG Files
```
File → DialogFileService.LoadFromFileAsync()
    → DialogParser.ParseFromFileAsync()
    → GffParser.ParseGffBuffer() [base class]
    → DialogBuilder.BuildDialogFromGff()
    → Dialog object
```

### Writing DLG Files
```
Dialog object → DialogFileService.SaveToFileAsync()
    → DialogParser.WriteToFileAsync()
    → DialogWriter.CreateDlgBuffer()
    → GffIndexFixer.CalculateFieldIndices()
    → DialogWriter.WriteListIndices()
    → Binary buffer → File
```

### Validation
```
Dialog object → DialogValidator.ValidateStructure()
    → Check pointer integrity
    → Check circular references
    → Return ParserResult
```

## GFF Binary Format

### Structure
GFF (Generic File Format) consists of:
- **Header**: File type, version
- **Structs**: Containers with type and field count
- **Fields**: Type, label, data
- **Labels**: String identifiers for fields
- **Field Data**: Complex data (strings, lists)
- **Field Indices**: Maps fields to data locations
- **List Indices**: Connection lists for conversation flow

### Critical Requirements
- Root struct must use Type = 0xFFFFFFFF
- Field indices must maintain 4:1 ratio (4 bytes per field)
- All 7 list sections must be present in ListIndices
- Entry-First struct ordering (entries, then replies, then starts)

## Class Relationships

```
DialogFileService (public API)
    ├── uses → DialogParser (internal)
    │           ├── extends → GffParser (base format)
    │           ├── uses → DialogBuilder (read)
    │           ├── uses → DialogWriter (write)
    │           └── uses → GffIndexFixer (indices)
    └── uses → DialogValidator (validation)
```

## Method Location Reference

### File Operations
- Open file: `DialogFileService.LoadFromFileAsync()` → `DialogParser.ParseFromFileAsync()`
- Save file: `DialogFileService.SaveToFileAsync()` → `DialogParser.WriteToFileAsync()`
- Validate: `DialogFileService.ValidateStructure()` → `DialogValidator.ValidateStructure()`

### GFF Reading
- Parse buffer: `DialogParser.ParseFromBufferAsync()` at line ~123
- Read structs: `GffParser.ReadStructs()` (base class)
- Build dialog: `DialogBuilder.BuildDialogFromGff()` at line ~24

### GFF Writing
- Create buffer: `DialogWriter.CreateDlgBuffer()` at line ~2932
- Write structs: `DialogWriter.ConvertDialogToGff()` at line ~99
- Write indices: `DialogWriter.WriteListIndices()` at line ~2039

### Validation
- Pointer check: `DialogValidator.ValidatePointerIntegrity()` at line ~73
- Circular ref: `DialogValidator.HasCircularReferences()` at line ~118
- Statistics: `DialogValidator.GetDialogStatistics()` at line ~155

## Data Models

### Dialog
**Location**: `Parley/Models/Dialog.cs`

**Properties**:
- `Entries` - List of entry nodes (NPC speech)
- `Replies` - List of reply nodes (PC speech)
- `Starts` - List of conversation start pointers
- `NumWords` - Word count estimate
- `DelayEntry` - Entry display delay
- `DelayReply` - Reply display delay
- `EndConversation` - End conversation script
- `EndConverAbort` - Abort conversation script
- `PreventZoomIn` - Camera zoom flag

### DialogNode
**Location**: `Parley/Models/DialogNode.cs`

**Properties**:
- `Text` - Localized text (LocString)
- `Speaker` - Creature tag
- `Script` - Conditional script
- `Pointers` - List of child connections
- `Animation` - Animation type
- `AnimLoop` - Animation loop flag
- `Sound` - Voice sound file
- `Quest` - Journal quest tag
- `Comment` - Developer comment
- `ActionParams` - Action script parameters
- `Delay` - Display delay

### DialogPtr
**Location**: `Parley/Models/DialogPtr.cs`

**Properties**:
- `Index` - Target node index
- `Active` - Conditional script
- `IsChild` - Link vs child flag
- `LinkComment` - Link comment
- `ConditionParams` - Conditional script parameters

## Testing

### Round-Trip Testing
The parser must preserve all data through read-write cycles:
```csharp
var original = await service.LoadFromFileAsync("original.dlg");
await service.SaveToFileAsync(original, "exported.dlg");
var exported = await service.LoadFromFileAsync("exported.dlg");
// exported must match original
```

### Validation Testing
```csharp
var dialog = await service.LoadFromFileAsync("test.dlg");
var result = service.ValidateStructure(dialog);
// result.Success == true, result.Warnings.Count == 0
```

### Aurora Compatibility Testing
Exported files must:
- Load in Aurora Conversation Editor
- Display correct tree structure
- Play correctly in Neverwinter Nights
- Preserve all scripts and parameters

## Logging

All parser operations log to `~/Parley/Logs/` via `UnifiedLogger`:
- File open/save operations
- GFF structure parsing
- Field index calculations
- Pointer resolution
- Validation results
- Errors and warnings

Log levels: DEBUG, INFO, WARNING, ERROR, CRITICAL
