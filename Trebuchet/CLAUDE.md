# CLAUDE.md - Trebuchet

Project guidance for Claude Code sessions working with the Trebuchet (Radoub Launcher) tool.

---

## Tool Overview

**Trebuchet** is the central hub for the Radoub toolset. It provides:

- **Tool Launcher** - Discover and launch Parley, Manifest, Quartermaster, Fence with recent file support
- **Module Editor** - Edit module.ifo properties (metadata, scripts, HAKs, variables, entry points)
- **Module Management** - Unpack, edit, and build/pack modules
- **Game Launcher** - Launch NWN:EE with selected module for testing
- **Global Settings** - Game paths, TLK configuration, theme/font preferences
- **Theme Editor** - Create and customize themes

---

## Architecture

```
Trebuchet/
├── CHANGELOG.md
├── CLAUDE.md (this file)
├── NonPublic/           # Research docs, not published
├── Trebuchet/
│   ├── Trebuchet.csproj
│   ├── App.axaml(.cs)
│   ├── Program.cs
│   ├── Services/
│   │   ├── CommandLineService.cs      # CLI args parsing (--help, --safemode, --module)
│   │   ├── GameLauncherService.cs     # Launch NWN:EE with module
│   │   ├── SettingsService.cs         # Tool-specific settings (window, recent modules)
│   │   ├── ToolLauncherService.cs     # Discover and launch Radoub tools
│   │   ├── ToolRecentFilesService.cs  # Read tool MRU from settings
│   │   └── UpdateService.cs           # Check for updates
│   ├── ViewModels/
│   │   ├── MainWindowViewModel.cs
│   │   ├── ModuleEditorViewModel.cs   # IFO editing logic
│   │   ├── SettingsWindowViewModel.cs
│   │   └── ThemeEditorViewModel.cs
│   ├── Views/
│   │   ├── MainWindow.axaml(.cs)
│   │   ├── ModuleEditorWindow.axaml(.cs)  # Module.ifo editor
│   │   ├── SettingsWindow.axaml(.cs)
│   │   └── ThemeEditorWindow.axaml(.cs)
│   ├── Themes/
│   │   ├── light.json
│   │   └── dark.json
│   └── Assets/
└── Trebuchet.Tests/  (TODO)
```

### Key Design Decisions

- **Folder**: `Trebuchet/` (user-facing name)
- **Namespace**: `RadoubLauncher` (internal, like Parley uses `ConversationEditor`)
- **Assembly**: `Trebuchet`

### Dependencies

- `Radoub.Formats` - GFF/ERF/IFO parsers, shared settings
- `Radoub.UI` - Theme manager, resource browsers, shared UI components

---

## Current Features (v1.5.0-alpha)

### Tool Launcher
- Discover installed Radoub tools (Parley, Manifest, Quartermaster, Fence)
- Launch tools with optional file argument
- Recent files dropdown per tool (reads from tool's settings.json)

### Module Editor
- **Metadata Tab**: Module name, description, tag, custom TLK
- **Version Tab**: Minimum game version, expansions, XP scale, DefaultBic, StartMovie
- **HAK Files Tab**: Ordered list with add/remove/reorder
- **Time Tab**: Dawn/dusk hours, minutes per hour, start date/time
- **Entry Point Tab**: Starting area dropdown, X/Y/Z coordinates
- **Scripts Tab**: All 16 standard module event scripts
- **NWN:EE Scripts Tab**: 6 extended event scripts (OnModuleStart, OnPlayerChat, etc.)
- **Variables Tab**: Add/edit/remove module-level local variables

### Module Management
- **Unpack**: Extract .mod to working directory for editing
- **Build**: Pack working directory back to .mod with automatic backup
- **Version Validation**: Warn if NWN:EE features target older minimum version

### Game Launcher
- **Test Module**: Launch with `+TestNewModule` (auto-selects first character)
- **Load Module**: Launch with `+LoadNewModule` (shows character select)

### Settings & Themes
- Game installation path configuration
- TLK language/gender selection
- Theme selection (light, dark, custom)
- Font size/family preferences
- Theme Editor for creating custom themes

---

## Development Workflow

### Building

```bash
# Build Trebuchet only
dotnet build Trebuchet/Trebuchet/Trebuchet.csproj

# Build all tools (includes Trebuchet)
dotnet build Radoub.sln

# Run
dotnet run --project Trebuchet/Trebuchet/Trebuchet.csproj
```

### Command Line Arguments

```bash
Trebuchet --help           # Show help
Trebuchet --safemode       # Reset theme/fonts to defaults
Trebuchet --module "path"  # Open with specific module
```

---

## Services

### ToolLauncherService

Discovers and launches Radoub tools:

1. Check `RadoubSettings.Instance.[ToolName]Path` (set when tools run)
2. Check same directory as Trebuchet
3. Check common installation paths

```csharp
var launcher = ToolLauncherService.Instance;
var tools = launcher.Tools;  // All known tools with availability status
launcher.LaunchTool("Parley");  // Launch by name
launcher.LaunchTool(tool, "--file myfile.dlg");  // Launch with args
```

### GameLauncherService

Launches NWN:EE with module for testing:

```csharp
var launcher = new GameLauncherService();
await launcher.LaunchGameWithModule(modulePath, GameLaunchMode.Test);  // +TestNewModule
await launcher.LaunchGameWithModule(modulePath, GameLaunchMode.Load);  // +LoadNewModule
```

### SettingsService

Tool-specific settings stored at `~/Radoub/Trebuchet/TrebuchetSettings.json`:

- Window position/size
- Recent modules list
- Theme and font preferences

### RadoubSettings (Shared)

Global settings at `~/Radoub/RadoubSettings.json`:

- Game installation paths
- TLK language/gender
- Tool paths (auto-registered)
- Current module path

---

## Module Editor Architecture

The Module Editor uses IFO parsing from `Radoub.Formats`:

- **IfoFile**: Model class with typed accessors for all IFO fields
- **IfoReader**: Parse module.ifo GFF to IfoFile
- **IfoWriter**: Write IfoFile back to GFF format
- **ErfWriter**: Update .mod files with backup support

### Working Directory Pattern

```
modules/
├── mymodule.mod           # Packed module (read-only in editor)
└── mymodule/              # Unpacked working directory (editable)
    ├── module.ifo
    ├── area001.are
    ├── area001.git
    └── ...
```

When a working directory exists alongside the .mod file, Trebuchet loads from there (editable mode). Otherwise, it loads from the packed .mod (read-only).

---

## Theme System

Uses `Radoub.UI.ThemeManager` for consistent theming across tools:

- Theme files in `Themes/` folder (light, dark, custom)
- Theme ID format: `org.radoub.theme.{name}` (universal IDs for shared themes)
- On startup, Trebuchet copies bundled themes to `~/Radoub/Themes/` so other tools can access them
- Accessibility: Font size scaling, high contrast support

---

## Commit Standards

Use `[Trebuchet]` prefix for Trebuchet-specific work:

```
[Trebuchet] feat: Add NWScript compiler integration (#1116)
[Trebuchet] fix: Handle missing module.ifo gracefully
```

Use `[Radoub]` for changes to shared infrastructure (Radoub.Formats, Radoub.UI).

---

## Testing Requirements

Before committing:

1. Build succeeds: `dotnet build Trebuchet/Trebuchet/Trebuchet.csproj`
2. App launches without errors
3. Tool cards display correctly
4. Tools launch when clicked (if installed)
5. Module Editor opens and saves without corruption

### Manual Test Checklist

- [ ] Open packed module - shows read-only status
- [ ] Unpack module - creates working directory
- [ ] Edit IFO fields - save succeeds
- [ ] Build module - creates .mod with backup
- [ ] Launch game with module - NWN:EE starts correctly
- [ ] Launch tools from cards - tools open

---

## Upcoming Features

### NWScript Compiler Integration (#1116)
- Bundle neverwinter.nim's `nwn_script_comp.exe`
- Compile .nss files before packing
- Checkbox to enable/disable compilation (for large modules)
- Timestamp comparison: prompt if .nss newer than .ncs

---

## Resources

- [Trebuchet CHANGELOG](CHANGELOG.md)
- [RadoubSettings.cs](../Radoub.Formats/Radoub.Formats/Settings/RadoubSettings.cs)
- [ThemeManager.cs](../Radoub.UI/Radoub.UI/Services/ThemeManager.cs)
- [IfoFile.cs](../Radoub.Formats/Radoub.Formats/Aurora/Ifo/IfoFile.cs)
- [neverwinter.nim](https://github.com/niv/neverwinter.nim) - Reference implementation
