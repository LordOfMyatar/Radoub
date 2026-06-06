# CLAUDE.md - Reliquary

Tool-specific guidance for Claude Code sessions working on Reliquary (PlaceableEditor namespace).

**Read the repository-level [CLAUDE.md](../CLAUDE.md) first** for shared conventions.

---

## Overview

**Reliquary** is a placeable blueprint editor for Neverwinter Nights UTP (Placeable Blueprint) files. Part of the Radoub toolset. Sister tool to Relique (same single-resource blueprint pattern).

### Key Information
- **Tool Name**: Reliquary
- **File Type**: `.utp` (Placeable Blueprint)
- **Internal Namespace**: `PlaceableEditor`
- **Assembly**: `Reliquary`
- **Current Version**: See `CHANGELOG.md` for latest version
- **Design Spec**: `NonPublic/PlaceableEditor/2026-05-28-reliquary-design.md`
- **Epic**: [#2289](https://github.com/LordOfMyatar/Radoub/issues/2289)

---

## Status

**Epic #2289 complete** — all 7 sprints landed; shipped as v0.1.0-alpha. Full editor wired (IdentityCombat, Behavior, Text, Inventory), undo/redo, browser metadata, cross-tool dispatch, FlaUI coverage.

| Sprint | Scope | State |
|--------|-------|-------|
| 4 | Project + services + browser panel + fixtures + Trebuchet registration | Done (#2349) |
| 5 | IdentityCombat + Behavior panels wired with UndoRedoManager | Done (#2352) |
| 6 | Text + Inventory panels, item palette caching, BrowserSaveNotifier | Done (#2358) |
| 7 | Cross-tool dispatch (Conversation → Parley), FlaUI smoke, v0.1.0-alpha release | Done (#2364) |

### Post-epic audit follow-ups (2026-06-06)

Resolved in PR #2371: #2367 (New Placeable flow), #2368 (Recent Files / MRU),
#2369 (Save/Load Script Set wired), #2370 (Portrait Browse).

Still open:

- #2354 — Faction combo from module repute.fac
- #2363 — Inventory column resize / palette gaps / non-resizable window

---

## Architecture

```
Reliquary/
├── CHANGELOG.md
├── CLAUDE.md (this file)
├── README.md
├── version.json                       (NBGV, "0.1-alpha")
├── Reliquary/                         (namespace: PlaceableEditor)
│   ├── Reliquary.csproj               (<AssemblyName>Reliquary</AssemblyName>)
│   ├── Program.cs
│   ├── App.axaml(.cs)                 (first-run ReliquaryPath registration)
│   ├── app.manifest
│   ├── Services/
│   │   ├── CommandLineService.cs      (--file, --safemode, --help)
│   │   └── SettingsService.cs         (singleton, ~/Radoub/Reliquary/ReliquarySettings.json)
│   └── Views/
│       ├── MainWindow.axaml(.cs)      (+ MainWindow.Lifecycle.cs)
│       └── Panels/
│           ├── PlaceableBrowserPanel.cs   (FileBrowserPanelBase subclass)
│           ├── IdentityCombatPanel.axaml(.cs)  (stub)
│           ├── BehaviorPanel.axaml(.cs)        (stub)
│           ├── TextPanel.axaml(.cs)            (stub)
│           └── InventoryPanel.axaml(.cs)       (stub)
└── Reliquary.Tests/                   (namespace: PlaceableEditor.Tests)
    ├── CommandLineServiceTests.cs
    ├── SettingsServiceTests.cs
    ├── Panels/PlaceableBrowserPanelIndexingTests.cs
    └── Fixtures/                       (5 real UTPs from LNS_DLG + round-trip tests)
```

---

## UTP File Format

UTP files are GFF-based placeable blueprints. Parser/writer is in `Radoub.Formats/Radoub.Formats/Utp/` (`UtpReader`, `UtpWriter`, `UtpFile`).

### Core Properties
- `LocName` — localized placeable name (note: `LocName`, not UTI's `LocalizedName`)
- `Tag` — placeable tag (max 32 chars)
- `TemplateResRef` — resource reference (max 16 chars)
- `Appearance` — index into placeables.2da
- `HasInventory` / `Useable` / `Static` / `Plot` — behavior flags
- `Conversation` — attached DLG resref
- `ItemList` — inventory contents (PlaceableItem, when HasInventory)

---

## Development

### Build & Run
```bash
dotnet build Reliquary/Reliquary/Reliquary.csproj
dotnet run --project Reliquary/Reliquary/Reliquary.csproj
dotnet test Reliquary/Reliquary.Tests
```

### Patterns
- Follow Relique/Quartermaster: `SettingsService` (tool-local), `RadoubSettings` (shared paths), `ThemeManager`, `UnifiedLogger`.
- **Delete-with-backup is inherited** from `FileBrowserPanelBase` (#2350) — do NOT hand-roll a delete handler. Subscribe to `FileDeleted` for editor cleanup only.
- Browser metadata read goes through `PlaceableBrowserPanel.ReadUtpMetadata` (static, testable seam).

---

## Commit Conventions

Use `[Reliquary]` prefix. Changes go in `Reliquary/CHANGELOG.md`.

```bash
[Reliquary] feat: Wire IdentityCombat panel (#XXXX)
```

---

## Dependencies

| Library | Purpose |
|---------|---------|
| Radoub.Formats | UTP parsing (UtpReader/UtpWriter), GameDataService, 2DA/TLK |
| Radoub.UI | FileBrowserPanelBase, ThemeManager, AboutWindow, BrushManager |
| Radoub.Dictionary | (future) spell-check for text fields |

---

## Resources

- [Reliquary CHANGELOG](CHANGELOG.md)
- [UTP Parser](../Radoub.Formats/Radoub.Formats/Utp/)
- Design + plans: `NonPublic/PlaceableEditor/` (NOT in tool directory)
- [Epic #2289](https://github.com/LordOfMyatar/Radoub/issues/2289)

---
