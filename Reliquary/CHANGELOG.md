# Changelog

All notable changes to Reliquary (placeable editor for `.utp` files) are documented here.

Format based on [Keep a Changelog](https://keepachangelog.com/); versioning via Nerdbank.GitVersioning.

---

## [0.1.0-alpha] - 2026-06-05
**Branch**: `reliquary/issue-2294` | **PR**: #TBD

### Sprint 4: Tool scaffolding + ReliquaryPath + browser panel + fixtures

- First Reliquary tool code — demoable skeleton project (`Reliquary.csproj`, namespace `PlaceableEditor`).
- CommandLineService (`--file`/`--safemode`/`--help`) and SettingsService singleton.
- `ReliquaryPath` added to shared `RadoubSettings`; exe path registered on first run.
- `PlaceableBrowserPanel` (UTP ResRef/Name/Tag) on the shared file-browser base.
- MainWindow partial-class skeleton with stubbed content panels.
- UTP test fixtures extracted from `LNS_DLG.mod`; Reliquary.Tests project added.
- Registered in Trebuchet launcher; added to `Radoub.sln`.

---
