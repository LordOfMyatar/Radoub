# Changelog - Radoub

All notable changes to Radoub (repository-level) will be documented in this file.

For tool-specific changes, see the individual tool changelogs:
- [Parley CHANGELOG](Parley/CHANGELOG.md)

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

---

## [Unreleased]

---

## [0.4.0] - TBD
**Branch**: `radoub/feat/issue-170-formats-library-tlk` | **PR**: #209

### Epic #170: Aurora Game Resource Reading Infrastructure (Phase 3 - TLK)

TLK (Talk Table) file reading support for Aurora Engine localization.

**Added**:
- `TlkFile` - Data model for TLK (Talk Table) files
- `TlkEntry` - Entry model with text, sound ResRef, and duration
- `TlkReader` - Parser for TLK format files
- Flag support: HasText (0x1), HasSound (0x2), HasSoundLength (0x4)
- Legacy artifact stripping (0xC0 bytes from old editors)
- Windows-1252 encoding support for NWN text
- 23 unit tests for TLK reading

---

## [0.3.0] - TBD
**Branch**: `radoub/feat/issue-90-gui-testing` | **PR**: #204

### Issue #90: Automated GUI Testing Infrastructure

FlaUI-based GUI testing framework for Radoub tools.

**Added**:
- `Radoub.UITests` project - Shared GUI test infrastructure
- FlaUI integration for Windows desktop testing (no external dependencies)
- Basic Parley launch smoke tests

---

## [0.2.0] - 2025-11-27
**Branch**: `radoub/feat/issue-170-erf-hak` | **PR**: #201

### Epic #170: Aurora Game Resource Reading Infrastructure (Phase 2)

ERF/HAK file reading support for Aurora Engine resource archives.

**Added**:
- `ErfFile` - Data model for ERF, HAK, MOD, SAV, NWM files
- `ErfReader` - Parser for ERF format files
- `ErfLocalizedString` - Localized description string support
- `ErfResourceEntry` - Resource metadata with offset/size tracking
- `ExtractResource()` - Extract individual resources from ERF archives
- 22 unit tests for ERF/HAK reading

---

### Documentation (PR #183)

Split large BioWare format documentation into chapter-based files for easier reading and reference.

**Creature Format** (`Documentation/BioWare_Markdown_Docs/Creature_Format/`):
- Ch1_Introduction.md
- Ch2_Creature_Struct.md
- Ch3_Calculations_and_Procedures.md
- Ch5_Creature_Related_2DA_Files.md
- README.md (index)

**Item Format** (`Documentation/BioWare_Markdown_Docs/Item_Format/`):
- Ch1_Introduction.md
- Ch2_Item_Struct.md
- Ch3_InventoryObject_Struct.md
- Ch4_Calculations_and_Procedures.md
- Ch5_Item_Related_2DA_Files.md
- README.md (index)

Original complete documents preserved.

### Sprint Planning Agent

Added `/sprint-plan` slash command (`.claude/commands/sprint-plan.md`) for automated sprint planning analysis.

---
