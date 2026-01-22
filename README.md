# Radoub

**Neverwinter Nights Modding Toolset**

*Bringing dead workflows back to life.*

A collection of cross-platform tools for creating and editing Neverwinter Nights modules. Built with modern technology while respecting the Aurora Engine's legacy.

> *Radoub* (French, "careening") - The process of repairing a ship's hull. Like undead pirates maintaining their vessel for eternal voyages, these tools help keep your NWN modules shipshape for adventures to come.

---

## Tools

### Ready for Preview

These tools are ready for adventurous users to try out. Like a zombie hungering for brains, we crave your feedback!

**Important**: Always work with copies of your modules, not originals.

#### Parley - Dialog Editor

Cross-platform dialog editor for Neverwinter Nights DLG files with modern UI, plugin system, and Aurora Engine compatibility.

**Status**: User Preview (Alpha)
**Platforms**: Windows, Linux, macOS (experimental)
**Key Features**:
- Dialog tree editing with undo/redo
- Native flowchart view (handles 100+ depth trees)
- Sound and script browsers with module HAK support
- Python plugin system with gRPC
- Color-blind accessible themes
- Dark mode and font scaling

**Learn more**: [Parley/README.md](Parley/README.md) | [Wiki](https://github.com/LordOfMyatar/Radoub/wiki/Parley)

#### Manifest - Journal Editor

Cross-platform journal editor for Neverwinter Nights JRL files.

**Status**: User Preview (Alpha)
**Platforms**: Windows, Linux, macOS (experimental)
**Key Features**:
- Quest category and entry editing
- TLK string resolution with multi-language support
- Spell-checking with custom dictionary support
- Theme support (dark/light, accessibility themes)
- Auto-detect game installation paths

**Learn more**: [Manifest/README.md](Manifest/README.md) | [Wiki](https://github.com/LordOfMyatar/Radoub/wiki/Manifest)

---

### In Development

#### Fence - Merchant Editor

Store/merchant editor for Neverwinter Nights UTM files.

**Status**: Nearing User Preview
**Platforms**: Windows, Linux, macOS (experimental)
**Features**:
- Store properties editing (markup, markdown, gold reserves)
- Item palette with search/filter
- Buy restrictions (WillOnlyBuy/WillNotBuy)
- Custom content support via module HAKs

#### Quartermaster - Creature & Inventory Editor

Cross-platform creature and inventory editor for Neverwinter Nights UTC and BIC files.

**Status**: In Development (user preview expected next sprint)
**Platforms**: Windows, Linux, macOS (experimental)
**Features**:
- Edit creature blueprints (.utc) and player characters (.bic)
- Visual equipment slots panel
- Inventory management with drag-and-drop
- Creature properties (abilities, feats, skills)
- Class and level configuration

#### Trebuchet - Tool Launcher & Settings Hub

Central launcher for the Radoub toolset with module-wide configuration.

**Status**: In Development
**Platforms**: Windows, Linux, macOS (experimental)
**Features**:
- Launch any Radoub tool
- Module-wide settings (game paths, HAKs, TLK)
- Theme configuration across all tools
- IFO module file editing

*Note: Development timelines have a sea-wide margin of error. We're building this ship while sailing it.*

---

### Shared Libraries

#### Radoub.Formats

Aurora Engine file format parsers used by all tools.

**Supported Formats**: GFF, KEY, BIF, TLK, 2DA, ERF, DLG, JRL, UTC, UTI, UTM, BIC, IFO, SSF

#### Radoub.Dictionary

D&D/NWN spell-checking library providing:
- Base language checking via [Hunspell](https://hunspell.github.io/) dictionaries
- Custom dictionary support for D&D/NWN terminology (proper nouns, spells, creatures)
- Term extraction from game files (.2da, dialogs)

---

## Documentation

**Wiki**: [github.com/LordOfMyatar/Radoub/wiki](https://github.com/LordOfMyatar/Radoub/wiki)

The wiki contains:
- User guides for each tool
- Developer architecture documentation
- Aurora Engine format specifications (converted from BioWare PDFs)
- Development history and AI collaboration notes

---

## Project Philosophy

**Modern Tools, Classic Respect**

The Radoub toolset aims to provide modern, accessible tools for Neverwinter Nights module creation while maintaining 100% compatibility with the Aurora Engine and existing toolsets.

**Key Principles**:
- Cross-platform support (Windows, Linux, macOS)
- Aurora Engine format compatibility
- Accessibility features (dark mode, font scaling, screen readers)
- Clean, maintainable codebase
- Open development process

---

## Installation

Each tool has its own installation instructions. See individual tool README files in their directories.

---

## Development

### Building

**Prerequisites**: .NET 9.0 SDK

```bash
# Clone the repository
git clone https://github.com/LordOfMyatar/Radoub.git
cd Radoub

# Build all tools at once
dotnet build Radoub.sln

# Or build individual tools
dotnet build Parley/Parley.sln
dotnet build Manifest/Manifest/Manifest.csproj
dotnet build Quartermaster/Quartermaster/Quartermaster.csproj
```

**Note**: `Radoub.sln` excludes `Radoub.IntegrationTests` (Windows-only FlaUI UI tests).

### Testing

```bash
# Windows (includes UI tests)
./Radoub.IntegrationTests/run-tests.ps1

# Linux/macOS (unit tests only)
./Radoub.IntegrationTests/run-tests.sh
```

### Project Structure

This is a multi-tool repository. Each tool maintains its own:
- Development documentation
- CHANGELOG
- Testing protocols
- Issue tracking (with tool labels)

**For Contributors**: See [CLAUDE.md](CLAUDE.md) for project guidance and [About/](About/) for development history.

---

## About the Project

**Development Model**: AI-Human Collaboration

This toolset is developed through collaboration between Claude (Anthropic's AI assistant) and human direction. The goal is to create high-quality, maintainable tools that serve the Neverwinter Nights modding community.

For the story behind this project, see the wiki:
- [Development Timeline](https://github.com/LordOfMyatar/Radoub/wiki/Development-Timeline) - Day-by-day chronicle
- [On Using Claude](https://github.com/LordOfMyatar/Radoub/wiki/On-Using-Claude) - Personal account of AI collaboration

---

## Community & Feedback

**Wanted**:
- Bug reports and feature requests
- Testing on different platforms
- Accessibility feedback
- Module compatibility testing
- Code review and contributions

**Not Wanted**:
- Generic AI criticism without technical substance
- Demands without context
- Unrealistic expectations for alpha software

Found a bug? Have a feature idea? Open an issue with details.

---

## License

See [LICENSE](LICENSE) for details.

---

## Acknowledgments

- BioWare/Beamdog for Neverwinter Nights and Aurora Engine
- Original Aurora Toolset developers
- NWN community for 20+ years of modding excellence
- [neverwinter.nim](https://github.com/niv/neverwinter.nim) (MIT) - Primary reference for Aurora file format parsing
- [WeCantSpell.Hunspell](https://github.com/aarondandy/WeCantSpell.Hunspell) (MIT) - .NET Hunspell port for spell-checking
- [Hunspell](https://hunspell.github.io/) - Spell-checking engine used by LibreOffice, Firefox, Chrome
- [LibreOffice Dictionaries](https://cgit.freedesktop.org/libreoffice/dictionaries/) - English dictionary files (BSD/Public Domain)
- All contributors and testers

---

*"Not all treasure is silver and gold, mate. Sometimes it's a readable dialog editor."*
