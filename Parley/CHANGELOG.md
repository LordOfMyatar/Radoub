# Changelog - Parley

All notable changes to Parley will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

---

## [Unreleased]

---

## [0.1.111-alpha] - 2026-01-10
**Branch**: `parley/issue-856` | **PR**: #857

### Sprint: Validation & Polish (#856)

- [x] #826 - Validate DLG Filename Length on Save
- [x] #827 - Validate Sound Files Are Mono on Assignment
- [x] #809 - FlowView keyboard shortcuts parity with TreeView

#### #826 - Filename Length Validation

Blocks saving DLG files with names exceeding Aurora Engine's 16-character limit.

| Save Point | Behavior |
|------------|----------|
| New File | Validates before creating |
| Save | Shows error dialog, prompts Save As |
| Save As | Validates before saving |
| Auto-save | Skips with status bar warning |

Error dialog shows filename, length, and maximum allowed.

#### #827 - Sound File Mono Validation

Shows confirmation dialog when assigning stereo WAV files to conversation nodes.
NWN conversation audio requires mono; stereo plays incorrectly in-game.
User can proceed anyway (for post-processing workflows) or cancel.

#### #809 - FlowView Keyboard Shortcuts

FlowView now supports all TreeView keyboard shortcuts:

| Shortcut | Action |
|----------|--------|
| Ctrl+D | Add Smart Node |
| Ctrl+Shift+D | Add Sibling Node |
| Ctrl+R | Add Context-Aware Reply |
| Delete | Delete Node |
| Ctrl+C/X/V/L | Copy/Cut/Paste/Link |
| Ctrl+Z/Y | Undo/Redo |
| Ctrl+Shift+Up/Down | Move Node Up/Down |
| Ctrl+J | Go to Parent Node |

Works in embedded (side-by-side), tabbed, and floating window modes.

---

## [0.1.110-alpha] - 2026-01-02
**Branch**: `parley/issue-723` | **PR**: #724

### Sprint: Settings Persistence & Cleanup (#723)

#### #719 - SettingsService Refactoring

Split SettingsService (1,405 → 1,275 lines) into focused services:

| New File | Lines | Responsibility |
|----------|-------|----------------|
| RecentFilesService.cs | 141 | Recent files list management |
| UISettingsService.cs | 178 | Fonts, themes, flowchart layout, scrollbars |

#### #717 - Dead Code Removal

Removed unused properties:
- `UseNewLayout` - feature flag never implemented
- `WarnMissingScriptInDialogDirectory` - never referenced

#### #718 - Settings Persistence Fix

Fixed silent data loss: `ExternalEditorPath` and `ScriptSearchPaths` now persist across restarts.
Added to SettingsData, LoadSettings(), and SaveSettings().

---

## [0.1.109-alpha] - 2026-01-01
**Branch**: `parley/refactor/split-treeview-safenode` | **PR**: #714

### Refactor: Split TreeViewSafeNode.cs (#708)

Split 597-line file into focused components for maintainability:

| File | Lines | Responsibility |
|------|-------|----------------|
| TreeViewSafeNode.cs | 473 | Core node wrapper, properties, children, subclasses |
| TreeViewNodePopulator.cs | 123 | PopulateChildren, lazy loading logic |
| TreeViewValidation.cs | 53 | CalculateUnreachableSiblings validation helper |

---

## [0.1.108-alpha] - 2026-01-01
**Branch**: `parley/refactor/split-script-browser-controller` | **PR**: #713

### Refactor: Split ScriptBrowserController.cs (#707)

Split 638-line controller into focused components for maintainability:

| File | Lines | Responsibility |
|------|-------|----------------|
| ScriptBrowserController.cs | 299 | Script browser window management |
| ParameterBrowserController.cs | 290 | Parameter browser and declaration caching |
| ScriptPreviewService.cs | 88 | Script preview loading and display |

---

## [0.1.107-alpha] - 2026-01-01
**Branch**: `parley/refactor/split-script-param-ui-manager` | **PR**: #712

### Refactor: Split ScriptParameterUIManager.cs (#706)

Split 666-line file into focused services for maintainability:

| File | Lines | Responsibility |
|------|-------|----------------|
| ScriptParameterUIManager.cs | 269 | Core row management, add/remove, change handling |
| ParameterValidationService.cs | 244 | Duplicate key validation, red borders, theme colors |
| ParameterPersistenceService.cs | 233 | UI-to-model sync, auto-trim, parameter caching |

---

## [0.1.106-alpha] - 2026-01-01
**Branch**: `parley/feat/sound-browser-refactor` | **PR**: #711

### Refactor: Extract services and models from SoundBrowserWindow (#611)

- Extract `SoundScanner` service for HAK/KEY/BIF file scanning
- Extract `SoundCache` service for thread-safe cache management
- Move models to `Models/Sound/`: SoundFileInfo, HakCacheEntry, KeyCacheEntry, BifSoundInfo
- Reduce SoundBrowserWindow.axaml.cs from 1,423 to 606 lines (57% reduction)

---

## [0.1.105-alpha] - 2026-01-01
**Branch**: `parley/refactor/settings-window-controllers` | **PR**: #710

### Refactor: Extract controllers from SettingsWindow.axaml.cs (#610)

Reduced SettingsWindow.axaml.cs from 1,904 to 449 lines (76% reduction) by extracting section-specific controllers:

| Controller | Lines | Responsibility |
|------------|-------|----------------|
| ResourcePathsController | 500 | Game paths, module paths, TLK settings |
| ThemeSettingsController | 197 | Theme selection, preview, easter eggs |
| DictionarySettingsController | 247 | Primary language, custom dictionaries |
| PluginSettingsController | 209 | Plugin list, enable/disable, safe mode |
| UISettingsController | 292 | Font, scrollbar, NPC coloring, spell check |
| LoggingSettingsController | 116 | Log level, retention, debug panel |
| AutoSaveSettingsController | 86 | Auto-save enabled state and interval |
| ParameterCacheController | 180 | Cache settings, stats, clear cache |

Moved inline `PluginListItemViewModel` to proper ViewModels location.

---

## [0.1.104-alpha] - 2025-12-31
**Branch**: `parley/sprint/focus-ux-fixes` | **PR**: #702

### Sprint: Focus & UX Bug Fixes (#679)

#### Fixed
- Focus no longer jumps to Script Parameters when navigating in FlowView (#664)
  - Added `focusNewRow` parameter to `AddParameterRow` - only focuses when user explicitly adds
- Autosave no longer overrides user node navigation (#594)
  - `RefreshTreeDisplayPreserveState` now checks if user has navigated away during refresh
- Validation warnings (⚠️ unreachable siblings) now update immediately when condition scripts change (#609)
  - Made `IsUnreachableSibling` updatable via property notification
  - Added `RefreshSiblingValidation()` to recalculate when `ScriptAppears` changes

---

## [0.1.103-alpha] - 2025-12-30
**Branch**: `parley/sprint/script-browser-fixes` | **PR**: #682

### Sprint: Script Browser Fixes (#681)

#### Fixed
- Script browse dialog no longer sets script when cancelled with X button (#571)
- Conditional script preview now shows built-in game scripts from BIF archives (#570)
  - ScriptService falls back to GameResourceService when script not found in filesystem

---

## [0.1.102-alpha] - 2025-12-28
**Branch**: `parley/refactor/remove-legacy-parser` | **PR**: #608

### Refactor: Remove legacy DLG parser code (#592)

Complete migration to `Radoub.Formats.Dlg` parser. Removed ~3,800 lines of legacy parsing code.

**Removed Files** (3,782 lines):
| File | Lines | Purpose |
|------|-------|---------|
| `DialogParser.cs` | 287 | Legacy parser facade |
| `DialogBuilder.cs` | 638 | GFF → Dialog model conversion |
| `DialogWriter.cs` | 1,768 | Dialog → GFF binary writing |
| `GffBinaryWriter.cs` | 660 | Low-level GFF serialization |
| `GffFieldFactory.cs` | 202 | GFF field creation helpers |
| `GffIndexFixer.cs` | 178 | Struct index patching |
| `IDialogParser.cs` | 49 | Parser interface + ParserResult class |

**Added/Updated:**
- `DlgAdapter.cs`: Added TLK resolution (StrRef → text lookup via GameResourceService)
- `ParserResult.cs`: Moved to Models/ namespace
- `DialogFileService.cs`: Simplified - removed `UseNewParser` flag, always uses Radoub.Formats

**Architecture:**
- `Radoub.Formats.Dlg.DlgReader/DlgWriter` → Raw DLG binary I/O
- `DlgAdapter` → Bidirectional conversion between DlgFile and Parley's Dialog model
- `DialogFileService` → High-level async API for dialog file operations

---

## [0.1.101-alpha] - 2025-12-27
**Branch**: `parley/refactor/remove-dialogbuilder` | **PR**: #605

### Refactor: DialogBuilder.cs Investigation (#599)

**Outcome:** DialogBuilder.cs is NOT a duplicate of DlgWriter - they serve different architectural layers.

| Component | Purpose |
|-----------|---------|
| `DlgReader/DlgWriter` (Radoub.Formats) | Raw DLG format I/O |
| `DialogBuilder` (Parley) | Converts GFF → Parley domain model with TLK resolution, pointer linking, link registry |

**Why DialogBuilder is needed:**
- Preserves `OriginalGffStruct.Type` for byte-perfect round-trip editing
- Resolves TLK StrRef values to display text
- Links `DialogPtr.Node` references for tree traversal
- Integrates with Parley's `LinkRegistry` for reference tracking

**Removed:**
- `ConvertGlobalToLocalIndex()` (60 lines) - dead code, never called

---

## [0.1.100-alpha] - 2025-12-27
**Branch**: `parley/refactor/remove-gffbinaryreader` | **PR**: #602

### Refactor: Remove legacy GffBinaryReader.cs (#598)

Full migration to Radoub.Formats.Gff types, removing duplicate GFF parsing code.

**Removed Files:**
- `GffBinaryReader.cs` (582 lines) - replaced by `Radoub.Formats.Gff.GffReader`
- `GffStructures.cs` (183 lines) - replaced by `Radoub.Formats.Gff` types

**Updated Files:**
- `DialogParser.cs` - now uses `GffReader.Read()` instead of manual parsing
- `DialogBuilder.cs` - uses `Radoub.Formats.Gff.GffStruct`
- `GffParser.cs` - simplified to use `GffReader`
- `DialogWriter.cs`, `GffBinaryWriter.cs`, `GffFieldFactory.cs`, `GffIndexFixer.cs`, `CreatureParser.cs`, `ModuleInfoParser.cs` - updated imports
- `DialogStructures.cs` - `OriginalGffStruct` now uses `Radoub.Formats.Gff.GffStruct`

**Impact:** -765 lines of duplicate code removed. All GFF parsing now uses the shared `Radoub.Formats` library.

All 500 tests pass.

---

## [0.1.99-alpha] - 2025-12-27
**Branch**: `parley/refactor/gff-field-factory` | **PR**: #601

### Refactor: Extract GffFieldFactory from DialogWriter (#534)

Extracted field creation methods from DialogWriter.cs into new GffFieldFactory class.

| File | Before | After | Change |
|------|--------|-------|--------|
| DialogWriter.cs | 1,915 | 1,767 | -148 lines (8%) |
| GffFieldFactory.cs | - | 201 | New file |

**Extracted Methods:**
- `BuildLocStringFieldData()` - CExoLocString binary data builder
- `BuildCExoStringFieldData()` - CExoString binary data builder
- `BuildCResRefFieldData()` - CResRef binary data builder
- `AddLabelAndField()` - Label/field collection helper
- `GetOrCreateTextOffset()` - Text data creation with offset tracking
- `CalculateEntryFieldCount()` / `CalculateReplyFieldCount()` - Field count calculations

All 500 tests pass.

---

## [0.1.98-alpha] - 2025-12-27
**Branch**: `parley/refactor/gff-binary-writer` | **PR**: #600

### Refactor: Extract GffBinaryWriter from DialogWriter (#533)

Extracted binary writing methods from DialogWriter.cs (2,593 lines) into new GffBinaryWriter class.

| File | Before | After | Change |
|------|--------|-------|--------|
| DialogWriter.cs | 2,593 | 1,915 | -678 lines (26%) |
| GffBinaryWriter.cs | - | 659 | New file |

**Extracted Methods:**
- `Write()` - Main binary output (header, structs, fields, labels, field data)
- `WriteFieldIndices()` - Field index array writing
- `WriteListIndices()` - List indices section (conversation flow)
- Supporting helpers: `WriteHeader`, `WriteStructs`, `WriteFields`, `WriteLabels`, `WriteFieldData`

All 500 tests pass.

---

## [0.1.97-alpha] - 2025-12-27
**Branch**: `parley/refactor/mainviewmodel-size` | **PR**: #596

### Refactor: Reduce MainViewModel.cs Size (#536)

Split MainViewModel (1,786 lines) into partial classes for maintainability:

| File | Lines | Purpose |
|------|-------|---------|
| MainViewModel.cs | 337 | Core properties, constructor, debug messaging |
| MainViewModel.FileOperations.cs | 199 | Load, Save, New, Close, Reload |
| MainViewModel.EditOperations.cs | 377 | Undo, Redo, Copy, Cut, Paste |
| MainViewModel.NodeOperations.cs | 271 | Add, Delete, Move nodes |
| MainViewModel.TreeOperations.cs | 593 | Tree population, refresh, navigation |
| MainViewModel.ScrapOperations.cs | 91 | Scrap restore and management |

Core file reduced from 1,786 → 337 lines (81% reduction).
All 500 tests pass.

---

## [0.1.96-alpha] - 2025-12-27
**Branch**: `parley/feat/dlg-parser-migration` | **PR**: #590

### Feat: Migrate DLG Parser to Radoub.Formats (#560)

Migrate Parley's DLG parser to the shared Radoub.Formats library.

#### Phase 1: Extract & Refactor
- [x] Create `Radoub.Formats/Dlg/` directory
- [x] Create `DlgFile` domain model (entries, replies, links, params)
- [x] Create `DlgReader` using GffReader infrastructure
- [x] Create `DlgWriter` using GffFieldBuilder helpers
- [x] Migrate to use shared `CExoLocString`
- [x] Add 17 comprehensive unit tests with round-trip validation

#### Phase 2: Update Parley
- [x] Create `DlgAdapter` for model conversion (ToDialog/ToDlgFile)
- [x] Update `DialogFileService` with `UseNewParser` feature flag
- [x] Wire up new parser with logging integration
- [x] Verify all 500+ Parley tests pass
- [x] Enable new parser by default (`UseNewParser = true`)
- [ ] Remove legacy parser code (see #592)

#### Notes
- **Legacy Parser**: `DialogFileService.UseNewParser` flag allows fallback to legacy parser if issues arise. Set to `false` to use legacy Parley parser.
- **DlgWriter**: Updated to write all required GFF fields per BioWare spec (Aurora Engine expects all fields present, even when empty)

---

## [0.1.95-alpha] - 2025-12-26
**Branch**: `parley/refactor/mainwindow-size` | **PR**: #569

### Refactor: Reduce MainWindow.axaml.cs Size (#535)

Part of Epic #479 (Parley Refactoring).

#### Added
- `MainWindow.Properties.cs` - Partial class for property panel handlers and auto-save logic
- `MainWindow.TreeOps.cs` - Partial class for tree expand/collapse and state persistence
- `MainWindow.Theme.cs` - Partial class for theme application and handlers

#### Changed
- MainWindow.axaml.cs reduced from 2,485 to 1,596 lines (36% reduction)
- Code-behind now uses partial classes for maintainability

---

## [0.1.94-alpha] - 2025-12-25
**Branch**: `radoub/sprint/dictionary-language` | **PR**: #539

### Sprint: Dictionary Language Selection (#515)

#### Added
- **Dictionaries tab** in Settings (#510)
  - Primary language dropdown (select Hunspell dictionary)
  - Custom dictionaries list with enable/disable toggles (like Plugins tab)
  - NWN/D&D dictionary now toggleable (previously always-on)
  - "Open Dictionaries Folder" and "Refresh" buttons
- Hot-swap dictionary support - changes apply immediately without restart
- `SpellCheckService.DictionariesReloaded` event for UI refresh

---

## [0.1.93-alpha] - 2025-12-25
**Branch**: `parley/fix/delete-focus-sibling` | **PR**: #538

### Fix: Deleting Node Jumps Focus to Root Instead of Nearest Sibling (#435)

After deleting a node, focus now moves to the nearest sibling instead of jumping to root.

#### Changed
- Focus behavior after node deletion:
  1. Previous sibling (if exists) - stays at same visual position
  2. Next sibling (if no previous sibling)
  3. Parent (if no siblings)

---

## [0.1.92-alpha] - 2025-12-24
**Branch**: `parley/refactor/remove-parser-dead-code` | **PR**: #532

### Refactor: Remove Dead Code from DialogParser (#531)

Remove ~2,000 lines of dead/duplicated code from DialogParser.cs that remained after Phase 1-3 refactoring extractions.

#### Changed
- DialogParser.cs reduced from ~3,900 lines to ~500 lines
- Removed duplicated implementations that now delegate to support classes:
  - DialogBuilder (Phase 2)
  - DialogWriter (Phase 3)
  - GffIndexFixer (Phase 1)

#### Removed
- Duplicated field creation methods (CreatePointerFields, CreateRootFields, etc.)
- Duplicated binary builders (BuildLocStringFieldData, BuildCExoStringFieldData, etc.)
- Duplicated calculation methods (CalculateEntryFieldCount, CalculateLabelSize, etc.)
- Duplicated index fixing methods

---

## [0.1.91-alpha] - 2025-12-24
**Branch**: `parley/refactor/service-coordinator` | **PR**: #530

### Refactor: Create ServiceCoordinator (#526)

Consolidate MainWindow's 24 service dependencies into organized container classes.

#### Added
- `Views/Helpers/MainWindowServices.cs` - Container for 15 service dependencies:
  - Core: Audio, Creature
  - Plugin: Plugin, PluginPanel
  - Property: PropertyPopulator, PropertyAutoSave, ParameterUI
  - UI: NodeCreation, ResourceBrowser, KeyboardShortcuts
  - Window: DebugLogging, WindowPersistence, PluginSelectionSync
  - TreeView/Dialog: DragDrop, Dialog
- `Views/Helpers/MainWindowControllers.cs` - Container for 6 controller dependencies:
  - Flowchart, TreeView, ScriptBrowser, Quest, FileMenu, EditMenu

#### Changed
- MainWindow field count reduced from 24 to 6 (ViewModel, Controls, Windows, Services, Controllers, UiState)
- Removed verbose development comments (Phase 0/1/2 notes)
- MainWindow reduced by ~49 lines

---

## [0.1.90-alpha] - 2025-12-24
**Branch**: `parley/refactor/dialog-factory` | **PR**: #529

### Refactor: Extract DialogFactory Service (#524)

Extract dialog creation from MainWindow.axaml.cs into dedicated DialogFactory service.

#### Added
- `Services/DialogFactory.cs` - Reusable dialog factory with:
  - `ShowConfirmDialogAsync()` - Yes/No confirmation with optional "Don't show again" checkbox
  - `ShowSaveErrorDialogAsync()` - Save failed dialog with Save As option

#### Changed
- MainWindow now uses DialogFactory for all confirmation dialogs
- MainWindow reduced by ~117 lines

---

## [0.1.89-alpha] - 2025-12-24
**Branch**: `parley/refactor/mainwindow-constructor` | **PR**: #528

### Refactor: Extract MainWindow Constructor Initialization (#522)

Reduce MainWindow.axaml.cs constructor from 140+ lines to focused initialization methods.

#### Changed
- Extract constructor into 5 focused initialization methods:
  - `InitializeServices()` - Core services and helpers
  - `InitializeControllers()` - UI controllers
  - `InitializeLogging()` - Logging infrastructure
  - `RegisterEventHandlers()` - Event subscriptions
  - `SetupUILayout()` - UI configuration
- Extract inline event handlers to named methods:
  - `OnWindowOpened()` - Window opened handler
  - `OnWindowPropertyChanged()` - Window property change handler
  - `OnDebugMessagesCollectionChanged()` - Debug message scroll handler
- Constructor reduced from ~225 lines to 22 lines

---

## [0.1.88-alpha] - 2025-12-24
**Branch**: `parley/sprint/mainview-quick-wins` | **PR**: #527

### Sprint: MainView Quick Wins (Tech Debt)

Quick refactoring wins for MainViewModel.

#### Changed
- #521 - Remove dead code from MainViewModel (~50 lines of commented blocks)
- #523 - Create RefreshTreeViewAndMarkDirty() helper to consolidate pattern
- #525 - Create UiStateManager for centralized UI state tracking

---

## [0.1.87-alpha] - 2025-12-24
**Branch**: `parley/sprint/flowview-bug-bash` | **PR**: #520 | **Closes**: #514

### Sprint: FlowView Bug Bash

Fix four related FlowView rendering and navigation bugs.

#### Fixed
- #455 - FlowView: Fit to Window doesn't center content, appears mostly off-screen
- #454 - FlowView: Shift+drag panning broken in side-by-side mode, scrollbars missing
- #394 - Floating FlowchartWindow doesn't render when opened before DLG file
- #378 - Flowchart doesn't clear when file closed
- UI Tests: ClickMenu now retries up to 5 times with 300ms delays and searches desktop for Avalonia popup menus

---

## [0.1.86-alpha] - 2025-12-23
**Branch**: `parley/sprint/script-browser-hak` | **PR**: #518 | **Closes**: #516

### Sprint: Script Browser HAK Support (Epic #364)

Enable script discovery in HAK/ERF archives using Radoub.Formats ERF parsing.

#### Added
- Script Browser: "📦 Show HAK" checkbox to include scripts from HAK files
- Script Browser: Scans dialog directory, override path, and NWN user hak folder for HAK files
- Script Browser: Displays HAK scripts with 📦 icon prefix and source attribution (e.g., "HAK: customscripts.hak")
- Script Browser: Preview extraction for HAK scripts - view source code directly from archives
- Script Browser: Preview extraction for built-in scripts - lists .nss source files from game BIFs
- Script Browser: HAK metadata caching for fast repeated access (persists across window instances)
- Script Browser: Priority ordering matches NWN resource system (Module > HAK > Built-in)
- ScriptEntry: Added HakPath, ErfEntry, FilePath properties for source tracking
- UI Tests: Settings isolation prevents test runs from modifying user preferences
- UI Tests: Pre-seed SideBySide flowchart layout for stable automated testing

---

## [0.1.85-alpha] - 2025-12-23
**Branch**: `parley/sprint/dictionary-integration` | **PR**: #509 | **Closes**: #505

### Sprint: Dictionary Integration (Epic #43)

Add spell-checking to Parley's Text and Comments fields.

#### Added
- Spell-checking for dialog Text field
- Spell-checking for Comments field
- Hunspell (en_US) + custom D&D/NWN dictionary support
- Bundled NWN/D&D terminology dictionary (~450 terms: spells, creatures, deities, etc.)
- Session ignore for unknown words
- Spelling suggestions on misspelled words
- Custom dictionary persistence at `~/Radoub/Dictionaries/custom.dic` (shared across Radoub tools)
- "Add to Dictionary" saves permanently across sessions
- Spell-check enable/disable toggle in Settings > UI Settings

#### Fixed
- Spell-check toggle now takes effect immediately (no restart required)
- Scripts tab parameter fields no longer stretch to full width

---

## [0.1.84-alpha] - 2025-12-22
**Branch**: `parley/feat/linux-tts-enhancement` | **PR**: #492

### Feature: Enhance Linux TTS with Piper and Voice Variants (#491)

#### Added

**Piper TTS Integration** (Neural voices - high quality):
- PiperTtsService for natural-sounding neural voice synthesis
- Preferred over espeak-ng when installed
- 13 neural voice models for NWN languages:
  - English US (Lessac, Amy), English GB (Alan, Alba)
  - German (Thorsten, Eva)
  - French (UPMC, Siwis)
  - Spanish (Sharvard, Carlfm)
  - Italian (Riccardo)
  - Polish (Gosia, Darkman)

**espeak-ng Voice Variants** (Formant synthesis - fallback):
- Male and female variants for 6 NWN languages
- Uses `+m3`/`+f3` suffixes for distinct voices
- Friendly display names (e.g., "English (Male)")

#### Installation (Linux)

**Option 1: Piper TTS** (recommended for quality)
```bash
pipx install piper-tts
mkdir -p ~/.local/share/piper-voices
cd ~/.local/share/piper-voices
# Download English voice:
wget https://huggingface.co/rhasspy/piper-voices/resolve/v1.0.0/en/en_US/lessac/medium/en_US-lessac-medium.onnx
wget https://huggingface.co/rhasspy/piper-voices/resolve/v1.0.0/en/en_US/lessac/medium/en_US-lessac-medium.onnx.json
```
More voices: https://rhasspy.github.io/piper-samples/

**Option 2: espeak-ng** (basic, no download required)
```bash
sudo apt install espeak-ng
```

#### Technical
- TtsServiceFactory auto-detects: Piper > espeak-ng (Linux), Piper > say > espeak-ng (macOS)

---

## [0.1.83-alpha] - 2025-12-22
**Branch**: `parley/fix/linux-espeak-audio` | **PR**: #490

### Fix: Linux espeak-ng Audio Playback (#489)

#### Fixed
- espeak-ng TTS now plays audio on Linux (disabled stdout/stderr redirection)
- Voice selection now uses language codes (e.g., "en-us") instead of display names
- English voices prioritized in voice list with "en" as default
- Timing-sensitive tests now skip on Linux (OS-specific timing jitter)

---

## [0.1.82-alpha] - 2025-12-21
**Branch**: `parley/sprint/simulator-warnings` | **PR**: #485 | **Closes**: #484

### Sprint: Conversation Simulator - Warnings System

Implement warning detection for unreachable NPC entries (Epic #222).

#### Added
- ⚠️ Per-node unreachable sibling warnings in main tree view
- ⚠️ Per-entry unreachable sibling warnings in conversation simulator
- "Show Dialog Warnings" toggle in Settings > UI Settings
- Live refresh of warnings when setting is toggled

#### Technical
- `TreeViewSafeNode.CalculateUnreachableSiblings()` - static method for reuse
- `ReplyOption.IsUnreachable` property for simulator entries
- MultiBinding pattern for compound visibility (warning + setting)

---

## [0.1.81-alpha] - 2025-12-21
**Branch**: `parley/fix/linux-tts-dropdown-overlap` | **PR**: #487

### Fix: Linux TTS Dropdown Overlap (#486)

#### Fixed
- TTS voice dropdown no longer overlaps Linux installation instructions when no text to speak

---

## [0.1.80-alpha] - 2025-12-21
**Branch**: `parley/sprint/tts-integration` | **PR**: #483 | **Closes**: #479

### Sprint: Text-to-Speech Integration (Epic #222)

Add cross-platform text-to-speech to the conversation simulator for immersive dialog testing.

#### Added
- ITtsService interface for platform-agnostic TTS
- WindowsTtsService (System.Speech.Synthesis)
- EspeakTtsService (Linux espeak-ng)
- MacOsSayTtsService (macOS say command)
- TtsServiceFactory for platform detection
- Per-speaker voice assignment UI (NPC voices + PC voice)
- Speed control slider (0.5x - 2.0x)
- Auto-Speak mode (speaks both NPC and PC lines automatically)
- Auto-Advance mode (advances when single reply, waits for TTS completion)
- SpeakCompleted event for proper PC→NPC speech sequencing
- Graceful degradation with install instructions per platform

---

## [0.1.79-alpha] - 2025-12-20
**Branch**: `parley/sprint/conversation-simulator-walker` | **PR**: #480 | **Closes**: #478

### Sprint: Conversation Simulator - Dialog Walker + Coverage

#### Dialog Walker UI
- Floating window (ConversationSimulatorWindow.axaml)
- NPC text display with speaker name
- Script info panel (condition script + action script)
- Reply selection (radio buttons)
- Controls: Speak (placeholder), Pause, Skip, Restart, Exit

#### Navigation
- Step through conversation branches
- User selects reply at each fork
- Traverse links within same .dlg
- Loop detection with warning

#### Coverage Tracking
- Track unique paths taken (path signatures)
- Display percentage: "X of Y paths (Z%)"
- Persist coverage per dialog file (like Scrap)
- Clear button to reset coverage

#### Warnings
- ⚠️ "No conditional scripts found"
- ⚠️ "NPC entries without conditions" (unreachable siblings)
- ⚠️ "Loop detected"

#### Theme-Aware Colors
- NPC speaker label uses SpeakerVisualHelper for theme-aware coloring
- Choices indicator toggles between PC (blue, "Choose response:") and NPC (orange, "Select start:") styling
- F6 keyboard shortcut opens Conversation Simulator

#### Bug Fixes
- Fix false "unsaved changes" prompt when opening Conversation Simulator
- Coverage now tracks unique visited entries (node-based) instead of paths

---

## [0.1.78-alpha] - 2025-12-20
**Branch**: `parley/sprint/scrap-settings-migration` | **PR**: #474 | **Closes**: #473

### Sprint: Scrap System Redesign & Settings Migration

#### Scrap System Redesign (Epic #458)
- Redesign scrap data model with deletion batch tracking
- Add "Restore with descendants" operation for subtree restoration
- UI improvements: child count indicators, batch grouping
- Fix #370: Redo now properly restores deleted nodes to scrap panel
- Closes #124: Restore entire subtree chain with one click

#### Settings Migration
- #412: Migrate game paths to shared RadoubSettings
- #472: Move Parley folder from ~/Parley/ to ~/Radoub/Parley/
- Auto-migration preserves existing user settings

---

## [0.1.77-alpha] - 2025-12-20
**Branch**: `parley/sprint/mainwindow-cleanup` | **PR**: #471 | **Closes**: #466

### Sprint 5: MainWindow Cleanup & Integration (#457)

Final cleanup after Sprints 1-4. Extract additional controllers for file and edit operations.

**Line count**: MainWindow 3,047 → 2,555 lines (-492 lines)

#### Refactored
- **FileMenuController** (539 lines) - Extracted file menu operations:
  - New/Open/Save/SaveAs/Close/Exit handlers
  - Recent files menu population
  - Module info display (UpdateModuleInfo, ClearModuleInfo)
  - File-related dialog helpers

- **EditMenuController** (245 lines) - Extracted edit menu operations:
  - Undo/Redo handlers
  - Cut/Copy/Paste node handlers
  - Copy to clipboard handlers (text, properties, tree structure)
  - Paste-as-link-after-cut dialog

#### Summary
Epic #457 complete. MainWindow reduced from 5,081 to 2,555 lines (**50% reduction**).

Total extracted across all sprints:
- FlowchartManager: 875 lines
- TreeViewUIController: 593 lines
- ScriptBrowserController: 632 lines
- QuestUIController: 487 lines
- FileMenuController: 539 lines
- EditMenuController: 245 lines

---

## [0.1.76-alpha] - 2025-12-20
**Branch**: `parley/sprint/quest-ui-controller` | **PR**: #470 | **Closes**: #465

### Sprint 4: Extract QuestUIController (#457)

Extract quest/journal UI code from MainWindow.axaml.cs into dedicated QuestUIController class.

**Line count**: MainWindow 3,421 → 3,047 lines (-374 lines)

#### Refactored
- Extract quest tag/entry text change handlers
- Move quest browser dialog handlers (browse quest, browse entry)
- Extract quest clear button handlers (clear tag, clear entry)
- Move journal loading/integration
- Move quest display update methods (name display, entry preview)

---

## [0.1.75-alpha] - 2025-12-20
**Branch**: `parley/sprint/script-browser-controller` | **PR**: #469 | **Closes**: #464

### Sprint 3: Extract ScriptBrowserController (#457)

Extract script browser code from MainWindow.axaml.cs into dedicated ScriptBrowserController class.

**Line count**: MainWindow 3,999 -> 3,421 lines (-578 lines)

#### Refactored
- Extract script browser dialog handlers (conditional, action, conversation scripts)
- Move script editor launching (external editor integration)
- Extract parameter browser and suggestion logic
- Move script preview loading and caching
- Consolidate parameter declarations management
- Remove duplicate AddParameterRow, OnParameterChanged, ShowTrimFeedback from MainWindow

---

## [0.1.74-alpha] - 2025-12-20
**Branch**: `parley/sprint/treeview-ui-controller` | **PR**: #468 | **Closes**: #463

### Sprint 2: Extract TreeViewUIController (#457)

Extract TreeView UI handling code from MainWindow.axaml.cs into dedicated TreeViewUIController class.

**Line count**: MainWindow 4,448 -> 3,999 lines (-449 lines)

#### Refactored
- Extract drag-drop UI event handlers (pointer events, drag over, drop, visual indicators)
- Move selection handling logic (selection changed, double-tap expansion)
- Extract expand/collapse operations (recursive expand/collapse with circular reference protection)
- Move link navigation (Go to Parent node)

---

## [0.1.73-alpha] - 2025-12-20
**Branch**: `parley/sprint/flowchart-manager` | **PR**: #467 | **Closes**: #462

### Sprint 1: Extract FlowchartManager (#457)

Extract flowchart-related code from MainWindow.axaml.cs into dedicated FlowchartManager class.

**Line count**: MainWindow 5,081 -> 4,448 lines (-633 lines)

#### Refactored
- Extract flowchart layout modes (floating, side-by-side, tabbed)
- Move PNG/SVG export logic to FlowchartManager
- Move flowchart node click handling and tree sync
- Move FlowView collapse/expand event handling
- Centralize selection sync to all flowchart panels

---

## [0.1.72-alpha] - 2025-12-20
**Branch**: `parley/sprint/treeview-nav-ux` | **PR**: #460 | **Closes**: #149, #150

### Sprint: TreeView Navigation UX (#459)

Quality-of-life enhancements for faster dialog authoring workflows.

#### Added
- **#149**: Child link jump - Navigate from link node to parent
  - Context menu: "Go to Parent Node"
  - Keyboard shortcut: Ctrl+J
- **#150**: Sibling node creation
  - Ctrl+Shift+D creates sibling of current node
  - Maintains NPC/PC alternation

---

## [0.1.71-alpha] - 2025-12-20
**Branch**: `parley/sprint/drag-drop-collapse` | **PR**: #452 | **Closes**: #251, #436, #450

### Sprint: Drag-Drop & Collapsible Nodes (#451)

Advanced navigation and organization features for TreeView and FlowView.

#### Added
- **DialogChangeEventBus**: Centralized event system for TreeView/FlowView synchronization
  - Singleton pattern with pub/sub for dialog structure changes
  - Event types: NodeAdded, NodeDeleted, NodeMoved, SelectionChanged, DialogRefreshed
  - Suppression support for batch operations
- **TreeViewDragDropService**: Drag-drop infrastructure for dialog tree (#450)
  - Drag threshold detection (5px movement)
  - Drop position calculation (Before/After/Into zones)
  - NPC/PC alternation rule validation
  - Circular reference prevention
  - Link node drag prevention (drag original instead)
- **#450**: TreeView drag-drop node reordering (Aurora Toolset parity)
  - Visual drop indicators (CSS border/background classes)
  - Reorder nodes within same parent
  - Reparent nodes to different parent (with validation)
  - Undo support for move operations
- **#251**: FlowView collapse/expand subtrees
  - Collapse All / Expand All toolbar buttons
  - Double-click node to toggle collapse
  - Child count indicator (▼ N / ▶ N) on nodes with children
  - Hidden node count in status bar
- **Drag-drop unit tests**: 29 tests covering validation and move operations
  - DragDropTests.cs: MoveNodeToPosition tests (16 tests)
  - DragDropValidationTests.cs: ValidateDrop tests (13 tests)

#### Deferred
- **#240**: FlowView visual node repositioning - Requires custom layout engine; AvaloniaGraphControl uses automatic Sugiyama layout

#### Fixed
- **#436**: FlowView now updates when nodes are added/deleted/moved
  - MainWindow subscribes to DialogChangeEventBus
  - Floating, embedded, and tabbed panels all update on structure changes

---

## Archive Summary (v0.1.14 - v0.1.70)

Major features added during November - December 2025:

- **MainViewModel Refactoring** (v0.1.14-v0.1.20, Epic #99) - Completed Phase 4-7, extracted 6 service managers, reduced MainViewModel 57% (2,956 to 1,258 lines)
- **Theme System** (v0.1.18, v0.1.65-v0.1.67) - 8 official themes, colorblind accessibility, speaker preferences file, warm/cool consistency audit
- **UI/UX Enhancements** (v0.1.19, v0.1.69) - Panel/window persistence, toolbar icons, Help documentation, flowchart persistence
- **Native Flowchart View** (v0.1.50-v0.1.56, Epic #325) - Cross-platform flowchart visualization, multiple layouts (floating/side-by-side/tabbed), PNG/SVG export, zoom controls, bidirectional tree selection sync
- **Script Parameter System** (v0.1.39-v0.1.47, Epic #1, #207) - Debounced auto-save, parameter validation fixes, condition/action panel scrolling
- **Sound Browser Improvements** (v0.1.41) - BIF archive scanning, subdata folder traversal, invalid WAV handling
- **Undo/Redo Fixes** (v0.1.40-v0.1.42) - Dialog state restoration, parent reference fixes, deep clone improvements
- **Linux Compatibility** (v0.1.68) - Case-insensitive script preview, BIF sound playback with ffplay, streaming BIF reading
- **Quest Browser** (v0.1.70) - TextBox + Browse pattern for Quest Tag/Entry, QuestBrowserWindow, Open in Manifest integration
- **Shared GFF Parser** (v0.1.66) - JournalService now uses Radoub.Formats.Jrl for JRL parsing
- **Plugin System Refinements** (v0.1.27-v0.1.38) - FlaUI automation tests, graceful shutdown, A11y identifiers, settings persistence
- **FlowView Updates** (v0.1.54, v0.1.56) - Mouse drag panning, scroll zoom, theme-aware colors, SVG export layout fixes

For complete details, see git history or contact maintainer.


## Archive Summary (v0.1.0 - v0.1.13)

Major features added during early development (October - November 2025):

- **Initial Release** (v0.1.0) - Aurora DLG reading/writing, tree view editor, undo/redo, sound/script browsers, cross-platform support
- **LinkRegistry System** (v0.1.1) - Fixed critical copy/paste corruption, pre-save validation
- **Accessibility** (v0.1.3) - Color-blind friendly speaker visuals (shapes + colors)
- **Performance** (v0.1.4) - Lazy loading for TreeView, eliminated O(2^depth) scaling
- **Plugin Foundation** (v0.1.5, Epic 0) - Python plugins, gRPC IPC, process isolation, security model
- **Script Parameters** (v0.1.6, Epic 1) - Parameter browsing, caching, intelligent suggestions
- **UI/UX Start** (v0.1.8, Epic 2) - Font customization, layout redesign, Scrap Tab
- **Logging & Diagnostics** (v0.1.10, Epic 126) - Auto path sanitization, log level filtering
- **MainViewModel Refactoring** (v0.1.9-v0.1.13, Epic 99) - Service extraction, SOLID patterns

For complete details, see git history or contact maintainer.

---

**Development**: This project was developed through AI-human collaboration. See `../About/CLAUDE_DEVELOPMENT_TIMELINE.md` and `../About/ON_USING_CLAUDE.md` for the full development story.
