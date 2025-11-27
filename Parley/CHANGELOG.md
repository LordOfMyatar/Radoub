# Changelog - Parley

All notable changes to Parley will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

---

## [Unreleased]

---

## [0.1.28-alpha] - TBD
**Branch**: `parley/feat/sprint-185-sound-browser` | **PR**: #TBD

### Sound Browser & Audio Sprint (#185)

Improving sound browser usability with location override and mono audio filtering.

**Child Issues**:
- #167 - Filter mono-only WAV files in Sound browser for conversation compatibility
- #168 - Sound browser should search HAK files (deferred to Epic #170)

**Additional Work**:
- Add location override (browse...) to Sound Browser (same pattern as Script Browser)

---

## [0.1.27-alpha] - 2025-11-25
**Branch**: `parley/feat/script-browser-sprint` | **PR**: #198

### Script Browser Sprint

Improving script browser usability with location override and better error handling.

**Issues**:
- #144 - Script browser fails when game/module paths not detected
- #145 - Script browser cannot override location per-dialog

**Deferred**:
- #146 - Support browsing built-in game scripts (blocked by Epic #170 - BIF parser needed)

**Completed**:
- Script Browser now defaults to dialog file's directory (where .dlg and .nss co-locate)
- Added location override row to Script Browser window
  - Shows current search path (sanitized with `~` for privacy)
  - "browse..." button to select alternate script folder
  - "reset" button to return to dialog directory (appears after override)
  - Per-session override (resets when browser closes)
- Improved "no scripts found" messaging
  - When override set: "No scripts found in selected folder"
  - When no dialog loaded: "No dialog loaded - use browse..."
- Script preview loads from dialog directory or override path

---

## [0.1.26-alpha] - 2025-11-25
**Branch**: `parley/tech-debt/sprint-1` | **PR**: #191

### Tech Debt Sprint 1

Addressing accumulated tech debt issues and codebase cleanup.

**Completed**:
- #189 - Fix flaky UnifiedLoggerTests due to shared static state
  - Added `[Collection("UnifiedLogger")]` to prevent parallel test execution
  - Implemented `IDisposable` to clean up shared callback between tests
  - Created `TestCollections.cs` with collection definition
- #192 - Removed dead `ResourceSettings.cs` (284 lines)
- #193 - Removed unused methods from `SoundService.cs` (`SearchSounds`, `ValidateSound`, `GetSoundPath`)
- #195 - Removed unused `IsScriptInDialogDirectory` from `ExternalEditorService.cs`
- #181 - Menu cleanup:
  - Removed duplicate "Save" from Edit menu (already in File menu)
  - Moved "Clear Debug Output" into View > Logging submenu
  - Added Help > Documentation link (opens GitHub docs)
  - Added Help > Report Issue link (opens GitHub issues)
- #169 - Namespace consolidation:
  - Moved 12 service classes from `Parley.Services` to `DialogEditor.Services`
  - Removed all `using Parley.Services` statements
- Fixed spurious parser warnings for empty nodes:
  - Changed `CExoLocString has no LocalizedStrings and no StrRef` from WARN to DEBUG
  - Removed validation warnings for nodes without text (valid NWN "[CONTINUE]" pattern)

**Issues Found During Scan**:
- #192 - Dead code: ResourceSettings.cs completely unused
- #193 - Dead code: Unused SoundService methods
- #194 - Code duplication: LocString/DialogNode cloning
- #195 - Dead code: ExternalEditorService unused method
- #196 - Exception handling: Empty catch blocks and swallowed exceptions

**Remaining**:
- #136 - Review: Verify orphaned link children handling in DeleteNode
- #23 - Add file path validation and input sanitization

---

## [0.1.24-alpha] - 2025-11-24
**Branch**: `parley/feat/issue-9-command-line` | **PR**: #187

### Command Line Support (Issue #9)

New command line interface for power users and automation.

**Features**:
- Direct file loading: `Parley dialog.dlg`
- Safe mode: `Parley --safe-mode` (clean slate - backs up config folder)
- Screenplay export: `Parley --screenplay dialog.dlg` (outputs to stdout)
- Output to file: `Parley --screenplay -o output.txt dialog.dlg`
- Help: `Parley --help`

**Safe Mode Details**:
- Backs up `~/Parley` to `~/Parley.safemode` before launch
- App starts with factory defaults (no config = fresh state)
- Disables plugins via `PluginSettingsService.SafeMode`
- To restore: delete `~/Parley` and rename `~/Parley.safemode` back to `~/Parley`

**Implementation**:
- `CommandLineService` handles argument parsing and screenplay generation
- `WindowPersistenceManager.HandleStartupFileAsync()` loads command line files
- `Program.BackupConfigForSafeMode()` handles config folder backup
- `Program.AttachToParentConsole()` enables console output on Windows (WinExe apps)
- Screenplay format shows speaker: text for each dialog node

---

## [0.1.23-alpha] - 2025-11-23
**Branch**: `parley/feat/epic-39-ux-improvements` | **PR**: #174

### Epic #39: UI/UX Enhancements (Complete)

Comprehensive UX improvements including modeless dialogs, per-NPC speaker preferences, focus management, delete confirmation preferences, and properties panel state management.

**Modeless Settings Window** (Issue #20):
- Settings window now non-blocking (Show() instead of ShowDialog())
- Users can keep Settings open while working in main window
- Theme changes apply immediately without dialog dismissal
- Settings window closes automatically when main window closes

**NPC Speaker Visual Preferences** (Issues #16, #36):
- Per-tag color and shape preferences for NPC speakers
- Shape/Color ComboBoxes next to Speaker field in Node Text panel (single row layout)
- Preferences persist across sessions in ParleySettings.json
- Controls update automatically when switching nodes
- Disabled for PC nodes and Owner (empty speaker)
- Speaker TextBox shortened to 150px (15 character limit)
- Setting to disable NPC tag coloring (use theme defaults only)
- Immediate tree view refresh when setting toggled

**Delete Confirmation Preferences** (Issue #14):
- "Don't show this again" checkbox on delete confirmation dialog
- ShowDeleteConfirmation setting persists across sessions
- Skips dialog when disabled (no confirmation needed)

**Properties Panel State Management** (Issue #3):
- Properties panel disabled when no node selected
- Conversation settings remain enabled (ROOT-level properties)
- HasNodeSelected binding prevents confusion with empty state
- Clear visual indication of disabled state

**Focus Preservation**:
- Selection preserved after changing speaker preferences
- Tree view refresh no longer loses focus
- RefreshTreeViewColors(DialogNode) overload maintains selection
- _isPopulatingProperties flag prevents event loops

**Script Field Layout**:
- Script TextBoxes constrained to 180px width (15 char limit)
- Edit Script and Browse buttons positioned adjacent to TextBox
- Left-aligned layout prevents buttons from floating to right edge
- Parameter key/value fields constrained to 150px width

**GitHub Issues Created**:
- #175 - Epic: Focus Management
- #176 - Theme colors: Warm NPC / Cool PC consistency
- #177 - Theme backgrounds: Darker gray for light text visibility

---

## [0.1.22-alpha] - 2025-11-23
**Branch**: `parley/feat/scrollbar-improvements` | **PR**: #172

### Scrollbar Improvements (Issue #63)

Comprehensive scrollbar visibility and usability improvements across all panels.

**Global Scrollbar Enhancements**:
- Increased scrollbar width to 18px (vertical) and height to 18px (horizontal)
- Always visible by default (AllowAutoHide = False)
- User preference in Settings > UI Settings > Scrollbar Settings
- Preference applies dynamically without restart

**SettingsWindow Scrollbars**:
- All 6 tabs: VerticalScrollBarVisibility="Auto", HorizontalScrollBarVisibility="Disabled"
- Clear overflow indication on every tab

**MainWindow TreeView**:
- Added 20px bottom padding to prevent last item clipping

**MainWindow Properties Panel (Scripts/Node/Scrap tabs)**:
- Scripts tab: Fixed nested ScrollViewer conflicts
- All tabs: HorizontalScrollBarVisibility settings for consistency
- Added padding and margins for scrollbar clearance
- Script preview sections: Horizontal scrolling enabled for long code lines

**UI Sizing Improvements**:
- Reduced button MinWidth: 70→55px (-21%)
- Reduced Play button: 50→40px (-20%)
- Reduced Quest ComboBoxes: 200→150px (-25%)
- Total space savings: ~60-90px in Properties panel

---

## [0.1.21-alpha] - 2025-11-23
**Branch**: `parley/feat/autosave-improvements` | **PR**: #165

### Autosave Improvements (Issues #18, #62)

Non-intrusive autosave enhancements with configurable intervals, better visual feedback, and improved save architecture.

**Window Title Sync Fix** (#18):
- Fixed asterisk (*) persisting after auto-save completion
- Window title now updates immediately when file saved
- Force UI refresh using Dispatcher.UIThread.Post with DispatcherPriority.Send

**Configurable Autosave Interval**:
- Settings UI: 0-60 minute interval slider (0 = fast debounce mode)
- Fast debounce mode: 2 seconds after last change (default)
- Periodic mode: 1-60 minute fixed intervals
- Per-session persistence
- Live label updates showing current mode

**Visual Feedback Improvements**:
- Status bar "Last saved: [time]" indicator with timestamp
- Theme-compliant colors using SystemAccentColor
- Non-intrusive save progress feedback
- Clear indication of save completion

**Save Architecture Refactor** (Epic #163 pattern):
- Extracted DialogSaveService from MainViewModel (~125→35 lines, 72% reduction)
- Service handles orphan cleanup, validation, index fixing
- Returns SaveResult with success/status/error info
- Maintains all safety features (orphan removal, pointer validation, multi-format support)

**Quest System Consistency**:
- Quest and QuestEntry changes now trigger autosave
- Status messages include "saved" feedback matching other properties
- Improved UX consistency across all property types

### Testing
- Added DialogSaveServiceTests.cs with 14 comprehensive tests
- Input validation tests (null dialog, null/empty filepath)
- Orphan cleanup verification
- Round-trip integrity tests (.dlg and .json)
- Pointer index validation and auto-fix
- Error handling coverage
- All 14 tests passing ✅

### Enhancement Issues Filed (Future Work)
- #166: Quest system UI refactor (TextBoxes with browse buttons)
- #167: Mono audio filtering for conversation compatibility
- #168: HAK file support in sound browser

---

## [0.1.20-alpha] - TBD
**Branch**: `parley/refactor/mainwindow-cleanup` | **PR**: #164

### Epic #163: MainWindow.axaml.cs Refactoring Sprint

Systematic service extraction from 4,126-line code-behind file, following Epic #99 SOLID patterns.

**FINAL RESULT**: MainWindow.axaml.cs reduced from 4,126 → 2,603 lines (-1,523, -37%)

### Services Extracted

#### PropertyAutoSaveService (#155) - 245 lines
- Extracted 179-line god method with 16-case switch statement
- Strategy pattern with property handlers dictionary
- MainWindow reduction: -706 lines

#### ScriptParameterUIManager (#156) - 352 lines
- Consolidated ~400 lines of duplicated parameter UI logic
- Handles conditional and action parameters with auto-trim feedback
- MainWindow reduction: -82 lines (initial)

#### NodeCreationHelper (#157) - 229 lines
- Smart node creation with debouncing (Issue #76)
- Tree navigation and selection management
- Auto-focus workflow for rapid entry
- MainWindow reduction: -71 lines

#### ResourceBrowserManager (#158) - 214 lines
- Unified Sound/Creature browser patterns
- Recent tags management and session cache
- MainWindow reduction: -90 lines

#### KeyboardShortcutManager (#159) - 215 lines
- Data-driven keyboard shortcuts (20+ shortcuts)
- Interface-based handler pattern (IKeyboardShortcutHandler)
- Replaced 147-line nested if/switch method
- MainWindow reduction: -87 lines

#### DebugAndLoggingHandler (#160) - 311 lines
- Log export with ZIP creation (117 lines)
- Scrap management (restore/clear operations)
- Debug console operations
- MainWindow reduction: -187 lines

#### WindowPersistenceManager (#161) - 252 lines
- Window position and panel size persistence
- Screen boundary validation
- Debug settings and animation values restoration
- MainWindow reduction: -150 lines

#### Duplicate Code Cleanup - 221 lines removed
- UpdateConditionParamsFromUI (95 lines) → ScriptParameterUIManager
- UpdateActionParamsFromUI (84 lines) → ScriptParameterUIManager
- FindLastAddedNode + FindLastAddedNodeRecursive (42 lines) → NodeCreationHelper

### Other Improvements

#### Build Warnings Fixed (5 → 0)
- ✅ DebugLogger.cs:41 - Added null-conditional operator
- ✅ NodeCreationHeadlessTests.cs:114 - Added Assert.NotNull check
- ✅ CopyPasteHeadlessTests.cs:104, 135 - Added Assert.NotNull checks
- ✅ OrphanNodeCleanupTests.cs:187 - Added Assert.NotNull check

#### Dead Code Cleanup (#162)
- ✅ Removed abandoned inline editing code

### Testing
- All 231 tests passing
- 16 GUI tests skipped (expected)
- No functional changes (pure refactoring)

### Abandoned Work
- **Inline Text Editing for Tree View** (Branch: `parley/feat/epic-39-inline-text-v2`)
  - Attempted inline editing in tree view with F2 key support
  - Abandoned due to Avalonia focus management complexity vs. value-add mismatch
  - Existing properties-panel workflow sufficient for rapid dialog entry
  - Dead code cleanup tracked in #162

---

## [0.1.19-alpha] - 2025-11-20
**Branch**: `parley/feat/epic-108-inline-editing` | **PR**: #142

### Epic #39: UI/UX Enhancements (Issue #108)

UI state persistence improvements - panel sizes, window position, and debug panel relocation.

### Added
- **Panel Size Persistence**: Left/right panel widths and top/bottom panel heights persist across sessions
- **Window Position Persistence**: Window position and size restore correctly on application launch
- **Debug Panel in Settings**: "Show Debug Panel" moved from View menu to Settings > Logging section as checkbox
  - Applies immediately when toggled (no restart required)
  - Visibility state persists across sessions

### Changed
- Default left panel width increased from 700px to 800px for better proportions
- Debug panel visibility now controlled via Settings > Logging instead of View menu

### Fixed
- Window position restoration now works correctly (previously defaulted to top-left)
- Panel sizes persist properly via code-behind (GridLength bindings don't support TwoWay mode)
- Position saves blocked during window initialization to prevent overwriting saved values
- Screen bounds validation prevents window spawning off-screen

### Technical Details
- GridSplitter positions saved when Grid PropertyChanged fires
- Window position saves blocked during initial 500ms restore window
- Added screen bounds validation for multi-monitor setups
- Enhanced debug logging for position save/restore troubleshooting

### Known Limitations
- Inline editing for tree view node text not implemented (deferred to future release)

---

## [0.1.18-alpha] - 2025-11-20
**Branch**: `parley/feat/epic-39-ui-ux` | **PR**: #140

### Epic #39: Theme System with 8 Themes and Auto-Refresh

Complete theme system with 8 official themes, automatic tree view refresh, and accessibility-focused colorblind options.

### Added
- **8 Official Themes**:
  - **Light** - Clean, professional light theme (#FFFFFF)
  - **Dark** - Modern dark theme (#2D2D2D)
  - **VSCode Dark** - Faithful VSCode recreation (#1E1E1E)
  - **Fluent Light** - Microsoft Fluent Design System (#F3F3F3)
  - **Deuteranopia** - Red-green colorblind safe (#DDDDDD)
  - **Protanopia** - Red colorblind safe (#DDDDDD)
  - **Tritanopia** - Blue-yellow colorblind safe (#DDDDDD)
  - **Angry Fruit Salad** - Easter egg nightmare theme (Comic Sans MS!)
- **Theme System Features**:
  - Tree view auto-refreshes on theme change (no file close/reopen needed)
  - ThemeManager.ThemeApplied event for UI refresh notifications
  - MainViewModel.RefreshTreeViewColors() public method
  - Automatic theme file deployment (copies to build output)
  - Theme-aware PC/Owner color overrides in SpeakerVisualHelper
- **Theme Development Guide** (NonPublic/Theme_Development_Guide.md)
  - JSON manifest structure documentation
  - Color palette design guidelines
  - Font and spacing configuration
  - WCAG accessibility guidelines
  - Colorblind-safe palette examples

### Changed
- **PC/Owner Colors** restored to original defaults:
  - PC (Reply nodes): #4FC3F7 (light blue)
  - Owner (Entry nodes): #FF8A65 (orange)
  - Themes can override these for accessibility
- **Colorblind Theme Backgrounds**: Medium gray (#DDDDDD) with darker sidebar (#AAAAAA) for better contrast
- **Window Background**: Now uses dynamic ThemeBackground resource
- **App.axaml**: Added Window background style binding to ThemeBackground

### Fixed
- Theme colors now apply to window backgrounds (Closes #60)
- Tree view node colors update immediately on theme change
- Fluent control backgrounds properly themed
- Multi-NPC speaker colors working with theme system (Addresses #16, #36)

### Technical
- **Services/ThemeManager.cs**: Theme discovery, loading, application, and event firing
- **Models/ThemeManifest.cs**: JSON theme definition model
- **Utils/SpeakerVisualHelper.cs**: Theme override support for PC/Owner colors
- **Parley.csproj**: Build configuration to copy Themes folder

---

## [0.1.17-alpha] - 2025-11-19
**Branch**: `parley/refactor/epic-99-cleanup-dead-code` | **PR**: #138

### Epic #99: MainViewModel Refactoring - Phase 7 (Dead Code Removal & Service Extraction)

Remove deprecated orphan containerization code and extract index management and node cloning to dedicated services.

### Added
- **IndexManager service** (~280 lines) - Extracted from MainViewModel
  - RecalculatePointerIndices - Recalculates all pointer indices after list changes
  - UpdatePointersForMove - Updates pointers during move operations
  - ValidateMoveIntegrity - Validates pointer index integrity
  - PerformMove - Full move operation with index tracking
- **NodeCloningService** (~140 lines) - Extracted from MainViewModel
  - CloneNode - Deep clones dialog nodes with all properties
  - CloneNodeWithDepth - Recursive cloning with circular reference detection
  - CloneLocString - Deep copies localized string objects
- **ReferenceManager service** (~135 lines) - Extracted from MainViewModel
  - HasOtherReferences - Counts references to nodes for cut operations
  - DetachNodeFromParent - Removes pointer references from parent nodes
  - CollectReachableNodes - Recursively collects reachable nodes (Issue #82 lazy loading fix)
- **PasteOperationsManager service** (~220 lines) - Extracted from MainViewModel
  - PasteAsDuplicate - Handles paste/move operations with validation
  - PasteToRoot - Paste logic for ROOT level with type conversion
  - PasteToParent - Paste logic for normal parents with type checking
- **RestoreFromScrap moved to ScrapManager service** (~145 lines) - Extracted from MainViewModel
  - RestoreFromScrap - Handles scrap restoration with validation and pointer creation
  - RestoreResult - Result object for separation of concerns

### Removed
- **~1,698 lines of code** from MainViewModel:
  - **693 lines** of deprecated orphan containerization code:
    - FindOrphanedNodes(TreeViewRootNode) - Replaced by OrphanNodeManager.RemoveOrphanedNodes
    - CreateOrUpdateOrphanContainers - Replaced by ScrapManager (Scrap Tab)
    - DetectAndContainerizeOrphansSync - No longer needed (orphans removed at save time)
    - CreateOrUpdateOrphanContainersInModel - Replaced by ScrapManager
    - CollectReachableNodesForOrphanDetection - Duplicate of OrphanNodeManager logic
    - IsNodeInSubtree/IsNodeInSubtreeRecursive - Replaced by OrphanNodeManager helpers
    - FindParentEntry - No longer used
    - CollectDialogSubtree/CollectDialogSubtreeChildren - Duplicate traversal logic
  - **~77 lines** of never-called tree building methods:
    - MarkReachableEntries - Never called from anywhere
    - FindOrphanedNodes() (no-param version) - Never called
    - MarkReachable - Only called by dead FindOrphanedNodes()
  - **~240 lines** of index management code (moved to IndexManager service):
    - RecalculatePointerIndices - Moved to IndexManager
    - PerformMove - Moved to IndexManager
    - UpdatePointersForMove - Moved to IndexManager
    - ValidateMoveIntegrity - Moved to IndexManager
  - **~98 lines** of node cloning code (moved to NodeCloningService):
    - CloneNode - Moved to NodeCloningService
    - CloneNodeWithDepth - Moved to NodeCloningService
    - CloneLocString - Moved to NodeCloningService
  - **~96 lines** of reference management code (moved to ReferenceManager service):
    - HasOtherReferences - Moved to ReferenceManager
    - DetachNodeFromParent - Moved to ReferenceManager
    - CollectReachableNodes - Dead code (never called), removed
  - **~138 lines** of paste operations code (moved to PasteOperationsManager service):
    - PasteAsDuplicate - Moved to PasteOperationsManager
    - PasteToRoot - Extracted to PasteOperationsManager
    - PasteToParent - Extracted to PasteOperationsManager
  - **~356 lines** of scrap restoration code (moved to ScrapManager service):
    - RestoreFromScrap - Moved to ScrapManager.RestoreFromScrap
    - Validation logic extracted to RestoreResult pattern

### Changed
- **MainViewModel reduced from 2,956 to 1,258 lines (-1,698 lines, 57% reduction)**
- **GOAL ACHIEVED**: MainViewModel now 258 lines BELOW the 1,000 line target
- MainViewModel now uses _indexManager service for all index operations
- MainViewModel now uses _cloningService for all node cloning operations
- MainViewModel now uses _referenceManager for reference counting and detachment
- MainViewModel now uses _pasteManager for paste operations
- MainViewModel now uses _scrapManager.RestoreFromScrap for scrap restoration
- PasteAsDuplicate now returns PasteResult for better separation of concerns
- RestoreFromScrap now returns RestoreResult for better separation of concerns
- Added null check guard in CutNode to prevent null reference warnings

### Notes
- All orphan functionality now consolidated in OrphanNodeManager service
- All index management now consolidated in IndexManager service
- All node cloning now consolidated in NodeCloningService service
- All reference management now consolidated in ReferenceManager service
- All paste operations now consolidated in PasteOperationsManager service
- All scrap restoration now consolidated in ScrapManager service
- Git history preserves original implementations if needed
- No functional changes - purely refactoring and dead code removal

### Tests
- ✅ Build successful (0 errors, 1 warning - pre-existing in DebugLogger)
- ✅ All 231 tests passing (16 skipped GUI tests)

---

## [0.1.16-alpha] - 2025-11-18
**Branch**: `parley/refactor/epic-99-node-operations` | **PR**: #137

### Epic #99: MainViewModel Refactoring - Phase 6 (NodeOperationsManager)

Extract node add/delete/move operations from MainViewModel into dedicated service.

### Added
- **NodeOperationsManager** (~530 lines): Service for tree node manipulation operations
  - AddSmartNode - Smart node addition based on context
  - AddEntryNode - NPC dialog node addition
  - AddPCReplyNode - Player response node addition
  - DeleteNode - Node deletion with link checking and scrap management
  - MoveNodeUp/MoveNodeDown - Node reordering in parent's child list
  - FindParentNode - Parent node lookup
  - FindSiblingForFocus - Focus management after cut/delete
  - Private helpers: CheckForIncomingLinks, CollectNodeAndChildren, DeleteNodeRecursive, RecalculatePointerIndices
- **TreeNavigationManagerTests.cs**: 16 new unit tests for TreeNavigationManager service
- **Parley.Tests/README.md**: Comprehensive testing documentation (231 tests)

### Changed
- MainViewModel reduced from 3,265 to 2,933 lines (-332 lines)
- AddSmartNode, AddEntryNode, AddPCReplyNode delegate to NodeOperationsManager
- DeleteNode simplified from ~70 lines to ~45 lines
- MoveNodeUp/MoveNodeDown simplified from ~50 lines each to ~20 lines each
- FindSiblingForFocus delegates to NodeOperationsManager
- Removed duplicate methods: CheckForIncomingLinks, CheckNodeForLinks, DeleteNodeRecursive, FindParentNode, CollectNodeAndChildren

### Fixed
- **Orphan handling regression (2025-11-18)**: Restored PR #132 "evil twin" fix for orphaned link children
- **CollectReachableNodes**: Now skips child links (IsLink=true) to properly detect orphaned nodes
- **RemoveOrphanedNodes**: Called immediately after deletion (not deferred to save)
- **RemoveOrphanedPointers**: Called second time after RemoveOrphanedNodes to clean stale pointers
- **RefreshTreeView**: Made synchronous to ensure orphan removal reflects immediately in UI
- **TreeView lazy loading**: Orphaned nodes no longer appear via stale pointer references

### Tests
- ✅ All 231 tests passing (211 original + 16 TreeNavigationManager + 4 orphan regression)
- ✅ Build succeeds
- Added compatibility shim for DeleteNodeRecursive to support existing tests using reflection
- Added 4 regression tests for orphaned link children scenarios

### Documentation
- Updated CODE_PATH_MAP.md with DELETE PATH call chain
- Updated CLAUDE.md with Phase 6 completion status
- Created comprehensive test suite README

---

## [0.1.15-alpha] - TBD
**Branch**: `parley/refactor/epic-99-property-panel` | **PR**: #135

### Epic #99: MainViewModel Refactoring - Phase 5 (PropertyPanelPopulator)

Extract property panel population logic from MainWindow into dedicated helper class.

### Added
- **PropertyPanelPopulator** (~370 lines): Helper class for populating properties panel in MainWindow
  - PopulateConversationSettings - Dialog-level properties (PreventZoom, ScriptEnd, ScriptAbort)
  - PopulateNodeType - Node type display (NPC/PC with speaker info)
  - PopulateSpeaker - Speaker field and related controls
  - PopulateBasicProperties - Text, Sound, Comment, Delay fields
  - PopulateAnimation - Animation selection and loop checkbox
  - PopulateIsChildIndicator - Child/Link warning display
  - PopulateScripts - Action and conditional script fields with callbacks
  - PopulateQuest - Quest tag, entry, and preview fields
  - PopulateParameterGrids - Script parameter grids
  - ClearAllFields - Clear and disable all property controls

### Changed
- MainWindow reduced from 4,405 to 4,019 lines (-386 lines)
- PopulatePropertiesPanel method reduced from ~300 lines to ~40 lines
- Removed duplicate methods: PopulateParameterGrids, ClearPropertiesPanel
- Property population logic now organized into focused helper methods

### Tests
- ✅ All 211 tests passing
- ✅ Build succeeds

---

## [0.1.14-alpha] - TBD
**Branch**: `parley/refactor/epic-99-tree-navigation` | **PR**: #133

### Epic #99: MainViewModel Refactoring - Phase 4 (TreeNavigationManager)

Extract tree navigation and traversal functionality from MainViewModel into dedicated service.

### Added
- **TreeNavigationManager** (~270 lines): Tree navigation, state management, and traversal
  - FindTreeNodeForDialogNode - Recursive node search
  - SaveTreeExpansionState/RestoreTreeExpansionState - Simple expansion state
  - CaptureExpandedNodePaths/RestoreExpandedNodePaths - Path-based state (for undo/redo)
  - CaptureTreeStructure - Debug/logging tree representation

### Changed
- MainViewModel reduced from 3,484 to 3,265 lines (-219 lines)
- Replaced inline tree navigation methods with service calls
- Tree state management now centralized in dedicated service

### Tests
- ✅ All 211 tests passing
- ✅ Build succeeds

---

## [0.1.13-alpha] - TBD
**Branch**: `parley/refactor/epic-99-orphan-manager` | **PR**: #132

### Epic #99: MainViewModel Refactoring - Phase 3 (OrphanNodeManager)

Extract orphan pointer cleanup functionality from MainViewModel into dedicated service.

### Added
- **OrphanNodeManager** (~150 lines): Handles orphaned pointer removal after node deletions
- Deprecated orphan container methods documented in service (preserved for reference)

### Changed
- MainViewModel reduced from 3,551 to 3,474 lines (-77 lines)
- Replaced inline RemoveOrphanedPointers with service call
- Deprecated methods (FindOrphanedNodes, CreateOrUpdateOrphanContainers) preserved but no longer called

### Fixed
- **Orphaning Bug**: PC Reply nodes appearing at root level after incorrect restore workflow
- Added `RemoveOrphanedNodes()` to detect and remove nodes with no incoming pointers
- Cleanup runs automatically before save to ensure dialog integrity

### Notes
- Most orphan functionality deprecated in favor of ScrapManager
- This extraction focuses on active orphan cleanup (pointers + nodes)
- Large deprecated methods kept for historical reference

### Tests
- ✅ All 211 tests passing (added 5 orphan cleanup tests)
- ✅ 16 skipped (Issue #130 - expected)
- ✅ Build succeeds
- New tests: `OrphanNodeCleanupTests` (5 tests covering orphan detection/removal)

---

## [0.1.12-alpha] - TBD
**Branch**: `parley/refactor/epic-99-undo-manager` | **PR**: #131

### Epic #99: MainViewModel Refactoring - Phase 2 (UndoRedoService)

Extract undo/redo functionality from MainViewModel into dedicated service following Phase 1 patterns (DialogEditorService, DialogClipboardService).

### Added
- **UndoRedoService** (~165 lines): Wraps UndoManager with UI state handling (TreeState, status messages, result pattern)
- **TreeState** class moved to service for reusability across undo/redo operations

### Changed
- MainViewModel reduced from 3,568 to 3,551 lines (-17 lines)
- Undo/Redo methods refactored to use service result pattern
- Replaced `_undoManager` field with `_undoRedoService` for better separation of concerns

### Tests
- ✅ All 206 tests passing
- ✅ 16 skipped (Issue #130 - expected)
- ✅ Build succeeds

---

## [0.1.11-alpha] - 2025-11-16
**Branch**: `parley/feat/issue-81-gui-tests` | **PR**: #129

### Issue #81: GUI Test Coverage Expansion (Phase 1)

Avalonia.Headless test implementation for critical GUI workflows. Added 28 tests: **12 passing**, 16 skipped pending DialogNodes refresh mechanism investigation (Issue #130).

### Added
- **Avalonia.Headless Test Framework**: Upgraded from 11.2.2 to 11.3.6 (matches Avalonia 11.3.6)
- **Testing Documentation**: Comprehensive [TESTING_GUIDE.md](Documentation/Developer/TESTING_GUIDE.md) for Headless test patterns
- **12 Passing Headless Tests**:
  - **DialogLoadingHeadlessTests** (7/7): File loading, node creation, collection management
  - **NodeCreationHeadlessTests** (3/7): Entry creation, smart node logic, index management
  - **NodeDeletionHeadlessTests** (0/6): All access DialogNodes (skipped)
  - **CopyPasteHeadlessTests** (2/8): Null handling, graceful degradation
- **16 Skipped Tests** (Issue #130): Require DialogNodes ObservableCollection refresh trigger research

### Changed
- Upgraded `Avalonia.Headless.XUnit` from 11.2.2 to 11.3.6 for compatibility
- **Test suite total**: 222 tests (206 passing, 16 skipped)

### Documentation
- **GUI_TEST_EXPANSION_PLAN.md** (NonPublic): Comprehensive test planning for 50+ future tests
- **GUI_TEST_FINDINGS.md** (NonPublic): DialogNodes auto-update architectural discovery

---

## [0.1.10-alpha] - TBD
**Branch**: `parley/feat/epic-126-logging` | **PR**: #127

### Epic #126: Logging & Diagnostics

Comprehensive logging system improvements including log level filtering, structured logging, performance monitoring, and enhanced diagnostic capabilities.

### Added
- **Issue #87**: Automatic path sanitization in UnifiedLogger
  - Privacy by default - no manual `SanitizePath()` calls required
  - Heuristic-based path detection for Windows/Unix absolute paths
  - Auto-detects paths containing `\Users\`, `/Users/`, `/home/`
  - Sanitizes embedded user profile paths in log messages
  - 20 comprehensive tests covering all sanitization scenarios
  - 100% backward compatible with existing manual sanitization calls

- **Issue #89**: Settings files now use `~` for portability and privacy
  - ParleySettings.json stores paths with `~` instead of full home directory
  - Automatically expands `~` when loading settings
  - Automatically contracts paths when saving settings
  - Makes config files safe to share for support without exposing usernames
  - Portable across different user accounts and machines
  - 9 comprehensive tests for path expansion/contraction
  - Fully backward compatible with existing settings files

- **Issue #113**: Log level filtering in Debug tab
  - Added TRACE log level (most verbose) to UnifiedLogger
  - Filter dropdown in Debug tab: All/Error/Warning/Info/Debug/Trace
  - Real-time filtering of debug messages
  - Maintains all 1000 messages in memory, filters display only
  - Prepares for #110 log reclassification work

### Fixed
- **Privacy Improvement**: UnifiedLogger now automatically sanitizes all logged paths
  - Replaces user home directory with `~` in all log messages
  - Prevents accidental privacy leaks from forgotten manual sanitization
  - No developer cognitive load - works automatically

### Changed
- UnifiedLogger sanitization is now automatic and transparent
  - Existing `SanitizePath()` calls continue to work (harmless double-sanitization)
  - Path detection handles Windows (`C:\`), Unix (`/home/`), and UNC paths (`\\server\`)
  - HTTP/HTTPS URLs excluded from sanitization

---

## [0.1.9-alpha] - 2025-11-16
**Branch**: `parley/refactor/issue-99-mainviewmodel` | **PR**: #115

### Issue #99: MainViewModel Refactoring

Refactoring MainViewModel (3,501 lines) to improve maintainability and separation of concerns.

### Phase 2: Service Implementation Complete
- **Created DialogEditorService** (~320 lines):
  - AddSmartNode, AddEntryNode, AddPCReplyNode operations
  - DeleteNode with hierarchy tracking and scrap integration
  - MoveNodeUp/MoveNodeDown for reordering
  - Proper uint/int type handling for DialogPtr.Index
  - Index recalculation after modifications

- **Created DialogClipboardService** (~330 lines):
  - CopyNode with deep clone support
  - CutNode for move operations
  - PasteAsDuplicate with cut/copy distinction
  - PasteAsLink for reference creation
  - Recursive node cloning with circular reference handling

- **MainViewModel Integration Complete**:
  - Added service instances to MainViewModel
  - Refactored AddSmartNode, AddEntryNode, AddPCReplyNode to use DialogEditorService
  - Refactored MoveNodeUp/MoveNodeDown to use DialogEditorService for child nodes
  - Updated CopyNode and CutNode to use DialogClipboardService
  - Preserved undo/redo coordination in ViewModel
  - Kept complex DeleteNode logic in ViewModel (link checking, orphan detection)
  - Kept complex Paste logic in ViewModel (LinkRegistry, node type conversion)

- **Results**:
  - Services build successfully
  - Parley runs without errors
  - ~650 lines extracted into services
  - MainViewModel reduced from 3,501 to 3,361 lines (140 line reduction)
  - Clear separation of concerns achieved
  - Complex operations appropriately kept in ViewModel

### Fixed
- **Scrap Restore Bug**: Fixed issue where scrap entries were deleted even when restore failed
  - Separated `GetNodeFromScrap` (retrieves without removing) from `RemoveFromScrap`
  - Only removes from scrap after successful restoration
  - Validates restore target (e.g., PC Reply to root) before making ANY changes
  - No dialog modifications occur if validation fails (no orphaned nodes)
  - Clear user feedback when no parent selected or invalid restore target
  - Prevents loss of scrap entries when user hasn't selected a valid parent

- **Restore Button Enable/Disable**: Improved UI to prevent invalid restore attempts
  - Added `CanRestoreFromScrap` property that checks all preconditions
  - Restore button disabled when no tree node selected
  - Restore button disabled when no scrap entry selected
  - Restore button disabled when no dialog loaded
  - Prevents confusing error messages by disabling invalid actions upfront

- **Scrap File Isolation**: Fixed scrap entries showing across different files
  - Scrap now shows only entries for the current file
  - Scrap clears when creating new dialog
  - Scrap updates correctly after Save As operations
  - Each file maintains its own scrap entries

- **Dialog Structure Validation**: Enforced proper node placement rules
  - NPC Entry nodes cannot be children of other NPC Entry nodes
  - NPC Entry can only go to root or under PC Reply
  - PC Reply can go under NPC Entry or NPC Reply (branching PC responses)
  - Prevents invalid dialog structures that cause issues in Aurora

- **Orphan Container Removal**: Fixed orphaning visible in Aurora after deletions
  - Removed `DetectAndContainerizeOrphansSync()` call after node deletion
  - No longer creates orphan containers in dialog files
  - Deleted nodes now only stored in Scrap Tab (user-controlled recovery)
  - Prevents Aurora from displaying unexpected orphan containers
  - Aligns with Scrap Tab approach: users restore what they want, rest is pruned

- **Auto-select ROOT on File Load**: Improved initial UI state consistency
  - ROOT node automatically selected when file loads or new dialog created
  - Shows conversation settings panel immediately
  - Provides clear default context for restore and add operations
  - Eliminates "no selection" state that caused restore button confusion
  - TreeView SelectedItem now bound to ViewModel for programmatic control

- **Enhanced Restore Button Validation**: Restore button now validates dialog structure
  - Button disables when trying to restore PC Reply to root
  - Button disables when trying to restore NPC Entry under NPC Entry
  - Prevents silent validation failures - button state matches actual validity
  - `CanRestoreFromScrap` now performs same validation as `RestoreFromScrap`
  - Immediate visual feedback for invalid restore operations

- **Issue #121: Copy/Cut Consistency**: Made Copy and Cut operations behave consistently
  - Both now create deep clones immediately (no deferred cloning)
  - Clipboard content isolated from source modifications
  - Prevents subtle bugs from shared references
  - All clipboard tests passing (164 total)

- **Issue #111: Child Link Display**: Fixed child links not showing as gray/IsChild
  - TreeViewSafeLinkNode now properly passes sourcePointer to base class
  - IsChild property correctly reads pointer.IsLink flag
  - Child links now display in gray (matching NWN Toolset)
  - Properties panel correctly shows IsChild=1 for link nodes

- **Issue #111: Child Link Deletion Behavior**: Fixed parent node deletion when deleting child link
  - Deleting a child link now only removes the pointer (doesn't delete parent)
  - Parent nodes properly preserved in dialog
  - Automated test coverage added
  - Aligns with NWN Toolset behavior

### Added
- **Developer Documentation**:
  - `Dev_CopyPaste_System.md` - Clipboard architecture and CloneMap pattern
  - `Dev_Orphan_Scrap_System.md` - Orphan detection with Ctrl+Z vs Scrap comparison

### Changed
- **DialogClipboardService**: Refactored PasteAsLink to use service delegation
  - Moved link pointer creation logic from MainViewModel to service
  - Cleaner separation of concerns
  - Consistent with other clipboard operations

### Next Steps
- Complete method migration to services
- Extract tree management to DialogTreeService
- Remove redundant fields from MainViewModel
- Target: Further reduce MainViewModel to ~1,500 lines

---

## [0.1.8-alpha] - 2025-11-15
**Branch**: `parley/feat/epic-2-ui-ux` | **PR**: #114

### Epic 112: Scrap Tab - Node Recovery System

Implemented complete scrap tab functionality replacing broken orphan container system, following Aurora's user-controlled approach.

### Added
- **Issue #112**: Scrap Tab for deleted/cut node recovery
  - Stores deleted nodes in `~/Parley/scrap.json` instead of polluting DLG files
  - Theme-aware badge showing scrap count with system accent color
  - Node type labels: "NPC Entry", "PC Reply", "NPC Reply" for clarity
  - Hierarchy information showing parent relationships and nesting level
  - Restore functionality with parent selection (root or specific node)
  - Auto-cleanup of entries older than 30 days
  - Per-file scrap tracking with sanitized paths
  - Timestamp display ("just now", "5m ago", "2h ago", etc.)

- **View Menu Enhancements**:
  - "Open Log Folder" menu item (View → Logging)
  - "Export Logs for Support" with ZIP creation and folder open prompt
  - Reorganized debug-related items into "Logging" submenu

- **GitHub Issue #113**: Created issue for future log level filtering in Debug tab

### Fixed
- **Issue #112**: Scrap deserialization uint overflow
  - Changed `GetInt32()` to `GetUInt32()` for Delay and QuestEntry fields
  - Fixes "One of the identified items was in an invalid format" error

- **Font Size Inheritance**: Scrap tab header now properly inherits global font size
  - Explicit `FontSize="{DynamicResource GlobalFontSize}"` binding on TabItem element
  - "Scrap" label inherits from parent TabItem
  - Badge counter remains fixed at 10pt for readability

- **File Close Behavior**: Properties and scrap panels now clear when closing a file
  - `SelectedScrapEntry` cleared in `CloseDialog()` method

### Changed
- Scrap storage location: `~/Parley/scrap.json` (consistent with other settings)
- Comprehensive logging throughout ScrapManager for diagnostics

---

## [0.1.8-alpha] - 2025-11-15
**Branch**: `parley/feat/epic-2-ui-ux` | **PR**: #107

### Epic 2: UI/UX Enhancements (In Progress)

Comprehensive UI/UX improvements including font customization, layout redesign, enhanced themes, autosave enhancements, and visual feedback improvements.

**Note**: Epic 2 contains many work items. This release covers initial font customization and layout improvements. Additional Epic 2 work continues in subsequent releases.

### Added
- **Issue #58**: Font sizing now works globally across all UI elements
  - Global styles in App.axaml apply font size to all controls
  - Dynamic resource binding for instant font size updates
  - Font size changes apply immediately from Settings window slider
  - Font size changes apply from View menu options
  - Persists across sessions via SettingsService

- **Issue #59**: Font selection now available for custom UI fonts
  - Font family dropdown in Settings → Appearance with live preview
  - Detects and lists available system fonts
  - "System Default" option for platform-specific defaults
  - Real-time font preview showing sample text
  - Global application to all UI elements via dynamic resources
  - Persists across sessions via SettingsService
  - Graceful fallback to system default if font unavailable

- **Issue #108**: Layout redesign with 70/30 split and tabbed properties
  - Implemented Mockup 1 "Compact Properties Tabs" design
  - Main workspace (tree + text) gets 70% width, properties 30%
  - Tree to text vertical ratio: 2:1
  - Replaced Expander-based properties with TabControl for cleaner UI
  - Scripts and Node Properties now in separate tabs (right panel)
  - Debug output moved to third tab in properties panel
  - Type field (PC/NPC) converted to display-only TextBlock for font scaling
  - All layouts use proportional star sizing for accessibility (no hardcoded sizes)
  - DRAGON comments added to protect orphan handling and node relationship code
  - Clear button added to Debug tab with performance improvements

### Fixed
- **Font sizing improvements**: Removed all hardcoded font sizes
  - Module info bar now respects global font size
  - Debug output panel scales with font settings
  - Script preview windows use dynamic font sizes
  - Dialog tree headers scale properly
  - Popup windows (Script Browser, Parameter Browser) scale correctly
  - Button padding adjusts with font size for better touch targets
  - All TextBlocks now inherit from global font settings
- **UI overlap issues**: Fixed button overlapping in parameter sections
  - Corrected Grid.Row indices for suggest buttons (was 3, should be 2)
  - Increased popup window widths to accommodate larger fonts
  - Parameter Browser: 700→900 width, MinWidth 600→800
  - Script Browser: 900→1000 width, MinWidth 700→900

### Infrastructure Added
- **Layout Feature Flag**: Added UseNewLayout setting infrastructure for future layout experiments
- **UI Converters**: Added BoolToTextWrappingConverter and NotNullConverter for enhanced UI binding
- **Testing Tools Enhancements**:
  - Home directory expansion support (C:~\ and ~/ notation)
  - Path sanitization in test output for privacy
  - Updated test suite configuration (replaced missing files)
  - All automated tests passing (146 unit tests, 5 round-trip tests)

### Known Issues
- **Issue #110**: Log level reclassification needed (INFO too verbose)
- **Issue #111**: Second delete of node with parent/child links causes nodes to disappear

### Planned Features (Remaining Epic 2 Work)
- Flow chart view tabbed interface (#108 continued)
- Inline tree editing with preference setting (#108 continued)
- Panel size/position persistence (#108 continued)
- Enhanced theme system with plugin architecture (#60)
- Color-blind accessible themes (#61)
- Rainbow brackets for nesting visualization (#70)
- Autosave improvements (#62)
- Scrollbar usability enhancements (#63)
- Additional UX polish items

---

## [0.1.8-alpha] - 2025-11-15
**Branch**: `parley/feat/epic-2-ui-ux` | **PR**: #107

### Epic 2: UI/UX Enhancements (In Progress)

Comprehensive UI/UX improvements including font customization, layout redesign, enhanced themes, autosave enhancements, and visual feedback improvements.

**Note**: Epic 2 contains many work items. This release covers initial font customization and layout improvements. Additional Epic 2 work continues in subsequent releases.

### Added
- **Issue #58**: Font sizing now works globally across all UI elements
  - Global styles in App.axaml apply font size to all controls
  - Dynamic resource binding for instant font size updates
  - Font size changes apply immediately from Settings window slider
  - Font size changes apply from View menu options
  - Persists across sessions via SettingsService

- **Issue #59**: Font selection now available for custom UI fonts
  - Font family dropdown in Settings → Appearance with live preview
  - Detects and lists available system fonts
  - "System Default" option for platform-specific defaults
  - Real-time font preview showing sample text
  - Global application to all UI elements via dynamic resources
  - Persists across sessions via SettingsService
  - Graceful fallback to system default if font unavailable

- **Issue #108**: Layout redesign with 70/30 split and tabbed properties
  - Implemented Mockup 1 "Compact Properties Tabs" design
  - Main workspace (tree + text) gets 70% width, properties 30%
  - Tree to text vertical ratio: 2:1
  - Replaced Expander-based properties with TabControl for cleaner UI
  - Scripts and Node Properties now in separate tabs (right panel)
  - Debug output moved to third tab in properties panel
  - Type field (PC/NPC) converted to display-only TextBlock for font scaling
  - All layouts use proportional star sizing for accessibility (no hardcoded sizes)
  - DRAGON comments added to protect orphan handling and node relationship code
  - Clear button added to Debug tab with performance improvements

### Fixed
- **Font sizing improvements**: Removed all hardcoded font sizes
  - Module info bar now respects global font size
  - Debug output panel scales with font settings
  - Script preview windows use dynamic font sizes
  - Dialog tree headers scale properly
  - Popup windows (Script Browser, Parameter Browser) scale correctly
  - Button padding adjusts with font size for better touch targets
  - All TextBlocks now inherit from global font settings
- **UI overlap issues**: Fixed button overlapping in parameter sections
  - Corrected Grid.Row indices for suggest buttons (was 3, should be 2)
  - Increased popup window widths to accommodate larger fonts
  - Parameter Browser: 700→900 width, MinWidth 600→800
  - Script Browser: 900→1000 width, MinWidth 700→900

### Infrastructure Added
- **Layout Feature Flag**: Added UseNewLayout setting infrastructure for future layout experiments
- **UI Converters**: Added BoolToTextWrappingConverter and NotNullConverter for enhanced UI binding
- **Testing Tools Enhancements**:
  - Home directory expansion support (C:~\ and ~/ notation)
  - Path sanitization in test output for privacy
  - Updated test suite configuration (replaced missing files)
  - All automated tests passing (146 unit tests, 5 round-trip tests)

### Known Issues
- **Issue #110**: Log level reclassification needed (INFO too verbose)
- **Issue #111**: Second delete of node with parent/child links causes nodes to disappear

### Planned Features (Remaining Epic 2 Work)
- Flow chart view tabbed interface (#108 continued)
- Inline tree editing with preference setting (#108 continued)
- Panel size/position persistence (#108 continued)
- Enhanced theme system with plugin architecture (#60)
- Color-blind accessible themes (#61)
- Rainbow brackets for nesting visualization (#70)
- Autosave improvements (#62)
- Scrollbar usability enhancements (#63)
- Additional UX polish items

---

## [0.1.7-alpha] - TBD
**Branch**: `parley/fix/housekeeping` | **PR**: #106

### Housekeeping: Code Quality & Technical Debt

Comprehensive codebase cleanup addressing security, code quality, test failures, and technical debt. No new user-facing features.

### Fixed
- **Issue #94**: LazyLoadingPerformanceTests failures (4 tests) - commit 09d12ea
- **Issue #95**: Hardcoded paths and path handling security - commit a6ab3cd
  - Removed developer paths from ScriptService, MainViewModel, CompareDialogs
  - Fixed command injection risk in AudioService
  - Improved path validation and sanitization
- **Issue #96**: Silent exception handling with proper logging - commit 4493d80
- **Issue #97**: All compiler and Avalonia warnings (8 warnings) - commit 43a11b6
- **Issue #98**: Removed commented code and unused methods - commit b084f83
- **Issue #100**: Converted TODOs to GitHub issues - commit 3992472
  - Created Epic #101 - Complete Core Plugin APIs
  - Created issues #102-#105 for plugin API implementation
  - Updated font TODOs to reference Epic 2 issues

### Changed
- Updated CLAUDE.md with preventive guidelines for security and code quality - commit 9ae6d73
- Added "Code Quality & Security Guidelines" section to project docs
- Added GitHub workflow guidelines (magic keywords, CHANGELOG references) - commit 4d47732

### Related Issues
- #99 - Refactor MainViewModel (future work)
- #101 - Complete Core Plugin APIs (new epic, tracked issues #102-#105)

---

## [0.1.6-alpha] - TBD
**Branch**: `parley/feat/epic-1-parameters` | **PR**: #93

### Epic 1: Script Parameters (COMPLETE)

Parameter browsing, caching, and intelligent suggestions for dialog script parameters. Full cache UI integration complete with live refresh and visual indicators for cached values.

### Added
- **Issue #53**: Script parameter declaration parsing
  - Parses `----KeyList----` and `----ValueList----` blocks from NWScript comments
  - Supports keyed ValueList format: `----ValueList-KEYNAME----`
  - Parameter declarations can appear anywhere in script file
  - ScriptParameterParser extracts parameter hints from .nss files
  - ScriptParameterDeclarations model with Keys, Values, ValuesByKey
  - Comprehensive test coverage in ScriptParameterParserTests.cs
- **Issue #54**: Parameter suggestion helper buttons
  - 💡 "Suggest" buttons next to parameter add buttons (Actions and Conditions)
  - Launches Parameter Browser window showing available keys and values
  - Automatically loads declarations when scripts are selected/changed
  - Script preview panel shows first 30 lines of script source code
  - Preview updates when script names change in textboxes
  - Non-blocking UI - browser doesn't block main window
- **Issue #55**: Parameter value caching system
  - JSON-based cache: `parameter_cache.json`
  - Cross-platform cache location:
    - Windows: `%APPDATA%/Parley/parameter_cache.json`
    - macOS: `~/Library/Application Support/Parley/parameter_cache.json`
    - Linux: `~/.config/Parley/parameter_cache.json`
  - MRU (Most Recently Used) ordering
  - Configurable limits: MaxValuesPerParameter (5-50, default 10), MaxScriptsInCache (default 1000)
  - Cache statistics display in Settings → Parameters tab
  - "Clear Cache" and "Refresh Stats" buttons
  - Cross-session persistence (loads on startup, saves on changes)
  - ParameterCacheService singleton with thread-safe operations

### New UI Components
- **ParameterBrowserWindow**: Dual-pane parameter browser
  - Left pane: Parameter keys list (declarations + cached keys)
  - Right pane: Values for selected key (declarations first, cached second with 🔵 marker)
  - Values prioritize script declarations (curated, less likely typos) over cached values
  - "Copy Key", "Copy Value", "Add Parameter" buttons
  - "Enable Cache" checkbox with live refresh (show/hide cached values immediately)
  - "Clear Cache" button (clears cache for current script)
  - "Refresh Journal" button for journal data integration
  - Non-modal window design (doesn't block main window)
  - Dark mode selection visibility fixes
- **Settings → Parameters Tab**: Global cache management UI
  - Enable/disable parameter caching checkbox
  - Max values per parameter slider (5-50)
  - Max cached scripts slider (100-5000)
  - Cache statistics display (scripts cached, parameters cached, total values)
  - Clear all cache and refresh stats buttons

### New Services
- **ParameterCacheService**: Manages parameter value caching
  - AddValue(): Adds value to cache with MRU ordering
  - GetValues(): Retrieves cached values for parameter
  - ClearCache(): Clears all or per-script cache
  - GetCacheStats(): Returns cache statistics
  - Platform-specific cache file path resolution
- **ExternalEditorService**: Opens scripts in external editors
  - Detects VS Code, Notepad++, Sublime Text on Windows
  - Falls back to system default editor if no editor configured
  - Settings integration for custom editor path
  - FindScriptPath() locates .nss files in module directories
- **ScriptService Enhancements**: Script content caching
  - GetScriptContentAsync(): Loads and caches full script text
  - GetParameterDeclarationsAsync(): Caches parsed declarations
  - ClearCache(): Clears script content and declaration cache
  - GetCacheStats(): Returns cache statistics
  - Prevents redundant file reads for same scripts

###Technical
- Script declarations loaded on-demand when suggestion button clicked
- Script preview uses cached content to minimize file I/O
- Parameter browser integrates with JournalService for FROM_JOURNAL_ENTRIES support
- Cache integration: Browser merges script declarations (priority) + cached values (secondary)
  - Declarations first (less likely to contain typos)
  - Cached values marked with 🔵 when not in declarations
  - Marker automatically stripped when value selected
- Cache enable/disable toggles in browser with immediate UI refresh
  - RefreshKeysList() rebuilds keys based on EnableCaching state
  - RefreshValuesListForSelectedKey() updates values and header counts
- Cache saves automatically when values added (debounced)
- Window position persistence (main window location saved across sessions)
- Double-tap to toggle TreeView node expansion
- ScriptServiceCacheTests verify caching behavior

### Known Issues
- **Test Failures**: 5 tests currently failing (4 LazyLoadingPerformanceTests, 1 ScriptServiceCacheTests)
  - LazyLoadingPerformanceTests: Pre-existing failures from main branch (issue #82 follow-up)
  - ScriptServiceCacheTests: Test references missing script file, needs test data setup

### Documentation
- **Script_Parameter_Browser.md**: User guide for parameter browser
  - Parameter declaration format examples
  - Using suggestion buttons and browser window
  - Cache management instructions
  - Journal integration notes
- **parameter_example.nss**: Example script with parameter declarations
  - Demonstrates KeyList and ValueList formats
  - Shows keyed ValueList syntax
  - Includes FROM_JOURNAL_ENTRIES example

---

## [0.1.5-alpha] - 2025-11-09
**Branch**: `parley/feat/epic-0-plugins` | **PR**: #84

### Epic 0: Plugin Foundation

Complete plugin architecture with Python support, process isolation, and comprehensive security.

### Added
- **Issue #45**: Process isolation infrastructure
  - PluginProcess manages single plugin lifecycle with health monitoring
  - PluginManager coordinates multiple plugins
  - Cross-platform named pipes (Windows: npipe, Unix: unix:/tmp/)
  - 10-second health checks with automatic crash detection
  - 5-second gRPC timeout protection
- **Issue #46**: Plugin manifest system
  - JSON manifest validation (id, version, permissions, trust level)
  - Semantic version matching with operators (>=, ^, ~)
  - PermissionChecker enforces manifest-based permissions
  - Trust levels: official, verified, unverified
- **Issue #47**: Plugin discovery with MEF
  - Scans Official and Community plugin directories
  - Version compatibility validation (parley_version checks)
  - Trust level assignment based on source directory
- **Issue #48**: Core plugin APIs
  - AudioService: Play/stop audio with permission checks
  - UIService: Notifications and dialogs
  - DialogService: Read current dialog and selected node
  - FileService: Sandboxed file access (~/Parley/PluginData/)
  - All services use gRPC over named pipes
- **Issue #49**: Python bootstrap library
  - pip-installable `parley-plugin` package
  - Plugin base class with lifecycle hooks (on_initialize, on_shutdown)
  - Event handlers (on_dialog_changed, on_node_selected)
  - Service wrappers for all APIs with async/await
  - @requires_permission decorator for permission enforcement
  - Comprehensive documentation and examples
- **Issue #50**: Security implementation
  - RateLimiter: 1000 calls/minute per plugin per operation
  - SecurityAuditLog: Tracks all security events
  - PluginSecurityContext: Unified security enforcement
  - Integrated across all plugin services
  - Sandbox path validation prevents directory traversal
- **Issue #51**: Security testing framework
  - 46 security tests (100% passing)
  - PermissionEnforcementTests: 13 tests for permission system (includes case-insensitive matching)
  - SandboxTests: 6 tests for file sandboxing
  - RateLimitTests: 10 tests for rate limiting
  - TimeoutTests: 7 tests for timeout protection
  - MaliciousPluginTests: 10 tests for attack scenarios
- **Issue #85**: Plugin settings UI integration
  - New "Plugins" tab in Settings window
  - Lists all discovered plugins with trust level badges (Official/Verified/Unverified)
  - Enable/disable toggles per plugin with live preview
  - Safe Mode checkbox to disable all plugins on next launch
  - "Open Plugins Folder" and "Refresh Plugin List" buttons
  - Settings persist to ParleySettings.json
  - Plugin manager passed to SettingsWindow for live plugin scanning
- **Issue #86**: Plugin crash recovery system
  - Tracks plugin crash count and last crash timestamp per plugin
  - Auto-disables plugins after 3 crashes
  - CrashRecoveryDialog shown on startup after crash
  - Records loaded plugins during session for crash attribution
  - "Dirty shutdown" detection via lastSessionCrashed flag
  - Session lifecycle management (SetSessionStarted/SetSessionEnded)
  - Integrated with PluginManager.OnPluginCrashed handler
- **Issue #56**: Plugin documentation
  - Comprehensive Plugin_Development_Guide.md
  - Getting started guide with minimal working example
  - Detailed API reference (Audio, UI, Dialog, File)
  - Security documentation (permissions, rate limiting, sandbox)
  - Best practices and error handling patterns
  - Testing and troubleshooting guides
  - Distribution and versioning guidance
  - Python 3.10+ requirement (3.12+ recommended)

### Technical
- gRPC over named pipes for cross-platform IPC
- Permission-based security model (audio.*, ui.*, dialog.*, file.*)
- Wildcard permissions with category scoping
- Sandboxed file system (all operations restricted to plugin data directory)
- Rate limiting with sliding time windows
- Comprehensive security audit logging (permission denials, rate limits, sandbox violations, timeouts, crashes)
- Python async/await plugin development
- Minimal example plugin demonstrates basic structure
- All plugin infrastructure ready for Epic 2 (themes), Epic 3 (flowchart), Epic 7 (voice)

---

## [0.1.4-alpha] - 2025-11-08

### Fixed
- **Issue #28**: Undo operations no longer corrupt IsLink flags on DialogPtr
  - Fixed deep cloning to properly copy IsLink state from source pointers
  - TreeView expansion state now preserved correctly across undo/redo
  - Added 11 comprehensive undo/redo tests to prevent regression
- **Issue #82**: Lazy loading eliminates exponential performance degradation
  - TreeView children no longer auto-populated on creation (lazy loading)
  - Performance improved from O(2^depth) to O(visible nodes)
  - Eliminated "One Million Objects" memory pressure at depth 20+
  - Orphan node detection fixed to traverse dialog model instead of TreeView
  - Link nodes handled correctly as terminal nodes
  - Added 8 comprehensive lazy loading tests

### Technical
- TreeViewSafeNode.Children getter no longer auto-populates
- TreeViewSafeNode.IsExpanded setter populates children on-demand
- MainViewModel.CollectReachableNodes traverses DialogNode.Pointers
- TreeViewRootNode no longer auto-expands on creation
- HasChildren property checks underlying DialogNode.Pointers
- Added LazyLoadingOrphanDetectionTests.cs (3 tests)
- Added LazyLoadingPerformanceTests.cs (5 tests)
- All 70 tests passing (65 existing + 3 orphan + 5 performance - 3 duplicate)

---

## [0.1.3-alpha] - 2025-11-08

### Added
- **Issue #16**: Color-blind friendly speaker visual system
  - Shape + color combo for NPC identification
  - PC: Circle (blue), Owner: Square (orange)
  - Other NPCs: 4 shapes × 5 colors = 20 combinations (hash-assigned)
  - Fully accessible for protanopia, deuteranopia, tritanopia users
  - Shape icons display next to node text in tree view
  - SpeakerVisualHelper utility class for consistent visual assignment

### Fixed
- **Issue #25**: Eliminated all nullable reference warnings
  - Fixed 26 warnings in main production code
  - Fixed 14 warnings in test code
  - Improved null safety across DialogViewModel, ConversationManager, MainWindow
  - Added null-conditional operators and Assert.NotNull checks throughout
- **PreventZoom field bug**: Dialog zoom setting now persists correctly
  - Fixed field name mismatch: "PreventZoom" → "PreventZoomIn"
  - Changed from hardcoded 0 to actual dialog.PreventZoom value

### Technical
- **Issue #24**: Added comprehensive XML documentation to SpeakerVisualHelper
  - All public methods documented with param/return descriptions
  - Enum members documented for IntelliSense
  - Accessibility context included in summaries
- **Issue #22**: Added 23 comprehensive GFF parser tests
  - Field index mapping validation (4:1 Aurora pattern)
  - Struct type validation (root, entry, reply)
  - CResRef format validation
  - Circular reference detection
  - Malformed GFF security tests
- Fixed xUnit test warnings (blocking operations, assertion style)
- Test coverage significantly improved for binary format handling
- Disabled parallel test execution to prevent logger file conflicts
- Fixed HotU analyzer to skip when game files not present (CI compatibility)
- Removed non-functional duplicate workflows from Parley/.github/

---

## [0.1.2-alpha] - 2025-11-06

### Fixed
- **Issue #2**: Version now displays correctly in title bar
  - Removed hardcoded InformationalVersion from .csproj
  - GitVersion now sets semantic version during build (e.g., "v0.1.2-alpha")
  - Fixed issue where title bar showed "v1.0.0" instead of actual release version
- **Issue #7**: CTRL+D auto-expansion after collapse all
  - Added ExpandToNode() to expand all ancestors, not just immediate parent
  - New nodes now visible even after full tree collapse
  - Entire path from root to new node expands automatically
- **Issue #10**: PC (Reply) nodes no longer allow invalid Speaker tag selection
  - Disabled Speaker dropdown and Browse button for Reply nodes
  - Prevents confusing UI interaction with field that doesn't save
  - Enforces Aurora format rule: Reply nodes have no Speaker tag
- **Issue #17**: Delete ROOT node no longer shows confirmation dialog
  - Added ROOT check before showing dialog
  - Now displays status message only: "Cannot delete ROOT node"
  - Silent blocking as intended in original design
- **Issue #19**: All node properties disabled when ROOT selected
  - Properties panel now returns early for ROOT node
  - Conversation settings (prevent zoom, on end, on abort) remain enabled
  - Animation dropdown and all node-specific fields disabled for ROOT

### Technical
- VersionHelper.Version now reads AssemblyInformationalVersion attribute
- PopulatePropertiesPanel checks for ROOT node and returns early
- OnDeleteNodeClick checks for ROOT before showing confirmation dialog
- ExpandToNode() recursively expands all ancestor nodes
- FindParentNode() and FindParentNodeRecursive() helpers added for tree traversal

---

## [0.1.1-alpha] - 2025-11-04

### Fixed
- **CRITICAL FIX (Issue #6)**: Copy/paste operations with node links no longer cause file corruption
  - Implemented LinkRegistry system to track all node references and maintain correct indices
  - Copy/paste operations now properly update pointer indices when nodes are duplicated
  - Delete operations correctly update indices for remaining nodes
  - Added pre-save validation to detect and auto-fix index issues
  - Save operation aborts if corruption is detected to prevent data loss
- **Issue #27**: Fixed orphaned node duplicate detection in tree view
  - Orphaned nodes now display correctly with full subtrees instead of appearing as duplicates
  - Root cause: CollectDialogSubtree was marking nodes as visited before recursing, breaking chain collection
  - Split into entry point + helper method to properly collect all descendants
  - Verified behavior matches Aurora Toolset exactly
- **Delete Operation Bug**: Fixed issue where deleting nodes with shared replies incorrectly removed unrelated nodes
  - DeleteNodeRecursive now checks if child nodes are referenced by other parents before deleting
  - Shared reply nodes are preserved when still in use by other dialog entries
  - Prevents cascade deletion of nodes that are still needed by other conversation branches
- **Privacy**: Removed hardcoded user paths from test files

### Added
- Comprehensive test suite with xUnit for parser validation
- LinkRegistry system for tracking all DialogPtr references
- Pre-save safety validations to prevent corrupted files
- Automatic index recalculation using LinkRegistry
- Delete operation tests to verify shared node handling

### Technical
- Refactored copy/paste/delete operations to use AddNodeInternal/RemoveNodeInternal
- All pointer operations now register with LinkRegistry for tracking
- RecalculatePointerIndices now uses LinkRegistry for validation
- DeleteNodeRecursive enhanced to check for shared references before deletion
- CollectDialogSubtree split into entry point + CollectDialogSubtreeChildren helper for proper recursion

---

## [0.1.0-alpha] - 2025-11-02

### Initial Public Release

**Status**: Alpha - Use with backup copies of modules

### Added
- Aurora-compatible DLG file reading and writing
- Tree view conversation editor
- Node properties editing (text, speaker, listener, scripts)
- Add, delete, move nodes in conversation tree
- Undo/redo system (Ctrl+Z/Ctrl+Y)
- Sound browser (MP3/WAV/BMU from game and module directories)
- Script browser with parameter preview
- Creature tag selection (from UTC files)
- Journal/Quest integration
- Dark mode and light theme support
- Copy tree structure to clipboard
- Recent files menu
- Cross-platform support (Windows, Linux, macOS)
- Settings dialog for game/module paths
- Keyboard shortcuts for common operations
- Comprehensive logging system

### Known Issues
- Copy/paste with node links can cause file corruption
- Delete operations with multiple parent references require testing
- macOS/Linux Steam/Beamdog path auto-detection not implemented
- Some dialogs are modal (block main window)

### Technical
- Built with .NET 9.0 and Avalonia UI
- MVVM architecture pattern
- DialogFileService API for file operations
- Parser refactoring for maintainability
- Circular reference protection
- Session-based logging

---

**Development**: This project was developed through AI-human collaboration. See `../About/CLAUDE_DEVELOPMENT_TIMELINE.md` and `../About/ON_USING_CLAUDE.md` for the full development story.
