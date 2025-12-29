# Changelog - Quartermaster

All notable changes to Quartermaster (Creature Editor) will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

---

## [Unreleased]

---

## [0.1.7-alpha] - 2025-12-28
**Branch**: `quartermaster/sprint/classes-levels-panel` | **PR**: #625

### Sprint: Classes & Levels Panel (#613)

Add the Classes & Levels panel displaying character class progression and alignment.

#### Added
- **Class Slots Display** - Shows active classes only (up to 8 per Beamdog EE)
  - Each class shows name, level, and hit die
  - "Add Class" placeholder button for future editing
  - Level +/- buttons as disabled placeholders for future editing
  - Total level calculation displayed
- **Alignment Display** - Shows character alignment with visual progress bars
  - Good-Evil axis with numeric value (0-100)
  - Lawful-Chaotic axis with numeric value (0-100)
  - Alignment name calculated from axis values (e.g., "Lawful Good", "True Neutral")
- **Auto-Levelup Package Display** - Shows the creature's StartingPackage
  - Package name resolved from packages.2da (hardcoded fallbacks)
- **Levelup Wizard Placeholder** - Disabled button for future levelup wizard sprint
- **Identity Section** - Race, gender, subrace, and deity display
- **CreatureDisplayService Integration** - ClassesPanel now uses 2DA/TLK lookups when available

#### Changed
- ClassesPanel uses CreatureDisplayService for class/race/gender name resolution
- Hit die display per class (d4-d12 based on class type)

---

## [0.1.6-alpha] - 2025-12-28
**Branch**: `quartermaster/sprint/stats-identity-panel` | **PR**: #622

### Sprint: Character Stats & Identity Panel (#612)

Add the Stats & Identity panel displaying character name, portrait, and core statistics.

#### Added
- **CreatureDisplayService** - Centralized service for resolving creature names from 2DA/TLK
  - Race name lookup from `racialtypes.2da` with TLK resolution
  - Gender name lookup from `gender.2da` with TLK resolution
  - Class name lookup from `classes.2da` with TLK resolution
  - Racial ability modifier lookup (StrAdjust, DexAdjust, etc.)
  - Hardcoded fallbacks for common races/genders/classes when 2DA not available
- **Base Attack Bonus calculation** from class levels
  - Reads `AttackBonusTable` from `classes.2da` for each class
  - Looks up BAB from `cls_atk_*.2da` at appropriate level
  - Sums BAB across multiclass levels
  - Falls back to estimated progression (full/3/4/half) when 2DA unavailable
- **Equipment attack bonus** from equipped items
  - Scans item properties for Enhancement Bonus (PropertyName 6) and Attack Bonus (PropertyName 56)
  - Enhancement bonuses don't stack (highest wins)
  - Attack bonuses stack
- **Feat name resolution** - FeatsPanel now uses `feat.2da` + TLK for feat names
  - High feat IDs (e.g., 1089) now resolve correctly instead of showing "Feat 1089"
  - Spell names for special abilities resolved via `spells.2da` + TLK
- **Navigation FlaUI tests** - Sidebar nav button tests for all 8 panels
- **Enhanced StatsPanel** - Complete overhaul with detailed stat breakdown
  - Ability scores: Base | Racial Modifier | Total | Bonus columns
  - Hit points: Base HP (dice rolls) | Max HP (with Con) | Current HP (with %)
  - Combat stats: Natural AC | Base Attack (with breakdown) | Speed | Challenge Rating
  - Saving throws: Base | Ability Modifier | Total columns
- **Sidebar character header** - Now shows resolved race/gender/class names via 2DA/TLK lookup

#### Fixed
- **Walk rate display** - Fixed creaturespeed.2da mapping (was off-by-one)
  - Row 0 = PC, Row 4 = Normal, etc. (not Row 0 = Immobile)

#### Changed
- MainWindow now uses `CreatureDisplayService` for character summary in sidebar
- MainWindow passes equipped items to StatsPanel for BAB calculation
- StatsPanel receives display service via `SetDisplayService()` method
- StatsPanel shows BAB breakdown: "(X base + Y equip)" when equipment contributes

---

## [0.1.5-alpha] - 2025-12-28
**Branch**: `quartermaster/sprint/app-shell-layout` | **PR**: #619

### Sprint: Application Shell & Layout Foundation (#618)

Establishes the Quartermaster application shell with a FlaUI-friendly sidebar + content layout that avoids tab controls.

#### Added
- **Sidebar navigation layout** - Replaced tab-based layout with sidebar + content pattern for FlaUI compatibility
- **Character header** - Portrait placeholder, character name, and race/class summary in sidebar
- **Navigation buttons** - Stats, Classes, Skills, Feats, Spells, Inventory, Advanced, Scripts sections
- **Content panel switching** - Single content area that swaps UserControls based on navigation selection
- **StatsPanel** - Basic stats display showing ability scores, combat stats, and saving throws
- **InventoryPanel** - Extracted inventory UI into standalone UserControl with equipment slots, backpack, and palette
- **PlaceholderPanel** - "Coming Soon" panels for unimplemented sections
- **AutomationIds** - FlaUI-friendly identifiers on all interactive elements (NavButton_*, ContentArea, CharacterName, etc.)
- **SidebarWidth setting** - Persisted sidebar width preference

#### Changed
- MainWindow layout refactored from 3-panel inventory view to sidebar + content pattern
- Settings now saves sidebar width instead of left panel width

---

## [0.1.4-alpha] - 2025-12-27
**Branch**: `quartermaster/fix/flaui-closes-vscode` | **PR**: #606

### Fix: FlaUI tests close VSCode instead of just the app (#593)

#### Fixed
- **StopApplication() window targeting** - Changed from `Alt+F4` keystroke to `App.Close()` method
  - Alt+F4 sends to focused window, which could be VSCode if test app lost focus
  - `App.Close()` sends WM_CLOSE directly to test process, ensuring correct targeting
  - Kept 200ms delay before close to prevent SkiaSharp render crashes

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
