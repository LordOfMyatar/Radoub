# Trebuchet

*Launch your modules into battle.*

---

## What is Trebuchet?

Trebuchet is the central launcher and module management hub for the Radoub toolset. It provides a unified interface for managing Neverwinter Nights modules, launching tools, and configuring shared settings.

**Status**: Alpha
**Platforms**: Windows, Linux, macOS (experimental)

---

## Alpha Status

**This is alpha software. Always work with backup copies of your module files.**

See [Known Issues](https://github.com/LordOfMyatar/Radoub/issues?q=is%3Aissue+is%3Aopen+label%3Atrebuchet) for current bug list.

---

## Features

### Tool Launcher

- Discover and launch Parley, Manifest, Quartermaster, and Fence
- Recent file support for quick access
- Pass files directly to tools via MRU dropdown

### Module Editor

- **Metadata**: Module name, description, tag, custom TLK
- **Version Info**: Minimum game version, expansions, XP scale, DefaultBic, StartMovie
- **HAK Files**: Ordered list with add/remove/reorder
- **Time Settings**: Dawn/dusk hours, start date and time
- **Entry Point**: Starting area with X/Y/Z coordinates
- **Event Scripts**: All 16 standard module scripts plus 6 NWN:EE extended scripts
- **Local Variables**: Module-level variable editing

### Module Management

- Unpack `.mod` files for editing
- Build/pack modules with automatic backups
- Module browser sidebar with recent modules

### Faction Editor

- Visual faction relationship editor
- Reputation matrix for faction interactions

### Build & Test

- **NWScript Compiler**: Compile scripts with configurable compiler path
- **Compile Uncompiled**: Checkbox to compile all uncompiled scripts during build
- **Recompile Selected**: Fix and recompile individual scripts after errors
- **Compiler Log**: View compiler output with one-click log access
- **Always Save Before Testing**: Optional auto-save before test launch

### Game Launcher

- **Test Module**: Auto-select first character for quick testing
- **Load Module**: Full character selection screen

### Settings & Theming

- Game path configuration
- TLK file selection
- NWScript compiler path
- Theme editor for creating and customizing themes across all tools
- Font preferences

---

## Documentation

Full documentation available in the wiki:

- [User Guide](https://github.com/LordOfMyatar/Radoub/wiki/Trebuchet)
- [Architecture](https://github.com/LordOfMyatar/Radoub/wiki/Trebuchet-Developer-Architecture)

---

## Quick Start

### Download

Get the latest release from [GitHub Releases](https://github.com/LordOfMyatar/Radoub/releases).

### Build from Source

```bash
git clone https://github.com/LordOfMyatar/Radoub.git
cd Radoub
dotnet build Trebuchet/Trebuchet/Trebuchet.csproj -c Release
dotnet run --project Trebuchet/Trebuchet/Trebuchet.csproj
```

Requires [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0).

---

## For Developers

See [CLAUDE.md](CLAUDE.md) for development guidance.

---

## Contributing

Contributions welcome!

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
