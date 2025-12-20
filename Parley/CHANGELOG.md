# Changelog - Parley

All notable changes to Parley will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

---

## [Unreleased]

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

## [0.1.70-alpha] - 2025-12-17
**Branch**: `parley/sprint/quest-browser` | **PR**: #446 | **Closes**: #166, #416

### Sprint: Quest Browser & Manifest Integration (#166, #416)

Replace Quest Tag/Entry ComboBoxes with TextBox + Browse pattern and add "Open in Manifest" button.

#### Changed
- **#166**: Quest Tag and Quest Entry fields now use TextBox with Browse button (like Sound/Script fields)
  - Users can manually type quest tags (faster for known values)
  - Browse button opens QuestBrowserWindow for discovery/selection
  - No need to load entire journal into memory at startup
  - Quest name looked up on-demand from JournalService cache

#### Added
- **#166**: QuestBrowserWindow for browsing and selecting journal entries
  - Two-pane layout: quests on left, entries on right
  - Search/filter by quest tag or name
  - Shows entry preview text and "Quest Complete" indicator
  - Double-click to select and close
- **#416**: "Open in Manifest" button in QuestBrowserWindow
  - Opens Manifest journal editor with selected quest/entry
  - Passes CLI args: `--file path.jrl --quest tag --entry id`
  - Button only enabled when quest is selected
  - Uses shared RadoubSettings for cross-tool path discovery
- Parley and Manifest now register their exe paths in `~/Radoub/RadoubSettings.json` on startup for cross-tool discovery

---

## [0.1.69-alpha] - 2025-12-16
**Branch**: `parley/sprint/ui-polish` | **PR**: #444

### Sprint: UI Polish (#443)

Small UI improvements for better UX.

#### Changed
- **#419 - Toolbar icons**: Added emoji icons to tree control buttons (📂 Expand All, 📁 Collapse All, ➕ Node, 🗑 Delete)
- **#371 - Documentation menu**: Help > Documentation now opens the User Wiki instead of the old documentation folder

#### Added
- **#377 - Flowchart persistence**: Flowchart state now saved across sessions
  - Floating window: Position and size remembered
  - SideBySide: Panel width (splitter position) remembered
  - All modes: Auto-reopens on app launch if was visible when closed
  - Settings: `FlowchartWindowLeft/Top/Width/Height`, `FlowchartPanelWidth`, `FlowchartVisible`

---

## [0.1.68-alpha] - 2025-12-16
**Branch**: `radoub/fix/linux-hak-and-script-preview` | **PR**: #440

### Fix: Linux Script Preview and Cross-Platform Compatibility

Fixes script preview not working on Linux due to case-sensitive filesystem.

#### Fixed
- **Script preview on Linux**: `ScriptService` now uses case-insensitive file matching
  - `Directory.GetFiles("MyScript.nss")` is case-sensitive on Linux (ext4, XFS)
  - Now uses `Directory.EnumerateFiles("*.nss")` with case-insensitive LINQ comparison
- **Script browser on Linux**: Same fix applied to `ScriptBrowserWindow`
- **PluginProcess.IsRunning**: Handle `InvalidOperationException` when checking `HasExited` on unstarted processes
- **BIF sound playback on Linux**: Sound files using IMA ADPCM format (0x0011) now play correctly
  - Changed Linux audio player preference order: `ffplay` → `paplay` → `aplay`
  - `aplay` only supports PCM/FLOAT formats, not ADPCM used by many NWN sounds
  - `ffplay` (FFmpeg) handles all formats including IMA ADPCM
- **Large BIF file handling**: Added streaming `BifReader.ReadMetadataOnly()` method
  - Avoids loading entire BIF files into memory when scanning for sounds
  - Uses on-demand extraction via `SourcePath` for resource data

#### Changed
- Test timing margins adjusted for VM environments (debounce tests)
- Path traversal tests updated for cross-platform behavior (backslash handling differs on Linux)

---

## [0.1.67-alpha] - 2025-12-15
**Branch**: `parley/sprint/theme-system-184` | **PR**: #439

### Sprint: Theme System (#184)

Final item to complete the Theme System sprint.

#### Changed
- #179 Separate NPC speaker preferences into dedicated config file
  - New file: `~/Parley/SpeakerPreferences.json` stores speaker visual preferences
  - Keeps main `ParleySettings.json` cleaner and focused
  - Automatic migration from existing settings on first load
  - Easier to share/backup speaker customizations between users

---

## [0.1.66-alpha] - 2025-12-14
**Branch**: `parley/sprint/shared-gff` | **PR**: #402

### Sprint: Use Shared GFF Parser (#398)

Update DialogEditor to use the shared JRL parser from Radoub.Formats.

#### Changed
- `JournalService` now uses `Radoub.Formats.Jrl.JrlReader` for JRL parsing
- Removed ~150 lines of manual GFF parsing from JournalService
- DialogEditor references Radoub.Formats shared library

#### Technical Notes
- DialogEditor keeps its own GFF types (GffStructures.cs, GffBinaryReader.cs) for DialogEditor-specific features
- JrlReader types converted to DialogEditor types via ConvertLocString()
- Future: Issue #403 tracks adding StrRef/TLK support for full internationalization

---

## [0.1.65-alpha] - 2025-12-14
**Branch**: `parley/feat/theme-color-audit-141` | **PR**: #393

### Feature: Theme Color Consistency Audit (#141)

Audit and fix PC/Owner color assignments across all themes for warm/cool consistency.

#### Audited (All Pass ✅)
- **Light**: `#FF8A65` (coral/warm) / `#4FC3F7` (cyan/cool) ✅
- **Dark**: `#FF8A65` (coral/warm) / `#4FC3F7` (cyan/cool) ✅
- **Fluent Light**: `#D83B01` (orange/warm) / `#0078D4` (blue/cool) ✅
- **Angry Fruit Salad**: `#FFFF00` (yellow) / `#00FFFF` (cyan) ✅

#### Fixed
- **VSCode Dark**: Changed `tree_entry` from `#4EC9B0` (teal) to `#CE9178` (tan/warm) - was both cool colors
- **Flowchart panel**: Now refreshes colors when theme changes (was not responding to theme switches)
- **Flowchart PC/Owner colors**: Fixed theme `tree_reply`/`tree_entry` colors not being applied:
  - Changed theme variant lookup from `Default` to `ActualThemeVariant` with brush fallback
  - Fixed DialogToFlowchartConverter passing `SpeakerDisplay` instead of raw `Speaker` tag (SpeakerVisualHelper expects empty string for Owner nodes)
- **Duplicate parameter validation**: Red border now clears properly when duplicate key is resolved
- **Validation colors**: Use theme error/success colors instead of hardcoded red/green for colorblind accessibility
- **Hardcoded colors audit**: Replaced hardcoded `Brushes.Red`/`Brushes.Green` in SettingsWindow and MainWindow with theme-aware error/success brushes

#### Previously Fixed (PR #367)
- Deuteranopia, Protanopia, Tritanopia

### Feature: Edit Mode Border Colors (#151)

Added theme-aware border colors for edit mode and auto-trim functionality.

#### Added
- `edit_mode_border`: Default editing state (cool tones)
- `edit_mode_unsaved`: Unsaved changes indicator (warm tones)
- `edit_mode_saved`: Saved state indicator (cool/green tones)
- `auto_trim_border`: Auto-trim active indicator

All 8 themes updated with appropriate colors following warm/cool conventions.

---

## [0.1.64-alpha] - 2025-12-14
**Branch**: `parley/sprint/stability-375` | **PR**: #376

### Sprint: Stability - Crash Investigation & Fixes (#375)

Fixed crashes in tree navigation by properly treating child/link nodes as terminal (bookmarks).

#### Fixed
- **Tree traversal crash**: All tree navigation methods now skip traversing child/link nodes - they are bookmarks pointing to nodes owned elsewhere
- **Null safety**: Added null checks and try-catch protection to `IsExpanded` setter and `PopulateChildrenInternal()`
- **Double-click crash**: Added error handling to `OnTreeViewItemDoubleTapped` (#374)
- **Flowchart-to-tree sync**: Fixed `FindTreeNodeForDialogNode`, `ExpandAncestors`, and other tree traversal methods to not traverse link nodes (#373)
- **Lazy load search**: Fixed `PopulateChildren()` to create children collection if null - tree search now works even on unexpanded nodes

#### Technical Details
- `IsChild` check added to 6 tree traversal methods in `TreeNavigationManager.cs`
- Same pattern as delete crash fix from #369 - links are terminal, don't traverse

#### Related Issues
- Fixes #373 - Flowchart bubble click doesn't expand tree
- Fixes #374 - Crash on double-click of tree node

---

## [0.1.63-alpha] - 2025-12-13
**Branch**: `parley/feat/epic-368-menu-reorganization` | **PR**: #369

### Epic: Menu Reorganization & Keyboard Shortcut Consistency (#368)

Menu cleanup and deduplication for better discoverability.

#### Changed
- **View menu reorganized**: Removed Font Size submenu (now in Settings only), added Settings... entry point
- **Edit menu slimmed**: Reorganized from 15 items to 10 top-level items
  - Tree operations (Move Up/Down, Expand/Collapse) moved to Edit > Tree submenu
  - Advanced copy operations moved to Edit > Copy Special submenu
  - Core operations (Undo/Redo, Cut/Copy/Paste, Add/Delete) remain at top level
- **Context menu updated**: Shortcuts now match Edit menu (Ctrl+Shift+Up/Down for move, not Alt+Up/Down)
- **Settings menu removed**: Consolidated into View > Settings... entry point

#### Fixed
- **Delete crash on complex dialogs**: `CheckNodeForLinks` was traversing link children (bookmarks), causing stack overflow on dialogs with many cross-references

#### Verified
- FlaUI keyboard shortcut tests pass (Ctrl+D, Ctrl+Z, Ctrl+Y all work in automation)
- Delete node works on xp2_valen.dlg (previously crashed)

---

## [0.1.62-alpha] - 2025-12-13
**Branch**: `parley/sprint/theme-polish-menu` | **PR**: #367

### Sprint: Theme Polish & Menu Cleanup (#363)

Theme system polish, menu organization, and UI improvements.

#### Fixed
- **#176 - Theme colors consistency**: Swapped NPC/PC colors in Deuteranopia and Protanopia themes
  - All colorblind themes now use warm colors for NPC, cool colors for PC
- **#177 - Theme contrast improvements**: Fixed colors for each colorblind condition
  - Deuteranopia/Protanopia: Orange `#B35900` (NPC), blue/teal `#007799` (PC)
  - Tritanopia: Replaced problematic blues with dark teals (`#2D6659`) - blue is indistinguishable for tritanopes
  - All tree and speaker colors now meet WCAG AA 4.5:1 minimum
- **#334 - Menu checkmark consistency**: Added checkmarks to Font Size menu
  - Font Size menu now shows ✓ next to current selection (consistent with Flowchart Layout menu)
  - Both menus initialize checkmarks correctly on window load
- **#197 - Copy Tree Structure uses screenplay format**: Refactored to reuse CLI screenplay generator
  - `SPEAKER: text` format with indentation for conversation flow
  - Shows NPC speaker tags when available, defaults to "NPC" or "PC"
  - Shared logic via `CommandLineService.GenerateScreenplay()`

---

## [0.1.61-alpha] - 2025-12-13
**Branch**: `parley/sprint/bug-squash-scrap-save` | **PR**: #365

### Sprint: Bug Squash - Scrap & Save Fixes (#362)

Quick wins fixing scrap panel bugs, UX improvements, and save validation.

#### Fixed
- **#352 - Scrap filtering**: Scrap panel now only shows entries for the current file
  - `AddToScrap`, `RemoveFromScrap`, and `ClearScrapForFile` now correctly filter entries
  - Previously showed all entries from all files
- **#356 - Undo removes from scrap**: When undoing a delete, restored nodes are removed from scrap
  - Added `RemoveRestoredNodes()` method to ScrapManager
  - Compares scrap entries against current dialog and removes matches
- **#353 - Terminal node display**: Empty nodes without children show `[END DIALOG]` instead of `[CONTINUE]`
  - Matches NWN Toolset behavior for identifying conversation endpoints
  - Updated TreeView DisplayText and FormattedDisplayText properties
  - Added test coverage for terminal vs non-terminal empty nodes
- **#289 - Block save with duplicate keys**: Manual save (Ctrl+S) now blocked when duplicate parameter keys exist
  - Added `HasAnyDuplicateKeys()` public method to ScriptParameterUIManager
  - Shows warning dialog explaining the issue
  - Prevents data corruption from duplicate key overwrites

---

## [0.1.60-alpha] - 2025-12-13
**Branch**: `parley/feat/cascade-delete-depth` | **PR**: #355

### Enhancement: Improve Cascade Delete Depth for Shared Nodes (#32)

Added comprehensive stress tests validating cascade delete at depths up to 500.

#### Added
- **CascadeDeleteStressTests.cs**: New test suite with:
  - Performance benchmarks at depths 10, 20, 50, 100
  - Linear tree deletion tests (no shared nodes)
  - Shared node tree deletion tests (verifies orphan cleanup)
  - External reference preservation tests (shared nodes with outside refs survive)
  - Stress tests at depth 500 (stack overflow protection)
  - Timing metrics for performance monitoring
- **DeepDialogGenerator.cs**: Test utility for creating deep dialog files
- **deep100_xref.dlg**: Test file with 100-depth chain and cross-references
- **deep20_xref.dlg**: Smaller test file for quick testing

#### Fixed
- **TreeView depth limit**: Increased from 50 to 250 to support dialogs up to depth ~125
  - Previously stopped rendering at depth 25 (Entry→Reply pairs count as 2 levels)
  - Now renders full depth-100 dialogs correctly

#### Changed
- **DeleteDeepTreeTests.cs**: Updated test documentation to clarify the difference between raw `DeleteNodeRecursive` (tested here) and full `DeleteNode` flow (tested in CascadeDeleteStressTests)

#### Verified
- Cascade delete works correctly at depth 100 with shared nodes
- Performance: depth 100 completes in ~30ms, depth 500 in ~35ms
- No stack overflow at extreme depths
- Shared nodes with external references correctly preserved
- Flowchart view works correctly with large/deep dialog files

---

## [0.1.59-alpha] - 2025-12-13
**Branch**: `parley/feat/custom-title-bar` | **PR**: #354

### Feat: Theme-aware Custom Title Bar (#139)

Custom title bar that matches the active theme instead of native Windows title bar.

#### Added
- Custom title bar with app icon and window title
- `ThemeTitleBar` and `ThemeTitleBarForeground` theme color properties
- Window drag support via title bar
- Double-click title bar to toggle maximize/restore
- Icon and title text scale with global font size setting

#### Changed
- All 8 theme JSON files updated with title bar colors
- Window uses `ExtendClientAreaToDecorationsHint` for custom chrome

---

## [0.1.58-alpha] - 2025-12-13
**Branch**: `parley/fix/dialog-nodes-refresh` | **PR**: #351

### Fix: DialogNodes ObservableCollection Refresh (#130)

Enabled 16 previously-skipped headless tests that verify DialogNodes tree updates correctly after node operations.

#### Changed
- **NodeCreationHeadlessTests.cs**: Fixed 7 tests with correct tree traversal
  - Added `GetFirstEntryNode()` and `GetEntryNodeAt()` helpers to access entry nodes via ROOT.Children
  - Added `IsExpanded = true` to trigger lazy loading before accessing children
  - Added `TreeViewPlaceholderNode` check to verify actual nodes vs placeholders
- **NodeDeletionHeadlessTests.cs**: Fixed 6 tests with correct tree traversal
  - Added same helper methods plus `GetRootChildrenCount()` for ROOT child counting
  - Fixed scrap tests to use relative count assertions (scrap file persists across runs)
- **CopyPasteHeadlessTests.cs**: Fixed 8 tests with correct tree traversal
  - Added `GetRootNode()` helper for paste operations (PasteAsDuplicate requires ROOT node, not null)
  - Added `IsExpanded = true` for PasteAsLink tests to populate reply children

#### Technical Notes
- Tree structure: `DialogNodes[0]` = ROOT node, entries are in `ROOT.Children[]`
- Lazy loading: Children are populated only when `IsExpanded = true` is set
- The DialogNodes refresh mechanism was already working correctly; tests were accessing tree nodes incorrectly

---

## [0.1.57-alpha] - 2025-12-13
**Branch**: `parley/fix/utc-slow-startup` | **PR**: #350

### Fix: UTC Reading Causes Slow Startup (#5)

Creature (UTC) files are now loaded lazily instead of during dialog open, improving startup performance.

#### Changed
- Removed synchronous UTC scanning from dialog load flow
- Creature loading now deferred until user opens the creature picker
- `ResourceBrowserManager` now accepts `getCurrentFilePath` callback for lazy loading
- `LoadCreaturesFromModuleDirectory` method removed from MainWindow

#### User Impact
- Faster dialog file opening, especially with large modules
- First creature picker open may show brief "Loading creatures..." message
- Subsequent creature picker opens use cached data (no delay)

### Fix: Saving to Read-Only File Fails Silently (#8)

Save operations now detect read-only files before attempting to write, with proper error dialogs.

#### Changed
- Added read-only file check in `DialogSaveService.SaveDialogAsync`
- `MainViewModel.SaveDialogAsync` now returns `bool` to indicate success/failure
- `OnSaveClick` shows error dialog with "Save As..." option when save fails
- `OnWindowClosing` now checks save result and offers Save As or Discard options
- `AutoSaveToFileAsync` shows ⚠ warning prefix in status bar on failure
- Added `ShowSaveErrorDialog` helper for consistent error presentation
- Extracted `ShowSaveAsDialogAsync` for reuse in close handler
- Added unit test for read-only file handling

#### User Impact
- File > Save now shows popup dialog with "Save As..." option when save fails
- Window close with unsaved changes offers Save As if normal save fails
- Auto-save shows visible ⚠ warning in status bar when it can't save
- No more silent failures or pretend saves - errors are always visible

---

## [0.1.56-alpha] - 2025-12-12
**Branch**: `parley/sprint/techdebt` | **PR**: #349

### Sprint: Parley Techdebt (#347)

#### Refactored - Node Creation Template (#344)
- Added `AddNodeWithUndoAndRefresh()` template method in MainViewModel
- Refactored `AddSmartNode()`, `AddEntryNode()`, `AddPCReplyNode()` to use template
- Reduced code duplication across node creation methods (~30 lines)

#### Refactored - Path Validation Helpers (#345)
- Added `ValidateBaseGamePath()` to ResourcePathHelper
- Added `AutoDetectBaseGamePath()` with Steam registry and common paths
- Added `PathValidationResult` record for UI feedback
- Added `ValidateGamePathWithMessage()`, `ValidateBaseGamePathWithMessage()`, `ValidateModulePathWithMessage()`
- Refactored SettingsWindow to use ResourcePathHelper methods (~70 lines removed)

---

## [0.1.55-alpha] - 2025-12-12
**Branch**: `parley/refactor/safe-control-finder` | **PR**: #348

### Refactor: MainWindow Tech Debt Sprint (#342, #343)

Option A sprint: MainWindow cleanup initiative.

#### Added - SafeControlFinder (#342)
- `SafeControlFinder` utility class for null-safe control access patterns
- Fluent API: `WithControl<T>()`, `WithControls<T1,T2>()` (up to 4 controls)
- Shorthand helpers: `SetText()`, `GetText()`, `SetChecked()`, `SetEnabled()`, `SetVisible()`
- Optional control caching for frequently-accessed controls
- 22 new unit tests for SafeControlFinder

#### Added - WindowLifecycleManager (#343)
- `WindowLifecycleManager` class for centralized window tracking
- Fluent API: `GetOrCreate<T>()`, `ShowOrActivate<T>()`, `WithWindow<T>()`
- Automatic Closed event handling and reference cleanup
- `WindowKeys` constants for well-known windows
- 15 new unit tests for WindowLifecycleManager

#### Changed
- MainWindow now uses `_controls` SafeControlFinder instance
- MainWindow now uses `_windows` WindowLifecycleManager for Settings and Flowchart windows
- Refactored `SaveCurrentNodeProperties()` to use fluent API (12 controls → cleaner lambdas)
- Refactored flowchart layout methods to use SafeControlFinder
- Refactored Settings window handlers to use WindowLifecycleManager
- Reduced window field declarations from 5 to 2 (browser windows retained due to complex result handling)
- Reduced FindControl calls from 109 → 87

#### Fixed
- F5 shortcut now properly opens flowchart (#339)

#### Documentation
- Updated README: Flowchart is now a native feature, not a plugin (#346)
- Removed flowchart-view plugin from Available Plugins table
- Added native flowchart to New Features list

#### Technical
- SafeControlFinder handles Avalonia's type mismatch exceptions gracefully
- WindowLifecycleManager supports custom onClosed callbacks for theme reloading
- Pattern enables coordinated multi-control updates with single null check
- 37 new tests total (22 + 15)

---

## [0.1.54-alpha] - 2025-12-12
**Branch**: `parley/sprint/flowchart-polish` | **PR**: #341

### Sprint: Flowchart Polish

Addresses post-release issues from Epic #325 (Native Flowchart View).

#### Fixed
- Scrollbars don't update when zooming (#336)
- SVG export layout doesn't reflect conversation flow (#338)
- Flowchart node colors don't follow theme or NPC overrides (#340)

#### Added
- Mouse drag panning support (#337)

---

## [0.1.53-alpha] - 2025-12-12
**Branch**: `parley/sprint/native-flowchart-s4-layout-export` | **PR**: #333

### Sprint 4: Layout Options & Export (#329)

Epic #325 - Native Flowchart View (Cross-Platform)

#### Added
- **Flexible layout modes** for flowchart visualization:
  - Floating Window (default): Opens flowchart in separate window (F5)
  - Side-by-Side: Embeds flowchart panel in resizable column beside tree
  - Tabbed: Adds flowchart as tab in properties panel area
- FlowchartLayout setting in SettingsService (persisted to settings.json)
- ROOT node in flowchart (shows dialog filename, all starting entries branch from ROOT)

#### Fixed
- Selection feedback loop causing NPC nodes to not update property panel
- Flowchart sibling node ordering (first-evaluated nodes now leftmost, matching reading order)
- View → Flowchart → Layout submenu with checkmark indicators
- **Flowchart export**:
  - Export as PNG (renders current graph panel at 96 DPI)
  - Export as SVG (generates structured vector diagram)
- View → Flowchart → Export submenu

#### Changed
- Extracted FlowchartPanel as reusable UserControl
- FlowchartWindow now thin wrapper around FlowchartPanel
- Refactored node click handling to support embedded panels

#### Technical
- FlowchartPanel.axaml: Reusable graph visualization component
- FlowchartExportService: PNG/SVG export with sanitized paths
- MainWindow: Dynamic column visibility for side-by-side mode
- FlowchartPanelViewModel: Exposed FlowchartGraph for SVG export

---

## [0.1.52-alpha] - 2025-12-10
**Branch**: `parley/sprint/native-flowchart-s3-polish` | **PR**: #332

### Sprint 3: Native Flowchart Visual Polish & Interaction (#328)

Epic #325 - Native Flowchart View (Cross-Platform)

#### Added
- Speaker-based node colors using SpeakerVisualHelper (matches TreeView colors)
- Theme-aware node backgrounds (light/dark mode support via ActualThemeVariant binding)
- Script indicators: ❓ for conditions (ScriptAppears), ⚡ for actions (ScriptAction)
- Link node visual distinction (opacity 70%, gray background)
- Bidirectional selection sync:
  - Flowchart → TreeView: Click node to select in tree
  - TreeView → Flowchart: Tree selection highlights flowchart node
- Zoom controls toolbar (+, -, Reset, Fit buttons)
- Mouse wheel zoom with Ctrl key
- Keyboard shortcuts: Ctrl+Plus/Minus for zoom, Ctrl+0 for reset

#### Fixed
- Nodes only reachable via links now appear in flowchart (previously missing)
- Link nodes no longer create layout edges to targets (fixes inverted parent/child positioning)
- Clicking link nodes in flowchart now selects the correct link instance in tree (not the target)
- Orphan nodes (one-liners with no children) now appear in flowchart
- Clicking link node in flowchart now highlights the link itself (not parent)

---

## [0.1.51-alpha] - 2025-12-10
**Branch**: `parley/sprint/native-flowchart-s2-rendering` | **PR**: #331

### Sprint 2: Native Flowchart Basic Rendering (#327)

Epic #325 - Native Flowchart View (Cross-Platform)

#### Added
- `FlowchartWindow.axaml` - Dedicated window for flowchart visualization
- `FlowchartPanelViewModel` - MVVM ViewModel with graph binding
- `FlowchartGraphAdapter` - Converts FlowchartGraph to AvaloniaGraphControl.Graph
- `FlowchartConverters` - IMultiValueConverters for node styling (background/border colors)
- View → Flowchart menu item (F5 shortcut)
- Node DataTemplates: Entry (orange), Reply (blue), Link (gray)
- Sugiyama hierarchical layout via AvaloniaGraphControl

---

## [0.1.50-alpha] - 2025-12-10
**Branch**: `parley/sprint/native-flowchart-s1-foundation` | **PR**: #330

### Sprint 1: Native Flowchart Foundation (#326)

Epic #325 - Native Flowchart View (Cross-Platform)

- Add AvaloniaGraphControl NuGet package
- Create FlowchartNode, FlowchartEdge, FlowchartGraph data models
- Implement DialogToFlowchartConverter service
- Unit tests for data transformation

---

## [0.1.49-alpha] - 2025-12-09

### Fix: macOS ARM64 Build - Include Both WebView Packages (#314)

**Problem**: macOS ARM64 builds failing with "libEGL.dylib not found". Conditional package references based on `$(RuntimeIdentifier)` don't work because RID isn't set during NuGet restore.

**Fix**: Include BOTH `WebViewControl-Avalonia` (x64) and `WebViewControl-Avalonia-ARM64` packages unconditionally. MSBuild resolves the correct RID-specific native libraries at build time.

---

## [0.1.48-alpha] - 2025-12-09

### Fix: macOS ARM64 Build - WebView Package Resolution (#314)

**Problem**: macOS ARM64 builds failing with "libEGL.dylib not found" during build step.

**Root Cause**: Package reference condition used `RuntimeInformation.OSArchitecture` which detects the build machine, not the target. GitHub's macOS runners are ARM64 (M1), causing wrong package selection.

**Fix**: Changed WebView package conditions to use `$(RuntimeIdentifier)` MSBuild property:
- When building with `-r osx-arm64`, uses `WebViewControl-Avalonia-ARM64`
- When building without RID or with x64/win, uses `WebViewControl-Avalonia`

---

## [0.1.47-alpha] - 2025-12-09

### Fix: macOS Build Failure (#314)

**Problem**: v0.1.46 broke macOS builds with "libEGL.dylib not found" error.

**Root Cause**: Removing `PublishSingleFile` globally to fix CEF subprocess on Windows broke native library bundling on macOS.

**Fix**: Split publish steps by OS:
- **Windows**: No `PublishSingleFile` - CEF subprocess requires separate DLL files
- **macOS/Linux**: Keep `PublishSingleFile` with `IncludeNativeLibrariesForSelfExtract` for native library bundling

---

## [0.1.46-alpha] - 2025-12-09

### Fix: CEF Subprocess Missing in Release Builds (#314)

**Problem**: Flowchart plugin crashes on launch in release builds with "Xilium.CefGlue.BrowserProcess.dll not found".

**Root Cause**: `PublishSingleFile=true` bundled CEF subprocess files into the main EXE, but CEF spawns `Xilium.CefGlue.BrowserProcess.exe` as a separate process which couldn't find its DLLs.

**Fix**: Removed `PublishSingleFile` from release workflow. CEF requires its subprocess files to remain as separate files. Trade-off: larger download size but plugins work correctly.

---

## [0.1.45-alpha] - 2025-12-08

### Fix: WebView Crash on Close (#314)

> *"I never look back, darling. It distracts from the now."* — Edna Mode
>
> Versions 0.1.43 and 0.1.44 were retracted due to this bug. We move forward.

**Problem**: Parley crashes (KERNELBASE.dll access violation) when closing with the flowchart plugin panel open. CEF/Chromium shutdown was racing with app exit.

**Root Cause**: `PluginPanelManager.CloseAllPanels()` used async `Dispatcher.UIThread.Post()` which returned immediately while WebView disposal was still in progress. App exited before CEF could clean up.

**Fix**: Changed to synchronous `Dispatcher.UIThread.Invoke()` to ensure each plugin panel window fully closes before continuing with app shutdown.

---

## [0.1.42-alpha] - 2025-12-08
**Branch**: `parley/fix/311-undo-redo-parent` | **PR**: #312

### Fix: Redo Not Restoring Dialog State (#292)

**Issue**: #292 - Redo operation fails to fully restore dialog state after undo

**Problem**: After undo/redo, the dialog was missing nodes (~300 bytes difference in file size). The `DeepCloneDialog()` method was not setting `Parent` references on cloned `DialogNode` and `DialogPtr` objects, causing the `LinkRegistry` to fail when rebuilding relationships.

**Solution**:
- Added `Parent = parentDialog` to `CloneNodeWithoutPointers()` for DialogNode cloning
- Added `Parent = parentDialog` to `ClonePointers()` for DialogPtr cloning
- Added `Parent = clone` to start pointer cloning

**Tests**: 314 unit tests passing, UI tests stable

---

## [0.1.41-alpha] - 2025-12-07
**Branch**: `parley/fix/sound-browser-220` | **PR**: #291

### Fix: Sound Browser Resource Scanning (#220)

**Issue**: #220 - Sound Browser: Missing BIF scanning and subdata folder traversal

**Problem**: The Sound Browser was not finding many base game sounds because:
1. BIF archives (containing most base game sounds) were not being scanned
2. Language-specific data folders (`lang/XX/data/`) were not traversed
3. HAK files in the game's `data/` folder were not scanned

**Solution**:

#### BIF Archive Scanning
- Added "BIF archives" checkbox to toggle scanning (can be slow for large installs)
- Added KEY file parsing to index BIF archive contents
- Scans `nwn_base.key` (NWN:EE) or `chitin.key` (Classic NWN)
- Extracts WAV resources from BIF files on demand
- Caches KEY and BIF data for faster subsequent scans
- Base game sounds display with 🎮 icon and "BIF:filename" source
- Legend updated to show both 🎮 (BIF) and 📦 (HAK) icons

#### Subdata Folder Traversal
- Now scans all `lang/XX/data/` folders for language-specific resources
- Finds HAK and loose sound files in language folders
- Supports all NWN:EE language installations (en, de, fr, es, it, pl)

#### Game Data HAK Scanning
- Scans HAK files in the game's `data/` folder
- Previously only scanned user's `hak/` folder and dialog directory

**Technical Details**:
- Uses Radoub.Formats KEY/BIF parsers for archive scanning
- BIF sounds are extracted to temp files for playback/validation
- Sound count now shows separate BIF and HAK totals
- Full mono/stereo validation available for extracted BIF sounds

### UX: Invalid WAV Playback Handling

- Invalid WAV files (non-standard format) now show ❌ icon in sound list
- Status bar shows warning when selecting invalid WAV files
- Play button re-enabled after playback error (was stuck disabled)
- Error message includes format details when playback fails

### UX: Sound Browser Settings Persistence

- Checkbox states (Game resources, HAK files, BIF archives) now persist across sessions
- Sound Browser opens with previously selected source filters
- Prevents unnecessary scanning when user only wants specific sources

### Infrastructure: Release Workflow Fix

- Fixed GitVersion action version mismatch in release workflow
- Updated from `gittools/actions@v4.2.0` with `versionSpec: '5.x'` to `@v3` with `versionSpec: '6.x'`
- Resolves release build failures on tag push

---

## [0.1.40-alpha] - 2025-12-07
**Branch**: `parley/fix/param-panel-scroll` | **PR**: #286

### Fix: Condition Parameters Panel Scrollbar (#278)

**Issue**: #278 - Condition parameters panel needs scrollbar or auto-sizing

**Problem**: When a script has many parameters, the condition/action parameters panel does not show a scrollbar. Users cannot access parameters that extend beyond the visible area (~4-5 rows max).

**Root Cause**: ScrollViewer had `MaxHeight="120"` constraint that limited scrollable area, preventing access to parameters beyond the visible rows.

**Fix**:
- Removed `MaxHeight` from both Conditions and Actions parameter ScrollViewers
- Changed middle row from `Height="Auto"` to `Height="*"` to fill available space
- Parent Border's `MaxHeight="200"` now controls overall panel height
- ScrollViewer can now scroll through all parameters within the Border's bounds

### Fix: Parameter Save Reliability (#287)

**Issue**: #287 - Parameters not fully saved when focus leaves node

**Problem**: When adding parameters and switching nodes, not all parameters were being saved correctly.

**Root Cause**: `ProcessParameterPanel` was accessing Grid children by index, which may not match visual column order in all cases.

**Fix**:
- Changed `ProcessParameterPanel` to find TextBox children by type using `OfType<TextBox>()`
- Added duplicate key validation with persistent red border (stays until corrected)
- All duplicate key textboxes are highlighted simultaneously
- Warning shown in status bar with ⚠️ icon
- **Critical**: Save is now BLOCKED when duplicate keys exist (prevents data corruption)
- Status bar shows "⛔ Cannot save: Fix duplicate keys first!" when blocked
- Added logging to track parameter processing for debugging

### UX: Delete Button Improvements

- Changed delete button from `×` to bold `X` for better legibility
- Fixed button centering (was off-center/clipped)
- Added right margin to parameter panel to prevent scrollbar overlap

### UX: Parameter Row Auto-Scroll & Focus

- When adding a new parameter row, ScrollViewer automatically scrolls to show the new row
- Key textbox automatically receives focus for immediate typing
- Works for both Conditions and Actions parameter panels

---

## [0.1.39-alpha] - 2025-12-06
**Branch**: `parley/fix/undo-redo-polish` | **PR**: #285

### Bug Fixes: Undo/Redo Polish (#253, #252)

**Work Items**:
- [x] #253 - Possible blank/no-op undo entries being stored
- [x] #252 - Redo (Ctrl+Y) does not auto-expand parent nodes when restoring deleted children

**Fixed (#253)**:
- Undo state now only saved when field value actually changes (not on focus alone)
- Tracks original value on focus, compares on blur before pushing to undo stack
- Eliminates spurious "blank" undo entries that required extra Ctrl+Z presses

**Fixed (#252)**:
- Tree state (selection, expansion paths) now stored WITH each undo state
- On undo: restores to tree state BEFORE the action (shows what you were looking at)
- On redo: restores to tree state AFTER the action (shows what you saw after)
- Fixed lazy loading issue where `Children.Count` was checked before children were populated
- All tree traversal methods now use `HasChildren` property and call `PopulateChildren()`

**Technical Changes**:
- `UndoState` class now stores `SelectedNodePath` and `ExpandedNodePaths`
- `UndoManager.Undo/Redo` now return `UndoState` (was `Dialog`)
- Updated unit tests for new return type

**Tests Updated**:
- Updated UndoRedoTests and UndoStackLimitTests for `UndoState` return type
- Total test count: 314 passing (16 skipped GUI tests)

---

## [0.1.38-alpha] - 2025-12-03
**Branch**: `parley/feat/epic-40-phase4-export` | **PR**: #277

### Epic 40 Phase 4: Export Features (#238-#240)

Fourth phase of the ChatMapper-style flowchart view plugin (Epic 3).

**Work Items**:
- [x] #238 - PNG export
- [x] #239 - SVG export
- [ ] #240 - Drag-drop node repositioning (deferred - requires tree view implementation first)

**Implemented (#238, #239)**:
- Added SVG and PNG export buttons to flowchart toolbar
- Export generates proper self-contained files with embedded styles
- PNG exports at 2x resolution for high quality
- Theme-aware exports (matches current dark/light mode)
- File save dialog with dialog name as default filename
- Chunked data transfer for large flowcharts (>20KB base64)
- JavaScript-side rendering with canvas API for PNG conversion

**Improved: Node Text Visibility**:
- Redesigned node styling: thick colored border with theme background
- Speaker/type color now shows as 4px border instead of fill
- Text always uses theme colors against theme background (readable in all themes)
- Updated legend to match new border-style node representation

**Improved: Node Content Display**:
- Word wrap with dynamic node heights (64 chars default, configurable)
- Speaker shape icons (Circle, Square, Triangle, Diamond, Pentagon, Star)
- Shapes sync with Parley preferences via extended gRPC API
- Increased speaker tag display from 10 to 24 characters

---

## [0.1.37-alpha] - 2025-12-02
**Branch**: `parley/feat/plugin-packaging` | **PR**: #276

### Feature: Self-contained plugin packaging (#248)

Distributable ZIP packages for plugins that users can extract directly to `~/Parley/`.

**Added**:
- `build-plugin-zip.ps1` - Build script to create distributable ZIP packages
- Auto-generates README.txt with installation instructions
- Includes parley_plugin client library in package
- Plugin Package Format specification (NonPublic/Plugin_Package_Format.md)
- GitHub Actions workflow for automated plugin releases (`release-plugin.yml`)
- Plugins section in README with download links

**ZIP Structure**:
```
plugin-name-X.Y.Z.zip
├── Plugins/plugin-name/    # Plugin files
├── Python/parley_plugin/   # Client library
└── README.txt              # Installation instructions
```

---

## [0.1.36-alpha] - 2025-11-30
**Branch**: `parley/fix/sandbox-security-hardening` | **PR**: #256

### Security Hardening for Sandboxed File I/O (#254)

Strengthens plugin security model with quick wins that don't require code signing.

**Implemented**:
- Add file size limits (10 MB default) to prevent disk fill DoS attacks
- Add symlink protection to prevent sandbox escape via ReparsePoint
- Per-plugin sandbox directories (`~/Parley/PluginData/{pluginId}/`)
- Wire FileServiceImpl to PluginFileService (security-checked implementation)

**Security Checklist**:
- [x] Path traversal prevention (already implemented)
- [x] File size limits (this PR)
- [x] Symlink blocking (this PR)
- [x] Per-plugin isolation (this PR)
- [x] Audit logging (already implemented)

---

## [0.1.35-alpha] - 2025-11-30
**Branch**: `parley/feat/epic-40-phase3-interaction` | **PR**: #249

### Epic 40 Phase 3: Interaction and Navigation (#234-#237)

Third phase of the ChatMapper-style flowchart view plugin (Epic 3).

**Work Items**:
- [x] #234 - Bidirectional node selection sync
- [x] #235 - User-controllable refresh settings (partial - toggle persistence has race condition, deferred)
- [ ] #236 - Minimap navigation panel (deferred to future phase)
- [x] #237 - Circular reference handling (already implemented via IsLink + processedX tracking)

**Implemented (#234)**:
- Added `SelectNode` gRPC RPC - plugins can request Parley to select a node in the tree view
- Added `DialogContextService.SelectedNodeId` tracking - syncs tree selection to plugins
- Added `NodeSelectionRequested` event - plugins can request selection changes
- JavaScript bridge in `PluginPanelWindow` - flowchart node clicks propagate to Parley
- Flowchart highlights selected node and scrolls into view when Parley selection changes
- Initial selection state passed to flowchart HTML on render
- Extracted `PluginSelectionSyncHelper` to keep MainWindow clean (~200 lines moved)

**Implemented (#235)**:
- Added manual refresh button (⟳) to force flowchart re-render
- Added auto-refresh toggle (⏸/▶) to pause/resume automatic updates
- Added sync selection toggle checkbox to enable/disable bidirectional navigation
- Added `GetPanelSetting` gRPC API for plugins to query UI toggle states
- Panel settings stored in C# PluginUIService for persistence attempts
- **Known Issue**: Toggle persistence has race condition when dialog changes fire before settings are stored

**Security Hardening**:
- Bundled D3.js and dagre.js locally (removed CDN dependency for supply chain security)
- Sanitized PYTHONPATH in PluginHost (no longer inherits potentially malicious paths)
- Refactored flowchart_plugin.py from 1157 to 550 lines (split to templates/static/vendor)

**Already Done (#237)**:
- `GetDialogStructure` uses `processedEntries`/`processedReplies` HashSets to prevent infinite recursion
- `IsLink` pointers create terminal "link" nodes instead of recursing into targets
- Link nodes styled with dashed borders and reduced opacity (Phase 2 #232)

---

## [0.1.34-alpha] - 2025-11-29
**Branch**: `parley/feat/epic-40-phase2-layout` | **PR**: #245

### Epic 40 Phase 2: Layout and Visual Design (#228-#232)

Second phase of the ChatMapper-style flowchart view plugin (Epic 3).

**Work Items**:
- [x] #228 - Sugiyama auto-layout (dagre.js) - Top-to-bottom hierarchical layout
- [x] #229 - Theme awareness - Dark/light mode CSS variables
- [x] #230 - NPC speaker color/shape integration - Consistent palette per speaker
- [x] #231 - Script indicators on nodes - ⚡ action, ❓ condition icons
- [x] #232 - Link node styling (grayed + dotted) - Dashed borders, reduced opacity

**Additional Improvements**:
- Added `GetSpeakerColors` gRPC API - Flowchart now uses Parley's configured speaker colors
- Added View > Plugin Panels menu item - Reopen closed plugin panels
- Empty nodes display `[Continue]` or `[End Dialog]` instead of blank text
- Changed settings path to `~/Parley` for cross-platform consistency
- Plugin startup notification moved to status bar (non-intrusive)
- Updated `deploy-plugins.ps1` to regenerate Python proto stubs automatically

**Technical Details**:
- Extended DialogNodeProto with HasCondition, HasAction, ConditionScript, ActionScript
- Extended DialogLinkProto with HasCondition, ConditionScript for edge styling
- dagre.js v0.8.5 for Sugiyama layout algorithm
- D3.js v7 for rendering and interactivity
- GetSpeakerColors RPC returns PC, Owner, and named speaker colors from SpeakerVisualHelper

---

## [0.1.33-alpha] - 2025-11-29
**Branch**: `parley/feat/epic-40-phase1-foundation` | **PR**: #244

### Epic 40 Phase 1: Flowchart Foundation (#223-#227)

First phase of the ChatMapper-style flowchart view plugin (Epic 3).

**Work Items**:
- [ ] #223 - Plugin scaffold and manifest
- [ ] #224 - Dockable/floating panel registration
- [ ] #225 - WebView.Avalonia.Cross integration
- [ ] #226 - Basic D3.js graph rendering
- [ ] #227 - Live dialog data integration

---

## [0.1.32-alpha] - 2025-11-28
**Branch**: `parley/feat/issue-146-builtin-scripts` | **PR**: #219

### Feature: Built-in Game Scripts & TLK Integration (#146)

Support browsing built-in game scripts from BIF files and resolving TLK StrRef values.

**Features**:
- [x] TLK string resolution for StrRef values in dialogs
- [x] Built-in script browser from base game BIF files
- [x] Visual distinction between module and built-in scripts (🎮 icon)
- [x] GameResourceService wrapper for Radoub.Formats integration
- [x] NWN:EE TLK path detection (lang/XX/data/dialog.tlk)
- [x] Embedded StrRef placeholder resolution (for dialogs with baked-in `<StrRef:N>` text)

---

## [0.1.31-alpha] - 2025-11-28
**Branch**: `parley/sprint/bugs-and-focus` | **PR**: #217

### Sprint: Bug Squash + Focus Management

Combined sprint addressing outstanding bugs and focus/navigation improvements.

**Bug Fixes**:
- [x] #178 - Script preview now clears when ROOT node selected
- [x] #12 - Links now show LinkComment instead of original node comment
- [x] #11 - Validation prevents invalid link operations (Entry → Entry, Reply → Reply)
- [x] #74 - Property changes now saved to undo stack when field gains focus
- [x] #123 - Paste as Link after Cut shows dialog with Undo/Paste as Copy/Cancel options

**Focus Management (#134)**:
- [x] #148 - Tab order navigation added to properties panel fields
- [x] #122 - Paste operations now focus on pasted/parent node instead of sibling
- [x] Move up/down focus preservation (already working via RefreshTreeViewAndSelectNode)
- [x] Paste duplicate focuses on pasted node
- [x] Paste link focuses on parent node

---

## [0.1.30-alpha] - 2025-11-28
**Branch**: `parley/fix/issue-196-tech-debt` | **PR**: #215

### Tech Debt Sprint

Addressing exception handling, code duplication, and attribution.

**Fixed - Exception Handling (#196)**:
- Added DEBUG logging to font preview fallback in SettingsWindow
- Added DEBUG logging to UTC file validation in CreatureParser
- Added TRACE logging to temp file cleanup in SoundBrowserWindow
- Added explanatory comment for intentional empty catch (font availability check)

**Fixed - Code Duplication (#194)**:
- Created `CloningHelper.cs` with shared cloning utilities
- `CloneLocString()` - centralized LocString cloning
- `CreateShallowNodeClone()` - centralized DialogNode shallow cloning
- Updated `NodeCloningService` and `DialogClipboardService` to use helper
- Removed ~30 lines of duplicate code

**Verified - Orphan Link Children (#136)**:
- Reviewed `NodeOperationsManager.DeleteNode()` implementation
- Confirmed PR #132 fix is correctly implemented
- `IdentifyOrphanedLinkChildren()` called before deletion
- `RemoveOrphanedLinkChildrenFromLists()` called before index recalculation
- All 23 orphan-related tests passing

**Added - Attribution (#205)**:
- Added arclight acknowledgment to README.md
- Credit to jd28/arclight for inspiration during early development

**Fixed - Copy Node Missing Scripts**:
- Copy/Cut now preserves `ScriptAppears` (conditional script) from source pointer
- Copy/Cut now preserves `ConditionParams` from source pointer
- Paste as Duplicate applies stored scripts to new pointer
- `GetNodeProperties` (Ctrl+Shift+P) now shows ScriptAppears and ConditionParams
- Added 11 new unit tests for script preservation (DialogClipboardServiceTests)
- Added 11 new unit tests for PasteOperationsManager (new test file)

---

## [0.1.29-alpha] - 2025-11-27
**Branch**: `parley/feat/issue-168-hak-sound-browser` | **PR**: #202

### HAK File Sound Browser Support (#168)

Sound browser now searches HAK files for sound resources.

**Completed**:
- Added HAK file scanning using Radoub.Formats ERF reader
- Sound browser scans dialog directory for `.hak` files
- HAK sounds display with 📦 indicator and source filename
- Preview playback for HAK sounds (extracts to temp file)
- **On-selection validation**: HAK sounds are validated when selected (extracts temporarily to check mono/stereo)
- Shows HAK count in status bar (e.g., "150 sounds (25 from HAK)")
- Source tracking for all sounds (Override, HAK name, Base Game)
- Respects NWN resource priority (Override → HAK → Base Game)

---

## [0.1.28-alpha] - 2025-11-27
**Branch**: `parley/feat/sprint-185-sound-browser` | **PR**: #200

### Sound Browser & Audio Sprint (#185)

Improving sound browser usability with location override and mono audio filtering.

**Child Issues**:
- #167 - Filter mono-only WAV files in Sound browser for conversation compatibility
- #168 - Sound browser should search HAK files (deferred to Epic #170)

**Additional Work**:
- Add location override (browse...) to Sound Browser (same pattern as Script Browser)

**Completed**:
- Added mono-only filter checkbox (default ON) - shows only conversation-compatible sounds
- Stereo files now show as errors (not just warnings) for dialog use
- Added location override row with browse.../reset buttons
- Show ⚠️ stereo indicator in file list when filter is disabled
- Added `IsMonoWav()` and `GetWavChannelCount()` helpers to SoundValidator

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
- #192 - Removed dead `ResourceSettings.cs` (284 lines)
- #193 - Removed unused methods from `SoundService.cs`
- #195 - Removed unused `IsScriptInDialogDirectory` from `ExternalEditorService.cs`
- #181 - Menu cleanup (duplicate Save, Help links)
- #169 - Namespace consolidation (Parley.Services → DialogEditor.Services)
- Fixed spurious parser warnings for empty nodes

---

## [0.1.24-alpha] - 2025-11-24
**Branch**: `parley/feat/issue-9-command-line` | **PR**: #187

### Command Line Support (Issue #9)

- Direct file loading: `Parley dialog.dlg`
- Safe mode: `Parley --safe-mode`
- Screenplay export: `Parley --screenplay dialog.dlg`

---

## [0.1.23-alpha] - 2025-11-23
**Branch**: `parley/feat/epic-39-ux-improvements` | **PR**: #174

### Epic #39: UI/UX Enhancements (Complete)

- Modeless Settings Window
- NPC Speaker Visual Preferences (per-tag colors/shapes)
- Delete Confirmation Preferences
- Properties Panel State Management
- Focus Preservation improvements

---

## [0.1.22-alpha] - 2025-11-23
**Branch**: `parley/feat/scrollbar-improvements` | **PR**: #172

### Scrollbar Improvements (Issue #63)

Global scrollbar visibility and usability improvements across all panels.

---

## [0.1.21-alpha] - 2025-11-23
**Branch**: `parley/feat/autosave-improvements` | **PR**: #165

### Autosave Improvements (Issues #18, #62)

- Configurable autosave interval (0-60 minutes)
- Visual feedback improvements
- Save architecture refactor (DialogSaveService)

---

## [0.1.20-alpha] - 2025-11-22
**Branch**: `parley/refactor/mainwindow-cleanup` | **PR**: #164

### Epic #163: MainWindow.axaml.cs Refactoring Sprint

MainWindow.axaml.cs reduced from 4,126 → 2,603 lines (-37%)

---

## [0.1.19-alpha] - 2025-11-20
**Branch**: `parley/feat/epic-108-inline-editing` | **PR**: #142

### Epic #39: UI/UX Enhancements (Issue #108)

- Panel Size Persistence
- Window Position Persistence
- Debug Panel moved to Settings

---

## [0.1.18-alpha] - 2025-11-20
**Branch**: `parley/feat/epic-39-ui-ux` | **PR**: #140

### Epic #39: Theme System with 8 Themes

8 official themes including colorblind-accessible options and auto-refresh on theme change.

---

## [0.1.17-alpha] - 2025-11-19
**Branch**: `parley/refactor/epic-99-cleanup-dead-code` | **PR**: #138

### Epic #99: MainViewModel Refactoring - Phase 7

MainViewModel reduced from 2,956 to 1,258 lines (-57%). Goal achieved.

---

## [0.1.16-alpha] - 2025-11-18
**Branch**: `parley/refactor/epic-99-node-operations` | **PR**: #137

### Epic #99: MainViewModel Refactoring - Phase 6 (NodeOperationsManager)

---

## [0.1.15-alpha] - 2025-11-17
**Branch**: `parley/refactor/epic-99-property-panel` | **PR**: #135

### Epic #99: MainViewModel Refactoring - Phase 5 (PropertyPanelPopulator)

---

## [0.1.14-alpha] - 2025-11-16
**Branch**: `parley/refactor/epic-99-tree-navigation` | **PR**: #133

### Epic #99: MainViewModel Refactoring - Phase 4 (TreeNavigationManager)

---

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
