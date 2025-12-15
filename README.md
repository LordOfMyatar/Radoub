# Radoub

**Neverwinter Nights Modding Toolset**

*Bringing dead workflows back to life.*

A collection of cross-platform tools for creating and editing Neverwinter Nights modules. Built with modern technology while respecting the Aurora Engine's legacy.

> *Radoub* (French, "careening") - The process of repairing a ship's hull. Like undead pirates maintaining their vessel for eternal voyages, these tools help keep your NWN modules shipshape for adventures to come.

---

## Tools

### Parley - Dialog Editor

Cross-platform dialog editor for Neverwinter Nights DLG files with modern UI, plugin system, and Aurora Engine compatibility.

**Status**: Alpha (v0.1.66)
**Platforms**: Windows, Linux, macOS
**Key Features**:
- Dialog tree editing with undo/redo
- Native flowchart view (handles 100+ depth trees)
- Sound and script browsers
- Python plugin system with gRPC
- Color-blind accessible themes
- Dark mode and font scaling

**Learn more**: [Parley/README.md](Parley/README.md) | [Wiki](https://github.com/LordOfMyatar/Radoub/wiki)

### Manifest - Journal Editor

Cross-platform journal editor for Neverwinter Nights JRL files.

**Status**: Alpha (v0.7.0)
**Platforms**: Windows, Linux, macOS
**Key Features**:
- Quest category and entry editing
- TLK string resolution with multi-language support
- Theme support (dark/light, accessibility themes)
- Auto-detect game installation paths

**Learn more**: [Manifest Wiki](https://github.com/LordOfMyatar/Radoub/wiki/Manifest)

### Radoub.Formats - Shared Library

Aurora Engine file format parsers used by Parley and Manifest.

**Supported Formats**: GFF, KEY, BIF, TLK, 2DA, ERF

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

This is a multi-tool repository. Each tool maintains its own:
- Development documentation
- Build instructions
- Testing protocols
- Issue tracking

**For Contributors**: See [CLAUDE.md](CLAUDE.md) for project guidance and [About/](About/) for development history.

---

## About the Project

**Development Model**: AI-Human Collaboration

This toolset is developed through collaboration between Claude (Anthropic's AI assistant) and human direction. The goal is to create high-quality, maintainable tools that serve the Neverwinter Nights modding community.

- **CLAUDE_DEVELOPMENT_TIMELINE.md**: Day-by-day development chronicle
- **ON_USING_CLAUDE.md**: Personal account of the collaboration experience

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
- All contributors and testers

---

*"Not all treasure is silver and gold, mate. Sometimes it's a working dialog editor."*
