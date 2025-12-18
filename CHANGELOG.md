# Changelog - Radoub

All notable changes to Radoub (repository-level) will be documented in this file.

For tool-specific changes, see the individual tool changelogs:
- [Parley CHANGELOG](Parley/CHANGELOG.md)
- [Manifest CHANGELOG](Manifest/CHANGELOG.md)

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

---

## [Unreleased]

---

## [0.8.3] - TBD
**Branch**: `radoub/feat/bundle-package` | **PR**: #TBD

### Feat: Bundle Parley and Manifest into Unified Package (#448)

Create combined release package with shared runtime and dependencies.

#### Added
- Bundled package build configuration
- Shared runtime and Avalonia dependencies (~670 MB saved)
- Combined release artifact

#### Notes
- CEF (plugin WebView) remains separate/optional for plugin developers
- Individual tool packages still available for standalone installation

---

## [0.8.2] - 2025-12-16
**Branch**: `radoub/fix/linux-hak-and-script-preview` | **PR**: #440

### Fix: Linux HAK Crash and Cross-Platform Compatibility

Fixes critical Linux issues discovered during cross-platform testing.

#### Fixed
- **ErfReader integer overflow**: Added overflow validation when casting `uint32` offsets to `int`
  - Prevents crashes when reading large ERF/HAK files (>2.1GB offsets)
  - Uses `long` arithmetic to detect overflow before casting
- **GameResourceResolver case-sensitive file lookup**: Fixed override folder lookup on Linux
  - `Directory.GetFiles` pattern matching is case-sensitive on Linux
  - Now uses `Directory.EnumerateFiles` with case-insensitive LINQ comparison
- **Bare exception handlers**: Replaced silent `catch` blocks with proper `catch (Exception ex)` and `Debug.WriteLine` logging

#### Changed
- CI workflows now run tests on both Windows and Linux (ubuntu-latest)
- Test artifacts named per-platform (`test-results-windows-latest`, `test-results-ubuntu-latest`)

---

## [0.8.1] - 2025-12-15
**Branch**: `radoub/docs/dev-docs-to-wiki` | **PR**: #433

### Docs: Move Developer Documentation to Wiki (#372, #424, #213)

Migrate developer documentation from repo to GitHub Wiki.

#### Added
- [Radoub.Formats wiki documentation](https://github.com/LordOfMyatar/Radoub/wiki/Radoub-Formats) (#213)
  - [GFF Format](https://github.com/LordOfMyatar/Radoub/wiki/Radoub-Formats-GFF)
  - [KEY Format](https://github.com/LordOfMyatar/Radoub/wiki/Radoub-Formats-KEY)
  - [BIF Format](https://github.com/LordOfMyatar/Radoub/wiki/Radoub-Formats-BIF)
  - [ERF Format](https://github.com/LordOfMyatar/Radoub/wiki/Radoub-Formats-ERF)
  - [TLK Format](https://github.com/LordOfMyatar/Radoub/wiki/Radoub-Formats-TLK)
  - [2DA Format](https://github.com/LordOfMyatar/Radoub/wiki/Radoub-Formats-2DA)
  - [JRL Format](https://github.com/LordOfMyatar/Radoub/wiki/Radoub-Formats-JRL)
- [Manifest CODE_PATH_MAP](https://github.com/LordOfMyatar/Radoub/wiki/Manifest-Developer-Architecture) (#424)
- [Script-Parameters](https://github.com/LordOfMyatar/Radoub/wiki/Script-Parameters) feature documentation

#### Changed
- Developer docs moved to wiki (verified against current code):
  - [Parley-Developer-Architecture](https://github.com/LordOfMyatar/Radoub/wiki/Parley-Developer-Architecture)
  - [Parley-Developer-Testing](https://github.com/LordOfMyatar/Radoub/wiki/Parley-Developer-Testing)
  - [Parley-Developer-Delete-Behavior](https://github.com/LordOfMyatar/Radoub/wiki/Parley-Developer-Delete-Behavior)
  - [Parley-Developer-CopyPaste](https://github.com/LordOfMyatar/Radoub/wiki/Parley-Developer-CopyPaste)
  - [Parley-Developer-Scrap-System](https://github.com/LordOfMyatar/Radoub/wiki/Parley-Developer-Scrap-System)
  - [Manifest-Developer-Architecture](https://github.com/LordOfMyatar/Radoub/wiki/Manifest-Developer-Architecture)
- NonPublic folder moved to Radoub root with tool hierarchy
- Research folder moved to NonPublic/Research
- Parley README simplified (full docs in wiki)
- Pre-merge command updated with wiki pages and freshness dates

#### Removed
- `Parley/Documentation/Developer/` - Migrated to wiki
- `Documentation/Research/` - Moved to NonPublic

---

## [0.8.0] - 2025-12-14

### Added
- **Manifest** tool - JRL (journal) editor for Neverwinter Nights
  - See [Manifest CHANGELOG](Manifest/CHANGELOG.md) for tool-specific changes

---

## [0.7.0] - 2025-12-14
**Branch**: `radoub/sprint/jrl-reader` | **PR**: #401

### Sprint: Create JRL Reader/Writer (#397)

Create JRL (journal) reader and writer in Radoub.Formats using the shared GFF parser.

#### Added
- `Radoub.Formats.Jrl` namespace
- `JrlReader` - Static JRL parser wrapping GffReader
- `JrlWriter` - Static JRL writer for round-trip
- `JrlFile` - JRL data models (JournalCategory, JournalEntry)
- JRL round-trip tests

---

## [0.6.0] - 2025-12-14
**Branch**: `radoub/sprint/gff-parser` | **PR**: #399

### Sprint: Move GFF Parser to Radoub.Formats (#396)

Extract static GFF parsing code from Parley to shared library.

#### Added
- `Radoub.Formats.Gff` namespace
- `GffReader` - Static GFF binary parser (3 overloads: path, stream, buffer)
- `GffWriter` - Static GFF binary writer
- `GffFile` - GFF data models (GffStruct, GffField, GffLabel, GffList, CExoLocString)
- GFF round-trip tests

#### Research
- Epic379_SharedInfrastructure_Research.md (moved to NonPublic/Research/)

---


## [0.5.4] - 2025-12-13
**Branch**: `radoub/feat/github-projects-sync` | **PR**: #366

### Enhancement: GitHub Projects CLI Integration (#360)

Integrate GitHub Projects with Claude Code slash commands for automatic project board updates.

#### Added
- `/grooming` - Add groomed issues to appropriate project board
- `/sprint-planning` - Add sprint issues to project and mark in-progress
- `/init-item` - Add issue to project when branch initialized, mark in-progress
- `.claude/github-projects-reference.md` - Quick reference for project IDs and field details
- Documentation for GitHub CLI project scope (`gh auth refresh -s project`)

---

## [0.5.3] - 2025-12-13
**Branch**: `radoub/feat/sprint-issue-creation` | **PR**: #361

### Enhancement: Sprint Planning Issue Creation (#359)

Update `/sprint-planning` command to optionally create GitHub issues for planned sprints.

#### Added
- Sprint issue creation workflow after planning completes
- User confirmation before creating issues
- Automatic `sprint` and tool labels on created issues
- Parent epic linking in sprint issue body
- Work items checklist in sprint body
- Support for creating multiple sprint issues at once

---

## [0.5.2] - 2025-12-13
**Branch**: `radoub/feat/grooming-command` | **PR**: #358

### Feat: /grooming Slash Command (#357)

Added `/grooming` command to review and format open issues.

---

## [0.5.1] - 2025-12-08
**Branch**: `radoub/chore/dependabot-updates-309` | **PR**: #310

### Dependencies

Updated GitHub Actions and NuGet packages (closes #309):
- `gittools/actions` 3 → 4 (GitVersion workflow)
- `actions/checkout` 4 → 6
- `actions/download-artifact` 4 → 6
- `Google.Protobuf` 3.29.3 → 3.33.2 (gRPC plugin communication)

### Added
- Dependabot configuration for automated dependency updates (NuGet + GitHub Actions)

### Fixed
- GitVersion.yml updated to v6.x format (replaced deprecated `tag` with `label`, `is-mainline` with `is-main-branch`, updated `prevent-increment` syntax)
- Release workflow: Build project instead of solution to support RuntimeIdentifier for platform-specific packages
- macOS ARM64: Use RuntimeInformation for ARM64 WebView package selection

---

## [0.5.0] - 2025-11-28
**Branch**: `radoub/sprint/game-file-formats` | **PR**: #214

### Sprint: Game File Formats Integration

GameResourceResolver unified API for Aurora Engine resource resolution.

**Added**:
- `GameResourceConfig` - Configuration for game paths (NWN:EE and Classic factories)
- `GameResourceResolver` - Unified resource lookup across Override/HAK/BIF sources
- `ResourceResult` - Resource data with source tracking (Override, Hak, Bif)
- `ResourceInfo` - Lightweight resource metadata for listings
- TLK string resolution with custom TLK support (StrRef >= 0x1000000)
- NWN:EE and Classic path conventions via factory methods
- Archive caching for performance
- 21 unit tests for GameResourceResolver

---

## [0.4.0] - 2025-11-27
**Branch**: `radoub/feat/issue-170-formats-library-tlk` | **PR**: #209

### Epic #170: Aurora Game Resource Reading Infrastructure (Phase 3 - TLK & 2DA)

TLK (Talk Table) and 2DA (Two-Dimensional Array) file reading support.

**Added - TLK**:
- `TlkFile` - Data model for TLK (Talk Table) files
- `TlkEntry` - Entry model with text, sound ResRef, and duration
- `TlkReader` - Parser for TLK format files
- Flag support: HasText (0x1), HasSound (0x2), HasSoundLength (0x4)
- Legacy artifact stripping (0xC0 bytes from old editors)
- Windows-1252 encoding support for NWN text
- 23 unit tests for TLK reading

**Added - 2DA**:
- `TwoDAFile` - Data model for 2DA game data tables
- `TwoDARow` - Row model with label and cell values
- `TwoDAReader` - Parser for 2DA format (text-based)
- DEFAULT value support for missing cells
- Quoted string handling for values with spaces
- Empty cell (****) support
- Case-insensitive column lookup
- 26 unit tests for 2DA reading

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
