# Changelog - Parley

All notable changes to Parley will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

---

## [Unreleased]

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
