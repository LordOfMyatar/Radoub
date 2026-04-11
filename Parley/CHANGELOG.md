# Changelog - Parley (Highlights)

All notable changes to Parley. One-line highlights per version; full details in git history.

---

## [0.1.162-alpha] - 2026-04-10 | PR #2048
**Branch**: `parley/issue-1977`

### Sprint: Quality of Life & Polish
- User-defined custom (non-color) tokens in token chooser (#1890)
- Sound Browser overhaul — correct labels, defaults, mono filter persistence (#1241)
- Fix ADPCM/DviAdpcm WAV playback (base game sounds now play correctly)
- Fix Sound Browser validation display and list refresh after channel detection

---

## [0.1.161-alpha] - 2026-03-29 | PR #2035
**Branch**: `parley/issue-1976`

### Sprint: UX Polish & Search Fix
- Fix --mod flag not working for module search context (#1927)
- Grey out 'Go to Parent Node' when not on a link node (#1967)
- Toggle to show entry/reply index numbers in tree and flow views (#1921)

---

## [0.1.160-alpha] - 2026-03-28 | PR #2024
- Lazy-load TTS factory and defer ResourcePathDetector startup
- Remove broken SVG export option

---

## [0.1.159-alpha] - 2026-03-24 | PR #1966
- Flowchart: configurable node width, drag-drop sibling reorder, drag-drop reparent

## [0.1.158-alpha] - 2026-03-22 | PR #1920
- Single-file search (Ctrl+F) and multi-file module search UI

## [0.1.157-alpha] - 2026-03-20 | PR #1879
- Fix NPC-to-NPC links visual bug

## [0.1.156-alpha] - 2026-03-19 | PR #1834
- Remove legacy migration code, defer GameDataService init

## [0.1.155-alpha] - 2026-02-28 | PR #1565
- FlowView quest tag icons, theme-aware BrushManager, test reliability

## [0.1.154-alpha] - 2026-02-28 | PR #1564
- Delete file from browser, persist character picker, split large controllers

## [0.1.153-alpha] - 2026-02-27 | PR #1558
- Replace 35 anti-pattern tests with 40 behavioral tests

## [0.1.152-alpha] - 2026-02-26 | PR #1537
- Split ConversationSimulatorViewModel, FlowchartManager into partials

## [0.1.150-alpha] - 2026-02-15 | PR #1374
- Replace 126 hardcoded theme values

## [0.1.148-alpha] - 2026-02-08 | PR #1270
- Decompose SettingsService into 3 focused sub-services

## [0.1.147-alpha] - 2026-02-07 | PR #1268
- MainWindow decomposition: extract 4 controllers/services

## [0.1.146-alpha] - [0.1.130-alpha] | 2026-02-07 | PRs #1237-#1264
- **17 sprints: DI & Testability Refactoring** -- Full DI architecture, service interfaces, singleton removal, property panel sub-populators, MainWindow partial classes, controller extraction, mock services, 80%+ test coverage

## [0.1.129-alpha] - 2026-02-01 | PR #1195
- Fix UI tests after DialogBrowserPanel, add AutomationIds

## [0.1.128-alpha] - 2026-02-01 | PR #1179
- Dialog validation for unsupported chars, status bar to top, word wrap fix

## [0.1.127-alpha] - 2026-01-31 | PR #1164
- **Collapsible Dialog Browser Panel** -- left panel with all .dlg files, search/filter, F4 toggle

## [0.1.126-alpha] - 2026-01-24 | PR #1104
- TreeView word wrap option, flowchart font follows theme

## [0.1.125-alpha] - 2026-01-24 | PR #1087
- Custom dialog browser for Open, shared IFO library, dark theme fixes

## [0.1.124-alpha] - 2026-01-19 | PR #1011
- Startup performance: background warmup of GameDataService

## [0.1.123-alpha] - 2026-01-19 | PR #1009
- Remove duplicate ScriptBrowserWindow (~1,089 lines)

## [0.1.122-alpha] - 2026-01-19 | PR #1000
- Fix duplicate themes, dark theme contrast, child link opacity

## [0.1.121-alpha] - 2026-01-16 | PR #918
- **Audio Features** -- Sound from HAK/BIF, WAV validation, NPC soundset/portrait preview, SSF parser

## [0.1.120-alpha] - 2026-01-16 | PR #913
- Extract WindowLayoutService from SettingsService

## [0.1.118-alpha] - 2026-01-15 | PR #904
- Remove WebView dependency (binary: 359 MB to 38 MB)

## [0.1.117-alpha] - 2026-01-15 | PR #902
- Configurable flowchart text lines, FlowView context menu parity

## [0.1.116-alpha] - 2026-01-15 | PR #900
- Replace hardcoded Gray colors, add FlowchartPanel focus indicator

## [0.1.115-alpha] - 2026-01-14 | PR #896
- Remove Python plugin system (~50 files), script cache timestamp validation

## [0.1.114-alpha] - 2026-01-12 | PR #888
- Token insertion spacing, TreeView speaker tag coloring

## [0.1.113-alpha] - 2026-01-12 | PR #878
- **Color Token Support** -- NWN `<c###>` parsing/rendering, Token Selector (Ctrl+T), simulator colored text

## [0.1.112-alpha] - 2026-01-11 | PR #876
- **Scrap System Overhaul** -- Hierarchical TreeView, selective restore, Swap Roles

## [0.1.111-alpha] - 2026-01-10 | PR #857
- DLG filename validation (16-char limit), sound mono validation, FlowView shortcuts

## [0.1.110-alpha] - 2026-01-02 | PR #724
- SettingsService split, dead code removal, settings persistence fix

## [0.1.109-alpha] - [0.1.105-alpha] | 2026-01-01 | PRs #710-#714
- **5 file-split refactorings** -- SoundBrowserWindow 57% smaller, SettingsWindow 76% smaller, 3 controllers split

## [0.1.104-alpha] - 2025-12-31 | PR #702
- Fix FlowView focus jumping, autosave nav override, live validation warnings

## [0.1.103-alpha] - 2025-12-30 | PR #682
- Script browse respects cancel, conditional preview shows BIF scripts

## [0.1.102-alpha] - 2025-12-28 | PR #608
- Remove ~3,800 lines legacy DLG parser; full migration to Radoub.Formats

## [0.1.100-alpha] - 2025-12-27 | PR #602
- Remove legacy GffBinaryReader (-765 lines); all GFF via shared library

## [0.1.99-alpha] - [0.1.97-alpha] | 2025-12-27 | PRs #596-#601
- Extract GffFieldFactory/GffBinaryWriter, split MainViewModel into 6 partials (81% reduction)

## [0.1.96-alpha] - 2025-12-27 | PR #590
- **DLG Parser Migration** -- DlgReader/DlgWriter in Radoub.Formats, 17 round-trip tests, DlgAdapter

## [0.1.95-alpha] - 2025-12-26 | PR #569
- MainWindow.axaml.cs: 2,485 to 1,596 lines via partial classes

## [0.1.94-alpha] - 2025-12-25 | PR #539
- Dictionary language selection in Settings, hot-swap support

## [0.1.93-alpha] - 2025-12-25 | PR #538
- Delete focuses nearest sibling instead of root

## [0.1.92-alpha] - [0.1.88-alpha] | 2025-12-24 | PRs #527-#532
- **MainWindow refactoring** -- Remove 2,000 lines dead code, consolidate 24 deps, extract DialogFactory, constructor 225 to 22 lines

## [0.1.87-alpha] - 2025-12-24 | PR #520
- Fix FlowView: Fit to Window, Shift+drag, empty render, clear on close

## [0.1.86-alpha] - 2025-12-23 | PR #518
- Script discovery in HAK/ERF archives, priority: Module > HAK > Built-in

## [0.1.85-alpha] - 2025-12-23 | PR #509
- **Spell-checking** -- Hunspell + NWN/D&D dictionary for Text/Comments, shared custom dictionary

## [0.1.84-alpha] - 2025-12-22 | PR #492
- Piper TTS (13 neural voices) and espeak-ng variants for Linux

## [0.1.82-alpha] - 2025-12-21 | PR #485
- Per-node unreachable sibling warnings in tree view and simulator

## [0.1.80-alpha] - 2025-12-21 | PR #483
- **Text-to-Speech** -- Cross-platform TTS, per-speaker voices, auto-speak, auto-advance

## [0.1.79-alpha] - 2025-12-20 | PR #480
- **Conversation Simulator** -- Step-through branches, coverage tracking, loop detection, F6

## [0.1.78-alpha] - 2025-12-20 | PR #474
- Scrap redesign (batch tracking, subtree restore), settings migration to ~/Radoub/

## [0.1.77-alpha] - [0.1.73-alpha] | 2025-12-20 | PRs #467-#471
- **Epic #457: MainWindow 5,081 to 2,555 lines (50%)** -- Extracted FlowchartManager, TreeViewUIController, ScriptBrowserController, QuestUIController, FileMenuController, EditMenuController

## [0.1.72-alpha] - 2025-12-20 | PR #460
- Go to Parent Node (Ctrl+J), sibling creation (Ctrl+Shift+D)

## [0.1.71-alpha] - 2025-12-20 | PR #452
- **Drag-Drop & Collapsible Nodes** -- TreeView drag-drop with undo, FlowView collapse/expand, DialogChangeEventBus

---

## Archive: v0.1.14 - v0.1.70 (Nov-Dec 2025)

- **MainViewModel Refactoring** (v0.1.14-v0.1.20) - 6 service managers, 57% reduction
- **Theme System** (v0.1.18, v0.1.65-v0.1.67) - 8 themes, colorblind accessibility
- **Native Flowchart View** (v0.1.50-v0.1.56) - Visualization, PNG/SVG export
- **Script Parameters** (v0.1.39-v0.1.47) - Auto-save, validation
- **Sound Browser** (v0.1.41) - BIF scanning, subdata traversal
- **Undo/Redo Fixes** (v0.1.40-v0.1.42) - State restoration, deep clone
- **Linux Compatibility** (v0.1.68) - Case-insensitive scripts, BIF playback
- **Quest Browser** (v0.1.70) - Browse pattern, Open in Manifest
- **Plugin System** (v0.1.27-v0.1.38) - FlaUI tests, settings persistence

## Archive: v0.1.0 - v0.1.13 (Oct-Nov 2025)

- **Initial Release** (v0.1.0) - DLG reading/writing, tree editor, undo/redo, cross-platform
- **LinkRegistry** (v0.1.1) - Fixed copy/paste corruption
- **Performance** (v0.1.4) - Lazy loading, eliminated O(2^depth)
- **Plugin Foundation** (v0.1.5) - Python plugins, gRPC, process isolation
- **Script Parameters** (v0.1.6) - Browsing, caching, suggestions
- **UI/UX** (v0.1.8) - Font customization, layout redesign, Scrap Tab
- **Logging** (v0.1.10) - Path sanitization, log level filtering

---

**Development**: See `../About/CLAUDE_DEVELOPMENT_TIMELINE.md` for project history.
