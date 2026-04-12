# Changelog - Trebuchet

All notable changes to Trebuchet (Radoub Launcher) will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

---

## [1.30.0-alpha] - 2026-04-11
**Branch**: `trebuchet/issue-2067` | **PR**: #TBD

### Sprint: ERF Importer & Marlinspike UTM Search (#2067)

- Promote ItemResolutionService to shared Radoub.UI library (#2014)
- Wire item name resolver into Marlinspike UTM search (#2014)
- ERF import wizard for selective resource extraction into module (#2016)

---

## [1.29.0-alpha] - 2026-04-11
**Branch**: `radoub/issue-2069` | **PR**: #2070

### Sprint: Marlinspike Search Provider Expansion (#2069)

- UtpSearchProvider: search placeable inventory item ResRefs (#1951)
- Dedicated ITP and FAC search providers with human-readable paths (#2001)
- StrRef-resolved text search option across all providers (#2000)

---

## [1.28.0-alpha] - 2026-03-29
**Branch**: `trebuchet/issue-1633` | **PR**: #2033

### Pre-warm shared palette cache on launch (#1633)
- Background BIF/HAK cache warm-up on Trebuchet startup
- Cross-process lock sentinels prevent redundant scanning
- Atomic cache writes for safe concurrent access

---

## [1.27.0-alpha] - 2026-03-28 | PR #2024
Rename Relique display name from "Item Blueprint Editor" to "Item Editor"

---

## [1.26.0-alpha] - 2026-03-26
**Branch**: `radoub/issue-2006` | **PR**: #2010

### Theme Unification & Startup Optimization (#2006, #1960)

- `--settings` CLI flag to open Trebuchet directly to settings window
- Startup optimization: theme copy timestamp check, cached tool discovery, deduplicated game discovery

---

## [1.25.0-alpha] - 2026-03-26
**Branch**: `trebuchet/issue-1933` | **PR**: #1999

### Marlinspike Search Panel (#1933)

- New 4th workspace tab: module-wide search & replace across all 17 GFF file types
- Results tree grouped by file type with double-click dispatch to correct Radoub tool
- Replace preview window with per-match selective checkboxes and backup

---

## [1.24.0-alpha] - 2026-03-17
**Branch**: `radoub/issue-1767` | **PR**: #1770

### Sprint: Cross-Tool & Trebuchet Bug Sweep (#1767)

- Fixed pop-up windows lacking borders and being immovable (#1689)
- Fixed log level preferences not propagating to launched tools (#1699)

---

## [1.23.0-alpha] - 2026-03-14
**Branch**: `trebuchet/issue-1678` | **PR**: #1688

### Sprint: Area Scanning Infrastructure (#1678)

- AreaScanService — shared area .git scanning infrastructure (#1320)
- Faction Editor: scan areas on faction delete to reindex creature FactionIDs (#1317)

---

## [1.22.0-alpha] - 2026-03-14
**Branch**: `trebuchet/issue-1677` | **PR**: #1679

### Sprint: UX Fixes — Lock Warning & Test Stability (#1677)

- .mod file lock detection: disable Build & Save when locked, show warning (#1495)
- Added ModuleFileLockService with polling timer

---

## [1.21.0-alpha] - 2026-02-21
**Branch**: `trebuchet/issue-1405` | **PR**: #1457

### Fix: Stale Logging to ~/Radoub/Radoub/Logs Ignores Retention Settings (#1405)

---

## [1.20.0-alpha] - 2026-02-17
**Branch**: `trebuchet/issue-1402` | **PR**: #1404

### Sprint: Code Quality & Release Readiness (#1402)

- Duplicate/dead code cleanup and ScriptCompilerService split (#1392, #1382)
- User-configurable preferred script compiler path (#1391)
- Added Fence & Trebuchet to release builds (#1383)

---

## [1.19.0-alpha] - 2026-02-15
**Branch**: `trebuchet/issue-1389` | **PR**: #1390

### Fix: Script Compiler Broken on Linux (#1389)

- Replaced bundled ARM64 Linux compiler binary with x86_64 from neverwinter.nim 2.1.2
- Added runtime executable permission for Linux/macOS

---

## [1.18.0-alpha] - 2026-02-15
**Branch**: `trebuchet/issue-1378` | **PR**: #1381

### Sprint: Build & Test Polish (#1378)

- Open Compiler Log button, compile uncompiled scripts checkbox
- Recompile selected scripts after error, always-save-before-testing option

---

## [1.17.0-alpha] - 2026-02-14
**Branch**: `trebuchet/issue-1351` | **PR**: #1360

### Sprint: Settings Migration Cleanup (#1351)

- Removed build settings from Settings window, moved compiler log out of user profile
- Moved TLK language settings to module metadata

---

## [1.16.0-alpha] - 2026-02-14
**Branch**: `trebuchet/issue-1354` | **PR**: #1359

### Sprint: ViewModel Tech Debt (#1354)

- Split MainWindowViewModel.cs (1367 → 5 partial classes)
- Decomposed ModuleEditorViewModel.cs (1676 → 9 files)

---

## [1.15.0-alpha] - 2026-02-14
**Branch**: `trebuchet/issue-1350` | **PR**: #1356

### Sprint: Post-Sprint 4 Bug Fixes (#1350)

- Fixed build/save warning not clearing after successful build (#1333)
- Fixed faction parent showing (None) for pre-existing factions (#1337)
- Font sizes now use DynamicResource instead of hardcoded values (#1316)

---

## [1.14.0-alpha] - 2026-02-14
**Branch**: `trebuchet/issue-1345` | **PR**: #1349

### Feat: Show failed scripts list with open-in-editor action (#1345)

- Failed scripts listed individually with checkboxes and "Open in Editor" button
- Code editor path configurable in Settings > Build Settings

---

## [1.13.0-alpha] - 2026-02-14
**Branch**: `trebuchet/issue-1331` | **PR**: #1339

### Sprint 4: Launch Tab + FlaUI Tests + Polish (#1331)

- Build & Test tab with game launch, DefaultBic, and build status
- Removed Dashboard tab — Module tab is now default
- Replaced dirty-module popup with inline "Build & Save" / "Test Anyway" buttons
- Keyboard navigation: Ctrl+1/2/3 switches workspace tabs
- 13 FlaUI integration tests covering smoke, tabs, content, toolbar, sidebar

---

## [1.12.0-alpha] - 2026-02-14
**Branch**: `trebuchet/issue-1330` | **PR**: #1336

### Sprint 3: Embed Faction Editor as Workspace Tab (#1330)

- Extracted FactionEditorPanel and embedded as workspace Factions tab
- Wired Save to toolbar for factions + IFO + module packing

---

## [1.11.0-alpha] - 2026-02-14
**Branch**: `trebuchet/issue-1329` | **PR**: #1334

### Sprint 2: Embed Module Editor as Workspace Tab (#1329)

- Extracted ModuleEditorPanel and embedded as workspace Module tab
- Wired Save to toolbar for IFO + module packing; removed separate window

---

## [1.10.0-alpha] - 2026-02-14
**Branch**: `trebuchet/issue-1328` | **PR**: #1332

### Sprint 1: Dashboard Layout Shell (#1328)

- Left sidebar panel (tool cards + recent modules) and workspace TabControl
- No-module empty state and UI/theme consistency

---

## [1.9.0-alpha] - 2026-02-12
**Branch**: `trebuchet/issue-950` | **PR**: #1315

### Feature: Faction Editor — Visual faction relationship management (#950)

---

## [1.8.0-alpha] - 2026-02-11
**Branch**: `trebuchet/issue-1260` | **PR**: #1308

### Sprint: Quick Wins + Manifest Integration (#1260)

- Subproject maturity badges (Alpha/Beta/Stable) on tool cards (#1245)
- Module dirty flag with "Build First / Test Anyway" prompt (#1161)
- Manifest auto-loads journal from Trebuchet's current module (#1079)

---

## [1.7.2-alpha] - 2026-02-11
**Branch**: `trebuchet/issue-1292` | **PR**: #1307

### Tech Debt: Fix silent theme distribution failure and HttpClient lifecycle (#1292)

---

## [1.7.1-alpha] - 2026-02-01
**Branch**: `radoub/issue-1185` | **PR**: #1187

### Feature: Clear Recent Modules (#1112)

---

## [1.7.0-alpha] - 2026-01-31
**Branch**: `trebuchet/issue-1150` | **PR**: #1159

### Sprint: Module Editor Polish (#1150)

- DefaultBic checkbox with .bic dropdown, auto-read from module IFO (#1110, #1103)
- CustomTlk browse button (#1111)
- Variable name auto-focus with inline validation (#1109)
- Script field browse and edit buttons for all 22 script fields (#1108)

---

## [1.6.1-alpha] - 2026-01-27
**Branch**: `radoub/issue-1138` | **PR**: #1139

### Sprint: Multi-Tool Tech Debt Cleanup (#1138)

- Replaced bare catch blocks with specific exception types and added logging

---

## [1.6.0-alpha] - 2026-01-25
**Branch**: `trebuchet/issue-1116` | **PR**: #1117

### Feature: NWScript Compiler Integration (#1116)

- Bundled neverwinter.nim's `nwn_script_comp.exe` for NWScript compilation
- Stale script detection (.nss vs .ncs timestamps)
- Build log with status bar feedback on failure

---

## [1.5.0-alpha] - 2026-01-25
**Branch**: `trebuchet/issue-1092` | **PR**: #1107

### Feature: IFO Field Version Validation for Backward Compatibility (#1092)

- Version-aware field writing: warns if NWN:EE features target older minimum version

---

## [1.4.0-alpha] - 2026-01-24
**Branch**: `trebuchet/issue-1095` | **PR**: #1102

### Sprint: IFO GUI & Module Management (#1095)

- DefaultBic, StartMovie, and NWN:EE Scripts tab with 6 extended event scripts (#1093)
- Unpack module to working directory (#1080) and build/pack with backup (#1081)
- Fixed ERF writer using space-padding instead of null-padding for ResRefs

---

## [1.3.1-alpha] - 2026-01-24
**Branch**: `trebuchet/issue-1090` | **PR**: #1091

### Fix: Module editor corrupts module.ifo — missing area list (#1090)

- Fixed field name case mismatch and added 19 missing IFO fields
- Updated IfoWriter to always write all fields for round-trip compatibility

---

## [1.3.0-alpha] - 2026-01-24
**Branch**: `trebuchet/issue-1061` | **PR**: #1077

### Sprint: Launch Workflow

- Tool launch with recent files dropdown per tool card (#1030)
- Custom ModuleBrowserWindow replacing OS file picker (#1023)
- Module Editor detects unpacked working directory for editable mode (#1038)

---

## [1.2.0-alpha] - 2026-01-20
**Branch**: `trebuchet/issue-953` | **PR**: #1037

### Feature: Module Editing (IFO Files)

- Tabbed IFO editor: metadata, version, HAKs, time, entry point, scripts, variables
- IfoFile/IfoReader/IfoWriter + ErfWriter infrastructure in Radoub.Formats
- Non-modal editor with automatic backup before MOD file modification

---

## [1.1.0-alpha] - 2026-01-20
**Branch**: `trebuchet/issue-951` | **PR**: #1025

### Feature: Launch NWN:EE with module for testing

- Test Module (+TestNewModule) and Load Module (+LoadNewModule) launch modes

---

## [1.0.0-alpha] - 2026-01-17
**Branch**: `radoub/issue-907` | **PR**: #939

### Initial Release

- Tool launcher with auto-discovery for Parley, Manifest, Quartermaster, Fence
- Light/dark themes with accessibility-aware font sizing
- Recent modules tracking and game/TLK status display
- Cross-platform: Windows, macOS, Linux

---
