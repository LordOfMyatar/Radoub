# Changelog - Manifest

All notable changes to Manifest will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

---

## [0.16.0-alpha] - 2026-04-13
**Branch**: `radoub/issue-2092` | **PR**: #2094

### Sprint: Upgrade to Avalonia 12 (#2092)
- Avalonia 12.0.x package upgrade

---

## [0.15.6-alpha] - 2026-03-24
**Branch**: `quartermaster/issue-1900` | **PR**: #1969

### Sprint: Module Switch & UI Bug Fixes (#1900)

- Module indicator theme refresh on theme change

---

## [0.15.5-alpha] - 2026-03-22
**Branch**: `radoub/issue-1936` | **PR**: #1938

### Sprint: Marlinspike Ctrl+F/H Rollout (#1936)

- Ctrl+F search and Ctrl+H replace for journal files

---

## [0.15.4-alpha] - 2026-03-17
**Branch**: `radoub/issue-1767` | **PR**: #1770

### Sprint: Cross-Tool & Trebuchet Bug Sweep (#1767)

- Fixed token preview display with explicit Foreground brush

---

## [0.15.3-alpha] - 2026-02-27
**Branch**: `radoub/issue-1555` | **PR**: #1559

### Sprint: Manifest & Fence Test Coverage + Anti-Pattern Cleanup (#1555)

- Added 22 new tests (EditOps, PropertyPanel, SpellCheckService)

---

## [0.15.2-alpha] - 2026-02-25
**Branch**: `radoub/issue-1523` | **PR**: #1525

### Sprint: File Splits (#1523)

- Split MainWindow.axaml.cs into 4 partial files
- Split SettingsWindow.axaml.cs into 3 partial files

---

## [0.15.1-alpha] - 2026-02-11
**Branch**: `radoub/issue-1300` | **PR**: #1310

### Sprint: Tech Debt Phase 3c - Trebuchet & Manifest Cleanup (#1300)

- Fixed settings persistence, command line race condition, and TLK log levels
- Added path traversal prevention to user path handlers

---

## [0.15.0-alpha] - 2026-02-11
**Branch**: `trebuchet/issue-1260` | **PR**: #1308

### Sprint: Trebuchet Module Management Integration (#1260)

- Auto-load journal from Trebuchet's current module when launched without --file

---

## [0.14.2-alpha] - 2026-02-01
**Branch**: `radoub/issue-1151` | **PR**: #1180

### Sprint: Bug Fixes - Manifest & Logging (#1151)

- Fixed token preview color not showing (property assignment order)
- Fixed duplicate theme list entries

---

## [0.14.1-alpha] - 2026-01-24
**Branch**: `radoub/issue-1097` | **PR**: #1100

### Sprint: Custom File Browser - Manifest & Path Standardization (#1097)

- File > Open now uses custom JournalBrowserWindow from Radoub.UI

---

## [0.14.0-alpha] - 2026-01-23
**Branch**: `manifest/issue-1059` | **PR**: #1070

### Sprint: Feature Parity (#1059)

- Token support in journal editor (Ctrl+T, Token Preview expander)
- New Journal command (Ctrl+N) to create module.jrl
- Tree panel MinWidth reduced for 1024px screen support

---

## [0.13.1-alpha] - 2026-01-15
**Branch**: `radoub/issue-908` | **PR**: #909

### Fix: Status bar file path truncation (#807)

- Long file paths now show ellipsis when truncated

---

## [0.13.0-alpha] - 2026-01-10
**Branch**: `manifest/issue-859` | **PR**: #862

### Sprint: Testing & Polish (#859)

- Added 28 unit tests for SettingsService and TlkService
- Unicode symbols for browse/detect buttons

---

## [0.12.0-alpha] - 2026-01-01
**Branch**: `radoub/sprint/refactor-cleanup` | **PR**: #709

### Refactor: Rename PreferencesWindow to SettingsWindow (#542)

- Renamed PreferencesWindow to SettingsWindow for cross-tool consistency

---

## [0.11.0-alpha] - 2025-12-25
**Branch**: `manifest/feat/dictionary-settings` | **PR**: #541

### Feature: Dictionary Settings & Recent Files (#540)

- Dictionary language selection UI in Settings (matching Parley)
- Hot-swap dictionary support (changes apply without restart)
- Recent Files menu (File > Open Recent, tracks last 10 files)

---

## [0.10.0-alpha] - 2025-12-25
**Branch**: `manifest/feat/flaui-smoke-tests` | **PR**: #537

### Feature: FlaUI Smoke Tests (#512)

- Application smoke tests, TLK integration tests, basic workflow tests

---

## [0.9.0-alpha] - 2025-12-23
**Branch**: `manifest/sprint/dictionary-integration` | **PR**: #511

### Sprint: Dictionary Integration (#506)

- SpellCheckTextBox with red squiggly underlines for misspellings
- Right-click context menu with suggestions and "Add to Dictionary"
- Shared custom dictionary with Parley at ~/Radoub/Dictionaries/custom.dic

---

## [0.8.0-alpha] - 2025-12-21
**Branch**: `manifest/sprint/cli-ui-polish` | **PR**: #493

### Sprint: CLI Arguments + UI Polish

- Command-line arguments: --file, --quest, --entry for cross-tool integration
- Journal entry layout improvements (reordered fields, larger text area)

---

## [0.7.0-alpha] - 2025-12-14
**Branch**: `manifest/feat/settings-ui` | **PR**: #422

### Feature: Settings UI (#421)

- Game path configuration (Base Game, User Documents) with auto-detect

---

## [0.6.0-alpha] - 2025-12-14
**Branch**: `manifest/sprint/testing-suite` | **PR**: #420

### Sprint: Testing Suite (#392)

- Manifest.Tests project with JRL round-trip and headless UI tests
- GitHub CI/CD workflows for releases and PR checks

---

## [0.5.0-alpha] - 2025-12-15
**Branch**: `manifest/sprint/ui-polish` | **PR**: #417

### Sprint: UI Polish (#391)

- Toolbar with icons, keyboard shortcuts, window state persistence
- Status bar with category/entry counts
- Tree view context menu, unique tag generation

---

## [0.4.0-alpha] - 2025-12-14
**Branch**: `manifest/sprint/theme-support` | **PR**: #415

### Sprint: Theme Support (#390)

- Theme system with light/dark/fluent/accessibility themes
- Runtime theme switching, persisted in settings

---

## [0.3.0-alpha] - 2025-12-14
**Branch**: `manifest/sprint/tlk-integration` | **PR**: #411

### Sprint: TLK Integration (#389)

- TlkService for loading and resolving localized strings
- Language dropdown and translation viewer in property panel
- Shared RadoubSettings and ResourcePathDetector in Radoub.Formats

---

## [0.2.0-alpha] - 2025-12-14
**Branch**: `manifest/sprint/file-io` | **PR**: #410

### Sprint: File I/O (#406)

- Open/save JRL files with dirty state tracking
- Property editing for categories and entries
- StrRef display, priority dropdown, auto-incrementing entry IDs

---

## [0.1.0-alpha] - 2025-12-14
**Branch**: `manifest/sprint/app-shell` | **PR**: #409

### Sprint: Application Shell (#405)

- Manifest project structure with Avalonia MainWindow
- Tree view, property panel, menu bar, settings and logging services

---
