# Changelog - Fence

All notable changes to Fence (Merchant Editor) will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

---

## [Unreleased]

---

## [0.1.19-alpha] - 2026-03-15
**Branch**: `fence/issue-1694` | **PR**: #1698

### Sprint: Core Bug Fixes

- [x] #1568 - PaletteCacheService tests fail (LoadCache_NoCache, GetCacheInfo_NoCache) — verified fixed by prior refactoring (all 7 tests pass)
- [x] #1536 - No dirty flag or save prompt when closing with unsaved changes — added TextChanged/CheckChanged/SelectionChanged tracking for all store property fields with IsLoading guard during population

---

## [0.1.18-alpha] - 2026-03-15
**Branch**: `fence/issue-1693` | **PR**: #1696

### Sprint: Store Inventory UX

- [x] #1662 - Add MinWidth=150 to Name columns on StoreInventoryGrid and ItemPaletteGrid to prevent collapse during resize

---

## [0.1.17-alpha] - 2026-03-11
**Branch**: `fence/issue-1634` | **PR**: #1661

### Sprint: Fence Tech Debt Cleanup

- [x] #1634 - Refactor hardcoded weapon/armor type names in StoreOperations to use BaseItemTypeService (reads StorePanel column from baseitems.2da)
- [x] #1635 - Add cancellation tokens to fire-and-forget palette tasks (CancellationTokenSource on window lifecycle)
- [x] #1637 - Standardize Task type syntax and replace inline error dialog with DialogHelper.ShowError

---

## [0.1.16-alpha] - 2026-02-27
**Branch**: `radoub/issue-1560` | **PR**: #1561

### TDD Audit Followup (#1560)

- [x] BaseItemTypeService tested with MockGameDataService — 2DA loading, TLK resolution, garbage filtering, unconfigured fallback (14 new tests)
- [x] ItemResolutionService tested with MockGameDataService — configured vs unconfigured paths, caching, resolution (13 new tests)

---

## [0.1.15-alpha] - 2026-02-27
**Branch**: `radoub/issue-1555` | **PR**: #1559

### Sprint: Manifest & Fence Test Coverage + Anti-Pattern Cleanup (#1555)

- [x] #1551 - Add CommandLineService tests (10), PaletteCacheService tests (7), extended SettingsService tests (11 new: panel clamping, visibility, ValidateRecentFilesAsync, persistence)

---

## [0.1.14-alpha] - 2026-02-25
**Branch**: `radoub/issue-1523` | **PR**: #1525

### Sprint: File Splits (#1523)

- [x] #1516 - Split MainWindow.axaml.cs - extracted StoreBrowser and LanguageMenu partial files

---

## [0.1.13-alpha] - 2026-02-16
**Branch**: `radoub/issue-1377` | **PR**: #1403

### Sprint: TLK Language & Delete Files (#1377)

- [x] #1361 - Item palette now displays in selected TLK language
- [x] #1362 - Add TLK language toggle (View > Language menu)
- [x] #1367 - Delete store files from module (context menu in browser panel)

---

## [0.1.12-alpha] - 2026-02-11
**Branch**: `radoub/issue-1259` | **PR**: #1311

### Sprint: Cross-Tool Inventory Unification (#1259)

Shared inventory patterns with Quartermaster, context menus, consistent interaction model.

---

## [0.1.11-alpha] - 2026-02-01
**Branch**: `fence/issue-1178` | **PR**: #1194

### Sprint: Final Polish (#1178)

Closes Epic #555.

- [x] #1042 - UI Layout Polish: Consolidate panels and improve usability
- [x] #1073 - Infinity toggle UI polish
- [x] #1120 - Add unit tests for ItemResolutionService and other untested services
- [x] #564 - Real UTM file validation tests

#### Changed
- **Infinity button**: Changed "∞ Selected" to "Make ∞" for clarity
- **Top panel layout**: Moved Comment to bottom row, Max Buy Price and Limited Gold to Row 2 with scripts
- **NewVar behavior**: Empty name field on create with auto-focus for immediate editing
- **Save As**: Defaults to current module directory

#### Added
- **ItemResolutionService tests**: 18 tests covering caching, fallback data, batch resolution, price calculations
- **UTM round-trip tests**: 85 tests covering all store fields with real file I/O validation
- **UTM field tests**: Added Comment, PaletteID, and complete store round-trip tests to Radoub.Formats

---

## [0.1.10-alpha] - 2026-01-31
**Branch**: `fence/issue-1144` | **PR**: #1168

### Feat: Integrate StoreBrowserPanel as collapsible left panel (#1144)

Part of Epic #1141 (File Browser Panel).

#### Added
- **Store browser panel** - Collapsible left panel showing all .utm files from current module
- **HAK scanning support** - "Show HAK" checkbox scans HAK files for .utm resources
- **HAK caching** - Scanned HAK contents cached with modification time validation
- **Single-click navigation** - Click a store to auto-save current file and load the new one
- **Current file highlight** - Currently loaded file highlighted in the list
- **View menu toggle** - View → Store Browser (F4) to show/hide panel
- **Panel width persistence** - Panel width saved to settings and restored on launch

#### Changed
- **MainWindow layout** - Outer grid now has store browser panel, splitter, and main content

---

## [0.1.9-alpha] - 2026-01-27
**Branch**: `radoub/issue-1138` | **PR**: #1139

### Sprint: Multi-Tool Tech Debt Cleanup (#1138)

- [x] #1119 - Extract shared TLK validation and label formatting utilities
  - Moved duplicate TLK validation to shared `TlkHelper` in Radoub.Formats
  - Moved duplicate label formatting to shared utility
- [x] #1121 - Code review cleanup: dead code, bugs, and minor issues
  - Remove unused `LoadBaseItemTypes()` method
  - Remove unused `ValidateAndRefreshRecentFilesAsync()` method
  - Remove empty PropertyChanged handler
  - Add exception logging to bare catch in SettingsWindow
  - Fix item #109 label bug (was BASE_ITEM_DIREMACE, now BASE_ITEM_THROWINGSTAR)

---

## [0.1.8-alpha] - 2026-01-24
**Branch**: `radoub/issue-1096` | **PR**: #1101

### Sprint: Custom File Browsers (#1096)

- [x] #1084 - Use custom file browser for .utm files
  - File > Open now uses `StoreBrowserWindow` from Radoub.UI
  - Shows stores from current module directory
  - Browse button to select different module folder
  - Consistent UX with other Radoub tools

---

## [0.1.7-alpha] - 2026-01-23
**Branch**: `fence/issue-1060` | **PR**: #1071

### Sprint: Completion Polish (#1060)

Part of Epic #555 (Merchant Editor Tool).

#### Work Items
- [x] #1043 - Add ResRef rename functionality
- [x] #1042 - UI Layout Polish - Consolidate panels and improve usability

#### Added
- **Editable ResRef column** - Store inventory ResRef can now be edited directly in grid (double-click to edit)
- **16-character limit** - Aurora Engine ResRef constraint enforced via MaxLength
- **Character validation** - Warns when ResRef contains non-standard characters (recommend a-z, 0-9, _)
- **Duplicate detection** - Warns when ResRef already exists in store inventory

#### Changed
- **Store Properties panel** - Now collapsible; merged Scripts & Comment into this panel
- **Buy Restrictions panel** - Moved "Buy Stolen Goods" here (was in Store Properties)
- **Pricing fields** - Replaced NumericUpDown controls with TextBox for cleaner UI
- **Infinity column** - Changed from checkbox to ∞ symbol (click to toggle); added tooltip
- **Script buttons** - Widened browse/clear buttons from 28 to 38 pixels for better clickability
- **Panel layout** - Reduced from 3 collapsible panels to 2 (Buy Restrictions + Local Variables)
- **Name-based store panel mapping** - Items assigned to panels by base item type name (not index) for compatibility

#### Removed
- **Scripts & Comment expander** - Consolidated into Store Properties panel

---

## [0.1.6-alpha] - 2026-01-21
**Branch**: `fence/issue-956` | **PR**: #1047

### Feature: Move status bar to top of window (#956)

Part of Sprint #1014 (Fence Completion).

#### Changed
- **Status bar position** - Moved from bottom to top of window for better visibility during loading operations
- **Status bar styling** - Added themed background, rounded corners, and full border to match panel headers

#### Fixed
- **Null reference warnings** - Added null-conditional operators for service calls that may occur before initialization
- **Async save operation** - File write now runs on background thread for UI responsiveness

---

## [0.1.5-alpha] - 2026-01-21
**Branch**: `fence/issue-1040` | **PR**: #1041

### Sprint: Fence Completion - Scripts, Variables & Polish (#1040)

Part of Epic #555 (Merchant Editor Tool).

#### Work Items
- [x] #945 - Add Scripts, Variables, and Comment fields
- [x] #1002 - Integrate IGameDataService for item data resolution (already done)
- [x] #1027 - Filter out bad strref errors in TLK lookups

#### Added
- **Scripts & Comment expander** - New collapsible section for OnOpenStore, OnStoreClosed scripts and Comment field
- **Script browser integration** - Browse button opens shared ScriptBrowserWindow for script selection
- **Comment field** - Multi-line text box for developer notes (max 120px height)

#### Fixed
- **Palette category filtering** - Filter out invalid category names (DELETED, BadStrRef, PAdding, etc.)
- **Duplicate categories** - Skip duplicate category IDs in palette loading
- **Item type dropdown filtering** - Filter garbage labels (deleted, padding, xp2spec*) from base item types
- **Item name resolution** - Filter invalid TLK strings (BadStrRef, DELETED, etc.) in item display names
- **Settings cache status** - Cache rebuild now shows "Rebuilt" status correctly after completion

---

## [0.1.4-alpha] - 2026-01-20
**Branch**: `fence/issue-958` | **PR**: #1033

### Fix: File loading delayed ~8 seconds (#958)

Continuation of performance optimization work started in 0.1.3-alpha.

#### Changed
- **Binary KEY cache format** - Reduced cache load time from 528ms to 33ms (16x faster)
- **HAK scanning disabled by default** - Eliminates 16+ second scan of 80+ HAK files
- **BIF metadata-only loading** - Avoids loading 500MB+ BIF files into memory
- **Background category loading** - UI stays responsive during initialization

---

## [0.1.3-alpha] - 2026-01-19
**Branch**: `fence/issue-1014` | **PR**: #1022

### Sprint: Fence Completion (#1014)

Part of Epic #555 (Merchant Editor Tool).

#### Work Items
- [ ] #945 - Add Scripts, Variables, and Comment fields
- [~] #958 - File loading performance (partial - still has hangs)
- [ ] #956 - Move status bar to top of window

#### Added
- **On-demand palette loading** - Items load when user selects a type filter, not on startup
- **Persistent KEY index cache** - KEY file parsing results cached to `~/Radoub/Cache/key_index_cache.json`

#### Changed
- **Non-blocking service initialization** - Window appears instantly, services initialize in background
- **Background threading** - Cache operations run on background thread with batched UI updates

#### Known Issues
- Performance still not at target (<2 seconds). BIF metadata loading and item resolution remain bottlenecks. See #958 for next steps.

---

## [0.1.2-alpha] - 2026-01-18
**Branch**: `radoub/issue-954` | **PR**: #955

### Sprint: Local Variables UI (#954)

#### Added
- **Local Variables panel** - View and edit store VarTable entries (int, float, string types)
- **Item palette caching** - Palette items cached to `~/Radoub/Fence/palette_cache.json` for faster startup
- **Cache management UI** - Settings → Cache tab shows cache info and "Clear and Reload Cache" button
- **First-launch notification** - Shows popup when building cache for first time
- **Creature item filtering** - Creature weapons (bite, claw, gore) excluded from palette
- **Async store inventory loading** - Store items load on background thread to avoid UI freeze

#### Fixed
- Float precision - Values rounded to 3 decimal places to match Aurora Engine typical precision
- Recent files menu async loading - No longer blocks UI on network paths
- Ctrl+S keyboard shortcut now works correctly

### Known Issues
- **Float precision display in Aurora Toolset** - Float values like `0.1` display as `0.100000001490116` in the BioWare toolset. This is an IEEE 754 binary floating-point limitation, not a Fence bug. Values like 0.1 cannot be represented exactly in binary. The toolset shows the raw float bits. Workaround: use binary-exact values (0.125, 0.25, 0.5) or store percentages as integers.

---

## [0.1.1-alpha] - 2026-01-17
**Branch**: `fence/issue-911` | **PR**: #944

### Sprint: Phase 2 - Full Editor Functionality

Part of Epic #555. Completes Fence to full editor functionality.

#### Added
- [x] **Create New Store** - File → New menu option (Ctrl+N)
- [x] **Search filters** - Store and palette search boxes filter by name/resref/type
- [x] **Type filter dropdown** - Filter palette by base item type
- [x] **Source checkboxes** - Standard/Custom content filtering
- [x] **Unit tests** - Fence.Tests project with 18 tests for BaseItemTypeService and SettingsService

#### Refactored
- **MainWindow partial classes** - Split 1024-line file into 4 partial classes (FileOps, ItemPalette, StoreOperations)

---

## [0.1.0-alpha] - 2026-01-15
**Branch**: `radoub/issue-558` | **PR**: #910

### Initial Release

Fence - Merchant Editor for Neverwinter Nights. Part of Epic #555.

#### Added
- **Project scaffold** - Avalonia UI application with theming and logging
- **MainWindow** - Two-panel layout with Store Inventory and Item Palette
- **Store Properties panel** - Name, Tag, pricing settings, black market flags
- **Buy Restrictions** - WillOnlyBuy/WillNotBuy as collapsible checkbox panel with base item types from baseitems.2da
- **Store Inventory** - DataGrid display with search/filter, infinite checkbox, resolved item names/types/prices from UTI files
- **Item Resolution** - ItemResolutionService loads UTI files via GameDataService, calculates sell/buy prices from markup/markdown
- **Item Palette** - Item browser with type filtering
- **File operations** - Open/Save UTM files, recent files menu
- **Double-click transfer** - Double-click to add/remove items from store
- **Settings** - Theme selection, font settings, resource paths
- **Non-modal dialogs** - All dialogs are non-blocking (per user requirement)
- **Theme support** - 8 themes including accessibility options

#### Dependencies
- UTM Parser from Radoub.Formats (#556)
- Shared UI components from Radoub.UI

---
