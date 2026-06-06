# Reliquary

Placeable blueprint editor for Neverwinter Nights `.utp` files. Part of the [Radoub](../README.md) toolset.

> **Status: Alpha (scaffolding).** Sprint 4 ships a demoable skeleton — browser sidebar plus stubbed editor panels. Full editing lands in later sprints (see [CHANGELOG](CHANGELOG.md)).

## What it edits

`.utp` files are Aurora Engine placeable blueprints — the objects you place in the world (chests, doors, statues, plants, containers). Reliquary brings full Aurora "Placeable Object Properties" parity to a single-page Avalonia editor consistent with the rest of the toolset.

Reliquary is the sister tool to [Relique](../Relique/README.md) (item editor): same single-resource blueprint pattern, plus Quartermaster-style inventory for placeables that hold items.

## Usage

```bash
Reliquary                       # Start with empty editor
Reliquary boulder001.utp        # Open a file
Reliquary --file chest.utp      # Open via flag
Reliquary -m LNS --file door.utp  # Resolve relative to a module
Reliquary --safemode            # Reset theme/fonts to defaults
Reliquary --help                # Show all options
```

Usually launched from the [Trebuchet](../Trebuchet/README.md) hub, which discovers it automatically.

## Build

```bash
dotnet build Reliquary/Reliquary/Reliquary.csproj
dotnet test  Reliquary/Reliquary.Tests
```

## Documentation

- [CHANGELOG](CHANGELOG.md)
- [CLAUDE.md](CLAUDE.md) — developer guidance
- [Radoub toolset](../README.md)
