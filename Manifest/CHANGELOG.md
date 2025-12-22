# Changelog - Manifest

All notable changes to Manifest will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

---

## [Unreleased]

---

## [0.8.0-alpha] - TBD
**Branch**: `manifest/sprint/cli-ui-polish` | **PR**: #493 | **Closes**: #447, #423

### Sprint: CLI Arguments + UI Polish

Add command-line argument support for cross-tool integration and improve journal entry layout.

#### Planned
- Command-line arguments for opening to specific file/quest/entry (#447)
- Journal entry layout improvements (#423)

---

## [0.7.0-alpha] - 2025-12-14
**Branch**: `manifest/feat/settings-ui` | **PR**: #422

### Feature: Settings UI (#421)

Add game path configuration to Settings UI for TLK resolution.

#### Added
- Game Paths section in Preferences window:
  - Base Game Installation (Steam/GOG) - for TLK files in data\ folder
  - User Documents path - for modules, override, custom TLKs
- Folder browser dialogs for manual path selection
- Auto-detect buttons for both paths
- Path validation with status messages
- Edit > Preferences menu entry (Ctrl+,)

---

## [0.6.0-alpha] - 2025-12-14
**Branch**: `manifest/sprint/testing-suite` | **PR**: #420

### Sprint: Testing Suite (#392)

Comprehensive testing for Manifest: unit tests, integration tests, and regression validation.

#### Added
- `Manifest.Tests` project with xUnit and Avalonia.Headless.XUnit
- JRL round-trip tests (empty, single category, multiple categories, edge cases)
- Headless UI tests for create/delete operations
- Test data generator for creating test JRL files
- Real module JRL file as test fixture

#### Test Coverage
- 24 Manifest.Tests tests (all pass)
- 461 Parley.Tests tests (all pass - regression check)
- 165 Radoub.Formats.Tests (all pass - shared library)

#### CI/CD (#413)
- GitHub release workflow (`manifest-release.yml`) - triggers on `manifest-v*` tags
- PR build check workflow (`manifest-pr-build.yml`)
- PR test workflow (`manifest-pr-tests.yml`)
- Cross-platform builds (Windows, macOS, Linux)
- Self-contained and framework-dependent variants

---

## [0.5.0-alpha] - 2025-12-15
**Branch**: `manifest/sprint/ui-polish` | **PR**: #417

### Sprint: UI Polish (#391)

Polish Manifest UI to match BioWare's original journal editor and provide professional UX.

#### Added
- Toolbar with emoji icons (Open, Save, Category, Entry, Delete)
- Keyboard shortcuts (Ctrl+O, Ctrl+S, Ctrl+Shift+N, Ctrl+E, Delete, F1)
- Window state persistence (position, size, maximized, splitter width)
- Status bar with category/entry counts and file path
- Tree view context menu

#### Fixed
- New categories now get unique tags (new_category, new_category_001, etc.)

---

## [0.4.0-alpha] - 2025-12-14
**Branch**: `manifest/sprint/theme-support` | **PR**: #415

### Sprint: Theme Support (#390)

Implement theme support matching Parley's theme system.

#### Added
- Theme loading from `~/Radoub/Manifest/Themes/`
- Default themes (light, dark, fluent-light, vscode-dark)
- Accessibility themes (deuteranopia, protanopia, tritanopia)
- ThemeManager service
- Theme color/font application
- Scrollbars always visible (no auto-hide)
- Theme selection persisted in ManifestSettings.json
- Runtime theme switching

---

## [0.3.0-alpha] - 2025-12-14
**Branch**: `manifest/sprint/tlk-integration` | **PR**: #411

### Sprint: TLK Integration (#389)

Integrate TLK (Talk Table) support for resolving localized strings in journal files.

#### Added
- `TlkService` for loading and resolving TLK strings across multiple languages
- Language dropdown in property panel to view text in different languages
- "View All..." button to see all available translations for a string
- Source indicator showing string origin (Embedded, TLK:N, or both)
- Graceful handling of missing StrRef ("not found" / "no game path" indicators)

#### Radoub.Formats (Shared Library)
- `Language` enum with BioWare language IDs and helper utilities
- `RadoubSettings` service for shared game path and TLK settings (`~/Radoub/RadoubSettings.json`)
- `ResourcePathDetector` for cross-platform game installation detection
- Auto-detect Steam/GOG game installation paths

#### Changed
- Property panel now uses TlkService to resolve and display localized strings
- SettingsService now references shared RadoubSettings for game paths

#### Notes
- Created #412 for Parley migration to shared settings

---

## [0.2.0-alpha] - 2025-12-14
**Branch**: `manifest/sprint/file-io` | **PR**: #410

### Sprint: File I/O (#406)

Implement JRL file reading and writing with proper state management.

#### Added
- Open JRL from file system using JrlReader
- Open JRL from module folder (File > Open from Module, Ctrl+M)
- Display categories/entries in TreeView
- Save JRL to file using JrlWriter
- Dirty state tracking with title indicator (*)
- Unsaved changes warning on close
- Property editing for categories (tag, name, priority, XP, comment)
- Property editing for entries (ID, end flag, text)
- Live tree view updates when properties change
- StrRef display for TLK references (category names and entry text)
- Priority dropdown (Highest/High/Medium/Low/Lowest)
- Full file path in title bar with ~ for home directory
- Auto-focus on name/text field when adding new categories/entries
- Entry IDs increment by 100 (allows inserting between existing entries)

---

## [0.1.0-alpha] - 2025-12-14
**Branch**: `manifest/sprint/app-shell` | **PR**: #409

### Sprint: Application Shell (#405)

Create foundational Manifest application structure and services.

#### Added
- Manifest project structure (mirroring Parley layout)
- Basic Avalonia MainWindow with tree view and property panel
- Simplified menu bar (File: Open/Save/Exit, Edit: Add Category/Entry/Delete, Help)
- Settings service: `~/Radoub/Manifest/ManifestSettings.json`
- Logging service: `~/Radoub/Manifest/Logs/`
- File open/save dialogs for JRL files

#### Notes
- No "New" or "Recent Files" - modules have exactly one JRL file

---
