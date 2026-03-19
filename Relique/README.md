# Relique

Item Blueprint Editor for Neverwinter Nights. Part of the [Radoub](../README.md) toolset.

## Overview

Relique creates and edits `.uti` (Item Blueprint) files for Neverwinter Nights. It provides a visual editor for item properties, stats, and enchantments.

## Features (Sprint 1 - Bootstrap)

- Open, save, and save-as for `.uti` files
- Command line support (`--file`, `--safemode`, `--help`)
- Item browser panel with search
- Theme support (light, dark, custom)
- Recent files menu
- Trebuchet launcher integration

## Usage

```bash
# Start with empty editor
ItemEditor

# Open a specific item
ItemEditor sword.uti
ItemEditor --file armor.uti

# Start in safe mode (reset theme/fonts)
ItemEditor --safemode
```

## Building

```bash
# Build Relique only
dotnet build Relique/Relique/Relique.csproj

# Build all Radoub tools
dotnet build Radoub.sln

# Run
dotnet run --project Relique/Relique/Relique.csproj

# Run tests
dotnet test Relique/Relique.Tests
```

## File Format

UTI files are GFF-based item blueprints containing:

- **Identity**: Localized name, tag, ResRef, description
- **Base Properties**: Base item type, cost, weight, stack size, charges
- **Flags**: Plot, stolen, cursed, droppable, identified, pickpocketable
- **Item Properties**: Enchantments and special abilities
- **Appearance**: Model parts, colors
- **Scripts**: OnActivate, OnAcquire, OnUnacquire, OnEquip, OnUnequip

## Dependencies

- **Radoub.Formats** - UTI file parsing (UtiReader/UtiWriter)
- **Radoub.UI** - Shared theme manager, About window, styles

## Status

Alpha - Active development under [Epic #1576](https://github.com/LordOfMyatar/Radoub/issues/1576).
