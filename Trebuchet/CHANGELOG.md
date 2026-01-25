# Changelog - Trebuchet

All notable changes to Trebuchet (Radoub Launcher) will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

---

## [Unreleased]

---

## [1.6.0-alpha] - 2026-01-25
**Branch**: `trebuchet/issue-1116` | **PR**: #1117

### Feature: NWScript Compiler Integration (#1116)

---

## [1.5.0-alpha] - 2026-01-25
**Branch**: `trebuchet/issue-1092` | **PR**: #1107

### Feature: IFO Field Version Validation for Backward Compatibility (#1092)

- Version-aware field writing: Check `Mod_MinGameVer` before writing version-specific fields
- Validation on save: Warn user if NWN:EE features target older version
- Field documentation: Document which fields require which minimum version

---

## [1.4.0-alpha] - 2026-01-24
**Branch**: `trebuchet/issue-1095` | **PR**: #1102

### Sprint: IFO GUI & Module Management (#1095)

- [x] #1093 - Expose DefaultBic and new IFO fields in GUI
- [x] #1080 - Unpack module files (.mod) for editing
- [x] #1081 - Build/pack module from working directory

#### IFO Fields in GUI (#1093)
- Add DefaultBic field to Version tab (default character file for new players)
- Add StartMovie field to Version tab (startup movie to play)
- Add new "NWN:EE Scripts" tab with 6 extended event scripts:
  - OnModuleStart, OnPlayerChat, OnPlayerTarget
  - OnPlayerGuiEvent, OnPlayerTileAction, OnNuiEvent (1.80+)

#### Build/Pack Module (#1081)
- Add "Build" button to main window header (visible when unpacked working directory exists)
- Pack working directory into .mod file with automatic timestamped backup
- Build status shown in status bar with progress indicator
- Uses ErfWriter to create proper MOD archives with null-padded ResRefs

#### Bug Fix: ERF writer null-padding
- Fixed ErfWriter using space-padding instead of null-padding for ResRefs
- This was causing module.ifo to not be found after rebuild
- ErfReader now also trims trailing spaces for backward compatibility

#### Unpack Module (#1080)
- Add "Unpack" button in Module Editor header (visible for packed modules)
- Extract all resources from .mod file to `<modulename>/` directory
- Auto-reload in editable mode after unpacking
- Uses memory-efficient streaming (doesn't load entire MOD into RAM)

---

## [1.3.1-alpha] - 2026-01-24
**Branch**: `trebuchet/issue-1090` | **PR**: #1091

### Fix: Module editor corrupts module.ifo - missing area list (#1090)

**Critical bug**: When saving module.ifo changes in Trebuchet's Module Editor, the module becomes corrupted - Aurora Engine cannot find the start location.

**Root cause**: Field name case mismatch (`Mod_Area_List` vs `Mod_Area_list`) and conditional field writing dropped fields.

**Fixes (in Radoub.Formats)**:
- Fixed `Mod_Area_list` field name case (lowercase 'l' matches actual GFF field)
- Added 19 missing IFO fields to IfoFile model:
  - NWN:EE scripts: OnModuleStart, OnPlayerChat, OnPlayerTarget, OnPlayerGuiEvent, OnPlayerTileAction, OnNuiEvent
  - Metadata: IsSaveGame, StartMovie, DefaultBic, ModuleUuid, PartyControl
  - Lists: ExpansionList, CutSceneList, GlobalVarList
- Updated IfoWriter to always write all fields (including empty lists) for round-trip compatibility
- Added round-trip tests with real module.ifo files

---

## [1.3.0-alpha] - 2026-01-24
**Branch**: `trebuchet/issue-1061` | **PR**: #1077

### Sprint: Launch Workflow

#### Tool Launch with Recent Files (#1030)
- Add dropdown menu below each tool card for quick file access
- Display tool's MRU (Most Recently Used) files from settings
- Validate files exist before showing in menu
- "Launch App Only" option at top of dropdown
- Support PathHelper ~ expansion for Parley's contracted paths

#### Custom Module Browser (#1023)
- Add ModuleBrowserWindow to Radoub.UI for browsing .mod files
- Replace OS file picker with custom browser in Open Module
- Show module name and file size in list
- Search/filter modules by name
- Browse to alternate folders if needed
- Consistent UI with existing Script/Dialog browsers

#### Module Editor Improvements (#1038)
- Detect unpacked working directory for module editing
- If `<modulename>/module.ifo` exists alongside `.mod`, load from there (editable)
- If no unpacked directory, load from `.mod` file (read-only with status message)
- Add `CanEditModule` binding - Edit button disabled when no valid module selected

#### Services
- Add ToolRecentFilesService for reading tool MRU from settings JSON
- Add ToolFileLaunchInfo for passing file path with tool launch

#### Follow-up Issues Created
- #1080 - Unpack module files for editing
- #1081 - Build/pack module from working directory

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
