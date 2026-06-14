# Changelog - Relique

All notable changes to Relique (Item Blueprint Editor) will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

---

## [0.10.25-alpha] - 2026-06-14
**Branch**: `relique/issue-2231` | **PR**: #2466

### Sprint 1: Undo/Redo adoption (#2231)

- Ctrl+Z / Ctrl+Y undo/redo for property add/remove/clear and every scalar field: name, tag, descriptions, comment, flags (Plot/Cursed/Stolen/Identified/Droppable), additional cost, stack size, charges, category, icon, model parts, and appearance colors.
- Document/whole-field undo — Ctrl+Z reverts the whole previous value, never char-by-char or to a blank.
- Local variables remain outside undo for now (full variable undo tracked in #2467).
- Shared `UndoRedoManager` refuse-to-push fix + `RecordedFieldEditCommand` — see root [CHANGELOG](../CHANGELOG.md).

---

## [0.10.24-alpha] - 2026-06-07
**Branch**: `relique/issue-2385` | **PR**: #2402

### Sprint: Cost & UX (#2385)

- Rationalize Add vs Add Checked buttons (#2234)
- Compute base Cost from properties + base AC, replacing static stored value (#2235)
- Mannequin idle pose — relaxed stance for armor preview (#2232)
- Docs: refresh root README (Reliquary listed) + add Ctrl+F Find to NEW_TOOL_BOOTSTRAP (#2401)

---

## [0.10.23-alpha] - 2026-06-06
**Branch**: `relique/issue-2258` | **PR**: #2379

### Fix: Property handlers skip #2166 rollback pattern (#2258)

- Batch-add, remove, and clear-all property handlers now roll back model changes when `RefreshAssignedProperties` throws.
- Guarded `OnWindowClosing` re-entry so cleanup runs once.

---

## [0.10.22-alpha] - 2026-06-06
**Branch**: `radoub/sprint/flaui-coverage` | **PR**: #2365

### FlaUI integration tests (#2168, closes #2303)

- Added Relique FlaUI tests (smoke + ItemBrowserPanel + deeper coverage). See root [CHANGELOG](../CHANGELOG.md) sprint entry.

---

## [0.10.21-alpha] - 2026-06-04
**Branch**: `trebuchet/sprint/compile-marlinspike-scope` | **PR**: #2345

### Fix: Item delete now backs up first (#2347)

- Deleting an item blueprint now saves a backup to `~/Radoub/Backups/` before removing the file, so a misclick can be restored.
- Delete-confirm dialog now sizes to content and is resizable so the Delete/Cancel buttons are never clipped off-window (#2348).

---

## [0.10.20-alpha] - 2026-05-28
**Branch**: `relique/issue-2257-2261` | **PR**: #2283

### Sprint: Critical lock + HAK cache fixes (#2257, #2261)

- File lock leaks on close/delete/open-failure paths + disabled Undo/Redo menu stubs (#2257)
- Shared HAK cache marked valid forever after HAK deletion; `ItemBrowserPanel` scans every HAK instead of module-referenced subset, unlike sibling Store/Creature panels (#2261)

---

## [0.10.19-alpha] - 2026-05-26
**Branch**: `radoub/issue-2244` | **PR**: #2266

### Fix: Radoub.Formats parser hardening — integer overflow, atomic writes, silent truncation (#2244)

- Shared `Radoub.Formats` hardening from full-codebase review. ERF/KEY/BIF: uint arithmetic that wrapped before bounds checks now uses long-promoted comparisons; `ErfWriter.UpdateResource` switched to `File.Replace` (atomic, original survives mid-rename failures); ERF `ResId` preserved instead of silently renumbered to sequential index. GFF: `ReadCExoString` / `CExoLocString` no longer abandon entire string tables on a single oversized entry; bare `catch {}` in `GffFile.GetFieldValue<T>` narrowed and logged. TLK: `CleanResRef` no longer strips mid-string whitespace asymmetric with the writer. Dead UTF-8 fallback removed. Affects every tool consuming GFF/ERF/KEY/BIF/TLK.

---

## [0.10.18-alpha] - 2026-05-25
**Branch**: `radoub/issue-2241` | **PR**: #2265

### Fix: GFF 64-bit field types (DWORD64/INT64/DOUBLE) silently corrupt on round-trip (#2241)

- Shared `Radoub.Formats` fix: DWORD64/INT64/DOUBLE were classified as simple types and read/written as 32-bit, silently zeroing values on save and producing wrong floats on load. Now treated as complex types per Aurora spec — value stored as 8 bytes in FieldData section. Affects every tool consuming GFF (UTC, UTI, UTM, BIC, IFO, JRL, DLG).

---

## [0.10.17-alpha] - 2026-05-25
**Branch**: `radoub/issue-2238` | **PR**: #2239

### Fix: Severe memory bloat — idle RSS dropped from multi-GB to ~377 MB (#2238)

- Shared `Radoub.Formats` resolver fix: HAK index loading switched to `ErfReader.ReadMetadataOnly` so the resolver no longer buffers entire HAK byte arrays on the Large Object Heap. Eliminates the OOM-kill and hard-reboot risk when running multiple Radoub apps concurrently against large-HAK modules (CEP3 etc.).

---

## [0.10.16-alpha] - 2026-05-25
**Branch**: `relique/issue-2229` | **PR**: #2230

### Sprint: Relique UI/UX Polish

- Main editor UI polish: padding (16→25px right), field width caps, Name/Tag and ResRef/Cost paired side-by-side, Stack+Charges paired, Flags/Quantities moved above Descriptions, Item Properties soft height cap (#2229)
- Available Properties tree preserves expansion state across add (#2227)
- Property edit auto-applies on combo change — `Apply Changes` button retired (#2226)
- Appearance: filtered Part-number dropdowns for armor (parts_*.2da, ACBONUS-sorted) and composite weapons (MDL scan, sorted ascending). Labels read `Part N — ID NNN`, with `(AC ±X)` on Torso only since only parts_chest contributes to item AC (#2164)
- Item Statistics panel prepends `Base Armor Class: N` for armor items (sprint feedback, ties to #2164)
- StatusBar file path now uses leading ellipsis + 500px cap so the filename stays visible when paths are long; tooltip shows full sanitized path (shared `Radoub.UI.StatusBarControl`, affects all tools)
- Filed during sprint: #2231 (epic: Undo/Redo across all tools — bootstrap checklist updated), #2232 (mannequin idle pose), #2233 (composite weapon parts render misaligned, cross-tool with QM), #2234 (Add vs Add Checked UX rationalization), #2235 (real Cost computation engine)

---

## [0.10.15-alpha] - 2026-05-25
**Branch**: `relique/issue-2217` | **PR**: #2225

### Sprint: Relique Bug Bash (#2217)

- Fix Armor Bonus property crash on swords (#2166)
- Restore Custom tokens category in right-click token insertion menu (#2075)
- Rename built executable from `ItemEditor.exe` to `Relique.exe` (#2080)

---

## [0.10.14-alpha] - 2026-05-24
**Branch**: `relique/issue-2199` | **PR**: #2208

### Feature: Adopt FileBrowserPanelBase Name/Tag Sort + Search (#2199)

- Wire save flow to refresh the browser row's Tag/Name without a full reindex when a UTI is saved
- New `IBrowserRowRefresher` + `BrowserSaveNotifier` seam in Radoub.UI so the post-save hook is unit-testable (regression guard if the call gets dropped in a future refactor)
- New `FileBrowserPanelBase.FindEntryByFilePath` + `ItemBrowserPanel.RefreshEntryFromDiskAsync` static seams

---

## [0.10.13-alpha] - 2026-05-03
**Branch**: `relique/issue-2106` | **PR**: #2165

### Feature: Base Game (BIF) Item Support in ItemBrowserPanel (#2106)

- Add "Base Game" checkbox to ItemBrowserPanel for lazy-loading base game items from BIF archives via `IGameDataService.ListResources(ResourceTypes.Uti)`
- New "Module" checkbox alongside HAK / Base Game so module .uti files can be filtered out — full filter-row parity with `StoreBrowserPanel` / `CreatureBrowserPanel`
- Click an HAK or BIF row to load it into the editor as a read-only preview (yellow `🔒 Read-Only` banner; Add / Add Checked / Edit / Remove / Clear All buttons disabled; available-property tree disabled; defensive guards in every mutator handler)
- Right-click any HAK or BIF row → "Copy to Module" available, prefilled with source ResRef/Tag/Name, copies the resource into the module folder under a new ResRef
- Bug fix: opening a real .uti file after previewing an archive item correctly resets read-only state

### Shared (Radoub.UI) Improvements

- New `ContextRequested` handler in `FileBrowserPanelBase` selects the row under the pointer before the context menu opens — fixes a latent bug where right-clicking an unselected row showed an empty/broken context menu (affected all four browsers: Item, Store, Creature, Dialog)
- `ItemBrowserPanel.ExtractItemArchiveBytes` and `IsItemArchiveEntry` promoted to `public static` so consumer tools can route archive previews

---

## [0.10.12-alpha] - 2026-05-03
**Branch**: `relique/issue-1908` | **PR**: #2157

### Feature: Item Model 3D Preview (#1908, PR3b)

- 3D preview of currently-edited item in the Appearance expander, with view-preset buttons (Front/Back/Left/Right/Reset) and a "No 3D model" placeholder
- Static rendering only (no animations) via shared `ModelPreviewGLControl`
- ModelType coverage: Simple weapons (single MDL), Layered items (single MDL + Cloth1/2 PLT colors), Composite weapons (3 ResRefs joined via `MdlPartComposer.ComposeFlat`), Armor (full `ArmorParts` dict on `pmh0` mannequin via `MdlPartComposer.Compose` + all 6 PLT colors)
- 100ms debounce on color spinner `PropertyChanged` so rapid edits coalesce into a single reload
- Per-MainWindow `TextureService` ownership matching QM's pattern
- Held weapons (sword, bow, crossbow, polearm, two-bladed, sling, thrown) get a 90° X-axis trophy rotation so they display blade-up; helmets, armor, and other non-held items keep their authored orientation
- ArmorParts changes (Torso, Pelvis, Belt, etc.) now trigger preview reloads via prefix-matched `ArmorPart_*` PropertyChanged events

### UI Polish

- Armor Parts list reordered to match Aurora's anatomical top-to-bottom + left/right paired layout, with Robe last
- User-friendly armor part labels ("Right Shoulder" instead of "RShoul")
- New Armor Class display next to the Armor Parts header (read-only, derived from `parts_chest.2da[Torso].ACBONUS` per Aurora item format spec)

### Shared Library Fixes (Radoub.Formats + Radoub.UI)

- Layer-2 color slots (Cloth2/Leather2/Metal2/Tattoo2) now correctly index the `pal_*01.tga` palette files — the `_02` files don't exist in NWN per Aurora item format spec Section 2.1.2.4. Fixes ColorPickerWindow showing all-gray swatches for layer-2 slots and layer-2 color spinners having no visible effect on PLT-rendered armor textures.
- `BaseItemTypeInfo.WeaponWield` + `IsHeldWeapon` exposed for tools that need to distinguish held weapons from other items.
- `MdlPartComposer.Compose/ComposeFlat`, `MdlPartBoneMap`, `MdlPartNaming` and `ItemModelResolver` shipped earlier in PR3a (#2160) and PR1 (#2151); this PR is the consumer wiring.

### Diagnostics

- `TextureService.RenderPltTexture` logs a one-time-per-PLT layer pixel histogram at INFO level for diagnosis of "color slot has no visible effect" reports
- `PaletteColorService.GetPalette` logs DEBUG entries on TGA load failure (was silently returning gray)

---

## [0.10.11-alpha] - 2026-05-01
**Branch**: `radoub/issue-2159` | **PR**: #2160

### Fix: Replace hardcoded font sizes with themable DynamicResource bindings

- Replace ~14 hardcoded `FontSize="11" / "16" / "20"` instances on MainWindow (Basic Properties, Descriptions, Flags and Quantities, Item Statistics, Item Properties section headers; description hint text; validation labels) and NewItemWizardWindow (step headers, validation, char counts, helper labels) with `{DynamicResource FontSizeSmall|FontSizeLarge|FontSizeXLarge}`
- Required for low-vision users — hardcoded font sizes do not scale with the Trebuchet font-size slider

---

## [0.10.10-alpha] - 2026-04-18
**Branch**: `relique/sprint/1905-categories` | **PR**: #2107

### Sprint: Editor Preview & 2DA-Sourced Categories (#1905)

- Replace hardcoded item property categories with 2DA-sourced data (#1903)

---

## [0.10.9-alpha] - 2026-04-18
**Branch**: `fence/issue-2065` | **PR**: #2097

### Copy-to-Module for Items (#1479)

- Right-click any HAK item in the browser → Copy to Module with rename dialog
- Edit TemplateResRef, Tag, and LocalizedName before writing to module
- Inherits shared implementation from FileBrowserPanelBase

---

## [0.10.8-alpha] - 2026-04-09
**Branch**: `relique/issue-1983` | **PR**: #2044

### Sprint: Preview & Settings (#1983)
- SettingsWindow with game paths, theme/font display, and Trebuchet integration (#2009)
- Item icon picker dialog with inventory size rendering (#1911)
- Fix: Semantic theme colors (Info, Success, etc.) now apply before window construction (all tools)

---

## [0.10.7-alpha] - 2026-03-29
**Branch**: `relique/issue-1982` | **PR**: #2036

### Sprint: Bug Fix Sweep (#1982)
- Filter available properties by base item type (#1972)
- Add VarTable (local variable) search to UtiSearchProvider (#1940)
- Resolve racial subtype names instead of showing raw labels (#1917)

*Note: #1948, #1909, #1915 were already fixed in v0.10.4-alpha (#2023)*

---

## [0.10.6-alpha] - 2026-03-29
**Branch**: `radoub/issue-1817` | **PR**: #2030

### Token Chooser Control (#1817)
- Token insertion UI for player-facing text fields (color tokens, custom tokens)

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
