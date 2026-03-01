# Fence

*Every merchant has their price. Now you can set it.*

---

## What is Fence?

Fence is a cross-platform merchant/store editor for Neverwinter Nights `.utm` files. Built with modern technology while maintaining Aurora Engine compatibility.

**Status**: Alpha
**Platforms**: Windows, Linux, macOS (experimental)

---

## Alpha Status

**This is alpha software. Always work with backup copies of your module files.**

See [Known Issues](https://github.com/LordOfMyatar/Radoub/issues?q=is%3Aissue+is%3Aopen+label%3Afence) for current bug list.

---

## Features

- **Store Properties**: Name, tag, markup/markdown percentages, gold reserves
- **Store Inventory**: 5 panels (Armor, Misc, Potions, Rings, Weapons) with resref editing
- **Item Palette**: Load items from BIF archives with search and filter
- **Buy Restrictions**: WillOnlyBuy/WillNotBuy checkboxes by base item type
- **Event Scripts**: OnOpenStore and OnStoreClose script assignment via shared ScriptBrowserWindow
- **Local Variables**: Variable editing for store-level scripting
- **Store Browser**: Collapsible browser panel with HAK support (F4 toggle)
- **TLK Language**: Item palette displays in selected TLK language
- **File Management**: Delete store files from browser panel context menu
- **Validation**: Duplicate resref detection and Aurora-safe character checking
- **Custom Content**: Support for module HAKs and custom TLK files

---

## Documentation

Full documentation available in the wiki:

- [User Guide](https://github.com/LordOfMyatar/Radoub/wiki/Fence)
- [Architecture](https://github.com/LordOfMyatar/Radoub/wiki/Fence-Developer-Architecture)

---

## Quick Start

### Download

Get the latest release from [GitHub Releases](https://github.com/LordOfMyatar/Radoub/releases).

### Build from Source

```bash
git clone https://github.com/LordOfMyatar/Radoub.git
cd Radoub
dotnet build Fence/Fence/Fence.csproj -c Release
dotnet run --project Fence/Fence/Fence.csproj
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
