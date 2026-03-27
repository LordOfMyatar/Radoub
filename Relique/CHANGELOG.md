# Changelog - Relique

All notable changes to Relique (Item Blueprint Editor) will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

---

## [Unreleased]

---

## [0.10.3-alpha] - 2026-03-24
**Branch**: `relique/issue-1831` | **PR**: #1973

### Sprint: Editor Polish (#1831)

- [x] #1806 — Color picker for appearance section (shared library move to Radoub.UI)
- [x] #1810 — Form layout improvements (statistics position, descriptions side-by-side)
- [x] #1814 — Conditional Stack Size/Charges based on base item type stacking column

---

## [0.10.2-alpha] - 2026-03-24
**Branch**: `relique/issue-1829` | **PR**: #1971

### Sprint: Bug Fixes (#1829)

- [x] #1802 — Module change from Trebuchet now detected via PropertyChanged subscription
- [x] #1803 — Title bar updates correctly on Recent Files open (added UpdateTitle helper)
- [x] #1804 — Statistics refresh on base item type change
- [x] #1809 — Property assignment uses move semantics (subtype-level filtering)

---

## [0.10.1-alpha] - 2026-03-22
**Branch**: `radoub/issue-1936` | **PR**: #1938

### Sprint: Marlinspike Ctrl+F/H Rollout (#1936)

- [x] #1932 — Ctrl+F search and Ctrl+H replace for item files

---

## [0.10.0-alpha] - 2026-03-22
**Branch**: `relique/issue-1832` | **PR**: #1904

### Sprint: Browsing & Selection Controls (#1832)

- [x] #1807 — Searchable Base Type dropdown
- [x] #1808 — Item portrait/icon chooser (inline grid; full picker deferred to #1911/#1912)
- [x] #1816 — Use standardized file browser instead of OS file picker

### Additional Fixes

- Load module HAK files for CEP-extended base item types (was showing 91 types, now full set)
- Filter reserved/placeholder entries from base type picker and item properties (BioWare Reserved, CEP Reserved, User slots 214-509)
- Icon scan uses MinRange/MaxRange from baseitems.2da (was scanning 1-255)
- Selected icon preview slot with proper Stretch=Uniform scaling
- F4 keyboard shortcut for item browser toggle
- WARN logging for placeholder icons (iinvalid_2x2) and failed icon lookups

---

## [0.9.0-alpha] - 2026-03-21
**Branch**: `relique/issue-1833` | **PR**: #1899

### Sprint: TLK Resolution, Wizard Icons, and MainWindow Split (#1833)

- [x] #1805 — Item Name TLK resolution for base game items
- [x] #1815 — Wizard: Search by item icon/image
- [x] #1796 — Split MainWindow.axaml.cs into 6 partial files (82 + 196 + 205 + 401 + 588 + 269)

---

## [0.8.0-alpha] - 2026-03-21
**Branch**: `relique/issue-1830` | **PR**: #1898

### Sprint: Properties Tree & Search UX (#1830)

- [x] #1811 — Disambiguate "On Hit" entries using nwscript.nss constants (Properties, Monster Hit, Cast Spell)
- [x] #1812 — Search auto-expands matching subcategories with bold highlighting
- [x] #1813 — Category filter ComboBox, right-click "Add to Item" context menu, property count label

---

## [0.7.0-alpha] - 2026-03-18
**Branch**: `itemeditor/issue-1784` | **PR**: #1788

### Rename product to Relique (#1784)

- Rename user-facing product name from ItemEditor to Relique
- Rename directories: `ItemEditor/` → `Relique/Relique/` (matches tool convention)
- Rename settings: `ItemEditorPath` → `ReliquePath` with migration for old JSON key
- Update UI strings, window titles, About dialog, help output, settings paths
- Update Trebuchet tool card, Fence context menu, cross-tool launcher
- Fix flaky CommandLine test race condition (`[Collection]`)
- Namespace stays `ItemEditor` (no code-level rename)

---

## [0.6.0-alpha] - 2026-03-17
**Branch**: `itemeditor/issue-1728` | **PR**: #1783

### Sprint 6: Item Wizard and Cross-Tool Launch Integration (#1728)

- [x] #1729 — Item creation wizard (type → name → palette → finish)
- [x] #1730 — Fence integration (Edit Item context menu + Refresh Palette)
- [x] #1731 — Quartermaster integration (Edit Item context menu + Refresh Palette)

---

## [0.5.0-alpha] - 2026-03-17
**Branch**: `itemeditor/issue-1723` | **PR**: #1779

### Sprint 5: Descriptions, Appearance, Variables, and Comments (#1723)

- [x] #1724 — Description fields with spell-check and token support
- [x] #1725 — Item Type Description from 2DA (read-only)
- [x] #1726 — Appearance section in collapsible Expander
- [x] #1727 — Local variables DataGrid and collapsible comments section

---

## [0.4.0-alpha] - 2026-03-17
**Branch**: `itemeditor/issue-1717` | **PR**: #1777

### Sprint 4: Property Editing, Bulk Operations, and Statistics (#1717)

- [x] #1718 — Edit existing properties (modify subtype/cost/param)
- [x] #1719 — Bulk property operations (multi-select add/remove with validation)
- [x] #1720 — ItemStatisticsService (auto-generate stats description)
- [x] #1721 — Undroppable and Identified checkboxes
- [x] #1722 — Unit tests for bulk operations and statistics

---

## [0.3.0-alpha] - 2026-03-17
**Branch**: `itemeditor/issue-1712` | **PR**: #1775

### Sprint 3: Item Property Display and Add/Remove (#1712)

- [x] #1713 — ItemPropertyService (2DA cascade for editing)
- [x] #1714 — Available Properties panel with tree view and search
- [x] #1715 — Assigned Properties panel and Add/Remove flow with cascading dropdowns
- [x] #1716 — Unit tests for ItemPropertyService and property operations

---

## [0.2.0-alpha] - 2026-03-17
**Branch**: `itemeditor/issue-1706` | **PR**: #1774

### Sprint 2: Basic Field Editing (#1706)

- [x] #1707 — Basic properties panel (name, tag, ResRef, base type, cost, weight)
- [x] #1708 — Flags, charges, and stack size editing
- [x] #1709 — Conditional fields (armor/weapon) based on base item type
- [x] #1710 — Palette category dropdown
- [x] #1711 — Round-trip unit tests for basic editing

---

## [0.1.0-alpha] - 2026-03-17
**Branch**: `itemeditor/issue-1700` | **PR**: #1773

### Sprint 1: Project Bootstrap (#1700)

- [x] #1701 — Project skeleton (csproj, Program.cs, App.axaml, version.json, test project)
- [x] #1702 — CommandLineService (--file, --safemode, --help) + SettingsService
- [x] #1703 — MainWindow with menu bar, status bar, item browser panel
- [x] #1704 — File operations: Open, Save, Save As for .uti files
- [x] #1705 — Trebuchet registration + CLAUDE.md, CHANGELOG.md, README.md

---
