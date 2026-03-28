# Changelog - Relique

All notable changes to Relique (Item Blueprint Editor) will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

---

## [0.10.5-alpha] - 2026-03-28
**Branch**: `radoub/issue-2022` | **PR**: #2024

### Sprint: Startup Performance & Code Cleanup (#2022)

- Fix fake-async startup and defer unnecessary initialization

---

## [0.10.4-alpha] - 2026-03-28
**Branch**: `radoub/issue-2021` | **PR**: #2023

### Sprint: Relique Path & CI Bug Fixes (#2021)

- Fix settings folder path ("ItemEditor" → "Relique") with migration (#1948/#1909)
- Fix logs writing to ~/Radoub/Logs instead of session directory (#1915)
- Fix flaky RadoubSettingsTests.CustomTlkPath_PersistsToFile on Linux CI (#2007)

---

## [0.10.3-alpha] - 2026-03-24
**Branch**: `relique/issue-1831` | **PR**: #1973

### Sprint: Editor Polish (#1831)

- Color picker for appearance section (moved to Radoub.UI shared library)
- Form layout improvements (statistics position, descriptions side-by-side)
- Conditional Stack Size/Charges based on base item stacking column

---

## [0.10.2-alpha] - 2026-03-24
**Branch**: `relique/issue-1829` | **PR**: #1971

### Sprint: Bug Fixes (#1829)

- Module change from Trebuchet now detected via PropertyChanged subscription
- Title bar updates correctly on Recent Files open
- Statistics refresh on base item type change
- Property assignment uses move semantics with subtype-level filtering

---

## [0.10.1-alpha] - 2026-03-22
**Branch**: `radoub/issue-1936` | **PR**: #1938

### Sprint: Marlinspike Ctrl+F/H Rollout (#1936)

- Ctrl+F search and Ctrl+H replace for item files

---

## [0.10.0-alpha] - 2026-03-22
**Branch**: `relique/issue-1832` | **PR**: #1904

### Sprint: Browsing & Selection Controls (#1832)

- Searchable Base Type dropdown
- Item portrait/icon chooser with inline grid
- Standardized file browser instead of OS file picker
- Load module HAK files for CEP-extended base item types
- Filter reserved/placeholder entries from base type picker and item properties
- Icon scan uses MinRange/MaxRange from baseitems.2da

---

## [0.9.0-alpha] - 2026-03-21
**Branch**: `relique/issue-1833` | **PR**: #1899

### Sprint: TLK Resolution, Wizard Icons, and MainWindow Split (#1833)

- Item Name TLK resolution for base game items
- Wizard: Search by item icon/image
- Split MainWindow.axaml.cs into 6 partial files

---

## [0.8.0-alpha] - 2026-03-21
**Branch**: `relique/issue-1830` | **PR**: #1898

### Sprint: Properties Tree & Search UX (#1830)

- Disambiguate "On Hit" entries using nwscript.nss constants
- Search auto-expands matching subcategories with bold highlighting
- Category filter ComboBox, right-click "Add to Item" context menu

---

## [0.7.0-alpha] - 2026-03-18
**Branch**: `itemeditor/issue-1784` | **PR**: #1788

### Rename product to Relique (#1784)

- Rename user-facing product from ItemEditor to Relique (namespace stays `ItemEditor`)
- Rename directories, settings keys (with migration), UI strings, cross-tool references

---

## [0.6.0-alpha] - 2026-03-17
**Branch**: `itemeditor/issue-1728` | **PR**: #1783

### Sprint 6: Item Wizard and Cross-Tool Launch Integration (#1728)

- Item creation wizard (type, name, palette, finish)
- Fence and Quartermaster integration (Edit Item context menu + Refresh Palette)

---

## [0.5.0-alpha] - 2026-03-17
**Branch**: `itemeditor/issue-1723` | **PR**: #1779

### Sprint 5: Descriptions, Appearance, Variables, and Comments (#1723)

- Description fields with spell-check and token support
- Appearance section in collapsible Expander
- Local variables DataGrid and collapsible comments section

---

## [0.4.0-alpha] - 2026-03-17
**Branch**: `itemeditor/issue-1717` | **PR**: #1777

### Sprint 4: Property Editing, Bulk Operations, and Statistics (#1717)

- Edit existing properties (modify subtype/cost/param)
- Bulk property operations with multi-select and validation
- ItemStatisticsService for auto-generated stats description

---

## [0.3.0-alpha] - 2026-03-17
**Branch**: `itemeditor/issue-1712` | **PR**: #1775

### Sprint 3: Item Property Display and Add/Remove (#1712)

- ItemPropertyService with 2DA cascade for editing
- Available Properties tree view with search
- Assigned Properties panel with Add/Remove flow and cascading dropdowns

---

## [0.2.0-alpha] - 2026-03-17
**Branch**: `itemeditor/issue-1706` | **PR**: #1774

### Sprint 2: Basic Field Editing (#1706)

- Basic properties panel (name, tag, ResRef, base type, cost, weight)
- Flags, charges, stack size, conditional fields, palette category
- Round-trip unit tests for basic editing

---

## [0.1.0-alpha] - 2026-03-17
**Branch**: `itemeditor/issue-1700` | **PR**: #1773

### Sprint 1: Project Bootstrap (#1700)

- Project skeleton with csproj, Program.cs, App.axaml, version.json, test project
- CommandLineService, SettingsService, MainWindow with menu/status/browser
- File operations (Open, Save, Save As) for .uti files
- Trebuchet registration

---
