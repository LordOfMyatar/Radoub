# Changelog - Quartermaster

All notable changes to Quartermaster (Creature Editor) will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

---

## [Unreleased]

---

## [0.1.3-alpha] - 2025-12-26
**Branch**: `quartermaster/feat/inventory-display-580` | **PR**: #TBD

### Feature: Wire up Inventory Display and File Search (#580)

#### Added
- GitHub workflows for Quartermaster (build + tests)
- External branding as "Quartermaster" (internal namespace remains CreatureEditor)

#### Infrastructure
- Workflow parity with Parley for PR checks and CI

---

## [0.1.2-alpha] - 2025-12-26
**Branch**: `radoub/sprint/creature-editor-cleanup` | **PR**: #584

### Sprint: CreatureEditor Cleanup (#582, #583)

Pre-emptive refactoring before codebase grows. Lessons from Parley's 2,400+ line MainWindow.

#### Refactoring (#582)
- **MainWindow.FileOps.cs** - Extracted file operations (Open/Save/Recent files)
- **MainWindow.Inventory.cs** - Extracted inventory population and UTI resolution
- **DialogHelper.cs** - Static helper for common dialogs (Unsaved/Error/About)
- MainWindow.axaml.cs reduced from 892 to 466 lines (48% reduction)

#### Testing (#583)
- **CreatureEditor.Tests project** - New xUnit test project (21 tests)
- **CommandLineServiceTests** - 9 tests for argument parsing
- **SettingsServiceTests** - 12 tests for property constraints and defaults
- FlaUI integration smoke tests added to Radoub.IntegrationTests

---

## [0.1.1-alpha] - 2025-12-26
**Branch**: `radoub/feat/uti-bif-loading` | **PR**: #581

### Feature: Load UTI Items from BIF Archives (#579)

#### Added
- **GameDataService integration** - Enables BIF/Override/TLK lookups for base game items
- **UTI loading from BIF archives** - Base game items (e.g., `nw_it_torch001`) now load full data
- **ItemViewModelFactory usage** - Proper name resolution via baseitems.2da and TLK strings
- **Resource resolution order**: Module directory → Override → HAK → BIF archives

#### Changed
- `CreatePlaceholderItem` now uses GameDataService for BIF lookups when module file not found
- Item display names resolved via TLK instead of showing ResRef placeholders
- Base item types resolved via baseitems.2da lookups

---

## [0.1.0-alpha] - 2025-12-26
**Branch**: `radoub/sprint/creature-editor-mvp` | **PR**: #578

### Sprint: Creature Editor MVP - Inventory Panel (#554)

Initial release of Quartermaster (Creature Editor).

#### Added
- **Project scaffold** - Avalonia UI application with theming and logging
- **MainWindow** - 3-panel layout with Equipment, Backpack, and Palette sections
- **File operations** - Open/Save/Recent Files for UTC and BIC files
- **Inventory display** - DataGrid shows backpack items from creature
- **UTI loading** - Loads item data from module directory
- **Equipment slots panel** - Visual display of 14 standard + 4 natural equipment slots
- **Command line support** - `--file` argument for opening files at startup
- **Settings persistence** - Window position, recent files, theme preferences

#### Known Limitations
- Equipment slots not visually populated from creature data
- Item palette not populated
- No item editing (view-only)

---
