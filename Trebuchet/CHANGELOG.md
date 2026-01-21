# Changelog - Trebuchet

All notable changes to Trebuchet (Radoub Launcher) will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

---

## [Unreleased]

---

## [1.2.0-alpha] - 2026-01-20
**Branch**: `trebuchet/issue-953` | **PR**: #TBD

### Feature: Module Editing (IFO Files)

- Add module editing capability for IFO (module info) files
- Load IFO files from modules (.mod) or directories
- Edit module properties (name, description, tag, etc.)
- Set/get module-level local variables
- Save changes back to IFO format

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
