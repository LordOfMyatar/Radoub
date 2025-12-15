# Changelog - Manifest

All notable changes to Manifest will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

---

## [Unreleased]

---

## [0.4.0-alpha] - 2025-12-14
**Branch**: `manifest/sprint/theme-support` | **PR**: #TBD

### Sprint: Theme Support (#390)

Implement theme support matching Parley's theme system.

#### Added
- Theme loading from `~/Radoub/Manifest/Themes/`
- Default themes (light, dark, fluent-light, vscode-dark)
- Accessibility themes (deuteranopia, protanopia, tritanopia)
- ThemeManager service
- Theme color/font application
- Scrollbar auto-hide preference
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
