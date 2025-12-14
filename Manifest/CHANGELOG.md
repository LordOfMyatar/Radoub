# Changelog - Manifest

All notable changes to Manifest will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

---

## [Unreleased]

---

## [0.2.0-alpha] - 2025-12-14
**Branch**: `manifest/sprint/file-io` | **PR**: #TBD

### Sprint: File I/O (#406)

Implement JRL file reading and writing with proper state management.

#### Added
- Open JRL from file system using JrlReader
- Display categories/entries in TreeView
- Save JRL to file using JrlWriter
- Dirty state tracking with title indicator (*)
- Unsaved changes warning on close

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
