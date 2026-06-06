# Changelog

All notable changes to Reliquary (placeable editor for `.utp` files) are documented here.

Format based on [Keep a Changelog](https://keepachangelog.com/); versioning via Nerdbank.GitVersioning.

---

## [0.2.0-alpha] - 2026-06-05
**Branch**: `reliquary/issue-2295` | **PR**: #2352

### Sprint 5: IdentityCombat + Behavior panels

- IdentityCombatPanel: portrait, identity fields, 3D preview; HasInventory/Static/Plot flag wiring.
- BehaviorPanel: 13 script slots, advanced behavior fields, shared VariablesPanel, script set presets.
- All mutations undoable via UndoRedoManager; placeable round-trip tests.

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
