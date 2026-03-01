# Quartermaster

*Every adventurer starts somewhere. Make sure they start right.*

---

## What is Quartermaster?

Quartermaster is a cross-platform creature and inventory editor for Neverwinter Nights `.utc` (creature blueprint) and `.bic` (player character) files. Built with modern technology while maintaining Aurora Engine compatibility.

**Status**: Alpha
**Platforms**: Windows, Linux, macOS (experimental)

---

## Alpha Status

**This is alpha software. Always work with backup copies of your module files.**

See [Known Issues](https://github.com/LordOfMyatar/Radoub/issues?q=is%3Aissue+is%3Aopen+label%3Aquartermaster) for current bug list.

---

## Features

### Creature Editing

- **Stats Panel**: Ability scores, HP, AC, attack bonuses, saving throws
- **Classes Panel**: Class levels, alignment, deity, race, gender
- **Skills Panel**: Skill ranks with progress bars
- **Feats Panel**: Add/remove feats with prerequisite validation
- **Spells Panel**: Known and memorized spells with metamagic feat variants
- **Scripts Panel**: Event scripts and conversation resref
- **Advanced Panel**: Flags, behavior settings, appearance values, local variables
- **Character Sheet**: Export creature summary as text or Markdown

### Visual Preview

- **Appearance Panel**: Real-time 3D body part rendering with OpenGL
- **Color Customization**: Skin, hair, and tattoo color selection via swatch palettes
- **Portrait Browser**: Browse and select from game portraits

### Inventory

- **Equipment Slots**: Visual equipment slot panel
- **Backpack**: Inventory management with item details
- **Item Resolution**: Loads items from module, Override, HAK archives, and BIF

### New Character Wizard

- 10-step guided creation process
- Appearance, ability scores, classes, skills, feats, spells, equipment, summary
- Point-buy ability allocation with racial modifiers
- Starting equipment from class packages
- Soundset browser with audio preview

### Level Up Wizard

- 5-step class progression (class, feats, skills, spells, summary)
- Auto-assign buttons for skills, feats, and spells from class packages
- Bonus feat restrictions for Fighter/Wizard class bonus slots
- Divine/auto-grant caster spell display
- Spell selection for spontaneous, prepared, and wizard spellbook casters

### Browsing

- **Creature Browser**: Load creatures from module directories and vaults
- **Soundset Browser**: Browse and preview soundsets with gender filtering

---

## Documentation

Full documentation available in the wiki:

- [User Guide](https://github.com/LordOfMyatar/Radoub/wiki/Quartermaster)
- [Architecture](https://github.com/LordOfMyatar/Radoub/wiki/Quartermaster-Developer-Architecture)

---

## Quick Start

### Download

Get the latest release from [GitHub Releases](https://github.com/LordOfMyatar/Radoub/releases).

### Build from Source

```bash
git clone https://github.com/LordOfMyatar/Radoub.git
cd Radoub
dotnet build Quartermaster/Quartermaster/Quartermaster.csproj -c Release
dotnet run --project Quartermaster/Quartermaster/Quartermaster.csproj
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
