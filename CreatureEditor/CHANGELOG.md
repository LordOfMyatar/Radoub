# Changelog - Quartermaster

All notable changes to Quartermaster (Creature Editor) will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

---

## [Unreleased]

---

## [0.1.4-alpha] - 2025-12-27
**Branch**: `quartermaster/fix/flaui-closes-vscode` | **PR**: #TBD

### Fix: FlaUI tests close VSCode instead of just the app (#593)

#### Fixed
- TBD - Investigation in progress

---

## [0.1.3-alpha] - 2025-12-26
**Branch**: `quartermaster/feat/inventory-display-580` | **PR**: #585

### Feature: Wire up Inventory Display and File Search (#580)

#### Added
- **Item palette population** - Loads items from module directory, Override folder, and BIF archives
- **Item filtering** - Filter panel wired to GameDataService for item type dropdown
- **GitHub workflows** - `quartermaster-pr-build.yml` and `quartermaster-pr-tests.yml`
- **External branding** - Window title, About dialog, CLI help, menus use "Quartermaster"
- **Equipment slot display** - Equipped items from BIC files now display in slots with name labels
- **Placeholder icons** - SVG icons from game-icons.net (CC BY 3.0) for equipment slots and item lists
- **ItemIconHelper** - Maps equipment slots and item types to placeholder icons

#### Changed
- PaletteList now displays filtered items from ItemFilterPanel
- ClearInventoryUI clears palette items and selection state
- Equipment slot control now shows slot name on top, item name on bottom
- Item lists (palette, backpack) now show item type icons

#### Fixed
- **BIC equipped items** - Fixed parsing of `EquippedRes` field (was using wrong field name)
- **BIC inventory items** - Fixed parsing of `InventoryRes` field for backpack items

#### Infrastructure
- Workflow parity with Parley for PR checks and CI
- Internal namespace remains `CreatureEditor` (like Parley/DialogEditor pattern)
- Added Avalonia.Svg.Skia package for SVG icon support

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

## [0.1.0-alpha] - 2025-12-26
**Branch**: `radoub/sprint/creature-editor-mvp` | **PR**: #578

### Sprint: Creature Editor MVP - Inventory Panel (#554)

Initial release of Quartermaster (Creature Editor).

#### Added
