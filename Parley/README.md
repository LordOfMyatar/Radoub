# Parley

*Not all treasure is silver and gold, mate. Sometimes it's a readable dialog editor.*

---

## What is Parley?

Parley is a cross-platform dialog editor for Neverwinter Nights `.dlg` files. Built with modern technology while maintaining Aurora Engine compatibility.

**Status**: Beta
**Platforms**: Windows, Linux, macOS (experimental)

---

## Beta Status

**This is beta software. Always work with backup copies of your module files.**

See [Known Issues](https://github.com/LordOfMyatar/Radoub/issues?q=is%3Aissue+is%3Aopen+label%3Aparley) for current bug list.

---

## Supported File Types

- `.dlg` — Dialog files (GFF-based, Aurora Engine)

---

## Features

- Dialog tree editing with full undo/redo
- Native flowchart view (View → Flowchart or F5) with PNG/SVG export
- Sound and script browsers with parameter preview
- Conversation simulator (F6) with text-to-speech and coverage tracking
- Color-coded NPCs for multi-NPC conversations
- NWN color token support with token selector (Ctrl+T)
- Orphan node management with scrap container and subtree restore
- Dialog file browser with module HAK support
- Quest browser for cross-tool journal navigation
- Spell-checking with custom D&D/NWN dictionary
- Auto-save with configurable intervals
- Cross-platform (Windows, Linux, macOS)
- Themes and accessibility options (dark mode, font scaling, color-blind modes)

---

## Documentation

Full documentation available in the wiki:

- [Getting Started](https://github.com/LordOfMyatar/Radoub/wiki/Getting-Started) - Installation and setup
- [User Guide](https://github.com/LordOfMyatar/Radoub/wiki) - Full documentation
- [Troubleshooting](https://github.com/LordOfMyatar/Radoub/wiki/Troubleshooting) - Common issues

---

## Quick Start

### Download

Get the latest release from [GitHub Releases](https://github.com/LordOfMyatar/Radoub/releases).

### Build from Source

```bash
git clone https://github.com/LordOfMyatar/Radoub.git
cd Radoub/Parley
dotnet build Parley.sln -c Release
dotnet run --project Parley/Parley.csproj
```

Requires [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0).

---

## For Developers

- [Architecture](https://github.com/LordOfMyatar/Radoub/wiki/Parley-Developer-Architecture) - Code organization
- [Testing Guide](https://github.com/LordOfMyatar/Radoub/wiki/Parley-Developer-Testing) - How to run and write tests

---

## Contributing

Contributions welcome! See [CLAUDE.md](CLAUDE.md) for development guidance.

**Wanted**: Bug reports, platform testing (Linux/macOS), accessibility feedback, code review.

---

## License

See [LICENSE](../LICENSE)

---

## Acknowledgments

- BioWare/Beamdog for NWN and Aurora format specs
- [neverwinter.nim](https://github.com/niv/neverwinter.nim) - Reference for Aurora file formats
- NWN modding community

---

_Part of the [Radoub](../) toolset_
