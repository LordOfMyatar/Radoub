# Parley

*Not all treasure is silver and gold, mate. Sometimes it's a readable dialog editor.*

## What is Parley?

Parley is a conversation editor designed for Neverwinter Nights conversation files based off of the conversation editor from Aurora Toolset. It aims to improve usability via a modern user interface. It allows you to work on conversation files outside of the Aurora Toolset.

---

## ⚠️ Alpha Status

**This is alpha software. Always work with backup copies of your module files.**

Parley is under active development. While the core dialog editing functionality appears stable and Aurora-compatible, some features are still being refined. See [Known Issues](#known-issues) below for details.

---

## New Features

### Current (Alpha)

- Color coded NPCs for multi-npc conversations
- Themes, Fonts, and Font Sizes
- Improved interface for script parameters including auto-trim
- Script parameter system to avoid typos
- Modeless dialogs (Settings, Script/Parameter browsers)
- Cross-platform (Win/Linux. Limited support for Mac)
- Sound browser (In Progress)
- Plugin system (In Progress: Python-based, process-isolated, gRPC communication)

### Roadmap

[Parley Project Roadmap](https://github.com/users/LordOfMyatar/projects/2)

---

## Why Parley?

### Problems with Aurora Toolset

- Fonts not following Windows 10/11 settings
- No dark mode
- Modal window management block workflow
- Script parameters easy to lose and hard to enter

### Parley Improvements

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

---

## Script Preview

Parley displays script source code (`.nss` files) in the script preview panel when available. For scripts without source:

- **Module scripts**: Place `.nss` files in your module directory
- **Compiled game resources**: Base game scripts are distributed as compiled `.ncs` files without source
- **Decompiling**: Use [nwnnsscomp](https://github.com/niv/neverwinter.nim) to decompile `.ncs` files:
  ```bash
  nwnnsscomp -d scriptname.ncs > scriptname.nss
  ```

The neverwinter.nim toolset is the recommended tool for working with NWN file formats.

---

## Known Issues

See [GitHub Issues](https://github.com/LordOfMyatar/Radoub/issues?q=is%3Aissue+is%3Aopen+label%3Aparley) for current bug list and feature requests.

---

## Contributing

Contributions welcome! See [parent repository guidelines](../CLAUDE.md) and [Parley-specific guidance](CLAUDE.md).

### Wanted

- Bug reports with reproduction steps
- Platform testing (especially Linux/macOS)
- Module compatibility testing
- Accessibility feedback
- Code review and improvements

### Before Contributing

- Read `CLAUDE.md` for development workflow
- Check existing issues to avoid duplicates
- Test with backup copies of modules

---

## Technical Details

### Stack

- .NET 9.0
- Avalonia UI (cross-platform)
- Aurora Engine GFF v3.28+ binary format

### Architecture

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
- [arclight](https://github.com/jd28/arclight) by jd28 - inspiration and guidance during early development

---

_Part of the [Radoub](../) toolset_
