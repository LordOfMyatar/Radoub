## What's New

### Highlights

**Marlinspike Search & Replace** — Module-wide find & replace across all 17 GFF file types, now available as a Trebuchet workspace tab and via Ctrl+F/H in every tool.

**ERF Import Wizard** — Extract resources from .erf/.hak/.mod/.sav archives into your module with search, type filtering, and conflict detection.

### New Tools in This Release

**Quartermaster** (Alpha) — NPC and character editor for .utc/.bic files. Make backups before using — this still needs a lot of work, but constructive feedback is welcome.

**Relique** (Alpha) — Item blueprint editor for .uti files.

### Parley

Drag-and-drop reorder/reparent in flowchart view. Improved focus handling and tree refresh coordination.

### Bug Fixes

- **Quartermaster**: Fix saving throws not updated after level-up
- **Quartermaster**: Fix Rogue short swords flagged as requiring Martial proficiency
- **Quartermaster**: Fix crash loading c_kocrachn model (unbounded MDL recursion)
- **Fence**: Fix loading UTM setting dirty flag without changes
- **Parley**: Fix NPC-to-NPC links visual bug
- **Parley**: Fix --mod flag not working for module search context
- **Marlinspike**: Fix VarTable replace silently failing

### Radoub (Shared)

- File locking prevents data corruption when the same file is open in multiple tools
- Service consolidation across all tools (~1,600 lines removed)
- New GFF parsers: UTP (Placeable), UTD (Door), ARE (Area)

## Tool Versions

| Tool | Version | Maturity |
|------|---------|----------|
| Parley | 0.2.37-alpha | Beta |
| Manifest | 0.16.25-alpha | Beta |
| Quartermaster | 0.2.93-alpha | Alpha (NEW) |
| Fence | 0.2.40-alpha | Alpha |
| Trebuchet | 1.19.34-alpha | Alpha |
| Relique | 0.1.23-alpha | Alpha (NEW) |

## Downloads

Includes .NET runtime — no separate install needed.

See individual tool CHANGELOGs for complete details.
