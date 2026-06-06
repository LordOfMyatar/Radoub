# Changelog

All notable changes to Reliquary (placeable editor for `.utp` files) are documented here.

Format based on [Keep a Changelog](https://keepachangelog.com/); versioning via Nerdbank.GitVersioning.

---

## [0.3.0-alpha] - 2026-06-06
**Branch**: `reliquary/issue-2296` | **PR**: #TBD

### Sprint 6: Text + Inventory + browser metadata

- TextPanel: Description (TLK) + builder Comments, token insertion, spell-check.
- InventoryPanel: backpack list + UTI palette + per-instance editor (stack/charges/plot), undoable add/remove.
- Browser metadata indexing (UTP tag/name) with background pass and save notifier.

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
