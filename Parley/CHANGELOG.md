# Changelog - Parley

All notable changes to Parley will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

---

## [Unreleased]

---

## [0.1.86-alpha] - 2025-12-23
**Branch**: `parley/sprint/script-browser-hak` | **PR**: #TBD | **Closes**: #516

### Sprint: Script Browser HAK Support (Epic #364)

Enable script discovery in HAK/ERF archives using Radoub.Formats ERF parsing.

#### Added
- TBD

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
