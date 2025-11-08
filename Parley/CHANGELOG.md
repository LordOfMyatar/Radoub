# Changelog - Parley

All notable changes to Parley will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

---

## [Unreleased]

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
