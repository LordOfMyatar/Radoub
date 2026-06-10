# Changelog

All notable changes to Reliquary (placeable editor for `.utp` files) are documented here.

Format based on [Keep a Changelog](https://keepachangelog.com/); versioning via Nerdbank.GitVersioning.

---

## [0.10.0-alpha] - 2026-06-09
**Branch**: `reliquary/sprint/preview-and-statusbar` | **PR**: #2438

### Sprint: 3D Preview Upgrades & Shared Status Bar

- Adopt the shared `StatusBarControl` with a live module indicator (#2428).
- 3D placeable preview gains rotate/zoom/pan/reset + fit-on-load camera controls and a backward-render fix, with the camera logic extracted into a shared Radoub.UI component (#2430).
- Preview shows a state selector (open/closed/activated/deactivated) limited to states the loaded model provides; Destroyed is omitted since stock models render it identically to default (#2431).

---

## [0.9.0-alpha] - 2026-06-07
**Branch**: `reliquary/sprint/browser-polish-ci` | **PR**: #2427

### Sprint: Editor Layout, Open-File Rename & Release Wiring

- Editor layout reorganized: Faction, Conversation, Initial State, and Treasure Model moved into the Identity & Combat panel; Scripts & Variables moved to the bottom (#2425).
- Rename an open saved placeable via right-click in the F4 browser (lock-aware save-rename-reload); editor ResRef stays read-only (#2424).
- Reliquary wired into the release pipeline so the alpha ships alongside the other tools.

---

## [0.8.0-alpha] - 2026-06-07
**Branch**: `reliquary/sprint/inventory-and-defaults` | **PR**: #2420

### Sprint: Inventory Palette Parity & Safe Defaults

- UTI palette brought to Fence/QM parity: resource coverage, base-item filtering, sorting, and detail icons (#2411).
- Static and Useable are now mutually exclusive — checking Static forces Useable off (#2412).
- F4 file browser shows a newly created placeable immediately after Save As (#2413).
- UTI palette height capped so the Add button stays visible with long CEP lists (#2414).
- Inventory items can be added via double-click or right-click context menu (#2415).
- Placeable palette category (PaletteID) is now a configurable combo, sourced from `placeablepal.itp` (#2416).
- New placeables seed game-safe defaults (HP/Appearance/Hardness/saves) to prevent Aurora divide-by-zero (#2417).

---

## [0.7.0-alpha] - 2026-06-06
**Branch**: `reliquary/sprint/epic-2289-followups` | **PR**: #2377

### Sprint: Epic #2289 placeable-editor follow-ups

- Faction combo populated from the module's `repute.fac`; selection is undoable (#2354).
- Inventory column resize, palette panel gaps, and non-resizable MainWindow fixed for large-font support (#2363).
- ResRef/Tag auto-sync with name (Relique-style linked checkbox) (#2372).
- Conversation field gains a Browse button to the shared DialogBrowserWindow (undoable) (#2373).
- 3D model preview now fits its allotted area in the IdentityCombat panel (#2375).
- Initial State edited as a named dropdown of placeable animation states instead of a raw number (#2376).

---

## [0.6.0-alpha] - 2026-06-06
**Branch**: `reliquary/issue-2367` | **PR**: #2371

### Post-epic audit follow-ups

- New Placeable flow: `File → New` (Ctrl+N) creates a blank, round-trippable placeable; first Save routes through Save As (#2367).
- Recent Files: MRU now persists to `ReliquarySettings.json` with an Open Recent menu, and Reliquary is registered in Trebuchet's per-tool Recent Files dropdown (#2368).
- Script sets: Save/Load Script Set now write and apply a reusable plain-text (`EventName=ResRef`) preset of the 13 event slots (load is one undo step); previously the buttons did nothing (#2369, #2374).
- Portrait Browse opens the shared PortraitBrowserWindow and sets PortraitId (undoable) with a live preview; previously stubbed (#2370).

---

## [0.5.0-alpha] - 2026-06-06
**Branch**: `radoub/sprint/flaui-coverage` | **PR**: #2365

### FlaUI coverage beyond smoke (#2304)

- Verified bootstrap FlaUI infra and extended Reliquary UI tests past launch/close. See root [CHANGELOG](../CHANGELOG.md) sprint entry.

---

## [0.4.0-alpha] - 2026-06-06
**Branch**: `reliquary/issue-2297` | **PR**: #2364

### Sprint 7: Cross-tool dispatch + FlaUI + v0.1.0-alpha release

- Cross-tool dispatch: `.utp` registered with ToolDispatchService; Conversation field opens DLG in Parley.
- FlaUI smoke test for the placeable editor; Reliquary added to `run-tests.ps1 -Tool`.
- Fixed: a placeable passed via `--file` now opens at startup, and the title bar shows the open file.
- UI uniformity audit against the bootstrap checklist (12/12 pass).

---

## [0.3.0-alpha] - 2026-06-06
**Branch**: `reliquary/issue-2296` | **PR**: #2358

### Sprint 6: Text + Inventory + browser metadata

- TextPanel: Description (TLK) + builder Comments, token insertion, spell-check.
- InventoryPanel (when Has Inventory): backpack list + UTI palette + read-only resolved details, undoable add/remove. (UTP stores only item ResRef + position, so no per-instance stack/charges editor.)
- Browser metadata indexing (UTP tag/name) with background pass and save notifier; lists module .utp plus base-game/HAK, with read-only preview of archive placeables.
- Document dirty-tracking + Save / Save As (Ctrl+S / Ctrl+Shift+S) with save-on-close/switch prompts; Save As copies a base-game placeable into the module to edit.
- Keyboard shortcuts (Ctrl+S/Z/Y/O, F4 to toggle the browser).

---

## [0.2.0-alpha] - 2026-06-05
**Branch**: `reliquary/issue-2295` | **PR**: #2352

### Sprint 5: IdentityCombat + Behavior panels

- IdentityCombatPanel: identity/combat fields, Static/Plot field enablement, appearance combo + 3D model preview.
- BehaviorPanel: 13 script slots with Browse + Edit (opens in the configured editor), advanced fields, shared VariablesPanel.
- All field/variable/script edits undoable via UndoRedoManager (Ctrl+Z/Y); placeable round-trip tests.
- Faction list, portrait browser, and script-set presets deferred to follow-ups (#2354 and later sprints).

---

## [0.1.0-alpha] - 2026-06-05
**Branch**: `reliquary/issue-2294` | **PR**: #2349

### Sprint 4: Tool scaffolding + ReliquaryPath + browser panel + fixtures

- First Reliquary tool code — demoable skeleton project (`Reliquary.csproj`, namespace `PlaceableEditor`).
- CommandLineService (`--file`/`--safemode`/`--help`) and SettingsService singleton.
- `ReliquaryPath` added to shared `RadoubSettings`; exe path registered on first run.
- `PlaceableBrowserPanel` (UTP ResRef/Name/Tag) on the shared file-browser base.
- MainWindow partial-class skeleton with stubbed content panels.
- UTP test fixtures extracted from `LNS_DLG.mod`; Reliquary.Tests project added.
- Registered in Trebuchet launcher; added to `Radoub.sln`.

---
