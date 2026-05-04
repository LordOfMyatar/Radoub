# Changelog - Relique

All notable changes to Relique (Item Blueprint Editor) will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

---

## [0.10.13-alpha] - 2026-05-03
**Branch**: `relique/issue-2106` | **PR**: #TBD

### Feature: Base Game (BIF) Item Support in ItemBrowserPanel (#2106)

- Add "Base Game" checkbox to ItemBrowserPanel alongside HAK, lazily loading items from base game BIF archives via `IGameDataService.GetPaletteItems(ResourceTypes.Uti)`
- BIF items now appear as Copy-to-Module candidates, unblocking the most common item-customization workflow (copy a base game item like `nw_wblcl001` as a template)
- Pattern parity with Fence's StoreBrowserPanel and Quartermaster's CreatureBrowserPanel

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
