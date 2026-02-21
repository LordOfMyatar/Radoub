## What's New

### Highlights

- **Introducing Trebuchet** — Radoub's launcher and module management hub, now included in release builds for the first time
- **Trebuchet**: NWScript Compiler Integration — compile scripts directly from Trebuchet with stale detection and build logs (#1116)
- **Trebuchet**: Module Management — unpack, edit IFO fields, and rebuild .mod files with GUI (#1095)
- **Trebuchet**: Faction Editor — visual faction relationship management embedded as workspace tab (#950)
- **Fence**: StoreBrowserPanel — collapsible left panel for browsing .utm files from current module (#1144)

### Trebuchet

Major release with workspace redesign: Module Editor, Faction Editor, and Build & Test are now embedded workspace tabs. NWScript compiler integration enables compile-test cycles without leaving Trebuchet. Build & Test polish adds compile-on-build, open-in-editor for failed scripts, and save-before-test options.

### Parley (Beta)

Massive refactoring epic completed (22 sprints): full dependency injection, service interfaces, MainWindow decomposition, and DLG parser migration to shared library. New DialogBrowserPanel, custom file browsers, and 126 hardcoded theme values eliminated.

### Manifest (Beta)

Token support with Ctrl+T insertion, New Journal command, auto-load from Trebuchet's module, and custom JournalBrowserWindow.

### Fence

TLK language toggle, delete store files from module, StoreBrowserPanel, full editor functionality (search, filters, scripts, variables), and performance fixes (16x faster cache loading).

### Radoub (Shared)

Cross-tool UI consistency sprint, NBGV versioning migration, unified CI/CD, Avalonia 11.3.12, and 5-phase tech debt cleanup across all tools.

## Tool Versions

| Tool | Version | Maturity |
|------|---------|----------|
| Parley | v0.2.2-alpha | Beta |
| Manifest | v0.16.1-alpha | Beta |
| Fence | v0.2.2-alpha | Alpha |
| Trebuchet | v1.19.4-alpha | Alpha |

**Note**: Quartermaster is not included in this release (active development).

## Downloads

- **Bundled**: Includes .NET runtime — works standalone
- **Unbundled**: Smaller download, requires .NET 9.0 installed

See individual tool CHANGELOGs for complete details.
