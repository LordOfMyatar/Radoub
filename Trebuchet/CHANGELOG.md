# Changelog - Trebuchet

All notable changes to Trebuchet (Radoub Launcher) will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

---

## [Unreleased]

---

## [1.2.0-alpha] - 2026-01-20
**Branch**: `trebuchet/issue-953` | **PR**: #1037

### Feature: Module Editing (IFO Files)

#### Module Editor UI
- Add "Edit..." button in Trebuchet header to open module editor
- Tabbed interface for organized editing:
  - **Metadata**: Module name, description, tag, custom TLK
  - **Version**: Minimum game version, expansion requirements, XP scale
  - **HAK Files**: Ordered list with add/remove/reorder controls
  - **Time**: Dawn/dusk hours, minutes per hour, start date/time
  - **Entry Point**: Starting area dropdown, X/Y/Z coordinates
  - **Scripts**: All 16 module event scripts
  - **Variables**: Add/edit/remove module-level local variables

#### Infrastructure (Radoub.Formats)
- Add `ErfWriter` for writing/updating MOD files with backup support
- Add `IfoFile` model class with typed accessors for all IFO fields
- Add `IfoReader` and `IfoWriter` for GFF-based IFO parsing/writing
- Integrate with existing `VarTableHelper` for module variable support

#### Features
- Load IFO from extracted module directories or packed .mod files
- Automatic backup creation before modifying MOD files
- Progress feedback during load/save operations
- Non-modal editor window (can use Trebuchet while editing)

---

## [1.1.0-alpha] - 2026-01-20
**Branch**: `trebuchet/issue-951` | **PR**: #1025

### Feature: Launch NWN:EE with module for testing

- Add ability to launch NWN:EE directly from Trebuchet
- **Test Module**: Launch with `+TestNewModule` (auto-selects first character)
- **Load Module**: Launch with `+LoadNewModule` (shows character select)
- Accelerate edit-test-fix cycle for module development

---

## [1.0.0-alpha] - 2026-01-17
**Branch**: `radoub/issue-907` | **PR**: #939

### Initial Release

- **Tool Launcher**: Discover and launch Parley, Manifest, Quartermaster, Fence
- **Tool Discovery**: Auto-detect installed tools from RadoubSettings, same directory, or common paths
- **Theme Support**: Light and dark themes with accessibility-aware font sizing
- **Recent Modules**: Track and quick-launch recently opened modules
- **Status Display**: Show game installation and TLK configuration status
- **Cross-Platform**: Windows, macOS, Linux compatible

---
