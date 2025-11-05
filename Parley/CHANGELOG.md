# Changelog - Parley

All notable changes to Parley will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

---

## [Unreleased]

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
