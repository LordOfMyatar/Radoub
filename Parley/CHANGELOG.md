# Changelog - Parley

All notable changes to Parley will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

---

## [Unreleased]

---

## [0.1.8-alpha] - TBD
**Branch**: `parley/feat/epic-2-ui-ux` | **PR**: #107

### Epic 2: UI/UX Enhancements

Comprehensive UI/UX improvements including font customization (README promised), enhanced themes, autosave enhancements, and visual feedback improvements.

**Note**: Epic 2 contains many work items. Implementation will be broken into focused increments.

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

### Planned Features
- Layout redesign with resizable panels (#108)
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
