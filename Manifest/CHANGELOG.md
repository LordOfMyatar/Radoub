# Changelog - Manifest

All notable changes to Manifest will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

---

## [Unreleased]

---

## [0.3.0-alpha] - 2025-12-14
**Branch**: `manifest/sprint/tlk-integration` | **PR**: #TBD

### Sprint: TLK Integration (#389)

Integrate TLK (Talk Table) support for resolving localized strings in journal files.

#### Added
- TLK loading (module custom TLK and game dialog.tlk fallback)
- StringRef resolution to display text
- Embedded string display
- Source indicator (TLK ref vs embedded) in UI
- Graceful handling of missing StringRef

#### Changed
- TBD

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
