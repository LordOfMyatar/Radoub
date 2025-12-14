# Research: Manifest Core Epic #380

**Date**: 2025-12-14
**Issue**: #380
**Status**: Research Complete

## Summary

Manifest is a standalone journal editor for NWN `.jrl` files. The infrastructure is ready: `Radoub.Formats.Jrl` provides read/write capability (validated with 16 tests). The application shell follows DialogEditor patterns for consistent UX.

## Key Findings

### Shared Library Ready

`Radoub.Formats.Jrl` (created in Epic #379) provides:
- `JrlReader.Read(path|stream|buffer)` - Parse JRL files
- `JrlWriter.Write(jrl)` - Write JRL files
- `JrlFile`, `JournalCategory`, `JournalEntry`, `JrlLocString` - Data models
- Full round-trip tested (16 tests pass)

**No additional library work needed for core functionality.**

### DialogEditor Patterns to Reuse

| Component | DialogEditor Location | Manifest Adaptation |
|-----------|----------------------|---------------------|
| Settings Service | `Services/SettingsService.cs` | Copy, change paths to `~/Radoub/Manifest/` |
| Unified Logger | `Services/UnifiedLogger.cs` | Copy, change paths to `~/Radoub/Manifest/Logs/` |
| Theme System | `Themes/*.axaml` | Reuse directly (shared Radoub themes) |
| Recent Files | SettingsService | Same pattern |
| Dirty State | `MainWindow.axaml.cs` | Same pattern |

### Project Structure

```
Manifest/
├── Manifest/
│   ├── App.axaml(.cs)
│   ├── Program.cs
│   ├── Manifest.csproj
│   ├── Models/
│   │   └── ViewModels.cs        # Category/Entry view models
│   ├── Services/
│   │   ├── SettingsService.cs   # Adapted from DialogEditor
│   │   └── UnifiedLogger.cs     # Adapted from DialogEditor
│   ├── Views/
│   │   ├── MainWindow.axaml(.cs)
│   │   └── SettingsWindow.axaml(.cs)
│   └── Themes/                   # Link to shared Radoub themes
├── Manifest.Tests/
│   └── ...
├── CHANGELOG.md
├── CLAUDE.md
└── README.md
```

### UI Design

Based on BioWare's original Journal Editor:

```
┌────────────────────────────────────────────────────────────────┐
│ File  Edit  Help                                               │
├────────────────────────────────────────────────────────────────┤
│ [+Category] [+Entry] [Delete] [Save]                           │
├────────────────────────────────────────────────────────────────┤
│ ▼ Main Quest                           │                       │
│   ├─ [1] Find the artifact             │                       │
│   ├─ [2] Return to village             │                       │
│   └─ [10] Quest complete ✓             │                       │
│ ▼ Side Quest A                         │                       │
│   └─ [1] Talk to merchant              │                       │
│ ▶ Side Quest B                         │                       │
├────────────────────────────────────────┼───────────────────────┤
│ Properties: Entry [2]                                          │
│                                                                │
│ ID:    [2      ]  ☐ Finish Category                            │
│ Text:  ┌────────────────────────────────────────────────┐      │
│        │ Return to the village elder with the artifact. │      │
│        │                                                │      │
│        └────────────────────────────────────────────────┘      │
│                                                                │
│                               [Apply] [OK] [Cancel]            │
└────────────────────────────────────────────────────────────────┘
```

### Sprint Breakdown

**Sprint 1: Application Shell** (Foundation)
- Project structure mirroring DialogEditor
- SettingsService adapted for `~/Radoub/Manifest/`
- UnifiedLogger adapted for `~/Radoub/Manifest/Logs/`
- Basic MainWindow with placeholder tree
- File > Open/Save dialogs
- Effort: Medium

**Sprint 2: File I/O** (Critical Path)
- Open JRL via JrlReader
- Display categories/entries in TreeView
- Save JRL via JrlWriter
- Dirty state tracking
- Effort: Medium

**Sprint 3: Category Editing**
- Category property panel (Name, Tag, Comment, Priority, XP)
- Add/Delete category
- Category reordering
- Effort: Medium

**Sprint 4: Entry Editing**
- Entry property panel (ID, Text, End flag)
- Add/Delete entry
- Entry reordering
- Auto-increment ID for new entries
- Effort: Medium

## Recommendation

**Approach**: Start with Sprint 1 (Application Shell) to establish foundation.

**Key Decisions**:
1. Use `Radoub.Formats.Jrl` types directly (no conversion like DialogEditor does)
2. Shared theme system with DialogEditor (both reference same theme files)
3. Separate settings file (`ManifestSettings.json`) from DialogEditor

**Dependencies**:
- Epic #379 complete ✅ (JRL reader/writer ready)
- No external library dependencies beyond Avalonia

## Open Questions

1. **Shared services**: Should Logger/Settings be extracted to `Radoub.Common` for reuse?
   - Pro: Less code duplication
   - Con: Adds complexity, coupling between apps
   - Recommendation: Copy for now, extract if a third tool is added

2. **Theme sharing**: Link to DialogEditor themes or copy?
   - Recommendation: Both apps reference `Radoub/Themes/` shared directory

3. **StrRef/TLK support**: Issue #403 tracks adding TLK lookup
   - Not blocking for MVP - can display embedded strings only

## Resources

- `Radoub.Formats.Jrl/` - JRL reader/writer (source)
- `Parley/Parley/Services/` - Settings, Logger patterns
- BioWare GFF Spec - `Documentation/Bioware_Aurora_GFF.md`

## Code References

- [JrlFile.cs](../../Radoub.Formats/Radoub.Formats/Jrl/JrlFile.cs) - Data models
- [JrlReader.cs](../../Radoub.Formats/Radoub.Formats/Jrl/JrlReader.cs) - Reader implementation
- [JrlWriter.cs](../../Radoub.Formats/Radoub.Formats/Jrl/JrlWriter.cs) - Writer implementation
- [SettingsService.cs](../../Parley/Parley/Services/SettingsService.cs) - Pattern to adapt
- [UnifiedLogger.cs](../../Parley/Parley/Services/UnifiedLogger.cs) - Pattern to adapt
