# Manifest

*Every quest needs a journal entry. Make yours count.*

---

## What is Manifest?

Manifest is a cross-platform journal editor for Neverwinter Nights `.jrl` files. Built with modern technology while maintaining Aurora Engine compatibility.

**Status**: Beta
**Platforms**: Windows, Linux, macOS (experimental)

---

## Beta Status

**This is beta software. Always work with backup copies of your module files.**

See [Known Issues](https://github.com/LordOfMyatar/Radoub/issues?q=is%3Aissue+is%3Aopen+label%3Amanifest) for current bug list.

---

## Supported File Types

- `.jrl` — Journal files (GFF-based, Aurora Engine)

---

## Features

- Quest category and entry editing
- TLK string resolution with multi-language support
- NWN color token support with token selector (Ctrl+T)
- Spell-checking with custom D&D/NWN dictionary
- Cross-tool navigation from Parley
- Auto-load journal from Trebuchet's current module
- Theme support (dark/light, color blind)
- Auto-detect game installation paths

---

## Documentation

Full documentation available in the wiki:

- [User Guide](https://github.com/LordOfMyatar/Radoub/wiki/Manifest)
- [Architecture](https://github.com/LordOfMyatar/Radoub/wiki/Manifest-Developer-Architecture)

---

## Quick Start

### Download

Get the latest release from [GitHub Releases](https://github.com/LordOfMyatar/Radoub/releases).

### Build from Source

```bash
git clone https://github.com/LordOfMyatar/Radoub.git
cd Radoub
dotnet build Manifest/Manifest/Manifest.csproj -c Release
dotnet run --project Manifest/Manifest/Manifest.csproj
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
