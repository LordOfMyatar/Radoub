# Changelog - Fence

All notable changes to Fence (Merchant Editor) will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

---

## [0.1.29-alpha] - 2026-04-30
**Branch**: `radoub/issue-1996` | **PR**: #2151

### Refactor: Adopt shared ItemDetailsPanel from Radoub.UI (#1996)

- Replace inline item detail block in MainWindow with shared `ItemDetailsPanel` (icon, name, type, ResRef, tag, value, source, properties)
- New `StoreItemExtrasPanel` companion control encapsulates Fence-only fields (sell/buy price, infinite, store panel)
- No behavior change — pure UI extraction for reuse across tools

---

## [0.1.28-alpha] - 2026-04-29
**Branch**: `radoub/issue-2144` | **PR**: #2150

### Refactor: Consolidate duplicate _cachedPaletteData (#2144)

- Remove local `_cachedPaletteData` field; read from `SharedPaletteCacheService` directly (single source of truth)

---

## [0.1.27-alpha] - 2026-04-26
**Branch**: `radoub/issue-2034-round-3` | **PR**: #2143

### Fix: Memory leaks round 3 — event unsubscription (#2034)

- Unsubscribe `CollectionChanged` and `DoubleTapped` handlers on window close

---

## [0.1.26-alpha] - 2026-04-18
**Branch**: `fence/issue-2065` | **PR**: #2097

### Sprint: Search & Copy-to-Module (#2065)

- Fix search bar inventory item name resolution (resolver race with async service init)
- Copy-to-Module rename dialog (ResRef/Tag/Name edit before write)
- Browser refresh after copy when Module checkbox is active
- Promote Copy-to-Module to shared `FileBrowserPanelBase` (closes #1479 — QM, Relique, Parley now inherit it)

---

## [0.1.25-alpha] - 2026-03-27
**Branch**: `fence/issue-1980` | **PR**: #2013

### Sprint: Detail Panel & Search (#1980)

- Display item properties in detail panel with game data chain resolution
- Search & replace store inventory item ResRefs with 16-char truncation warnings
- Parity with QM creature browser — BIF checkbox, module-aware HAK scan, "Copy to Module" for archive entries

---

## [0.1.24-alpha] - 2026-03-26
**Branch**: `fence/issue-1981` | **PR**: #1994

### Sprint: Performance & Cross-Tool Polish (#1981)

- Parallelize cold-cache palette loading (Task.WhenAll)
- Load item icons from HAK/CEP content
- Show item source location (UTI/BIF/HAK) in palette details
- Added Help > Export Logs for Support and Open Log Folder

---

## [0.1.23-alpha] - 2026-03-22
**Branch**: `fence/issue-1826` | **PR**: #1954

### Sprint: Item Display & Palette Standardization (#1826)

- Add item icons to store inventory grid
- Migrated to shared ItemViewModel and ItemFilterPanel (text search, slot filter, property search)

---

## [0.1.22-alpha] - 2026-03-22
**Branch**: `radoub/issue-1936` | **PR**: #1938

### Sprint: Marlinspike Ctrl+F/H Rollout (#1936)

- Ctrl+F search and Ctrl+H replace for store files

---

## [0.1.21-alpha] - 2026-03-19
**Branch**: `radoub/issue-1825` | **PR**: #1834

### Sprint: Legacy Code & Unused Package Cleanup (#1825)

- Remove unused CommunityToolkit.Mvvm and System.Text.Json packages

---

## [0.1.20-alpha] - 2026-03-17
**Branch**: `radoub/issue-1767` | **PR**: #1770

### Sprint: Cross-Tool & Trebuchet Bug Sweep (#1767)

- Fix: Loading UTM no longer sets dirty flag without changes
- Edit in ItemEditor context menu for store inventory items
- Refresh Item Palette menu item in View menu

---

## [0.1.19-alpha] - 2026-03-15
**Branch**: `fence/issue-1694` | **PR**: #1698

### Sprint: Core Bug Fixes

- Fix PaletteCacheService test failures
- Add dirty flag and save prompt when closing with unsaved changes

---

## [0.1.18-alpha] - 2026-03-15
**Branch**: `fence/issue-1693` | **PR**: #1696

### Sprint: Store Inventory UX

- Add MinWidth to Name columns to prevent collapse during resize

---

## [0.1.17-alpha] - 2026-03-11
**Branch**: `fence/issue-1634` | **PR**: #1661

### Sprint: Fence Tech Debt Cleanup

- Refactor hardcoded weapon/armor type names to use BaseItemTypeService (baseitems.2da)
- Add cancellation tokens to fire-and-forget palette tasks
- Standardize Task type syntax and replace inline error dialog with DialogHelper

---

## [0.1.16-alpha] - 2026-02-27
**Branch**: `radoub/issue-1560` | **PR**: #1561

### TDD Audit Followup (#1560)

- BaseItemTypeService tests (14 new) and ItemResolutionService tests (13 new) with MockGameDataService

---

## [0.1.15-alpha] - 2026-02-27
**Branch**: `radoub/issue-1555` | **PR**: #1559

### Sprint: Test Coverage & Anti-Pattern Cleanup (#1555)

- Added CommandLineService (10), PaletteCacheService (7), and extended SettingsService (11) tests

---

## [0.1.14-alpha] - 2026-02-25
**Branch**: `radoub/issue-1523` | **PR**: #1525

### Sprint: File Splits (#1523)

- Split MainWindow.axaml.cs into StoreBrowser and LanguageMenu partial files

---

## [0.1.13-alpha] - 2026-02-16
**Branch**: `radoub/issue-1377` | **PR**: #1403

### Sprint: TLK Language & Delete Files (#1377)

- Item palette displays in selected TLK language
- TLK language toggle (View > Language menu)
- Delete store files from module via context menu

---

## [0.1.12-alpha] - 2026-02-11
**Branch**: `radoub/issue-1259` | **PR**: #1311

### Sprint: Cross-Tool Inventory Unification (#1259)

- Shared inventory patterns with Quartermaster, context menus, consistent interaction model

---

## [0.1.11-alpha] - 2026-02-01
**Branch**: `fence/issue-1178` | **PR**: #1194

### Sprint: Final Polish (#1178)

- UI layout polish — consolidated panels and improved usability
- Infinity toggle UI polish ("Make ∞" label)
- ItemResolutionService tests (18) and UTM round-trip tests (85)

---

## [0.1.10-alpha] - 2026-01-31
**Branch**: `fence/issue-1144` | **PR**: #1168

### Feat: Store Browser Panel (#1144)

- Collapsible left panel showing all .utm files from current module
- HAK scanning support with modification-time cache validation
- Single-click navigation with auto-save and current file highlight

---

## [0.1.9-alpha] - 2026-01-27
**Branch**: `radoub/issue-1138` | **PR**: #1139

### Sprint: Multi-Tool Tech Debt Cleanup (#1138)

- Extract shared TLK validation and label formatting to Radoub.Formats
- Remove dead code, fix item #109 label bug (was DIREMACE, now THROWINGSTAR)

---

## [0.1.8-alpha] - 2026-01-24
**Branch**: `radoub/issue-1096` | **PR**: #1101

### Sprint: Custom File Browsers (#1096)

- File > Open now uses StoreBrowserWindow from Radoub.UI

---

## [0.1.7-alpha] - 2026-01-23
**Branch**: `fence/issue-1060` | **PR**: #1071

### Sprint: Completion Polish (#1060)

- Editable ResRef column with 16-char limit, character validation, duplicate detection
- Consolidated Store Properties panel (merged Scripts & Comment)
- Name-based store panel mapping for compatibility

---

## [0.1.6-alpha] - 2026-01-21
**Branch**: `fence/issue-956` | **PR**: #1047

### Feature: Status Bar Repositioned (#956)

- Moved status bar from bottom to top of window
- Async save operation on background thread

---

## [0.1.5-alpha] - 2026-01-21
**Branch**: `fence/issue-1040` | **PR**: #1041

### Sprint: Scripts, Variables & Polish (#1040)

- Scripts & Comment expander with ScriptBrowserWindow integration
- Filter out invalid palette categories and garbage item type labels
- Cache rebuild status reporting fix

---

## [0.1.4-alpha] - 2026-01-20
**Branch**: `fence/issue-958` | **PR**: #1033

### Fix: File Loading Performance (#958)

- Binary KEY cache format — 16x faster load (528ms to 33ms)
- HAK scanning disabled by default (eliminates 16+ second scan)
- BIF metadata-only loading (avoids 500MB+ memory usage)

---

## [0.1.3-alpha] - 2026-01-19
**Branch**: `fence/issue-1014` | **PR**: #1022

### Sprint: Fence Completion (#1014)

- On-demand palette loading (items load when user selects a type filter)
- Persistent KEY index cache
- Non-blocking service initialization

---

## [0.1.2-alpha] - 2026-01-18
**Branch**: `radoub/issue-954` | **PR**: #955

### Sprint: Local Variables UI (#954)

- Local Variables panel for VarTable entries (int, float, string)
- Item palette caching for faster startup
- Async store inventory loading to avoid UI freeze

---

## [0.1.1-alpha] - 2026-01-17
**Branch**: `fence/issue-911` | **PR**: #944

### Sprint: Phase 2 - Full Editor Functionality

- Create New Store (File > New, Ctrl+N)
- Search filters and type filter dropdown for store and palette
- MainWindow split into 4 partial classes

---

## [0.1.0-alpha] - 2026-01-15
**Branch**: `radoub/issue-558` | **PR**: #910

### Initial Release

- Two-panel layout with Store Inventory and Item Palette
- Store properties editing (name, tag, gold, prices, markup/markdown)
- Buy restrictions (WillOnlyBuy/WillNotBuy checkboxes)
- Item resolution from UTI files with price calculations
- File operations (Open/Save UTM, recent files)
- 8 themes including accessibility options

---
