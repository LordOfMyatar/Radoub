# Parley

**Cross-Platform Dialog Editor for Neverwinter Nights**

Modern dialog editor for Neverwinter Nights DLG files with 100% Aurora Engine compatibility. Part of the Radoub toolset.

---

## ⚠️ Alpha Status

**This is alpha software. Always work with backup copies of your module files.**

Parley is under active development. While the core dialog editing functionality appears stable and Aurora-compatible, some features are still being refined. See [Known Issues](#known-issues) below for details.

---

## Features

### Current (Alpha)
- ✅ Read and write Aurora-compatible DLG files
- ✅ Tree view conversation editing
- ✅ Node properties editing (text, speaker, listener, scripts)
- ✅ Add, delete, move nodes
- ✅ Undo/redo system (Ctrl+Z/Ctrl+Y)
- ✅ Sound browser (In Progress)
- ✅ Script browser with parameter preview
- ✅ Creature tag selection (from UTC files)
- ✅ Journal quest integration
- ✅ Modeless dialogs (Settings, Script/Parameter browsers)
- ✅ NPC speaker visual preferences (per-tag colors and shapes)
- ✅ Auto-save with configurable intervals
- ✅ Dark mode and accessibility themes
- ✅ Font scaling and scrollbar visibility settings
- ✅ Recent files menu
- ✅ Cross-platform (Win/Linux. Limited support for Mac)
- ✅ **Plugin system** (In Progress: Python-based, process-isolated, gRPC communication)

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

### Binary Releases (Recommended)

Download the latest release from [GitHub Releases](https://github.com/LordOfMyatar/Radoub/releases).

**Self-Contained Builds** (No .NET required):
- **Windows**: Extract `Parley-win-x64.zip` and run `Parley.exe`
- **macOS**: Extract `Parley-osx-arm64.zip` and open `Parley.app`
  - First launch: System Preferences → Security & Privacy → Allow
  - Apple Silicon (M1/M2/M3) Macs
- **Linux**: Extract `Parley-linux-x64.tar.gz`, run `chmod +x Parley`, then `./Parley`

**Framework-Dependent Builds** (Requires [.NET 9 Runtime](https://dotnet.microsoft.com/download/dotnet/9.0)):
- Smaller downloads (`-fd` suffix): `Parley-win-x64-fd.zip`, `Parley-osx-arm64-fd.zip`, `Parley-linux-x64-fd.tar.gz`

### From Source

**Prerequisites**:
- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- Git

**Build Steps**:
```bash
git clone https://github.com/LordOfMyatar/Radoub.git
cd Radoub/Parley
dotnet build Parley.sln -c Release
dotnet run --project Parley/Parley.csproj
```

---

## Usage

### First Launch

1. **Open Settings** (Tools → Settings or Ctrl+,)
2. **Configure Resource Paths**:
   - **Base Game Installation**: Path to NWN installation (optional, for base game resources)
   - **Module Directory**: Path to your NWN modules folder
     - Windows: `~\Documents\Neverwinter Nights\modules`
     - Linux: `~/.local/share/Neverwinter Nights/modules`
     - macOS: `~/Library/Application Support/Neverwinter Nights/modules`
3. **Customize UI** (optional):
   - Choose theme (Standard, Dark, or accessibility themes)
   - Adjust font size
   - Configure auto-save interval
   - Enable/disable NPC tag coloring

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
- Set scripts and parameters (with script browser and parameter preview)
- Configure quest/journal entries
- Assign sounds (with audio preview)
- Customize NPC speaker colors and shapes (per-tag preferences)

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

---

## Known Issues

See [GitHub Issues](https://github.com/LordOfMyatar/Radoub/issues?q=is%3Aissue+is%3Aopen+label%3Aparley) for current bug list and feature requests.

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
