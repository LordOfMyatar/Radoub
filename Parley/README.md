# Parley

**Cross-Platform Dialog Editor for Neverwinter Nights**

Modern dialog editor for Neverwinter Nights DLG files with 100% Aurora Engine compatibility. Part of the Radoub toolset.

---

## ⚠️ Alpha Status

**This is alpha software. Always work with backup copies of your module files.**

Known Issues:
- Copy/paste with complex node links needs improvement
- Delete operations with multiple parent references require testing
- See [GitHub Issues](../../issues?q=is%3Aissue+is%3Aopen+label%3Aparley) for current bug list

---

## Features

### Current (Alpha)
- ✅ Read and write Aurora-compatible DLG files
- ✅ Tree view conversation editing
- ✅ Node properties editing (text, speaker, listener, scripts)
- ✅ Add, delete, move nodes
- ✅ Undo/redo system (Ctrl+Z/Ctrl+Y)
- ✅ Sound browser (MP3/WAV/BMU from game and module)
- ✅ Script browser with parameter preview
- ✅ Creature tag selection (from UTC files)
- ✅ Journal quest integration
- ✅ Dark mode and font scaling
- ✅ Copy tree structure to clipboard
- ✅ Recent files menu
- ✅ Cross-platform (Windows, Linux, macOS)

### Roadmap
See [parent README](../README.md) for full roadmap.

---

## Why Parley?

**Problems with Aurora Toolset**:
- Fonts not following Windows 10/11 settings
- No dark mode
- Modal window management block workflow
- Script parameters easy to lose and hard to enter
- Wine and other utilties required for cross platform support

**Parley Improvements**:
- Adjustable font size
- Dark mode support
- Cross-platform (Windows, Linux, macOS)
- Modern UI with better script parameter handling and AutoTrim
- Modeless dialogs for better workflow
- Active development and community feedback

---

## Installation

### Binary Release (Coming Soon)
Pre-built binaries will be available from GitHub Releases.

### From Source

**Prerequisites**:
- .NET 9.0 SDK
- Git

**Build Steps**:
```bash
# Clone repository
git clone https://github.com/YourUsername/Radoub.git
cd Radoub/Parley

# Build
dotnet build Parley.sln -c Release

# Run
dotnet run --project Parley/Parley.csproj
```

**Supported Platforms**:
- ✅ Windows 10/11 (tested)
- ⚠️ Linux (needs testing)
- ⚠️ macOS (needs testing)

---

## Usage

### First Launch

1. **Open Settings** (Tools → Settings or Ctrl+,)
2. **Set Game Directory**: Point to your NWN installation
   - Windows: `C:\Users\[Name]\Documents\Neverwinter Nights`
   - Linux: `~/.local/share/Neverwinter Nights`
   - macOS: `~/Library/Application Support/Neverwinter Nights`
3. **Set Module Directory** (optional): Choose specific module folder

### Opening a Dialog File

- **File → Open** (Ctrl+O): Browse for `.dlg` file
- **Recent Files**: Quick access to recently opened files
- **Command Line**: `Parley.exe path/to/dialog.dlg`

### Editing Dialogs

**Tree View**:
- Click node to select and view properties
- Right-click for context menu (Add, Delete, Cut, Paste, Copy Tree)
- Drag to reorder siblings (in progress)

**Properties Panel**:
- Edit text, speaker, listener
- Set scripts and parameters
- Configure quest/journal entries
- Assign sounds

**Keyboard Shortcuts**:
- `Ctrl+Z` / `Ctrl+Y`: Undo/Redo
- `Ctrl+X`: Cut node
- `Ctrl+C`: Copy tree structure to clipboard
- `Ctrl+S`: Save
- `Ctrl+Shift+S`: Save As
- `Ctrl+N`: New dialog
- `Ctrl+O`: Open
- `Ctrl+,`: Settings
- `Ctrl+Shift+Up/Down`: Move node up/down

### Testing in Aurora Toolset

After editing:
1. Save file in Parley
2. Open module in Aurora Toolset
3. Test conversation in-game or via toolset preview
4. Report any issues to GitHub

---

## Known Issues

### Critical
- None currently

### Major
- **Copy/Paste**: Complex node links may not preserve correctly ([#XX](link))
- **Delete**: Nodes with multiple parents need careful handling ([#XX](link))

### Minor
- **macOS/Linux**: Auto-detection of Steam/Beamdog paths not implemented ([#70](link))
- **Modal Dialogs**: Some dialogs block main window ([#69](link))

See [all issues](../../issues?q=is%3Aissue+is%3Aopen+label%3Aparley)

---

## Contributing

Contributions welcome! See [parent repository guidelines](../CLAUDE.md) and [Parley-specific guidance](CLAUDE.md).

**Wanted**:
- Bug reports with reproduction steps
- Platform testing (especially Linux/macOS)
- Module compatibility testing
- Accessibility feedback
- Code review and improvements

**Before Contributing**:
- Read `CLAUDE.md` for development workflow
- Check existing issues to avoid duplicates
- Test with backup copies of modules

---

## Technical Details

**Stack**:
- .NET 9.0
- Avalonia UI (cross-platform)
- Aurora Engine GFF v3.28+ binary format

**Architecture**:
- MVVM pattern
- Dialog file service layer
- Comprehensive logging
- Undo/redo system
- Circular reference protection

See [PARSER_ARCHITECTURE.md](Documentation/PARSER_ARCHITECTURE.md) for parser details.

---

## License

See [LICENSE](../LICENSE)

---

## Acknowledgments

- BioWare/Beamdog for NWN and Aurora format specs
- Original Aurora Toolset developers
- NWN modding community

---

*Part of the [Radoub](../) toolset*