# Changelog - Fence

All notable changes to Fence (Merchant Editor) will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

---

## [Unreleased]

---

## [0.1.3-alpha] - 2026-01-19
**Branch**: `fence/issue-1014` | **PR**: #1022

### Sprint: Fence Completion (#1014)

Part of Epic #555 (Merchant Editor Tool).

#### Work Items
- [ ] #945 - Add Scripts, Variables, and Comment fields
- [ ] #958 - File loading performance (target: under 2 seconds)
- [ ] #956 - Move status bar to top of window

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
