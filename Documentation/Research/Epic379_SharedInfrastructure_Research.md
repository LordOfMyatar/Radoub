# Research: Epic #379 - Shared Infrastructure

**Date**: 2025-12-14
**Issue**: #379
**Status**: Research Complete

## Summary

This epic extracts shared GFF parsing infrastructure from Parley to enable the multi-tool Radoub ecosystem. Research confirms that core GFF classes (GffStructures, GffBinaryReader, GffParser) are mostly generic and can be extracted with minimal refactoring. JRL (journal) files are GFF-based and can leverage the same parser.

## Key Findings

### 1. GFF Parser Analysis

#### Files to Extract (in `Parley/Parley/Parsers/`)

| File | Lines | Coupling | Extraction Difficulty |
|------|-------|----------|----------------------|
| `GffStructures.cs` | 181 | None | **TRIVIAL** - pure data models |
| `GffBinaryReader.cs` | 581 | GffStructures only | **EASY** - static methods |
| `GffParser.cs` | 406 | Uses UnifiedLogger | **EASY** - logger replaceable |
| `GffIndexFixer.cs` | 176 | Dialog/DialogNode types | **HARD** - Parley-specific |

#### Dependency Graph

```
GffStructures.cs (standalone)
    ↓
GffBinaryReader.cs (uses GffStructures)
    ↓
GffParser.cs (abstract base, uses both above)
    ↑
    ├─ DialogParser.cs (DLG files)
    ├─ CreatureParser.cs (UTC files)
    └─ ModuleInfoParser.cs (IFO files)
```

#### Key Classes

- **GffHeader**: 56-byte header (file type, version, offsets, counts)
- **GffStruct**: Container for fields, with type ID and data offset
- **GffField**: Named data with type (BYTE, INT, DWORD, CExoString, CExoLocString, List, Struct)
- **GffLabel**: Field names (16-byte max, multiple parsing strategies)
- **GffList**: Ordered collection of struct indices
- **CExoLocString**: Localized string with language/gender variants

### 2. Radoub.Formats Patterns

#### Existing Structure

```
Radoub.Formats/
├── Common/ResourceTypes.cs    # Shared constants
├── Key/KeyReader.cs, KeyFile.cs
├── Bif/BifReader.cs, BifFile.cs
├── Erf/ErfReader.cs, ErfFile.cs
├── Tlk/TlkReader.cs, TlkFile.cs
├── TwoDA/TwoDAReader.cs, TwoDAFile.cs
└── Resolver/GameResourceResolver.cs
```

#### Reader Pattern (established)

```csharp
public static class [Format]Reader
{
    public static [Format]File Read(string filePath);  // Path overload
    public static [Format]File Read(Stream stream);     // Stream overload
    public static [Format]File Read(byte[] buffer);     // Core implementation
}
```

#### Key Design Decisions

- Static reader classes (no instantiation)
- Three Read overloads (path → stream → buffer)
- InvalidDataException for parse errors with context
- Case-insensitive ResRef matching
- Reference BioWare specs + neverwinter.nim in comments

### 3. JRL File Format

#### Structure

JRL files are **100% GFF-compliant** with FileType = `"JRL "`:

```
JRL File
└── Categories (List)
    └── JournalCategory (Struct)
        ├── Tag (CExoString) - Quest identifier
        ├── Name (CExoLocString) - Localized quest name
        ├── Priority (DWORD) - 0=Highest to 4=Lowest
        ├── XP (DWORD) - Completion reward
        ├── Comment (CExoString) - Builder notes
        └── EntryList (List)
            └── JournalEntry (Struct)
                ├── ID (DWORD) - Entry number
                ├── Text (CExoLocString) - Journal text
                └── End (WORD) - Is completion endpoint
```

#### Existing Implementation

- `JournalService.cs` in Parley already parses JRL using `GffBinaryReader`
- `JournalStructures.cs` has data models
- Used for script parameter autocomplete

## Recommended Approach

### Sprint 1: Move GFF Parser to Radoub.Formats

**Scope**: Extract GffStructures + GffBinaryReader + GffParser base

**Steps**:
1. Create `Radoub.Formats/Gff/` directory
2. Copy GffStructures.cs → `GffFile.cs` (rename to match pattern)
3. Copy GffBinaryReader.cs → `GffReader.cs` (make static, three overloads)
4. Remove UnifiedLogger dependency (use exceptions instead)
5. Add comprehensive unit tests
6. **Gate**: Parley builds (still uses local copy)

**Risk**: Low - no coupling to Dialog types

### Sprint 2: Create JRL Reader/Writer

**Scope**: JrlReader + JrlWriter in Radoub.Formats

**Steps**:
1. Create `Radoub.Formats/Jrl/` directory
2. Implement JrlReader using GffReader internally
3. Create JrlFile model with Categories/Entries
4. Implement JrlWriter for round-trip
5. Add round-trip tests with real module JRL files
6. **Gate**: Parse official module JRLs correctly

**Risk**: Medium - writer needs careful GFF construction

### Sprint 3: Update Parley to Use Shared GFF

**Scope**: Point Parley at Radoub.Formats.Gff

**Steps**:
1. Add project reference to Radoub.Formats
2. Update `using` statements in parsers
3. Remove duplicate GFF code from Parley/Parsers/
4. Keep GffIndexFixer in Parley (Dialog-specific)
5. Full regression testing
6. **Gate**: All Parley tests pass + manual smoke test

**Risk**: Medium - need careful refactoring of parser inheritance

## Open Questions

1. **GffIndexFixer**: Should this move to Radoub.Formats with a generic interface, or stay in Parley as Dialog-specific logic?
   - **Recommendation**: Keep in Parley for now - it's tightly coupled to Dialog structure

2. **GffParser base class**: Move abstract base or keep format-specific parsers separate?
   - **Recommendation**: Start with static GffReader only (matches Radoub.Formats pattern), let each tool implement its own format-specific parser

3. **Writer implementation**: When to add GffWriter?
   - **Recommendation**: Add as part of JrlWriter sprint - JRL needs round-trip, and GffWriter is required

## Architecture Decision

**Recommended**: Static reader pattern (matches existing Radoub.Formats)

```csharp
// Radoub.Formats.Gff
public static class GffReader
{
    public static GffFile Read(string filePath);
    public static GffFile Read(Stream stream);
    public static GffFile Read(byte[] buffer);
}

public static class GffWriter
{
    public static byte[] Write(GffFile gff);
    public static void Write(GffFile gff, string filePath);
    public static void Write(GffFile gff, Stream stream);
}

// Radoub.Formats.Jrl (uses GffReader internally)
public static class JrlReader
{
    public static JrlFile Read(string filePath);
    // ... overloads
}
```

This keeps Parley's DialogParser/DialogBuilder/DialogWriter as Parley-specific code that uses the shared GffReader/GffWriter.

## Resources

- BioWare GFF Spec: `Documentation/BioWare_Markdown_Docs/Bioware_Aurora_GFF_Format.md`
- BioWare JRL Spec: `Documentation/BioWare_Markdown_Docs/Bioware_Aurora_Journal_Format.md`
- neverwinter.nim GFF: https://github.com/niv/neverwinter.nim/blob/master/neverwinter/gff.nim
- Existing Parley GFF: `Parley/Parley/Parsers/GffBinaryReader.cs`

## Code References

- GFF structures: [GffStructures.cs](Parley/Parley/Parsers/GffStructures.cs)
- GFF reader: [GffBinaryReader.cs](Parley/Parley/Parsers/GffBinaryReader.cs)
- GFF base parser: [GffParser.cs](Parley/Parley/Parsers/GffParser.cs)
- Journal service: [JournalService.cs](Parley/Parley/Services/JournalService.cs)
- Existing patterns: [KeyReader.cs](Radoub.Formats/Radoub.Formats/Key/KeyReader.cs)

---

🤖 Generated with [Claude Code](https://claude.ai/claude-code)
